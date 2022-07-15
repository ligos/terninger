using System;
using System.Threading.Tasks;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.Helpers;

namespace MurrayGrant.Terninger.EntropySources.Local
{
    /// <summary>
    /// An entropy source which uses RandomNumberGenerator.Create().
    /// </summary>
    [AsyncHint(IsAsync.Never)]
    public class CryptoRandomSource : IEntropySource
    {
        private RandomNumberGenerator _Rng;
        private int _ResultLength;

        public string Name { get; set; }

        public CryptoRandomSource(Configuration config) 
            : this(
                resultLength: config?.SampleSize ?? Configuration.Default.SampleSize
            ) 
        { }

        public CryptoRandomSource(int resultLength = 16, RandomNumberGenerator rng = null)
        {
            this._ResultLength = Math.Max(4, Math.Min(64 * 1024, resultLength));
            this._Rng = rng ?? RandomNumberGenerator.Create();
        }

        public void Dispose()
        {
            _Rng.TryDispose();
        }

        public Task<byte[]> GetEntropyAsync(EntropyPriority priority)
        {
            // When in high priority, we return double the normal amount.
            var length = (priority == EntropyPriority.High ? _ResultLength * 2 : _ResultLength);
            var result = new byte[length];
            _Rng.GetBytes(result);
            return Task.FromResult(result);
        }

        public class Configuration
        {
            public static readonly Configuration Default = new Configuration();

            /// <summary>
            /// // Bytes returned each time source is sampled. Default: 16, Minimum: 4, Maximum: 64k
            /// </summary>
            public int SampleSize { get; set; } = 16;
        }
    }
}
