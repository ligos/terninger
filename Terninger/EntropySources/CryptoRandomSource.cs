using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.Helpers;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// An entropy source which wraps RNGCryptoServiceProvider.
    /// </summary>
    public class CryptoRandomSource : IEntropySource
    {
        private readonly RNGCryptoServiceProvider _rng;
        public string Name => typeof(CryptoRandomSource).FullName;


        public CryptoRandomSource()
        {
            _rng = new RNGCryptoServiceProvider();
        }

        public void Dispose()
        {
            _rng.TryDispose();
        }

        public Task<EntropySourceInitialisationResult> Initialise()
        {
            return Task.FromResult(EntropySourceInitialisationResult.Successful);
        }

        public Task<byte[]> GetEntropyAsync()
        {
            var result = new byte[16];
            _rng.GetBytes(result);
            return Task.FromResult(result);
        }
    }
}
