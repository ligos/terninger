using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.Random;

using BigMath;
using BigMath.Utils;

namespace MurrayGrant.Terninger.Accumulator
{
    /// <summary>
    /// Entropy Accumulator as defined in section 9.5.
    /// Consists of a number of EntropyPool objects, and methods to add entropy and derive new seed material.
    /// </summary>
    public sealed class EntropyAccumulator
    {
        // As specified in 9.5.2, up to 64 pools which are used by the _PoolIndex counter.
        // These serve as a guaranteed long term pool on entropy.
        // Smaller indexes are used more frequently than larger indexes (multiple times per second for the former, days, months or years for the latter).
        private readonly EntropyPool[] _LinearPools;
        private ulong _ReseedCount;            // Used to determine which of the linear pools will be used when generating a key.

        // Dodis et al proposes distributing entropy using a PRNG in sections 5.2 and 6.3 of https://eprint.iacr.org/2014/167.pdf, that is in Add().
        // Mostly because I can't understand their math, Turninger maintains a separate set of pools instead, which are drawn from at random in NextSeed().
        // This improves performance against an attacker injecting a constant rate of entropy known to them.
        private readonly EntropyPool[] _RandomPools;
        private readonly IRandomNumberGenerator _Rng;       // Used to derive which of the random pools will be used when generating a key.
        private const int _RandomFactor = 2;                // Random selection chances is 1/2^(this+1)
        // To simplify adding, this is a flat array of all linear and random pools.
        private readonly EntropyPool[] _AllPools;
        private int _PoolIndex;
        // Incoming entropy is broken into chunks at least this long, so that large entropy packets (eg: 1kB) are distributed more evenly.
        private const int _ChunkSize = 16;      // TODO: allow this to be configurable.

        // Counters and totals to see how much entropy is in the accumulator.

        /// <summary>
        /// The total entropy accumulated over the entire life of this instance.
        /// </summary>
        public Int128 TotalEntropyBytes => this._AllPools.Sum(p => p.TotalEntropyBytes);

        /// <summary>
        /// The total available entropy since last seed event.
        /// As it is rare for all pools to be used for a reseed, this rarely reaches zero.
        /// </summary>
        public Int128 AvailableEntropyBytesSinceLastSeed => this._AllPools.Sum(p => p.EntropyBytesSinceLastDigest);

        /// <summary>
        /// The amount of entropy accumulated in pool zero, since last reseed.
        /// </summary>
        public Int128 PoolZeroEntropyBytesSinceLastSeed => this._AllPools[0].EntropyBytesSinceLastDigest;

        /// <summary>
        /// The maximum amount of entropy accumulated in any one pool.
        /// </summary>
        public Int128 MaxPoolEntropyBytesSinceLastSeed => this._AllPools.Max(p => p.EntropyBytesSinceLastDigest);

        /// <summary>
        /// The minimum amount of entropy accumulated in any one pool.
        /// </summary>
        public Int128 MinPoolEntropyBytesSinceLastSeed => this._AllPools.Min(p => p.EntropyBytesSinceLastDigest);

        /// <summary>
        /// The total number of calls to NextSeed().
        /// </summary>
        public Int128 TotalReseedEvents { get; private set; }


        /// <summary>
        /// Bitmask of linear pools used in the last NextSeed() call.
        /// </summary>
        public ulong LinearPoolsUsedInLastSeedGeneration { get; private set; }
        /// <summary>
        /// Bitmask of random pools used in the last NextSeed() call.
        /// </summary>
        public ulong RandomPoolsUsedInLastSeedGeneration { get; private set; }
        /// <summary>
        /// Total number of pools used in the last NextSeed() call.
        /// </summary>
        public int PoolCountUsedInLastSeedGeneration => NumberOfSetBits(LinearPoolsUsedInLastSeedGeneration) + NumberOfSetBits(RandomPoolsUsedInLastSeedGeneration);

        /// <summary>
        /// Total number of pools.
        /// </summary>
        public int TotalPoolCount => _AllPools.Length;
        /// <summary>
        /// Total linear pools.
        /// </summary>
        public int LinearPoolCount => _LinearPools.Length;
        /// <summary>
        /// Total random pools.
        /// </summary>
        public int RandomPoolCount => _RandomPools.Length;


