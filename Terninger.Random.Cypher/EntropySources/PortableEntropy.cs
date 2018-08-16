using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// Low quality synchronous entropy sources.
    /// </summary>
    public static class PortableEntropy
    {
        [ThreadStatic]
        private static Stopwatch _Stopwatch;
        [ThreadStatic]
        private static ulong _Counter;

        /// <summary>
        /// Gets 32 bytes of entropy in the supplied buffer.
        /// </summary>
        public static void Fill32(byte[] buffer)
        {
            if (buffer == null) ThrowArgumentNullException();
            if (buffer.Length < 32) ThrowFillBufferSizeException(32, buffer.Length);

            // To make a PRNG unpredictable (and non-deterministic), we can create a small amount of cheap, portable entropy.
            // The inputs to this change regularly, and are very fast to obtain.
            // However, they are not high quality entropy sources: of the 256 bits returned, only 16-32 are going to be hard for an attacker to guess.
            // PERF: ~300ns on Murray's laptop - creating the ICryptoTransform object is ~4000ns, which is the main cost of re-seeding.

            EnsureThreadStaticsInitialised();

            // PERF: all these are doing an extra copy, which isn't needed. Span<T> should help.

            // Current date and time.
            var a = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            Buffer.BlockCopy(a, 0, buffer, 0, a.Length);

            // CLR / GC memory stats + counter.
            _Counter = unchecked(_Counter + 1);
            var gcCollections = ((ulong)GC.CollectionCount(0) << 32)
                                & ((ulong)GC.CollectionCount(1) ^ (ulong)GC.CollectionCount(2));
            var gcTotalMemory = (ulong)GC.GetTotalMemory(false);
            var b = BitConverter.GetBytes(gcCollections ^ gcTotalMemory ^ _Counter);
            Buffer.BlockCopy(b, 0, buffer, 8, b.Length);

            // High precision timer ticks.
            var c = BitConverter.GetBytes(_Stopwatch.ElapsedTicks);
            Buffer.BlockCopy(c, 0, buffer, 16, c.Length);

            // System uptime.
            var tickCount = ((long)Environment.TickCount);
            var d = BitConverter.GetBytes(tickCount);
            Buffer.BlockCopy(d, 0, buffer, 24, d.Length);
        }

        /// <summary>
        /// Gets 32 bytes of entropy.
        /// </summary>
        public static byte[] Get32()
        {
            var result = new byte[32];
            Fill32(result);
            return result;
        }


        /// <summary>
        /// Gets 16 bytes of entropy in the supplied buffer.
        /// </summary>
        public static void Fill16(byte[] buffer)
        {
            if (buffer == null) ThrowArgumentNullException();
            if (buffer.Length < 16) ThrowFillBufferSizeException(16, buffer.Length);

            // To make a PRNG unpredictable (and non-deterministic), we can create a small amount of cheap, portable entropy.
            // The inputs to this change regularly, and are very fast to obtain.
            // However, they are not high quality entropy sources: of the 128 bits returned, only 16-32 are going to be hard for an attacker to guess.
            // PERF: ~230ns on Murray's laptop - creating the ICryptoTransform object is ~4000ns, which is the main cost of re-seeding.

            EnsureThreadStaticsInitialised();

            // PERF: all these are doing an extra copy, which isn't needed. Span<T> should help.

            // Current date and time + CLR / GC memory stats.
            var ticks = (ulong)DateTime.UtcNow.Ticks;
            _Counter = unchecked(_Counter + 1);
            var gcCollections = ((ulong)GC.CollectionCount(0) << 32)
                                & ((ulong)GC.CollectionCount(1) ^ (ulong)GC.CollectionCount(2));
            var gcTotalMemory = (ulong)GC.GetTotalMemory(false);
            var a = BitConverter.GetBytes(ticks ^ gcCollections ^ gcTotalMemory ^ _Counter);
            Buffer.BlockCopy(a, 0, buffer, 0, a.Length);

            // High precision timer ticks + system uptime.
            var b = BitConverter.GetBytes(((long)Environment.TickCount << 32) ^ _Stopwatch.ElapsedTicks);
            Buffer.BlockCopy(b, 0, buffer, 8, b.Length);
        }

        /// <summary>
        /// Gets 16 bytes of entropy.
        /// </summary>
        public static byte[] Get16()
        {
            var result = new byte[16];
            Fill16(result);
            return result;
        }

        /// <summary>
        /// Gets a null result.
        /// </summary>
        public static byte[] GetNull() => null;

        private static void EnsureThreadStaticsInitialised()
        {
            if (_Stopwatch == null)
                _Stopwatch = Stopwatch.StartNew();
        }

        private static void ThrowArgumentNullException() => throw new ArgumentNullException("buffer");
        private static void ThrowFillBufferSizeException(int minSizeRequired, int actualSize)
            => throw new ArgumentException($"Buffer size is too small to fill. Minimum {minSizeRequired} bytes required, actual array size: {actualSize}.");
   }
}
