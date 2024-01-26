using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.NetworkInformation;
using System.IO;
using System.Diagnostics;

using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.LibLog;
using MurrayGrant.Terninger.PersistentState;

namespace MurrayGrant.Terninger.EntropySources.Network
{
    /// <summary>
    /// An entropy source which uses ping timings as input.
    /// </summary>
    [AsyncHint(IsAsync.Always)]
    public class PingStatsSource : EntropySourceWithPeriod, IPersistentStateSource
    {
        public override string Name { get; set; }

        private IRandomNumberGenerator _Rng;

        private int _ServersPerSample;                   // Runs this many pings in parallel to different servers.
        public int ServersPerSample => _ServersPerSample;
        private readonly int _PingsPerSample;                    // Runs this many pings in sequence to the same server.
        public int PingsPerSample => _PingsPerSample;
        public int TotalPingsPerSample => _ServersPerSample * _PingsPerSample;

        private readonly TimeSpan _Timeout;

        public string SourcePath { get; private set; }
        private List<IPAddress> _Servers = new List<IPAddress>();
        public int ServerCount => _Servers.Count;

        private int _NextServer;
        private bool _ServersInitialised;

        private bool _UseRandomSourceForUnitTest;

        public PingStatsSource(Configuration config)
            : this(
                  sourcePath:           config?.ServerFilePath,
                  pingsPerSample:       config?.PingsPerSample       ?? Configuration.Default.PingsPerSample,
                  serversPerSample:     config?.ServersPerSample     ?? Configuration.Default.ServersPerSample,
                  timeout:              config?.Timeout              ?? Configuration.Default.Timeout,
                  periodNormalPriority: config?.PeriodNormalPriority ?? Configuration.Default.PeriodNormalPriority,
                  periodHighPriority:   config?.PeriodHighPriority   ?? Configuration.Default.PeriodHighPriority,
                  periodLowPriority:    config?.PeriodLowPriority    ?? Configuration.Default.PeriodLowPriority
            )
        { }
        public PingStatsSource(TimeSpan? periodNormalPriority = null, TimeSpan? periodHighPriority = null, TimeSpan? periodLowPriority = null, string sourcePath = null, IEnumerable<IPAddress> servers = null, int? pingsPerSample = null, int? serversPerSample = null, IRandomNumberGenerator rng = null, TimeSpan? timeout = null)
            : base (periodNormalPriority.GetValueOrDefault(Configuration.Default.PeriodNormalPriority),
                  periodHighPriority.GetValueOrDefault(Configuration.Default.PeriodHighPriority),
                  periodLowPriority.GetValueOrDefault(Configuration.Default.PeriodLowPriority))
        {
            this._ServersPerSample = serversPerSample.GetValueOrDefault(Configuration.Default.ServersPerSample);
            this._PingsPerSample = pingsPerSample.GetValueOrDefault(Configuration.Default.PingsPerSample);
            if (_ServersPerSample <= 0)
                throw new ArgumentOutOfRangeException(nameof(pingsPerSample), pingsPerSample, "Pings per sample must be at least one.");
            if (_PingsPerSample <= 0)
                throw new ArgumentOutOfRangeException(nameof(serversPerSample), serversPerSample, "Servers per sample must be at least one.");
            this.SourcePath = sourcePath;
            if (servers != null)
                this._Servers.AddRange(servers);
            this._Rng = rng ?? StandardRandomWrapperGenerator.StockRandom();
            this._Timeout = timeout.GetValueOrDefault(Configuration.Default.Timeout);
        }
        internal PingStatsSource(bool useDiskSourceForUnitTests)
            : this(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, null, null, 6, 8, null)
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

