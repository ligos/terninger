using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.Helpers;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// An entropy source which uses system processes statistics as input.
    /// </summary>
    public class ProcessStatsSource : IEntropySource
    {
        public string Name => typeof(ProcessStatsSource).FullName;

        private const int _ItemsPerProcess = 17;            // This many properties are read from each process. Based on available properties.

        private IRandomNumberGenerator _Rng;

        // Config properties.
        private DateTime _NextSampleTimestamp;
        private int _ItemsPerResultChunk = 70;              // This many Int64 stats are combined into one final hash. 70 should span a bit over 4 processes each.
        public int StatsPerChunk => _ItemsPerResultChunk;
        private double _PeriodMinutes = 10.0;               // 10 minutes between runs, by default.
        public double PeriodMinutes => _PeriodMinutes;

        public ProcessStatsSource() : this(10.0, 70, null) { }
        public ProcessStatsSource(double periodMinutes) : this(periodMinutes, 70, null) { }
        public ProcessStatsSource(double periodMinutes, int itemsPerResultChunk) : this(periodMinutes, itemsPerResultChunk, null) { }
        public ProcessStatsSource(double periodMinutes, int itemsPerResultChunk, IRandomNumberGenerator rng)
        {
            this._PeriodMinutes = periodMinutes >= 0.0 ? periodMinutes : 10.0;
            this._ItemsPerResultChunk = itemsPerResultChunk > 0 ? itemsPerResultChunk : 70;
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
            if (config.IsTruthy("ProcessStatsSource.Enabled") == false)
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.DisabledByConfig, "ProcessStatsSource has been disabled in entropy source configuration."));

            config.TryParseAndSetDouble("ProcessStatsSource.PeriodMinutes", ref _PeriodMinutes);
            if (_PeriodMinutes < 0.0 || _PeriodMinutes > 1440.0)
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, new ArgumentOutOfRangeException("ProcessStatsSource.PeriodMinutes", _PeriodMinutes, "Config item ProcessStatsSource.PeriodMinutes must be between 0 and 1440 (one day)")));

            config.TryParseAndSetInt32("ProcessStatsSource.StatsPerChunk", ref _ItemsPerResultChunk);
            if (_ItemsPerResultChunk <= 0 || _ItemsPerResultChunk > 10000)
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, new ArgumentOutOfRangeException("ProcessStatsSource.StatsPerChunk", _ItemsPerResultChunk, "Config item ProcessStatsSource.StatsPerChunk must be between 1 and 10000")));

            _Rng = prngFactory() ?? StandardRandomWrapperGenerator.StockRandom();
            _NextSampleTimestamp = DateTime.UtcNow;

            return Task.FromResult(EntropySourceInitialisationResult.Successful());
        }

        public Task<byte[]> GetEntropyAsync()
        {
            // This reads details of all processes running on the system, and uses them as inputs to a hash for final result.
            // Often, different properties or processes will throw exceptions.
            // Given this isn't trivial work, we run in a separate threadpool task.
            // There's also a period where we won't sample.

            // Return early until we're past the next sample time.
            if (_NextSampleTimestamp > DateTime.UtcNow)
                return Task.FromResult<byte[]>(null);

            return Task.Run(() =>
            {
                var ps = Process.GetProcesses();        // TODO: assert we can do this during initialisation.

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

                // Shuffle the details, so there isn't a repetition of similar stats.
                processStatsNoZero.ShuffleInPlace(_Rng);

                // Get digests of the stats to return.
                var result = ByteArrayHelpers.LongsToDigestBytes(processStatsNoZero, _ItemsPerResultChunk);

                // Set the next run time.
                _NextSampleTimestamp = DateTime.UtcNow.AddMinutes(_PeriodMinutes);

                return result;
            });
        }
    }
}
