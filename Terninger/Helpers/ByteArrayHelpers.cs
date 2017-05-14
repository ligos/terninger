using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public static byte[] ParseFromHexString(this string s)
        {
            var result = new byte[s.Length / 2];
            for (int i = 0; i < s.Length / 2; i++)
            {
                result[i] = Byte.Parse(s.Substring(i * 2, 2));
            }
            return result;
        }
    }
}
