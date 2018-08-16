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
        private static readonly int MaxSourcesToCount = 256;        // After this many sources, we don't bother with the single source rule.

        // Hash Algorithm support is a bit of a mess.
        //  - net452 only has HashAlgorithm, IncrementalHash is first available in net471.
        //  - netstandard1.3 has HashAlgorithm, but not TransformFinalBlock(), you must use IncrementalHash.
        //  - netstandard2.0 has both.
        //  - Both look to be getting support for Span<T>.
        //  - Most 3rd party has functions use another interface altogether (eg: System.Data.HashFunctions)
        //  - HashAlgorithm tells you the size of the hash (which we use), IncrementalHash doesn't.
        //  - In box IncrementalHash seems to be limited to the list in HashAlgorithmName (MD5, SHA1, SHA2 family); and isn't obviously inheritable.
        // All in all, we sort the mess out in the constructor, and slightly prefer IncrementalHash.
        // TODO: abstract this all out into some better interface.

#if (NETSTANDARD1_3 || NETSTANDARD2_0)
        private readonly IncrementalHash _IncHash;
#endif
        private readonly HashAlgorithm _HashAlg;

        private readonly int _SingleSourceCountAppliesFrom;
        private readonly int _HashLengthInBytes;
        private readonly int _QuarterHashLengthInBytes;
        private readonly double _MaxSingleSourceRatio;
        private readonly Dictionary<IEntropySource, int> _CountOfBytesBySource = new Dictionary<IEntropySource, int>(EntropySourceComparer.Value);

        // Counters so we know how much entropy has accumulated in this pool.
        public Int128 TotalEntropyBytes { get; private set; }
        public Int128 EntropyBytesSinceLastDigest { get; private set; }

        // Default to SHA512 as the hash algorithm, and a 60% limit for a single source.
#if (NETSTANDARD1_3 || NETSTANDARD2_0)
        public EntropyPool() :
            this(IncrementalHash.CreateHash(HashAlgorithmName.SHA512), 0.6) { }
        public EntropyPool(IncrementalHash hash) : this(hash, 0.6) { }
        public EntropyPool(IncrementalHash hash, double maxSingleSourceRatio)
        {
            if (hash == null) throw new ArgumentNullException(nameof(hash));
            if (maxSingleSourceRatio < 0 || maxSingleSourceRatio > 1.0) throw new ArgumentOutOfRangeException(nameof(maxSingleSourceRatio), "Max Single Source Ratio must be between 0 and 1.0.");

            // IncrementalHash doesn't tell us how big its output is, so we just hash an empty array to find out.
            hash.AppendData(new byte[64]);
            var hashSize = hash.GetHashAndReset().Length;
            _IncHash = hash;

            // Identical to HashAlgorithm ctor below.
            _HashLengthInBytes = hashSize;
            _QuarterHashLengthInBytes = (hashSize / 4);
            _SingleSourceCountAppliesFrom = _QuarterHashLengthInBytes * 3;        // Minimum threshold for single source rule to apply (75% of hash size).
            _MaxSingleSourceRatio = maxSingleSourceRatio;       // Defaults to 60% (0.6).
            if (_MaxSingleSourceRatio == 0.0)
                _MaxSingleSourceRatio = 1.0;        // 0 means nothing could be accumulated, so interpert it as 1.0, which means the single source rule is not enforced.
        }
#else
        public EntropyPool() :      // Only for net452, otherwise we prefer the IncrementalHash version.
            this(SHA512.Create(), 0.6) { }
