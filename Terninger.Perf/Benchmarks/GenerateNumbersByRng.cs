using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Random;
using Rand = System.Random;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Code;

namespace MurrayGrant.Terninger.Perf.Benchmarks
{
    public class GenerateNumbersByRng
    {
        private static readonly byte[] _ZeroKey32Bytes = new byte[32];

        private readonly CypherBasedPrngGenerator _TerningerCypher = CypherBasedPrngGenerator.Create(_ZeroKey32Bytes);
        private readonly PooledEntropyCprngGenerator _TerningerPooled = RandomGenerator.CreateTerninger().StartAndWaitForSeedAsync().GetAwaiter().GetResult();
        private readonly CryptoRandomWrapperGenerator _SystemCryptoRandom = new CryptoRandomWrapperGenerator();
        private readonly StandardRandomWrapperGenerator _SystemRandom = new StandardRandomWrapperGenerator(new Rand(1));

        [Benchmark]
        public int TerningerCypher()
        {
            return _TerningerCypher.GetRandomInt32();
        }
        [Benchmark]
        public int CryptoRandom()
        {
            return _SystemCryptoRandom.GetRandomInt32();
        }
        [Benchmark]
        public int SystemRandom()
        {
            return _SystemRandom.GetRandomInt32();
        }
        [Benchmark]
        public int TerningerPooled()
        {
            return _TerningerPooled.GetRandomInt32();
        }
    }
}
