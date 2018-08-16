using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Random;

namespace MurrayGrant.Terninger
{
    public static class PooledRandomExtensions
    {

        /// <summary>
        /// Creates a light weight PRNG from a Pooled Generator.
        /// If the Pooled Generator has not created its first seed, this will throw.
        /// </summary>
        public static IRandomNumberGenerator CreateCypherBasedGenerator(this PooledEntropyCprngGenerator pooledRng) => CreateCypherBasedGenerator(pooledRng, 1024);
        /// <summary>
        /// Creates a light weight PRNG from a Pooled Generator.
        /// If the Pooled Generator has not created its first seed, this will throw.
        /// </summary>
        public static IRandomNumberGenerator CreateCypherBasedGenerator(this PooledEntropyCprngGenerator pooledRng, int bufferSize)
        {
            if (pooledRng == null)
                throw new ArgumentNullException(nameof(pooledRng));

            var key = pooledRng.GetRandomBytes(32);
            var result = CypherBasedPrngGenerator.Create(key, outputBufferSize: bufferSize);
            return result;
        }

        /// <summary>
        /// Creates a light weight PRNG from a Pooled Generator.
        /// This will wait until the Pooled Generator has created its first seed.
        /// </summary>
        public static Task<IRandomNumberGenerator> CreateCypherBasedGeneratorAsync(this PooledEntropyCprngGenerator pooledRng)
        {
            if (pooledRng == null)
                throw new ArgumentNullException(nameof(pooledRng));

            if (pooledRng.ReseedCount > 0)
                return Task.FromResult(pooledRng.CreateCypherBasedGenerator());
            return WaitAndGet(pooledRng);
        }
        private static async Task<IRandomNumberGenerator> WaitAndGet(PooledEntropyCprngGenerator pooledRng)
        {
            await pooledRng.StartAndWaitForFirstSeed();
            return pooledRng.CreateCypherBasedGenerator();
        }

        /// <summary>
        /// Creates a light weight PRNG from a Pooled Generator.
        /// If the Pooled Generator has not created its first seed, this will use the system crypto random to derive a seed.
        /// </summary>
        public static IRandomNumberGenerator CreateCypherBasedGeneratorOrUninitialised(this PooledEntropyCprngGenerator pooledRng)
        {
            if (pooledRng == null)
                throw new ArgumentNullException(nameof(pooledRng));

            if (pooledRng.ReseedCount > 0)
                return pooledRng.CreateCypherBasedGenerator();
            else
                return CypherBasedPrngGenerator.CreateWithSystemCrngKey();
        }

    }
}
