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
using System.Net.NetworkInformation;
using System.Net;
using System.Collections.Generic;

namespace MurrayGrant.Terninger.Test.Slow
{
    [TestClass]
    public class EntropySourceTests
    {
        public static readonly string UnitTestUserAgent = WebClientHelpers.DefaultUserAgent.Replace("unconfigured", "UnitTests; terninger@ligos.net");

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void Cheap16Bytes_Fuzzing()
        {
            FuzzCheapEntropy(10000, CheapEntropy.Get16, "Entropy_Cheap16", WaitAndGenerateGarbage).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Fuzzing")]
        public void Cheap32Bytes_Fuzzing()
        {
            FuzzCheapEntropy(10000, CheapEntropy.Get32, "Entropy_Cheap32", WaitAndGenerateGarbage).GetAwaiter().GetResult();
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void StaticLocal32Bytes_Raw()
        {
            FuzzLongEntropy(100, StaticLocalEntropy.GetLongsForDigest, "Entropy_StaticLocal32_Raw", WaitAndGenerateGarbage).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Fuzzing")]
        public void StaticLocal32Bytes_Fuzzing()
        {
            FuzzCheapEntropyTask(100, StaticLocalEntropy.Get32, "Entropy_StaticLocal32", WaitAndGenerateGarbage).GetAwaiter().GetResult();
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
            FuzzEntropySource(10000, new TimerSource(), "Entropy_" + nameof(TimerSource), Sleep).GetAwaiter().GetResult();
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void GCMemorySource_Fuzzing()
        {
            FuzzEntropySource(10000, new GCMemorySource(), "Entropy_" + nameof(GCMemorySource), GenerateGarbage).GetAwaiter().GetResult();
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void CryptoRandomSource_Fuzzing()
        {
            FuzzEntropySource(1000, new CryptoRandomSource(), "Entropy_" + nameof(CryptoRandomSource), DoNothing).GetAwaiter().GetResult();
        }


        [TestMethod]
        [TestCategory("Fuzzing")]
        public void ProcessStatsSource_Fuzzing()
        {
            FuzzEntropySource(10, new ProcessStatsSource() { LogRawStats = true }, "Entropy_" + nameof(ProcessStatsSource), DoNothing).GetAwaiter().GetResult();
            FuzzEntropySource(10, new ProcessStatsSource(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500), 70, null) { LogRawStats = true }, "Entropy_" + nameof(ProcessStatsSource) + "2", Sleep500).GetAwaiter().GetResult();
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void NetworkStatsSource_Fuzzing()
        {
            FuzzEntropySource(10, new NetworkStatsSource(), "Entropy_" + nameof(NetworkStatsSource), DoNothing).GetAwaiter().GetResult();
            FuzzEntropySource(10, new NetworkStatsSource(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500), 17, null), "Entropy_" + nameof(NetworkStatsSource) + "2", Sleep500).GetAwaiter().GetResult();
        }


        [TestMethod]
        [TestCategory("Network")]
        public void PingStatsSource_Network()
        {
            FuzzEntropySource(10, new PingStatsSource(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, null, 6, 8, null), "Entropy_" + nameof(PingStatsSource), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public async Task PingStatsSource_EnsureAllServers()
        {
            var servers = await PingStatsSource.LoadInternalServerListAsync();
            var p = new Ping();
            var failedServers = new List<Tuple<IPAddress, object>>();
            foreach (var s in servers)
            {
                // Make 5 attempts to successfully ping each server.
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        var result = await p.SendPingAsync(s, 5000);
                        if (result.Status == IPStatus.Success)
                            break;
                        if (i == 4)
                            failedServers.Add(Tuple.Create(s, (object)result.Status));
                        else
                            await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        failedServers.Add(Tuple.Create(s, (object)ex));
                        break;
                    }
                }
            }

            if (failedServers.Any())
                throw new Exception("Servers failed to ping:\n" + String.Join("\n", failedServers.Select(x => x.Item1.ToString() + " - " + x.Item2.ToString())));

        }


        [TestMethod]
        [TestCategory("Network")]
        public void ExternalWebContentSource_Network()
        {
            FuzzEntropySource(10, new ExternalWebContentSource(UnitTestUserAgent, null, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 5, null), "Entropy_" + nameof(ExternalWebContentSource), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public async Task ExternalWebContentSource_TestAllServers()
        {
            var urls = await ExternalWebContentSource.LoadInternalServerListAsync();
            var failedUrls = new List<Tuple<Uri, object>>();
            foreach (var url in urls)
            {
                // Make 3 attempts to successfully connect to each server.
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        var wc = WebClientHelpers.Create(userAgent: UnitTestUserAgent);
                        var result = await wc.DownloadStringTaskAsync(url);
                        break;
                    }
                    catch (Exception ex)
                    {
                        await Task.Delay(1000);
                        if (i == 2)
                            failedUrls.Add(Tuple.Create(url, (object)ex));
                    }
                }
            }

            if (failedUrls.Any())
                throw new Exception("Urls failed to GET: n" + String.Join("\n", failedUrls.Select(x => x.Item1.ToString() + " - " + x.Item2.ToString())));

        }



        [TestMethod]
        [TestCategory("Network")]
        public void AnuExternalRandomSource_Network()
        {
            FuzzEntropySource(5, new AnuExternalRandomSource(UnitTestUserAgent, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), "Entropy_" + nameof(AnuExternalRandomSource), Sleep500).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void BeaconNistExternalRandomSource_Network()
        {
            FuzzEntropySource(5, new BeaconNistExternalRandomSource(UnitTestUserAgent, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), "Entropy_" + nameof(BeaconNistExternalRandomSource), Sleep30000).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void HotbitsExternalRandomSourcePseudoRandom_Network()
        {
            FuzzEntropySource(5, new HotbitsExternalRandomSource(UnitTestUserAgent, 32, "", TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), "Entropy_" + nameof(HotbitsExternalRandomSource) + "_PseudoRangom", Sleep500).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void HotbitsExternalRandomSourceTrueRandom_Network()
        {
            var maybeApiKey = Environment.GetEnvironmentVariable("Terninger_UnitTest_HotBitsApiKey") ?? "";
            if (String.IsNullOrEmpty(maybeApiKey))
            {
                Assert.Inconclusive("No API key available: get one from https://www.fourmilab.ch/hotbits/ and set it in the 'Terninger_UnitTest_HotBitsApiKey' environment variable.");
                return;
            }
            FuzzEntropySource(2, new HotbitsExternalRandomSource(UnitTestUserAgent, 32, maybeApiKey, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), "Entropy_" + nameof(HotbitsExternalRandomSource) + "_TrueRandom", Sleep500).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void RandomNumbersInfoExternalRandomSource_Network()
        {
            FuzzEntropySource(5, new RandomNumbersInfoExternalRandomSource(UnitTestUserAgent, 32, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), "Entropy_" + nameof(RandomNumbersInfoExternalRandomSource), Sleep500).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void RandomOrgExternalRandomSourcePublic_Network()
        {
            FuzzEntropySource(5, new RandomOrgExternalRandomSource(UnitTestUserAgent, 32, Guid.Empty, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), "Entropy_" + nameof(RandomOrgExternalRandomSource) + "_Public", Sleep500).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void RandomOrgExternalRandomSourceApi_Network()
        {
            var maybeApiKey = Environment.GetEnvironmentVariable("Terninger_UnitTest_RandomOrgApiKey") ?? "";
            if (!Guid.TryParse(maybeApiKey, out var apiKey))
            {
                Assert.Inconclusive("No API key available: get one from https://api.random.org and set it in the 'Terninger_UnitTest_RandomOrgApiKey' environment variable.");
                return;
            }
            FuzzEntropySource(5, new RandomOrgExternalRandomSource(UnitTestUserAgent, 32, apiKey, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), "Entropy_" + nameof(RandomOrgExternalRandomSource) + "_Api", Sleep500).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void RandomServerExternalRandomSource_Network()
        {
            FuzzEntropySource(5, new RandomServerExternalRandomSource(UnitTestUserAgent, 32, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), "Entropy_" + nameof(RandomServerExternalRandomSource), Sleep500).GetAwaiter().GetResult();
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
        private async Task FuzzCheapEntropyTask(int iterations, Func<Task<byte[]>> getter, string filename, Action extra)
        {
            using (var sw = new StreamWriter(filename + ".txt", false, Encoding.UTF8))
            {
                await sw.WriteLineAsync($"{iterations:N0} iterations");

                for (int i = 0; i < iterations; i++)
                {
                    var bytes = await getter();
                    if (bytes == null)
                        await sw.WriteLineAsync("<null>");
                    else
                        await sw.WriteLineAsync(bytes.ToHexString());
                    extra();
                }
            }
        }
        private async Task FuzzLongEntropy(int iterations, Func<long[]> getter, string filename, Action extra)
        {
            using (var sw = new StreamWriter(filename + ".txt", false, Encoding.UTF8))
            {
                await sw.WriteLineAsync($"{iterations:N0} iterations");

                for (int i = 0; i < iterations; i++)
                {
                    var longs = getter();
                    if (longs == null)
                        await sw.WriteLineAsync("<null>");
                    else
                        await sw.WriteLineAsync(longs.LongsToHexString());
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
        private static void Sleep30000()
        {
            System.Threading.Thread.Sleep(30000);
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
