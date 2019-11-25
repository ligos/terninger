using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Random;

namespace MurrayGrant.Terninger.EntropySources.Local
{
    /// <summary>
    /// An entropy source based on the current system date and time.
    /// </summary>
    [AsyncHint(IsAsync.Never)]
    public class CurrentTimeSource : IEntropySource
    {
        private bool _HasRunOnce = false;

        public string Name { get; set; }

        public void Dispose()
        {
            // Nothing required.
        }

        public Task<byte[]> GetEntropyAsync(EntropyPriority priority)
        {
            byte[] result;
            if (!_HasRunOnce)
            {
                // On first run, we include the entire 64 bit value. 
                result = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
                _HasRunOnce = true;
            }
            else
            {
                // All subsequent runs only include the lower 32 bits.
                result = BitConverter.GetBytes(unchecked((uint)DateTime.UtcNow.Ticks));
            }
            
            return Task.FromResult(result);
        }
    }
}
