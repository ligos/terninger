using System;
using SysRand = System.Random;


namespace MurrayGrant.Terninger.Random
{
    /// <summary>
    /// Wrapper for IRandomNumberGenerator around Random.
    /// </summary>
    public sealed class StandardRandomWrapperGenerator : IRandomNumberGenerator
    {
        private readonly SysRand _Rng;

        public StandardRandomWrapperGenerator() : this(new SysRand()) { }
        public StandardRandomWrapperGenerator(SysRand rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            _Rng = rng;
        }

        public void Dispose()
        {
        }

        public static IRandomNumberGenerator StockRandom() => new StandardRandomWrapperGenerator(new SysRand());
        public static IRandomNumberGenerator StockRandom(int seed) => new StandardRandomWrapperGenerator(new SysRand(seed));

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
