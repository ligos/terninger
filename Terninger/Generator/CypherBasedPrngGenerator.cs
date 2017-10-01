using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.CryptoPrimitives;

using BigMath;

namespace MurrayGrant.Terninger.Generator
{
    public class CypherBasedPrngGenerator : IRandomNumberGenerator, IDisposable
    {
        // A block cypher or keyed hash algorithm.
        // Default: AES with 256 bit key, as specified in 9.4
        // K, as specified in 9.4.1, is stored in _Cypher.Key
        // Hash and HMAC based primitives are also supported.
        // FUTURE: allow stream cyphers
        private readonly ICryptoPrimitive _CryptoPrimitive;

        // C, as specified in 9.4.1
        // A 128, 256 or 512 bit integer, and a string of bytes to be encrypted.
        private readonly CypherCounter _Counter;           
        
        private readonly int _BlockSizeInBytes;     // 16, 32 or 64 bytes. Most block cyphers are 16 bytes; Rijndael, HMACs and hashes can be longer.
        private readonly int _KeySizeInBytes;       // 16 or 32 bytes. 16 bytes allows for AES 128.
        private readonly int _RekeyBlockCount;      // Number of blocks required to re-key. At least key size.
        private readonly int _RekeyByteCount;       // Number of bytes required to re-key. At least key size.

        // Defaults to SHA512; section 9.4 specifies SHA256 as default, but SHA512 should work in all cases and provide no worse entropy.
        private readonly HashAlgorithm _HashFunction;

        // Additional to Fortuna spec: an source of cheap entropy which can be injected during re-seeds.
        // Note: the use of this will make this non-deterministic.
        private readonly Func<byte[]> _AdditionalEntropyGetter;     

        private bool _Disposed = false;

        public int MaxRequestBytes => 2 << 20;      // As sepecified in 9.4.4.
        public int BlockSizeBytes => _BlockSizeInBytes;

        public Int128 BytesRequested { get; private set; }
        public Int128 BytesGenerated { get; private set; }


        /// <summary>
        /// Initialise the CPRNG with the given key material, and default cypher (AES 256) and hash algorithm (SHA256), and zero counter.
        /// </summary>
        public CypherBasedPrngGenerator(byte[] key) 
            : this(key, CryptoPrimitive.Aes256(), SHA512.Create(), new CypherCounter(16), null) { }

        /// <summary>
        /// Initialise the CPRNG with the given key material, and default cypher (AES 256) and hash algorithm (SHA256), zero counter and supplied additional entropy source.
        /// </summary>
        public CypherBasedPrngGenerator(byte[] key, Func<byte[]> additionalEntropyGetter)
            : this(key, CryptoPrimitive.Aes256(), SHA512.Create(), new CypherCounter(16), additionalEntropyGetter) { }

        /// <summary>
        /// Initialise the CPRNG with the given key material, specified encryption algorithm and initial counter.
        /// </summary>
        public CypherBasedPrngGenerator(byte[] key, ICryptoPrimitive cryptoPrimitive, HashAlgorithm hashAlgorithm, CypherCounter initialCounter) 
            : this(key, cryptoPrimitive, hashAlgorithm, initialCounter, null) { }

