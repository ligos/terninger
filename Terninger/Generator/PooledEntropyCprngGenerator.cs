using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.Accumulator;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.LibLog;

using BigMath;

namespace MurrayGrant.Terninger.Generator
{
    public class PooledEntropyCprngGenerator : IDisposableRandomNumberGenerator
    {
        // The main random number generator for Fortuna and Terniner.

        // The PRNG based on a cypher or other crypto primitive, as specifeid in section 9.4.
        private readonly IReseedableRandomNumberGenerator _Prng;
        private readonly object _PrngLock = new object();
        // The entropy accumulator, as specified in section 9.5.
        private readonly EntropyAccumulator _Accumulator;
        private readonly object _AccumulatorLock = new object();
        // Multiple entropy sources, as specified in section 9.5.1.
        private readonly List<IEntropySource> _EntropySources;
        public int SourceCount => this._EntropySources.Count;

        // A thread used to schedule reading from entropy sources.
        // TODO: make an interface so we don't need to own a real thread (eg: instead use WinForms timers).
        private readonly Thread _SchedulerThread;

        private bool _Disposed = false;
        private readonly static ILog Logger = LogProvider.For<PooledEntropyCprngGenerator>();

        public int MaxRequestBytes => _Prng.MaxRequestBytes;

        // Various state to determine polling intervals, reseed times, etc.
        private DateTime _LastReseedUtc;
        private DateTime _LastRandomRequestUtc;
        private Int128 _ReseedCountAtLastRandomRequest;

        public PooledGeneratorConfig Config { get; private set; }

        public Guid UniqueId { get; private set; }

        public Int128 BytesRequested { get; private set; }
        public Int128 ReseedCount => this._Accumulator.TotalReseedEvents;

        /// <summary>
        /// Reports how aggressively the generator is trying to read entropy.
        /// </summary>
        public EntropyPriority EntropyPriority { get; private set; }

        /// <summary>
        /// True if the generator is currently gathering entropy.
        /// </summary>
        public bool IsRunning => _SchedulerThread.IsAlive;
        private readonly CancellationTokenSource _ShouldStop = new CancellationTokenSource();
        private readonly EventWaitHandle _WakeSignal = new EventWaitHandle(false, EventResetMode.AutoReset);
        private readonly WaitHandle[] _AllSignals;
        private const int WakeReason_ExternalReseed = 0;
        private const int WakeReason_GeneratorStopped = 1;
        private const int WakeReason_SleepElapsed = WaitHandle.WaitTimeout;

        /// <summary>
        /// Event is raised after each time the generator is reseeded.
        /// </summary>
        public event EventHandler<PooledEntropyCprngGenerator> OnReseed;


        public PooledEntropyCprngGenerator(IEnumerable<IEntropySource> initialisedSources) 
            : this(initialisedSources, new EntropyAccumulator(), CypherBasedPrngGenerator.CreateWithCheapKey(), new PooledGeneratorConfig()) { }
        public PooledEntropyCprngGenerator(IEnumerable<IEntropySource> initialisedSources, EntropyAccumulator accumulator)
            : this(initialisedSources, accumulator, CypherBasedPrngGenerator.CreateWithCheapKey(), new PooledGeneratorConfig()) { }
        public PooledEntropyCprngGenerator(IEnumerable<IEntropySource> initialisedSources, IReseedableRandomNumberGenerator prng)
            : this(initialisedSources, new EntropyAccumulator(), prng, new PooledGeneratorConfig()) { }
        public PooledEntropyCprngGenerator(IEnumerable<IEntropySource> initialisedSources, EntropyAccumulator accumulator, IReseedableRandomNumberGenerator prng)
            : this(initialisedSources, accumulator, prng, new PooledGeneratorConfig()) { }

