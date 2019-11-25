using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Random;

namespace MurrayGrant.Terninger.EntropySources.Test
{
    /// <summary>
    /// A null entropy source. Just returns zero byte arrays.
    /// </summary>
    [AsyncHint(IsAsync.Never)]
    public class NullSource : IEntropySource
    {
        public string Name { get; set; }

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
