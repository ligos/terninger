using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.LibLog;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// Base class for entropy sources with a regular period they pool.
    /// They will return 
    /// This class is abstract.
    /// </summary>
    public abstract class EntropySourceWithPeriod : IEntropySource
    {
        public abstract string Name { get; }

        protected readonly TimeSpan _PeriodHighPriority;
        public TimeSpan PeriodHighPriority => _PeriodHighPriority;
        protected readonly TimeSpan _PeriodNormalPriority;
        public TimeSpan PeriodNormalPriority => _PeriodNormalPriority;
        protected readonly TimeSpan _PeriodLowPriority;
        public TimeSpan PeriodLowPriority => _PeriodLowPriority;

        protected DateTime _LastPollDatestamp;

        protected static readonly ILog Log = LogProvider.GetCurrentClassLogger();


        protected EntropySourceWithPeriod(TimeSpan periodNormalPriority, TimeSpan periodHighPriority, TimeSpan periodLowPriority)
        {
            if (periodNormalPriority < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(periodNormalPriority), periodNormalPriority, "Periods cannot be negative.");
            if (periodHighPriority < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(periodHighPriority), periodHighPriority, "Periods cannot be negative.");
            if (periodLowPriority < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(periodLowPriority), periodLowPriority, "Periods cannot be negative.");

            this._PeriodNormalPriority = periodNormalPriority;
            this._PeriodHighPriority = periodHighPriority;
            this._PeriodLowPriority = periodLowPriority;
        }

        public virtual void Dispose()
        {
        }

        public async Task<byte[]> GetEntropyAsync(EntropyPriority priority)
        {
            // Determine the next poll time.
            var period = PriorityToPeriod(priority);
            var nextRunTime = _LastPollDatestamp.Add(period);
            if (Log.IsTraceEnabled())
                Log.Trace("Period for priority {0} is {1}. Next run time after {2} (utc).", priority, period, nextRunTime);

            // If we're before the next run time, we don't run.
            if (nextRunTime >= DateTime.UtcNow)
            {
                Log.Trace("Not after next run time.");
                return null;
            }

            // Now we get the real entropy.
            Log.Trace("Reading entropy...");
            var result = await GetInternalEntropyAsync(priority);
            Log.Debug("Read {0:N0} bytes of entropy.", (result == null ? 0 : result.Length));

            // And update when we last ran.
            _LastPollDatestamp = DateTime.UtcNow;
            Log.Trace("Next run at {0} UTC (at priority {1}).", _LastPollDatestamp.Add(period), period);

            return result;
        }

        protected abstract Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority);

        private TimeSpan PriorityToPeriod(EntropyPriority priority) => priority == EntropyPriority.Normal ? _PeriodNormalPriority
                                                                     : priority == EntropyPriority.High ? _PeriodHighPriority
                                                                     : priority == EntropyPriority.Low ? _PeriodLowPriority
                                                                     : _PeriodNormalPriority;       // Shouldn't happen.
    }
}
