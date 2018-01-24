using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// A null entropy source. Just returns zero byte arrays.
    /// </summary>
    [SourceDisabledByDefault]
    public class NullSource : IEntropySource
    {
        public string Name => typeof(NullSource).FullName;

        public void Dispose()
        {
            // Nothing required.
        }

        public Task<EntropySourceInitialisationResult> Initialise(IEntropySourceConfig config, Func<IRandomNumberGenerator> prngFactory)
        {
            if (config.IsTruthy("NullSource.Enabled") == true)
                return Task.FromResult(EntropySourceInitialisationResult.Successful());
            else
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.DisabledByConfig, "NullSource is disabled by default. Set NullSource.Enabled in entropy source configuration to enable."));
        }

        public Task<byte[]> GetEntropyAsync()
        {
            return Task.FromResult(new byte[8]);
        }
    }
}
