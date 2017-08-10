using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;

namespace MurrayGrant.Terninger.Perf.Benchmarks
{
    public class CheapEntropy
    {
        private static readonly Stopwatch _Stopwatch = Stopwatch.StartNew();
        private static readonly NetworkInterface _FirstNetworkInterface = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().First();
        private static readonly long[] _NetstatsArray = new long[6];
        private static readonly Process _CurrentProcess = Process.GetCurrentProcess();
        private static readonly long[] _ProcessStatsArray = new long[13];
        private static readonly IntPtr _ThisThread = GetCurrentThread();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetThreadTimes(IntPtr hThread, out long lpCreationTime, out long lpExitTime, out long lpKernelTime, out long lpUserTime);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();

        [Benchmark]
        public DateTime DateTimeUtcNow()
        {
            // ~9ns on Murray's laptop
            return DateTime.UtcNow;
        }
        [Benchmark]
        public long GCTotalMemory()
        {
            // ~40ns on Murray's laptop
            return GC.GetTotalMemory(false);
        }
        [Benchmark]
        public long StopwatchElapsedTicks()
        {
            // ~30ns on Murray's laptop
            return _Stopwatch.ElapsedTicks;
        }
        [Benchmark]
        public long ProcessWorkingSet()
        {
            // ~12ns on Murray's laptop
            return _CurrentProcess.WorkingSet64;
        }
        [Benchmark]
        public long GCCollectionCount()
        {
            // ~24ns on Murray's laptop
            return ((long)GC.CollectionCount(0) << 32)
                & ((long)GC.CollectionCount(1) ^ (long)GC.CollectionCount(2));
        }
        [Benchmark]
        public long EnvironmentTickCount()
        {
            // ~4ns on Murray's laptop
            return Environment.TickCount;
        }

        [Benchmark]
        public byte[] Get16()
        {
            // 230ns on Murray's laptop
            return MurrayGrant.Terninger.EntropySources.CheapEntropy.Get16();
        }
        [Benchmark]
        public byte[] Get32()
        {
            // 300ns on Murray's laptop
            return MurrayGrant.Terninger.EntropySources.CheapEntropy.Get32();
        }


        //[Benchmark]
        public long EnvironmentWorkingSet()
        {
            // ~2.5us on Murray's laptop
            // This looks to create a new process object on each call.
            return Environment.WorkingSet;
        }
        //[Benchmark]
        public long[] NetworkStats()
        {
            // ~170us on Murray's laptop
            var stats = _FirstNetworkInterface.GetIPStatistics();
            _NetstatsArray[0] = stats.BytesReceived;
            _NetstatsArray[1] = stats.BytesSent;
            _NetstatsArray[2] = stats.NonUnicastPacketsReceived;
            _NetstatsArray[3] = stats.NonUnicastPacketsSent;
            _NetstatsArray[4] = stats.UnicastPacketsReceived;
            _NetstatsArray[5] = stats.UnicastPacketsSent;
            return _NetstatsArray;
        }
        //[Benchmark]
        public long[] ProcessStats()
        {
            // ~22us on Murray's laptop
            _ProcessStatsArray[0] = _CurrentProcess.MaxWorkingSet.ToInt64();
            _ProcessStatsArray[1] = _CurrentProcess.NonpagedSystemMemorySize64;
            _ProcessStatsArray[2] = _CurrentProcess.PagedMemorySize64;
            _ProcessStatsArray[3] = _CurrentProcess.PagedSystemMemorySize64;
            _ProcessStatsArray[4] = _CurrentProcess.PeakPagedMemorySize64;
            _ProcessStatsArray[5] = _CurrentProcess.PeakVirtualMemorySize64;
            _ProcessStatsArray[6] = _CurrentProcess.PeakWorkingSet64;
            _ProcessStatsArray[7] = _CurrentProcess.PrivateMemorySize64;
            _ProcessStatsArray[8] = _CurrentProcess.WorkingSet64;
            _ProcessStatsArray[9] = _CurrentProcess.VirtualMemorySize64;
            _ProcessStatsArray[10] = _CurrentProcess.UserProcessorTime.Ticks;
            _ProcessStatsArray[11] = _CurrentProcess.TotalProcessorTime.Ticks;
            _ProcessStatsArray[12] = _CurrentProcess.PrivilegedProcessorTime.Ticks;
            return _ProcessStatsArray;
        }

        //[Benchmark]
        public long GetThreadTimes()
        {
            // ~360ns on Murray's laptop
            // Probably not portable to Linux.
            long user;
            long kernal;
            GetThreadTimes(_ThisThread, out _, out _, out user, out kernal);
            return user;
        }

    }
}
