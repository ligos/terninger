using System;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.CryptoPrimitives;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class BlockCypherTests
    {
        private static readonly byte[] _ZeroKey16Bytes = new byte[16];
        private static readonly byte[] _ZeroKey32Bytes = new byte[32];
        private static readonly byte[] _IncrementedKey32Bytes = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();

        [TestMethod]
        public void ConstructDefaultCrng()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey32Bytes);
            // Creating a generator should not actually generate any bytes.
            Assert.AreEqual(crng.BytesGenerated, 0L);
            Assert.AreEqual(crng.BytesRequested, 0L);
        }
        [TestMethod]
        public void ConstructAesManagedCrng()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256Managed(), SHA256.Create(), new CypherCounter(16));
            // Creating a generator should not actually generate any bytes.
            Assert.AreEqual(crng.BytesGenerated, 0L);
            Assert.AreEqual(crng.BytesRequested, 0L);
        }
        [TestMethod]
        public void ConstructAesCspCrng()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey32Bytes, NativeCryptoPrimitives.GetAes256Csp(), SHA256.Create(), new CypherCounter(16));
            // Creating a generator should not actually generate any bytes.
            Assert.AreEqual(crng.BytesGenerated, 0L);
            Assert.AreEqual(crng.BytesRequested, 0L);
        }

        [TestMethod]
        public void GenerateSingleBlock()
        {
            var crng = CypherBasedPrngGenerator.Create(_ZeroKey32Bytes, outputBufferSize: 0);
            var buffer = new byte[crng.BlockSizeBytes];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + 32);
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }

        [TestMethod]
        public void GenerateTwoBlocksInOneRequest()
        {
            var crng = CypherBasedPrngGenerator.Create(_ZeroKey32Bytes, outputBufferSize: 0);
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + 32);
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }

        [TestMethod]
        public void GenerateTwoBlocksInTwoRequests()
        {
            var crng = CypherBasedPrngGenerator.Create(_ZeroKey32Bytes, outputBufferSize: 0);
            var buffer1 = new byte[crng.BlockSizeBytes * 2];
            var buffer2 = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer1);
            crng.FillWithRandomBytes(buffer2);

            Assert.IsFalse(buffer1.All(b => b == 0));
            Assert.IsFalse(buffer2.All(b => b == 0));
            Assert.IsFalse(buffer1.SequenceEqual(buffer2));
            Assert.AreEqual(crng.BytesGenerated, buffer1.Length + buffer2.Length + 64);
            Assert.AreEqual(crng.BytesRequested, buffer1.Length + buffer2.Length);
        }

        [TestMethod]
        public void GeneratorsWithSameKeyYieldSameResult()
        {
            var crng1 = new CypherBasedPrngGenerator(_ZeroKey32Bytes);
            var crng2 = new CypherBasedPrngGenerator(_ZeroKey32Bytes);
            var buffer1 = new byte[crng1.BlockSizeBytes];
            var buffer2 = new byte[crng2.BlockSizeBytes];
            crng1.FillWithRandomBytes(buffer1);
            crng2.FillWithRandomBytes(buffer2);

            Assert.IsTrue(buffer1.SequenceEqual(buffer2));
        }

        [TestMethod]
        public void GeneratorsWithDifferentKeyYieldDifferentResult()
        {
            var crng1 = new CypherBasedPrngGenerator(_ZeroKey32Bytes);
            var crng2 = new CypherBasedPrngGenerator(_IncrementedKey32Bytes);
            var buffer1 = new byte[crng1.BlockSizeBytes];
            var buffer2 = new byte[crng2.BlockSizeBytes];
            crng1.FillWithRandomBytes(buffer1);
            crng2.FillWithRandomBytes(buffer2);

            Assert.IsFalse(buffer1.SequenceEqual(buffer2));
        }

        [TestMethod]
        public void GenerateOneByte()
        {
            var crng = CypherBasedPrngGenerator.Create(_ZeroKey32Bytes, outputBufferSize: 0);
            var result = crng.GetRandomBytes(1);

            Assert.AreEqual(crng.BytesGenerated, crng.BlockSizeBytes + 32);
            Assert.AreEqual(crng.BytesRequested, result.Length);
        }
        [TestMethod]
        public void GenerateFourBytes()
        {
            var crng = CypherBasedPrngGenerator.Create(_ZeroKey32Bytes, outputBufferSize: 0);
            var result = crng.GetRandomBytes(4);

            Assert.AreEqual(crng.BytesGenerated, crng.BlockSizeBytes + 32);
            Assert.AreEqual(crng.BytesRequested, result.Length);
        }

        [TestMethod]
        public void GenerateMaximumSingleRequestBytes()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey32Bytes);
            var result = crng.GetRandomBytes(crng.MaxRequestBytes);

            Assert.AreEqual(crng.BytesGenerated, crng.MaxRequestBytes + 32);
            Assert.AreEqual(crng.BytesRequested, result.Length);
        }


        [TestMethod]
        public void GenerateTwiceMaximumSingleRequestBytes()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey32Bytes);
            var result = crng.GetRandomBytes(crng.MaxRequestBytes * 2);

            Assert.AreEqual(crng.BytesGenerated, result.Length + 64);
            Assert.AreEqual(crng.BytesRequested, result.Length);
        }

        [TestMethod]
        public void Reseed()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey32Bytes);
            var preReseedResult = crng.GetRandomBytes(4);

            Assert.IsFalse(preReseedResult.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, 1024);
            Assert.AreEqual(crng.BytesRequested, preReseedResult.Length);

            crng.Reseed(_IncrementedKey32Bytes);
            Assert.AreEqual(crng.BytesGenerated, 2048);
            Assert.AreEqual(crng.BytesRequested, preReseedResult.Length);

            var postReseedResult = crng.GetRandomBytes(4);
            Assert.IsFalse(postReseedResult.All(b => b == 0));
            Assert.IsFalse(preReseedResult.SequenceEqual(postReseedResult));
            Assert.AreEqual(crng.BytesGenerated, 2048);
            Assert.AreEqual(crng.BytesRequested, preReseedResult.Length + postReseedResult.Length);
        }

        [TestMethod]
        public void ReseedTakesEffectImmediately()
        {
            // As the PRNG uses buffering internally, there may be random bytes left over after a reseed.
            // Those left over bytes must be discarded.
            var crng1 = new CypherBasedPrngGenerator(_ZeroKey32Bytes);
            var crng2 = new CypherBasedPrngGenerator(_ZeroKey32Bytes);
            var preReseedResult1 = crng1.GetRandomBytes(64);
            var preReseedResult2 = crng2.GetRandomBytes(64);
            Assert.IsTrue(preReseedResult1.SequenceEqual(preReseedResult2));

            crng1.Reseed(_IncrementedKey32Bytes);
            var postReseedResult1 = crng1.GetRandomBytes(64);
            var postReseedResult2 = crng2.GetRandomBytes(64);
            Assert.IsFalse(postReseedResult1.SequenceEqual(postReseedResult2));
        }
    }
}