        public EntropyAccumulator() : this(20, 12, CypherBasedPrngGenerator.CreateWithCheapKey(), DefaultHashCreator) { }
        public EntropyAccumulator(IRandomNumberGenerator rng) : this(20, 12, rng, DefaultHashCreator) { }
        public EntropyAccumulator(int linearPoolCount, int randomPoolCount) : this(linearPoolCount, randomPoolCount, CypherBasedPrngGenerator.CreateWithCheapKey(), DefaultHashCreator) { }
        public EntropyAccumulator(int linearPoolCount, int randomPoolCount, IRandomNumberGenerator rng) : this(linearPoolCount, randomPoolCount, rng, DefaultHashCreator) { }
        public EntropyAccumulator(int linearPoolCount, int randomPoolCount, IRandomNumberGenerator rng, Func<HashAlgorithm> hashCreator)
        {
            if (linearPoolCount < 0) throw new ArgumentOutOfRangeException(nameof(linearPoolCount), linearPoolCount, "Zero or more pools are required.");
            if (linearPoolCount > 64) throw new ArgumentOutOfRangeException(nameof(linearPoolCount), linearPoolCount, "A maxium of 64 pools is allowed for linear pools.");
            if (randomPoolCount < 0) throw new ArgumentOutOfRangeException(nameof(randomPoolCount), randomPoolCount, "Zero or more pools are required.");
            if (randomPoolCount > 64) throw new ArgumentOutOfRangeException(nameof(randomPoolCount), randomPoolCount, "A maxium of 64 pools is allowed for random pools.");
            if (linearPoolCount + randomPoolCount < 4) throw new ArgumentOutOfRangeException(nameof(linearPoolCount) + "/" + nameof(randomPoolCount), linearPoolCount + randomPoolCount, "Must have minimum of 4 pools in total.");
            if (hashCreator == null) throw new ArgumentNullException(nameof(hashCreator));
            if (randomPoolCount > 0 && rng == null) throw new ArgumentException("A random number generator is required when using random pools.", nameof(rng));

            // Build the pools.
            _LinearPools = new EntropyPool[linearPoolCount];
            for (int i = 0; i < _LinearPools.Length; i++)
                _LinearPools[i] = new EntropyPool(hashCreator());
            _RandomPools = new EntropyPool[randomPoolCount];
            for (int i = 0; i < _RandomPools.Length; i++)
                _RandomPools[i] = new EntropyPool(hashCreator());
            _AllPools = _LinearPools.Concat(_RandomPools).ToArray();

            TotalReseedEvents = Int128.Zero;
            _ReseedCount = 0UL;     // Start from the first pool!
            _PoolIndex = 0;         // Start adding entropy from the beginning!
            _Rng = rng;
        }
        private static HashAlgorithm DefaultHashCreator() => SHA512.Create();

        /// <summary>
        /// Adds entropy from a particular source to the accumulator.
        /// </summary>
        public void Add(EntropyEvent entropy)
        {
            // Based on Fortunata spec 9.5.6

            // Entropy is added in a round robin fashion.
            // Larger packets are broken up into smaller chunks to be distributed more evenly between pools.
            var poolIndex = _PoolIndex;
            foreach (var e in entropy.ToChunks(_ChunkSize))
            {
                if (poolIndex >= _AllPools.Length)
                    poolIndex = 0;
                _AllPools[poolIndex].Add(e, entropy.Source);
                poolIndex = poolIndex + 1;
            }
            _PoolIndex = poolIndex;
        }

        /// <summary>
        /// Gets the next batch of seed material from the accumulated entropy. 
        /// This will be at least the hash size, but may be up to TotalPools * hash size.
        /// </summary>
        public byte[] NextSeed()
        {
            // Get the number used to determine which pools will be used.
            ulong reseedCount = unchecked(_ReseedCount + 1);

            // Get digests from all the pools to form the final seed.
            var digests = new List<byte[]>();

            // Linear pools.
            var linearPoolUsedMask = GetDigestsFromLinearPools(digests, reseedCount);

            // Random pools.
            var randomPoolUsedMask = GetDigestsFromRandomPools(digests);

            // Flatten the result.
            // PERF: a block copy function will likely be faster.
            var result = digests.SelectMany(x => x).ToArray();

            // Update counters and other properties.
            _ReseedCount = reseedCount;
            TotalReseedEvents = TotalReseedEvents + 1;
            LinearPoolsUsedInLastSeedGeneration = linearPoolUsedMask;
            RandomPoolsUsedInLastSeedGeneration = randomPoolUsedMask;

            return result;
        }

