using MurrayGrant.Terninger.CryptoPrimitives;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace MurrayGrant.Terninger.Test
{
    public static class NativeCryptoPrimitives
    {
        internal static ICryptoPrimitive GetAes256Csp() => new BlockCypherCryptoPrimitive(new AesCryptoServiceProvider() { KeySize = 256 });
        internal static ICryptoPrimitive GetAes128Csp() => new BlockCypherCryptoPrimitive(new AesCryptoServiceProvider() { KeySize = 128 });
        internal static ICryptoPrimitive GetAes256Cng() => throw new NotImplementedException();
        internal static ICryptoPrimitive GetAes128Cng() => throw new NotImplementedException();

        internal static ICryptoPrimitive GetSha256Csp() => throw new NotImplementedException();
        internal static ICryptoPrimitive GetSha512Csp() => throw new NotImplementedException();
#if NET452
        internal static ICryptoPrimitive GetSha256Cng() => new HashCryptoPrimitive(new SHA256Cng());
        internal static ICryptoPrimitive GetSha512Cng() => new HashCryptoPrimitive(new SHA512Cng());
#else
        internal static ICryptoPrimitive GetSha256Cng() => throw new NotImplementedException();
        internal static ICryptoPrimitive GetSha512Cng() => throw new NotImplementedException();
#endif

    }
}