        /// <summary>
        /// Initialise the CPRNG with the given PRNG, accumulator, entropy sources and thread.
        /// This does not start the generator.
        /// </summary>
        public PooledEntropyCprngGenerator(IEnumerable<IEntropySource> initialisedSources, EntropyAccumulator accumulator, IReseedableRandomNumberGenerator prng, PooledGeneratorConfig config)
        {
            if (initialisedSources == null) throw new ArgumentNullException(nameof(initialisedSources));
            if (accumulator == null) throw new ArgumentNullException(nameof(accumulator));
            if (prng == null) throw new ArgumentNullException(nameof(prng));

            this.UniqueId = Guid.NewGuid();
            this._Prng = prng;      // Note that this is keyed with a low entropy key.
            this._Accumulator = accumulator;
            this._EntropySources = new List<IEntropySource>(initialisedSources);
            this.Config = config ?? new PooledGeneratorConfig();
            this._SchedulerThread = new Thread(ThreadLoop, 256 * 1024);
            _SchedulerThread.Name = "Terninger Worker Thread - " + UniqueId.ToString("X");
            _SchedulerThread.IsBackground = true;
            this.EntropyPriority = EntropyPriority.High;        // A new generator must reseed as quickly as possible.
            // Important, the index of these are used in WakeReason()
            _AllSignals = new[] { _WakeSignal, _ShouldStop.Token.WaitHandle };
        }

        public static PooledEntropyCprngGenerator Create(IEnumerable<IEntropySource> initialisedSources = null
                            , EntropyAccumulator accumulator = null
                            , IReseedableRandomNumberGenerator prng = null
                            , PooledGeneratorConfig config = null)
            => new PooledEntropyCprngGenerator(initialisedSources ?? Enumerable.Empty<IEntropySource>()
                                            , accumulator ?? new EntropyAccumulator()
                                            , prng ?? CypherBasedPrngGenerator.CreateWithCheapKey()
                                            , config ?? new PooledGeneratorConfig());

        public void Dispose()
        {
            if (_Disposed) return;

            lock (_PrngLock)
            {
                this.RequestStop();

                if (_Prng != null)
                    _Prng.TryDispose();
                if (_EntropySources != null)
                    foreach (var s in _EntropySources)
                        s.TryDispose();
                _Disposed = true;
            }
        }

        /// <summary>
        /// Fill the supplied buffer with random bytes.
        /// This will throw if the inital seed has not been generated - await StartAndWaitForFirstSeed() first.
        /// </summary>
        public void FillWithRandomBytes(byte[] toFill) => FillWithRandomBytes(toFill, 0, toFill.Length);
        /// <summary>
        /// File the supplied buffer with count random bytes at the specified offset.
        /// This will throw if the inital seed has not been generated - await StartAndWaitForFirstSeed() first.
        /// </summary>
        public void FillWithRandomBytes(byte[] toFill, int offset, int count)
        {
            if (this.ReseedCount == 0)
                throw new InvalidOperationException("The random number generator has not accumulated enough entropy to be used. Please wait until ReSeedCount > 1, or await StartAndWaitForFirstSeed().");

            lock (_PrngLock)
            {
                _Prng.FillWithRandomBytes(toFill, offset, count);
            }
            // Metrics to help decide priority.
            this.BytesRequested = this.BytesRequested + count;
            this._LastRandomRequestUtc = DateTime.UtcNow;
            this._ReseedCountAtLastRandomRequest = this.ReseedCount;
            // Any request for data in low priority should raise the level to normal.
            if (this.EntropyPriority == EntropyPriority.Low)
            {
                this.EntropyPriority = EntropyPriority.Normal;
                this._WakeSignal.Set();
            }
        }




        /// <summary>
        /// Start the main entropy loop, to gather entropy over time.
        /// </summary>
        public void Start()
        {
            _LastRandomRequestUtc = DateTime.UtcNow;
            _LastReseedUtc = DateTime.UtcNow;
            this._SchedulerThread.Start();
        }

        /// <summary>
        /// Start the main entropy loop, and wait for the first seed.
        /// </summary>
        public Task StartAndWaitForFirstSeed() => StartAndWaitForNthSeed(1);
        /// <summary>
        /// Start the main entropy loop, and wait for the specified reseed event.
        /// </summary>
        public Task StartAndWaitForNthSeed(Int128 seedNumber)
        {
            if (this.ReseedCount >= seedNumber)
                return Task.FromResult(0);
            this.Start();
            return WaitForNthSeed(seedNumber);
        }

