using System;
using System.Security.Cryptography;
using System.Linq;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Test;
using MurrayGrant.Terninger.Accumulator;
using Rand = System.Random;

namespace MurrayGrant.Terninger.Test.Slow
{
    [TestClass]
    public class AccumulatorTests
    {
        private byte[] _Zero8Bytes = new byte[8];
        private byte[] _Incrementing8Bytes = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
        private byte[] _Zero16Bytes = new byte[16];
        private byte[] _Incrementing16Bytes = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
        private byte[] _Zero1KBytes = new byte[1*1024];
        private byte[] _Zero2KBytes = new byte[2*1024];

        private IRandomNumberGenerator _Rng = CreateRandomGenerator();

        [TestMethod]
        public void Pool_List2InterleavedSources()
        {
            var p = new EntropyPool();
            var s1 = new NullSource();
            var s2 = new NullSource();
            var e1 = new EntropyEvent(_Zero16Bytes, s1);
            var e2 = new EntropyEvent(_Zero16Bytes, s2);

            using (var sw = new StreamWriter("pool_2interleaved.txt", false, Encoding.UTF8))
            {
                sw.WriteLine($"Total: Source 1 : Source 2");
                for (int i = 0; i < 10000; i++)
                {
                    if (i % 2 == 0)
                        p.Add(e1);
                    else
                        p.Add(e2);
                    var counts = p.GetCountOfBytesBySource();
                    if (counts.Count == 2)
                        sw.WriteLine($"{p.EntropyBytesSinceLastDigest} :{counts[s1]:N0} :{counts[s2]:N0}");
                }
            }
        }
        [TestMethod]
        public void Pool_List2BiasedSources()
        {
            var p = new EntropyPool();
            var s1 = new NullSource();
            var s2 = new NullSource();
            var e1 = new EntropyEvent(_Zero16Bytes, s1);
            var e2 = new EntropyEvent(_Zero16Bytes, s2);

            using (var sw = new StreamWriter("pool_2biased.txt", false, Encoding.UTF8))
            {
                sw.WriteLine($"Total: Source 1 : Source 2");
                for (int i = 0; i < 10000; i++)
                {
                    if (i % 3 == 0)
                        p.Add(e1);
                    else
                        p.Add(e2);
                    var counts = p.GetCountOfBytesBySource();
                    if (counts.Count == 2)
                        sw.WriteLine($"{p.EntropyBytesSinceLastDigest} :{counts[s1]:N0} :{counts[s2]:N0}");
                }
            }
        }

        [TestMethod]
        public void Pool_List3InterleavedSources()
        {
            var p = new EntropyPool();
            var s1 = new NullSource();
            var s2 = new NullSource();
            var s3 = new NullSource();
            var e1 = new EntropyEvent(_Zero16Bytes, s1);
            var e2 = new EntropyEvent(_Zero16Bytes, s2);
            var e3 = new EntropyEvent(_Zero16Bytes, s3);

            using (var sw = new StreamWriter("pool_3interleaved.txt", false, Encoding.UTF8))
            {
                sw.WriteLine($"Total: Source 1 : Source 2 : Source 3");
                for (int i = 0; i < 10000; i++)
                {
                    if (i % 3 == 0)
                        p.Add(e1);
                    else if (i % 3 == 1)
                        p.Add(e2);
                    else
                        p.Add(e3);
                    var counts = p.GetCountOfBytesBySource();
                    if (counts.Count == 3)
                        sw.WriteLine($"{p.EntropyBytesSinceLastDigest} :{counts[s1]:N0} :{counts[s2]:N0} :{counts[s3]:N0}");
                }
            }
        }
        [TestMethod]
        public void Pool_List3BiasedSources()
        {
            var p = new EntropyPool();
            var s1 = new NullSource();
            var s2 = new NullSource();
            var s3 = new NullSource();
            var e1 = new EntropyEvent(_Zero16Bytes, s1);
            var e2 = new EntropyEvent(_Zero16Bytes, s2);
            var e3 = new EntropyEvent(_Zero16Bytes, s3);

            using (var sw = new StreamWriter("pool_3biased.txt", false, Encoding.UTF8))
            {
                sw.WriteLine($"Total: Source 1 : Source 2 : Source 3");
                for (int i = 0; i < 10000; i++)
                {
                    if (i % 6 == 0)
                        p.Add(e1);
                    else if (i % 6 == 1 || i % 6 == 2)
                        p.Add(e2);
                    else
                        p.Add(e3);
                    var counts = p.GetCountOfBytesBySource();
                    if (counts.Count == 3)
                        sw.WriteLine($"{p.EntropyBytesSinceLastDigest} :{counts[s1]:N0} :{counts[s2]:N0} :{counts[s3]:N0}");
                }
            }
        }



