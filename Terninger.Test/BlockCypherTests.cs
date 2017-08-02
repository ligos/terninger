using System;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Generator;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class BlockCypherTests
    {
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
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged());
            // Creating a generator should not actually generate any bytes.
            Assert.AreEqual(crng.BytesGenerated, 0L);
            Assert.AreEqual(crng.BytesRequested, 0L);
        }
        [TestMethod]
        public void ConstructAesCspCrng()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesCryptoServiceProvider());
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
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + (crng.BlockSizeBytes * 2));
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }
        [TestMethod]
        public void GenerateSingleBlockAesManaged()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged());
            var buffer = new byte[crng.BlockSizeBytes];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + (crng.BlockSizeBytes * 2));
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }
        [TestMethod]
        public void GenerateSingleBlockAesCsp()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesCryptoServiceProvider());
            var buffer = new byte[crng.BlockSizeBytes];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + (crng.BlockSizeBytes * 2));
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }

        [TestMethod]
        public void GenerateTwoBlocksInOneRequest()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + (crng.BlockSizeBytes * 2));
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }
        [TestMethod]
        public void AesCyphersProduceSameRandomBlocks()
        {
            var crngManaged = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged());
            var crngCsp = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesCryptoServiceProvider());
            var bufferManaged = new byte[crngManaged.BlockSizeBytes * 2];
            var bufferCsp = new byte[crngManaged.BlockSizeBytes * 2];
            crngManaged.FillWithRandomBytes(bufferManaged);
            crngCsp.FillWithRandomBytes(bufferCsp);

            CollectionAssert.AreEqual(bufferManaged, bufferCsp);
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
            Assert.AreEqual(crng.BytesGenerated, buffer1.Length + buffer2.Length + (crng.BlockSizeBytes * 4));
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

            Assert.AreEqual(crng.BytesGenerated, crng.BlockSizeBytes + (crng.BlockSizeBytes * 2));
            Assert.AreEqual(crng.BytesRequested, result.Length);
        }
        [TestMethod]
        public void GenerateFourBytes()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            var result = crng.GetRandomBytes(4);

            Assert.AreEqual(crng.BytesGenerated, crng.BlockSizeBytes + (crng.BlockSizeBytes * 2));
            Assert.AreEqual(crng.BytesRequested, result.Length);
        }

        [TestMethod]
        public void GenerateMaximumSingleRequestBytes()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            var result = crng.GetRandomBytes(crng.MaxRequestBytes);

            Assert.AreEqual(crng.BytesGenerated, crng.MaxRequestBytes + (crng.BlockSizeBytes * 2));
            Assert.AreEqual(crng.BytesRequested, result.Length);
        }


        [TestMethod]
        public void GenerateTwiceMaximumSingleRequestBytes()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes);
            var result = crng.GetRandomBytes(crng.MaxRequestBytes * 2);

            Assert.AreEqual(crng.BytesGenerated, result.Length + (crng.BlockSizeBytes * 4));
            Assert.AreEqual(crng.BytesRequested, result.Length);
        }
    }
}
