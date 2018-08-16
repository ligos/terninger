using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace MurrayGrant.Terninger.CryptoPrimitives
{
    /// <summary>
    /// Container for a hash primitive. The key is prepended to the counter data.
    /// </summary>
    public class HashCryptoPrimitive : ICryptoPrimitive
    {
        private readonly HashAlgorithm _Hash;
        private byte[] _Key;

        public static ICryptoPrimitive Sha256() => new HashCryptoPrimitive(SHA256.Create());
        public static ICryptoPrimitive Sha512() => new HashCryptoPrimitive(SHA512.Create());

        public HashCryptoPrimitive(HashAlgorithm hash)
        {
            if (hash == null)
                throw new ArgumentNullException(nameof(hash));
            this._Hash = hash;
            this._Key = new byte[hash.HashSize / 8];
        }

        public void Dispose()
        {
            if (_Key != null)
                Array.Clear(_Key, 0, _Key.Length);
        }

        public string Name => _Hash.GetType().Name;

        public int KeySizeBytes => _Hash.HashSize / 8;
        public int BlockSizeBytes => _Hash.HashSize / 8;

        public byte[] Key
        {
            get => _Key;
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                if (value.Length != KeySizeBytes)
                    throw new ArgumentOutOfRangeException("value", value.Length, $"New key was not {KeySizeBytes} bytes.");
                _Key = value;
            }
        }
        
        public ICryptoTransform CreateEncryptor() => new HashAndKeyTransform(_Hash, _Key);

        internal class HashAndKeyTransform : ICryptoTransform
        {
            private readonly HashAlgorithm _Hash;
            private readonly byte[] _KeyAndData;
            private readonly int _DataOffset;
            internal HashAndKeyTransform(HashAlgorithm hash, byte[] key)
            {
                _Hash = hash;
                _KeyAndData = new byte[key.Length * 2];
                _DataOffset = key.Length;
                Buffer.BlockCopy(key, 0, _KeyAndData, 0, key.Length);
            }

#if NETSTANDARD2_0 || NETFRAMEWORK
            public int InputBlockSize => _Hash.InputBlockSize;
            public int OutputBlockSize => _Hash.OutputBlockSize;
            public bool CanTransformMultipleBlocks => _Hash.CanTransformMultipleBlocks;
            public bool CanReuseTransform => _Hash.CanReuseTransform;
#else
            public int InputBlockSize => throw new PlatformNotSupportedException();
            public int OutputBlockSize => throw new PlatformNotSupportedException();
            public bool CanTransformMultipleBlocks => throw new PlatformNotSupportedException();
            public bool CanReuseTransform => throw new PlatformNotSupportedException();
#endif

            public void Dispose()
            {
                Array.Clear(_KeyAndData, 0, _KeyAndData.Length);
            }

            public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
            {
                // Incorporate the key and input into a single buffer, then hash.
                Buffer.BlockCopy(inputBuffer, inputOffset, _KeyAndData, _DataOffset, inputCount);
                var hashed = _Hash.ComputeHash(_KeyAndData);
                Buffer.BlockCopy(hashed, 0, outputBuffer, outputOffset, hashed.Length);
                return inputCount;
            }

            public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
            {
                throw new NotImplementedException();
            }
        }
    }
}
