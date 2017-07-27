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
            BenchmarkDotNet.Running.BenchmarkSwitcher.FromTypes(new[] {
                typeof(Benchmarks.GenerateNumbers),
                typeof(Benchmarks.GenerateBlocks),
                typeof(Benchmarks.CreateGenerator),
                typeof(Benchmarks.CheapEntropy),
                typeof(Benchmarks.CrypoOperations),
            }).Run();
        }
    }
}
