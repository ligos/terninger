﻿using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.LibLog;

namespace MurrayGrant.Terninger.EntropySources.Local
{
    /// <summary>
    /// An entropy source which uses system processes statistics as input.
    /// Polling defaults: 10 minutes at normal priority, 30 seconds at high priority, 50 minutes at low priority (5 x normal).
    /// </summary>
    [AsyncHint(IsAsync.Always)]
    public class ProcessStatsSource : EntropySourceWithPeriod
    {
        public override string Name { get; set; }

        private const int _ItemsPerProcess = 17;            // This many properties are read from each process. Based on available properties.

        private IRandomNumberGenerator _Rng;

        // Config properties.
        private int _ItemsPerResultChunk = 70;              // This many Int64 stats are combined into one final hash. 70 should span a bit over 4 processes each.
        public int StatsPerChunk => _ItemsPerResultChunk;

        // This logs the raw stat long array to Trace. Only for testing.
        internal bool LogRawStats { get; set; }

        // Polling defaults: 10 minutes at normal priority, 30 seconds at high priority, 50 minutes at low priority (5 x normal).

        public ProcessStatsSource(Configuration config)
            : this(
                  periodNormalPriority: config?.PeriodNormalPriority ?? Configuration.Default.PeriodNormalPriority,
                  periodHighPriority:   config?.PeriodHighPriority   ?? Configuration.Default.PeriodHighPriority,
                  periodLowPriority:    config?.PeriodLowPriority    ?? Configuration.Default.PeriodLowPriority,
                  itemsPerResultChunk:  config?.ItemsPerChunk        ?? Configuration.Default.ItemsPerChunk,
                  rng: null
            )
        { }
        public ProcessStatsSource(TimeSpan? periodNormalPriority = null, TimeSpan? periodHighPriority = null, TimeSpan? periodLowPriority = null, int? itemsPerResultChunk = null, IRandomNumberGenerator rng = null)
            : base (periodNormalPriority.GetValueOrDefault(Configuration.Default.PeriodNormalPriority),
                  periodHighPriority.GetValueOrDefault(Configuration.Default.PeriodHighPriority), 
                  periodLowPriority.GetValueOrDefault(Configuration.Default.PeriodLowPriority))
        {
            this._ItemsPerResultChunk = itemsPerResultChunk.GetValueOrDefault(Configuration.Default.ItemsPerChunk);
            if (_ItemsPerResultChunk < 1 || _ItemsPerResultChunk > 10000)
                throw new ArgumentOutOfRangeException(nameof(itemsPerResultChunk), itemsPerResultChunk, "Items per chunk must be between 1 and 10000");

            this._Rng = rng ?? StandardRandomWrapperGenerator.StockRandom();
        }

        public override void Dispose()
        {
            var disposable = _Rng as IDisposable;
            if (disposable != null)
                DisposeHelper.TryDispose(disposable);
            disposable = null;
        }

        protected override Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            // This reads details of all processes running on the system, and uses them as inputs to a hash for final result.
            // Often, different properties or processes will throw exceptions.
            // Given this isn't trivial work, we run in a separate threadpool task.

