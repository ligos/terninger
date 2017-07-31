using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;

namespace MurrayGrant.Terninger.Generator
{
    /// <summary>
    /// An object encapsulating a 128, 256 or 512 bit counter, which is incremented by an ICryptoTransform to produce random bytes.
    /// This may be a single counter or an array of them.
    /// </summary>
    public class CypherCounter : IDisposable
    {
        // TODO: this will eventually need to be an array of counters to allow for higher encryption performance.
        // However, to be compatible with ICryptoTransform, it needs to remain as a byte[].
        private readonly byte[] _Counter;
        private readonly Action _Incrementor;

        // Counter must always be a multiple of BlockSize.
        public int BlockSizeBytes { get; private set; }

        public bool Disposed { get; private set; }

        public CypherCounter(int blockSizeBytes) : this(blockSizeBytes, new byte[blockSizeBytes] ) { }
        public CypherCounter(int blockSizeBytes, ulong counter) : this(blockSizeBytes, CreateCounterWithInitialValue(blockSizeBytes, counter)) { }
        public CypherCounter(int blockSizeBytes, byte[] counter)
        {
            if (counter == null) throw new ArgumentNullException(nameof(counter));
            if (!(blockSizeBytes == 16 || blockSizeBytes == 32 || blockSizeBytes == 64)) throw new ArgumentOutOfRangeException(nameof(blockSizeBytes), blockSizeBytes, "Block size must be 16, 32 or 64 bytes");
            if (counter.Length != blockSizeBytes) throw new ArgumentOutOfRangeException(nameof(counter), counter.Length, $"Counter must be {blockSizeBytes} bytes");

            BlockSizeBytes = blockSizeBytes;
            _Counter = counter;
            if (blockSizeBytes == 16) _Incrementor = IncrementNested16;
            if (blockSizeBytes == 32) _Incrementor = IncrementNested32;
            if (blockSizeBytes == 64) _Incrementor = IncrementNested64;
        }

        /// <summary>
        /// Clears the counter and marks it as disposed. Further access to the counter will throw ObjectDisposedException.
        /// </summary>
        public void Dispose()
        {
            Array.Clear(_Counter, 0, _Counter.Length);
            Disposed = true;
        }

        /// <summary>
        /// Lets you observe the current counter value.
        /// </summary>
        public byte[] GetCounter()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(CypherCounter));
            return _Counter.ToArray();
        }

        /// <summary>
        /// Increments the current counter value.
        /// </summary>
        public void Increment()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(CypherCounter));
            _Incrementor();
        }

        /// <summary>
        /// Encrypts the counter into the buffer at the block number indicated, then increments the counter.
        /// </summary>
        public void EncryptAndIncrement(ICryptoTransform cypher, byte[] buffer, int blockNumber)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(CypherCounter));
            var encryptedCount = cypher.TransformBlock(_Counter, 0, _Counter.Length, buffer, BlockSizeBytes * blockNumber);
            Debug.Assert(encryptedCount == _Counter.Length);
            Increment();
        }

        private void IncrementNested16()
        {
            try
            {
                ulong c1 = BitConverter.ToUInt64(_Counter, 0) + 1;
                var c1Bytes = BitConverter.GetBytes(c1);
                Buffer.BlockCopy(c1Bytes, 0, _Counter, 0, c1Bytes.Length);
            }
            catch (OverflowException)
            {
                // Lower half overflowed: increment the upper half and reset lower.
                try
                {
                    ulong c2 = BitConverter.ToUInt64(_Counter, 8) + 1;
                    var c2Bytes = BitConverter.GetBytes(c2);
                    Array.Clear(_Counter, 0, 8);
                    Buffer.BlockCopy(c2Bytes, 0, _Counter, 8, c2Bytes.Length);
                }
                catch (OverflowException)
                {
                    // Both overflowed: reset counter.
                    Array.Clear(_Counter, 0, _Counter.Length);
                }
            }
        }
        private void IncrementNested32() { throw new NotImplementedException(); }
        private void IncrementNested64() { throw new NotImplementedException(); }

        private static byte[] CreateCounterWithInitialValue(int blockSizeBytes, ulong value)
        {
            var result = new byte[blockSizeBytes];
            var valueBytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(valueBytes, 0, result, 0, valueBytes.Length);
            return result;
        }
    }
}
