using System;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Local;
using MurrayGrant.Terninger.EntropySources.Test;
using MurrayGrant.Terninger.EntropySources.Network;
using MurrayGrant.Terninger.Accumulator;

using System.IO;
using System.Text;
using System.Threading.Tasks;
using Rand = System.Random;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class EntropySourceTests
    {
        [TestMethod]
        public void NullSource_IsNull()
        {
            var source = new NullSource();
            var result = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.IsTrue(result.All(b => b == 0));
        }

        [TestMethod]
        public void CounterSource_Increments()
        {
            var source = new CounterSource();
            var result1 = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            var result2 = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.IsFalse(result1.All(b => b == 0));
            Assert.IsFalse(result2.All(b => b == 0));
            Assert.AreEqual(result1.ToHexString(), "0100000000000000");
            Assert.AreEqual(result2.ToHexString(), "0200000000000000");
        }

        [TestMethod]
        public void UserSuppliedSource_IsWhatIsSupplied()
        {
            var initial = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var source = new UserSuppliedSource(initial);
            var result = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            CollectionAssert.AreEqual(result, initial);
            // Second call should have cleared the stored entropy.
            var result2 = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.IsNull(result2);
        }
        [TestMethod]
        public void UserSuppliedSource_SetEntropy()
        {
            var initial = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var source = new UserSuppliedSource(initial);
            var result = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            CollectionAssert.AreEqual(result, initial);

            var updated = new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 };
            source.SetEntropy(updated);
            var result2 = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            CollectionAssert.AreEqual(result2, updated);

            // After a call the entropy should be cleared.
            var result3 = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.IsNull(result3);
        }

        [TestMethod]
        public void CurrentTimeSource_IsNotNull()
        {
            var source = new CurrentTimeSource();
            var result = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.IsFalse(result.All(b => b == 0));
        }

        [TestMethod]
        public void TimerSource_IsNotNull()
        {
            var source = new TimerSource();
            System.Threading.Thread.Sleep(1);
            var result = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.IsFalse(result.All(b => b == 0));
        }

        [TestMethod]
        public void GCMemorySource_IsNotNull()
        {
            var source = new GCMemorySource();
            var result = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.IsFalse(result.All(b => b == 0));
        }

        [TestMethod]
        public void CryptoRandomSource_IsNotNull()
        {
            var source = new CryptoRandomSource();
            var result = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.AreEqual(result.Length, 16);
            Assert.IsFalse(result.All(b => b == 0));
        }

        [TestMethod]
        public void CryptoRandomSource_32ByteResults()
        {
            var source = new CryptoRandomSource(32);
            var result = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.AreEqual(result.Length, 32);
            Assert.IsFalse(result.All(b => b == 0));
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
            var source = new ProcessStatsSource(itemsPerResultChunk: 10000);
            Assert.AreEqual(source.StatsPerChunk, 10000);
            var result = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.AreEqual(result.Length, 32);
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
            var source = new NetworkStatsSource(itemsPerResultChunk: 10000);
            Assert.AreEqual(source.StatsPerChunk, 10000);
            var result = source.GetEntropyAsync(EntropyPriority.Normal).GetAwaiter().GetResult();
            Assert.AreEqual(result.Length, 32);
        }


        [TestMethod]
        public void PingStatsSource_Fake()
        {
            FuzzEntropySource(10, new PingStatsSource(true), "Entropy_" + nameof(PingStatsSource) + "Fake", DoNothing).GetAwaiter().GetResult();
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
        public void ExternalWebContentSource_Fake()
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
            FuzzEntropySource(1, new RandomNumbersInfoExternalRandomSource(true), "Entropy_" + nameof(RandomNumbersInfoExternalRandomSource) + "_FromFile", DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_BeaconNist()
        {
            FuzzEntropySource(1, new BeaconNistExternalRandomSource(true), "Entropy_" + nameof(BeaconNistExternalRandomSource) + "_FromFile", DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_Anu()
        {
            FuzzEntropySource(1, new AnuExternalRandomSource(true), "Entropy_" + nameof(AnuExternalRandomSource) + "_FromFile", DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_HotBits()
        {
            FuzzEntropySource(1, new HotbitsExternalRandomSource(true), "Entropy_" + nameof(HotbitsExternalRandomSource) + "_FromFile", DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_RandomOrgPublic()
        {
            FuzzEntropySource(1, new RandomOrgExternalRandomSource(true, String.Empty), "Entropy_" + nameof(RandomOrgExternalRandomSource) + "_Public_FromFile", DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_RandomOrgApi()
        {
            FuzzEntropySource(1, new RandomOrgExternalRandomSource(true, "FakeApiKey"), "Entropy_" + nameof(RandomOrgExternalRandomSource) + "_Public_FromFile", DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_QrngEthzCh()
        {
            FuzzEntropySource(1, new QrngEthzChExternalRandomSource(true), "Entropy_" + nameof(QrngEthzChExternalRandomSource) + "_Public_FromFile", DoNothing).GetAwaiter().GetResult();
        }


        [TestMethod]
        public void TestHttpClientDefaults()
        {
            var http = HttpClientHelpers.Create();
            Assert.AreEqual(HttpClientHelpers.DefaultTimeout, http.Timeout);
            Assert.AreEqual(HttpClientHelpers.UserAgentString(), String.Join(" ", http.DefaultRequestHeaders.UserAgent.Select(x => x.ToString())));
        }


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
            return new StandardRandomWrapperGenerator(new Rand(1));
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
