using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.Helpers;

namespace MurrayGrant.Terninger.EntropySources.Local
{
    /// <summary>
    /// An entropy source which uses RandomNumberGenerator.Create().
    /// </summary>
    public class CryptoRandomSource : IEntropySource
    {
        private RandomNumberGenerator _Rng;
        private int _ResultLength;

        public string Name { get; set; }

        public CryptoRandomSource() : this(16, RandomNumberGenerator.Create()) { }
        public CryptoRandomSource(int resultLength) : this(resultLength, RandomNumberGenerator.Create()) { }
        public CryptoRandomSource(int resultLength, RandomNumberGenerator rng)
        {
            this._ResultLength = resultLength;
            this._Rng = rng;
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
    }
}