        public static Task<IReadOnlyCollection<IPAddress>> LoadInternalServerListAsync()
        {
            var log = LibLog.LogProvider.For<PingStatsSource>();
            log.Debug("Loading internal server list...");
            using (var stream = typeof(PingStatsSource).Assembly.GetManifestResourceStream(typeof(PingStatsSource), "PingServerList.txt"))
            {
                return LoadServerListAsync(stream);
            }
        }
        public static Task<IReadOnlyCollection<IPAddress>> LoadServerListAsync(string path)
        {
            var log = LibLog.LogProvider.For<PingStatsSource>();
            log.Debug("Loading source server list from '{0}'...", path);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 32 * 1024, FileOptions.SequentialScan))
            {
                return LoadServerListAsync(stream);
            }
        }
        public static async Task<IReadOnlyCollection<IPAddress>> LoadServerListAsync(Stream stream)
        {
            var log = LibLog.LogProvider.For<PingStatsSource>();
            
            var servers = new List<IPAddress>();
            using (var reader = new StreamReader(stream, Encoding.UTF8, false, 32 * 1024, true))
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
                        log.Trace("Read IP {0} on line {1}", ip, lineNum);
                        servers.Add(ip);
                    }
                    else
                        // Couldn't parse IP.
                        log.Warn("Unable to parse IP for {0}: {1} (line {2:N0})", nameof(PingStatsSource), l, lineNum);
                }
            }
            log.Debug("Loaded {0:N0} server IP addresses.", servers.Count);
            return servers;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            if (!_ServersInitialised && !_UseRandomSourceForUnitTest)
            {
                Log.Debug("Initialising server list.");
                try
                {
                    if (!String.IsNullOrEmpty(SourcePath))
                        _Servers.AddRange(await LoadServerListAsync(SourcePath));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unable to open Server Source File '{0}'.", SourcePath);
                }
                if (String.IsNullOrEmpty(SourcePath) && _Servers.Count == 0)
                    _Servers.AddRange(await LoadInternalServerListAsync());
                if (_Servers.Count == 0)
                    Log.Error("No servers are available. This entropy source will be disabled.");

                this._ServersPerSample = Math.Min(_ServersPerSample, _Servers.Count);
                _Servers.ShuffleInPlace(_Rng);
                _ServersInitialised = true;
            }
            if (_Servers.Count == 0)
            {
                return null;
            }

            // Do x pings to y servers in parallel.
            // Time each of them, use the high precision part as the result.
            // TODO: perhaps do real DNS queries rather than ICMP pings, as many have disabled ping. Note that this will need a 3rd party library.

            // TODO: check to see if there is a network available before trying this. Eg: https://stackoverflow.com/a/8345173/117070

            // Select the servers we will ping.
            var serversToSample = new List<PingAndStopwatch>(_ServersPerSample);
            for (int i = 0; i < _ServersPerSample; i++)
            {
                if (_NextServer >= _Servers.Count)
                    _NextServer = 0;
                serversToSample.Add(new PingAndStopwatch(_Servers[_NextServer], _Timeout));
                _NextServer = _NextServer + 1;
            }

            // Now ping the servers and time how long it takes.
            var result = new List<byte>((_ServersPerSample + _PingsPerSample) * sizeof(ushort));
            for (int c = 0; c < _PingsPerSample; c++)
            {
                if (!_UseRandomSourceForUnitTest)
                {
                    await Task.WhenAll(serversToSample.Select(x => x.ResetAndRun()).ToArray());

                    foreach (var s in serversToSample.Where(x => x.Timing.TotalMilliseconds > 0 && x.Timing < _Timeout))
                    {
                        var timingBytes = BitConverter.GetBytes(unchecked((ushort)s.Timing.Ticks));
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

            // Check for IPs where every attempt was a failure: something is likely wrong.
            foreach (var server in serversToSample.Where(x => x.Failures == _PingsPerSample))
                Log.Warn("Every attempt to ping IP {0} failed. Server is likely offline or firewalled.", server.IP);

            return result.ToArray();
        }

        #region IPersistentStateSource

        // TODO: implement properly

        bool IPersistentStateSource.HasUpdates => false;

        void IPersistentStateSource.Initialise(IDictionary<string, NamespacedPersistentItem> state)
        {
            // TODO: implement.
        }

        IEnumerable<NamespacedPersistentItem> IPersistentStateSource.GetCurrentState(PersistentEventType eventType)
        {
            // TODO: implement.
            yield break;
        }

        #endregion

        private class PingAndStopwatch
        {
            private static readonly ILog Log = LibLog.LogProvider.For<PingStatsSource>();

            public PingAndStopwatch(IPAddress ip, TimeSpan timeout)
            {
                this.IP = ip;
                this.Timeout = timeout;
            }
            public readonly IPAddress IP;
            public readonly Ping Ping = new Ping();
            public readonly TimeSpan Timeout;
            public TimeSpan Timing { get; private set; }
            public int Failures { get; private set; }

            public async Task ResetAndRun()
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var result = await Ping.SendPingAsync(IP, (int)Timeout.TotalMilliseconds);
                    sw.Stop();
                    Log.Trace("Ping to '{0}' in {1:N2}ms, result: {2}", IP, sw.Elapsed.TotalMilliseconds, result.Status);
                    if (result.Status == IPStatus.Success)
                        Timing = sw.Elapsed;
                    else
                    {
                        Timing = TimeSpan.Zero;
                        Failures = Failures + 1;
                    }
                }
                catch (Exception ex)
                {
                    Log.WarnException("Exception when trying to ping {0}", ex, IP);
                    sw.Stop();
                    Timing = TimeSpan.Zero;
                    Failures = Failures + 1;
                }
            }
          
        }

        public class Configuration
        {
            public static readonly Configuration Default = new Configuration();

            /// <summary>
            /// Number of servers to ping from the list each sample.
            /// Default: 8. Minimum: 1. Maximum: 100.
            /// </summary>
            public int ServersPerSample { get; set; } = 8;

            /// <summary>
            /// Number of times to ping each server per sample.
            /// Default: 6. Minimum: 1. Maximum: 100.
            /// </summary>
            public int PingsPerSample { get; set; } = 6;

            /// <summary>
            /// Path to file containing server list.
            /// If left blank, an internal list is used.
            /// </summary>
            public string ServerFilePath { get; set; }

            /// <summary>
            /// Timeout to use for ping requests. Default: 5 seconds.
            /// </summary>
            public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

            /// <summary>
            /// Sample period at normal priority. Default: 5 minutes.
            /// </summary>
            public TimeSpan PeriodNormalPriority { get; set; } = TimeSpan.FromMinutes(5);

            /// <summary>
            /// Sample period at high priority. Default: 15 seconds.
            /// </summary>
            public TimeSpan PeriodHighPriority { get; set; } = TimeSpan.FromSeconds(15);

            /// <summary>
            /// Sample period at low priority. Default: 25 minutes.
            /// </summary>
            public TimeSpan PeriodLowPriority { get; set; } = TimeSpan.FromHours(25);
        }
    }
}
