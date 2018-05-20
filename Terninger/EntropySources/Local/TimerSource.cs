using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;

namespace MurrayGrant.Terninger.EntropySources.Local
{
    /// <summary>
    /// Am entropy source based on a high precision timer.
    /// </summary>
    public class TimerSource : IEntropySource
    {
        public string Name { get; set; }

        private readonly Stopwatch _Timer = Stopwatch.StartNew();

        public void Dispose()
        {
            _Timer.Reset();
        }

        public Task<byte[]> GetEntropyAsync(EntropyPriority priority)
        {
            // Only returning the lower 32 bits of the timer, as the upper 32 bits will be pretty static.
            var result = BitConverter.GetBytes(unchecked((uint)_Timer.ElapsedTicks));
            return Task.FromResult(result);
        }
    }
}
