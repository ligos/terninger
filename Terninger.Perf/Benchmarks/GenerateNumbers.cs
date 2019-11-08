using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Random;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Code;

namespace MurrayGrant.Terninger.Perf.Benchmarks
{
    [SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net472)]
    [SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.NetCoreApp21)]
    [SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.NetCoreApp30)]
    [MemoryDiagnoser]
    [AllStatisticsColumn]
    public class GenerateNumbers
    {
        private static readonly byte[] _ZeroKey32Bytes = new byte[32];
        [Params(0, 1024, 4096)]
        public int BufferSize;

        private CypherBasedPrngGenerator _Generator;

        [GlobalSetup]
        public void Setup()
        {
            _Generator = CypherBasedPrngGenerator.Create(_ZeroKey32Bytes, outputBufferSize: BufferSize);
        }

        [Benchmark]
        public bool Boolean()
        {
            return _Generator.GetRandomBoolean();
        }
        [Benchmark]
        public uint UInt32()
        {
            return _Generator.GetRandomUInt32();
        }
        [Benchmark]
        public int Int32()
        {
            return _Generator.GetRandomInt32();
        }
        [Benchmark]
        public int Int32_Range32()
        {
            return _Generator.GetRandomInt32(32);
        }
        [Benchmark]
        public int Int32_Range33()
        {
            return _Generator.GetRandomInt32(33);
        }
        [Benchmark]
        public int Int32_Range47()
        {
            return _Generator.GetRandomInt32(47);
        }
        [Benchmark]
        public ulong UInt64()
        {
            return _Generator.GetRandomUInt64();
        }
        [Benchmark]
        public long Int64()
        {
            return _Generator.GetRandomInt64();
        }
        [Benchmark]
        public float Single()
        {
            return _Generator.GetRandomSingle();
        }
        [Benchmark]
        public double Double()
        {
            return _Generator.GetRandomDouble();
        }
        [Benchmark]
        public decimal Decimal()
        {
            return _Generator.GetRandomDecimal();
        }
        [Benchmark]
        public Guid Guid()
        {
            return _Generator.GetRandomGuid();
        }
    }
}
