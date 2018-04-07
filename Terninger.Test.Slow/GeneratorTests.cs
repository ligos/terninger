using System;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Accumulator;
using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Test;
using MurrayGrant.Terninger.EntropySources.Local;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using MurrayGrant.Terninger.Helpers;

namespace MurrayGrant.Terninger.Test.Slow
{
    [TestClass]
    public class GeneratorTests
    {
        private static readonly byte[] _ZeroKey32Bytes = new byte[32];

        [TestMethod]
        [TestCategory("Fuzzing")]
        public async Task CypherGenerator_ZeroKeyNoExtraEntropy()
        {
            var rng = CypherBasedPrngGenerator.Create(_ZeroKey32Bytes);

            await FuzzGenerator(10000, 1, 64, rng, nameof(CypherBasedPrngGenerator) + "_ZeroKey");
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public async Task CypherGenerator_CheapKeyNoExtraEntropy()
        {
            var rng = CypherBasedPrngGenerator.CreateWithCheapKey();

            await FuzzGenerator(10000, 1, 64, rng, nameof(CypherBasedPrngGenerator) + "_CheapKey");
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public async Task CypherGenerator_LocalAndCheapKeyNoExtraEntropy()
        {
            var key = SHA256.Create().ComputeHash((await StaticLocalEntropy.Get32()).Concat(CheapEntropy.Get32()).ToArray());
            var rng = CypherBasedPrngGenerator.Create(key);

            await FuzzGenerator(10000, 1, 64, rng, nameof(CypherBasedPrngGenerator) + "_LocalAndCheapKey");
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public async Task CypherGenerator_LocalAndCheapKeyWithExtraEntropy()
        {
            var key = SHA256.Create().ComputeHash((await StaticLocalEntropy.Get32()).Concat(CheapEntropy.Get32()).ToArray());
            var rng = CypherBasedPrngGenerator.Create(key, additionalEntropyGetter: CheapEntropy.Get16);

            await FuzzGenerator(10000, 1, 64, rng, nameof(CypherBasedPrngGenerator) + "_LocalAndCheapKeyAndExtraEntropy");
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public async Task PooledGenerator()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(64), new CurrentTimeSource(), new GCMemorySource(), new NetworkStatsSource(), new ProcessStatsSource(), new TimerSource() };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng = new PooledEntropyCprngGenerator(sources, acc);

            await rng.StartAndWaitForFirstSeed();

            await FuzzGenerator(10000, 1, 64, rng, nameof(PooledEntropyCprngGenerator));

            await rng.Stop();
        }


        private async Task FuzzGenerator(int iterations, int bytesPerRequestMin, int bytesPerRequestMax, IRandomNumberGenerator generator, string filename)
        {
            var rng = new StandardRandomWrapperGenerator();
            using (var sw = new StreamWriter(filename + ".txt", false, Encoding.UTF8))
            {
                await sw.WriteLineAsync($"{generator.GetType().FullName} - {iterations:N0} iterations");

                for (int i = 0; i < iterations; i++)
                {
                    var bytesToGet = rng.GetRandomInt32(bytesPerRequestMin, bytesPerRequestMax);
                    var bytes = generator.GetRandomBytes(bytesToGet);
                    if (bytes == null)
                        await sw.WriteLineAsync("<null>");
                    else
                        await sw.WriteLineAsync(bytes.ToHexString());
                }
            }
        }
    }
}