        /// <summary>
        /// Wait for the specified reseed event.
        /// </summary>
        private async Task WaitForNthSeed(Int128 seedNumber)
        {
            // TODO: work out how to do this without polling.
            while (this.ReseedCount < seedNumber)
                await Task.Delay(100);
        }

        /// <summary>
        /// Stop the generater from gathering entropy.
        /// </summary>
        public void RequestStop()
        {
            Logger.Debug("Sending stop signal to generator thread.");
            _ShouldStop.Cancel();
        }
        /// <summary>
        /// Stop the generater from gathering entropy. A Task is returned when the generator has stopped.
        /// </summary>
        public async Task Stop()
        {
            Logger.Debug("Sending stop signal to generator thread.");
            _ShouldStop.Cancel();
            await Task.Delay(1);
            // TODO: work out how to do this without polling. Thread.Join() perhaps.
            while (this._SchedulerThread.IsAlive)
                await Task.Delay(100);
        }

        /// <summary>
        /// Request the internal random number generator be reseeded as soon as possible.
        /// </summary>
        public void StartReseed()
        {
            Reseed();
        }
        /// <summary>
        /// Request the internal random number generator be reseeded as soon as possible. A Task is returned when the reseed is completed.
        /// </summary>
        public Task Reseed() {
            Logger.Debug("Received external reseed request.");
            this.EntropyPriority = EntropyPriority.High;
            this._WakeSignal.Set();
            return WaitForNthSeed(this.ReseedCount + 1);
        }

        /// <summary>
        /// Resets the entropy contained in the first (zero) pool.
        /// When combined with Reseed(), this forces a minimal amount of entropy to be gathered before a reseed.
        /// This can be useful after reloading the generator from disk, or resuming from hybernation / sleep.
        /// </summary>
        public void ResetPoolZero()
        {
            Logger.Debug("Received external pool zero reset.");
            lock (_AccumulatorLock)
            {
                _Accumulator.ResetPoolZero();
            }
        }


        /// <summary>
        /// Sets the generator priority to the value provided.
        /// </summary>
        public void SetPriority(EntropyPriority priority)
        {
            Logger.Debug("Received external priority: {0}, was {1}.", priority, EntropyPriority);
            this.EntropyPriority = priority;
            if (this.EntropyPriority == EntropyPriority.High)
                this._WakeSignal.Set();
        }

        /// <summary>
        /// Add an initialised and ready to use entropy source to the generator.
        /// </summary>
        public void AddInitialisedSource(IEntropySource source)
        {
            lock(_EntropySources)
            {
                _EntropySources.Add(source);
            }
        }


        private void ThreadLoop()
        {
            try
            {
                ThreadLoopInner();
            }
            catch (Exception ex)
            {
                Logger.FatalException("Unhandled exception in generator. Generator will now stop, no further entropy will be generated or available.", ex);
                this.Dispose();
            }
        }
        private void ThreadLoopInner()
        {
            while (!_ShouldStop.IsCancellationRequested)
            {
                Logger.Trace("Running entropy loop.");

                var sources = GetSources();
                if (sources == null || !sources.Any())
                {
                    // No entropy sources; sleep and try again soon.
                    Logger.Trace("No entropy sources available yet.");
                }
                else
                {
                    // Poll all sources.
                    Logger.Trace("Gathering entropy from {0:N0} source(s).", sources.Count());
                    this.PollSources(sources);
                    Logger.Trace("Accumulator stats (bytes): available entropy = {0}, first pool entropy = {1}, min pool entropy = {2}, max pool entropy = {3}, total entropy ever seen {4}.", _Accumulator.AvailableEntropyBytesSinceLastSeed, _Accumulator.PoolZeroEntropyBytesSinceLastSeed, _Accumulator.MinPoolEntropyBytesSinceLastSeed, _Accumulator.MaxPoolEntropyBytesSinceLastSeed, _Accumulator.TotalEntropyBytes);


                    // Reseed the generator (if requirements for reseed are met).
                    bool didReseed = MaybeReseedGenerator();

                    // Update the priority based on recent data requests.
                    MaybeUpdatePriority(didReseed);
                }

                // Wait for some period of time before polling again.
                var sleepTime = WaitTimeBetweenPolls();
                Logger.Trace("Sleeping entropy loop for {0}.", sleepTime);
                if (sleepTime > TimeSpan.Zero)
                {
                    // The thread should be woken on cancellation or external signal.
                    int wakeIdx = WaitHandle.WaitAny(_AllSignals, sleepTime);
                    Logger.Trace("Entropy loop woken up, reason: {0}", WakeReason(wakeIdx));
                }
            }
        }

