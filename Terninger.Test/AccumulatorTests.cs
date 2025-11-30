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

namespace MurrayGrant.Terninger.Test
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
        public void Pool_NoEntropyIsZero()
        {
            var p = new EntropyPool();
            Assert.AreEqual(0, p.TotalEntropyBytes);
            Assert.AreEqual(0, p.EntropyBytesSinceLastDigest);
        }
        [TestMethod]
        public void Pool_NoEntropyIsSameDigest()
        {
            var p1 = new EntropyPool();
            var p2 = new EntropyPool();
            CollectionAssert.AreEqual(p1.GetDigest(), p2.GetDigest());
        }
        [TestMethod]
        public void Pool_OneEventAddsToCounters()
        {
            var p = new EntropyPool();
            p.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual(8, p.TotalEntropyBytes);
            Assert.AreEqual(8, p.EntropyBytesSinceLastDigest);
        }
        [TestMethod]
        public void Pool_OneEventDigestIsNonZero()
        {
            var p = new EntropyPool();
            p.Add(EventFromBytes(_Zero8Bytes));
            Assert.IsFalse(p.GetDigest().All(b => b == 0));
        }
        [TestMethod]
        public void Pool_TwoEventAddsToCounters()
        {
            var p = new EntropyPool();
            p.Add(EventFromBytes(_Zero8Bytes));
            p.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual(16, p.TotalEntropyBytes);
            Assert.AreEqual(16, p.EntropyBytesSinceLastDigest);
        }

        [TestMethod]
        public void Pool_CountersResetAfterDigest()
        {
            var p = new EntropyPool();
            p.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual(8, p.TotalEntropyBytes);
            Assert.AreEqual(8, p.EntropyBytesSinceLastDigest);
            p.GetDigest();
            Assert.AreEqual(8, p.TotalEntropyBytes);
            Assert.AreEqual(0, p.EntropyBytesSinceLastDigest);

            p.Add(EventFromBytes(_Zero16Bytes));
            Assert.AreEqual(24, p.TotalEntropyBytes);
            Assert.AreEqual(16, p.EntropyBytesSinceLastDigest);
            p.GetDigest();
            Assert.AreEqual(24, p.TotalEntropyBytes);
            Assert.AreEqual(0, p.EntropyBytesSinceLastDigest);
        }


        [TestMethod]
        public void Pool_SameEntropyIsSameDigest()
        {
            var p1 = new EntropyPool();
            var p2 = new EntropyPool();
            p1.Add(EventFromBytes(_Zero8Bytes));
            p2.Add(EventFromBytes(_Zero8Bytes));
            CollectionAssert.AreEqual(p1.GetDigest(), p2.GetDigest());
            p1.Add(EventFromBytes(_Incrementing16Bytes));
            p2.Add(EventFromBytes(_Incrementing16Bytes));
            CollectionAssert.AreEqual(p1.GetDigest(), p2.GetDigest());
        }

        [TestMethod]
        public void Pool_DifferentEntropyIsDifferentDigest()
        {
            var p1 = new EntropyPool();
            var p2 = new EntropyPool();
            p1.Add(EventFromBytes(_Zero8Bytes));
            p2.Add(EventFromBytes(_Incrementing8Bytes));
            CollectionAssert.AreNotEqual(p1.GetDigest(), p2.GetDigest());
            p1.Add(EventFromBytes(_Zero16Bytes));
            p2.Add(EventFromBytes(_Incrementing16Bytes));
            CollectionAssert.AreNotEqual(p1.GetDigest(), p2.GetDigest());
        }

        [TestMethod]
        public void Pool_CanUseDifferentHashAlgorithm()
        {
            var p = new EntropyPool(SHA256.Create());
            p.Add(EventFromBytes(_Zero8Bytes));
            Assert.IsFalse(p.GetDigest().All(b => b == 0));
        }

        //[TestMethod]
        public void Pool_CanUseNonShaBasedAlgorithm()
        {
            var blake2b = System.Data.HashFunction.Blake2.Blake2BFactory.Instance.Create();
          
            Assert.Fail("TODO: must change the EntropyPool to accept an object which we can plug various hash implementations into.");
            var p = new EntropyPool(SHA256.Create());
            p.Add(EventFromBytes(_Zero8Bytes));
            Assert.IsFalse(p.GetDigest().All(b => b == 0));
        }

#if NETCOREAPP2_1 || NETCOREAPP3_0
        [TestMethod]
        public void Pool_CanUseIncrementalHash()
        {
            var p = new EntropyPool(IncrementalHash.CreateHash(HashAlgorithmName.SHA256));
            p.Add(EventFromBytes(_Zero8Bytes));
            Assert.IsFalse(p.GetDigest().All(b => b == 0));
        }
