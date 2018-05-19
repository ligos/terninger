using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.Accumulator;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Local;
using MurrayGrant.Terninger.EntropySources.Network;

namespace MurrayGrant.Terninger
{
    public class RandomGenerator
    {
        public static readonly Uri Website = new Uri("https://bitbucket/ligos/terninger");

        /// <summary>
        /// Creates a pooled random number generator that conforms to the Fortuna spec.
        /// This is more conservative than Terninger; no randomised pools or low priority mode.
        /// </summary>
        public static PooledEntropyCprngGenerator CreateFortuna() => 
                        PooledEntropyCprngGenerator.Create(
                            initialisedSources: StandardSources(), 
                            accumulator: new EntropyAccumulator(32, 0),
                            config: new PooledEntropyCprngGenerator.PooledGeneratorConfig()
                            {
                                ReseedCountBeforeSwitchToLowPriority = Int32.MaxValue,
                                TimeBeforeSwitchToLowPriority = TimeSpan.MaxValue,
                            }
                        );

        /// <summary>
        /// Creates a pooled random number generator that makes improvements and changes to the Fortuna spec.
        /// This allows for less CPU usage, uses randomised pools, but isn't "official".
        /// </summary>
        public static PooledEntropyCprngGenerator CreateTerninger() =>
                        PooledEntropyCprngGenerator.Create(
                            initialisedSources: StandardSources(),
                            accumulator: new EntropyAccumulator(16, 16, CypherBasedPrngGenerator.CreateWithCheapKey())
                        );


        /// <summary>
        /// The standard set of entropy sources, local to this computer.
        /// These are included by default in RandomNumberGenerator.CreateFortuna() and RandomNumberGenerator.CreateTerninger(). 
        /// </summary>
        public static IEnumerable<IEntropySource> StandardSources() => new IEntropySource[]
        {
            new CurrentTimeSource(),
            new GCMemorySource(),
            new TimerSource(),
            new CryptoRandomSource(),
            new NetworkStatsSource(),
            new ProcessStatsSource(),
        };

        /// <summary>
        /// An additional set of sources which gather entropy from external network sources such as ping timings, web content and 3rd party entropy generators.
        /// </summary>
        /// <param name="userAgent">A user agent string to include in web requests. Highly recommended to identify yourself in case of problems.</param>
        /// <param name="hotBitsApiKey">API key for true random source at https://www.fourmilab.ch/hotbits </param>
        /// <param name="randomOrgApiKey">API for https://api.random.org </param>
        public static IEnumerable<IEntropySource> NetworkSources(string userAgent = null, string hotBitsApiKey = null, Guid? randomOrgApiKey = null) => new IEntropySource[]
        {
            new PingStatsSource(),
            new ExternalWebContentSource(userAgent),
            new AnuExternalRandomSource(userAgent),
            new BeaconNistExternalRandomSource(userAgent),
            new HotbitsExternalRandomSource(userAgent, hotBitsApiKey),
            new RandomNumbersInfoExternalRandomSource(userAgent),
            new RandomOrgExternalRandomSource(userAgent, randomOrgApiKey.GetValueOrDefault()),
            new RandomServerExternalRandomSource(userAgent),
        };

        /// <summary>
        /// If you have one-off externally derived entropy, you can add it here.
        /// If you have a stream of external entropy, you should add your own UserSuppliedSource and call SetEntropy().
        /// </summary>
        public static IEntropySource UserSuppliedEntropy(byte[] entropy) => new UserSuppliedSource(entropy);


        /// <summary>
        /// Create a random number generator based on a cryptographic cypher using a random seed based on the system crypto random number generator.
        /// </summary>
        public static IRandomNumberGenerator CreateCypherBasedGenerator() => CypherBasedPrngGenerator.CreateWithSystemCrngKey();
        /// <summary>
        /// Create a random number generator based on a cryptographic cypher using the supplied seed.
        /// </summary>
        public static IRandomNumberGenerator CreateCypherBasedGenerator(byte[] seed) => CypherBasedPrngGenerator.Create(seed);

        /// <summary>
        /// Create an unbuffered (slower but more secure) random number generator based on a cryptographic cypher using a random seed based on the system crypto random number generator.
        /// </summary>
        public static IRandomNumberGenerator CreateUnbufferedCypherBasedGenerator() => CypherBasedPrngGenerator.CreateWithSystemCrngKey(outputBufferSize: 0);
        /// <summary>
        /// Create an unbuffered (slower but more secure) random number generator based on a cryptographic cypher using the supplied seed.
        /// </summary>
        public static IRandomNumberGenerator CreateUnbufferedCypherBasedGenerator(byte[] seed) => CypherBasedPrngGenerator.Create(seed, outputBufferSize: 0);

    }
}
