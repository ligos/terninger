using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace MurrayGrant.Terninger.EntropySources
{
    public static class CheapEntropy
    {
        [ThreadStatic]
        private static Stopwatch _Stopwatch;
        [ThreadStatic]
        private static Process _CurrentProcess;
        [ThreadStatic]
        private static ulong _Counter;

        /// <summary>
        /// Gets 32 bytes of entropy.
        /// </summary>
        public static byte[] Get32()
        {
            // To make a PRNG unpredictable (and non-deterministic), we can create a small amount of cheap, portable entropy.
            // The inputs to this change regularly, and are very fast to obtain.
            // However, they are not high quality entropy sources: of the 256 bits returned, only 16-32 are going to be hard for an attacker to guess.
            // PERF: ~300ns on Murray's laptop - creating the ICryptoTransform object is ~4000ns, which is the main cost of re-seeding.

            EnsureThreadStaticsInitialised();
            var result = new byte[32];

            // PERF: all these are doing an extra copy, which isn't needed.

            // Current date and time.
            var a = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            Buffer.BlockCopy(a, 0, result, 0, a.Length);

            // CLR / GC memory stats + counter.
            _Counter = unchecked(_Counter + 1);
            var gcCollections = ((ulong)GC.CollectionCount(0) << 32)
                                & ((ulong)GC.CollectionCount(1) ^ (ulong)GC.CollectionCount(2));
            var gcTotalMemory = (ulong)GC.GetTotalMemory(false);
            var b = BitConverter.GetBytes(gcCollections ^ gcTotalMemory ^ _Counter);
            Buffer.BlockCopy(b, 0, result, 8, b.Length);

            // High precision timer ticks.
            var c = BitConverter.GetBytes(_Stopwatch.ElapsedTicks);
            Buffer.BlockCopy(c, 0, result, 16, c.Length);

            // Process working set & system uptime.
            var workingSetAndTickCount = ((long)Environment.TickCount << 32) ^ _CurrentProcess.WorkingSet64;
            var d = BitConverter.GetBytes(workingSetAndTickCount);
            Buffer.BlockCopy(d, 0, result, 24, d.Length);

            return result;
        }

        /// <summary>
        /// Gets 16 bytes of entropy.
        /// </summary>
        public static byte[] Get16()
        {
            // To make a PRNG unpredictable (and non-deterministic), we can create a small amount of cheap, portable entropy.
            // The inputs to this change regularly, and are very fast to obtain.
            // However, they are not high quality entropy sources: of the 128 bits returned, only 16-32 are going to be hard for an attacker to guess.
            // PERF: ~230ns on Murray's laptop - creating the ICryptoTransform object is ~4000ns, which is the main cost of re-seeding.

            EnsureThreadStaticsInitialised();
            var result = new byte[16];

            // PERF: all these are doing an extra copy, which isn't needed.

            // Current date and time + CLR / GC memory stats.
            var ticks = (ulong)DateTime.UtcNow.Ticks;
            _Counter = unchecked(_Counter + 1);
            var gcCollections = ((ulong)GC.CollectionCount(0) << 32)
                                & ((ulong)GC.CollectionCount(1) ^ (ulong)GC.CollectionCount(2));
            var gcTotalMemory = (ulong)GC.GetTotalMemory(false);
            var a = BitConverter.GetBytes(ticks ^ gcCollections ^ gcTotalMemory ^ _Counter);
            Buffer.BlockCopy(a, 0, result, 0, a.Length);

            // High precision timer ticks + Process working set & system uptime.
            var b = BitConverter.GetBytes(((long)Environment.TickCount << 32) ^ _CurrentProcess.WorkingSet64 ^ _Stopwatch.ElapsedTicks);
            Buffer.BlockCopy(b, 0, result, 8, b.Length);

            return result;
        }

        /// <summary>
        /// Gets a null result.
        /// </summary>
        public static byte[] GetNull()
        {
            return null;
        }

        private static void EnsureThreadStaticsInitialised()
        {
            if (_Stopwatch == null)
                _Stopwatch = Stopwatch.StartNew();
            if (_CurrentProcess == null)
                _CurrentProcess = Process.GetCurrentProcess();
        }

    }
}