#endif

        [TestMethod]
        public void Pool_First48BytesAreOK()
        {
            var p = new EntropyPool();
            var evt = EventFromBytes(_Zero8Bytes);
            p.Add(evt);
            p.Add(evt);
            p.Add(evt);
            p.Add(evt);
            p.Add(evt);
            p.Add(evt);
            Assert.AreEqual(48, p.TotalEntropyBytes);
            Assert.AreEqual(48, p.EntropyBytesSinceLastDigest);
        }
        [TestMethod]
        public void Pool_After48BytesAreIgnored()
        {
            var p = new EntropyPool();
            var evt = EventFromBytes(_Zero8Bytes);
            p.Add(evt);
            p.Add(evt);
            p.Add(evt);
            p.Add(evt);
            p.Add(evt);
            p.Add(evt);
            p.Add(evt);     // This triggers the "too much entropy from one source" thing.
            Assert.AreEqual(48, p.TotalEntropyBytes);
            Assert.AreEqual(48, p.EntropyBytesSinceLastDigest);
        }
        [TestMethod]
        public void Pool_TwoSourcesCanGoBeyond48BytesEachWhenEvenlyDistributed()
        {
            var p = new EntropyPool();
            var evt = EventFromBytes(_Zero16Bytes);
            var evt2 = EventFromBytes(_Zero16Bytes);
            p.Add(evt);
            p.Add(evt2);
            p.Add(evt);
            p.Add(evt2);
            p.Add(evt);
            p.Add(evt2);
            p.Add(evt);
            p.Add(evt2);
            Assert.AreEqual(128, p.TotalEntropyBytes);
            Assert.AreEqual(128, p.EntropyBytesSinceLastDigest);
        }
        [TestMethod]
        public void Pool_ThreeSourcesCanGoBeyond48BytesEachWhenEvenlyDistributed()
        {
            var p = new EntropyPool();
            var evt = EventFromBytes(_Zero16Bytes);
            var evt2 = EventFromBytes(_Zero16Bytes);
            var evt3 = EventFromBytes(_Zero16Bytes);
            p.Add(evt);
            p.Add(evt2);
            p.Add(evt3);
            p.Add(evt);
            p.Add(evt2);
            p.Add(evt3);
            p.Add(evt);
            p.Add(evt2);
            p.Add(evt3);
            p.Add(evt);
            p.Add(evt2);
            p.Add(evt3);
            Assert.AreEqual(192, p.TotalEntropyBytes);
            Assert.AreEqual(192, p.EntropyBytesSinceLastDigest);
        }
        [TestMethod]
        public void Pool_OneSourceIsLimitedWhenDominating()
        {
            var p = new EntropyPool();
            var evt = EventFromBytes(_Zero16Bytes);
            var evt2 = EventFromBytes(_Zero16Bytes);
            p.Add(evt);
            p.Add(evt);
            p.Add(evt);
            p.Add(evt);     // Ignored.
            Assert.AreEqual(48, p.TotalEntropyBytes);
            Assert.AreEqual(48, p.EntropyBytesSinceLastDigest);

            p.Add(evt2);
            p.Add(evt2);
            p.Add(evt2);
            Assert.AreEqual(96, p.TotalEntropyBytes);
            Assert.AreEqual(96, p.EntropyBytesSinceLastDigest);

            p.Add(evt);
            p.Add(evt);
            p.Add(evt);
            p.Add(evt);     // Ignored.
            Assert.AreEqual(144, p.TotalEntropyBytes);
            Assert.AreEqual(144, p.EntropyBytesSinceLastDigest);
        }
        [TestMethod]
        public void Pool_GetDigestResetsCountsBySource()
        {
            var p = new EntropyPool();
            var evt = EventFromBytes(_Zero16Bytes);
            p.Add(evt);
            Assert.HasCount(1, p.GetCountOfBytesBySource());
            var digest = p.GetDigest();
            Assert.HasCount(0, p.GetCountOfBytesBySource());
        }


        [TestMethod]
        public void Accumulator_DefaultCreator()
        {
            var a = new EntropyAccumulator();
            Assert.AreEqual(28, a.LinearPoolCount);
            Assert.AreEqual(12, a.RandomPoolCount);
            Assert.AreEqual(40, a.TotalPoolCount);
            Assert.AreEqual(0, a.TotalEntropyBytes);
            Assert.AreEqual(0, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(0, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_WithRandomGenerator()
        {
            var a = new EntropyAccumulator(_Rng);
            Assert.AreEqual(28, a.LinearPoolCount);
            Assert.AreEqual(12, a.RandomPoolCount);
            Assert.AreEqual(40, a.TotalPoolCount);
            Assert.AreEqual(0, a.TotalEntropyBytes);
            Assert.AreEqual(0, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(0, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_WithMinimumPools()
        {
            var a = new EntropyAccumulator(2, 2, _Rng);
            Assert.AreEqual(2, a.LinearPoolCount);
            Assert.AreEqual(2, a.RandomPoolCount);
            Assert.AreEqual(4, a.TotalPoolCount);
            Assert.AreEqual(0, a.TotalEntropyBytes);
            Assert.AreEqual(0, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(0, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_WithMaximumPools()
        {
            var a = new EntropyAccumulator(64, 64, _Rng);
            Assert.AreEqual(64, a.LinearPoolCount);
            Assert.AreEqual(64, a.RandomPoolCount);
            Assert.AreEqual(128, a.TotalPoolCount);
            Assert.AreEqual(0, a.TotalEntropyBytes);
            Assert.AreEqual(0, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(0, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_WithMinimumRandomPools()
        {
            var a = new EntropyAccumulator(0, 4, _Rng);
            Assert.AreEqual(0, a.LinearPoolCount);
            Assert.AreEqual(4, a.RandomPoolCount);
            Assert.AreEqual(4, a.TotalPoolCount);
            Assert.AreEqual(0, a.TotalEntropyBytes);
            Assert.AreEqual(0, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(0, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_WithMinimumLinearPools()
        {
            var a = new EntropyAccumulator(4, 0, _Rng);
            Assert.AreEqual(4, a.LinearPoolCount);
            Assert.AreEqual(0, a.RandomPoolCount);
            Assert.AreEqual(4, a.TotalPoolCount);
            Assert.AreEqual(0, a.TotalEntropyBytes);
            Assert.AreEqual(0, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(0, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_WithMaximumRandomPools()
        {
            var a = new EntropyAccumulator(0, 64, _Rng);
            Assert.AreEqual(0, a.LinearPoolCount);
            Assert.AreEqual(64, a.RandomPoolCount);
            Assert.AreEqual(64, a.TotalPoolCount);
            Assert.AreEqual(0, a.TotalEntropyBytes);
            Assert.AreEqual(0, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(0, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_WithMaximumLinearPools()
        {
            var a = new EntropyAccumulator(64, 0, _Rng);
            Assert.AreEqual(64, a.LinearPoolCount);
            Assert.AreEqual(0, a.RandomPoolCount);
            Assert.AreEqual(64, a.TotalPoolCount);
            Assert.AreEqual(0, a.TotalEntropyBytes);
            Assert.AreEqual(0, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(0, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_WithCustomHashFunction()
        {
            var a = new EntropyAccumulator(16, 16, _Rng, () => SHA256.Create());
            Assert.AreEqual(16, a.LinearPoolCount);
            Assert.AreEqual(16, a.RandomPoolCount);
            Assert.AreEqual(32, a.TotalPoolCount);
            Assert.AreEqual(0, a.TotalEntropyBytes);
            Assert.AreEqual(0, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(0, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.PoolZeroEntropyBytesSinceLastSeed);
        }

        [TestMethod]
        public void Accumulator_Failure_TooManyLinearPools()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EntropyAccumulator(65, 0, _Rng));
        }
        [TestMethod]
        public void Accumulator_Failure_TooManyRandomPools()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EntropyAccumulator(0, 65, _Rng));
        }
        [TestMethod]
        public void Accumulator_Failure_TooFewPools()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EntropyAccumulator(1, 1, _Rng));
        }


        [TestMethod]
        public void Accumulator_AddOnceIncreasesPoolEntropy()
        {
            var a = new EntropyAccumulator(_Rng);
            a.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual(8, a.TotalEntropyBytes);
            Assert.AreEqual(8, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(8, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(8, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_AddTwiceIncreasesPoolEntropy()
        {
            var a = new EntropyAccumulator(_Rng);
            a.Add(EventFromBytes(_Zero8Bytes));
            a.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual(16, a.TotalEntropyBytes);
            Assert.AreEqual(16, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(8, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(8, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_AddPoolCountAddsToAllPools()
        {
            var a = new EntropyAccumulator(_Rng);
            for (int i = 0; i < a.TotalPoolCount; i++)
                a.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual(_Zero8Bytes.Length * a.TotalPoolCount, a.TotalEntropyBytes);
            Assert.AreEqual(_Zero8Bytes.Length * a.TotalPoolCount, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(8, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(8, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(8, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_AddPoolCountPlusOneAddsToAllPoolsAndFirstPool()
        {
            var a = new EntropyAccumulator(_Rng);
            for (int i = 0; i < a.TotalPoolCount; i++)
                a.Add(EventFromBytes(_Zero8Bytes));
            a.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual((_Zero8Bytes.Length * a.TotalPoolCount) + _Zero8Bytes.Length, a.TotalEntropyBytes);
            Assert.AreEqual((_Zero8Bytes.Length * a.TotalPoolCount) + _Zero8Bytes.Length, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(16, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(8, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(16, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_AddPoolCountTwiceAddsToAllPools()
        {
            var a = new EntropyAccumulator(_Rng);
            for (int i = 0; i < a.TotalPoolCount * 2; i++)
                a.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual(_Zero8Bytes.Length * a.TotalPoolCount * 2, a.TotalEntropyBytes);
            Assert.AreEqual(_Zero8Bytes.Length * a.TotalPoolCount * 2, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(16, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(16, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(16, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_NextSeedWithZeroEntropy()
        {
            var a = new EntropyAccumulator(_Rng);
            Assert.IsNotEmpty(a.NextSeed());
            Assert.AreEqual(0, a.TotalEntropyBytes);
            Assert.AreEqual(0, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(1, a.TotalReseedEvents);
            Assert.AreEqual(0, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_NextSeedResetsCountersSinceLastSeed()
        {
            var a = new EntropyAccumulator(_Rng);
            a.Add(EventFromBytes(_Zero8Bytes));
            Assert.IsNotEmpty(a.NextSeed());
            Assert.AreEqual(8, a.TotalEntropyBytes);
            Assert.AreEqual(0, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(1, a.TotalReseedEvents);
            Assert.AreEqual(0, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_NextSeedResetsCountersSinceLastSeedTwice()
        {
            var a = new EntropyAccumulator(_Rng);
            a.Add(EventFromBytes(_Zero8Bytes));
            Assert.IsNotEmpty(a.NextSeed());
            Assert.AreEqual(8, a.TotalEntropyBytes);
            Assert.AreEqual(0, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(1, a.TotalReseedEvents);
            Assert.AreEqual(0, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.PoolZeroEntropyBytesSinceLastSeed);

            a.Add(EventFromBytes(_Zero8Bytes));
            Assert.IsNotEmpty(a.NextSeed());
            Assert.AreEqual(16, a.TotalEntropyBytes);
            Assert.AreEqual(0, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(2, a.TotalReseedEvents);
            Assert.AreEqual(0, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.PoolZeroEntropyBytesSinceLastSeed);
        }

        [TestMethod]
        public void Accumulator_2kBEntropyPacket()
        {
            // 2kB from one source is enough to trip the "too much entropy from one source" thing on pools.
            // 1kB extra is almost enough to balance it out.
            var a = new EntropyAccumulator(32, 0, _Rng);
            a.Add(EventFromBytes(_Zero1KBytes));
            a.Add(EventFromBytes(_Zero2KBytes));
            Assert.AreEqual(3072, a.TotalEntropyBytes);
            Assert.AreEqual(3072, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(96, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(96, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(96, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_1kBEntropyPacket()
        {
            var a = new EntropyAccumulator(32, 0, _Rng);
            a.Add(EventFromBytes(_Zero1KBytes));
            Assert.AreEqual(_Zero1KBytes.Length, a.TotalEntropyBytes);
            Assert.AreEqual(_Zero1KBytes.Length, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(32, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(32, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(32, a.PoolZeroEntropyBytesSinceLastSeed);
        }

        [TestMethod]
        public void Accumulator_24ByteEntropyPacketUneven()
        {
            var a = new EntropyAccumulator(32, 0, _Rng);
            a.Add(EventFromBytes(new byte[24]));
            Assert.AreEqual(24, a.TotalEntropyBytes);
            Assert.AreEqual(24, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(16, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(16, a.PoolZeroEntropyBytesSinceLastSeed);
        }
        [TestMethod]
        public void Accumulator_48ByteEntropyPacketUneven()
        {
            var a = new EntropyAccumulator(32, 0, _Rng);
            a.Add(EventFromBytes(new byte[48]));
            Assert.AreEqual(48, a.TotalEntropyBytes);
            Assert.AreEqual(48, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
            Assert.AreEqual(16, a.MaxPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.MinPoolEntropyBytesSinceLastSeed);
            Assert.AreEqual(16, a.PoolZeroEntropyBytesSinceLastSeed);
        }


        [TestMethod]
        public void Accumulator_RandomEntropyPacketSizes()
        {
            var a = new EntropyAccumulator(32, 0, _Rng);
            int totalEntropy = 0;
            for (int i = 0; i < 1000; i++)
            {
                var bytes = new byte[_Rng.GetRandomInt32(128)+1];
                a.Add(EventFromBytes(bytes));
                totalEntropy = totalEntropy + bytes.Length;
            }
            Assert.AreEqual(totalEntropy, a.TotalEntropyBytes);
            Assert.AreEqual(totalEntropy, a.AvailableEntropyBytesSinceLastSeed);
            Assert.AreEqual(0, a.TotalReseedEvents);
        }

        private static IRandomNumberGenerator CreateRandomGenerator() => new StandardRandomWrapperGenerator(new Rand(1));
        private static EntropyEvent EventFromBytes(byte[] bytes)
        {
            return new EntropyEvent(bytes, new NullSource());
        }
    }
}
