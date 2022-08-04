using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Helpers;

namespace MurrayGrant.Terninger.PersistentState
{
    /// <summary>
    /// A stream backed state writer which stores each item on a separate line using key values.
    /// </summary>
    /// <remarks>
    /// See TextFileReaderWriter for file format details.
    /// </remarks>
    public sealed class TextStreamWriter : IPersistentStateWriter
    {
        public string Separator { get; }
        readonly string[] Separators;
        public Encoding Encoding { get; }
        public Stream Stream { get; }

        public string Location => TextFileReaderWriter.StreamName(Stream);

        public TextStreamWriter(Stream stream, string separator = TextFileReaderWriter.DefaultSeparator, Encoding encoding = null)
        {
            _ = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException("A seekable stream is required.", nameof(stream));

            this.Stream = stream;
            this.Separator = String.IsNullOrEmpty(separator) ? TextFileReaderWriter.DefaultSeparator : separator;
            this.Separators = new[] { this.Separator };
            this.Encoding = encoding ?? TextFileReaderWriter.DefaultEncoding;
        }

        public async Task WriteAsync(PersistentItemCollection items)
        {
            _ = items ?? throw new ArgumentNullException(nameof(items));

            // Determine header.
            // The header line won't change length after it's created below, which makes it easier to update the hash.
            // (In fact, aside from the count of items, the length can be determined at compile time).
            var headerChecksum = new byte[32];
            var headerLine = HeaderLine(headerChecksum, items.Count);

            using (var writer = new StreamWriter(this.Stream, this.Encoding, bufferSize: 4096, leaveOpen: true))
            {
                writer.NewLine = "\n";

                // Write header line.
                await writer.WriteLineAsync(headerLine);

                // Write body.
                foreach (var item in items)
                {
                    var line = $"{item.Namespace}{Separator}{item.Key}{Separator}{item.ValueEncoding}{Separator}{item.ValueAsEncodedString}";
                    await writer.WriteLineAsync(line);
                }
            }

            if (items.Count == 0)
                return;

            // Determine checksum.
            TextFileReaderWriter.SeekToBeginningOfData(this.Stream);
            headerChecksum = TextFileReaderWriter.ChecksumFromCurrentPosition(this.Stream);

            // Update header with hash.
            this.Stream.Seek(0L, SeekOrigin.Begin);
            headerLine = HeaderLine(headerChecksum, items.Count);
            var headerBytes = Encoding.GetBytes(headerLine);
            await this.Stream.WriteAsync(headerBytes, 0, headerBytes.Length);
        }

        string HeaderLine(byte[] headerChecksum, int itemCount)
            => $"{TextFileReaderWriter.MagicString}{Separator}{TextFileReaderWriter.FileVersionNumber}{Separator}{Convert.ToBase64String(headerChecksum)}{Separator}{itemCount}";

    }
}
