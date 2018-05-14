using System;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Accumulator;
using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Test;
using MurrayGrant.Terninger.EntropySources.Local;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class PooledGeneratorTests
    {
        [TestMethod]
        public void ConstructMinimal()
        {
            var sources = new IEntropySource[] { new NullSource(), new NullSource() };
            var rng = new PooledEntropyCprngGenerator(sources);
            // Creating a generator should not actually generate any bytes or even start the generator.
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
        }
        [TestMethod]
        public void ConstructWithAccumulator()
        {
            var sources = new IEntropySource[] { new NullSource(), new NullSource() };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng = new PooledEntropyCprngGenerator(sources, acc);
            // Creating a generator should not actually generate any bytes or even start the generator.
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
        }
        [TestMethod]
        public void ConstructWithPrng()
        {
            var sources = new IEntropySource[] { new NullSource(), new NullSource() };
            var prng = new CypherBasedPrngGenerator(new StandardRandomWrapperGenerator().GetRandomBytes(32));
            var rng = new PooledEntropyCprngGenerator(sources, prng);
            // Creating a generator should not actually generate any bytes or even start the generator.
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
        }
        [TestMethod]
        public void ConstructWithAccumulatorAndPrng()
        {
            var sources = new IEntropySource[] { new NullSource(), new NullSource() };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var prng = new CypherBasedPrngGenerator(new StandardRandomWrapperGenerator().GetRandomBytes(32));
            var rng = new PooledEntropyCprngGenerator(sources, acc, prng);
            // Creating a generator should not actually generate any bytes or even start the generator.
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
        }

        [TestMethod]
        public void ConstructWithLocalSources()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(), new CurrentTimeSource(), new GCMemorySource(), new NetworkStatsSource(), new ProcessStatsSource(), new TimerSource() };
            var rng = new PooledEntropyCprngGenerator(sources);
            // Creating a generator should not actually generate any bytes or even start the generator.
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 6);
        }

        [TestMethod]
        public async Task InitialiseWithLocalSources()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(), new CurrentTimeSource(), new GCMemorySource(), new NetworkStatsSource(), new ProcessStatsSource(), new TimerSource() };
            var rng = new PooledEntropyCprngGenerator(sources);
            // Creating a generator should not actually generate any bytes or even start the generator.
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 6);

            await rng.Stop();
        }

        [TestMethod]
        public async Task GetFirstSeed()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(64), new CurrentTimeSource(), new GCMemorySource(), new TimerSource(), new UserSuppliedSource(CypherBasedPrngGenerator.CreateWithCheapKey().GetRandomBytes(2048)) };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng = new PooledEntropyCprngGenerator(sources, acc);
            // Creating a generator should not actually generate any bytes or even start the generator.
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 5);

            await rng.StartAndWaitForFirstSeed();
            Assert.IsTrue(rng.ReseedCount >= 1);
            Assert.IsTrue(acc.TotalEntropyBytes > 0);
            Assert.AreNotEqual(rng.EntropyPriority, EntropyPriority.High);

            await rng.Stop();
        }

        [TestMethod]
        public async Task GetRandomBytes()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(64), new CurrentTimeSource(), new GCMemorySource(), new TimerSource(), new UserSuppliedSource(CypherBasedPrngGenerator.CreateWithCheapKey().GetRandomBytes(2048)) };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng = new PooledEntropyCprngGenerator(sources, acc);
            // Creating a generator should not actually generate any bytes or even start the generator.
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 5);

            await rng.StartAndWaitForFirstSeed();
            Assert.IsTrue(rng.ReseedCount >= 1);
            Assert.IsTrue(acc.TotalEntropyBytes > 0);
            Assert.AreNotEqual(rng.EntropyPriority, EntropyPriority.High);

            var bytes = rng.GetRandomBytes(16);
            Assert.IsFalse(bytes.All(b => b == 0));

            await rng.Stop();
        }

        [TestMethod]
        public async Task ForceReseed()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(64), new CurrentTimeSource(), new GCMemorySource(), new TimerSource(), new UserSuppliedSource(CypherBasedPrngGenerator.CreateWithCheapKey().GetRandomBytes(2048)) };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng = new PooledEntropyCprngGenerator(sources, acc);
            // Creating a generator should not actually generate any bytes or even start the generator.
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 5);

            await rng.StartAndWaitForFirstSeed();
            Assert.IsTrue(rng.ReseedCount >= 1);
            Assert.IsTrue(acc.TotalEntropyBytes > 0);
            Assert.AreNotEqual(rng.EntropyPriority, EntropyPriority.High);

            var reseedCount = rng.ReseedCount;
            var entropy = acc.TotalEntropyBytes;
            await rng.Reseed();
            Assert.IsTrue(rng.ReseedCount > reseedCount);
            Assert.IsTrue(acc.TotalEntropyBytes > entropy);

            await rng.Stop();
        }

        [TestMethod]
        public async Task EventIsRaisedOnReseed()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(64), new CurrentTimeSource(), new GCMemorySource(), new TimerSource(), new UserSuppliedSource(CypherBasedPrngGenerator.CreateWithCheapKey().GetRandomBytes(2048)) };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng = new PooledEntropyCprngGenerator(sources, acc);
            var reseedCountOnEvent = rng.ReseedCount;
            rng.OnReseed += (o, e) => reseedCountOnEvent = e.ReseedCount;
            // Creating a generator should not actually generate any bytes or even start the generator.
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 5);

            await rng.StartAndWaitForFirstSeed();
            Assert.AreEqual(reseedCountOnEvent, rng.ReseedCount);

            await rng.Stop();
        }

        [TestMethod]
        public async Task CreatePrngFromPooled()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(64), new CurrentTimeSource(), new GCMemorySource(), new TimerSource(), new UserSuppliedSource(CypherBasedPrngGenerator.CreateWithCheapKey().GetRandomBytes(2048)) };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng = new PooledEntropyCprngGenerator(sources, acc);
            // Creating a generator should not actually generate any bytes or even start the generator.
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 5);

            await rng.StartAndWaitForFirstSeed();
            Assert.IsTrue(rng.ReseedCount >= 1);
            Assert.IsTrue(acc.TotalEntropyBytes > 0);
            Assert.AreNotEqual(rng.EntropyPriority, EntropyPriority.High);

            var prng = rng.CreatePrng();
            Assert.IsNotNull(prng);

            var bytes = prng.GetRandomBytes(16);
            Assert.IsFalse(bytes.All(b => b == 0));

            await rng.Stop();
        }

    }
}
