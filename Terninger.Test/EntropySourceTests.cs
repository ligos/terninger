using System;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.EntropySources;
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
            FuzzEntropySource(100, new NullSource(), "Entropy_" + nameof(NullSource), new EntropySourceConfigFromDictionary(new[] { "NullSource.Enabled", "True" }), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void NullSource_IsDisabledByDefault()
        {
            var source = new NullSource();
            var initResultCode = source.Initialise(EntropySourceConfigFromDictionary.Empty, GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.DisabledByConfig);
        }
        [TestMethod]
        public void NullSource_EnabledByConfig()
        {
            var source = new NullSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "NullSource.Enabled", "True" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void CounterSource_Fuzzing()
        {
            FuzzEntropySource(100, new CounterSource(), "Entropy_" + nameof(CounterSource), new EntropySourceConfigFromDictionary(new[] { "CounterSource.Enabled", "True" }), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void CounterSource_IsDisabledByDefault()
        {
            var source = new CounterSource();
            var initResultCode = source.Initialise(EntropySourceConfigFromDictionary.Empty, GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.DisabledByConfig);
        }
        [TestMethod]
        public void CounterSource_EnabledByConfig()
        {
            var source = new CounterSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "CounterSource.Enabled", "True" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void CurrentTimeSource_Fuzzing()
        {
            FuzzEntropySource(10000, new CurrentTimeSource(), "Entropy_" + nameof(CurrentTimeSource), EntropySourceConfigFromDictionary.Empty, DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void CurrentTimeSource_IsEnabledByDefault()
        {
            var source = new CurrentTimeSource();
            var initResultCode = source.Initialise(EntropySourceConfigFromDictionary.Empty, GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
        }
        [TestMethod]
        public void CurrentTimeSource_DisabledByConfig()
        {
            var source = new CurrentTimeSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "CurrentTimeSource.Enabled", "False" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.DisabledByConfig);
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void TimerSource_Fuzzing()
        {
            FuzzEntropySource(10000, new TimerSource(), "Entropy_" + nameof(TimerSource), EntropySourceConfigFromDictionary.Empty, DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void TimerSource_IsEnabledByDefault()
        {
            var source = new TimerSource();
            var initResultCode = source.Initialise(EntropySourceConfigFromDictionary.Empty, GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
        }
        [TestMethod]
        public void TimerSource_DisabledByConfig()
        {
            var source = new TimerSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "TimerSource.Enabled", "False" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.DisabledByConfig);
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void GCMemorySource_Fuzzing()
        {
            FuzzEntropySource(1000, new GCMemorySource(), "Entropy_" + nameof(GCMemorySource), EntropySourceConfigFromDictionary.Empty, GenerateGarbage).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void GCMemorySource_IsEnabledByDefault()
        {
            var source = new GCMemorySource();
            var initResultCode = source.Initialise(EntropySourceConfigFromDictionary.Empty, GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
        }
        [TestMethod]
        public void GCMemorySource_DisabledByConfig()
        {
            var source = new GCMemorySource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "GCMemorySource.Enabled", "False" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.DisabledByConfig);
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void CryptoRandomSource_Fuzzing()
        {
            FuzzEntropySource(100, new CryptoRandomSource(), "Entropy_" + nameof(CryptoRandomSource), EntropySourceConfigFromDictionary.Empty, DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void CryptoRandomSource_IsEnabledByDefault()
        {
            var source = new CryptoRandomSource();
            var initResultCode = source.Initialise(EntropySourceConfigFromDictionary.Empty, GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
            var result = source.GetEntropyAsync().GetAwaiter().GetResult();
            Assert.AreEqual(result.Length, 16);
        }
        [TestMethod]
        public void CryptoRandomSource_DisabledByConfig()
        {
            var source = new CryptoRandomSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "CryptoRandomSource.Enabled", "False" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.DisabledByConfig);
        }
        [TestMethod]
        public void CryptoRandomSource_32ByteResults()
        {
            var source = new CryptoRandomSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "CryptoRandomSource.ResultLength", "32" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
            var result = source.GetEntropyAsync().GetAwaiter().GetResult();
            Assert.AreEqual(result.Length, 32);
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void ProcessStatsSource_Fuzzing()
        {
            FuzzEntropySource(10, new ProcessStatsSource(), "Entropy_" + nameof(ProcessStatsSource), EntropySourceConfigFromDictionary.Empty, DoNothing).GetAwaiter().GetResult();
            FuzzEntropySource(10, new ProcessStatsSource(), "Entropy_" + nameof(ProcessStatsSource) + "2", new EntropySourceConfigFromDictionary(new[] { "ProcessStatsSource.PeriodMinutes", "0.01" }), Sleep500).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ProcessStats_IsEnabledByDefault()
        {
            var source = new ProcessStatsSource();
            var initResultCode = source.Initialise(EntropySourceConfigFromDictionary.Empty, GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
        }
        [TestMethod]
        public void ProcessStatsSource_DisabledByConfig()
        {
            var source = new ProcessStatsSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "ProcessStatsSource.Enabled", "False" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.DisabledByConfig);
        }
        [TestMethod]
        public void ProcessStatsSource_ConfigPeriod()
        {
            var source = new ProcessStatsSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "ProcessStatsSource.PeriodMinutes", "100.0" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
            Assert.AreEqual(source.PeriodMinutes, 100.0);
        }
        [TestMethod]
        public void ProcessStatsSource_MaxStatCount()
        {
            var source = new ProcessStatsSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "ProcessStatsSource.StatsPerChunk", "10000" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
            Assert.AreEqual(source.StatsPerChunk, 10000);
            var result = source.GetEntropyAsync().GetAwaiter().GetResult();
            Assert.AreEqual(result.Length, 32);
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void NetworkStatsSource_Fuzzing()
        {
            FuzzEntropySource(10, new NetworkStatsSource(), "Entropy_" + nameof(NetworkStatsSource), EntropySourceConfigFromDictionary.Empty, DoNothing).GetAwaiter().GetResult();
            FuzzEntropySource(10, new NetworkStatsSource(), "Entropy_" + nameof(NetworkStatsSource) + "2", new EntropySourceConfigFromDictionary(new[] { "NetworkStatsSource.PeriodMinutes", "0.01" }), Sleep500).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void NetworkStatsSource_IsEnabledByDefault()
        {
            var source = new NetworkStatsSource();
            var initResultCode = source.Initialise(EntropySourceConfigFromDictionary.Empty, GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
        }
        [TestMethod]
        public void NetworkStatsSource_DisabledByConfig()
        {
            var source = new NetworkStatsSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "NetworkStatsSource.Enabled", "False" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.DisabledByConfig);
        }
        [TestMethod]
        public void NetworkStatsSource_ConfigPeriod()
        {
            var source = new NetworkStatsSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "NetworkStatsSource.PeriodMinutes", "100.0" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
            Assert.AreEqual(source.PeriodMinutes, 100.0);
        }
        [TestMethod]
        public void NetworkStatsSource_MaxStatCount()
        {
            var source = new NetworkStatsSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "NetworkStatsSource.StatsPerChunk", "10000" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
            Assert.AreEqual(source.StatsPerChunk, 10000);
            var result = source.GetEntropyAsync().GetAwaiter().GetResult();
            Assert.AreEqual(result.Length, 32);
        }


        [TestMethod]
        [TestCategory("Fuzzing")]
        public void PingStatsSource_Fuzzing()
        {
            FuzzEntropySource(10, new PingStatsSource(), "Entropy_" + nameof(PingStatsSource) + "Fake", EntropySourceConfigFromDictionary.Empty, DoNothing).GetAwaiter().GetResult();
            FuzzEntropySource(4, new PingStatsSource(), "Entropy_" + nameof(PingStatsSource) + "Fake2", new EntropySourceConfigFromDictionary(new[] { "PingStatsSource.PeriodMinutes", "0.01", "PingStatsSource.UseRandomSourceForUnitTests", "True" }), Sleep500).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void PingStatsSource_Network()
        {
            FuzzEntropySource(20, new PingStatsSource(), "Entropy_" + nameof(PingStatsSource), new EntropySourceConfigFromDictionary(new[] { "PingStatsSource.PeriodMinutes", "0.01" }), Sleep500).GetAwaiter().GetResult();
        }
        [TestCategory("Network")]
        public void PingStatsSource_EnsureAllServers()
        {
            throw new NotImplementedException("ping all servers");
        }

        [TestMethod]
        public void PingStatsSource_IsEnabledByDefault()
        {
            var source = new PingStatsSource();
            var initResultCode = source.Initialise(EntropySourceConfigFromDictionary.Empty, GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
        }
        [TestMethod]
        public void PingStatsSource_DisabledByConfig()
        {
            var source = new PingStatsSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "PingStatsSource.Enabled", "False" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.DisabledByConfig);
        }
        [TestMethod]
        public void PingStatsSource_ConfigPeriod()
        {
            var source = new PingStatsSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "PingStatsSource.PeriodMinutes", "100.0" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
            Assert.AreEqual(source.PeriodMinutes, 100.0);
        }
        [TestMethod]
        public void PingStatsSource_ServersPerSample()
        {
            var source = new PingStatsSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "PingStatsSource.ServersPerSample", "20" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
            Assert.AreEqual(source.ServersPerSample, 20);
        }
        [TestMethod]
        public void PingStatsSource_PingsPerSample()
        {
            var source = new PingStatsSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "PingStatsSource.PingsPerSample", "1000" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
            Assert.AreEqual(source.PingsPerSample, 1000);
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void ExternalWebContentSource_Fuzzing()
        {
            FuzzEntropySource(10, new ExternalWebContentSource(), "Entropy_" + nameof(ExternalWebContentSource) + "Fake", new EntropySourceConfigFromDictionary(new[] { "ExternalWebContentSource.UseRandomSourceForUnitTests", "True" }), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void ExternalWebContentSource_Network()
        {
            FuzzEntropySource(20, new ExternalWebContentSource(), "Entropy_" + nameof(ExternalWebContentSource), EntropySourceConfigFromDictionary.Empty, DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void ExternalWebContentSource_TestAllServers()
        {
            throw new NotImplementedException("fetch from all servers");
        }

        [TestMethod]
        public void ExternalWebContentSource_IsEnabledByDefault()
        {
            var source = new ExternalWebContentSource();
            var initResultCode = source.Initialise(EntropySourceConfigFromDictionary.Empty, GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
        }
        [TestMethod]
        public void ExternalWebContentSource_DisabledByConfig()
        {
            var source = new ExternalWebContentSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "ExternalWebContentSource.Enabled", "False" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.DisabledByConfig);
        }
        [TestMethod]
        public void ExternalWebContentSource_ConfigDownloadDelay()
        {
            var source = new ExternalWebContentSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "ExternalWebContentSource.DownloadDelayMinutes", "100.0" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
            Assert.AreEqual(source.DownloadDelayMinutes, 100.0);
        }
        [TestMethod]
        public void ExternalWebContentSource_ServersPerSample()
        {
            var source = new ExternalWebContentSource();
            var initResultCode = source.Initialise(new EntropySourceConfigFromDictionary(new[] { "ExternalWebContentSource.ServersPerSample", "20" }), GetGenerator).GetAwaiter().GetResult();
            Assert.AreEqual(initResultCode.Reason, EntropySourceInitialisationReason.Successful);
            Assert.AreEqual(source.ServersPerSample, 20);
        }


        [TestMethod]
        public void ExternalServerRandomSource_RandomNumbersInfo()
        {
            FuzzEntropySource(1, new ExternalServerRandomSource(), "Entropy_" + nameof(ExternalServerRandomSource) + "_RandomNumbersInfo", DefaultExternalServerConfig("ExternalServerRandomSource.RandomNumbersInfoEnabled", "True"), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_BeaconNist()
        {
            FuzzEntropySource(1, new ExternalServerRandomSource(), "Entropy_" + nameof(ExternalServerRandomSource) + "_BeaconNist", DefaultExternalServerConfig("ExternalServerRandomSource.BeaconNistEnabled", "True"), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_AnuEnabled()
        {
            FuzzEntropySource(1, new ExternalServerRandomSource(), "Entropy_" + nameof(ExternalServerRandomSource) + "_Anu", DefaultExternalServerConfig("ExternalServerRandomSource.AnuEnabled", "True"), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_HotBitsPseudoRandomEnabled()
        {
            FuzzEntropySource(1, new ExternalServerRandomSource(), "Entropy_" + nameof(ExternalServerRandomSource) + "_HotBitsPseudoRandom", DefaultExternalServerConfig("ExternalServerRandomSource.HotBitsPseudoRandomEnabled", "True"), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_HotBitsTrueRandomEnabled()
        {
            FuzzEntropySource(1, new ExternalServerRandomSource(), "Entropy_" + nameof(ExternalServerRandomSource) + "_HotBitsTrueRandom", DefaultExternalServerConfig("ExternalServerRandomSource.HotBitsTrueRandomEnabled", "True", "ExternalServerRandomSource.HotBitsApiKey", "HB1sampleKey"), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_RandomOrgPublicEnabled()
        {
            FuzzEntropySource(1, new ExternalServerRandomSource(), "Entropy_" + nameof(ExternalServerRandomSource) + "_RandomOrgPublic", DefaultExternalServerConfig("ExternalServerRandomSource.RandomOrgEnabled", "True"), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_RandomOrgApiEnabled()
        {
            FuzzEntropySource(1, new ExternalServerRandomSource(), "Entropy_" + nameof(ExternalServerRandomSource) + "_RandomOrgApi", DefaultExternalServerConfig("ExternalServerRandomSource.RandomOrgApiEnabled", "True", "ExternalServerRandomSource.RandomOrgApiKey", "a7fad70c-5b04-4124-b9a2-d802f4a7689f"), DoNothing).GetAwaiter().GetResult();
        }
        [TestMethod]
        public void ExternalServerRandomSource_RandomServerEnabled()
        {
            FuzzEntropySource(1, new ExternalServerRandomSource(), "Entropy_" + nameof(ExternalServerRandomSource) + "RandomServer", DefaultExternalServerConfig("ExternalServerRandomSource.RandomServerEnabled", "True"), DoNothing).GetAwaiter().GetResult();
        }

        // TODO: live network tests for all these sources.
        
        [TestMethod]
        [TestCategory("Fuzzing")]
        public void ExternalServerRandomSource_FuzzingAll()
        {
            FuzzEntropySource(20, new ExternalServerRandomSource(), "Entropy_" + nameof(ExternalServerRandomSource) + "Fake", new EntropySourceConfigFromDictionary(new[] { "ExternalServerRandomSource.UseDiskSourceForUnitTests", "True" }), DoNothing).GetAwaiter().GetResult();
            FuzzEntropySource(20, new ExternalServerRandomSource(), "Entropy_" + nameof(ExternalServerRandomSource) + "Fake2", new EntropySourceConfigFromDictionary(new[] { "ExternalServerRandomSource.UseDiskSourceForUnitTests", "True" }), Sleep500).GetAwaiter().GetResult();
        }
        [TestMethod]
        [TestCategory("Network")]
        public void ExternalServerRandomSource_Network()
        {
            FuzzEntropySource(50, new ExternalServerRandomSource(), "Entropy_" + nameof(ExternalServerRandomSource), EntropySourceConfigFromDictionary.Empty, DoNothing).GetAwaiter().GetResult();
        }


        private async Task FuzzEntropySource(int iterations, IEntropySource source, string filename, IEntropySourceConfig conf, Action extra)
        {
            using (var sw = new StreamWriter(filename + ".txt", false, Encoding.UTF8))
            {
                await sw.WriteLineAsync($"{source.GetType().FullName} - {iterations:N0} iterations");

                var initResult = await source.Initialise(conf, GetGenerator);
                await sw.WriteLineAsync($"Init result: {initResult.Reason}");
                if (initResult.Reason != EntropySourceInitialisationReason.Successful)
                {
                    await sw.WriteLineAsync($"Exception: {initResult.Exception}");
                    throw new Exception("Init failed.", initResult.Exception);
                }

                for (int i = 0; i < iterations; i++)
                {
                    var bytes = await source.GetEntropyAsync();
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

        private EntropySourceConfigFromDictionary DefaultExternalServerConfig(params string[] additionalOverrides)
        {
            // By default, all sources are disabled for unit tests.
            // We'd then enabled them one by one for specific tests.
            return new EntropySourceConfigFromDictionary(new[] {
                "ExternalServerRandomSource.RandomOrgPublicEnabled", "False",
                "ExternalServerRandomSource.RandomOrgApiEnabled", "False",
                "ExternalServerRandomSource.AnuEnabled", "False",
                "ExternalServerRandomSource.RandomNumbersInfoEnabled", "False",
                "ExternalServerRandomSource.RandomServerEnabled", "False",
                "ExternalServerRandomSource.HotBitsPseudoRandomEnabled", "False",
                "ExternalServerRandomSource.HotBitsTrueRandomEnabled", "False",
                "ExternalServerRandomSource.BeaconNistEnabled", "False",
                
                // Don't forget to load source data from unit test files.
                "ExternalServerRandomSource.UseDiskSourceForUnitTests", "True",

                // Order isn't randomised for unit tests.
                "ExternalServerRandomSource.RandomiseSourceOrder", "False",
            }.Concat(additionalOverrides ?? new string[0]));
        }
    }
}
