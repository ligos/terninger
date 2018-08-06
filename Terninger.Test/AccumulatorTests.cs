using System;
using System.Security.Cryptography;
using System.Linq;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Test;
using MurrayGrant.Terninger.Accumulator;

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
            Assert.AreEqual(p.TotalEntropyBytes, 0);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 0);
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
            Assert.AreEqual(p.TotalEntropyBytes, 8);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 8);
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
            Assert.AreEqual(p.TotalEntropyBytes, 16);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 16);
        }

        [TestMethod]
        public void Pool_CountersResetAfterDigest()
        {
            var p = new EntropyPool();
            p.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual(p.TotalEntropyBytes, 8);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 8);
            p.GetDigest();
            Assert.AreEqual(p.TotalEntropyBytes, 8);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 0);

            p.Add(EventFromBytes(_Zero16Bytes));
            Assert.AreEqual(p.TotalEntropyBytes, 24);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 16);
            p.GetDigest();
            Assert.AreEqual(p.TotalEntropyBytes, 24);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 0);
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
            var p = new EntropyPool(new SHA256Managed());
            p.Add(EventFromBytes(_Zero8Bytes));
            Assert.IsFalse(p.GetDigest().All(b => b == 0));
        }

        //[TestMethod]
        public void Pool_CanUseNonShaBasedAlgorithm()
        {
            var blake2b = System.Data.HashFunction.Blake2.Blake2BFactory.Instance.Create();
          
            Assert.Fail("TODO: must change the EntropyPool to accept an object which we can plug various hash implementations into.");
            var p = new EntropyPool(new SHA256Managed());
            p.Add(EventFromBytes(_Zero8Bytes));
            Assert.IsFalse(p.GetDigest().All(b => b == 0));
        }

