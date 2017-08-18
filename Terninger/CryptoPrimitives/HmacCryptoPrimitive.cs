using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace MurrayGrant.Terninger.CryptoPrimitives
{
    /// <summary>
    /// Container for an HMAC - that is a hash plus a key.
    /// </summary>
    public class HmacCryptoPrimitive : ICryptoPrimitive
    {
        // As an HMAC cannot be re-keyed, we recreate it completely on every re-key event.
        private Func<HMAC> _HmacCreator;
        private HMAC _Hmac;

        public HmacCryptoPrimitive(Func<HMAC> hmacCreator)
        {
            if (hmacCreator == null)
                throw new ArgumentNullException(nameof(hmacCreator));
            this._HmacCreator = hmacCreator;
            this._Hmac = hmacCreator();
            if (_Hmac == null)
                throw new InvalidOperationException("HMAC Creator returned null HMAC object.");
        }

        public void Dispose()
        {
            DisposeHmac();
            _HmacCreator = null;
        }
        private void DisposeHmac()
        {
            if (_Hmac != null)
            {
                Array.Clear(_Hmac.Key, 0, _Hmac.Key.Length);
                _Hmac.Dispose();
                _Hmac = null;
            }

        }

        public string Name => _Hmac.GetType().Name;

        public int KeySizeBytes => _Hmac.Key.Length;
        public int BlockSizeBytes => _Hmac.HashSize / 8;

        public byte[] Key
        {
            get => _Hmac.Key;
            set
            {
                // Destroy the previous hmac.
                DisposeHmac();
                
                // As an HMAC cannot be re-keyed after it is first used, we recreate it completely on every re-key event.
                var hmac = _HmacCreator();
                hmac.Key = value;
                _Hmac = hmac;
            }
        }
        
        public ICryptoTransform CreateEncryptor() => _Hmac;
    }
}
