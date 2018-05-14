using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MurrayGrant.Terninger.Generator;

namespace MurrayGrant.Terninger
{
    public static class RandomNumberExtensions
    {
        #region GetRandomBytes()
        /// <summary>
        /// Generates the requested number of random bytes.
        /// </summary>
        /// <param name="generator"></param>
        /// <param name="byteCount"></param>
        /// <returns></returns>
        public static byte[] GetRandomBytes(this IRandomNumberGenerator generator, int byteCount)
        {
            // PERF: pre-allocate or pool or cache the buffers.
            if (byteCount > generator.MaxRequestBytes)
                // We need to loop for a larger request.
                // For greater chance of inlining, we push the more complex logic to a separate method.
                return GetRandomBytesLarge(generator, byteCount);

            var result = new byte[byteCount];
            generator.FillWithRandomBytes(result);
            return result;
        }
        private static byte[] GetRandomBytesLarge(this IRandomNumberGenerator generator, int byteCount)
        {
            var result = new byte[byteCount];
            int offset = 0;
            while (offset < byteCount)
            {
                var remaining = byteCount - offset;
                var bytesToGetThisTime = generator.MaxRequestBytes < remaining ? generator.MaxRequestBytes : remaining;
                generator.FillWithRandomBytes(result, offset, bytesToGetThisTime);

                offset = offset + bytesToGetThisTime;
            }
            return result;
        }
        #endregion

        #region GetRandomBoolean()
        public static bool GetRandomBoolean(this IRandomNumberGenerator generator)
        {
            var buf = new byte[1];      // PERF: pre-allocate or pool or cache the buffers.
            generator.FillWithRandomBytes(buf);
            return buf[0] >= 128;       // Low values of the byte are false, high values are true.
        }
        #endregion


        // Implementation for GetRandom...() based on http://codereview.stackexchange.com/questions/6304/algorithm-to-convert-random-bytes-to-integers
        #region GetRandom[U]Int32()
        public static uint GetRandomUInt32(this IRandomNumberGenerator generator)
        {
            var buf = new byte[4];      // PERF: pre-allocate or pool or cache the buffers.
            generator.FillWithRandomBytes(buf);
            var i = BitConverter.ToUInt32(buf, 0);
            return i;
        }
        public static int GetRandomInt32(this IRandomNumberGenerator generator)
        {
            var i = GetRandomUInt32(generator);
            return (int)(i & (uint)Int32.MaxValue);
        }
        public static int GetRandomInt32(this IRandomNumberGenerator generator, int maxExlusive)
        {
            if (maxExlusive <= 0) throw new ArgumentOutOfRangeException("maxExlusive", maxExlusive, "maxExlusive must be positive");

            // Let k = (Int32.MaxValue + 1) % maxExcl
            // Then we want to exclude the top k values in order to get a uniform distribution
            // You can do the calculations using uints if you prefer to only have one %
            uint k = (((uint)Int32.MaxValue % (uint)maxExlusive) + (uint)1);
            var result = GetRandomInt32(generator);
            while (result > Int32.MaxValue - (int)k)
                result = GetRandomInt32(generator);
            return result % maxExlusive;
        }
        public static int GetRandomInt32(this IRandomNumberGenerator generator, int minValue, int maxValue)
        {
            if (minValue < 0)
                throw new ArgumentOutOfRangeException("minValue", minValue, "minValue must be non-negative");
            if (maxValue <= minValue)
                throw new ArgumentOutOfRangeException("maxValue", maxValue, "maxValue must be greater than minValue");
            return minValue + GetRandomInt32(generator, maxValue - minValue);
        }
        #endregion

        #region GetRandom[U]Int64()
        public static ulong GetRandomUInt64(this IRandomNumberGenerator generator)
        {
            var buf = new byte[8];      // PERF: pre-allocate or pool or cache the buffers.
            generator.FillWithRandomBytes(buf);
            ulong i = BitConverter.ToUInt64(buf, 0);
            return i;
        }
        public static long GetRandomInt64(this IRandomNumberGenerator generator)
        {
            var i = GetRandomUInt64(generator);
            return (long)(i & (ulong)Int64.MaxValue);
        }
        public static long GetRandomInt64(this IRandomNumberGenerator generator, long maxExlusive)
        {
            if (maxExlusive <= 0) throw new ArgumentOutOfRangeException("maxExlusive", maxExlusive, "maxExlusive must be positive");

            // Let k = (Int64.MaxValue + 1) % maxExcl
            // Then we want to exclude the top k values in order to get a uniform distribution
            // You can do the calculations using uints if you prefer to only have one %
            ulong k = (((ulong)Int64.MaxValue % (ulong)maxExlusive) + (ulong)1);
            var result = GetRandomInt64(generator);
            while (result > Int64.MaxValue - (long)k)
                result = GetRandomInt64(generator);
            return result % maxExlusive;
        }
        public static long GetRandomInt64(this IRandomNumberGenerator generator, long minValue, long maxValue)
        {
            if (minValue < 0)
                throw new ArgumentOutOfRangeException("minValue", minValue, "minValue must be non-negative");
            if (maxValue <= minValue)
                throw new ArgumentOutOfRangeException("maxValue", maxValue, "maxValue must be greater than minValue");
            return minValue + GetRandomInt64(generator, maxValue - minValue);
        }
        #endregion

