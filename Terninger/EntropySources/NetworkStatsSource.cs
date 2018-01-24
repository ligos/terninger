using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.NetworkInformation;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.Helpers;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// An entropy source which uses current network statistics as input.
    /// </summary>
    public class NetworkStatsSource : IEntropySource
    {
        public string Name => typeof(NetworkStatsSource).FullName;
        
        private bool _HasRunOnce = false;
        private IRandomNumberGenerator _Rng;

        // Config properties.
        private DateTime _NextSampleTimestamp;
        private int _ItemsPerResultChunk = 17;              // This many Int64 stats are combined into one final hash.
        public int StatsPerChunk => _ItemsPerResultChunk;
        private double _PeriodMinutes = 1.0;                // 1 minute between runs, by default.
        public double PeriodMinutes => _PeriodMinutes;

        public NetworkStatsSource() : this(1.0, 17, null) { }
        public NetworkStatsSource(double periodMinutes) : this(periodMinutes, 17, null) { }
        public NetworkStatsSource(double periodMinutes, int itemsPerResultChunk) : this(periodMinutes, itemsPerResultChunk, null) { }
        public NetworkStatsSource(double periodMinutes, int itemsPerResultChunk, IRandomNumberGenerator rng)
        {
            this._PeriodMinutes = periodMinutes;
            this._ItemsPerResultChunk = itemsPerResultChunk;
            this._Rng = rng ?? StandardRandomWrapperGenerator.StockRandom();
        }

        public void Dispose()
        {
            var disposable = _Rng as IDisposable;
            if (disposable != null)
                DisposeHelper.TryDispose(disposable);
            disposable = null;
        }

        public Task<EntropySourceInitialisationResult> Initialise(IEntropySourceConfig config, Func<IRandomNumberGenerator> prngFactory)
        {
            if (config.IsTruthy("NetworkStatsSource.Enabled") == false)
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.DisabledByConfig, "NetworkStatsSource has been disabled in entropy source configuration."));

            config.TryParseAndSetDouble("NetworkStatsSource.PeriodMinutes", ref _PeriodMinutes);
            if (_PeriodMinutes < 0.0 || _PeriodMinutes > 1440.0)
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, new ArgumentOutOfRangeException("NetworkStatsSource.PeriodMinutes", _PeriodMinutes, "Config item NetworkStatsSource.PeriodMinutes must be between 0 and 1440 (1 day)")));

            config.TryParseAndSetInt32("NetworkStatsSource.StatsPerChunk", ref _ItemsPerResultChunk);
            if (_ItemsPerResultChunk <= 0 || _ItemsPerResultChunk > 10000)
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, new ArgumentOutOfRangeException("NetworkStatsSource.StatsPerChunk", _ItemsPerResultChunk, "Config item NetworkStatsSource.StatsPerChunk must be between 1 and 10000")));

            _Rng = prngFactory() ?? StandardRandomWrapperGenerator.StockRandom();
            _NextSampleTimestamp = DateTime.UtcNow;

            return Task.FromResult(EntropySourceInitialisationResult.Successful());
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

        public Task<byte[]> GetEntropyAsync()
        {
            // This reads details of all network interfaces running on the system, and uses them as inputs to a hash for final result.
            // Given this isn't trivial work, we run in a separate threadpool task.
            // There's also a period where we won't sample.

            // Return early until we're past the next sample time.
            if (_NextSampleTimestamp > DateTime.UtcNow)
                return Task.FromResult<byte[]>(null);

            return Task.Run(() =>
            {
                var ins = NetworkInterface.GetAllNetworkInterfaces();

                // First result includes IP address, hardware address, etc.
                var allStats = new List<long>();
                if (!_HasRunOnce)
                {
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

                // Shuffle the details, so there isn't a repetition of similar stats.
                statsNoZero.ShuffleInPlace(_Rng);

                // Convert to digest byte array to return.
                var result = ByteArrayHelpers.LongsToDigestBytes(statsNoZero, _ItemsPerResultChunk);

                // Set the next run time.
                _NextSampleTimestamp = DateTime.UtcNow.AddMinutes(_PeriodMinutes);

                return result;
            });
        }


    }
}