#endif
        public EntropyPool(HashAlgorithm hash) : this(hash, 0.6) { }
        public EntropyPool(HashAlgorithm hash, double maxSingleSourceRatio)
        {
            if (hash == null) throw new ArgumentNullException(nameof(hash));
            if (maxSingleSourceRatio < 0 || maxSingleSourceRatio > 1.0) throw new ArgumentOutOfRangeException(nameof(maxSingleSourceRatio), "Max Single Source Ratio must be between 0 and 1.0.");

            // We slightly prefer IncrementalHash, but there seems to be a closed set of algorithms in-box.
            var hashSize = hash.HashSize / 8;
#if NET452
            _HashAlg = hash;
#else
            var hashName = hash.GetType().FullName;
            _IncHash = GetIncrementalHashOrThrow(hashName);
            if (_IncHash == null)
                _HashAlg = hash;        // Fallback if unknown HashAgorithm.
#endif

            // Identical to HashAlgorithm ctor below.
            _HashLengthInBytes = hashSize;
            _QuarterHashLengthInBytes = hashSize / 4;
            _SingleSourceCountAppliesFrom = _QuarterHashLengthInBytes * 3;        // Minimum threshold for single source rule to apply (75% of hash size).
            _MaxSingleSourceRatio = maxSingleSourceRatio;       // Defaults to 60% (0.6).
            if (_MaxSingleSourceRatio == 0.0)
                _MaxSingleSourceRatio = 1.0;        // 0 means nothing could be accumulated, so interpert it as 1.0, which means the single source rule is not enforced.        
        }

#if (NETSTANDARD1_3 || NETSTANDARD2_0)
        private IncrementalHash GetIncrementalHashOrThrow(string hashAlgorithmClassName)
        {
            if (hashAlgorithmClassName.Contains("SHA512"))
                return IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
            else if (hashAlgorithmClassName.Contains("SHA256"))
                return IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            else if (hashAlgorithmClassName.Contains("SHA384"))
                return IncrementalHash.CreateHash(HashAlgorithmName.SHA384);
            else if (hashAlgorithmClassName.Contains("SHA1"))
                return IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            else if (hashAlgorithmClassName.Contains("MD5"))
                return IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            else
    #if NETSTANDARD1_3
                // No IncrementalHash support: not supported.
                throw new ArgumentException($"HashAlgorithm based class '{hashAlgorithmClassName}' is not supported in netstandard1.3, use netstandard2.0 or net452.");
    #else
                // No IncrementalHash support: we'll use HashAlgorithm instead.
                return null;
    #endif
        }
#endif

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
            AccumulateBlock(entropy, bytesToaccept);

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

        private void AccumulateBlock(byte[] entropy, int length)
        {
            // See above for hash algorithm mess.
#if (NETSTANDARD2_0 || NETSTANDARD1_3)
            if (_IncHash != null)
                _IncHash.AppendData(entropy, 0, length);
#endif
#if (NETSTANDARD2_0 || NET452)
            if (_HashAlg != null)
                _HashAlg.TransformBlock(entropy, 0, length, null, 0);
#endif
        }

        /// <summary>
        /// Gets a hash digest of the entropy which has been accumulated.
        /// Note: if no entropy has accumulated, the result is deterministic.
        /// </summary>
        public byte[] GetDigest()
        {
            byte[] result = null;

            // See above for hash algorithm mess.
#if (NETSTANDARD2_0 || NETSTANDARD1_3)
            if (_IncHash != null)
                result = _IncHash.GetHashAndReset();
#endif
#if (NETSTANDARD2_0 || NET452)
            if (_HashAlg != null)
            {
                // As the final block needs some input, we use part of the total entropy counter.
                _HashAlg.TransformFinalBlock(BitConverter.GetBytes(TotalEntropyBytes.Low), 0, 8);
                result = _HashAlg.Hash;
            }
#endif

            if (result == null) ThrowGetDigestResultIsNull();
            if (result.Length != _HashLengthInBytes) ThrowDigestLengthIsWrong(result);

            EntropyBytesSinceLastDigest = Int128.Zero;
            _CountOfBytesBySource.Clear();
            return result;
        }

        private void ThrowGetDigestResultIsNull() => throw new Exception("Unable to set result in GetDigest() - internal assertion failure.");
        private void ThrowDigestLengthIsWrong(byte[] result) => throw new Exception($"Result length is wrong in GetDigest() - expected length = {_HashLengthInBytes}, actual = {result.Length}.");
    }
}