#if NETCOREAPP2_1
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
            Assert.AreEqual(p.TotalEntropyBytes, 48);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 48);
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
            Assert.AreEqual(p.TotalEntropyBytes, 48);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 48);
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
            Assert.AreEqual(p.TotalEntropyBytes, 128);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 128);
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
            Assert.AreEqual(p.TotalEntropyBytes, 192);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 192);
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
            Assert.AreEqual(p.TotalEntropyBytes, 48);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 48);

            p.Add(evt2);
            p.Add(evt2);
            p.Add(evt2);
            Assert.AreEqual(p.TotalEntropyBytes, 96);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 96);

            p.Add(evt);
            p.Add(evt);
            p.Add(evt);
            p.Add(evt);     // Ignored.
            Assert.AreEqual(p.TotalEntropyBytes, 144);
            Assert.AreEqual(p.EntropyBytesSinceLastDigest, 144);
        }
        [TestMethod]
        public void Pool_GetDigestResetsCountsBySource()
        {
            var p = new EntropyPool();
            var evt = EventFromBytes(_Zero16Bytes);
            p.Add(evt);
            Assert.AreEqual(p.GetCountOfBytesBySource().Count, 1);
            var digest = p.GetDigest();
            Assert.AreEqual(p.GetCountOfBytesBySource().Count, 0);
        }


        [TestMethod]
        public void Accumulator_DefaultCreator()
        {
            var a = new EntropyAccumulator();
            Assert.AreEqual(a.LinearPoolCount, 16);
            Assert.AreEqual(a.RandomPoolCount, 16);
            Assert.AreEqual(a.TotalPoolCount, 32);
            Assert.AreEqual(a.TotalEntropyBytes, 0);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 0);
        }
        [TestMethod]
        public void Accumulator_WithRandomGenerator()
        {
            var a = new EntropyAccumulator(_Rng);
            Assert.AreEqual(a.LinearPoolCount, 16);
            Assert.AreEqual(a.RandomPoolCount, 16);
            Assert.AreEqual(a.TotalPoolCount, 32);
            Assert.AreEqual(a.TotalEntropyBytes, 0);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 0);
        }
        [TestMethod]
        public void Accumulator_WithMinimumPools()
        {
            var a = new EntropyAccumulator(2, 2, _Rng);
            Assert.AreEqual(a.LinearPoolCount, 2);
            Assert.AreEqual(a.RandomPoolCount, 2);
            Assert.AreEqual(a.TotalPoolCount, 4);
            Assert.AreEqual(a.TotalEntropyBytes, 0);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 0);
        }
        [TestMethod]
        public void Accumulator_WithMaximumPools()
        {
            var a = new EntropyAccumulator(64, 64, _Rng);
            Assert.AreEqual(a.LinearPoolCount, 64);
            Assert.AreEqual(a.RandomPoolCount, 64);
            Assert.AreEqual(a.TotalPoolCount, 128);
            Assert.AreEqual(a.TotalEntropyBytes, 0);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 0);
        }
        [TestMethod]
        public void Accumulator_WithMinimumRandomPools()
        {
            var a = new EntropyAccumulator(0, 4, _Rng);
            Assert.AreEqual(a.LinearPoolCount, 0);
            Assert.AreEqual(a.RandomPoolCount, 4);
            Assert.AreEqual(a.TotalPoolCount, 4);
            Assert.AreEqual(a.TotalEntropyBytes, 0);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 0);
        }
        [TestMethod]
        public void Accumulator_WithMinimumLinearPools()
        {
            var a = new EntropyAccumulator(4, 0, _Rng);
            Assert.AreEqual(a.LinearPoolCount, 4);
            Assert.AreEqual(a.RandomPoolCount, 0);
            Assert.AreEqual(a.TotalPoolCount, 4);
            Assert.AreEqual(a.TotalEntropyBytes, 0);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 0);
        }
        [TestMethod]
        public void Accumulator_WithMaximumRandomPools()
        {
            var a = new EntropyAccumulator(0, 64, _Rng);
            Assert.AreEqual(a.LinearPoolCount, 0);
            Assert.AreEqual(a.RandomPoolCount, 64);
            Assert.AreEqual(a.TotalPoolCount, 64);
            Assert.AreEqual(a.TotalEntropyBytes, 0);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 0);
        }
        [TestMethod]
        public void Accumulator_WithMaximumLinearPools()
        {
            var a = new EntropyAccumulator(64, 0, _Rng);
            Assert.AreEqual(a.LinearPoolCount, 64);
            Assert.AreEqual(a.RandomPoolCount, 0);
            Assert.AreEqual(a.TotalPoolCount, 64);
            Assert.AreEqual(a.TotalEntropyBytes, 0);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 0);
        }
        [TestMethod]
        public void Accumulator_WithCustomHashFunction()
        {
            var a = new EntropyAccumulator(16, 16, _Rng, () => new SHA256Managed());
            Assert.AreEqual(a.LinearPoolCount, 16);
            Assert.AreEqual(a.RandomPoolCount, 16);
            Assert.AreEqual(a.TotalPoolCount, 32);
            Assert.AreEqual(a.TotalEntropyBytes, 0);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 0);
        }

        [TestMethod]
        public void Accumulator_Failure_TooManyLinearPools()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new EntropyAccumulator(65, 0, _Rng));
        }
        [TestMethod]
        public void Accumulator_Failure_TooManyRandomPools()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new EntropyAccumulator(0, 65, _Rng));
        }
        [TestMethod]
        public void Accumulator_Failure_TooFewPools()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new EntropyAccumulator(1, 1, _Rng));
        }


        [TestMethod]
        public void Accumulator_AddOnceIncreasesPoolEntropy()
        {
            var a = new EntropyAccumulator(_Rng);
            a.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual(a.TotalEntropyBytes, 8);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 8);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 8);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 8);
        }
        [TestMethod]
        public void Accumulator_AddTwiceIncreasesPoolEntropy()
        {
            var a = new EntropyAccumulator(_Rng);
            a.Add(EventFromBytes(_Zero8Bytes));
            a.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual(a.TotalEntropyBytes, 16);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 16);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 8);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 8);
        }
        [TestMethod]
        public void Accumulator_AddPoolCountAddsToAllPools()
        {
            var a = new EntropyAccumulator(_Rng);
            for (int i = 0; i < a.TotalPoolCount; i++)
                a.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual(a.TotalEntropyBytes, _Zero8Bytes.Length * a.TotalPoolCount);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, _Zero8Bytes.Length * a.TotalPoolCount);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 8);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 8);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 8);
        }
        [TestMethod]
        public void Accumulator_AddPoolCountPlusOneAddsToAllPoolsAndFirstPool()
        {
            var a = new EntropyAccumulator(_Rng);
            for (int i = 0; i < a.TotalPoolCount; i++)
                a.Add(EventFromBytes(_Zero8Bytes));
            a.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual(a.TotalEntropyBytes, (_Zero8Bytes.Length * a.TotalPoolCount) + _Zero8Bytes.Length);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, (_Zero8Bytes.Length * a.TotalPoolCount) + _Zero8Bytes.Length);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 16);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 8);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 16);
        }
        [TestMethod]
        public void Accumulator_AddPoolCountTwiceAddsToAllPools()
        {
            var a = new EntropyAccumulator(_Rng);
            for (int i = 0; i < a.TotalPoolCount * 2; i++)
                a.Add(EventFromBytes(_Zero8Bytes));
            Assert.AreEqual(a.TotalEntropyBytes, _Zero8Bytes.Length * a.TotalPoolCount * 2);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, _Zero8Bytes.Length * a.TotalPoolCount * 2);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 16);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 16);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 16);
        }
        [TestMethod]
        public void Accumulator_NextSeedWithZeroEntropy()
        {
            var a = new EntropyAccumulator(_Rng);
            Assert.IsTrue(a.NextSeed().Length > 0);
            Assert.AreEqual(a.TotalEntropyBytes, 0);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.TotalReseedEvents, 1);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 0);
        }
        [TestMethod]
        public void Accumulator_NextSeedResetsCountersSinceLastSeed()
        {
            var a = new EntropyAccumulator(_Rng);
            a.Add(EventFromBytes(_Zero8Bytes));
            Assert.IsTrue(a.NextSeed().Length > 0);
            Assert.AreEqual(a.TotalEntropyBytes, 8);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.TotalReseedEvents, 1);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 0);
        }
        [TestMethod]
        public void Accumulator_NextSeedResetsCountersSinceLastSeedTwice()
        {
            var a = new EntropyAccumulator(_Rng);
            a.Add(EventFromBytes(_Zero8Bytes));
            Assert.IsTrue(a.NextSeed().Length > 0);
            Assert.AreEqual(a.TotalEntropyBytes, 8);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.TotalReseedEvents, 1);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 0);

            a.Add(EventFromBytes(_Zero8Bytes));
            Assert.IsTrue(a.NextSeed().Length > 0);
            Assert.AreEqual(a.TotalEntropyBytes, 16);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.TotalReseedEvents, 2);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 0);
        }

        [TestMethod]
        public void Accumulator_2kBEntropyPacket()
        {
            // 2kB from one source is enough to trip the "too much entropy from one source" thing on pools.
            // 1kB extra is almost enough to balance it out.
            var a = new EntropyAccumulator(32, 0, _Rng);
            a.Add(EventFromBytes(_Zero1KBytes));
            a.Add(EventFromBytes(_Zero2KBytes));
            Assert.AreEqual(a.TotalEntropyBytes, 3072);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 3072);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 96);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 96);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 96);
        }
        [TestMethod]
        public void Accumulator_1kBEntropyPacket()
        {
            var a = new EntropyAccumulator(32, 0, _Rng);
            a.Add(EventFromBytes(_Zero1KBytes));
            Assert.AreEqual(a.TotalEntropyBytes, _Zero1KBytes.Length);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, _Zero1KBytes.Length);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 32);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 32);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 32);
        }

        [TestMethod]
        public void Accumulator_24ByteEntropyPacketUneven()
        {
            var a = new EntropyAccumulator(32, 0, _Rng);
            a.Add(EventFromBytes(new byte[24]));
            Assert.AreEqual(a.TotalEntropyBytes, 24);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 24);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 16);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 16);
        }
        [TestMethod]
        public void Accumulator_48ByteEntropyPacketUneven()
        {
            var a = new EntropyAccumulator(32, 0, _Rng);
            a.Add(EventFromBytes(new byte[48]));
            Assert.AreEqual(a.TotalEntropyBytes, 48);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, 48);
            Assert.AreEqual(a.TotalReseedEvents, 0);
            Assert.AreEqual(a.MaxPoolEntropyBytesSinceLastSeed, 16);
            Assert.AreEqual(a.MinPoolEntropyBytesSinceLastSeed, 0);
            Assert.AreEqual(a.PoolZeroEntropyBytesSinceLastSeed, 16);
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
            Assert.AreEqual(a.TotalEntropyBytes, totalEntropy);
            Assert.AreEqual(a.AvailableEntropyBytesSinceLastSeed, totalEntropy);
            Assert.AreEqual(a.TotalReseedEvents, 0);
        }

        private static IRandomNumberGenerator CreateRandomGenerator() => new StandardRandomWrapperGenerator(new Random(1));
        private static EntropyEvent EventFromBytes(byte[] bytes)
        {
            return new EntropyEvent(bytes, new NullSource());
        }
    }
}
