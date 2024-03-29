﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.NetworkInformation;

using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.LibLog;
using Uh = MurrayGrant.Terninger.Helpers.UnitHelpers;

namespace MurrayGrant.Terninger.EntropySources.Local
{
    /// <summary>
    /// An entropy source which uses current network statistics as input.
    /// Polling defaults: 1 minute at normal priority, 5 seconds at high priority, 5 minutes at low priority (5 x normal).
    /// </summary>
    [AsyncHint(IsAsync.Always)]
    public class NetworkStatsSource : EntropySourceWithPeriod
    {
        public override string Name { get; set; }

        private bool _HasRunOnce = false;
        private IRandomNumberGenerator _Rng;
        // Bunch of flags to notice if we get exceptions on certain properties - as not all platforms support everything.
        private bool _BytesReceivedFailed;
        private bool _BytesSentFailed;
        private bool _IncomingPacketsDiscardedFailed;
        private bool _IncomingPacketsWithErrorsFailed;
        private bool _IncomingUnknownProtocolPacketsFailed;
        private bool _NonUnicastPacketsReceivedFailed;
        private bool _NonUnicastPacketsSentFailed;
        private bool _OutgoingPacketsDiscardedFailed;
        private bool _OutgoingPacketsWithErrorsFailed;
        private bool _OutputQueueLengthFailed;
        private bool _UnicastPacketsReceivedFailed;
        private bool _UnicastPacketsSentFailed;
        private bool _AddressValidLifetimeFailed;

        // Config properties.
        private int _ItemsPerResultChunk = 17;              // This many Int64 stats are combined into one final hash.
        public int StatsPerChunk => _ItemsPerResultChunk;

        // This logs the raw stat long array to Trace. Only for testing.
        internal bool LogRawStats { get; set; }

        public NetworkStatsSource(Configuration config)
            : this(
                  periodNormalPriority: config?.PeriodNormalPriority ?? Configuration.Default.PeriodNormalPriority,
                  periodHighPriority:   config?.PeriodHighPriority   ?? Configuration.Default.PeriodHighPriority,
                  periodLowPriority:    config?.PeriodLowPriority    ?? Configuration.Default.PeriodLowPriority,
                  itemsPerResultChunk:  config?.ItemsPerChunk        ?? Configuration.Default.ItemsPerChunk,
                  rng: null
            )
        { }
        public NetworkStatsSource(TimeSpan? periodNormalPriority = null, TimeSpan? periodHighPriority = null, TimeSpan? periodLowPriority = null, int? itemsPerResultChunk = null, IRandomNumberGenerator rng = null)
            : base(periodNormalPriority.GetValueOrDefault(Configuration.Default.PeriodNormalPriority), 
                  periodHighPriority.GetValueOrDefault(Configuration.Default.PeriodHighPriority), 
                  periodLowPriority.GetValueOrDefault(Configuration.Default.PeriodLowPriority))
        {
            this._ItemsPerResultChunk = itemsPerResultChunk.GetValueOrDefault(Configuration.Default.ItemsPerChunk);
            if (_ItemsPerResultChunk < 1 || _ItemsPerResultChunk > 10000)
                throw new ArgumentOutOfRangeException(nameof(itemsPerResultChunk), itemsPerResultChunk, "Items per chunck must be between 1 and 10000");

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

                    // Lease lifetime: only available on Windows.
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        foreach (var l in props.UnicastAddresses.Select(x => x.DhcpLeaseLifetime).Where(x => x != 0L))
                            yield return l;
                    }

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
                    // Note that these can throw on some platforms, so we do a bunch of exception wrapping.
                    var stats = i.GetIPStatistics();
                    if (!_BytesReceivedFailed)
                        ExceptionHelper.TryAndIgnoreException(() => Uh.ToUnit(() => allStats.Add(stats.BytesReceived)), ref _BytesReceivedFailed);
                    if (!_BytesSentFailed)
                        ExceptionHelper.TryAndIgnoreException(() => Uh.ToUnit(() => allStats.Add(stats.BytesSent)), ref _BytesSentFailed);
                    if (!_IncomingPacketsDiscardedFailed)
                        ExceptionHelper.TryAndIgnoreException(() => Uh.ToUnit(() => allStats.Add(stats.IncomingPacketsDiscarded)), ref _IncomingPacketsDiscardedFailed);
                    if (!_IncomingPacketsWithErrorsFailed)
                        ExceptionHelper.TryAndIgnoreException(() => Uh.ToUnit(() => allStats.Add(stats.IncomingPacketsWithErrors)), ref _IncomingPacketsWithErrorsFailed);
                    if (!_IncomingUnknownProtocolPacketsFailed)
                        ExceptionHelper.TryAndIgnoreException(() => Uh.ToUnit(() => allStats.Add(stats.IncomingUnknownProtocolPackets)), ref _IncomingUnknownProtocolPacketsFailed);
                    if (!_NonUnicastPacketsReceivedFailed)
                        ExceptionHelper.TryAndIgnoreException(() => Uh.ToUnit(() => allStats.Add(stats.NonUnicastPacketsReceived)), ref _NonUnicastPacketsReceivedFailed);
                    if (!_NonUnicastPacketsSentFailed)
                        ExceptionHelper.TryAndIgnoreException(() => Uh.ToUnit(() => allStats.Add(stats.NonUnicastPacketsSent)), ref _NonUnicastPacketsSentFailed);
                    if (!_OutgoingPacketsDiscardedFailed)
                        ExceptionHelper.TryAndIgnoreException(() => Uh.ToUnit(() => allStats.Add(stats.OutgoingPacketsDiscarded)), ref _OutgoingPacketsDiscardedFailed);
                    if (!_OutgoingPacketsWithErrorsFailed)
                        ExceptionHelper.TryAndIgnoreException(() => Uh.ToUnit(() => allStats.Add(stats.OutgoingPacketsWithErrors)), ref _OutgoingPacketsWithErrorsFailed);
                    if (!_OutputQueueLengthFailed)
                        ExceptionHelper.TryAndIgnoreException(() => Uh.ToUnit(() => allStats.Add(stats.OutputQueueLength)), ref _OutputQueueLengthFailed);
                    if (!_UnicastPacketsReceivedFailed)
                        ExceptionHelper.TryAndIgnoreException(() => Uh.ToUnit(() => allStats.Add(stats.UnicastPacketsReceived)), ref _UnicastPacketsReceivedFailed);
                    if (!_UnicastPacketsSentFailed)
                        ExceptionHelper.TryAndIgnoreException(() => Uh.ToUnit(() => allStats.Add(stats.UnicastPacketsSent)), ref _UnicastPacketsSentFailed);

