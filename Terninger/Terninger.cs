using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.Accumulator;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Local;

namespace MurrayGrant.Terninger
{
    /// <summary>
    /// Terninger random number generator: and implementation of Fortuna in C#.
    /// </summary>
    public static class RandomGenerator
    {
        /// <summary>
        /// For more information.
        /// </summary>
        public static readonly Uri Website = new Uri("https://bitbucket/ligos/terninger");

        /// <summary>
        /// Creates a pooled random number generator that conforms to the Fortuna spec.
        /// This is more conservative than Terninger; no randomised pools or low priority mode.
        /// </summary>
        public static PooledEntropyCprngGenerator CreateFortuna() => 
                        PooledEntropyCprngGenerator.Create(
                            initialisedSources: BasicSources(), 
                            accumulator: new EntropyAccumulator(32, 0),
                            config: new PooledEntropyCprngGenerator.PooledGeneratorConfig()
                            {
                                // Set the ways to enter low priority mode so high they should never be hit.
                                ReseedCountBeforeSwitchToLowPriority = Int32.MaxValue,
                                TimeBeforeSwitchToLowPriority = TimeSpan.MaxValue,
                                // Reseed more aggressively as well.
                                MaximumBytesGeneratedBeforeReseed = 1024L * 1024L,
                                MinimumTimeBetweenReseeds = TimeSpan.FromHours(1),
                            }
                        );

        /// <summary>
        /// Creates a pooled random number generator that makes improvements and changes to the Fortuna spec.
        /// This allows for less CPU usage, uses randomised pools, but isn't "official".
        /// </summary>
        public static PooledEntropyCprngGenerator CreateTerninger() =>
                        PooledEntropyCprngGenerator.Create(
                            initialisedSources: BasicSources(),
                            accumulator: new EntropyAccumulator(16, 16, CypherBasedPrngGenerator.CreateWithCheapKey())
                        );


        /// <summary>
        /// The basic standard set of entropy sources, local to this computer.
        /// These are included by default in RandomNumberGenerator.CreateFortuna() and RandomNumberGenerator.CreateTerninger(). 
        /// </summary>
        public static IEnumerable<IEntropySource> BasicSources(CryptoRandomSource.Configuration cryptoRandomConfig = null) => new IEntropySource[]
        {
            new CurrentTimeSource(),
            new GCMemorySource(),
            new TimerSource(),
            new CryptoRandomSource(cryptoRandomConfig),
        };


        /// <summary>
        /// If you have one-off externally derived entropy, you can add it here.
        /// If you have a stream of external entropy, you should add your own UserSuppliedSource and call SetEntropy() as required.
        /// </summary>
        public static IEntropySource UserSuppliedEntropy(byte[] entropy) => new UserSuppliedSource(entropy);


        /// <summary>
        /// Create a random number generator based on a cryptographic cypher using a random seed based on the system crypto random number generator.
        /// </summary>
        public static IRandomNumberGenerator CreateCypherBasedGenerator() => CypherBasedPrngGenerator.CreateWithSystemCrngKey();
        /// <summary>
        /// Create a random number generator based on a cryptographic cypher using the supplied seed.
        /// This generator is deterministic based on the seed (that is, the same seed gives the same sequence of random numbers).
        /// </summary>
        public static IRandomNumberGenerator CreateCypherBasedGenerator(byte[] seed) => CypherBasedPrngGenerator.Create(seed);

        /// <summary>
        /// Create an unbuffered (slower but more secure) random number generator based on a cryptographic cypher using a random seed based on the system crypto random number generator.
        /// </summary>
        public static IRandomNumberGenerator CreateUnbufferedCypherBasedGenerator() => CypherBasedPrngGenerator.CreateWithSystemCrngKey(outputBufferSize: 0);
        /// <summary>
        /// Create an unbuffered (slower but more secure) random number generator based on a cryptographic cypher using the supplied seed.
        /// This generator is deterministic based on the seed (that is, the same seed gives the same sequence of random numbers).
        /// </summary>
        public static IRandomNumberGenerator CreateUnbufferedCypherBasedGenerator(byte[] seed) => CypherBasedPrngGenerator.Create(seed, outputBufferSize: 0);

    }
}
