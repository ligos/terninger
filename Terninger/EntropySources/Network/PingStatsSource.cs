using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.NetworkInformation;
using System.IO;
using System.Diagnostics;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.LibLog;

namespace MurrayGrant.Terninger.EntropySources.Network
{
    /// <summary>
    /// An entropy source which uses ping timings as input.
    /// </summary>
    public class PingStatsSource : EntropySourceWithPeriod
    {
        public override string Name => typeof(PingStatsSource).FullName;

        private IRandomNumberGenerator _Rng;

        private readonly int _ServersPerSample;                   // Runs this many pings in parallel to different servers. 6 by default.
        public int ServersPerSample => _ServersPerSample;
        private readonly int _PingsPerSample;                    // Runs this many pings in sequence to the same server. 6 by default.
        public int PingsPerSample => _PingsPerSample;
        public int TotalPingsPerSample => _ServersPerSample * _PingsPerSample;

        private static readonly int _Timeout = 5000;        // 5 second timeout by default. TODO: make it configurable.

        private List<IPAddress> _Servers;
        public int ServerCount => _Servers.Count;
        private int _NextServer;

        private bool _UseRandomSourceForUnitTest;

        public PingStatsSource() : this(TimeSpan.FromMinutes(5.0), null, 6, 8) { }
        public PingStatsSource(TimeSpan periodNormalPriority) : this(periodNormalPriority, null, 6, 8) { }
        public PingStatsSource(TimeSpan periodNormalPriority, IEnumerable<IPAddress> servers) : this(periodNormalPriority, servers, 6, 8) { }
        public PingStatsSource(TimeSpan periodNormalPriority, IEnumerable<IPAddress> servers, int pingsPerSample, int serversPerSample) : this(periodNormalPriority, TimeSpan.FromSeconds(15), new TimeSpan(periodNormalPriority.Ticks * 5), servers, pingsPerSample, serversPerSample, null) { }
        public PingStatsSource(TimeSpan periodNormalPriority, TimeSpan periodHighPriority, TimeSpan periodLowPriority, IEnumerable<IPAddress> servers, int pingsPerSample, int serversPerSample, IRandomNumberGenerator rng)
            : base (periodNormalPriority, periodHighPriority, periodLowPriority)
        {
            if (pingsPerSample <= 0)
                throw new ArgumentOutOfRangeException(nameof(pingsPerSample), pingsPerSample, "Pings per sample must be at least one.");
            if (serversPerSample <= 0)
                throw new ArgumentOutOfRangeException(nameof(serversPerSample), serversPerSample, "Servers per sample must be at least one.");

            this._Servers = (servers ?? LoadInternalServerList()).ToList();
            if (_Servers.Count <= 0)
                throw new ArgumentOutOfRangeException(nameof(servers), servers, "At least one server must be provided.");

            this._ServersPerSample = serversPerSample > 0 ? serversPerSample : 6;
            this._ServersPerSample = Math.Min(_ServersPerSample, _Servers.Count);
            this._PingsPerSample = pingsPerSample > 0 ? pingsPerSample : 6;
            this._Rng = rng ?? StandardRandomWrapperGenerator.StockRandom();
            _Servers.ShuffleInPlace(_Rng);
        }
        internal PingStatsSource(bool useDiskSourceForUnitTests)
            : this(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, null, 6, 8, null)
        {
            this._UseRandomSourceForUnitTest = useDiskSourceForUnitTests;
        }


        public override void Dispose()
        {
            var asIDisposable = _Rng as IDisposable;
            if (asIDisposable != null)
            {
                asIDisposable.Dispose();
                _Rng = null;
            }
        }

        private async Task<List<IPAddress>> LoadInternalServerListAsync()
        {
            Log.Debug("Loading internal server list...");
            var servers = new List<IPAddress>();
            using (var stream = typeof(PingStatsSource).Assembly.GetManifestResourceStream(typeof(PingStatsSource), "PingServerList.txt"))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                int lineNum = 0;
                while (!reader.EndOfStream)
                {
                    var l = await reader.ReadLineAsync();
                    lineNum = lineNum + 1;

                    if (String.IsNullOrWhiteSpace(l))
                        continue;
                    if (l.StartsWith("#"))
                        continue;
                    if (IPAddress.TryParse(l.Trim(), out var ip))
                    {
                        Log.Trace("Read IP {0} on line {1}", ip, lineNum);
                        servers.Add(ip);
                    }
                    else
                        // Couldn't parse IP.
                        Log.Warn("Unable to parse IP for {0}: {1} (line {2:N0})", nameof(PingStatsSource), l, lineNum);
                }
            }
            Log.Debug("Loaded {0:N0} server IP addresses from internal list.", servers.Count);
            return servers;
        }
        private List<IPAddress> LoadInternalServerList()
        {
            return LoadInternalServerListAsync().GetAwaiter().GetResult();
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            // Do x pings to y servers in parallel.
            // Time each of them, use the high precision part as the result.
            // TODO: perhaps do real DNS queries rather than ICMP pings, as many have disabled ping. Note that this will need a 3rd party library.

            // TODO: check to see if there is a network available before trying this.

            // Select the servers we will ping.
            var serversToSample = new List<PingAndStopwatch>(_ServersPerSample);
            for (int i = 0; i < _ServersPerSample; i++)
            {
                if (_NextServer >= _Servers.Count)
                    _NextServer = 0;
                serversToSample.Add(new PingAndStopwatch(_Servers[_NextServer]));
                _NextServer = _NextServer + 1;
            }

            // Now ping the servers and time how long it takes.
            var result = new List<byte>((_ServersPerSample + _PingsPerSample) * sizeof(ushort));
            for (int c = 0; c < _PingsPerSample; c++)
            {
                if (!_UseRandomSourceForUnitTest)
                {
                    await Task.WhenAll(serversToSample.Select(x => x.ResetAndRun()).ToArray());

                    foreach (var s in serversToSample.Where(x => x.Stopwatch.ElapsedMilliseconds > 0 && x.Stopwatch.ElapsedMilliseconds < _Timeout))
                    {
                        var timingBytes = BitConverter.GetBytes(unchecked((ushort)s.Stopwatch.Elapsed.Ticks));
                        result.Add(timingBytes[0]);
                        result.Add(timingBytes[1]);
                    }
                }
                else
                {
                    // Unit tests simply read random bytes.
                    result.AddRange(_Rng.GetRandomBytes(2));
                }
            }

            return result.ToArray();
        }

        private class PingAndStopwatch
        {
            public PingAndStopwatch(IPAddress ip)
            {
                this.IP = ip;
            }
            public readonly IPAddress IP;
            public readonly Ping Ping = new Ping();
            public readonly Stopwatch Stopwatch = new Stopwatch();

            public Task ResetAndRun()
            {
                // TODO: exception handling??
                Stopwatch.Restart();
                return Ping.SendPingAsync(IP, PingStatsSource._Timeout)
                            .ContinueWith((x) => {
                                Stopwatch.Stop();
                                return x;
                            }, TaskContinuationOptions.ExecuteSynchronously);
            }
           
        }
    }
}
