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

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class EntropySourceTests
    {
        [TestMethod]
        [TestCategory("Fuzzing")]
        public void Cheap16Bytes_Fuzzing()
        {
            FuzzCheapEntropy(1000, CheapEntropy.Get16, "Entropy_Cheap16", WaitAndGenerateGarbage).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Fuzzing")]
        public void Cheap32Bytes_Fuzzing()
        {
            FuzzCheapEntropy(1000, CheapEntropy.Get32, "Entropy_Cheap32", WaitAndGenerateGarbage).GetAwaiter().GetResult();
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void NullSource_Fuzzing()
        {
            FuzzEntropySource(100, new NullSource(), "Entropy_" + nameof(NullSource), DoNothing).GetAwaiter().GetResult();
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void CounterSource_Fuzzing()
        {
            FuzzEntropySource(100, new CounterSource(), "Entropy_" + nameof(CounterSource), DoNothing).GetAwaiter().GetResult();
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void CurrentTimeSource_Fuzzing()
        {
            FuzzEntropySource(10000, new CurrentTimeSource(), "Entropy_" + nameof(CurrentTimeSource), DoNothing).GetAwaiter().GetResult();
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void TimerSource_Fuzzing()
        {
            FuzzEntropySource(10000, new TimerSource(), "Entropy_" + nameof(TimerSource), DoNothing).GetAwaiter().GetResult();
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void GCMemorySource_Fuzzing()
        {
            FuzzEntropySource(1000, new GCMemorySource(), "Entropy_" + nameof(GCMemorySource), GenerateGarbage).GetAwaiter().GetResult();
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void CryptoRandomSource_Fuzzing()
        {
            FuzzEntropySource(100, new CryptoRandomSource(), "Entropy_" + nameof(CryptoRandomSource), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void CryptoRandomSource_32ByteResults()
        {
            var source = new CryptoRandomSource(32);
            var result = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.AreEqual(result.Length, 32);
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void ProcessStatsSource_Fuzzing()
        {
            FuzzEntropySource(10, new ProcessStatsSource(), "Entropy_" + nameof(ProcessStatsSource), DoNothing).GetAwaiter().GetResult();
            FuzzEntropySource(10, new ProcessStatsSource(TimeSpan.FromSeconds(5)), "Entropy_" + nameof(ProcessStatsSource) + "2", Sleep500).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ProcessStatsSource_ConfigPeriod()
        {
            var source = new ProcessStatsSource(TimeSpan.FromMinutes(100));
            Assert.AreEqual(source.PeriodNormalPriority.TotalMinutes, 100.0);
        }
        [TestMethod]
        public void ProcessStatsSource_MaxStatCount()
        {
            var source = new ProcessStatsSource(TimeSpan.FromMinutes(1), 10000);
            Assert.AreEqual(source.StatsPerChunk, 10000);
            var result = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.AreEqual(result.Length, 32);
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void NetworkStatsSource_Fuzzing()
        {
            FuzzEntropySource(10, new NetworkStatsSource(), "Entropy_" + nameof(NetworkStatsSource), DoNothing).GetAwaiter().GetResult();
            FuzzEntropySource(10, new NetworkStatsSource(TimeSpan.FromSeconds(5)), "Entropy_" + nameof(NetworkStatsSource) + "2", Sleep500).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void NetworkStatsSource_ConfigPeriod()
        {
            var source = new NetworkStatsSource(TimeSpan.FromMinutes(100));
            Assert.AreEqual(source.PeriodNormalPriority.TotalMinutes, 100.0);
        }
        [TestMethod]
        public void NetworkStatsSource_MaxStatCount()
        {
            var source = new NetworkStatsSource(TimeSpan.FromMinutes(1), 10000);
            Assert.AreEqual(source.StatsPerChunk, 10000);
            var result = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.AreEqual(result.Length, 32);
        }


        [TestMethod]
        [TestCategory("Fuzzing")]
        public void PingStatsSource_Fuzzing()
        {
            FuzzEntropySource(10, new PingStatsSource(), "Entropy_" + nameof(PingStatsSource) + "Fake", DoNothing).GetAwaiter().GetResult();
            FuzzEntropySource(4, new PingStatsSource(true), "Entropy_" + nameof(PingStatsSource) + "Fake2", Sleep500).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void PingStatsSource_Network()
        {
            FuzzEntropySource(20, new PingStatsSource(TimeSpan.FromSeconds(5)), "Entropy_" + nameof(PingStatsSource), Sleep500).GetAwaiter().GetResult();
        }
        [TestCategory("Network")]
        public void PingStatsSource_EnsureAllServers()
        {
            throw new NotImplementedException("ping all servers");
        }

        [TestMethod]
        public void PingStatsSource_ConfigPeriod()
        {
            var source = new PingStatsSource(TimeSpan.FromMinutes(100));
            Assert.AreEqual(source.PeriodNormalPriority.TotalMinutes, 100.0);
        }
        [TestMethod]
        public void PingStatsSource_ServersPerSample()
        {
            var source = new PingStatsSource(TimeSpan.FromMinutes(1), null, 10, 20);
            Assert.AreEqual(source.ServersPerSample, 20);
        }
        [TestMethod]
        public void PingStatsSource_PingsPerSample()
        {
            var source = new PingStatsSource(TimeSpan.FromMinutes(1), null, 1000, 20);
            Assert.AreEqual(source.PingsPerSample, 1000);
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void ExternalWebContentSource_Fuzzing()
        {
            FuzzEntropySource(10, new ExternalWebContentSource(true), "Entropy_" + nameof(ExternalWebContentSource) + "Fake", DoNothing).GetAwaiter().GetResult();
        }


        [TestMethod]
        public void ExternalWebContentSource_ConfigPeriod()
        {
            var source = new ExternalWebContentSource("", null, TimeSpan.FromMinutes(100));
            Assert.AreEqual(source.PeriodNormalPriority.TotalMinutes, 100.0);
        }
        [TestMethod]
        public void ExternalWebContentSource_ServersPerSample()
        {
            var source = new ExternalWebContentSource("", null, TimeSpan.FromMinutes(100), 20);
            Assert.AreEqual(source.ServersPerSample, 20);
        }


        [TestMethod]
        public void ExternalServerRandomSource_RandomNumbersInfo()
        {
            FuzzEntropySource(1, new RandomNumbersInfoExternalRandomSource(true), "Entropy_" + nameof(RandomNumbersInfoExternalRandomSource), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_BeaconNist()
        {
            FuzzEntropySource(1, new BeaconNistExternalRandomSource(true), "Entropy_" + nameof(BeaconNistExternalRandomSource), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_Anu()
        {
            FuzzEntropySource(1, new AnuExternalRandomSource(true), "Entropy_" + nameof(AnuExternalRandomSource), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_HotBits()
        {
            FuzzEntropySource(1, new HotbitsExternalRandomSource(true), "Entropy_" + nameof(HotbitsExternalRandomSource), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_RandomOrgPublic()
        {
            FuzzEntropySource(1, new RandomOrgExternalRandomSource(true, Guid.Empty), "Entropy_" + nameof(RandomOrgExternalRandomSource) + "_Public", DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_RandomOrgApi()
        {
            FuzzEntropySource(1, new RandomOrgExternalRandomSource(true, Guid.NewGuid()), "Entropy_" + nameof(RandomOrgExternalRandomSource) + "_Public", DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_RandomServer()
        {
            FuzzEntropySource(1, new RandomServerExternalRandomSource(true), "Entropy_" + nameof(RandomServerExternalRandomSource), DoNothing).GetAwaiter().GetResult();
        }

        // TODO: live network tests for all these sources.



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