        #region GetRandomSingle|Double|Decimal()
        public static float GetRandomSingle(this IRandomNumberGenerator generator)
        {
            return GetRandomUInt32(generator) * (1.0f / UInt32.MaxValue);
        }
        public static double GetRandomDouble(this IRandomNumberGenerator generator)
        {
            return GetRandomUInt64(generator) * (1.0 / UInt64.MaxValue);
        }
        public static decimal GetRandomDecimal(this IRandomNumberGenerator generator)
        {
            // Generate 96 bits of randomness for the decimal (which is its full precision).
            // However, 96 bits will sometimes exceed the required range of 0.0-1.0
            // Masking off the top bit (giving 95 bits precision) gives a distribution of 0.0-0.25.
            // And we multiply by 4 to spread that in the range 0.0-1.0 nicely.
            // Note that the .038m part is based on looking at the final distribution and fitting to right up to the top.
            // PERF: no loops! Just a bitwise and and decimal multiply.
            // PERF: cache / reuse the byte array.

            var rawBytes = new byte[12];
            generator.FillWithRandomBytes(rawBytes);

            var lo = BitConverter.ToInt32(rawBytes, 0);
            var mid = BitConverter.ToInt32(rawBytes, 4);
            var hi = BitConverter.ToInt32(rawBytes, 8) & 0x7ffffff;     
            var d = new decimal(lo, mid, hi, false, 28);
            d = d * 4.038m;
            return d;
        }
        #endregion


        public static Guid GetRandomGuid(this IRandomNumberGenerator generator)
        {
            var rawBytes = GetRandomBytes(generator, 16);
            // https://en.wikipedia.org/wiki/Universally_unique_identifier
            // https://tools.ietf.org/html/rfc4122
            rawBytes[7] = (byte)(rawBytes[7] & (byte)0x0f | (byte)0x40);        // Set the magic version bits.
            rawBytes[8] = (byte)(rawBytes[8] & (byte)0x3f | (byte)0x80);        // Set the magic variant bits.
            var result = new Guid(rawBytes);
            return result;
        }

        public static RandomByteStream GetRandomStream(this IRandomNumberGenerator generator)
        {
            return new RandomByteStream(generator);
        }

        public static void ShuffleInPlace<T>(this IList<T> list, IRandomNumberGenerator generator)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (generator == null) throw new ArgumentNullException(nameof(generator));

            for (int i = list.Count - 1; i > 0; i--)
            {
                // Swap element "i" with a random earlier element it (or itself)
                int swapIndex = generator.GetRandomInt32(i + 1);
                T tmp = list[i];
                list[i] = list[swapIndex];
                list[swapIndex] = tmp;
            }
        }
        public static void ShuffleInPlace<T>(this T[] array, IRandomNumberGenerator generator)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (generator == null) throw new ArgumentNullException(nameof(generator));

            for (int i = array.Length - 1; i > 0; i--)
            {
                // Swap element "i" with a random earlier element it (or itself)
                int swapIndex = generator.GetRandomInt32(i + 1);
                T tmp = array[i];
                array[i] = array[swapIndex];
                array[swapIndex] = tmp;
            }
        }


        /// <summary>
        /// Creates a light weight PRNG from a Pooled Generator.
        /// </summary>
        public static IRandomNumberGenerator CreateCypherBasedGenerator(this PooledEntropyCprngGenerator pooledRng) => CreateCypherBasedGenerator(pooledRng, 1024);
        /// <summary>
        /// Creates a light weight PRNG from a Pooled Generator.
        /// </summary>
        public static IRandomNumberGenerator CreateCypherBasedGenerator(this PooledEntropyCprngGenerator pooledRng, int bufferSize)
        {
            var key = pooledRng.GetRandomBytes(32);
            var result = CypherBasedPrngGenerator.Create(key, outputBufferSize: bufferSize);
            return result;
        }
    }
}
