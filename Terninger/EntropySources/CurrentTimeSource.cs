using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// An entropy source based on the current system date and time.
    /// </summary>
    public class CurrentTimeSource : IEntropySource
    {
        private bool _HasRunOnce = false;

        public string Name => typeof(CurrentTimeSource).FullName;

        public void Dispose()
        {
            // Nothing required.
        }

        public Task<EntropySourceInitialisationResult> Initialise(IEntropySourceConfig config, Func<IRandomNumberGenerator> prngFactory)
        {
            if (config.IsTruthy("CurrentTimeSource.Enabled") == false)
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.DisabledByConfig, "CurrentTimeSource has been disabled in entropy source configuration."));
            else
                return Task.FromResult(EntropySourceInitialisationResult.Successful());
        }

        public Task<byte[]> GetEntropyAsync()
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
                result = BitConverter.GetBytes(unchecked((int)DateTime.UtcNow.Ticks));
            }
            
            return Task.FromResult(result);
        }
    }
}
