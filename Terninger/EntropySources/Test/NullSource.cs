using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;

namespace MurrayGrant.Terninger.EntropySources.Test
{
    /// <summary>
    /// A null entropy source. Just returns zero byte arrays.
    /// </summary>
    public class NullSource : IEntropySource
    {
        public string Name => typeof(NullSource).FullName;

        public void Dispose()
        {
            // Nothing required.
        }

        public Task<byte[]> GetEntropyAsync(EntropyPriority priority)
        {
            return Task.FromResult(new byte[8]);
        }
    }
}
