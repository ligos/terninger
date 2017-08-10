using System;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.EntropySources;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class BlockCypherVariationTests
    {
        private static readonly byte[] _ZeroKey16Bytes = new byte[16];
        private static readonly byte[] _ZeroKey32Bytes = new byte[32];

        [TestMethod]
        public void GenerateSingleBlockAesManaged()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged(), SHA256.Create(), new CypherCounter(16));
            var buffer = new byte[crng.BlockSizeBytes];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + (crng.BlockSizeBytes * 2));
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }
        [TestMethod]
        public void GenerateSingleBlockAesCsp()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesCryptoServiceProvider(), SHA256.Create(), new CypherCounter(16));
            var buffer = new byte[crng.BlockSizeBytes];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + (crng.BlockSizeBytes * 2));
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }


        [TestMethod]
        public void AesCyphersProduceSameRandomBlocks()
        {
            var crngManaged = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged(), SHA256.Create(), new CypherCounter(16));
            var crngCsp = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesCryptoServiceProvider(), SHA256.Create(), new CypherCounter(16));
            var bufferManaged = new byte[crngManaged.BlockSizeBytes * 2];
            var bufferCsp = new byte[crngManaged.BlockSizeBytes * 2];
            crngManaged.FillWithRandomBytes(bufferManaged);
            crngCsp.FillWithRandomBytes(bufferCsp);

            CollectionAssert.AreEqual(bufferManaged, bufferCsp);
        }

        [TestMethod]
        public void Aes128And256CyphersProduceDifferentRandomBlocks()
        {
            var crngAes128 = new BlockCypherCprngGenerator(_ZeroKey16Bytes, new AesManaged() { KeySize = 128 }, SHA256.Create(), new CypherCounter(16));
            var crngAes256 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged() { KeySize = 256 }, SHA256.Create(), new CypherCounter(16));
            var buffer128 = new byte[crngAes128.BlockSizeBytes * 4];
            var buffer256 = new byte[crngAes256.BlockSizeBytes * 2];
            crngAes128.FillWithRandomBytes(buffer128);
            crngAes256.FillWithRandomBytes(buffer256);

            CollectionAssert.AreNotEqual(buffer128, buffer256);
        }

        [TestMethod]
        public void Sha256And512AlgorithmsProduceDifferentRandomBlocks()
        {
            var crngSha256 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged() { KeySize = 256 }, SHA256.Create(), new CypherCounter(16));
            var crngSha512 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged() { KeySize = 256 }, SHA512.Create(), new CypherCounter(16));
            var buffer256 = new byte[crngSha256.BlockSizeBytes * 2];
            var buffer512 = new byte[crngSha512.BlockSizeBytes * 2];
            crngSha256.FillWithRandomBytes(buffer256);
            crngSha512.FillWithRandomBytes(buffer512);

            CollectionAssert.AreNotEqual(buffer256, buffer512);
        }


        [TestMethod]
        public void SameCypherWithDifferentCountersProduceDifferentRandomBlocks()
        {
            var crng1 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, Aes.Create(), SHA256.Create(), new CypherCounter(16, 1));
            var crng2 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, Aes.Create(), SHA256.Create(), new CypherCounter(16, 2));
            var buffer1 = new byte[crng1.BlockSizeBytes * 2];
            var buffer2 = new byte[crng2.BlockSizeBytes * 2];
            crng1.FillWithRandomBytes(buffer1);
            crng2.FillWithRandomBytes(buffer2);

            CollectionAssert.AreNotEqual(buffer1, buffer2);
        }

        [TestMethod]
        public void Sha384AgorithmsWorks()
        {
            var crngSha384 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged() { KeySize = 256 }, SHA384.Create(), new CypherCounter(16));
            var buffer384 = new byte[crngSha384.BlockSizeBytes * 2];
            crngSha384.FillWithRandomBytes(buffer384);

            Assert.IsFalse(buffer384.All(b => b == 0));
        }
        [TestMethod]
        public void Md5AndAes128Works()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey16Bytes, new AesManaged() { KeySize = 128 }, MD5.Create(), new CypherCounter(16));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Sha1AndAes128Works()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey16Bytes, new AesManaged() { KeySize = 128 }, SHA1.Create(), new CypherCounter(16));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }

        [TestMethod]
        public void Rijndael128Key128BlockWorks()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey16Bytes, new RijndaelManaged() { KeySize = 128, BlockSize = 128 }, SHA256.Create(), new CypherCounter(16));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Rijndael256Key128BlockWorks()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new RijndaelManaged() { KeySize = 256, BlockSize = 128 }, SHA256.Create(), new CypherCounter(16));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Rijndael128Key256BlockWorks()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey16Bytes, new RijndaelManaged() { KeySize = 128, BlockSize = 256 }, SHA256.Create(), new CypherCounter(32));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Rijndael256Key256BlockWorks()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new RijndaelManaged() { KeySize = 256, BlockSize = 256 }, SHA256.Create(), new CypherCounter(32));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }

        [TestMethod]
        public void SameCypherAndKeyButAdditionalEntropyProduceDifferentRandomBlocks16Bytes()
        {
            var crng1 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, Aes.Create(), SHA256.Create(), new CypherCounter(16), CheapEntropy.Get16);
            var crng2 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, Aes.Create(), SHA256.Create(), new CypherCounter(16), CheapEntropy.Get16);
            var buffer1 = new byte[crng1.BlockSizeBytes * 2];
            var buffer2 = new byte[crng2.BlockSizeBytes * 2];
            crng1.FillWithRandomBytes(buffer1);
            crng2.FillWithRandomBytes(buffer2);

            CollectionAssert.AreNotEqual(buffer1, buffer2);
        }
        [TestMethod]
        public void SameCypherAndKeyButAdditionalEntropyProduceDifferentRandomBlocks32Bytes()
        {
            var crng1 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, Aes.Create(), SHA256.Create(), new CypherCounter(16), CheapEntropy.Get32);
            var crng2 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, Aes.Create(), SHA256.Create(), new CypherCounter(16), CheapEntropy.Get32);
            var buffer1 = new byte[crng1.BlockSizeBytes * 2];
            var buffer2 = new byte[crng2.BlockSizeBytes * 2];
            crng1.FillWithRandomBytes(buffer1);
            crng2.FillWithRandomBytes(buffer2);

            CollectionAssert.AreNotEqual(buffer1, buffer2);
        }


        [TestMethod]
        public void MismatchedKeyMaterialAndCypherKeySizeThrows()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => 
                new BlockCypherCprngGenerator(_ZeroKey16Bytes, new AesManaged() { KeySize = 256 }, SHA256.Create(), new CypherCounter(16))
            );
        }

        [TestMethod]
        public void Non16Or32ByteCypherThrows()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new BlockCypherCprngGenerator(new byte[14], new TripleDESCryptoServiceProvider(), SHA256.Create(), new CypherCounter(16))
            );
        }

        [TestMethod]
        public void SmallerHashThanCypherKeySizeThrows()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new BlockCypherCprngGenerator(_ZeroKey32Bytes, Aes.Create(), MD5.Create(), new CypherCounter(16))
            );
        }
        [TestMethod]
        public void DifferentCounterAndCypherBlockSizeThrows()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new BlockCypherCprngGenerator(_ZeroKey32Bytes, Aes.Create(), SHA256.Create(), new CypherCounter(32))
            );
        }
    }
}