        private IEnumerable<IEntropySource> GetSources()
        {
            // Read and randomise sources.
            IEntropySource[] sources;
            lock (_EntropySources)
            {
                sources = _EntropySources.ToArray();
            }
            // There may not be any sources until some time after the generator is started.
            if (sources.Length == 0)
            {
                // The thread should be woken on cancellation or external signal.
                int wakeIdx = WaitHandle.WaitAny(_AllSignals, 100);
                var wasTimeout = wakeIdx == WaitHandle.WaitTimeout;
                return null;
            }
            // Note if we could use some more sources.
            if (sources.Length <= 2)
                Logger.Warn("Only {0} entropy source(s) are available. A minimum of 2 is required; 3 or more recommended.", sources.Length);

            lock (_PrngLock)
            {
                // Sources are shuffled so the last source isn't easily determined (the last source can bias the accumulator, particularly if malicious).
                sources.ShuffleInPlace(_Prng);
            }
            return sources;
        }

        private void PollSources(IEnumerable<IEntropySource> sources)
        {
            // Poll all sources.
            // TODO: read up to N in parallel.
            foreach (var source in sources)
            {
                byte[] maybeEntropy;
                try
                {
                    // These may come from 3rd parties, use external hardware or do IO: anything could go wrong!
                    Logger.Trace("Reading entropy from source '{0}' (of type '{1}'.", source.Name, source.GetType().Name);
                    maybeEntropy = source.GetEntropyAsync(this.EntropyPriority).GetAwaiter().GetResult();
                    if (maybeEntropy == null || maybeEntropy.Length == 0)
                        Logger.Trace("Read {0:N0} byte(s) of entropy from source '{1}' (of type '{2}').", 0, source.Name, source.GetType().Name);
                    else
                        Logger.Debug("Read {0:N0} byte(s) of entropy from source '{1}' (of type '{2}').", maybeEntropy.Length, source.Name, source.GetType().Name);
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Unhandled exception from entropy source '{0}' (of type '{1}').", ex, source.Name, source.GetType().Name);
                    // TODO: if a particular source keeps throwing, should we ignore it??
                    maybeEntropy = null;
                }


                if (maybeEntropy != null)
                {
                    lock (_AccumulatorLock)
                    {
                        _Accumulator.Add(new EntropyEvent(maybeEntropy, source));
                    }
                }
                if (_ShouldStop.IsCancellationRequested)
                    break;
                if (this.EntropyPriority == EntropyPriority.High && this.ShouldReseed())
                {
                    // Break out of the loop early when in high priority 
                    Logger.Trace("Have gathered a minimal amount of entropy in High priority, ending entropy source loop early.");
                    break;
                }
            }
        }

