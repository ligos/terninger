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
            var s = MemoryStreamOf(@"TngrData,1,xrPpBkLAl4QKhOq73qEhMopVKIR3ogMiuY6fvZ1vOq8=,1
SomeNamespace,aKey,dmFsdWU=");
            var reader = new TextStreamReader(s);
            var result = await reader.ReadAsync();
            var expected = new PersistentItemCollection(new[]
            {
                new NamespacedPersistentItem("SomeNamespace", "aKey", Encoding.UTF8.GetBytes("value")),
            });
            CollectionAssert.AreEquivalent(expected.ToList(), result.ToList());
        }

        [TestMethod]
        public async Task StreamReader_ReadsFrom_DataFileWithManyItems()
        {
            var s = MemoryStreamOf(@"TngrData,1,KP5nD1ivMguYZNLr2SfmXJ5LPIqI/vFuPeufR7GZKbo=,6
Namespace,Key,RGF0YQ==
Namespace,Key2,T3RoZXJkYXRh
Namespace,Integer,KgAAAA==
Global,Thing,AAECAwQFBgcJCgsMDQ4P
Global,Key,RGF0YQ==
SomeNamespace,aKey,dmFsdWU=");
            var reader = new TextStreamReader(s);
            var result = await reader.ReadAsync();
            var expected = new PersistentItemCollection(new[]
            {
                new NamespacedPersistentItem("SomeNamespace", "aKey", Encoding.UTF8.GetBytes("value")),
            });
            CollectionAssert.AreEquivalent(expected.ToList(), result.ToList());
        }

        #region Failure cases
        [TestMethod]
        public void StreamReader_Throws_WhenShortHeader()
        {
        }
        [TestMethod]
        public void StreamReader_Throws_WhenWrongMagicString()
        {
        }
        [TestMethod]
        public void StreamReader_Throws_WhenNonIntegerVersion()
        {
        }
        [TestMethod]
        public void StreamReader_Throws_WhenNonBase64Checksum()
        {
        }
        [TestMethod]
        public void StreamReader_DoesNotThrow_WhenNonIntegerItemCount()
        {
        }
        [TestMethod]
        public void StreamReader_Throws_WhenShortDataLine()
        {
        }
        [TestMethod]
        public void StreamReader_Throws_WhenInvalidCharactersInNamespace()
        {
        }
        [TestMethod]
        public void StreamReader_Throws_WhenBlankNamespace()
        {
        }
        [TestMethod]
        public void StreamReader_Throws_WhenInvalidCharactersInKey()
        {
        }
        [TestMethod]
        public void StreamReader_Throws_WhenBlankKey()
        {
        }
        [TestMethod]
        public void StreamReader_Throws_WhenNonBase64Data()
        {
        }
        [TestMethod]
        public void StreamReader_Throws_WhenHeaderChecksumDoesNotMatchDataContent()
        {
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
            items.SetItem("SomeNamespace", "aKey", Encoding.UTF8.GetBytes("value"));
            var writer = new TextStreamWriter(new MemoryStream());
            await writer.WriteAsync(items);
            var expectedContent = @"TngrData,1,xrPpBkLAl4QKhOq73qEhMopVKIR3ogMiuY6fvZ1vOq8=,1
SomeNamespace,aKey,dmFsdWU=";
            var actualContent = await TextFromUtf8Stream(writer.Stream);
            Assert.AreEqual(expectedContent, actualContent);
        }

        [TestMethod]
        public async Task StreamWriter_Writes_ManyItems()
        {
            var items = new PersistentItemCollection(new[]
            {
                new NamespacedPersistentItem("Namespace", "Key", Encoding.UTF8.GetBytes("Data")),
                new NamespacedPersistentItem("Namespace", "Key2", Encoding.UTF8.GetBytes("Otherdata")),
                new NamespacedPersistentItem("Namespace", "Integer", BitConverter.GetBytes(42)),
                new NamespacedPersistentItem("Global", "Thing", new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 9, 10, 11, 12, 13, 14, 15 }),
                new NamespacedPersistentItem("Global", "Key", Encoding.UTF8.GetBytes("Data")),
            });
            items.SetItem("SomeNamespace", "aKey", Encoding.UTF8.GetBytes("value"));
            var writer = new TextStreamWriter(new MemoryStream());
            await writer.WriteAsync(items);
            var expectedContent = @"TngrData,1,xrPpBkLAl4QKhOq73qEhMopVKIR3ogMiuY6fvZ1vOq8=,1
SomeNamespace,aKey,dmFsdWU=";
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
                new NamespacedPersistentItem("Namespace", "Key", Encoding.UTF8.GetBytes("Data")),
            });
            var writer = new TextStreamWriter(new MemoryStream());
            await writer.WriteAsync(items);
            var reader = new TextStreamReader(writer.Stream);
            var roundTrippedItems = await reader.ReadAsync();
            CollectionAssert.AreEquivalent(items.ToList(), roundTrippedItems.ToList());
        }

        [TestMethod]
        public async Task RoundTrip_ManyItems()
        {
            var items = new PersistentItemCollection(new[] {
                new NamespacedPersistentItem("Namespace", "Key", Encoding.UTF8.GetBytes("Data")),
                new NamespacedPersistentItem("Namespace", "Key2", Encoding.UTF8.GetBytes("Otherdata")),
                new NamespacedPersistentItem("Namespace", "Integer", BitConverter.GetBytes(42)),
                new NamespacedPersistentItem("Global", "Thing", Guid.NewGuid().ToByteArray()),
                new NamespacedPersistentItem("Global", "Key", Encoding.UTF8.GetBytes("Data")),
            });
            var writer = new TextStreamWriter(new MemoryStream());
            await writer.WriteAsync(items);
            var reader = new TextStreamReader(writer.Stream);
            var roundTrippedItems = await reader.ReadAsync();
            CollectionAssert.AreEquivalent(items.ToList(), roundTrippedItems.ToList());
        }
        #endregion

        private Stream MemoryStreamOf(string fileContent)
            => new MemoryStream(
                Encoding.UTF8.GetBytes(
                    fileContent
                        .Replace(",", "\u001F")
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
                    .Replace("\u001F", ",")
                    .Replace("\n", "\r\n")
                    .TrimEnd();
        }
    }
}
