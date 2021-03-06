﻿using System;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.CryptoPrimitives;
using MurrayGrant.Terninger.EntropySources;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class BlockCypherVariationTests
    {
        private static readonly byte[] _ZeroKey16Bytes = new byte[16];
        private static readonly byte[] _ZeroKey32Bytes = new byte[32];
        private static readonly byte[] _ZeroKey64Bytes = new byte[64];

        [TestMethod]
        public void GenerateSingleBlockAesManaged()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256Managed(), SHA256.Create(), new CypherCounter(16), 0);
            var buffer = new byte[crng.BlockSizeBytes];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + (crng.BlockSizeBytes * 2));
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }
        [TestMethod]
        public void GenerateSingleBlockAesCsp()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey32Bytes, NativeCryptoPrimitives.GetAes256Csp(), SHA256.Create(), new CypherCounter(16), 0);
            var buffer = new byte[crng.BlockSizeBytes];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + (crng.BlockSizeBytes * 2));
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }


        [TestMethod]
        public void AesCyphersProduceSameRandomBlocks()
        {
            var crngManaged = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256Managed(), SHA256.Create(), new CypherCounter(16));
            var crngCsp = new CypherBasedPrngGenerator(_ZeroKey32Bytes, NativeCryptoPrimitives.GetAes256Csp(), SHA256.Create(), new CypherCounter(16));
            var bufferManaged = new byte[crngManaged.BlockSizeBytes * 2];
            var bufferCsp = new byte[crngManaged.BlockSizeBytes * 2];
            crngManaged.FillWithRandomBytes(bufferManaged);
            crngCsp.FillWithRandomBytes(bufferCsp);

            CollectionAssert.AreEqual(bufferManaged, bufferCsp);
        }

        [TestMethod]
        public void Aes128And256CyphersProduceDifferentRandomBlocks()
        {
            var crngAes128 = new CypherBasedPrngGenerator(_ZeroKey16Bytes, CryptoPrimitive.Aes128Managed(), SHA256.Create(), new CypherCounter(16));
            var crngAes256 = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256Managed(), SHA256.Create(), new CypherCounter(16));
            var buffer128 = new byte[crngAes128.BlockSizeBytes * 4];
            var buffer256 = new byte[crngAes256.BlockSizeBytes * 2];
            crngAes128.FillWithRandomBytes(buffer128);
            crngAes256.FillWithRandomBytes(buffer256);

            CollectionAssert.AreNotEqual(buffer128, buffer256);
        }

        [TestMethod]
        public void Sha256And512AlgorithmsProduceDifferentRandomBlocks()
        {
            var crngSha256 = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256Managed(), SHA256.Create(), new CypherCounter(16));
            var crngSha512 = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256Managed(), SHA512.Create(), new CypherCounter(16));
            var buffer256 = new byte[crngSha256.BlockSizeBytes * 2];
            var buffer512 = new byte[crngSha512.BlockSizeBytes * 2];
            crngSha256.FillWithRandomBytes(buffer256);
            crngSha512.FillWithRandomBytes(buffer512);

            CollectionAssert.AreNotEqual(buffer256, buffer512);
        }


        [TestMethod]
        public void SameCypherWithDifferentCountersProduceDifferentRandomBlocks()
        {
            var crng1 = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256(), SHA256.Create(), new CypherCounter(16, 1));
            var crng2 = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256(), SHA256.Create(), new CypherCounter(16, 2));
            var buffer1 = new byte[crng1.BlockSizeBytes * 2];
            var buffer2 = new byte[crng2.BlockSizeBytes * 2];
            crng1.FillWithRandomBytes(buffer1);
            crng2.FillWithRandomBytes(buffer2);

            CollectionAssert.AreNotEqual(buffer1, buffer2);
        }

        [TestMethod]
        public void Sha384AgorithmWorks()
        {
            var crngSha384 = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256Managed(), SHA384.Create(), new CypherCounter(16));
            var buffer384 = new byte[crngSha384.BlockSizeBytes * 2];
            crngSha384.FillWithRandomBytes(buffer384);

            Assert.IsFalse(buffer384.All(b => b == 0));
        }
        [TestMethod]
        public void Md5AndAes128Works()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey16Bytes, CryptoPrimitive.Aes128Managed(), MD5.Create(), new CypherCounter(16));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Sha1AndAes128Works()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey16Bytes, CryptoPrimitive.Aes128Managed(), SHA1.Create(), new CypherCounter(16));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }

        [TestMethod]
        public void Rijndael128Key128BlockWorks()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey16Bytes, CryptoPrimitive.RijndaelManaged(128, 128), SHA256.Create(), new CypherCounter(16));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Rijndael256Key128BlockWorks()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.RijndaelManaged(256, 128), SHA256.Create(), new CypherCounter(16));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
