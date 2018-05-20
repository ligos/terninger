using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;

namespace MurrayGrant.Terninger.EntropySources.Local
{
    /// <summary>
    /// Am entropy source based on managed memory / garbage collector stats.
    /// </summary>
    public class GCMemorySource : IEntropySource
    {
        public string Name { get; set; }

        public void Dispose()
        {
        }

        public Task<byte[]> GetEntropyAsync(EntropyPriority priority)
        {
            uint gcCollections = 0;
            for (int i = GC.MaxGeneration; i >= 0; i--)
                gcCollections = (gcCollections << 5) ^ (uint)GC.CollectionCount(i);
            var low = BitConverter.GetBytes(gcCollections);

            var gcTotalMemory = GC.GetTotalMemory(false);
            var high = BitConverter.GetBytes((uint)gcTotalMemory);

            var result = new byte[8];
            Buffer.BlockCopy(low, 0, result, 0, low.Length);
            Buffer.BlockCopy(high, 0, result, 4, high.Length);

            return Task.FromResult(result);
        }
    }
}
