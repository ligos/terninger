using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.NetworkInformation;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.LibLog;

namespace MurrayGrant.Terninger.EntropySources.Local
{
    /// <summary>
    /// An entropy source which uses current network statistics as input.
    /// Polling defaults: 1 minute at normal priority, 5 seconds at high priority, 5 minutes at low priority (5 x normal).
    /// </summary>
    public class NetworkStatsSource : EntropySourceWithPeriod
    {
        public override string Name => typeof(NetworkStatsSource).FullName;
        
        private bool _HasRunOnce = false;
        private IRandomNumberGenerator _Rng;

        // Config properties.
        private int _ItemsPerResultChunk = 17;              // This many Int64 stats are combined into one final hash.
        public int StatsPerChunk => _ItemsPerResultChunk;

        public NetworkStatsSource() : this(TimeSpan.FromMinutes(1.0), 17) { }
        public NetworkStatsSource(TimeSpan periodNormalPriority) : this(periodNormalPriority, 17) { }
        public NetworkStatsSource(TimeSpan periodNormalPriority, int itemsPerResultChunk) : this(periodNormalPriority, TimeSpan.FromSeconds(5), new TimeSpan(periodNormalPriority.Ticks * 5), itemsPerResultChunk, null) { }
        public NetworkStatsSource(TimeSpan periodNormalPriority, TimeSpan periodHighPriority, TimeSpan periodLowPriority, int itemsPerResultChunk, IRandomNumberGenerator rng)
            : base(periodNormalPriority, periodHighPriority, periodLowPriority)
        {
            if (itemsPerResultChunk < 1 || itemsPerResultChunk > 10000)
                throw new ArgumentOutOfRangeException(nameof(itemsPerResultChunk), itemsPerResultChunk, "Items per chunck must be between 1 and 10000");

            this._ItemsPerResultChunk = itemsPerResultChunk;
            this._Rng = rng ?? StandardRandomWrapperGenerator.StockRandom();
        }

        public override void Dispose()
        {
            var disposable = _Rng as IDisposable;
            if (disposable != null)
                DisposeHelper.TryDispose(disposable);
            disposable = null;
        }

        public static IEnumerable<long> GetNetworkInterfaceStaticProperties() => GetNetworkInterfaceStaticProperties(NetworkInterface.GetAllNetworkInterfaces());
        public static IEnumerable<long> GetNetworkInterfaceStaticProperties(NetworkInterface[] ins)
        {
            foreach (var i in ins)
            {
                if (i.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    // Name and speed.
                    foreach (var l in i.Name.ToLongs())
                        yield return l;
                    foreach (var l in i.Description.ToLongs())
                        yield return l;
                    yield return i.Speed;
                    
                    
                    var props = i.GetIPProperties();
                    // IP addresses.
                    foreach (var l in props.UnicastAddresses.SelectMany(x => x.Address.ToLongs()).Where(x => x != 0L))
                        yield return l;

                    // Gateway addresses.
                    foreach (var l in props.GatewayAddresses.SelectMany(x => x.Address.ToLongs()).Where(x => x != 0L))
                        yield return l;

                    // Lease lifetime.
                    foreach (var l in props.UnicastAddresses.Select(x => x.DhcpLeaseLifetime).Where(x => x != 0L))
                        yield return l;

                    // DNS suffix.
                    foreach (var l in props.DnsSuffix.ToLongs().Where(x => x != 0L))
                        yield return l;

                    // And physical addresses.
                    foreach (var l in i.GetPhysicalAddress().GetAddressBytes().ToLongs().Where(x => x != 0L))
                        yield return l;
                }
            }
        }

        protected override Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            // This reads details of all network interfaces running on the system, and uses them as inputs to a hash for final result.
            // Given this isn't trivial work, we run in a separate threadpool task.
        
            return Task.Run(() =>
            {
                Log.Trace("Beginning to gather entropy.");
                var ins = NetworkInterface.GetAllNetworkInterfaces();
                Log.Trace("Found {0:N0} interfaces.", ins.Length);

                // First result includes IP address, hardware address, etc.
                var allStats = new List<long>();
                if (!_HasRunOnce)
                {
                    Log.Trace("Including static properties on first run.");
                    allStats.AddRange(GetNetworkInterfaceStaticProperties(ins));
                    _HasRunOnce = true;
                }

                // After that, its just the number of packets, etc.
                foreach (var i in ins)
                {
                    // Most of these will be zero.
                    var stats = i.GetIPStatistics();
                    allStats.Add(stats.BytesReceived);
                    allStats.Add(stats.BytesSent);
                    allStats.Add(stats.IncomingPacketsDiscarded);
                    allStats.Add(stats.IncomingPacketsWithErrors);
                    allStats.Add(stats.IncomingUnknownProtocolPackets);
                    allStats.Add(stats.NonUnicastPacketsReceived);
                    allStats.Add(stats.NonUnicastPacketsSent);
                    allStats.Add(stats.OutgoingPacketsDiscarded);
                    allStats.Add(stats.OutgoingPacketsWithErrors);
                    allStats.Add(stats.OutputQueueLength);
                    allStats.Add(stats.UnicastPacketsReceived);
                    allStats.Add(stats.UnicastPacketsSent);

                    var props = i.GetIPProperties();
                    allStats.AddRange(props.UnicastAddresses.Select(x => x.AddressValidLifetime));      // Remaining lease duration.
                }

                // Remove zeros and shuffle to prevent obvious correlations.
                var statsNoZero = allStats.Where(x => x != 0L).ToArray();
                Log.Trace("Read {0:N0} non-zero stat items.", statsNoZero.Length);

                // Shuffle the details, so there isn't a repetition of similar stats.
                statsNoZero.ShuffleInPlace(_Rng);

                // Convert to digest byte array to return.
                var result = ByteArrayHelpers.LongsToDigestBytes(statsNoZero, _ItemsPerResultChunk);
                Log.Trace("Converted stats to {0:N0} bytes of entropy.", result.Length);

                return result;
            });
        }
    }
}
