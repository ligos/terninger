using System;
using System.Security.Cryptography;

namespace MurrayGrant.Terninger.CryptoPrimitives
{
    /// <summary>
    /// Container for a SymmetricAlgorithm with a key.
    /// </summary>
    public class BlockCypherCryptoPrimitive : ICryptoPrimitive
    {
        private readonly SymmetricAlgorithm _Cypher;

        public static ICryptoPrimitive Aes256()
        {
            var aes = Aes.Create();
            aes.KeySize = 256;
            return new BlockCypherCryptoPrimitive(aes);
        }
        public static ICryptoPrimitive Aes128()
        {
            var aes = Aes.Create();
            aes.KeySize = 128;
            return new BlockCypherCryptoPrimitive(aes);
        }

        public BlockCypherCryptoPrimitive(SymmetricAlgorithm cypher)
        {
            if (cypher == null)
                throw new ArgumentNullException(nameof(cypher));
            this._Cypher = cypher;
            this._Cypher.IV = new byte[BlockSizeBytes];
        }

        public void Dispose()
        {
            Array.Clear(_Cypher.Key, 0, _Cypher.Key.Length);
            _Cypher.Dispose();
        }

        public string Name => _Cypher.GetType().Name + " " + KeySizeBytes.ToString();

        public int KeySizeBytes => _Cypher.KeySize / 8;
        public int BlockSizeBytes => _Cypher.BlockSize / 8;

        public byte[] Key
        {
            get => _Cypher.Key;
            set => _Cypher.Key = value;
        }
        
        public ICryptoTransform CreateEncryptor() => _Cypher.CreateEncryptor();
        
    }
}
