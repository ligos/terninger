﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;

using BenchmarkDotNet.Attributes;

namespace MurrayGrant.Terninger.Perf.Benchmarks
{
    public class GenerateNumbers
    {
        private static readonly byte[] _ZeroKey32Bytes = new byte[32];
        private static readonly BlockCypherCprngGenerator _Generator = new BlockCypherCprngGenerator(_ZeroKey32Bytes);

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
