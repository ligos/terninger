using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace MurrayGrant.Terninger.CryptoPrimitives
{
    /// <summary>
    /// Shortcut constructors / factories for various ICryptoPrimitive instances.
    /// </summary>
    public static class CryptoPrimitive
    {
        public static ICryptoPrimitive Aes256()
        {
            var aes = Aes.Create();
            aes.KeySize = 256;
            return new BlockCypherCryptoPrimitive(aes);
        }
        public static ICryptoPrimitive Aes128()
        {
            var aes = Aes.Create();
            aes.KeySize = 128;
            return new BlockCypherCryptoPrimitive(aes);
        }

        public static ICryptoPrimitive HmacSha256() => new HmacCryptoPrimitive(() => new HMACSHA256(new byte[32]));
        public static ICryptoPrimitive HmacSha512() => new HmacCryptoPrimitive(() => new HMACSHA512(new byte[64]));

        public static ICryptoPrimitive Sha256() => new HashCryptoPrimitive(SHA256.Create());
        public static ICryptoPrimitive Sha512() => new HashCryptoPrimitive(SHA512.Create());

#if NETSTANDARD2_0 || NET452
        public static ICryptoPrimitive Aes256Managed() => new BlockCypherCryptoPrimitive(new AesManaged() { KeySize = 256 });
        public static ICryptoPrimitive Aes128Managed() => new BlockCypherCryptoPrimitive(new AesManaged() { KeySize = 128 });

        public static ICryptoPrimitive RijndaelManaged(int keyBits, int blockBits) => new BlockCypherCryptoPrimitive(new RijndaelManaged() { KeySize = keyBits, BlockSize = blockBits });

        public static ICryptoPrimitive Sha256Managed() => new HashCryptoPrimitive(new SHA256Managed());
        public static ICryptoPrimitive Sha512Managed() => new HashCryptoPrimitive(new SHA512Managed());
#endif
    }
}
