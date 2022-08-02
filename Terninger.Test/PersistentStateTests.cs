using System;
using System.Security.Cryptography;
using System.Linq;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Test;
using MurrayGrant.Terninger.PersistentState;
using System.Threading.Tasks;
using MurrayGrant.Terninger.Helpers;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class PersistentStateTests
    {
        #region Reader Tests

        [TestMethod]
        public async Task StreamReader_ReadsFrom_EmptyFile()
        {
            var s = MemoryStreamOf(@"TngrData,1,AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=,0");
            var reader = new TextStreamReader(s);
            var result = await reader.ReadAsync();
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task StreamReader_ReadsFrom_DataFileWithOneItem()
        {
            var s = MemoryStreamOf(@"TngrData,1,C6879AC3611B142E4DDDFFC1AE1F4140EA9B1F1424FBDC4F6DD2E6B848AEE855,1
SomeNamespace,aKey,base64,dmFsdWU=");
            var reader = new TextStreamReader(s);
            var result = (await reader.ReadAsync()).ToList();
            var expected = new[]
            {
                new NamespacedPersistentItem("SomeNamespace", "aKey", ValueEncoding.Base64, Encoding.UTF8.GetBytes("value")),
            };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[0], result[0]);
            }
        }

        [TestMethod]
        public async Task StreamReader_ReadsFrom_DataFileWithManyItems()
        {
            var s = MemoryStreamOf(@"TngrData,1,5DA37102117D3F07AC40CF8D32FD9C9227B37F4F14CF4361ED83690ADB9A2186,6
Namespace,Key,utf8text,Data
Namespace,Key2,utf8text,OtherData
Namespace,Integer,hex,2A000000
Global,Thing,hex,000102030405060708090A0B0C0D0E0F
Global,Key,base64,RGF0YQ==
SomeNamespace,aKey,utf8text,value");
            var reader = new TextStreamReader(s);
            var result = (await reader.ReadAsync()).ToList();
            var expected = new[]
            {
                new NamespacedPersistentItem("Namespace", "Key", ValueEncoding.Utf8Text, Encoding.UTF8.GetBytes("Data")),
                new NamespacedPersistentItem("Namespace", "Key2", ValueEncoding.Utf8Text, Encoding.UTF8.GetBytes("Otherdata")),
                new NamespacedPersistentItem("Namespace", "Integer", ValueEncoding.Hex, BitConverter.GetBytes(42)),
                new NamespacedPersistentItem("Global", "Thing", ValueEncoding.Hex, new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 9, 10, 11, 12, 13, 14, 15 }),
                new NamespacedPersistentItem("Global", "Key", ValueEncoding.Base64, Encoding.UTF8.GetBytes("Data")),
                new NamespacedPersistentItem("SomeNamespace", "aKey", ValueEncoding.Utf8Text, Encoding.UTF8.GetBytes("value")),
            };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[0], result[0]);
            }
        }

        #region Failure cases
        [TestMethod]
        public async Task StreamReader_Throws_WhenShortHeader()
        {
            var s = MemoryStreamOf(@"TngrData,1");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }

        [TestMethod]
        public async Task StreamReader_Throws_WhenWrongMagicString()
        {
            var s = MemoryStreamOf(@"00000000,1,AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=,0");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }

        [TestMethod]
        public async Task StreamReader_Throws_WhenNonIntegerVersion()
        {
            var s = MemoryStreamOf(@"TngrData,z,AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=,0");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }

        [TestMethod]
        public async Task StreamReader_Throws_WhenNonBase64Checksum()
        {
            var s = MemoryStreamOf(@"TngrData,z,••™=,0");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }

        [TestMethod]
        public async Task StreamReader_Throws_WhenNonHexChecksum()
        {
            var s = MemoryStreamOf(@"TngrData,z,abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl,0");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }

        [TestMethod]
        public async Task StreamReader_DoesNotThrow_WhenNonIntegerItemCount()
        {
            var s = MemoryStreamOf(@"TngrData,1,AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=,z");
            var reader = new TextStreamReader(s);
            await reader.ReadAsync();
        }

        [TestMethod]
        public async Task StreamReader_Throws_WhenShortDataLine()
        {
            var s = MemoryStreamOf(@"TngrData,1,0CBDE130FBE6ED6A6709013810F81CC4CB6B6D236A49569DA1DD0F0A114FDF6E,1
SomeNamespace");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }

        [TestMethod]
        public async Task StreamReader_Throws_WhenInvalidCharactersInNamespace()
        {
            var s = MemoryStreamOf(@"TngrData,1,0CBDE130FBE6ED6A6709013810F81CC4CB6B6D236A49569DA1DD0F0A114FDF6E,1
Some	Namespace,aKey,dmFsdWU=");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }

        [TestMethod]
        public async Task StreamReader_Throws_WhenBlankNamespace()
        {
            var s = MemoryStreamOf(@"TngrData,1,0CBDE130FBE6ED6A6709013810F81CC4CB6B6D236A49569DA1DD0F0A114FDF6E,1
,aKey,dmFsdWU=");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }

        [TestMethod]
        public async Task StreamReader_Throws_WhenInvalidCharactersInKey()
        {
            var s = MemoryStreamOf(@"TngrData,1,0CBDE130FBE6ED6A6709013810F81CC4CB6B6D236A49569DA1DD0F0A114FDF6E,1
SomeNamespace,a	Key,dmFsdWU=");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }

        [TestMethod]
        public async Task StreamReader_Throws_WhenBlankKey()
        {
            var s = MemoryStreamOf(@"TngrData,1,0CBDE130FBE6ED6A6709013810F81CC4CB6B6D236A49569DA1DD0F0A114FDF6E,1
SomeNamespace,,dmFsdWU=");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }

        [TestMethod]
        public async Task StreamReader_Throws_WhenNonBase64Data()
        {
            var s = MemoryStreamOf(@"TngrData,1,0CBDE130FBE6ED6A6709013810F81CC4CB6B6D236A49569DA1DD0F0A114FDF6E,1
SomeNamespace,aKey,base64,•™=");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }

        [TestMethod]
        public async Task StreamReader_Throws_WhenNonHexData()
        {
            var s = MemoryStreamOf(@"TngrData,1,0CBDE130FBE6ED6A6709013810F81CC4CB6B6D236A49569DA1DD0F0A114FDF6E,1
SomeNamespace,aKey,hex,•™=");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }

        [TestMethod]
        public async Task StreamReader_Throws_WhenUnknownEncodingType()
        {
            var s = MemoryStreamOf(@"TngrData,1,0CBDE130FBE6ED6A6709013810F81CC4CB6B6D236A49569DA1DD0F0A114FDF6E,1
SomeNamespace,aKey,base95,•™=");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }

        [TestMethod]
        public async Task StreamReader_Throws_WhenHeaderChecksumDoesNotMatchDataContent()
        {
            var s = MemoryStreamOf(@"TngrData,1,0000000000000000000000000000000000000000000000000000000000000000,1
SomeNamespace,aKey,dmFsdWU=");
            var reader = new TextStreamReader(s);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => reader.ReadAsync());
        }
        #endregion

        #endregion

        #region Writer Tests
        [TestMethod]
        public async Task StreamWriter_Writes_EmptyCollection()
        {
            var items = new PersistentItemCollection();
            var writer = new TextStreamWriter(new MemoryStream());
            await writer.WriteAsync(items);
            var expectedContent = @"TngrData,1,AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=,0";
            var actualContent = await TextFromUtf8Stream(writer.Stream);
            Assert.AreEqual(expectedContent, actualContent);
        }

        [TestMethod]
        public async Task StreamWriter_Writes_OneItem()
        {
            var items = new PersistentItemCollection();
            items.SetItem("SomeNamespace", "aKey", ValueEncoding.Utf8Text, Encoding.UTF8.GetBytes("value"));
            var writer = new TextStreamWriter(new MemoryStream());
            await writer.WriteAsync(items);
            var expectedContent = @"TngrData,1,UDpxL5ZiKhda8ok3/asKFbmdaihfvAzJmVhxzBP/SaI=,1
SomeNamespace,aKey,Utf8Text,value";
            var actualContent = await TextFromUtf8Stream(writer.Stream);
            Assert.AreEqual(expectedContent, actualContent);
        }

        [TestMethod]
        public async Task StreamWriter_Writes_ManyItems()
        {
            var items = new PersistentItemCollection(new[]
            {
                new NamespacedPersistentItem("Namespace", "Key", ValueEncoding.Utf8Text, Encoding.UTF8.GetBytes("Data")),
                new NamespacedPersistentItem("Namespace", "Key2", ValueEncoding.Utf8Text, Encoding.UTF8.GetBytes("Otherdata")),
                new NamespacedPersistentItem("Namespace", "Integer", ValueEncoding.Hex, BitConverter.GetBytes(42)),
                new NamespacedPersistentItem("Global", "Thing", ValueEncoding.Base64, new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 9, 10, 11, 12, 13, 14, 15 }),
                new NamespacedPersistentItem("Global", "Key", ValueEncoding.Utf8Text, Encoding.UTF8.GetBytes("Data")),
            });
            items.SetItem("SomeNamespace", "aKey", ValueEncoding.Utf8Text, Encoding.UTF8.GetBytes("value"));
            var writer = new TextStreamWriter(new MemoryStream());
            await writer.WriteAsync(items);
            var expectedContent = @"TngrData,1,DBvlW8Nt/XTVKr/aMGWZd8N6KQ9nb8d+BNBWbfzSs8A=,6
Namespace,Key,Utf8Text,Data
Namespace,Key2,Utf8Text,Otherdata
Namespace,Integer,Hex,2A000000
Global,Thing,Base64,AAECAwQFBgcJCgsMDQ4P
Global,Key,Utf8Text,Data
SomeNamespace,aKey,Utf8Text,value";
            var actualContent = await TextFromUtf8Stream(writer.Stream);
            Assert.AreEqual(expectedContent, actualContent);
        }
        #endregion

        #region Round trip
        [TestMethod]
        public async Task RoundTrip_ZeroItems()
        {
            var items = new PersistentItemCollection();
            var writer = new TextStreamWriter(new MemoryStream());
            await writer.WriteAsync(items);
            var reader = new TextStreamReader(writer.Stream);
            var roundTrippedItems = await reader.ReadAsync();
            CollectionAssert.AreEquivalent(items.ToList(), roundTrippedItems.ToList());
        }

        [TestMethod]
        public async Task RoundTrip_OneItem()
        {
            var items = new PersistentItemCollection(new[] {
                new NamespacedPersistentItem("Namespace", "Key", ValueEncoding.Utf8Text, Encoding.UTF8.GetBytes("Data")),
            });
            var writer = new TextStreamWriter(new MemoryStream());
            await writer.WriteAsync(items);
            var reader = new TextStreamReader(writer.Stream);
            var roundTrippedItems = await reader.ReadAsync();

            Assert.AreEqual(items["Namespace"]["Key"], roundTrippedItems["Namespace"]["Key"]);
        }

        [TestMethod]
        public async Task RoundTrip_ManyItems()
        {
            var items = new PersistentItemCollection(new[] {
                new NamespacedPersistentItem("Namespace", "Key", ValueEncoding.Utf8Text, Encoding.UTF8.GetBytes("Data")),
                new NamespacedPersistentItem("Namespace", "Key2", ValueEncoding.Hex, Encoding.UTF8.GetBytes("Otherdata")),
                new NamespacedPersistentItem("Namespace", "Integer", ValueEncoding.Base64, BitConverter.GetBytes(42)),
                new NamespacedPersistentItem("Global", "Thing", ValueEncoding.Hex, Guid.NewGuid().ToByteArray()),
                new NamespacedPersistentItem("Global", "Key", ValueEncoding.Base64, Encoding.UTF8.GetBytes("Data")),
            });
            var writer = new TextStreamWriter(new MemoryStream());
            await writer.WriteAsync(items);
            var reader = new TextStreamReader(writer.Stream);
            var roundTrippedItems = await reader.ReadAsync();

            Assert.AreEqual(items["Namespace"]["Key"], roundTrippedItems["Namespace"]["Key"]);
            Assert.AreEqual(items["Namespace"]["Key2"], roundTrippedItems["Namespace"]["Key2"]);
            Assert.AreEqual(items["Namespace"]["Integer"], roundTrippedItems["Namespace"]["Integer"]);
            Assert.AreEqual(items["Global"]["Thing"], roundTrippedItems["Global"]["Thing"]);
            Assert.AreEqual(items["Global"]["Key"], roundTrippedItems["Global"]["Key"]);
        }
        #endregion

        private Stream MemoryStreamOf(string fileContent)
            => new MemoryStream(
                Encoding.UTF8.GetBytes(
                    fileContent
                        .Replace(",", "\t")
                        .Replace("\r\n", "\n")
                    )
                );

        private async Task<string> TextFromUtf8Stream(Stream s)
        {
            var content = new byte[(int)s.Length];
            s.Seek(0L, SeekOrigin.Begin);
            await s.ReadAsync(content, 0, content.Length);
            var text = Encoding.UTF8.GetString(content);
            return text
                    .Replace("\t", ",")
                    .Replace("\n", "\r\n")
                    .TrimEnd();
        }
    }
}
