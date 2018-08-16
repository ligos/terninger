using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace MurrayGrant.Terninger.CryptoPrimitives
{
    /// <summary>
    /// Interface to various encryption and hash algorithms.
    /// </summary>
    public interface ICryptoPrimitive : IDisposable
    {
        string Name { get; }

        int KeySizeBytes { get; }
        int BlockSizeBytes { get; }

        byte[] Key { get; set; }

        ICryptoTransform CreateEncryptor();
    }
}
