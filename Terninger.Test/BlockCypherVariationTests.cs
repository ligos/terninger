using System;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Generator;

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
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged(), SHA256.Create());
            var buffer = new byte[crng.BlockSizeBytes];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + (crng.BlockSizeBytes * 2));
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }
        [TestMethod]
        public void GenerateSingleBlockAesCsp()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesCryptoServiceProvider(), SHA256.Create());
            var buffer = new byte[crng.BlockSizeBytes];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
            Assert.AreEqual(crng.BytesGenerated, buffer.Length + (crng.BlockSizeBytes * 2));
            Assert.AreEqual(crng.BytesRequested, buffer.Length);
        }


        [TestMethod]
        public void AesCyphersProduceSameRandomBlocks()
        {
            var crngManaged = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged(), SHA256.Create());
            var crngCsp = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesCryptoServiceProvider(), SHA256.Create());
            var bufferManaged = new byte[crngManaged.BlockSizeBytes * 2];
            var bufferCsp = new byte[crngManaged.BlockSizeBytes * 2];
            crngManaged.FillWithRandomBytes(bufferManaged);
            crngCsp.FillWithRandomBytes(bufferCsp);

            CollectionAssert.AreEqual(bufferManaged, bufferCsp);
        }

        [TestMethod]
        public void Aes128And256CyphersProduceDifferentRandomBlocks()
        {
            var crngAes128 = new BlockCypherCprngGenerator(_ZeroKey16Bytes, new AesManaged() { KeySize = 128 }, SHA256.Create());
            var crngAes256 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged() { KeySize = 256 }, SHA256.Create());
            var buffer128 = new byte[crngAes128.BlockSizeBytes * 4];
            var buffer256 = new byte[crngAes256.BlockSizeBytes * 2];
            crngAes128.FillWithRandomBytes(buffer128);
            crngAes256.FillWithRandomBytes(buffer256);

            CollectionAssert.AreNotEqual(buffer128, buffer256);
        }

        [TestMethod]
        public void Sha256And512AlgorithmsProduceDifferentRandomBlocks()
        {
            var crngSha256 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged() { KeySize = 256 }, SHA256.Create());
            var crngSha512 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged() { KeySize = 256 }, SHA512.Create());
            var buffer256 = new byte[crngSha256.BlockSizeBytes * 2];
            var buffer512 = new byte[crngSha512.BlockSizeBytes * 2];
            crngSha256.FillWithRandomBytes(buffer256);
            crngSha512.FillWithRandomBytes(buffer512);

            CollectionAssert.AreNotEqual(buffer256, buffer512);
        }

        [TestMethod]
        public void Sha384AgorithmsWorks()
        {
            var crngSha384 = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new AesManaged() { KeySize = 256 }, SHA384.Create());
            var buffer384 = new byte[crngSha384.BlockSizeBytes * 2];
            crngSha384.FillWithRandomBytes(buffer384);

            Assert.IsFalse(buffer384.All(b => b == 0));
        }
        [TestMethod]
        public void Md5AndAes128Works()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey16Bytes, new AesManaged() { KeySize = 128 }, MD5.Create());
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Sha1AndAes128Works()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey16Bytes, new AesManaged() { KeySize = 128 }, SHA1.Create());
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }

        [TestMethod]
        public void Rijndael128Key128BlockWorks()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey16Bytes, new RijndaelManaged() { KeySize = 128, BlockSize = 128 }, SHA256.Create());
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Rijndael256Key128BlockWorks()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new RijndaelManaged() { KeySize = 256, BlockSize = 128 }, SHA256.Create());
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Rijndael128Key256BlockWorks()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey16Bytes, new RijndaelManaged() { KeySize = 128, BlockSize = 256 }, SHA256.Create());
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }
        [TestMethod]
        public void Rijndael256Key256BlockWorks()
        {
            var crng = new BlockCypherCprngGenerator(_ZeroKey32Bytes, new RijndaelManaged() { KeySize = 256, BlockSize = 256 }, SHA256.Create());
            var buffer = new byte[crng.BlockSizeBytes * 2];
            crng.FillWithRandomBytes(buffer);

            Assert.IsFalse(buffer.All(b => b == 0));
        }

        [TestMethod]
        public void MismatchedKeyMaterialAndCypherKeySizeThrows()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => 
                new BlockCypherCprngGenerator(_ZeroKey16Bytes, new AesManaged() { KeySize = 256 }, SHA256.Create())
            );
        }

        [TestMethod]
        public void Non16Or32ByteCypherThrows()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new BlockCypherCprngGenerator(new byte[14], new TripleDESCryptoServiceProvider(), SHA256.Create())
            );
        }

        [TestMethod]
        public void SmallerHashThanCypherKeySizeThrows()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new BlockCypherCprngGenerator(_ZeroKey32Bytes, Aes.Create(), MD5.Create())
            );
        }
    }
}