            return Task.Run(() =>
            {
                Log.Trace("Beginning to gather entropy.");
                var ps = Process.GetProcesses();        // TODO: assert we can do this during initialisation?
                Log.Trace("Found {0:N0} processes.", ps.Length);

                var processStats = new long[ps.Length * _ItemsPerProcess];

                // Read details from all processes.
                // PERF: This takes several seconds, which isn't helped by the fact a large number of these will throw exceptions when not running as admin.
                for (int i = 0; i < ps.Length; i++)
                {
                    var p = ps[i];
                    processStats[(i * _ItemsPerProcess) + 0] = p.TryAndIgnoreException(x => x.Id);
                    processStats[(i * _ItemsPerProcess) + 1] = p.TryAndIgnoreException(x => x.MainWindowHandle.ToInt64());
                    processStats[(i * _ItemsPerProcess) + 2] = p.TryAndIgnoreException(x => x.MaxWorkingSet.ToInt64());
                    processStats[(i * _ItemsPerProcess) + 3] = p.TryAndIgnoreException(x => x.NonpagedSystemMemorySize64);
                    processStats[(i * _ItemsPerProcess) + 4] = p.TryAndIgnoreException(x => x.PagedMemorySize64);
                    processStats[(i * _ItemsPerProcess) + 5] = p.TryAndIgnoreException(x => x.PagedSystemMemorySize64);
                    processStats[(i * _ItemsPerProcess) + 6] = p.TryAndIgnoreException(x => x.PeakPagedMemorySize64);
                    processStats[(i * _ItemsPerProcess) + 7] = p.TryAndIgnoreException(x => x.PeakVirtualMemorySize64);
                    processStats[(i * _ItemsPerProcess) + 8] = p.TryAndIgnoreException(x => x.PeakWorkingSet64);
                    processStats[(i * _ItemsPerProcess) + 9] = p.TryAndIgnoreException(x => x.PrivateMemorySize64);
                    processStats[(i * _ItemsPerProcess) + 10] = p.TryAndIgnoreException(x => x.WorkingSet64);
                    processStats[(i * _ItemsPerProcess) + 11] = p.TryAndIgnoreException(x => x.VirtualMemorySize64);
                    processStats[(i * _ItemsPerProcess) + 12] = p.TryAndIgnoreException(x => x.UserProcessorTime.Ticks);
                    processStats[(i * _ItemsPerProcess) + 13] = p.TryAndIgnoreException(x => x.TotalProcessorTime.Ticks);
                    processStats[(i * _ItemsPerProcess) + 14] = p.TryAndIgnoreException(x => x.PrivilegedProcessorTime.Ticks);
                    processStats[(i * _ItemsPerProcess) + 15] = p.TryAndIgnoreException(x => x.StartTime.Ticks);
                    processStats[(i * _ItemsPerProcess) + 16] = p.TryAndIgnoreException(x => x.HandleCount);
                }

                // Remove all zero items (to prevent silly things like a mostly, or all, zero hash result).
                var processStatsNoZero = processStats.Where(x => x != 0L).ToArray();
                Log.Trace("Read {0:N0} non-zero stat items.", processStatsNoZero.Length);
                if (LogRawStats)
                    Log.Trace("Raw stats: ", processStatsNoZero.LongsToHexString());

                // Shuffle the details, so there isn't a repetition of similar stats.
                processStatsNoZero.ShuffleInPlace(_Rng);

                // Get digests of the stats to return.
                var result = ByteArrayHelpers.LongsToDigestBytes(processStatsNoZero, _ItemsPerResultChunk);
                Log.Trace("Converted stats to {0:N0} bytes of entropy.", result.Length);

                return result;
            });
        }

        public class Configuration
        {
            public static readonly Configuration Default = new Configuration();

            /// <summary>
            /// Sample period at normal priority. Default: 10 minutes.
            /// </summary>
            public TimeSpan PeriodNormalPriority { get; set; } = TimeSpan.FromMinutes(10.0);

            /// <summary>
            /// Sample period at high priority. Default: 30 seconds.
            /// </summary>
            public TimeSpan PeriodHighPriority { get; set; } = TimeSpan.FromSeconds(30.0);

            /// <summary>
            /// Sample period at low priority. Default: 50 minutes.
            /// </summary>
            public TimeSpan PeriodLowPriority { get; set; } = TimeSpan.FromMinutes(50.0);

            /// <summary>
            /// Number of items from processes read per sample. There are up to 17 items per process.
            /// Default: 70. Minimum: 1. Maximum: 10000.
            /// </summary>
            public int ItemsPerChunk { get; set; } = 70;

            // TODO: configure the random generator between StockRandom, TerningerRandom
        }
    }
}
