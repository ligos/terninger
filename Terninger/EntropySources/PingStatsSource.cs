using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.NetworkInformation;
using System.IO;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.Helpers;
using System.Diagnostics;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// An entropy source which uses ping timings as input.
    /// </summary>
    [NetworkSource]
    public class PingStatsSource : IEntropySource
    {
        public string Name => typeof(PingStatsSource).FullName;

        private IRandomNumberGenerator _Rng;
        private DateTime _NextSampleTimestamp;

        private int _ServersPerSample = 6;                   // Runs this many pings in parallel to different servers. 6 by default.
        public int ServersPerSample => _ServersPerSample;
        private int _PingsPerSample = 6;                    // Runs this many pings in sequence to the same server. 6 by default.
        public int PingsPerSample => _PingsPerSample;
        public int TotalPingsPerSample => _ServersPerSample * _PingsPerSample;

        private double _PeriodMinutes = 15.0;                // 15 minutes between runs, by default.
        public double PeriodMinutes => _PeriodMinutes;

        private static readonly int _Timeout = 5000;        // 5 second timeout by default. TODO: make it configurable.

        private List<IPAddress> _Servers;
        public int ServerCount => _Servers.Count;
        private int _NextServer;

        private bool _UseRandomSourceForUnitTest;

        public PingStatsSource() : this(15.0, null, 6, 6, null) { }
        public PingStatsSource(double periodMinutes) : this(periodMinutes, null, 6, 6, null) { }
        public PingStatsSource(double periodMinutes, IEnumerable<IPAddress> servers) : this(periodMinutes, servers, 6, 6, null) { }
        public PingStatsSource(double periodMinutes, IEnumerable<IPAddress> servers, int pingsPerSample, int serversPerSample) : this(periodMinutes, servers, 6, 6, null) { }
        public PingStatsSource(double periodMinutes, IEnumerable<IPAddress> servers, int pingsPerSample, int serversPerSample, IRandomNumberGenerator rng)
        {
            this._PeriodMinutes = periodMinutes >= 0.0 ? periodMinutes : 15.0;
            this._Servers = (servers ?? LoadInternalServerList()).ToList();
            this._ServersPerSample = serversPerSample > 0 ? serversPerSample : 6;
            this._ServersPerSample = Math.Min(_ServersPerSample, _Servers.Count);
            this._PingsPerSample = pingsPerSample > 0 ? pingsPerSample : 6;
            this._Rng = rng ?? StandardRandomWrapperGenerator.StockRandom();
            _Servers.ShuffleInPlace(_Rng);
        }


        public void Dispose()
        {
            var asIDisposable = _Rng as IDisposable;
            if (asIDisposable != null)
            {
                asIDisposable.Dispose();
                _Rng = null;
            }
        }

        public async Task<EntropySourceInitialisationResult> Initialise(IEntropySourceConfig config, Func<IRandomNumberGenerator> prngFactory)
        {
            if (config.IsTruthy("PingStatsSource.Enabled") == false)
                return EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.DisabledByConfig, "PingStatsSource has been disabled in entropy source configuration.");

            config.TryParseAndSetDouble("PingStatsSource.PeriodMinutes", ref _PeriodMinutes);
            if (_PeriodMinutes < 0.0 || _PeriodMinutes > 1440.0)
                return EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, new ArgumentOutOfRangeException("PingStatsSource.PeriodMinutes", _PeriodMinutes, "Config item PingStatsSource.PeriodMinutes must be between 0 and 1440 (1 day)"));

            config.TryParseAndSetInt32("PingStatsSource.ServersPerSample", ref _ServersPerSample);
            if (_ServersPerSample <= 0 || _ServersPerSample > 1000)
                return EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, new ArgumentOutOfRangeException("PingStatsSource.ServersPerSample", _ServersPerSample, "Config item PingStatsSource.ServersPerSample must be between 1 and 1000"));

            config.TryParseAndSetInt32("PingStatsSource.PingsPerSample", ref _PingsPerSample);
            if (_PingsPerSample <= 0 || _PingsPerSample > 1000)
                return EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, new ArgumentOutOfRangeException("PingStatsSource.PingsPerSample", _ServersPerSample, "Config item PingStatsSource.PingsPerSample must be between 1 and 1000"));

            // Magic unit test source of data (random number generator).
            _UseRandomSourceForUnitTest = config.IsTruthy("PingStatsSource.UseRandomSourceForUnitTests") == true;

            // Load the list of servers.
            // TODO: allow this to be configured from any text file.
            var servers = await LoadInternalServerListAsync();
            if (servers.Count == 0)
                return EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, "No IP Addresses were read from supplied server list file: PingServerList.txt");
            if (servers.Count < _ServersPerSample)
                return EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, $"PingStatsSource.ServersPerSample is {_ServersPerSample}, but only {servers.Count} servers were read from supplied server list file: PingServerList.txt");

            _Rng = prngFactory();
            _Servers = servers;
            _Servers.ShuffleInPlace(_Rng);      // Shuffle the order of servers so it is not entirely predictible.
            _NextSampleTimestamp = DateTime.UtcNow;

            return EntropySourceInitialisationResult.Successful();
        }

        private async Task<List<IPAddress>> LoadInternalServerListAsync()
        {
            var servers = new List<IPAddress>();
            using (var stream = typeof(PingStatsSource).Assembly.GetManifestResourceStream(typeof(PingStatsSource), "PingServerList.txt"))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                while (!reader.EndOfStream)
                {
                    var l = await reader.ReadLineAsync();
                    if (String.IsNullOrWhiteSpace(l))
                        continue;
                    if (l.StartsWith("#"))
                        continue;
                    if (IPAddress.TryParse(l.Trim(), out var ip))
                        servers.Add(ip);
                    // TODO: log if we can't parse IP addresses.
                }

            }
            return servers;
        }
        private List<IPAddress> LoadInternalServerList()
        {
            return LoadInternalServerListAsync().GetAwaiter().GetResult();
        }
        public async Task<byte[]> GetEntropyAsync()
        {
            // Do x pings to y servers in parallel.
            // Time each of them, use the high precision part as the result.
            // TODO: perhaps do real DNS queries rather than ICMP pings, as many have disabled ping. Note that this will need a 3rd party library.
            // TODO: check to see if there is a network available before trying this.

            // Return nothing if the next polling interval hasn't been reached.
            if (DateTime.UtcNow < this._NextSampleTimestamp)
                return null;

            // Nothing to select from.
            if (_ServersPerSample <= 0 || _Servers.Count == 0 || _PingsPerSample <= 0)
                return null;

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
            var result = new List<byte>((_ServersPerSample + _PingsPerSample) * sizeof(Int16));
            for (int c = 0; c < _PingsPerSample; c++)
            {
                if (!_UseRandomSourceForUnitTest)
                {
                    await Task.WhenAll(serversToSample.Select(x => x.ResetAndRun()).ToArray());

                    foreach (var s in serversToSample.Where(x => x.Stopwatch.ElapsedMilliseconds < _Timeout))
                    {
                        var timingBytes = BitConverter.GetBytes(unchecked((short)s.Stopwatch.Elapsed.Ticks));
                        result.Add(timingBytes[0]);
                        result.Add(timingBytes[1]);
                    }
                }
                else
                {
                    result.AddRange(_Rng.GetRandomBytes(2));
                }
            }

            this._NextSampleTimestamp = DateTime.UtcNow.AddMinutes(_PeriodMinutes);
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
                            }
                            , TaskContinuationOptions.ExecuteSynchronously);
            }
           
        }
    }
}
