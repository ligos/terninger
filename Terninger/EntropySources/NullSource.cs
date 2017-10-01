using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.EntropySources
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

        public Task<byte[]> GetEntropyAsync()
        {
            return Task.FromResult(new byte[8]);
        }
    }
}
