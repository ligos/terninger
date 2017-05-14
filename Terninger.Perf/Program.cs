using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.Perf
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkDotNet.Running.BenchmarkRunner.Run<Benchmarks.CrypoOperations>();
            //BenchmarkDotNet.Running.BenchmarkRunner.Run<Benchmarks.CheapEntropy>();
            //BenchmarkDotNet.Running.BenchmarkRunner.Run<Benchmarks.CreateGenerator>();
            //BenchmarkDotNet.Running.BenchmarkRunner.Run<Benchmarks.GenerateBlocks>();
        }
    }
}
