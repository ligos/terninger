using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

using BigMath;

using MurrayGrant.Terninger.EntropySources;

namespace MurrayGrant.Terninger.Accumulator
{
    /// <summary>
    /// A pool of accumulated entropy, as defined in 9.5.2 of Fortuna spec.
    /// </summary>
    public sealed class EntropyPool
    {
        private readonly HashAlgorithm _Hash;

        // Counters so we know how much entropy has accumulated in this pool.
        public Int128 TotalEntropyBytes { get; private set; }
        public Int128 EntropyBytesSinceLastDigest { get; private set; }

        // Default to SHA512 as the hash algorithm.
        public EntropyPool() : this(SHA512.Create()) { }
        public EntropyPool(HashAlgorithm hash)
        {
            if (hash == null) throw new ArgumentNullException(nameof(hash));
            _Hash = hash;
        }

        /// <summary>
        /// Adds a single entropy event to the pool.
        /// </summary>
        public void Add(EntropyEvent e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (e.Entropy == null) throw new ArgumentNullException(nameof(e.Entropy));
            if (e.Source == null) throw new ArgumentNullException(nameof(e.Source));

            // TODO: track the source to prevent any single source dominating this pool.
            //  Need to be careful when few sources in play or minimal entropy has been added.

            // Accumulate this event in the hash function.
            _Hash.TransformBlock(e.Entropy, 0, e.Entropy.Length, null, 0);

            // Increment counters.
            TotalEntropyBytes = TotalEntropyBytes + e.Entropy.Length;
            EntropyBytesSinceLastDigest = EntropyBytesSinceLastDigest + e.Entropy.Length;
        }

        /// <summary>
        /// Gets a hash digest of the entropy which has been accumulated.
        /// Note: if no entropy has accumulated, the result is deterministic.
        /// </summary>
        public byte[] GetDigest()
        {
            // As the final block needs some input, we use part of the total entropy counter.
            _Hash.TransformFinalBlock(BitConverter.GetBytes(TotalEntropyBytes.Low), 0, 8);
            EntropyBytesSinceLastDigest = Int128.Zero;
            return _Hash.Hash;
        }
    }
}