        private bool MaybeReseedGenerator()
        {
            var didReseed = false;

            if (this.ShouldReseed())
            {
                Logger.Debug("Beginning re-seed. Accumulator stats (bytes): available entropy = {0}, first pool entropy = {1}, min pool entropy = {2}, max pool entropy = {3}, total entropy ever seen {4}.", _Accumulator.AvailableEntropyBytesSinceLastSeed, _Accumulator.PoolZeroEntropyBytesSinceLastSeed, _Accumulator.MinPoolEntropyBytesSinceLastSeed, _Accumulator.MaxPoolEntropyBytesSinceLastSeed, _Accumulator.TotalEntropyBytes);
                byte[] seedMaterial;
                lock (_AccumulatorLock)
                {
                    seedMaterial = _Accumulator.NextSeed();
                }
                Logger.Trace("Got {0:N0} bytes of entropy from {1:N0} accumulator pool(s) as seed material.", seedMaterial.Length, _Accumulator.PoolCountUsedInLastSeedGeneration);

                lock (this._PrngLock)
                {
                    this._Prng.Reseed(seedMaterial);
                }
                didReseed = true;
                this._LastReseedUtc = DateTime.UtcNow;
                Logger.Trace("Reseed complete.");



                Logger.Trace("Raising OnReseed event.");
                try
                {
                    // 3rd party code paranoia.
                    this.OnReseed?.Invoke(this, this);
                }
                catch (Exception ex)
                {
                    Logger.WarnException("Exception when raising OnReseed event.", ex);
                }
            }
            return didReseed;
        }

        private void MaybeUpdatePriority(bool didReseed)
        {
            var now = DateTime.UtcNow;
            var originalPriority = this.EntropyPriority;
            if (didReseed && originalPriority == EntropyPriority.High)
            {
                // If we reseed in high priorty, drop to normal priority.
                this.EntropyPriority = EntropyPriority.Normal;
                Logger.Debug("After reseed in High priority, dropping to Normal.");
            }
            else if (this.EntropyPriority == EntropyPriority.Normal && this._LastRandomRequestUtc < now.Subtract(Config.TimeBeforeSwitchToLowPriority))
            {
                // If there has been a long time since the last request, drop to low priority.
                this.EntropyPriority = EntropyPriority.Low;
                Logger.Debug("Exceeded {0} since last request for random bytes: dropping priority to Low.", Config.TimeBeforeSwitchToLowPriority);
            }
            else if (this.EntropyPriority == EntropyPriority.Normal && this._ReseedCountAtLastRandomRequest < this.ReseedCount - Config.ReseedCountBeforeSwitchToLowPriority)
            {
                // If there has been several reseeds since the last request, drop to low priority.
                this.EntropyPriority = EntropyPriority.Low;
                Logger.Debug("Exceeded {0} reseed events since last request for random bytes: dropping priority to Low.", Config.ReseedCountBeforeSwitchToLowPriority);
            }

            Logger.Trace("MaybeUpdatePriority(): priority was {0}, is now {1}.", originalPriority, this.EntropyPriority);
        }

        private bool ShouldReseed()
        {
            var now = DateTime.UtcNow;
            if (this._ShouldStop.IsCancellationRequested)
            {
                // Cancelled: abort as quickly as possible.
                Logger.Trace("ShouldReseed(): false - cancellation requested.");
                return false;
            }
            else if (_LastReseedUtc > now.Subtract(Config.MinimumTimeBetweenReseeds))
            {
                // Within minimum reseed interval: no reseed.
                Logger.Trace("ShouldReseed(): false - within minimum reseed time.");
                return false;
            }
            else if (_LastReseedUtc < now.Subtract(Config.MaximumTimeBeforeReseed))
            {
                // Outside of maximum reseed interval: must reseed.
                Logger.Trace("ShouldReseed(): true - exceeded time allowed before reseed.");
                return true;
            }
            else if (this.EntropyPriority == EntropyPriority.High)
            {
                // Enough entropy gathered: reseed.
                Logger.Trace("ShouldReseed(): true - exceeded required bytes in pool zero for High priority.");
                return this._Accumulator.PoolZeroEntropyBytesSinceLastSeed > Config.EntropyToTriggerReseedInHighPriority;
            }
            else if (this.EntropyPriority == EntropyPriority.Normal)
            {
                // Enough entropy gathered: reseed.
                Logger.Trace("ShouldReseed(): true - exceeded required bytes in pool zero for Normal priority.");
                return this._Accumulator.PoolZeroEntropyBytesSinceLastSeed > Config.EntropyToTriggerReseedInNormalPriority;
            }
            else if (this.EntropyPriority == EntropyPriority.Low)
            {
                // Enough entropy gathered: reseed.
                Logger.Trace("ShouldReseed(): true - exceeded required bytes in pool zero for Low priority.");
                return this._Accumulator.PoolZeroEntropyBytesSinceLastSeed > Config.EntropyToTriggerReseedInLowPriority;
            }

            Logger.Warn("ShouldReseed(): false - unexpected state.");
            return false;
        }

