using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace MurrayGrant.Terninger.CryptoPrimitives
{
    /// <summary>
    /// Container for a SymmetricAlgorithm with a key.
    /// </summary>
    public class BlockCypherCryptoPrimitive : ICryptoPrimitive
    {
        private readonly SymmetricAlgorithm _Cypher;

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

        public static BlockCypherCryptoPrimitive Aes256() => new BlockCypherCryptoPrimitive(Aes.Create());
        public static BlockCypherCryptoPrimitive Aes256Managed() => new BlockCypherCryptoPrimitive(new AesManaged() { KeySize = 256 });
        public static BlockCypherCryptoPrimitive Aes128Managed() => new BlockCypherCryptoPrimitive(new AesManaged() { KeySize = 128 });

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
