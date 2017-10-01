using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// Am entropy source based on managed memory / garbage collector stats.
    /// </summary>
    public class GCMemorySource : IEntropySource
    {
        public string Name => typeof(GCMemorySource).FullName;

        public void Dispose()
        {
        }

        public Task<EntropySourceInitialisationResult> Initialise()
        {
            return Task.FromResult(EntropySourceInitialisationResult.Successful);
        }

        public Task<byte[]> GetEntropyAsync()
        {
            long gcCollections = 0L;
            for (int i = GC.MaxGeneration - 1; i >= 0; i--)
                gcCollections = (gcCollections << 12) ^ (long)GC.CollectionCount(i);
            var low = BitConverter.GetBytes(gcCollections);

            var gcTotalMemory = GC.GetTotalMemory(false);
            var high = BitConverter.GetBytes(gcTotalMemory);

            var result = new byte[16];
            Buffer.BlockCopy(low, 0, result, 0, low.Length);
            Buffer.BlockCopy(high, 0, result, 8, high.Length);

            return Task.FromResult(result);
        }
    }
}
