using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// Am entropy source based on a high precision timer.
    /// </summary>
    public class TimerSource : IEntropySource
    {
        public string Name => typeof(TimerSource).FullName;
        private readonly Stopwatch _Timer = Stopwatch.StartNew();

        public void Dispose()
        {
            _Timer.Reset();
        }

        public Task<EntropySourceInitialisationResult> Initialise(IEntropySourceConfig config, Func<IRandomNumberGenerator> prngFactory)
        {
            if (config.IsTruthy("TimerSource.Enabled") == false)
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.DisabledByConfig, "TimerSource has been disabled in entropy source configuration."));
            else
                return Task.FromResult(EntropySourceInitialisationResult.Successful());
        }

        public Task<byte[]> GetEntropyAsync()
        {
            // Only returning the lower 32 bits of the timer, as the upper 32 bits will be pretty static.
            var result = BitConverter.GetBytes(unchecked((int)_Timer.ElapsedTicks));
            return Task.FromResult(result);
        }
    }
}
