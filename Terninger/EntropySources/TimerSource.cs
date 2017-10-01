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

        public Task<EntropySourceInitialisationResult> Initialise()
        {
            return Task.FromResult(EntropySourceInitialisationResult.Successful);
        }

        public Task<byte[]> GetEntropyAsync()
        {
            return Task.FromResult(BitConverter.GetBytes(_Timer.ElapsedTicks));
        }
    }
}