#if NET471
        [TestMethod]
        public void Rijndael128Key256BlockWorks()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey16Bytes, CryptoPrimitive.RijndaelManaged(128, 256), SHA256.Create(), new CypherCounter(32));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Rijndael256Key256BlockWorks()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.RijndaelManaged(256, 256), SHA256.Create(), new CypherCounter(32));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
#endif

        [TestMethod]
        public void SameCypherAndKeyButAdditionalEntropyProduceDifferentRandomBlocks16Bytes()
        {
            var crng1 = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256(), SHA256.Create(), new CypherCounter(16), 0, CheapEntropy.Get16);
            var crng2 = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256(), SHA256.Create(), new CypherCounter(16), 0, CheapEntropy.Get16);
            var buffer1 = new byte[crng1.BlockSizeBytes * 2];
            var buffer2 = new byte[crng2.BlockSizeBytes * 2];
            crng1.FillWithRandomBytes(buffer1);
            crng2.FillWithRandomBytes(buffer2);

            CollectionAssert.AreNotEqual(buffer1, buffer2);
        }
        [TestMethod]
        public void SameCypherAndKeyButAdditionalEntropyProduceDifferentRandomBlocks32Bytes()
        {
            var crng1 = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256(), SHA256.Create(), new CypherCounter(16), 0, CheapEntropy.Get32);
            var crng2 = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256(), SHA256.Create(), new CypherCounter(16), 0, CheapEntropy.Get32);
            var buffer1 = new byte[crng1.BlockSizeBytes * 2];
            var buffer2 = new byte[crng2.BlockSizeBytes * 2];
            crng1.FillWithRandomBytes(buffer1);
            crng2.FillWithRandomBytes(buffer2);

            CollectionAssert.AreNotEqual(buffer1, buffer2);
        }

        [TestMethod]
        public void Hmac256CryptoPrimitiveWorks()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.HmacSha256(), SHA256.Create(), new CypherCounter(32));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Hmac512CryptoPrimitiveWorks()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey64Bytes, CryptoPrimitive.HmacSha512(), SHA512.Create(), new CypherCounter(64));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Sha256CryptoPrimitiveWorks()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Sha256(), SHA256.Create(), new CypherCounter(32));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Sha512CryptoPrimitiveWorks()
        {
            var crng = new CypherBasedPrngGenerator(_ZeroKey64Bytes, CryptoPrimitive.Sha512(), SHA512.Create(), new CypherCounter(64));
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }

        [TestMethod]
        public void MismatchedKeyMaterialAndCypherKeySizeThrows()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => 
                new CypherBasedPrngGenerator(_ZeroKey16Bytes, CryptoPrimitive.Aes256Managed(), SHA256.Create(), new CypherCounter(16))
            );
        }

        [TestMethod]
        public void Non16Or32ByteCypherThrows()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new CypherBasedPrngGenerator(new byte[14], CreateCryptoPrimitive(new TripleDESCryptoServiceProvider()), SHA256.Create(), new CypherCounter(16))
            );
        }

        [TestMethod]
        public void SmallerHashThanCypherKeySizeThrows()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256(), MD5.Create(), new CypherCounter(16))
            );
        }
        [TestMethod]
        public void DifferentCounterAndCypherBlockSizeThrows()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new CypherBasedPrngGenerator(_ZeroKey32Bytes, CryptoPrimitive.Aes256(), SHA256.Create(), new CypherCounter(32))
            );
        }


        [TestMethod]
        public void BufferedGeneratorCopesWithAllSizedRequests()
        {
            var crng = CypherBasedPrngGenerator.Create(_ZeroKey32Bytes, outputBufferSize: 1024);
            var buffer = new byte[1280];
            for (int i = 1; i < 1280; i++)
            {
                crng.FillWithRandomBytes(buffer, 0, i);
                Assert.IsFalse(buffer.All(b => b == 0));
                Array.Clear(buffer, 0, buffer.Length);
            }
        }

        private static ICryptoPrimitive CreateCryptoPrimitive(SymmetricAlgorithm cypher)
        {
            return new BlockCypherCryptoPrimitive(cypher);
        }
    }
}