        // FUTURE: work out how often to poll based on minimum and rate entropy is being consumed.
        private TimeSpan WaitTimeBetweenPolls() => EntropyPriority == EntropyPriority.High ? Config.PollWaitTimeInHighPriority
                                                 : EntropyPriority == EntropyPriority.Normal ? Config.PollWaitTimeInNormalPriority
                                                 : EntropyPriority == EntropyPriority.Low ? Config.PollWaitTimeInLowPriority
                                                 : TimeSpan.FromSeconds(1);     // Impossible case.

        private string WakeReason(int wakeIdx) => wakeIdx == WakeReason_SleepElapsed ? "sleep time elapsed"
                                                : wakeIdx == WakeReason_ExternalReseed ? "external reseed request"
                                                : wakeIdx == WakeReason_GeneratorStopped ? "generator stopped"
                                                : "unknown";

        public class PooledGeneratorConfig
        {
            // TODO: this class should be immutable.

            /// <summary>
            /// Minimum time between reseed events.
            /// Deafult: 100ms (according to Fortuna spec).
            /// </summary>
            public TimeSpan MinimumTimeBetweenReseeds { get; set; } = TimeSpan.FromMilliseconds(100);
            /// <summary>
            /// After this time, a reseed will be required.
            /// Default: 12 hours.
            /// </summary>
            public TimeSpan MaximumTimeBeforeReseed { get; set; } = TimeSpan.FromHours(12);


            /// <summary>
            /// Number of bytes of entropy in first pool to trigger a reseed when in High priority.
            /// Default: 48 bytes.
            /// </summary>
            /// <remarks>
            /// Actual entropy selected will be minimum of this, maximum of pool count * this, median of 3 * this.
            /// First seed will usually only have this amount of entropy.
            /// </remarks>
            public int EntropyToTriggerReseedInHighPriority { get; set; } = 48;
            /// <summary>
            /// Number of bytes of entropy in first pool to trigger a reseed when in Normal priority.
            /// Default: 128 bytes.
            /// </summary>
            /// <remarks>
            /// Actual entropy selected will be minimum of this, maximum of pool count * this, median of 3 * this.
            /// </remarks>
            public int EntropyToTriggerReseedInNormalPriority { get; set; } = 128;
            /// <summary>
            /// Number of bytes of entropy in first pool to trigger a reseed when in Idle priority.
            /// Default: 128 bytes.
            /// </summary>
            /// <remarks>
            /// Actual entropy selected will be minimum of this, maximum of pool count * this, median of 3 * this.
            /// </remarks>
            public int EntropyToTriggerReseedInLowPriority { get; set; } = 128;


            /// <summary>
            /// Time to wait between entropy polls when in High priority.
            /// Default: 1 ms
            /// </summary>
            public TimeSpan PollWaitTimeInHighPriority { get; set; } = TimeSpan.FromMilliseconds(1);
            /// <summary>
            /// Time to wait between entropy polls when in Normal priority.
            /// Default: 10 sec
            /// </summary>
            public TimeSpan PollWaitTimeInNormalPriority { get; set; } = TimeSpan.FromSeconds(10);
            /// <summary>
            /// Time to wait between entropy polls when in Low priority.
            /// Default: 1 min
            /// </summary>
            public TimeSpan PollWaitTimeInLowPriority { get; set; } = TimeSpan.FromSeconds(60);


            /// <summary>
            /// Number of reseeds at Normal priority without any further random requests before generator will drop to Low priority.
            /// Default: 10.
            /// </summary>
            public int ReseedCountBeforeSwitchToLowPriority { get; set; } = 10;

            /// <summary>
            /// Time at Normal priority without any further random requests before generator will drop to Low priority.
            /// Default: 2 hours.
            /// </summary>
            public TimeSpan TimeBeforeSwitchToLowPriority { get; set; } = TimeSpan.FromHours(2);
        }
    }
}
