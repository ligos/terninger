using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Cryptography;

namespace MurrayGrant.Terninger.Generator
{
    public class BlockCypherCprngGenerator : IRandomNumberGenerator, IDisposable
    {
        // AES with 256 bit key, as specified in 9.4
        // K, as specified in 9.4.1, is stored in _Cypher.Key
        // FUTURE: allow any SymmetricAlgorithm
        // FUTURE: allow any of SymmetricAlgorithm, keyed hash algorithm, or stream cypher.
        private readonly SymmetricAlgorithm _Cypher;

        // C, as specified in 9.4.1
        // A 128 bit integer, and a string of bytes to be encrypted.
        // FUTURE: make a class to encapsulate the counter to allow for counters of different size.
        private readonly CypherCounter _Counter;           

        // FUTURE: make these read only variable based on the cypher passed in.
        private const int _BlockSizeInBytes = 128 / 8;
        private const int _KeySizeInBytes = 256 / 8;

        // SHA256, as specified in 9.4
        private readonly HashAlgorithm _HashFunction;

        private bool _Disposed = false;

        public int MaxRequestBytes => 2 << 20;      // As sepecified in 9.4.4.
        public int BlockSizeBytes => _BlockSizeInBytes;

        // TODO: these should be bigger than longs; at least the same size as _CounterData
        public long BytesRequested { get; private set; }
        public long BytesGenerated { get; private set; }


        /// <summary>
        /// Initialise the CPRNG with the given key material, and .
        /// </summary>
        public BlockCypherCprngGenerator(byte[] key) 
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length != _KeySizeInBytes) throw new ArgumentOutOfRangeException(nameof(key), $"Key must be ${_KeySizeInBytes} bytes long.");

            // Section 9.4.1 - Initialisation
            // Main difference from spec: we accept a key rather than waiting for a Reseed event.
            _Cypher = new AesManaged() {
                KeySize = 256,
                Key = new byte[_KeySizeInBytes],
                IV = new byte[_BlockSizeInBytes],
                Mode = CipherMode.CBC,
            };
            _Counter = new CypherCounter(_BlockSizeInBytes);
            _HashFunction = new SHA256Managed();

            // Difference from spec: re key our cypher immediately with the supplied key.
            Reseed(key);
        }

        public void Dispose()
        {
            if (_Disposed) return;
            // Zero any key, IV and counter material.
            if (_Cypher != null)
            {
                Array.Clear(_Cypher.Key, 0, _Cypher.Key.Length);
                Array.Clear(_Cypher.IV, 0, _Cypher.IV.Length);
            }
            if (_Counter != null && !_Counter.Disposed)
                _Counter.Dispose();
                
            // Dispose disposable .NET objects
            try { _Cypher.Dispose(); } catch { }
            try { _HashFunction.Dispose(); } catch { }
            _Disposed = true;
        }

        public void FillWithRandomBytes(byte[] toFill) => FillWithRandomBytes(toFill, 0, toFill.Length);
        public void FillWithRandomBytes(byte[] toFill, int offset, int count)
        {
            // Section 9.4.4 - Generate Random Data
            // Difference from spec: does not return byte[] but fills a byte[] argument to allow for less allocations.

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

            // As per spec: generate blocks and copy to output.
            // In the event the requested bytes are not a multiple of the block size, additional bytes are discarded.
            // PERF: Can this be done without allocating an array in GenerateRandomBlocks()? 
            //       Any partial last block can't be allowed to spill over or escape though.
            var randomData = GenerateRandomBlocks(blocksRequired);
            Buffer.BlockCopy(randomData, 0, toFill, offset, count);         // PERF: copying bytes

            // As per spec: After each request for random bytes, rekey to destroy evidence of previous key.
            // This ensures you cannot "rewind" the generator if you discover the key.
            // PERF: could generate this random data at the same time as the data we are returning.
            var newKeyData = GenerateRandomBlocks(2);
            Reseed(newKeyData);

            // Extra: Counting bytes requested.
            BytesRequested = BytesRequested + count;
        }

        public void Reseed(byte[] newSeed)
        {
            // Section 9.4.2 - Reseed
            if (newSeed == null) throw new ArgumentNullException(nameof(newSeed));
            if (newSeed.Length < _Cypher.Key.Length)
                throw new InvalidOperationException($"New seed data must be at least {_Cypher.Key.Length} bytes.");

            // As per spec: Compute new key by combining the current key and new seed material using SHA 256.
            var combinedKeyMaterial = _Cypher.Key.Concat(newSeed).ToArray();
            _Cypher.Key = _HashFunction.ComputeHash(combinedKeyMaterial).ToArray();

            // As per spec: Increment the counter data.
            // Implementation specific: this is a separate method to abstract the duel byte[] and Int128 type.
            // FUTURE: make a class to encapsulate the counter to allow for counters of different size.
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
            var encryptor = _Cypher.CreateEncryptor();
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
