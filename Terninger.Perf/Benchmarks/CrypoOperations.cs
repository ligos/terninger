using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

using BenchmarkDotNet.Attributes;

namespace MurrayGrant.Terninger.Perf.Benchmarks
{
    [SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net48)]
    [SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.NetCoreApp31)]
    [SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net60)]
    [MemoryDiagnoser]
    [AllStatisticsColumn]
    public class CrypoOperations
    {
        private static readonly Aes _Aes128 = new AesCryptoServiceProvider() { KeySize = 128 };
        private static readonly Aes _Aes256 = new AesCryptoServiceProvider() { KeySize = 256 };
        private static readonly ICryptoTransform _Aes128ct = _Aes128.CreateEncryptor();
        private static readonly ICryptoTransform _Aes256ct = _Aes256.CreateEncryptor();

        private static readonly int BlockSizeBytes = _Aes128.BlockSize / 8;

        private static readonly byte[] _Block1In = new byte[BlockSizeBytes];
        private static readonly byte[] _Block1Out = new byte[BlockSizeBytes];

        private static readonly byte[] _Block8In = new byte[BlockSizeBytes * 8];
        private static readonly byte[] _Block8Out = new byte[BlockSizeBytes * 8];

        private static readonly byte[] _Block32In = new byte[BlockSizeBytes * 32];
        private static readonly byte[] _Block32Out = new byte[BlockSizeBytes* 32];

        private static readonly byte[] _Block128In = new byte[BlockSizeBytes * 128];
        private static readonly byte[] _Block128Out = new byte[BlockSizeBytes * 128];

        private static readonly byte[] _Block512In = new byte[BlockSizeBytes * 512];
        private static readonly byte[] _Block512Out = new byte[BlockSizeBytes * 512];

        [Benchmark]
        public Aes Create()
        {
            // ~54us on Murray's laptop.
            return new AesCryptoServiceProvider();
        }

        [Benchmark]
        public ICryptoTransform CreateEncryptor128()
        {
            // ~4us on Murray's laptop.
            return _Aes128.CreateEncryptor();
        }
        [Benchmark]
        public ICryptoTransform CreateEncryptor256()
        {
            // ~4us on Murray's laptop.
            return _Aes256.CreateEncryptor();
        }

        [Benchmark]
        public byte[] BlockCopy1()
        {
            // ~17ns on Murray's laptop.
            Buffer.BlockCopy(_Block1In, 0, _Block1Out, 0, _Block1In.Length);
            return _Block1Out;
        }
        [Benchmark]
        public byte[] Encrypt1Block128()
        {
            // ~500ns on Murray's laptop.
            _Aes128ct.TransformBlock(_Block1In, 0, _Block1In.Length, _Block1Out, 0);
            return _Block1Out;
        }
        [Benchmark]
        public byte[] Encrypt1Block256()
        {
            // ~540ns on Murray's laptop.
            _Aes256ct.TransformBlock(_Block1In, 0, _Block1In.Length, _Block1Out, 0);
            return _Block1Out;
        }

        [Benchmark]
        public byte[] BlockCopy8()
        {
            // ~25ns on Murray's laptop.
            Buffer.BlockCopy(_Block8In, 0, _Block8Out, 0, _Block8In.Length);
            return _Block8Out;
        }
        [Benchmark]
        public byte[] Encrypt8Block128ForLoop()
        {
            // ~4us on Murray's laptop.
            for (int i = 0; i < 8; i++)
                _Aes128ct.TransformBlock(_Block8In, i * BlockSizeBytes, BlockSizeBytes, _Block8Out, i * BlockSizeBytes);
            return _Block8Out;
        }
        [Benchmark]
        public byte[] Encrypt8Block128()
        {
            // ~1.1us on Murray's laptop.
            _Aes128ct.TransformBlock(_Block8In, 0, _Block8In.Length, _Block8Out, 0);
            return _Block8Out;
        }
        [Benchmark]
        public byte[] Encrypt8Block256()
        {
            // ~1.4us on Murray's laptop.
            _Aes256ct.TransformBlock(_Block8In, 0, _Block8In.Length, _Block8Out, 0);
            return _Block8Out;
        }

        [Benchmark]
        public byte[] BlockCopy32()
        {
            // ~43ns on Murray's laptop.
            Buffer.BlockCopy(_Block32In, 0, _Block32Out, 0, _Block32In.Length);
            return _Block32Out;
        }
        [Benchmark]
        public byte[] Encrypt32Block128ForLoop()
        {
            // ~16.8us on Murray's laptop.
            for (int i = 0; i < 32; i++)
                _Aes128ct.TransformBlock(_Block32In, i * BlockSizeBytes, BlockSizeBytes, _Block32Out, i * BlockSizeBytes);
            return _Block32Out;
        }
        [Benchmark]
        public byte[] Encrypt32Block128()
        {
            // ~3.1us on Murray's laptop.
            _Aes128ct.TransformBlock(_Block32In, 0, _Block32In.Length, _Block32Out, 0);
            return _Block32Out;
        }
        [Benchmark]
        public byte[] Encrypt32Block256()
        {
            // ~4.3us on Murray's laptop.
            _Aes256ct.TransformBlock(_Block32In, 0, _Block32In.Length, _Block32Out, 0);
            return _Block32Out;
        }

        [Benchmark]
        public byte[] BlockCopy128()
        {
            // ~115ns on Murray's laptop.
            Buffer.BlockCopy(_Block128In, 0, _Block128Out, 0, _Block128In.Length);
            return _Block128Out;
        }
        [Benchmark]
        public byte[] Encrypt128Block128ForLoop()
        {
            // ~65us on Murray's laptop.
            for (int i = 0; i < 128; i++)
                _Aes128ct.TransformBlock(_Block128In, i * BlockSizeBytes, BlockSizeBytes, _Block128Out, i * BlockSizeBytes);
            return _Block128Out;
        }
        [Benchmark]
        public byte[] Encrypt128Block128()
        {
            // ~11.5us on Murray's laptop.
            _Aes128ct.TransformBlock(_Block128In, 0, _Block128In.Length, _Block128Out, 0);
            return _Block128Out;
        }
        [Benchmark]
        public byte[] Encrypt128Block256()
        {
            // ~15.7us on Murray's laptop.
            _Aes256ct.TransformBlock(_Block128In, 0, _Block128In.Length, _Block128Out, 0);
            return _Block128Out;
        }

        [Benchmark]
        public byte[] BlockCopy512()
        {
            // ~780ns on Murray's laptop.
            Buffer.BlockCopy(_Block512In, 0, _Block512Out, 0, _Block512In.Length);
            return _Block512Out;
        }
        [Benchmark]
        public byte[] Encrypt512Block128ForLoop()
        {
            // ~270us on Murray's laptop.
            for (int i = 0; i < 512; i++)
                _Aes128ct.TransformBlock(_Block512In, i * BlockSizeBytes, BlockSizeBytes, _Block512Out, i * BlockSizeBytes);
            return _Block512Out;
        }
        [Benchmark]
        public byte[] Encrypt512Block128()
        {
            // ~45.5us on Murray's laptop.
            _Aes128ct.TransformBlock(_Block512In, 0, _Block512In.Length, _Block512Out, 0);
            return _Block512Out;
        }
        [Benchmark]
        public byte[] Encrypt512Block256()
        {
            // ~62us on Murray's laptop.
            _Aes256ct.TransformBlock(_Block512In, 0, _Block512In.Length, _Block512Out, 0);
            return _Block512Out;
        }

    }
}
