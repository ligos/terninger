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
    /// An entropy source which uses RandomNumberGenerator.Create().
    /// </summary>
    public class CryptoRandomSource : IEntropySource
    {
        private RandomNumberGenerator _Rng;
        private int _ResultLength;

        public string Name => typeof(CryptoRandomSource).FullName;

        public CryptoRandomSource() : this(16) { }
        public CryptoRandomSource(int resultLength)
        {
            this._ResultLength = resultLength;
        }

        public void Dispose()
        {
            _Rng.TryDispose();
        }

        public Task<EntropySourceInitialisationResult> Initialise(IEntropySourceConfig config, Func<IRandomNumberGenerator> prngFactory)
        {
            if (config.IsTruthy("CryptoRandomSource.Enabled") == false)
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.DisabledByConfig, "CryptoRandomSource has been disabled in entropy source configuration."));

            config.TryParseAndSetInt32("CryptoRandomSource.ResultLength", ref _ResultLength);
            if (_ResultLength < 4 || _ResultLength > 1024)
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, new ArgumentOutOfRangeException("CryptoRandomSource.ResultLength", _ResultLength, "Config item CryptoRandomSource.ResultLength must be between 4 and 1024")));

            _Rng = RandomNumberGenerator.Create();

            return Task.FromResult(EntropySourceInitialisationResult.Successful());
        }

        public Task<byte[]> GetEntropyAsync()
        {
            var result = new byte[_ResultLength];
            _Rng.GetBytes(result);
            return Task.FromResult(result);
        }
    }
}
