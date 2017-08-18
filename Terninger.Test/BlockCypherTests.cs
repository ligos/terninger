using System;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Generator;
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
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            // Creating a generator should not actually generate any bytes.
            Assert.AreEqual(crng.BytesGenerated, 0L);
            Assert.AreEqual(crng.BytesRequested, 0L);
        }
        [TestMethod]
        public void ConstructAesManagedCrng()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256Managed(), SHA256.Create(), new CypherCounter(16));
            // Creating a generator should not actually generate any bytes.
            Assert.AreEqual(crng.BytesGenerated, 0L);
            Assert.AreEqual(crng.BytesRequested, 0L);
        }
        [TestMethod]
        public void ConstructAesCspCrng()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256Native(), SHA256.Create(), new CypherCounter(16));
            // Creating a generator should not actually generate any bytes.
            Assert.AreEqual(crng.BytesGenerated, 0L);
            Assert.AreEqual(crng.BytesRequested, 0L);
        }

        [TestMethod]
        public void GenerateSingleBlock()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            var buffer = new byte[crng.BlockSizeBytes];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + 32);
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }

        [TestMethod]
        public void GenerateTwoBlocksInOneRequest()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + 32);
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }

        [TestMethod]
        public void GenerateTwoBlocksInTwoRequests()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
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
            var crng1 = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            var crng2 = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            var buffer1 = new byte[crng1.BlockSizeBytes];
            var buffer2 = new byte[crng2.BlockSizeBytes];
            crng1.FillWithRandomBytes(buffer1);
            crng2.FillWithRandomBytes(buffer2);

            Assert.IsTrue(buffer1.SequenceEqual(buffer2));
        }

        [TestMethod]
        public void GeneratorsWithDifferentKeyYieldDifferentResult()
        {
            var crng1 = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            var crng2 = new BlockCypherCprngGenerator(_IncrementedKey32Bytes);
            var buffer1 = new byte[crng1.BlockSizeBytes];
            var buffer2 = new byte[crng2.BlockSizeBytes];
            crng1.FillWithRandomBytes(buffer1);
            crng2.FillWithRandomBytes(buffer2);

            Assert.IsFalse(buffer1.SequenceEqual(buffer2));
        }

        [TestMethod]
        public void GenerateOneByte()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            var result = crng.GetRandomBytes(1);

            Assert.AreEqual(crng.BytesGenerated, crng.BlockSizeBytes + 32);
            Assert.AreEqual(crng.BytesRequested, result.Length);
        }
        [TestMethod]
        public void GenerateFourBytes()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            var result = crng.GetRandomBytes(4);

            Assert.AreEqual(crng.BytesGenerated, crng.BlockSizeBytes + 32);
            Assert.AreEqual(crng.BytesRequested, result.Length);
        }

        [TestMethod]
        public void GenerateMaximumSingleRequestBytes()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            var result = crng.GetRandomBytes(crng.MaxRequestBytes);

            Assert.AreEqual(crng.BytesGenerated, crng.MaxRequestBytes + 32);
            Assert.AreEqual(crng.BytesRequested, result.Length);
        }


        [TestMethod]
        public void GenerateTwiceMaximumSingleRequestBytes()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            var result = crng.GetRandomBytes(crng.MaxRequestBytes * 2);

            Assert.AreEqual(crng.BytesGenerated, result.Length + 64);
            Assert.AreEqual(crng.BytesRequested, result.Length);
        }
    }
}
