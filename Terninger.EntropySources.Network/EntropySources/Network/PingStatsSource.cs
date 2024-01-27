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
using System.Net.Sockets;
using System.Threading;

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

        private readonly int _TargetsPerSample;                   // Runs this many pings in parallel to different targets.
        public int TargetsPerSample => _TargetsPerSample;
        private readonly int _PingsPerSample;                    // Runs this many pings in sequence to the same target.
        public int PingsPerSample => _PingsPerSample;
        public int TotalPingsPerSample => _TargetsPerSample * _PingsPerSample;

        private readonly TimeSpan _Timeout;

        public string SourcePath { get; private set; }
        private List<PingTarget> _Targets = new List<PingTarget>();
        public int TargetCount => _Targets.Count;
        private bool _TargetsIsModifiedForPersistentState;

        private readonly bool _EnableTargetDiscovery;
        private readonly int _DesiredTargetCount;
        private readonly ushort[] _TcpPorts;

        private int _NextTarget;

        private bool _UseRandomSourceForUnitTest;

        public PingStatsSource(Configuration config)
            : this(
                  sourcePath:           config?.TargetFilePath,
                  discoverTargets:      config?.DiscoverTargets      ?? Configuration.Default.DiscoverTargets,
                  desiredTargetCount:   config?.DesiredTargetCount   ?? Configuration.Default.DesiredTargetCount,
                  tcpPingPorts:         config?.TcpPingPorts         ?? Configuration.Default.TcpPingPorts,
                  pingsPerSample:       config?.PingsPerSample       ?? Configuration.Default.PingsPerSample,
                  targetsPerSample:     config?.TargetsPerSample     ?? Configuration.Default.TargetsPerSample,
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
            IEnumerable<PingTarget> targets = null, 
            bool? discoverTargets = null,
            int? desiredTargetCount = null,
            IEnumerable<ushort> tcpPingPorts = null,
            int? pingsPerSample = null, 
            int? targetsPerSample = null, 
            IRandomNumberGenerator rng = null, 
            TimeSpan? timeout = null
        )
            : base (periodNormalPriority.GetValueOrDefault(Configuration.Default.PeriodNormalPriority),
                  periodHighPriority.GetValueOrDefault(Configuration.Default.PeriodHighPriority),
                  periodLowPriority.GetValueOrDefault(Configuration.Default.PeriodLowPriority))
        {
            this._EnableTargetDiscovery = discoverTargets.GetValueOrDefault(Configuration.Default.DiscoverTargets);
            this._DesiredTargetCount = desiredTargetCount.GetValueOrDefault(Configuration.Default.DesiredTargetCount);
            this._TcpPorts = (tcpPingPorts ?? Configuration.Default.TcpPingPorts).ToArray();
            this._TargetsPerSample = targetsPerSample.GetValueOrDefault(Configuration.Default.TargetsPerSample);
            this._PingsPerSample = pingsPerSample.GetValueOrDefault(Configuration.Default.PingsPerSample);
            if (_TargetsPerSample <= 0)
                throw new ArgumentOutOfRangeException(nameof(pingsPerSample), pingsPerSample, "Pings per sample must be at least one.");
            if (_PingsPerSample <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetsPerSample), targetsPerSample, "Targets per sample must be at least one.");
            if (_DesiredTargetCount <= 0 && _EnableTargetDiscovery)
                throw new ArgumentOutOfRangeException(nameof(targetsPerSample), targetsPerSample, "Desired target count must be at least one.");
            this.SourcePath = sourcePath;
            if (targets != null)
                this._Targets.AddRange(targets);
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

        public static Task<IReadOnlyCollection<PingTarget>> LoadInternalTargetListAsync()
        {
            var log = LibLog.LogProvider.For<PingStatsSource>();
            log.Debug("Loading internal target list...");
            using (var stream = typeof(PingStatsSource).Assembly.GetManifestResourceStream(typeof(PingStatsSource), "PingTargetList.txt"))
            {
                return LoadTargetListAsync(stream);
            }
        }
        public static Task<IReadOnlyCollection<PingTarget>> LoadTargetListAsync(string path)
        {
            var log = LibLog.LogProvider.For<PingStatsSource>();
            log.Debug("Loading source target list from '{0}'...", path);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 32 * 1024, FileOptions.SequentialScan))
            {
                return LoadTargetListAsync(stream);
            }
        }
        public static async Task<IReadOnlyCollection<PingTarget>> LoadTargetListAsync(Stream stream)
        {
            var log = LibLog.LogProvider.For<PingStatsSource>();

            var targets = new List<PingTarget>();
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
                        targets.Add(target);
                    }
                    else
                        log.Warn("Unable to parse target for {0}: {1} (line {2:N0})", nameof(PingStatsSource), l, lineNum);
                }
            }
            log.Debug("Loaded {0:N0} targets.", targets.Count);
            return targets;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            if (_Targets.Count == 0 && !_UseRandomSourceForUnitTest)
            {
                // No prior persisted state: load a seed list to get started.
                await InitialiseTargetsFromSeedSource();
            }
            if (_Targets.Count == 0 && !_EnableTargetDiscovery)
            {
                // No targets and no discovery means no entropy! Return early; an error is logged in InitialiseTargetsFromSeedSource()
                return null;
            }
            if (!IsNetworkAvailable(10_000_000) && !_UseRandomSourceForUnitTest)
            {
                Log.Info("No usable network interface. Will wait and try again later.");
                return null;
            }

            // Gather entropy!
            byte[] result = null;
            IEnumerable<PingTarget> forRemoval = Enumerable.Empty<PingTarget>();
            if (_Targets.Count > 0)
            {
                (result, forRemoval) = await GatherEntropyFromTargets();
            }
            if (_UseRandomSourceForUnitTest)
                // Discovery involves real targets; sorry unit tests :-(
                return result;

            // Anything which failed all ping attempts will be removed.
            if (forRemoval.Any())
            {
                RemoveFailedTargets(forRemoval);
            }

            // Anything which was just an IP address should run discovery to convert into an ICMP / TCP target.
            // Collect a few IP targets, which will be sent to discovery.
            var forDiscovery = _Targets.OfType<IpAddressTarget>().Take(_TargetsPerSample).ToList();
            if (forDiscovery.Any())
            {
                await DiscoverTargets(forDiscovery);
            }

            // Finally, if nothing needed to be discovered from the main ping run, try to discover new targets up until the desired count.
            if (!forDiscovery.Any() && _EnableTargetDiscovery && _Targets.Count < _DesiredTargetCount)
            {
                await DiscoverTargets(_TargetsPerSample);
            }

            EnsureCountersAreValid();

            return result;
        }

        private async Task InitialiseTargetsFromSeedSource()
        {
            Log.Debug("Initialising target list.");
            try
            {
                if (!String.IsNullOrEmpty(SourcePath))
                    _Targets.AddRange(await LoadTargetListAsync(SourcePath));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to open Target Source File '{0}'.", SourcePath);
            }
            if (String.IsNullOrEmpty(SourcePath) && _Targets.Count == 0)
                _Targets.AddRange(await LoadInternalTargetListAsync());
            if (_Targets.Count == 0 && !_EnableTargetDiscovery)
                Log.Error("No targets are available and target discovery is disabled. This entropy source will be disabled.");

            RandomNumberExtensions.ShuffleInPlace(_Targets, _Rng);
            EnsureCountersAreValid();
            _TargetsIsModifiedForPersistentState = true;
        }

        private async Task<(byte[] entropy, IReadOnlyCollection<PingTarget> forRemoval)> GatherEntropyFromTargets()
        {
            // Do x pings to y targets in parallel.
            // Time each of them, use the high precision part as the result.

            // Select the targets we will ping.
            var countToSample = Math.Min(_TargetsPerSample, _Targets.Count);
            var targetsToSample = new List<PingAndStopwatch>(countToSample);
            for (int i = 0; i < _TargetsPerSample; i++)
            {
                if (_NextTarget >= _Targets.Count)
                    _NextTarget = 0;
                targetsToSample.Add(new PingAndStopwatch(_Targets[_NextTarget], _Timeout));
                _NextTarget = _NextTarget + 1;
            }

            // Now ping the targets and time how long it takes.
            var result = new List<byte>((countToSample + _PingsPerSample) * sizeof(ushort));
            for (int c = 0; c < _PingsPerSample; c++)
            {
                if (!_UseRandomSourceForUnitTest)
                {
                    await Task.WhenAll(targetsToSample.Select(x => x.ResetAndRun()).ToArray());

                    foreach (var s in targetsToSample.Where(x => x.Timing.TotalMilliseconds > 0 && x.Timing < _Timeout))
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

            // Collect any other targets which failed every attempt, which will be removed from the target list.
            var forRemoval = targetsToSample.Where(x => x.Target is not IpAddressTarget && x.Failures == _PingsPerSample).Select(x => x.Target).ToList();

            return (result.ToArray(), forRemoval);
        }

        private Task DiscoverTargets(int targetCount)
        {
            if (targetCount == 0)
                return Task.FromResult(new object());

            var targets = new List<IpAddressTarget>(targetCount);
            var bytes = new byte[4];

            while (targets.Count < targetCount)
            {
                // TODO: IPv6 support based on https://www.iana.org/assignments/ipv6-unicast-address-assignments/ipv6-unicast-address-assignments.xhtml
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
                if (_Targets.Any(x => ip.Equals(x.IPAddress)))
                    // Let's not add the same target twice!
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

            Log.Debug("Running discovery pings for {0:N0} IP addresses. Before discovery, there are {1:N0} targets.", targets.Count(), _Targets.Count);
            Log.Trace("Running discovery pings for: {0}", String.Join(", ", targets.Select(x => x.IPAddress)));

            var allPossibleTargets = targets.Select(x => PingTarget.ForIcmpPing(x.IPAddress)).Cast<PingTarget>();
            foreach (var port in _TcpPorts)
                allPossibleTargets = allPossibleTargets.Concat(targets.Select(x => PingTarget.ForTcpPing(x.IPAddress, port)).Cast<PingTarget>());
            var targetsToSample = allPossibleTargets.Select(x => new PingAndStopwatch(x, _Timeout)).ToList();

            await Task.WhenAll(targetsToSample.Where(x => x.Failures >= 0).Select(x => x.ResetAndRun()).ToArray());
            await Task.WhenAll(targetsToSample.Where(x => x.Failures >= 1).Select(x => x.ResetAndRun()).ToArray());
            await Task.WhenAll(targetsToSample.Where(x => x.Failures >= 2).Select(x => x.ResetAndRun()).ToArray());

            var toAdd = targetsToSample.Where(x => x.Failures < 3).Select(x => x.Target).ToList();
            AddNewTargets(toAdd);
            RemoveDiscoveryTargets(targets);

            Log.Debug("After discovery, there are {0:N0} targets.", _Targets.Count);
        }

        private void AddNewTargets(IReadOnlyCollection<PingTarget> newTargets)
        {
            foreach (var target in newTargets)
            {
                Log.Trace("Adding discovered target {0} to target list.", target);
                _Targets.Add(target);
                _TargetsIsModifiedForPersistentState = true;
            }

            if (newTargets.Any())
            {
                // Reshuffle whenever we add something new.
                RandomNumberExtensions.ShuffleInPlace(_Targets, _Rng);
                _NextTarget = 0;
            }
        }

        private void RemoveFailedTargets(IEnumerable<PingTarget> failedTargets)
        {
            foreach (var t in failedTargets)
            {
                if (_Targets.Remove(t))
                {
                    Log.Trace("Removed target {0} after all ping attempts failed.", t);
                    _TargetsIsModifiedForPersistentState = true;
                }
            }
        }

        private void RemoveDiscoveryTargets(IEnumerable<PingTarget> discoveryTargets)
        {
            foreach (var t in discoveryTargets)
            {
                if (_Targets.Remove(t))
                {
                    Log.Trace("Removed target {0} after discovery was run.", t);
                    _TargetsIsModifiedForPersistentState = true;
                }
            }
        }

        private void EnsureCountersAreValid()
        {
            // Assume this is run because of adding and removing targets, so we've probably shuffled the target list recently.
            if (_NextTarget > _Targets.Count)
                _NextTarget = 0;
        }

        #region IPersistentStateSource

        bool IPersistentStateSource.HasUpdates => _TargetsIsModifiedForPersistentState;

        void IPersistentStateSource.Initialise(IDictionary<string, NamespacedPersistentItem> state)
        {
            if (state.TryGetValue("TargetCount", out var countItem)
                && Int32.TryParse(countItem.ValueAsUtf8Text, out var count)
                && count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    if (state.TryGetValue("Target." + i.ToString(CultureInfo.InvariantCulture), out var item)
                        && PingTarget.TryParse(item.ValueAsUtf8Text, out var target))
                    {
                        _Targets.Add(target);
                    }
                }
                Log.Debug("Loaded {0:N0} target(s) from persistent state.", _Targets.Count);
            }

            _TargetsIsModifiedForPersistentState = false;
        }

        IEnumerable<NamespacedPersistentItem> IPersistentStateSource.GetCurrentState(PersistentEventType eventType)
        {
            var targets = _Targets.ToList();

            yield return NamespacedPersistentItem.CreateText("TargetCount", targets.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < targets.Count; i++)
            {
                yield return NamespacedPersistentItem.CreateText("Target." + i.ToString(CultureInfo.InvariantCulture), targets[i].ToString());
            }

            _TargetsIsModifiedForPersistentState = false;
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
            public readonly TimeSpan Timeout;
            public TimeSpan Timing { get; private set; }
            public int Failures { get; private set; }

            public async Task ResetAndRun()
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var (success, status) = await Target.Ping(Timeout);
                    sw.Stop();

                    Log.Trace("Ping to '{0}' in {1:N2}ms, result: {2}", Target, sw.Elapsed.TotalMilliseconds, status);
                    if (success)
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

        public abstract class PingTarget : IEquatable<PingTarget>
        {
            public readonly IPAddress IPAddress;

            private readonly static char[] SplitCharacter = new[] { ' ' };

            public static IpAddressTarget ForIpAddressOnly(IPAddress ip)
                => new IpAddressTarget(ip);

            public static IcmpTarget ForIcmpPing(IPAddress ip)
                => new IcmpTarget(ip);
            
            public static TcpTarget ForTcpPing(IPAddress ip, ushort port)
                => new TcpTarget(ip, port);

            public static bool TryParse(string s, out PingTarget result)
            {
                if (string.IsNullOrEmpty(s))
                {
                    result = null;
                    return false;
                }
                s = s.Trim();

                // Cases:
                //  1.2.3.4 (just IPv4 address)
                //  1.2.3.4:80 (IPv4 + port)
                //  1.2.3.4:ICMP (IPv4 + ICMP)
                //  2001::1 (just IPV6 address)
                //  [2001::1] (just IPV6 address with brackets)
                //  [2001::1]:80 (IPV6 address + port)
                //  [2001::1]:ICMP (IPV6 address + ICMP)

                // Start by finding the address part, and port / ICMP part.
                var isIpv4 = s.IndexOf('.') > 0;
                var isIpv6 = s.IndexOf('.') < 0;
                var indexOfLastColon = s.LastIndexOf(':');

                var addressPart = "";
                var portOrIcmpPart = "";
                if (isIpv4 && indexOfLastColon <= 0)
                {
                    addressPart = s;
                }
                else if (isIpv4 && indexOfLastColon > 0)
                {
                    addressPart = s.Substring(0, indexOfLastColon);
                    portOrIcmpPart = s.Substring(indexOfLastColon + 1);
                }
                else if (isIpv6)
                {
                    var indexOfOpenBracket = s.IndexOf('[');
                    var indexOfCloseBracket = s.IndexOf(']');
                    var hasOpenAndCloseBracketsForIpv6 = indexOfOpenBracket >= 0 && indexOfCloseBracket > 0;
                    var lastColonIsAfterCloseBracket = indexOfLastColon > indexOfCloseBracket;

                    if (!hasOpenAndCloseBracketsForIpv6)
                    {
                        addressPart = s;
                    }
                    else // hasOpenAndCloseBracketsForIpv6
                    {
                        addressPart = s.Substring(indexOfOpenBracket + 1, indexOfCloseBracket - 1 - indexOfOpenBracket);
                        if (lastColonIsAfterCloseBracket)
                            portOrIcmpPart = s.Substring(indexOfLastColon + 1);
                    }
                }

                // Based on if there is a port or ICMP or not, return the correct object type.
                if (!String.IsNullOrEmpty(addressPart) && String.IsNullOrEmpty(portOrIcmpPart)
                    && IPAddress.TryParse(addressPart, out var justIp))
                {
                    result = ForIpAddressOnly(justIp);
                    return true;
                }
                else if (!String.IsNullOrEmpty(addressPart) && portOrIcmpPart.Equals("ICMP", StringComparison.InvariantCultureIgnoreCase)
                    && IPAddress.TryParse(addressPart, out var ipForIcmp))
                {
                    result = ForIcmpPing(ipForIcmp);
                    return true;
                }
                else if (!String.IsNullOrEmpty(addressPart) && !String.IsNullOrEmpty(portOrIcmpPart)
                    && IPAddress.TryParse(addressPart, out var ipForPort)
                    && ushort.TryParse(portOrIcmpPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
                    && port > 0)
                {
                    result = ForTcpPing(ipForPort, port);
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            public override bool Equals(object obj)
                => obj is PingTarget t
                && Equals(t);

            public virtual bool Equals(PingTarget other)
                => other != null
                && GetType() == other.GetType()
                && IPAddress == other.IPAddress;

            public override int GetHashCode()
                => GetType().GetHashCode() ^ IPAddress.GetHashCode();

            public PingTarget(IPAddress ipAddress)
            {
                IPAddress = ipAddress;
            }

            public abstract Task<(bool isSuccess, object error)> Ping(TimeSpan timeout);
        }

        // Just an IP address, no ICMP or port number. Need to test both
        public sealed class IpAddressTarget : PingTarget, IEquatable<IpAddressTarget>
        {
            internal IpAddressTarget(IPAddress ip)
                : base(ip)
            { }

            public bool Equals(IpAddressTarget other)
                => other != null
                && IPAddress == other.IPAddress;

            public override string ToString()
                => IPAddress.ToString();

            public override async Task<(bool isSuccess, object error)> Ping(TimeSpan timeout)
            {
                // Although we don't know if this target is for ICMP, that's the default implementation.
                var result = await new Ping().SendPingAsync(IPAddress, (int)timeout.TotalMilliseconds);
                return (result.Status == IPStatus.Success, result.Status);
            }
        }

        // IP address as ICMP ping target.
        public sealed class IcmpTarget : PingTarget, IEquatable<IcmpTarget>
        {
            internal IcmpTarget(IPAddress ip)
                : base(ip)
            { }

            public bool Equals(IcmpTarget other)
                => other != null
                && IPAddress == other.IPAddress;

            public override string ToString()
                => (IPAddress.AddressFamily == AddressFamily.InterNetworkV6 ? "[" + IPAddress.ToString() + "]" : IPAddress.ToString())
                + ":ICMP";


            public override async Task<(bool isSuccess, object error)> Ping(TimeSpan timeout)
            {
                var result = await new Ping().SendPingAsync(IPAddress, (int)timeout.TotalMilliseconds);
                return (result.Status == IPStatus.Success, result.Status);
            }
        }

        // IP address + port number as TCP ping target.
        public sealed class TcpTarget : PingTarget, IEquatable<TcpTarget>
        {
            public ushort Port { get; }

            internal TcpTarget(IPAddress ip, ushort port)
                : base(ip)
            {
                Port = port;
            }

            public override bool Equals(PingTarget other)
                => base.Equals(other)
                && other is TcpTarget t
                && Port == t.Port;

            public bool Equals(TcpTarget other)
                => other != null
                && IPAddress == other.IPAddress
                && Port == other.Port;

            public override int GetHashCode()
                => GetType().GetHashCode() ^ IPAddress.GetHashCode() ^ Port;

            public override string ToString()
                => (IPAddress.AddressFamily == AddressFamily.InterNetworkV6 ? "[" + IPAddress.ToString() + "]" : IPAddress.ToString())
                + ":" 
                + Port.ToString(CultureInfo.InvariantCulture);

            public override async Task<(bool isSuccess, object error)> Ping(TimeSpan timeout)
            {
                using (var socket = new Socket(IPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                using (var cancel = new CancellationTokenSource())
                {
                    try
                    {
                        cancel.CancelAfter(timeout);
#if NET6_0_OR_GREATER
                        await socket.ConnectAsync(IPAddress, Port, cancel.Token);
                        return (true, SocketError.Success);
#else
                        // netstandard2.0 doesn't have a nice async API for Socket.Connect(),
                        // so we have to wire everything up manually
                        var tcs = new TaskCompletionSource<(bool isSuccess, SocketError error)>();
                        var e = new SocketAsyncEventArgs()
                        {
                            RemoteEndPoint = new IPEndPoint(IPAddress, Port),
                            UserToken = tcs,
                        };
                        e.Completed += (sender, e2) =>
                        {
                            var tcs2 = (TaskCompletionSource<(bool isSuccess, SocketError error)>)e2.UserToken;
                            tcs.TrySetResult((e2.SocketError == SocketError.Success, e2.SocketError));
                        };
                        cancel.Token.Register(arg =>
                        {
                            var tcs3 = (TaskCompletionSource<(bool isSuccess, SocketError error)>)arg;
                            tcs.TrySetResult((false, SocketError.TimedOut));
                        }, tcs);

                        socket.ConnectAsync(e);
                        return await tcs.Task;
#endif
                    }
                    catch (OperationCanceledException)
                    {
                        return (false, SocketError.TimedOut);
                    }
                    catch (SocketException ex)
                    {
                        return (false, ex.SocketErrorCode);
                    }
                }
            }
        }

        public class Configuration
        {
            public static readonly Configuration Default = new Configuration();

            /// <summary>
            /// Number of targets to ping from the list each sample.
            /// Default: 8. Minimum: 1. Maximum: 100.
            /// </summary>
            public int TargetsPerSample { get; set; } = 8;

            /// <summary>
            /// Number of times to ping each target per sample.
            /// Default: 6. Minimum: 1. Maximum: 100.
            /// </summary>
            public int PingsPerSample { get; set; } = 6;

            /// <summary>
            /// Path to file containing initial target list.
            /// If left blank, an internal list is used.
            /// Note this is only used as the initial list once, persistent state is used after that.
            /// </summary>
            public string TargetFilePath { get; set; }

            /// <summary>
            /// Automatically discover new targets to ping by randomly scanning the Internet.
            /// Default: true.
            /// </summary>
            public bool DiscoverTargets { get; set; } = true;

            /// <summary>
            /// Count of targets to accumulate when discovering.
            /// Default: 1024. Minimum: 1. Maximum: 65536.
            /// Each target will be recorded in persistent state.
            /// Note that each endpoint is counted as one target. So 1.1.1.1:80 and 1.1.1.1:443 and 1.1.1.1:ICMP count as three.
            /// </summary>
            public int DesiredTargetCount { get; set; } = 1024;

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
