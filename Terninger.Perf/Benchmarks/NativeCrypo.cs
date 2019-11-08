using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

using BenchmarkDotNet.Attributes;

namespace MurrayGrant.Terninger.Perf.Benchmarks
{
    [SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net472)]
    [SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.NetCoreApp21)]
    [SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.NetCoreApp30)]
    [MemoryDiagnoser]
    [AllStatisticsColumn]
    public class NativeCrypo
    {
        private Aes _Aes256;
        private AesCng _Aes256Cng;
        private AesCryptoServiceProvider _Aes256Csp;
        private AesManaged _Aes256Managed;

        private ICryptoTransform _Aes256Encryptor;
        private ICryptoTransform _Aes256ManagedEncryptor;
        private ICryptoTransform _Aes256CngEncryptor;
        private ICryptoTransform _Aes256CspEncryptor;

        private static readonly int BlockSizeBytes = 256 / 8;

        private static readonly byte[] _Block1In = new byte[BlockSizeBytes];
        private static readonly byte[] _Block1Out = new byte[BlockSizeBytes];

        private static readonly byte[] _Block8In = new byte[BlockSizeBytes * 8];
        private static readonly byte[] _Block8Out = new byte[BlockSizeBytes * 8];

        private static readonly byte[] _Block32In = new byte[BlockSizeBytes * 32];
        private static readonly byte[] _Block32Out = new byte[BlockSizeBytes* 32];

        [GlobalSetup]
        public void Setup()
        {
            _Aes256 = Aes.Create();
            _Aes256.KeySize = 256;
            _Aes256Cng = new AesCng { KeySize = 256 };
            _Aes256Csp = new AesCryptoServiceProvider() { KeySize = 256 };
            _Aes256Managed = new AesManaged() { KeySize = 256 };

            _Aes256Encryptor = _Aes256.CreateEncryptor();
            _Aes256ManagedEncryptor = _Aes256Managed.CreateEncryptor();
            _Aes256CngEncryptor = _Aes256Cng.CreateEncryptor();
            _Aes256CspEncryptor = _Aes256Csp.CreateEncryptor();
        }

        [Benchmark]
        public Aes Create()
        {
            var result = Aes.Create();
            result.KeySize = 256;
            return result;
        }
        [Benchmark]
        public AesManaged CreateManaged()
        {
            return new AesManaged() { KeySize = 256 };
        }
        [Benchmark]
        public AesCng CreateCng()
        {
            return new AesCng { KeySize = 256 };
        }
        [Benchmark]
        public AesCryptoServiceProvider CreateCsp()
        {
            return new AesCryptoServiceProvider() { KeySize = 256 };
        }

        [Benchmark]
        public ICryptoTransform CreateEncryptorCng()
        {
            return _Aes256Cng.CreateEncryptor();
        }
        [Benchmark]
        public ICryptoTransform CreateEncryptorCsp()
        {
            return _Aes256Csp.CreateEncryptor();
        }
        [Benchmark]
        public ICryptoTransform CreateEncryptorManaged()
        {
            return _Aes256Managed.CreateEncryptor();
        }
        [Benchmark]
        public ICryptoTransform CreateEncryptor()
        {
            return _Aes256.CreateEncryptor();
        }

        [Benchmark]
        public byte[] Encrypt1BlockCng()
        {
            _Aes256CngEncryptor.TransformBlock(_Block1In, 0, _Block1In.Length, _Block1Out, 0);
            return _Block1Out;
        }
        [Benchmark]
        public byte[] Encrypt1BlockCsp()
        {
            _Aes256CspEncryptor.TransformBlock(_Block1In, 0, _Block1In.Length, _Block1Out, 0);
            return _Block1Out;
        }
        [Benchmark]
        public byte[] Encrypt1BlockManaged()
        {
            _Aes256ManagedEncryptor.TransformBlock(_Block1In, 0, _Block1In.Length, _Block1Out, 0);
            return _Block1Out;
        }
        [Benchmark]
        public byte[] Encrypt1Block()
        {
            _Aes256Encryptor.TransformBlock(_Block1In, 0, _Block1In.Length, _Block1Out, 0);
            return _Block1Out;
        }

        [Benchmark]
        public byte[] Encrypt8BlockCng()
        {
            _Aes256CngEncryptor.TransformBlock(_Block8In, 0, _Block8In.Length, _Block8Out, 0);
            return _Block8Out;
        }
        [Benchmark]
        public byte[] Encrypt8BlockCsp()
        {
            _Aes256CspEncryptor.TransformBlock(_Block8In, 0, _Block8In.Length, _Block8Out, 0);
            return _Block8Out;
        }
        [Benchmark]
        public byte[] Encrypt8BlockManaged()
        {
            _Aes256ManagedEncryptor.TransformBlock(_Block8In, 0, _Block8In.Length, _Block8Out, 0);
            return _Block8Out;
        }
        [Benchmark]
        public byte[] Encrypt8Block()
        {
            _Aes256Encryptor.TransformBlock(_Block8In, 0, _Block8In.Length, _Block8Out, 0);
            return _Block8Out;
        }

        [Benchmark]
        public byte[] Encrypt32BlockCng()
        {
            _Aes256CngEncryptor.TransformBlock(_Block32In, 0, _Block32In.Length, _Block32Out, 0);
            return _Block32Out;
        }
        [Benchmark]
        public byte[] Encrypt32BlockCsp()
        {
            _Aes256CspEncryptor.TransformBlock(_Block32In, 0, _Block32In.Length, _Block32Out, 0);
            return _Block32Out;
        }
        [Benchmark]
        public byte[] Encrypt32BlockManaged()
        {
            _Aes256ManagedEncryptor.TransformBlock(_Block32In, 0, _Block32In.Length, _Block32Out, 0);
            return _Block32Out;
        }
        [Benchmark]
        public byte[] Encrypt32Block()
        {
            _Aes256Encryptor.TransformBlock(_Block32In, 0, _Block32In.Length, _Block32Out, 0);
            return _Block32Out;
        }
    }
}
