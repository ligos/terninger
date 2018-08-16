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
        public static ICryptoPrimitive Aes256() => BlockCypherCryptoPrimitive.Aes256();
        public static ICryptoPrimitive Aes128() => BlockCypherCryptoPrimitive.Aes128();

        public static ICryptoPrimitive HmacSha256() => HmacCryptoPrimitive.HmacSha256();
        public static ICryptoPrimitive HmacSha512() => HmacCryptoPrimitive.HmacSha512();

        public static ICryptoPrimitive Sha256() => HashCryptoPrimitive.Sha256();
        public static ICryptoPrimitive Sha512() => HashCryptoPrimitive.Sha512();

#if NETSTANDARD2_0 || NET452
        public static ICryptoPrimitive Aes256Managed() => new BlockCypherCryptoPrimitive(new AesManaged() { KeySize = 256 });
        public static ICryptoPrimitive Aes128Managed() => new BlockCypherCryptoPrimitive(new AesManaged() { KeySize = 128 });

        public static ICryptoPrimitive RijndaelManaged(int keyBits, int blockBits) => new BlockCypherCryptoPrimitive(new RijndaelManaged() { KeySize = keyBits, BlockSize = blockBits });

        public static ICryptoPrimitive Sha256Managed() => new HashCryptoPrimitive(new SHA256Managed());
        public static ICryptoPrimitive Sha512Managed() => new HashCryptoPrimitive(new SHA512Managed());
#endif
    }
}
