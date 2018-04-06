using System;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Local;
using MurrayGrant.Terninger.EntropySources.Test;
using MurrayGrant.Terninger.EntropySources.Network;
using MurrayGrant.Terninger.Accumulator;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.Test.Slow
{
    [TestClass]
    public class EntropySourceTests
    {
        [TestMethod]
        [TestCategory("Network")]
        public void PingStatsSource_Network()
        {
            FuzzEntropySource(50, new PingStatsSource(TimeSpan.FromSeconds(5)), "Entropy_" + nameof(PingStatsSource), Sleep500).GetAwaiter().GetResult();
        }
        [TestCategory("Network")]
        public void PingStatsSource_EnsureAllServers()
        {
            throw new NotImplementedException("ping all servers");
        }


        [TestMethod]
        [TestCategory("Fuzzing")]
        public void ExternalWebContentSource_Fuzzing()
        {
            FuzzEntropySource(50, new ExternalWebContentSource(true), "Entropy_" + nameof(ExternalWebContentSource) + "Fake", DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void ExternalWebContentSource_Network()
        {
            FuzzEntropySource(20, new ExternalWebContentSource(), "Entropy_" + nameof(ExternalWebContentSource), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void ExternalWebContentSource_TestAllServers()
        {
            throw new NotImplementedException("fetch from all servers");
        }

        // TODO: live network tests for all external random sources.



        private async Task FuzzEntropySource(int iterations, IEntropySource source, string filename, Action extra)
        {
            using (var sw = new StreamWriter(filename + ".txt", false, Encoding.UTF8))
            {
                await sw.WriteLineAsync($"{source.GetType().FullName} - {iterations:N0} iterations");

                for (int i = 0; i < iterations; i++)
                {
                    var bytes = await source.GetEntropyAsync(EntropyPriority.High);
                    if (bytes == null)
                        await sw.WriteLineAsync("<null>");
                    else
                        await sw.WriteLineAsync(bytes.ToHexString());
                    extra();
                }
            }
        }
        private async Task FuzzCheapEntropy(int iterations, Func<byte[]> getter, string filename, Action extra)
        {
            using (var sw = new StreamWriter(filename + ".txt", false, Encoding.UTF8))
            {
                await sw.WriteLineAsync($"{iterations:N0} iterations");

                for (int i = 0; i < iterations; i++)
                {
                    var bytes = getter();
                    if (bytes == null)
                        await sw.WriteLineAsync("<null>");
                    else
                        await sw.WriteLineAsync(bytes.ToHexString());
                    extra();
                }
            }
        }

        private static IRandomNumberGenerator GetGenerator()
        {
            return new StandardRandomWrapperGenerator(new Random(1));
        }
        private static void DoNothing()
        {
        }
        internal static byte[] _Garbage;
        internal static long _SomeResult = 63799;
        internal static long _SomePrime = 81281;
        private static void Wait()
        {
            var l = _SomeResult;
            for (int i = 0; i < 100; i++)
            {
                l = unchecked(l * _SomePrime);
            }
            _SomeResult = l;
        }
        private static void Sleep()
        {
            System.Threading.Thread.Sleep(1);
        }
        private static void Sleep500()
        {
            System.Threading.Thread.Sleep(500);
        }
        private static void GenerateGarbage()
        {
            _Garbage = new byte[256];
        }
        private static void WaitAndGenerateGarbage()
        {
            Wait();
            GenerateGarbage();
        }
    }
}
