using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;

using BenchmarkDotNet.Attributes;

namespace MurrayGrant.Terninger.Perf.Benchmarks
{
    public class CreateGenerator
    {
        private static readonly byte[] _ZeroKey32Bytes = new byte[32];

        [Benchmark]
        public IRandomNumberGenerator CreateDefault()
        {
            return new CypherBasedPrngGenerator(_ZeroKey32Bytes);
        }
    }
}