        /// <summary>
        /// Initialise the CPRNG with the given key material, specified encryption algorithm, initial counter and additional entropy source.
        /// </summary>
        public CypherBasedPrngGenerator(byte[] key, ICryptoPrimitive cryptoPrimitive, HashAlgorithm hashAlgorithm, CypherCounter initialCounter, Func<byte[]> additionalEntropyGetter) 
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (cryptoPrimitive == null) throw new ArgumentNullException(nameof(cryptoPrimitive));
            if (hashAlgorithm == null) throw new ArgumentNullException(nameof(hashAlgorithm));
            if (initialCounter == null) throw new ArgumentNullException(nameof(initialCounter));
            _KeySizeInBytes = cryptoPrimitive.KeySizeBytes;
            _BlockSizeInBytes = cryptoPrimitive.BlockSizeBytes;
            if (!(_KeySizeInBytes == 16 || _KeySizeInBytes == 32 || _KeySizeInBytes == 64)) throw new ArgumentOutOfRangeException(nameof(cryptoPrimitive), _KeySizeInBytes, $"Encryption Algorithm KeySize must be 16, 32 or 64 bytes long.");
            if (!(_BlockSizeInBytes == 16 || _BlockSizeInBytes == 32 || _BlockSizeInBytes == 64)) throw new ArgumentOutOfRangeException(nameof(cryptoPrimitive), _BlockSizeInBytes, $"Encryption Algorithm BlockSize must be 16, 32 or 64 bytes long.");
            if (key.Length != _KeySizeInBytes) throw new ArgumentOutOfRangeException(nameof(key), key.Length, $"Key must be {_KeySizeInBytes} bytes long, based on encryption algorithm used.");
            if (hashAlgorithm.HashSize / 8 < _KeySizeInBytes) throw new ArgumentOutOfRangeException(nameof(hashAlgorithm), hashAlgorithm.HashSize / 8, $"Hash Algorithm Size must be at least cypher Key Size (${_KeySizeInBytes} bytes).");
            if (initialCounter.BlockSizeBytes != _BlockSizeInBytes) throw new ArgumentOutOfRangeException(nameof(initialCounter), initialCounter.BlockSizeBytes, $"Counter block size must be equal to Crypto Primitive BlockSize.");
            _RekeyByteCount = _KeySizeInBytes;
            _RekeyBlockCount = (int)Math.Ceiling((double)_RekeyByteCount / (double)_BlockSizeInBytes);

            // Section 9.4.1 - Initialisation
            // Main difference from spec: we accept a key rather than waiting for a Reseed event.
            cryptoPrimitive.Key = new byte[_KeySizeInBytes];
            _CryptoPrimitive = cryptoPrimitive;

            _Counter = initialCounter;
            _HashFunction = hashAlgorithm;

            // If no getter is supplied, we still create a function, which returns null.
            if (additionalEntropyGetter == null)
                _AdditionalEntropyGetter = () => null;
            else
                _AdditionalEntropyGetter = additionalEntropyGetter;

            // Difference from spec: re key our cypher immediately with the supplied key.
            Reseed(key);
        }

        /// <summary>
        /// Alternate constructor with named parameters.
        /// </summary>
        public static CypherBasedPrngGenerator Create(byte[] key, 
                        ICryptoPrimitive cryptoPrimitive = null, 
                        HashAlgorithm hashAlgorithm = null, 
                        CypherCounter initialCounter = null, 
                        Func<byte[]> additionalEntropyGetter = null)
        {
            return new CypherBasedPrngGenerator(key,
                        cryptoPrimitive ?? CryptoPrimitive.Aes256(),
                        hashAlgorithm ?? SHA512.Create(),
                        initialCounter ?? new CypherCounter(16),
                        additionalEntropyGetter);
        }

        public void Dispose()
        {
            if (_Disposed) return;
            // Zero any key, IV and counter material.
            if (_CryptoPrimitive != null)
                _CryptoPrimitive.Dispose();
            if (_Counter != null && !_Counter.Disposed)
                _Counter.Dispose();
                
            // Dispose disposable .NET objects
            try { _CryptoPrimitive.Dispose(); } catch { }
            try { _HashFunction.Dispose(); } catch { }
            _Disposed = true;
        }

