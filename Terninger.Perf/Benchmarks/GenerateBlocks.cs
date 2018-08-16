using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Random;

using BenchmarkDotNet.Attributes;

namespace MurrayGrant.Terninger.Perf.Benchmarks
{
    public class GenerateBlocks
    {
        private static readonly byte[] _ZeroKey32Bytes = new byte[32];
        private static readonly CypherBasedPrngGenerator _Generator = new CypherBasedPrngGenerator(_ZeroKey32Bytes);
        private static readonly int _BlockSize = _Generator.BlockSizeBytes;

        [Benchmark]
        public byte[] OneBlock()
        {
            return _Generator.GetRandomBytes(_BlockSize);
        }
        [Benchmark]
        public byte[] TenBlocks()
        {
            return _Generator.GetRandomBytes(_BlockSize * 10);
        }
        [Benchmark]
        public byte[] ThirtyTwoKBytes()
        {
            return _Generator.GetRandomBytes(32 * 1024);
        }
    }
}