                    // Remaining lease duration.
                    if (!_AddressValidLifetimeFailed)
                        ExceptionHelper.TryAndIgnoreException(() => Uh.ToUnit(() =>
                        {
                            var props = i.GetIPProperties();
                            allStats.AddRange(props.UnicastAddresses.Select(x => x.AddressValidLifetime));
                        }), ref _AddressValidLifetimeFailed);
                }

                // Remove zeros and shuffle to prevent obvious correlations.
                var statsNoZero = allStats.Where(x => x != 0L).ToArray();
                Log.Trace("Read {0:N0} non-zero stat items.", statsNoZero.Length);
                if (LogRawStats)
                    Log.Trace("Raw stats: ", statsNoZero.LongsToHexString());

                // Shuffle the details, so there isn't a repetition of similar stats.
                statsNoZero.ShuffleInPlace(_Rng);

                // Convert to digest byte array to return.
                var result = ByteArrayHelpers.LongsToDigestBytes(statsNoZero, _ItemsPerResultChunk);
                Log.Trace("Converted stats to {0:N0} bytes of entropy.", result.Length);

                return result;
            });
        }

        public class Configuration
        {
            public static readonly Configuration Default = new Configuration();

            /// <summary>
            /// Sample period at normal priority. Default: 1 minute.
            /// </summary>
            public TimeSpan PeriodNormalPriority { get; set; } = TimeSpan.FromMinutes(1.0);

            /// <summary>
            /// Sample period at high priority. Default: 5 seconds.
            /// </summary>
            public TimeSpan PeriodHighPriority { get; set; } = TimeSpan.FromSeconds(5.0);

            /// <summary>
            /// Sample period at low priority. Default: 5 minutes.
            /// </summary>
            public TimeSpan PeriodLowPriority { get; set; } = TimeSpan.FromMinutes(5.0);

            /// <summary>
            /// Number of network statistics read per sample.
            /// Default: 17. Minimum: 1. Maximum: 10000.
            /// </summary>
            public int ItemsPerChunk { get; set; } = 17;

            // TODO: configure the random generator between StockRandom, TerningerRandom
        }
    }
}