        public void FillWithRandomBytes(byte[] toFill) => FillWithRandomBytes(toFill, 0, toFill.Length);
        public void FillWithRandomBytes(byte[] toFill, int offset, int count)
        {
            // Section 9.4.4 - Generate Random Data
            // Difference from spec: does not return byte[] but fills a byte[] argument to allow for less allocations.

            if (_Disposed) throw new ObjectDisposedException(nameof(CypherBasedPrngGenerator));
            if (toFill == null) throw new ArgumentNullException(nameof(toFill));
            // Assert 0 <= n <= 2^20 - that is, you can't ask for 0 or less bytes, nor more than MaxRequestBytes (1MB)
            if (count <= 0) throw new ArgumentOutOfRangeException($"At least one byte of random data must be requested.");
            if (count > MaxRequestBytes) throw new ArgumentOutOfRangeException($"A maximum of {MaxRequestBytes} bytes of data can be requested per call.");
            // TODO: extra assertions about offset & count to make sure we don't get array out of bounds errors.

            // Determine the number of blocks required to fullfil the request.
            // PERF: division operation and branch.
            int remainder;
            var blocksRequired = Math.DivRem(count, _BlockSizeInBytes, out remainder);
            if (remainder > 0)
                blocksRequired = blocksRequired + 1;

            // As per spec: generate blocks and copy to output, also include enough additional randomness for a re-key of the cypher.
            // In the event the requested bytes are not a multiple of the block size, additional bytes are discarded.
            // PERF: Can this be done without allocating an array in GenerateRandomBlocks()? 
            //       Any partial last block can't be allowed to spill over or escape though.
            var randomDataPlusNewKeyMaterial = GenerateRandomBlocks(blocksRequired + _RekeyBlockCount);
            Buffer.BlockCopy(randomDataPlusNewKeyMaterial, 0, toFill, offset, count);         // PERF: copying bytes

            // As per spec: After each request for random bytes, rekey to destroy evidence of previous key.
            // This ensures you cannot "rewind" the generator if you discover the key.
            // PERF: could generate this random data at the same time as the data we are returning.

            Reseed(randomDataPlusNewKeyMaterial.LastBytes(_RekeyByteCount));

            // Extra: Counting bytes requested.
            BytesRequested = BytesRequested + count;
        }

        public void Reseed(byte[] newSeed)
        {
            // Section 9.4.2 - Reseed
            if (_Disposed) throw new ObjectDisposedException(nameof(CypherBasedPrngGenerator));
            if (newSeed == null) throw new ArgumentNullException(nameof(newSeed));
            if (newSeed.Length < _CryptoPrimitive.Key.Length)
                throw new InvalidOperationException($"New seed data must be at least {_CryptoPrimitive.Key.Length} bytes.");

            // As per spec: Compute new key by combining the current key and new seed material.
            var combinedKeyMaterial = _CryptoPrimitive.Key.Concat(newSeed);
            // Additional to spec: add the additional entropy, if any is supplied.
            var additionalEntropy = _AdditionalEntropyGetter();
            if (additionalEntropy != null && additionalEntropy.Length > 0)
                combinedKeyMaterial = combinedKeyMaterial.Concat(additionalEntropy);
            
            // Spec says SHA 256 should be used as a hash function. We allow any hash function which produces the cypher key length of bytes.
            // We ensure the cypher key is of the correct size (as the hash function may return more bytes than required).
            _CryptoPrimitive.Key = _HashFunction.ComputeHash(combinedKeyMaterial.ToArray()).EnsureArraySize(_KeySizeInBytes);

            // As per spec: Increment the counter data.
            _Counter.Increment();
        }
        

        private byte[] GenerateRandomBlocks(int blockCount)
        {
            // Section 9.4.3 - Generate Blocks
            // As per spec: blockCount is k, the number of blocks to generate.
            // As per spec: result is r, an empty string of bytes
            // PERF: could allocate a large amount of memory here.
            var result = new byte[blockCount * _BlockSizeInBytes];

            // PERF: there is non-trivial overhead in CreateEncryptor()
            var encryptor = _CryptoPrimitive.CreateEncryptor();
            // Append the necessary blocks.
            for (int i = 0; i < blockCount; i++)
            {
                // As per spec: Encrypt and accumulate - but we don't need to concat as we have preallocted the array.
                // As per spec: Increment the counter
                _Counter.EncryptAndIncrement(encryptor, result, i);
            }
            // Extra: counting bytes generated.
            BytesGenerated = BytesGenerated + result.Length;
            return result;
        }
    }
}
