using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;

namespace MurrayGrant.Terninger.Random
{
    /// <summary>
    /// An object encapsulating a large counter (minimum 64 bits), which is incremented by an ICryptoTransform to produce random bytes.
    /// This may be a single counter or an array of them.
    /// </summary>
    public class CypherCounter : IDisposable
    {
        // TODO: this will eventually need to be an array of counters to allow for higher encryption performance.
        // However, to be compatible with ICryptoTransform, it needs to remain as a byte[].
        private readonly byte[] _Counter;

        // Counter must always be a multiple of BlockSize.
        public int BlockSizeBytes { get; private set; }

        public bool Disposed { get; private set; }

        public CypherCounter(int blockSizeBytes) : this(blockSizeBytes, new byte[blockSizeBytes] ) { }
        public CypherCounter(int blockSizeBytes, ulong counter) : this(blockSizeBytes, CreateCounterWithInitialValue(blockSizeBytes, counter)) { }
        public CypherCounter(int blockSizeBytes, byte[] counter)
        {
            if (counter == null) throw new ArgumentNullException(nameof(counter));
            if (blockSizeBytes % 8 != 0) throw new ArgumentOutOfRangeException(nameof(blockSizeBytes), blockSizeBytes, "Block size must be multiple of 8 bytes");
            if (counter.Length != blockSizeBytes) throw new ArgumentOutOfRangeException(nameof(counter), counter.Length, $"Counter must be {blockSizeBytes} bytes");

            BlockSizeBytes = blockSizeBytes;
            _Counter = counter;
        }
        /// <summary>
        /// Creates a random counter using the generator supplied.
        /// </summary>
        public static CypherCounter CreateRandom(int blockSizeBytes, IRandomNumberGenerator rand)
        {
            return new CypherCounter(blockSizeBytes, rand.GetRandomBytes(blockSizeBytes));
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
        /// Primarily for unit testing.
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
            try
            {
                IncrementLower();
            }
            catch (OverflowException)
            {
                AddNested(1);
            }
        }

        /// <summary>
        /// Encrypts the counter into the buffer at the block number indicated, then increments the counter.
        /// </summary>
        public void EncryptAndIncrement(ICryptoTransform cypher, byte[] buffer, int blockNumber)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(CypherCounter));
            var encryptedCount = cypher.TransformBlock(_Counter, 0, _Counter.Length, buffer, BlockSizeBytes * blockNumber);
            if (encryptedCount != _Counter.Length)
                throw new Exception($"Assert failed: encrypted byte count ({encryptedCount}) != counter size ({_Counter.Length})");
            Increment();
        }

        /// <summary>
        /// Set the counter after loading from persistent state.
        /// A random uint32 is added to make observing state more difficult.
        /// </summary>
        internal void SetCounter(byte[] value, UInt32 randomToAdd)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(CypherCounter));
            if (value.Length != BlockSizeBytes)
                return;
            Buffer.BlockCopy(value, 0, _Counter, 0, value.Length);
            AddNested(randomToAdd);
        }

        private void IncrementLower()
        {
            // PERF: common case.
            ulong c = BitConverter.ToUInt64(_Counter, 0) + 1;      // Will throw on overflow.
            var bytes = BitConverter.GetBytes(c);
            Buffer.BlockCopy(bytes, 0, _Counter, 0, bytes.Length);
        }
        private void AddNested(UInt32 number)
        {
            // PERF: Uncommon case.
            var numberToAdd = number;
            var maxIterations = _Counter.Length / 8;
            for (int i = 0; i < maxIterations; i++)
            {
                try
                {
                    ulong c = BitConverter.ToUInt64(_Counter, i * 8) + numberToAdd;       // Will throw on overflow.
                    var bytes = BitConverter.GetBytes(c);
                    Buffer.BlockCopy(bytes, 0, _Counter, i * 8, bytes.Length);
                    return;     // If this does not overflow, we should break out of the loop.
                }
                catch (OverflowException)
                {
                    // On overflow, clear the chunk we just overflowed on, and loop to increment the next chunk.
                    Array.Clear(_Counter, i*8, 8);
                    numberToAdd = 1;
                }
            }
        }

        private static byte[] CreateCounterWithInitialValue(int blockSizeBytes, ulong value)
        {
            var result = new byte[blockSizeBytes];
            var valueBytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(valueBytes, 0, result, 0, valueBytes.Length);
            return result;
        }
    }
}