        /// <summary>
        /// Resets any entropy gathered in pool zero.
        /// </summary>
        public void ResetPoolZero()
        {
            // Get the digest from pool zero and discard it.
            _AllPools[0].GetDigest();
        }

        private ulong GetDigestsFromLinearPools(ICollection<byte[]> digests, ulong reseedCount)
        {
            // Based on Fortunata spec 9.5.5
            // Will always add at least one digest from a pool (pool zero).
            ulong linearPoolUsedMask = 0;
            if (_LinearPools.Length > 0)
            {
                for (int i = 0; i < _LinearPools.Length; i++)
                {
                    if (PoolIsUsedLinear(i, reseedCount))
                    {
                        digests.Add(_LinearPools[i].GetDigest());
                        linearPoolUsedMask = linearPoolUsedMask | (1UL << i);
                    }
                }
            }
            return linearPoolUsedMask;
        }
        private ulong GetDigestsFromRandomPools(ICollection<byte[]> digests)
        {
            // Based on Dodis et al sections 5.2 and 6.3.
            ulong randomPoolUsedMask = 0;
            if (_RandomPools.Length > 0)
            {
                // Create a bit mask to determine which pools to draw from.
                ulong randomPoolNumber = 0;
                ulong randomPoolMask = (1UL << (_RandomPools.Length)) - 1;
                var anyDigests = digests.Any();
                do
                {
                    // Chance of random selection is at best 1/2, and may be less, depending on the value of _RandomFactor.
                    randomPoolNumber = _Rng.GetRandomUInt64();
                    for (int i = 0; i < _RandomFactor; i++)
                        randomPoolNumber = randomPoolNumber & _Rng.GetRandomUInt64();

                    // If any random pools are defined, and there isn't already a digest, we must ensure at least one pool is drawn from.
                } while (!anyDigests && (randomPoolNumber & randomPoolMask) == 0);

                // Read from pools.
                for (int i = 0; i < _RandomPools.Length; i++)
                {
                    if (PoolIsUsedRandom(i, randomPoolNumber))
                    {
                        digests.Add(_RandomPools[i].GetDigest());
                        randomPoolUsedMask = randomPoolUsedMask | (1UL << i);
                    }
                }
            }
            return randomPoolUsedMask;
        }

        private static bool PoolIsUsedLinear(int i, ulong reseedCount)
        {
            // 9.5.2
            // Pool P[i] is included if 2^i is a divisor of r. Thus, P[0] is used every reseed, P[1] every other reseed, P[2] every fourth reseed, etc
            var pow = ULongPow(2, i);
            var remainder = reseedCount % pow;
            var result = remainder == 0;
            return result;
        }

        private static bool PoolIsUsedRandom(int i, ulong rand)
        {
            // So that random pools are very random, we simply see if the i-th bit of the random number is set.
            // Usually multiple random UInt64s are and-ed together, so there's 1/2 chance for each pool at best.
            var maybeSetBit = rand & (1UL << i);
            return maybeSetBit > 0;
        }

        private static ulong ULongPow(uint bas, int pow)
        {
            // https://stackoverflow.com/a/383596
            ulong ret = 1;
            ulong x = bas;
            while (pow != 0)
            {
                if ((pow & 1) == 1)
                    ret *= x;
                x *= x;
                pow >>= 1;
            }
            return ret;
        }

        private static int NumberOfSetBits(ulong i)
        {
            // https://stackoverflow.com/a/2709523
            i = i - ((i >> 1) & 0x5555555555555555UL);
            i = (i & 0x3333333333333333UL) + ((i >> 2) & 0x3333333333333333UL);
            return (int)(unchecked(((i + (i >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
        }
    }
}

