using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.Generator
{
    /// <summary>
    /// Interface to any source of random numbers based on filling byte arrays.
    /// </summary>
    public interface IRandomNumberGenerator
    {
        /// <summary>
        /// Maximum number of bytes which can be requested from the generator in one call.
        /// </summary>
        int MaxRequestBytes { get; }

        /// <summary>
        /// Fills the array with random bytes.
        /// </summary>
        /// <param name="toFill">Array of bytes to overwrite with random data. Between 0 and MaxRequestBytes in size.</param>
        void FillWithRandomBytes(byte[] toFill);

        /// <summary>
        /// Fills the array with count random bytes at the specified offset.
        /// </summary>
        /// <param name="toFill">Array of bytes to overwrite with random data.</param>
        /// <param name="offset">Offset into toFill to write to.</param>
        /// <param name="count">Number of bytes to write, between 0 and MaxRequestBytes in size.</param>
        void FillWithRandomBytes(byte[] toFill, int offset, int count);
    }

    /// <summary>
    /// Interface which adds Dispose() to IRandomNumberGenerator.
    /// </summary>
    public interface IDisposableRandomNumberGenerator : IRandomNumberGenerator, IDisposable { }

    /// <summary>
    /// Interface which builds on the standard source of random numbers by allowed additional seed material to be added.
    /// </summary>
    public interface IReseedableRandomNumberGenerator : IDisposableRandomNumberGenerator, IDisposable
    {
        /// <summary>
        /// Adds additional seed material to the random number generator.
        /// Existing material may be discarded or added to.
        /// </summary>
        void Reseed(byte[] newSeed);
    }
}
