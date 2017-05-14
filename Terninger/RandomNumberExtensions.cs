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

        public static bool GetRandomBoolean(this IRandomNumberGenerator generator)
        {
            throw new NotImplementedException();
        }
        public static int GetRandomInt32(this IRandomNumberGenerator generator)
        {
            throw new NotImplementedException();
        }
        public static long GetRandomInt64(this IRandomNumberGenerator generator)
        {
            throw new NotImplementedException();
        }
        public static float GetRandomSingle(this IRandomNumberGenerator generator)
        {
            throw new NotImplementedException();
        }
        public static double GetRandomDouble(this IRandomNumberGenerator generator)
        {
            throw new NotImplementedException();
        }

        public static RandomByteStream GetRandomStream(this IRandomNumberGenerator generator)
        {
            return new RandomByteStream(generator);
        }
    }
}
