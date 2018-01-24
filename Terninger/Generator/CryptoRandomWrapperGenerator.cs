using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.Generator
{
    /// <summary>
    /// Wrapper for IRandomNumberGenerator around RandomNumberGenerator in System.Security.Cryptography namespace.
    /// </summary>
    public sealed class CryptoRandomWrapperGenerator : IRandomNumberGenerator
    {
        private readonly RandomNumberGenerator _Rng;

        public CryptoRandomWrapperGenerator() : this(RandomNumberGenerator.Create()) { }
        public CryptoRandomWrapperGenerator(RandomNumberGenerator rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            _Rng = rng;
        }

        public int MaxRequestBytes => Int32.MaxValue;

        public void FillWithRandomBytes(byte[] toFill)
        {
            _Rng.GetBytes(toFill);
        }

        public void FillWithRandomBytes(byte[] toFill, int offset, int count)
        {
            var buf = new byte[count];
            _Rng.GetBytes(buf);
            Buffer.BlockCopy(buf, 0, toFill, offset, count);
        }
    }
}
