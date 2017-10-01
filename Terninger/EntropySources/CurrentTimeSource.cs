using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// Am entropy source based on the current system date and time.
    /// </summary>
    public class CurrentTimeSource : IEntropySource
    {
        public string Name => typeof(CurrentTimeSource).FullName;

        public void Dispose()
        {
            // Nothing required.
        }

        public Task<byte[]> GetEntropyAsync()
        {
            return Task.FromResult(BitConverter.GetBytes(DateTime.UtcNow.Ticks));
        }
    }
}
