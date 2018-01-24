using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.Helpers
{
    public static class ConvertersToInt64
    {
        public static IEnumerable<long> ToLongs(this IPAddress ip)
        {
            if (ip == null)
                return Enumerable.Empty<long>();
            else
                return ip.GetAddressBytes().ToLongs();
        }

        public static IEnumerable<long> ToLongs(this byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                yield break;

            for (int i = 0; i < bytes.Length; i += 8)
            {
                if (bytes.Length > i+8)
                    // More than 8 bytes left: grab a chunk.
                    yield return BitConverter.ToInt64(bytes, i);
                else
                {
                    // Less than 8 bytes left: work through byte by byte.
                    var result = 0L;
                    for (int j = 0; j+i < bytes.Length; j++)
                    {
                        result = result | (((long)bytes[j+i]) << j*8);
                    }
                    yield return result;
                }
            }
        }

        public static IEnumerable<long> ToLongs(this string s)
        {
            if (s == null || s == "")
                return Enumerable.Empty<long>();
            else
                return Encoding.UTF8.GetBytes(s).ToLongs();
        }
    }
}
