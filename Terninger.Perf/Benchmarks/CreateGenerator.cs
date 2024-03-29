﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Random;

using BenchmarkDotNet.Attributes;

namespace MurrayGrant.Terninger.Perf.Benchmarks
{
    [SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net48)]
    [SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.NetCoreApp31)]
    [SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net60)]
    [MemoryDiagnoser]
    [AllStatisticsColumn]
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
