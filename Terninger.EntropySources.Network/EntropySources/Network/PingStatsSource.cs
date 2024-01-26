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
using System.Globalization;

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

        private readonly int _ServersPerSample;                   // Runs this many pings in parallel to different servers.
        public int ServersPerSample => _ServersPerSample;
        private readonly int _PingsPerSample;                    // Runs this many pings in sequence to the same server.
        public int PingsPerSample => _PingsPerSample;
        public int TotalPingsPerSample => _ServersPerSample * _PingsPerSample;

        private readonly TimeSpan _Timeout;

        public string SourcePath { get; private set; }
        private List<PingTarget> _Servers = new List<PingTarget>();
        public int ServerCount => _Servers.Count;

        private readonly bool _EnableServerDiscovery;
        private readonly int _DesiredServerCount;
        private readonly ushort[] _TcpPorts;

        private int _NextServer;

        private bool _UseRandomSourceForUnitTest;

        public PingStatsSource(Configuration config)
            : this(
                  sourcePath:           config?.ServerFilePath,
                  discoverServers:      config?.DiscoverServers      ?? Configuration.Default.DiscoverServers,
                  desiredServerCount:   config?.DesiredServerCount   ?? Configuration.Default.DesiredServerCount,
                  tcpPingPorts:         config?.TcpPingPorts         ?? Configuration.Default.TcpPingPorts,
                  pingsPerSample:       config?.PingsPerSample       ?? Configuration.Default.PingsPerSample,
                  serversPerSample:     config?.ServersPerSample     ?? Configuration.Default.ServersPerSample,
                  timeout:              config?.Timeout              ?? Configuration.Default.Timeout,
                  periodNormalPriority: config?.PeriodNormalPriority ?? Configuration.Default.PeriodNormalPriority,
                  periodHighPriority:   config?.PeriodHighPriority   ?? Configuration.Default.PeriodHighPriority,
                  periodLowPriority:    config?.PeriodLowPriority    ?? Configuration.Default.PeriodLowPriority
            )
        { }
        public PingStatsSource(
            TimeSpan? periodNormalPriority = null, 
            TimeSpan? periodHighPriority = null, 
            TimeSpan? periodLowPriority = null, 
            string sourcePath = null, 
            IEnumerable<PingTarget> servers = null, 
            bool? discoverServers = null,
            int? desiredServerCount = null,
            IEnumerable<ushort> tcpPingPorts = null,
            int? pingsPerSample = null, 
            int? serversPerSample = null, 
            IRandomNumberGenerator rng = null, 
            TimeSpan? timeout = null
        )
            : base (periodNormalPriority.GetValueOrDefault(Configuration.Default.PeriodNormalPriority),
                  periodHighPriority.GetValueOrDefault(Configuration.Default.PeriodHighPriority),
                  periodLowPriority.GetValueOrDefault(Configuration.Default.PeriodLowPriority))
        {
            this._EnableServerDiscovery = discoverServers.GetValueOrDefault(Configuration.Default.DiscoverServers);
            this._DesiredServerCount = desiredServerCount.GetValueOrDefault(Configuration.Default.DesiredServerCount);
            this._TcpPorts = (tcpPingPorts ?? Configuration.Default.TcpPingPorts).ToArray();
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
            : this(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, null, null, false, 0, Enumerable.Empty<ushort>(), 6, 8, null)
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

        public static Task<IReadOnlyCollection<PingTarget>> LoadInternalServerListAsync()
        {
            var log = LibLog.LogProvider.For<PingStatsSource>();
            log.Debug("Loading internal server list...");
            using (var stream = typeof(PingStatsSource).Assembly.GetManifestResourceStream(typeof(PingStatsSource), "PingServerList.txt"))
            {
                return LoadServerListAsync(stream);
            }
        }
        public static Task<IReadOnlyCollection<PingTarget>> LoadServerListAsync(string path)
        {
            var log = LibLog.LogProvider.For<PingStatsSource>();
            log.Debug("Loading source server list from '{0}'...", path);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 32 * 1024, FileOptions.SequentialScan))
            {
                return LoadServerListAsync(stream);
            }
        }
        public static async Task<IReadOnlyCollection<PingTarget>> LoadServerListAsync(Stream stream)
        {
            var log = LibLog.LogProvider.For<PingStatsSource>();

            var servers = new List<PingTarget>();
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

                    if (PingTarget.TryParse(l, out var target))
                    {
                        log.Trace("Read target {0} on line {1}", target, lineNum);
                        servers.Add(target);
                    }
                    else
                        log.Warn("Unable to parse target for {0}: {1} (line {2:N0})", nameof(PingStatsSource), l, lineNum);
                }
            }
            log.Debug("Loaded {0:N0} server IP addresses.", servers.Count);
            return servers;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            if (_Servers.Count == 0 && !_UseRandomSourceForUnitTest)
            {
                // No prior persisted state: load a seed list to get started.
                await InitialiseServersFromSeedSource();
            }
            if (_Servers.Count == 0 && !_EnableServerDiscovery)
            {
                // No servers and no discovery means no entropy! Return early; an error is logged in InitialiseServersFromSeedSource()
                return null;
            }
            if (!IsNetworkAvailable(10_000_000) && !_UseRandomSourceForUnitTest)
            {
                Log.Info("No usable network interface is up yet. Will wait and try again later.");
                return null;
            }

            byte[] result = null;
            IEnumerable<IpAddressTarget> forDiscovery = Enumerable.Empty<IpAddressTarget>();
            IEnumerable<PingTarget> forRemoval = Enumerable.Empty<PingTarget>();
            if (_Servers.Count > 0)
            {
                (result, forDiscovery, forRemoval) = await GatherEntropyFromTargets();
            }

            // Anything which failed all ping attempts will be removed.
            if (forRemoval.Any())
                RemoveTargets(forRemoval, Enumerable.Empty<PingTarget>());

            // Anything which was just an IP address should run discovery to convert into an ICMP / TCP target.
            if (forDiscovery.Any() && !_UseRandomSourceForUnitTest)
            {
                await DiscoverTargets(forDiscovery);
            }

            // Finally, if nothing needed to be discovered from the main ping run, try to discover new targets up until the desired count.
            if (!forDiscovery.Any() && _EnableServerDiscovery && _Servers.Count < _DesiredServerCount && !_UseRandomSourceForUnitTest)
            {
                await DiscoverTargets(_ServersPerSample);
            }

            EnsureCountersAreValid();

            return result;
        }

        private async Task InitialiseServersFromSeedSource()
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
            if (_Servers.Count == 0 && !_EnableServerDiscovery)
                Log.Error("No servers are available and server discovery is disabled. This entropy source will be disabled.");

            RandomNumberExtensions.ShuffleInPlace(_Servers, _Rng);
            EnsureCountersAreValid();
        }

        private async Task<(byte[] entropy, IReadOnlyCollection<IpAddressTarget> forDiscovery, IReadOnlyCollection<PingTarget> forRemoval)> GatherEntropyFromTargets()
        {
            // Do x pings to y servers in parallel.
            // Time each of them, use the high precision part as the result.

            // Select the servers we will ping.
            var countToSample = Math.Min(_ServersPerSample, _Servers.Count);
            var serversToSample = new List<PingAndStopwatch>(countToSample);
            for (int i = 0; i < _ServersPerSample; i++)
            {
                if (_NextServer >= _Servers.Count)
                    _NextServer = 0;
                serversToSample.Add(new PingAndStopwatch(_Servers[_NextServer], _Timeout));
                _NextServer = _NextServer + 1;
            }

            // Now ping the servers and time how long it takes.
            var result = new List<byte>((countToSample + _PingsPerSample) * sizeof(ushort));
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

            // Collect any IP targets, which will be sent to discovery.
            var forDiscovery = serversToSample.Where(x => x.Target is IpAddressTarget).Select(x => x.Target).Cast<IpAddressTarget>().ToList();

            // Collect any other targets which failed every attempt, which will be removed from the server list.
            var forRemoval = serversToSample.Where(x => x.Target is not IpAddressTarget && x.Failures == _PingsPerSample).Select(x => x.Target).ToList();

            return (result.ToArray(), forDiscovery, forRemoval);
        }

        private Task DiscoverTargets(int targetCount)
        {
            var targets = new List<IpAddressTarget>(targetCount);
            var bytes = new byte[4];

            while (targets.Count < targetCount)
            {
                // TODO: IPv6 support
                // IPv4 address space is so full we can pick random bytes and its pretty likely we'll hit something.
                _Rng.FillWithRandomBytes(bytes);

                if (bytes[0] == 0
                    || bytes[0] == 127
                    || (bytes[0] >= 224 && bytes[0] <= 239)
                    || bytes[0] >= 240
                )
                    // 0.x.x.x is reserved for "this" network
                    // 127.x.x.x is localhost and won't give useful timings
                    // 224-239.x.x.x is multicast
                    // 240-255.x.x.x is reserved for future use (probably never)
                    continue;
                
                var ip = new IPAddress(bytes);
                if (_Servers.Any(x => ip.Equals(x.IPAddress)))
                    // Let's not add the same server twice!
                    continue;

                targets.Add(PingTarget.ForIpAddressOnly(ip));
            }

            return DiscoverTargets(targets);
        }

        private async Task DiscoverTargets(IEnumerable<IpAddressTarget> targets)
        {
            // Discovery runs 3 pings for ICMP + all TCP ports configured.
            // If any one of the pings returns OK, the target will be added, and IpAddressTarget removed.
            if (!targets.Any())
                return;

            Log.Debug("Running discovery pings for {0:N0} IP addresses. Before discovery, there are {1:N0} targets.", targets.Count(), _Servers.Count);
            Log.Trace("Running discovery pings for: {0}", String.Join(",", targets.Select(x => x.IPAddress)));

            var allPossibleTargets = targets.Select(x => PingTarget.ForIcmpPing(x.IPAddress)).Cast<PingTarget>();
            foreach (var port in _TcpPorts)
                allPossibleTargets = allPossibleTargets.Concat(targets.Select(x => PingTarget.ForTcpPing(x.IPAddress, port)).Cast<PingTarget>());
            var serversToSample = allPossibleTargets.Select(x => new PingAndStopwatch(x, _Timeout)).ToList();

            for (int c = 0; c < 3; c++)
            {
                await Task.WhenAll(serversToSample.Select(x => x.ResetAndRun()).ToArray());
            }

            var toAdd = serversToSample.Where(x => x.Failures < 3).Select(x => x.Target).ToList();
            AddNewTargets(toAdd);
            RemoveTargets(Enumerable.Empty<PingTarget>(), targets);

            Log.Debug("After discovery, there are {0:N0} targets.", _Servers.Count);
        }

        private void AddNewTargets(IReadOnlyCollection<PingTarget> newTargets)
        {
            foreach (var target in newTargets)
            {
                Log.Trace("Adding discovered target {0} to server list.", target);
                _Servers.Add(target);
            }
           

            if (newTargets.Any())
                // Reshuffle whenever we add something new.
                RandomNumberExtensions.ShuffleInPlace(_Servers, _Rng);
        }

        private void RemoveTargets(IEnumerable<PingTarget> failedTargets, IEnumerable<PingTarget> discoveryTargets)
        {
            foreach (var t in failedTargets)
            {
                if (_Servers.Remove(t))
                    Log.Trace("Removed target {0} after all ping attempts failed.", t);
            }
            foreach (var t in discoveryTargets)
            {
                if (_Servers.Remove(t))
                    Log.Trace("Removed target {0} after discovery was run.", t);
            }
        }

        private void EnsureCountersAreValid()
        {
            if (_NextServer > _Servers.Count)
                _NextServer = 0;
        }

        #region IPersistentStateSource

        // TODO: implement properly

        bool IPersistentStateSource.HasUpdates => true;

        void IPersistentStateSource.Initialise(IDictionary<string, NamespacedPersistentItem> state)
        {
            // TODO: implement.
        }

        IEnumerable<NamespacedPersistentItem> IPersistentStateSource.GetCurrentState(PersistentEventType eventType)
        {
            // TODO: implement.
            yield return NamespacedPersistentItem.CreateText("ServerCount", _Servers.Count.ToString(CultureInfo.InvariantCulture));
        }

        #endregion

        // From https://stackoverflow.com/a/8345173
        private static bool IsNetworkAvailable(long minimumSpeed)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return false;

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // discard because of standard reasons
                if ((ni.OperationalStatus != OperationalStatus.Up) ||
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) ||
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel))
                    continue;

                // this allow to filter modems, serial, etc.
                // I use 10000000 as a minimum speed for most cases
                if (ni.Speed < minimumSpeed)
                    continue;

                // discard virtual cards (virtual box, virtual pc, etc.)
                if ((ni.Description.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (ni.Name.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;

                // discard "Microsoft Loopback Adapter", it will not show as NetworkInterfaceType.Loopback but as Ethernet Card.
                if (ni.Description.Equals("Microsoft Loopback Adapter", StringComparison.OrdinalIgnoreCase))
                    continue;

                return true;
            }
            return false;
        }

        private class PingAndStopwatch
        {
            private static readonly ILog Log = LibLog.LogProvider.For<PingStatsSource>();

            public PingAndStopwatch(PingTarget target, TimeSpan timeout)
            {
                this.Target = target;
                this.Timeout = timeout;
            }
            public readonly PingTarget Target;
            public readonly Ping Ping = new Ping();
            public readonly TimeSpan Timeout;
            public TimeSpan Timing { get; private set; }
            public int Failures { get; private set; }

            public async Task ResetAndRun()
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    PingReply result;
                    if (Target is IcmpTarget icmpTarget)
                        result = await Ping.SendPingAsync(icmpTarget.IPAddress, (int)Timeout.TotalMilliseconds);
                    else if (Target is IpAddressTarget ipTarget)
                        result = await Ping.SendPingAsync(ipTarget.IPAddress, (int)Timeout.TotalMilliseconds);
                    else
                        throw new NotImplementedException("TCP ping not implemented yet");
                    sw.Stop();

                    Log.Trace("Ping to '{0}' in {1:N2}ms, result: {2}", Target, sw.Elapsed.TotalMilliseconds, result.Status);
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
                    Log.WarnException("Exception when trying to ping {0}", ex, Target);
                    sw.Stop();
                    Timing = TimeSpan.Zero;
                    Failures = Failures + 1;
                }
            }
          
        }

        public abstract class PingTarget
        {
            public readonly IPAddress IPAddress;

            public Status State { get; internal set; }
            private readonly static char[] SplitCharacter = new[] { ' ' };

            public static IpAddressTarget ForIpAddressOnly(IPAddress ip)
                => new IpAddressTarget(ip);

            public static IcmpTarget ForIcmpPing(IPAddress ip)
                => new IcmpTarget(ip);
            
            public static TcpTarget ForTcpPing(IPAddress ip, ushort port)
                => new TcpTarget(ip, port);

            public static bool TryParse(string s, out PingTarget result)
            {
                var parts = s.Split(SplitCharacter, StringSplitOptions.RemoveEmptyEntries);
                IPAddress ip;
                if (parts.Length == 1
                    && IPAddress.TryParse(parts[0].Trim(), out ip))
                {
                    result = ForIpAddressOnly(ip);
                    return true;
                }
                else if (parts.Length == 2
                    && IPAddress.TryParse(parts[0].Trim(), out ip)
                    && String.Equals(parts[1], "ICMP", StringComparison.InvariantCultureIgnoreCase))
                {
                    result = ForIcmpPing(ip);
                    return true;
                }
                else if (parts.Length == 2
                    && IPAddress.TryParse(parts[0].Trim(), out ip)
                    && ushort.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
                    && port > 0)
                {
                    result = ForTcpPing(ip, port);
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            public PingTarget(IPAddress ipAddress)
            {
                IPAddress = ipAddress;
            }

            public enum Status
            {
                Untested = 0,
                Working = 1,
                Failure = 2,
            }
        }

        // Just an IP address, no ICMP or port number. Need to test both
        public sealed class IpAddressTarget : PingTarget
        {
            internal IpAddressTarget(IPAddress ip)
                : base(ip)
            { }

            public override string ToString()
                => IPAddress.ToString();
        }

        // IP address as ICMP ping target.
        public sealed class IcmpTarget : PingTarget
        {
            internal IcmpTarget(IPAddress ip)
                : base(ip)
            { }

            public override string ToString()
                => IPAddress.ToString() + ":ICMP";
        }

        // IP address + port number as TCP ping target.
        public sealed class TcpTarget : PingTarget
        {
            public ushort Port { get; }

            internal TcpTarget(IPAddress ip, ushort port)
                : base(ip)
            {
                Port = port;
            }

            public override string ToString()
                => IPAddress.ToString() + ":" + Port.ToString(CultureInfo.InvariantCulture);
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
            /// Path to file containing initial server list.
            /// If left blank, an internal list is used.
            /// Note this is only used as the initial list once, persistent state is used after that.
            /// </summary>
            public string ServerFilePath { get; set; }

            /// <summary>
            /// Automatically discover new servers to ping by randomly scanning the Internet.
            /// Default: true.
            /// </summary>
            public bool DiscoverServers { get; set; } = true;

            /// <summary>
            /// Count of servers to accumulate when discovering servers.
            /// Default: 1024. Minimum: 1. Maximum: 65536.
            /// Each server will be recorded in persistent state.
            /// Note that each endpoint is counted as one server. So 1.1.1.1:80 and 1.1.1.1:443 count as two.
            /// </summary>
            public int DesiredServerCount { get; set; } = 1024;

            /// <summary>
            /// List of ports to try TCP pings.
            /// Default: 21, 22, 53, 80, 161, 443, 8080, 8443
            /// Set to an empty list to disable TCP ping.
            /// </summary>
            public IEnumerable<ushort> TcpPingPorts { get; set; } = new ushort[] { 21, 22, 53, 80, 161, 443, 8080, 8443 };  // Based on https://www.shodan.io/search/facet?query=*&facet=port

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
            /// Sample period at low priority. Default: 1 hour.
            /// </summary>
            public TimeSpan PeriodLowPriority { get; set; } = TimeSpan.FromHours(1);
        }
    }
}