        [TestMethod]
        public void Accumulator_ListLinearPoolsUsedIn1000SeedEvents()
        {
            var a = new EntropyAccumulator(32, 0, _Rng);
            using (var sw = new StreamWriter("accumulator_linearUsage.txt", false, Encoding.UTF8))
            {
                sw.WriteLine($"Seed:Ent'py:# :Pools Used (linear)             :Pools Used (random)        ");
                for (int i = 0; i < 10000; i++)
                {
                    for (int j = 0; j < a.TotalPoolCount; j++)
                        a.Add(EventFromBytes(_Incrementing16Bytes));
                    var availableEntropy = (long)a.AvailableEntropyBytesSinceLastSeed;
                    var seed = a.NextSeed();
                    sw.WriteLine($"{i+1:00000}:{availableEntropy:000000}:{a.PoolCountUsedInLastSeedGeneration:00}:{Convert.ToString((long)a.LinearPoolsUsedInLastSeedGeneration, 2),32}:{Convert.ToString((long)a.RandomPoolsUsedInLastSeedGeneration, 2),32}");
                    Assert.IsTrue(availableEntropy >= a.TotalPoolCount * _Incrementing16Bytes.Length);      // Not every pool is used, so we may have extra bytes here. 
                    Assert.IsTrue(seed.Length > 0);
                }
            }
            Assert.AreEqual(a.TotalReseedEvents, 10000);
        }
        [TestMethod]
        public void Accumulator_ListRandomPoolsUsedIn1000SeedEvents()
        {
            var a = new EntropyAccumulator(0, 32, _Rng);
            using (var sw = new StreamWriter("accumulator_randomUsage.txt", false, Encoding.UTF8))
            {
                sw.WriteLine($"Seed:Ent'py:# :Pools Used (linear)             :Pools Used (random)        ");
                for (int i = 0; i < 10000; i++)
                {
                    for (int j = 0; j < a.TotalPoolCount; j++)
                        a.Add(EventFromBytes(_Incrementing16Bytes));
                    var availableEntropy = (long)a.AvailableEntropyBytesSinceLastSeed;
                    var seed = a.NextSeed();
                    sw.WriteLine($"{i + 1:00000}:{availableEntropy:000000}:{a.PoolCountUsedInLastSeedGeneration:00}:{Convert.ToString((long)a.LinearPoolsUsedInLastSeedGeneration, 2),32}:{Convert.ToString((long)a.RandomPoolsUsedInLastSeedGeneration, 2),32}");
                    Assert.IsTrue(availableEntropy >= a.TotalPoolCount * _Incrementing16Bytes.Length);      // Not every pool is used, so we may have extra bytes here. 
                    Assert.IsTrue(seed.Length > 0);
                }
            }
            Assert.AreEqual(a.TotalReseedEvents, 10000);
        }
        [TestMethod]
        public void Accumulator_ListLinearAndRandomPoolsUsedIn1000SeedEvents()
        {
            var a = new EntropyAccumulator(16, 16, _Rng);
            using (var sw = new StreamWriter("accumulator_linearAndRandomUsage.txt", false, Encoding.UTF8))
            {
                sw.WriteLine($"Seed:Ent'py:# :Pools Used (linear)             :Pools Used (random)        ");
                for (int i = 0; i < 10000; i++)
                {
                    for (int j = 0; j < a.TotalPoolCount; j++)
                        a.Add(EventFromBytes(_Incrementing16Bytes));
                    var availableEntropy = (long)a.AvailableEntropyBytesSinceLastSeed;
                    var seed = a.NextSeed();
                    sw.WriteLine($"{i + 1:00000}:{availableEntropy:000000}:{a.PoolCountUsedInLastSeedGeneration:00}:{Convert.ToString((long)a.LinearPoolsUsedInLastSeedGeneration, 2),32}:{Convert.ToString((long)a.RandomPoolsUsedInLastSeedGeneration, 2),32}");
                    Assert.IsTrue(availableEntropy >= a.TotalPoolCount * _Incrementing16Bytes.Length);      // Not every pool is used, so we may have extra bytes here. 
                    Assert.IsTrue(seed.Length > 0);
                }
            }
            Assert.AreEqual(a.TotalReseedEvents, 10000);
        }
        [TestMethod]
        public void Accumulator_ListDefaultLinearAndRandomPoolsUsedIn1000SeedEvents()
        {
            var defA = new EntropyAccumulator();
            var a = new EntropyAccumulator(defA.LinearPoolCount, defA.RandomPoolCount, _Rng);
            using (var sw = new StreamWriter("accumulator_defaultLinearAndRandomUsage.txt", false, Encoding.UTF8))
            {
                sw.WriteLine($"Seed:Ent'py:# :Pools Used (linear)             :Pools Used (random)        ");
                for (int i = 0; i < 10000; i++)
                {
                    for (int j = 0; j < a.TotalPoolCount; j++)
                        a.Add(EventFromBytes(_Incrementing16Bytes));
                    var availableEntropy = (long)a.AvailableEntropyBytesSinceLastSeed;
                    var seed = a.NextSeed();
                    sw.WriteLine($"{i + 1:00000}:{availableEntropy:000000}:{a.PoolCountUsedInLastSeedGeneration:00}:{Convert.ToString((long)a.LinearPoolsUsedInLastSeedGeneration, 2),32}:{Convert.ToString((long)a.RandomPoolsUsedInLastSeedGeneration, 2),32}");
                    Assert.IsTrue(availableEntropy >= a.TotalPoolCount * _Incrementing16Bytes.Length);      // Not every pool is used, so we may have extra bytes here. 
                    Assert.IsTrue(seed.Length > 0);
                }
            }
            Assert.AreEqual(a.TotalReseedEvents, 10000);
        }


        private static IRandomNumberGenerator CreateRandomGenerator() => new StandardRandomWrapperGenerator(new Rand(1));
        private static EntropyEvent EventFromBytes(byte[] bytes)
        {
            return new EntropyEvent(bytes, new NullSource());
        }
    }
}
