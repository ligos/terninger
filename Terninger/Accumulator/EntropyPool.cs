﻿using System;
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
        private static readonly int MaxSourcesToCount = 256;        // After this many sources, we don't bother with the single source rule.

        private readonly HashAlgorithm _Hash;
        private readonly int _SingleSourceCountAppliesFrom;
        private readonly int _QuarterHashLengthInBytes;
        private readonly double _MaxSingleSourceRatio;
        private readonly Dictionary<IEntropySource, int> _CountOfBytesBySource = new Dictionary<IEntropySource, int>(EntropySourceComparer.Value);

        // Counters so we know how much entropy has accumulated in this pool.
        public Int128 TotalEntropyBytes { get; private set; }
        public Int128 EntropyBytesSinceLastDigest { get; private set; }

        // Default to SHA512 as the hash algorithm, and a 60% limit for a single source.
        public EntropyPool() : this(SHA512.Create(), 0.6) { }
        public EntropyPool(HashAlgorithm hash) : this(hash, 0.6) { }
        public EntropyPool(HashAlgorithm hash, double maxSingleSourceRatio)
        {
            if (hash == null) throw new ArgumentNullException(nameof(hash));
            if (maxSingleSourceRatio < 0 || maxSingleSourceRatio > 1.0) throw new ArgumentOutOfRangeException(nameof(maxSingleSourceRatio), "Max Single Source Ratio must be between 0 and 1.0.");
            _Hash = hash;
            _QuarterHashLengthInBytes = ((hash.HashSize / 8) / 4);
            _SingleSourceCountAppliesFrom = _QuarterHashLengthInBytes * 3;        // Minimum threshold for single source rule to apply (75% of hash size).
            _MaxSingleSourceRatio = maxSingleSourceRatio;       // Defaults to 60% (0.6).
            if (_MaxSingleSourceRatio == 0.0)
                _MaxSingleSourceRatio = 1.0;        // 0 means nothing could be accumulated, so interpert it as 1.0, which means the single source rule is not enforced.
        }

        /// <summary>
        /// Adds a single entropy event to the pool.
        /// </summary>
        public void Add(EntropyEvent e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (e.Entropy == null) throw new ArgumentNullException(nameof(e.Entropy));
            if (e.Source == null) throw new ArgumentNullException(nameof(e.Source));
            Add(e.Entropy, e.Source);
        }
        internal void Add(byte[] entropy, IEntropySource source)
        {
            // Determine how much of the packet will be accepted.
            _CountOfBytesBySource.TryGetValue(source, out var countFromSource);
            var bytesToaccept = BytesToAcceptFromSource(entropy.Length, countFromSource);

            if (bytesToaccept <= 0)
                // Ignoring this packet entirely.
                return;
            
            // Accumulate the packer into the hash function.
            // Note that this may only incorporate part of the packet.
            _Hash.TransformBlock(entropy, 0, bytesToaccept, null, 0);

            // Increment counters. Note that this may overflow for very long lived pools.
            if (_CountOfBytesBySource.Count <= MaxSourcesToCount && countFromSource < Int32.MaxValue)
            {
                try
                {
                    _CountOfBytesBySource[source] = countFromSource + bytesToaccept;
                }
                catch (OverflowException)
                {
                    _CountOfBytesBySource[source] = Int32.MaxValue;
                }
            }
            TotalEntropyBytes = TotalEntropyBytes + bytesToaccept;
            EntropyBytesSinceLastDigest = EntropyBytesSinceLastDigest + bytesToaccept;
        }

        /// <summary>
        /// Get a copy of the current count of bytes by source.
        /// </summary>
        public IReadOnlyDictionary<IEntropySource, int> GetCountOfBytesBySource() 
            => new Dictionary<IEntropySource, int>(_CountOfBytesBySource, EntropySourceComparer.Value);

        private int BytesToAcceptFromSource(int byteCount, int countFromSource)
        {
            // We may not accept all (or any) bytes from an entropy packet.
            // The goal is to prevent any single source instance from dominating the pool, 
            // which could an attacker to guess or influence the state of the overall generator.
            // By default, the threshold is set to 60% - so no source should have more than 60% of bytes accumulated in a pool.
            
            // When minimal entropy has been received, we accept everything.
            if (EntropyBytesSinceLastDigest < _SingleSourceCountAppliesFrom)
                return byteCount;
            // If we have a stack of sources, we accept everything.
            if (_CountOfBytesBySource.Count > MaxSourcesToCount)
                return byteCount;

            // Problem: if you interleave 2 sources with equal sizes, this "deadlocks" - both end up with an equal number of bytes and neither can add more.
            // As neither is really overwhelming the source, this should be allowed.
            // Solution: if all sources are within half the hash length bytes of each other, we allow up to half of the hash size extra.
            var halfHashLength = _QuarterHashLengthInBytes * 2;
            var allowExtraBytes = _CountOfBytesBySource.Count > 1 && (_CountOfBytesBySource.Values.Max() - _CountOfBytesBySource.Values.Min() <= halfHashLength);
            var extraAllowance = allowExtraBytes ? halfHashLength : 0;

            // Otherwise, we enforce the source ratio.
            var maxBytesAllowed = (int)(
                                    (EntropyBytesSinceLastDigest > Int32.MaxValue ? Int32.MaxValue : (int)EntropyBytesSinceLastDigest) 
                                    * _MaxSingleSourceRatio
                                )
                                + extraAllowance;
            var result = maxBytesAllowed - countFromSource;
            if (result < 0)
                return 0;
            else
                return Math.Min(byteCount, result);
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
            _CountOfBytesBySource.Clear();
            return _Hash.Hash;
        }
    }
}


