using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace MurrayGrant.Terninger.Helpers
{
    public static class ByteArrayHelpers
    {
        public static string ToHexString(this byte[] bytes)
        {
            var result = new StringBuilder(bytes.Length * 2, bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                result.Append(bytes[i].ToString("X2"));
            }
            return result.ToString();
        }

        public static byte ToHexAsciiHighNibble(this byte b) => HexToAsciiByteLookup[((b & 0x000000f0) >> 4)];
        public static byte ToHexAsciiLowNibble(this byte b) => HexToAsciiByteLookup[(b & 0x0000000f)];
        private static byte[] HexToAsciiByteLookup = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' }.Select(ch => (byte)ch).ToArray();

        public static byte[] ParseFromHexString(this string s)
        {
            var result = new byte[s.Length / 2];
            for (int i = 0; i < s.Length / 2; i++)
            {
                result[i] = Byte.Parse(s.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
            }
            return result;
        }

        private static HashSet<char> HexDigits = new HashSet<char>(new [] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f', 'A', 'B', 'C', 'D', 'E', 'F' });
        public static bool IsHexString(this string s)
        {
            if (s == null) return false;
            if (s == "") return false;
            if (s.Length % 2 != 0) return false;        // Hex strings always have an even number of characters.
            var result = s.All(ch => HexDigits.Contains(ch));
            return result;
        }

        /// <summary>
        /// Ensure the byte array passed in is exactly required bytes in length.
        /// Longer arrays are trucated, shorter arrays are padded.
        /// </summary>
        public static byte[] EnsureArraySize(this byte[] bytes, int requiredSize)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            // Simple case.
            if (bytes.Length == requiredSize) return bytes;
            // More bytes than required: truncate.
            if (bytes.Length > requiredSize) return bytes.Take(requiredSize).ToArray();
            // Need more bytes than are available: pad with zeros (as we can't magically add extra entropy).
            if (bytes.Length < requiredSize)
            {
                var result = new byte[requiredSize];
                Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
                return result;
            }
            throw new Exception("Unexpected state");
        }

        /// <summary>
        /// Returns a new array with a copy of the last bytes of this array.
        /// </summary>
        public static byte[] LastBytes(this byte[] bytes, int count)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < count) throw new ArgumentOutOfRangeException(nameof(count), count, $"Count {count} is larger than bytes {bytes.Length}.");
            var result = new byte[count];
            Buffer.BlockCopy(bytes, bytes.Length - count, result, 0, count);
            return result;
        }

        public static string LongsToHexString(this long[] longs)
        {
            var result = new StringBuilder(longs.Length * 16);
            for (int i = 0; i < longs.Length; i++)
            {
                result.Append(longs[i].ToString("x8"));
            }
            return result.ToString();
        }

        public static byte[] LongsToDigestBytes(this long[] longs) => LongsToDigestBytes(longs, longs.Length);
        public static byte[] LongsToDigestBytes(this long[] longs, int itemsPerChunk)
        {
            // Produce hashes of chunks of results to return.
            // We use an SHA256 hash of many individual stats to produce output.
            
            var hash = SHA256.Create();
            var resultCount = longs.Length / itemsPerChunk;
            var chunkSizeBytes = itemsPerChunk * sizeof(long);
            if (resultCount == 0)
            {
                // Not enough stats based on ItemsPerResultChunk - so just hash everything.
                resultCount = 1;
                chunkSizeBytes = longs.Length * sizeof(long);
            }
            var hashSizeBytes = hash.HashSize / 8;
            var result = new byte[resultCount * hashSizeBytes];
            for (int i = 0; i < resultCount; i++)
            {
                // Copy to byte[] so we can hash.
                var statsAsBytes = new byte[chunkSizeBytes];
                Buffer.BlockCopy(longs, chunkSizeBytes * i, statsAsBytes, 0, statsAsBytes.Length);
                // Hash and copy to result.
                var h = hash.ComputeHash(statsAsBytes);
                Buffer.BlockCopy(h, 0, result, hashSizeBytes * i, hashSizeBytes);
            }
            return result;
        }

    }
}
