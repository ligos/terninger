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
        public static ICryptoPrimitive Aes256Managed() => new BlockCypherCryptoPrimitive(new AesManaged() { KeySize = 256 });
        public static ICryptoPrimitive Aes128Managed() => new BlockCypherCryptoPrimitive(new AesManaged() { KeySize = 128 });

        public static ICryptoPrimitive RijndaelManaged(int keyBits, int blockBits) => new BlockCypherCryptoPrimitive(new RijndaelManaged() { KeySize = keyBits, BlockSize = blockBits });
    }
}
