using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Accumulator;
using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Test;
using MurrayGrant.Terninger.EntropySources.Local;
using MurrayGrant.Terninger.PersistentState;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class PooledGeneratorTests
    {
        [TestMethod]
        public void ConstructMinimal()
        {
            var sources = new IEntropySource[] { new NullSource(), new NullSource() };
            var rng = PooledEntropyCprngGenerator.Create(initialisedSources: sources);
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
            var rng = PooledEntropyCprngGenerator.Create(initialisedSources: sources, accumulator: acc);
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
            var rng = PooledEntropyCprngGenerator.Create(initialisedSources: sources, prng: prng);
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
            var rng = PooledEntropyCprngGenerator.Create(initialisedSources: sources, accumulator: acc, prng: prng);
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
            var rng = PooledEntropyCprngGenerator.Create(initialisedSources: sources);
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
            var rng = PooledEntropyCprngGenerator.Create(initialisedSources: sources);
            // Creating a generator should not actually generate any bytes or even start the generator.
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 6);

            await rng.Stop();
        }

        [TestMethod]
        public async Task GetFirstSeed_AllSyncSources()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(64), new CurrentTimeSource(), new GCMemorySource(), new TimerSource(), new UserSuppliedSource(CypherBasedPrngGenerator.CreateWithCheapKey().GetRandomBytes(2048)) };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng = PooledEntropyCprngGenerator.Create(sources, accumulator: acc, config: Conf());
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 5);

            await rng.StartAndWaitForFirstSeed();
            Assert.IsTrue(rng.ReseedCount >= 1);
            Assert.IsTrue(acc.TotalEntropyBytes > 0);
            System.Threading.Thread.Sleep(1);           // EntropyPriority is only updated after the reseed event, so it might not be current.
            Assert.AreNotEqual(rng.EntropyPriority, EntropyPriority.High);

            await rng.Stop();
        }

        [TestMethod]
        public async Task GetFirstSeed_AllAsyncSources()
        {
            var sources = new IEntropySource[] { new AsyncCryptoRandomSource(), new ProcessStatsSource(), new NetworkStatsSource() };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng = PooledEntropyCprngGenerator.Create(sources, accumulator: acc, config: Conf());
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 3);

            await rng.StartAndWaitForFirstSeed();
            Assert.IsTrue(rng.ReseedCount >= 1);
            Assert.IsTrue(acc.TotalEntropyBytes > 0);
            System.Threading.Thread.Sleep(1);           // EntropyPriority is only updated after the reseed event, so it might not be current.
            Assert.AreNotEqual(rng.EntropyPriority, EntropyPriority.High);

            await rng.Stop();
        }

        [TestMethod]
        public async Task GetFirstSeed_MixedSyncAndAsyncSources()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(32), new CurrentTimeSource(), new GCMemorySource(), new TimerSource(), new AsyncCryptoRandomSource(), new ProcessStatsSource(), new NetworkStatsSource() };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng = PooledEntropyCprngGenerator.Create(sources, accumulator: acc, config: Conf());
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 7);

            await rng.StartAndWaitForFirstSeed();
            Assert.IsTrue(rng.ReseedCount >= 1);
            Assert.IsTrue(acc.TotalEntropyBytes > 0);
            System.Threading.Thread.Sleep(1);           // EntropyPriority is only updated after the reseed event, so it might not be current.
            Assert.AreNotEqual(rng.EntropyPriority, EntropyPriority.High);

            await rng.Stop();
        }

        [TestMethod]
        public async Task GetFirstSeed_WithPersistentState()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(64), new CurrentTimeSource(), new GCMemorySource(), new TimerSource(), new UserSuppliedSource(CypherBasedPrngGenerator.CreateWithCheapKey().GetRandomBytes(2048)) };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var testState = new InMemoryState(new NamespacedPersistentItem[]
            {
                NamespacedPersistentItem.CreateBinary("UniqueID", Guid.Parse("351de340-be56-46e0-b843-9bd3ca952afa").ToByteArray(), theNamespace: "PooledEntropyCprngGenerator")
            });
            var rng = PooledEntropyCprngGenerator.Create(sources, accumulator: acc, config: Conf(), persistentStateReader: testState, persistentStateWriter: testState);
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 5);
            Assert.AreEqual(rng.UniqueId, Guid.Empty);

            await rng.StartAndWaitForFirstSeed();
            Assert.AreEqual(rng.UniqueId, Guid.Parse("351de340-be56-46e0-b843-9bd3ca952afa"));
            Assert.IsTrue(rng.ReseedCount >= 1);
            Assert.IsTrue(acc.TotalEntropyBytes > 0);
            System.Threading.Thread.Sleep(1);           // EntropyPriority is only updated after the reseed event, so it might not be current.
            Assert.AreNotEqual(rng.EntropyPriority, EntropyPriority.High);

            _ = rng.GetRandomBytes(1024);
            
            await rng.Stop();
            var peristedUniqueId = new Guid(testState.Items["PooledEntropyCprngGenerator"]["UniqueID"].Value);
            var peristedBytesRequested = BigMath.Int128.Parse(testState.Items["PooledEntropyCprngGenerator"]["BytesRequested"].ValueAsUtf8Text);
            Assert.AreEqual(peristedUniqueId, Guid.Parse("351de340-be56-46e0-b843-9bd3ca952afa"));
            Assert.AreEqual(peristedBytesRequested, (BigMath.Int128)1024);
        }

        [TestMethod]
        public async Task GetRandomBytes()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(64), new CurrentTimeSource(), new GCMemorySource(), new TimerSource(), new UserSuppliedSource(CypherBasedPrngGenerator.CreateWithCheapKey().GetRandomBytes(2048)) };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng = PooledEntropyCprngGenerator.Create(sources, accumulator: acc, config: Conf());
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
            var rng = PooledEntropyCprngGenerator.Create(sources, accumulator: acc, config: Conf());
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
            var rng = PooledEntropyCprngGenerator.Create(sources, accumulator: acc, config: Conf());
            Object setInReseedEvent = null;
            rng.OnReseed += (o, e) => setInReseedEvent = new object();
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 5);

            await rng.StartAndWaitForFirstSeed();
            Assert.IsNotNull(setInReseedEvent);

            await rng.Stop();
        }

        [TestMethod]
        public async Task FirstSeedYieldsDifferentBytes()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(64), new CurrentTimeSource(), new GCMemorySource(), new TimerSource(), new UserSuppliedSource(CypherBasedPrngGenerator.CreateWithCheapKey().GetRandomBytes(2048)) };
            var acc1 = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng1 = PooledEntropyCprngGenerator.Create(sources, accumulator: acc1, config: Conf());
            await rng1.StartAndWaitForFirstSeed();

            var acc2 = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng2 = PooledEntropyCprngGenerator.Create(sources, accumulator: acc2, config: Conf());
            await rng2.StartAndWaitForFirstSeed();

            var bytesFrom1 = rng1.GetRandomBytes(64);
            Assert.IsFalse(bytesFrom1.All(b => b == 0));
            var bytesFrom2 = rng2.GetRandomBytes(64);
            Assert.IsFalse(bytesFrom2.All(b => b == 0));
            // Expect two generates with different inputs will give different bytes from from the start.
            Assert.IsFalse(bytesFrom1.SequenceEqual(bytesFrom2));

            await rng1.Stop();
            await rng2.Stop();
        }


        [TestMethod]
        public async Task CreatePrngFromPooled()
        {
            var sources = new IEntropySource[] { new CryptoRandomSource(64), new CurrentTimeSource(), new GCMemorySource(), new TimerSource(), new UserSuppliedSource(CypherBasedPrngGenerator.CreateWithCheapKey().GetRandomBytes(2048)) };
            var acc = new EntropyAccumulator(new StandardRandomWrapperGenerator());
            var rng = PooledEntropyCprngGenerator.Create(sources, accumulator: acc, config: Conf());
            Assert.AreEqual(rng.BytesRequested, 0);
            Assert.AreEqual(rng.ReseedCount, 0);
            Assert.AreEqual(rng.IsRunning, false);
            Assert.AreEqual(rng.EntropyPriority, EntropyPriority.High);
            Assert.AreEqual(rng.SourceCount, 5);

            await rng.StartAndWaitForFirstSeed();
            Assert.IsTrue(rng.ReseedCount >= 1);
            Assert.IsTrue(acc.TotalEntropyBytes > 0);
            System.Threading.Thread.Sleep(1);           // EntropyPriority is only updated after the reseed event, so it might not be current.
            Assert.AreNotEqual(rng.EntropyPriority, EntropyPriority.High);

            var prng = rng.CreateCypherBasedGenerator();
            Assert.IsNotNull(prng);

            var bytes = prng.GetRandomBytes(16);
            Assert.IsFalse(bytes.All(b => b == 0));

            await rng.Stop();
        }


        private PooledEntropyCprngGenerator.PooledGeneratorConfig Conf() => new PooledEntropyCprngGenerator.PooledGeneratorConfig()
        {
            MinimumTimeBetweenReseeds = TimeSpan.FromTicks(1),
        };

        [AsyncHint(IsAsync.Always)]
        public class AsyncCryptoRandomSource : IEntropySource
        {
            private RandomNumberGenerator _Rng;
            private int _ResultLength;

            public string Name { get; set; }

            public AsyncCryptoRandomSource() : this(16, RandomNumberGenerator.Create()) { }
            public AsyncCryptoRandomSource(int resultLength) : this(resultLength, RandomNumberGenerator.Create()) { }
            public AsyncCryptoRandomSource(int resultLength, RandomNumberGenerator rng)
            {
                this._ResultLength = resultLength;
                this._Rng = rng;
            }

            public void Dispose()
            {
                _Rng.Dispose();
            }

            public async Task<byte[]> GetEntropyAsync(EntropyPriority priority)
            {
                var result = new byte[_ResultLength];
                _Rng.GetBytes(result);
                await Task.Delay(50);
                return result;
            }
        }

        public class InMemoryState : IPersistentStateReader, IPersistentStateWriter
        {
            public InMemoryState(IEnumerable<NamespacedPersistentItem> initialState)
            {
                Items = new PersistentItemCollection(initialState);
            }

            public PersistentItemCollection Items;

            public string Location => "RAM";

            public Task<PersistentItemCollection> ReadAsync()
                => Task.FromResult(Items);
    
            public Task WriteAsync(PersistentItemCollection items)
            {
                Items = items;
                return Task.CompletedTask;
            }
        }
    }
}
