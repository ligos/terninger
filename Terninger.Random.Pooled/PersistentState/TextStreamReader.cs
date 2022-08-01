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
    /// A stream backed state reader which stores each item on a separate line using key values.
    /// </summary>
    /// <remarks>
    /// See TextFileReaderWriter for file format details.
    /// </remarks>
    public sealed class TextStreamReader : IPersistentStateReader
    {
        public string Separator { get; }
        readonly string[] Separators;
        public Encoding Encoding { get; }
        public Stream Stream { get; }

        public TextStreamReader(Stream stream, string separator = TextFileReaderWriter.DefaultSeparator, Encoding encoding = null)
        {
            _ = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException("A seekable stream is required.", nameof(stream));

            this.Stream = stream;
            this.Separator = String.IsNullOrEmpty(separator) ? TextFileReaderWriter.DefaultSeparator : separator;
            this.Separators = new[] { this.Separator };
            this.Encoding = encoding ?? TextFileReaderWriter.DefaultEncoding;
        }

        public async Task<PersistentItemCollection> ReadAsync()
        {
            var result = new PersistentItemCollection();
            byte[] sha256Checksum = new byte[32];
            
            this.Stream.Seek(0L, SeekOrigin.Begin);
            using (var reader = new StreamReader(this.Stream, this.Encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true))
            {
                bool isHeader = true;
                int fileVersion = 0;
                int lineNumber = 1;
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (isHeader)
                    {
                        // Read header.
                        (fileVersion, sha256Checksum, _) = ParseHeaderLine(line);
                    }
                    else
                    {
                        // Read data.
                        var item = ParseDataLine(line, lineNumber, fileVersion);
                        result.SetItem(item);
                    }

                    ++lineNumber;
                    isHeader = false;
                }
            }

            // Validate the data portion of the file content via hash.
            if (result.Count > 0)
                AssertFileChecksum(this.Stream, sha256Checksum);

            return result;
        }

        private (int fileVersion, byte[] sha256Checksum, int? itemCount) ParseHeaderLine(string line)
        {
            var parts = line.Split(this.Separators, StringSplitOptions.None);
            if (parts.Length < 4)
                throw new InvalidDataException($"Unable to parse header line of '{TextFileReaderWriter.StreamName(Stream)}': at least 4 items expected, but found {parts.Length}.");
            
            if (parts[0] != TextFileReaderWriter.MagicString)
                throw new InvalidDataException($"Unable to parse header line of '{TextFileReaderWriter.StreamName(Stream)}': expected magic string '{TextFileReaderWriter.MagicString}', but found '{parts[0]}'.");
            if (!Int32.TryParse(parts[1], out var fileVersion))
                throw new InvalidDataException($"Unable to parse header line of '{TextFileReaderWriter.StreamName(Stream)}': could not parse file version '{parts[1]}'.");

            byte[] sha256Checksum;
            if (parts[2].Length == 64)
            {
                try
                {
                    sha256Checksum = ByteArrayExtensions.ParseHexString(parts[2]);
                }
                catch (IndexOutOfRangeException ex)
                {
                    throw new InvalidDataException($"Unable to parse header line of '{TextFileReaderWriter.StreamName(Stream)}': could not parse checksum '{parts[2]}'.", ex);
                }
                catch (FormatException ex)
                {
                    throw new InvalidDataException($"Unable to parse header line of '{TextFileReaderWriter.StreamName(Stream)}': could not parse checksum '{parts[2]}'.", ex);
                }
            }
            else
            {
                try
                {
                    sha256Checksum = Convert.FromBase64String(parts[2]);
                }
                catch (FormatException ex)
                {
                    throw new InvalidDataException($"Unable to parse header line of '{TextFileReaderWriter.StreamName(Stream)}': could not parse checksum '{parts[2]}'.", ex);
                }
            }
            if (!Int32.TryParse(parts[3], out var itemCount))
                itemCount = -1;     // Item count is a hint anyway, if we can't parse it no big deal.

            return (fileVersion, sha256Checksum, itemCount <= 0 ? (int?)null : itemCount);
        }

        private NamespacedPersistentItem ParseDataLine(string line, int lineNumber, int fileVersion)
        {
            var parts = line.Split(this.Separators, StringSplitOptions.None);
            if (parts.Length < 4)
                throw new InvalidDataException($"Unable to read data line {lineNumber:N0} of '{TextFileReaderWriter.StreamName(Stream)}': at least 4 items expected, but found {parts.Length}.");

            var itemNamespace = parts[0];
            if (!PersistentItemCollection.ValidKey(itemNamespace))
                throw new InvalidDataException($"Unable to read data line {lineNumber:N0} of '{TextFileReaderWriter.StreamName(Stream)}': namespace '{itemNamespace}' contains invalid characters in the range {PersistentItemCollection.InvalidKeyUnicodeRanges}.");
            var key = parts[1];
            if (!PersistentItemCollection.ValidKey(key))
                throw new InvalidDataException($"Unable to read data line {lineNumber:N0} of '{TextFileReaderWriter.StreamName(Stream)}': key '{key}' contains invalid characters in the range {PersistentItemCollection.InvalidKeyUnicodeRanges}.");

            var encodingString = parts[2];
            ValueEncoding encoding;
            byte[] data;
            if (String.Equals(encodingString, nameof(ValueEncoding.Base64), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    data = Convert.FromBase64String(parts[3]);
                    encoding = ValueEncoding.Base64;
                }
                catch (FormatException ex)
                {
                    throw new InvalidDataException($"Unable to read data line {lineNumber:N0} of '{TextFileReaderWriter.StreamName(Stream)}': could not parse base64 value '{parts[3]}'.", ex);
                }
            }
            else if (String.Equals(encodingString, nameof(ValueEncoding.Hex), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    data = ByteArrayExtensions.ParseHexString(parts[3]);
                    encoding = ValueEncoding.Hex;
                }
                catch (IndexOutOfRangeException ex)
                {
                    throw new InvalidDataException($"Unable to read data line {lineNumber:N0} of '{TextFileReaderWriter.StreamName(Stream)}': could not parse hex value '{parts[3]}'.", ex);
                }
                catch (FormatException ex)
                {
                    throw new InvalidDataException($"Unable to read data line {lineNumber:N0} of '{TextFileReaderWriter.StreamName(Stream)}': could not parse hex value '{parts[3]}'.", ex);
                }
            }
            else if (String.Equals(encodingString, nameof(ValueEncoding.Utf8Text), StringComparison.OrdinalIgnoreCase))
            {
                data = Encoding.UTF8.GetBytes(parts[3]);
                encoding = ValueEncoding.Utf8Text;
            }
            else
            {
                throw new InvalidDataException($"Unable to read data line {lineNumber:N0} of '{TextFileReaderWriter.StreamName(Stream)}': unknown value encoding '{parts[2]}'.");
            }

            return new NamespacedPersistentItem(itemNamespace, key, encoding, data);
        }

        private void AssertFileChecksum(Stream stream, byte[] checksumFromHeader)
        {
            TextFileReaderWriter.SeekToBeginningOfData(stream);

            var hash = TextFileReaderWriter.ChecksumFromCurrentPosition(stream);
            if (!hash.AllEqual(checksumFromHeader))
                throw new InvalidDataException($"Header checksum '{checksumFromHeader.ToHexString()}' does not match data checksum '{hash.ToHexString()}': file content has been corrupted.");
        }
    }
}
