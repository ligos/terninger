using System;
using System.Collections.Generic;
using System.Linq;

namespace MurrayGrant.Terninger.Helpers
{
    public static class ByteArrayExtensions
    {
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

    }
}
