using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.Generator
{
    /// <summary>
    /// Wrapper for IRandomNumberGenerator around Random.
    /// </summary>
    public sealed class StandardRandomWrapperGenerator : IRandomNumberGenerator
    {
        private readonly Random _Rng;

        public StandardRandomWrapperGenerator() : this(new Random()) { }
        public StandardRandomWrapperGenerator(Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            _Rng = rng;
        }

        // TODO: would be nice to have a light-weight PRNG which accepts a 128 bit seed.

        public static IRandomNumberGenerator StockRandom() => new StandardRandomWrapperGenerator(new Random());
        public static IRandomNumberGenerator StockRandom(int seed) => new StandardRandomWrapperGenerator(new Random(seed));

        public int MaxRequestBytes => Int32.MaxValue;

        public void FillWithRandomBytes(byte[] toFill)
        {
            _Rng.NextBytes(toFill);
        }

        public void FillWithRandomBytes(byte[] toFill, int offset, int count)
        {
            var buf = new byte[count];
            _Rng.NextBytes(buf);
            Buffer.BlockCopy(buf, 0, toFill, offset, count);
        }
    }
}
