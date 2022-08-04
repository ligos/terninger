using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.Accumulator;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.PersistentState;
using MurrayGrant.Terninger.LibLog;

using BigMath;

namespace MurrayGrant.Terninger.Random
{
    public sealed class PooledEntropyCprngGenerator : IRandomNumberGenerator
    {
        // The main random number generator for Fortuna and Terniner.

        // The PRNG based on a cypher or other crypto primitive, as specifeid in section 9.4.
        private readonly IReseedableRandomNumberGenerator _Prng;
        private readonly object _PrngLock = new object();
        // The entropy accumulator, as specified in section 9.5.
        private readonly EntropyAccumulator _Accumulator;
        private readonly object _AccumulatorLock = new object();
        // Multiple entropy sources, as specified in section 9.5.1.
        private readonly List<SourceAndMetadata> _EntropySources;
        public int SourceCount => this._EntropySources.Count;

        // A task used to schedule reading from entropy sources.
        // TODO: should we have a TaskFactory or TaskScheduler to allow a non-thread pool task?
        private Task _SchedulerTask;
        private readonly List<Task> _OutstandingEntropySourceTasks = new List<Task>();

        private bool _Disposed = false;
        private readonly static ILog Logger = LogProvider.For<PooledEntropyCprngGenerator>();

        public int MaxRequestBytes => _Prng.MaxRequestBytes;

        // Various state to determine polling intervals, reseed times, etc.
        private DateTime _LastReseedUtc;
        private DateTime _LastRandomRequestUtc;
        private Int128 _ReseedCountAtLastRandomRequest;
        private Int128 _BytesGeneratedAtLastReseed;

        public PooledGeneratorConfig Config { get; private set; }

        public Guid UniqueId { get; private set; }

        public Int128 BytesRequested { get; private set; }
        public Int128 ReseedCount => this._Accumulator.TotalReseedEvents;

        /// <summary>
        /// Reports how aggressively the generator is trying to read entropy.
        /// </summary>
        public EntropyPriority EntropyPriority { get; private set; }

        private IPersistentStateReader _PersistentStateReader;
        private IPersistentStateWriter _PersistentStateWriter;

        /// <summary>
        /// True if the generator is currently gathering entropy.
        /// </summary>
        public bool IsRunning => _SchedulerTask != null && !_SchedulerTask.IsCanceled && !_SchedulerTask.IsCompleted && !_SchedulerTask.IsFaulted;
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
        private List<(Int128 seedNumber, TaskCompletionSource<Int128> source)> _OnReesedTaskSources = new List<(Int128, TaskCompletionSource<Int128>)>();
        
        /// <summary>
        /// Initialise the CPRNG with the given PRNG, accumulator, entropy sources and persistent state reader / writer.
        /// This does not start the generator.
        /// </summary>
        private PooledEntropyCprngGenerator(
            IEnumerable<IEntropySource> initialisedSources, 
            EntropyAccumulator accumulator, 
            IReseedableRandomNumberGenerator prng, 
            PooledGeneratorConfig config, 
            IPersistentStateReader persistentStateReader, 
            IPersistentStateWriter persistentStateWriter)
        {
            if (initialisedSources == null) throw new ArgumentNullException(nameof(initialisedSources));
            if (accumulator == null) throw new ArgumentNullException(nameof(accumulator));
            if (prng == null) throw new ArgumentNullException(nameof(prng));

            this._Prng = prng;      // Note that this is keyed with a low entropy key.
            this._Accumulator = accumulator;
            this._EntropySources = new List<SourceAndMetadata>();
            foreach (var s in initialisedSources.Where(s => s != null))
                _EntropySources.Add(AssignSourceUniqueName(s));
            this.Config = config ?? new PooledGeneratorConfig();
            this.EntropyPriority = EntropyPriority.High;        // A new generator must reseed as quickly as possible.
            // Important, the index of these are used in WakeReason()
            _AllSignals = new[] { _WakeSignal, _ShouldStop.Token.WaitHandle };
            this._PersistentStateReader = persistentStateReader;
            this._PersistentStateWriter = persistentStateWriter;
        }

        /// <summary>
        /// Initialise the CPRNG with the given PRNG, accumulator, entropy sources and persistent state reader / writer.
        /// This does not start the generator.
        /// </summary>
        public static PooledEntropyCprngGenerator Create(IEnumerable<IEntropySource> initialisedSources = null
                            , EntropyAccumulator accumulator = null
                            , IReseedableRandomNumberGenerator prng = null
                            , PooledGeneratorConfig config = null
                            , IPersistentStateReader persistentStateReader = null
                            , IPersistentStateWriter persistentStateWriter = null)
            => new PooledEntropyCprngGenerator(initialisedSources ?? Enumerable.Empty<IEntropySource>()
                                            , accumulator ?? new EntropyAccumulator()
                                            , prng ?? CypherBasedPrngGenerator.CreateWithCheapKey()
                                            , config ?? new PooledGeneratorConfig()
                                            , persistentStateReader
                                            , persistentStateWriter);

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
                        s.Source.TryDispose();
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
            if (this._SchedulerTask == null)
            {
                Logger.Debug("Starting Terninger worker task.");
                _LastRandomRequestUtc = DateTime.UtcNow;
                _LastReseedUtc = DateTime.UtcNow;
                this._SchedulerTask = Task.Run(() =>
                {
                    try
                    {
                        Logger.Debug("Begin initialisation.");
                        var persistentState = TryLoadPersistentState().GetAwaiter().GetResult();
                        if (persistentState?.Count > 0)
                            InitialiseInternalObjectsFromPersistentState(persistentState);
                        Logger.Debug("Initialisation complete.");

                        Logger.Info("Starting Terninger worker loop for generator {0}.", UniqueId);
                        WorkerLoop(persistentState);
                        Logger.Info("Stopped Terninger pooling loop for generator {0}.", UniqueId);
                    }
                    catch (Exception ex)
                    {
                        Logger.FatalException("Unhandled exception in generator {0}. Generator will now stop, no further entropy will be generated or available.", ex, UniqueId);
                        this.Dispose();
                    }
                });
            }
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
                return Task.FromResult(this);
            this.Start();
            return WaitForNthSeed(seedNumber);
        }

        /// <summary>
        /// Wait for the specified reseed event.
        /// </summary>
        private async Task WaitForNthSeed(Int128 seedNumber)
        {
            var reseedSource = new TaskCompletionSource<Int128>();
            lock (_OnReesedTaskSources)
            {
                _OnReesedTaskSources.Add((seedNumber, reseedSource));
            }
            await reseedSource.Task;
        }

        /// <summary>
        /// Stop the generator from gathering entropy.
        /// </summary>
        public void RequestStop()
        {
            Logger.Debug("Sending stop signal to generator thread.");
            _ShouldStop.Cancel();
        }
        /// <summary>
        /// Stop the generator from gathering entropy. A Task is returned when the generator has stopped.
        /// </summary>
        public Task Stop()
        {
            Logger.Debug("Sending stop signal to generator thread.");
            _ShouldStop.Cancel();
            return _SchedulerTask
#if NET452
                ?? Task.FromResult<object>(null);
#else
                ?? Task.CompletedTask;
#endif
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
            var reseedCount = this.ReseedCount;
            this._WakeSignal.Set();
            return WaitForNthSeed(reseedCount + 1);
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
            if (source == null) return;
            lock(_EntropySources)
            {
                _EntropySources.Add(AssignSourceUniqueName(source));
            }
        }
        /// <summary>
        /// Add an initialised and ready to use entropy source to the generator.
        /// </summary>
        public void AddInitialisedSources(IEnumerable<IEntropySource> sources)
        {
            if (sources == null) return;
            lock (_EntropySources)
            {
                foreach (var s in sources.Where(s => s != null))
                {
                    _EntropySources.Add(AssignSourceUniqueName(s));
                }
            }
        }
        private SourceAndMetadata AssignSourceUniqueName(IEntropySource source)
        {
            // Assumption: the caller has a lock on _EntropySources.

            // Choses a name for entropy sources based on:
            // a) user supplied name.
            // b) type name.
            // c) a unique-ifier.

            var candidateName = source.Name;
            if (String.IsNullOrEmpty(candidateName))
                candidateName = source.GetType().Name;

            var baseName = candidateName;
            int uniquifier = 1;
            while (_EntropySources.Any(x => String.Equals(candidateName, x.UniqueName, StringComparison.CurrentCultureIgnoreCase)))
            {
                candidateName = baseName + " " + (uniquifier++);
            }
            return new SourceAndMetadata(source, candidateName);
        }

        private void WorkerLoop(PersistentItemCollection loadedPersistentState)
        {
            while (!_ShouldStop.IsCancellationRequested)
            {
                Logger.Trace("Running entropy loop.");

                var (syncSources, asyncSources) = GetSources();
                if (!syncSources.Any() && !asyncSources.Any())
                {
                    // No entropy sources; sleep and try again soon.
                    Logger.Trace("No entropy sources available yet.");
                }
                else
                {
                    // Initialise any entropy sources from persistent state.
                    InitialiseEntropySourcesFromPersistentState(loadedPersistentState);

                    // Poll all sources.
                    Logger.Trace("Gathering entropy from {0:N0} source(s).", syncSources.Count() + asyncSources.Count());
                    this.PollSources(syncSources, asyncSources).GetAwaiter().GetResult();
                    Logger.Trace("Accumulator stats (bytes): available entropy = {0}, first pool entropy = {1}, min pool entropy = {2}, max pool entropy = {3}, total entropy ever seen {4}.", _Accumulator.AvailableEntropyBytesSinceLastSeed, _Accumulator.PoolZeroEntropyBytesSinceLastSeed, _Accumulator.MinPoolEntropyBytesSinceLastSeed, _Accumulator.MaxPoolEntropyBytesSinceLastSeed, _Accumulator.TotalEntropyBytes);


                    // Reseed the generator (if requirements for reseed are met).
                    bool didReseed = MaybeReseedGenerator();

                    // Update the priority based on recent data requests.
                    MaybeUpdatePriority(didReseed);

                    // And update any awaiters / event subscribers.
                    if (didReseed)
                        RaiseOnReseedEvent();

                    // Gather state to save and write.
                    var writeEvent = didReseed ? WritePersistentEvent.Reseed : WritePersistentEvent.Periodic;
                    GatherAndWritePeristentStateIfRequired(writeEvent).GetAwaiter().GetResult();
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

            // When the generator stops, we write state one last time.
            GatherAndWritePeristentStateIfRequired(WritePersistentEvent.Stopping).GetAwaiter().GetResult();
        }

        private (IEnumerable<SourceAndMetadata> syncSources, IEnumerable<SourceAndMetadata> asyncSource) GetSources()
        {
            // Read and randomise sources.
            SourceAndMetadata[] sources;
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
                return (Enumerable.Empty<SourceAndMetadata>(), Enumerable.Empty<SourceAndMetadata>());
            }
            // Note if we could use some more sources.
            if (sources.Length <= 2)
                Logger.Warn("Only {0} entropy source(s) are available. A minimum of 2 is required; 3 or more recommended.", sources.Length);

            lock (_PrngLock)
            {
                // Sources are shuffled so the last source isn't easily determined (the last source can bias the accumulator, particularly if malicious).
                sources.ShuffleInPlace(_Prng);
            }

            // Identify very likely sync sources.
            var syncSources = sources.Where(x => x.ExceptionScore > -10
                                            &&
                                            ((x.AsyncScore <= -10 && (x.AsyncHint == IsAsync.Never || x.AsyncHint == IsAsync.AfterInit))
                                            || (x.AsyncScore <= -20 && (x.AsyncHint == IsAsync.Rarely || x.AsyncHint == IsAsync.Unknown))
                                            ))
                                    .ToArray();
            var asyncSources = sources.Where(x => x.ExceptionScore > -10 && !syncSources.Contains(x)).ToArray();

            return (syncSources, asyncSources);
        }

        private async Task PollSources(IEnumerable<SourceAndMetadata> syncSources, IEnumerable<SourceAndMetadata> asyncSources)
        {
            // Poll all sources.
            if (_OutstandingEntropySourceTasks.Any())
            {
                Logger.Trace("Ensuring previous async sources have completed.");
                await Task.WhenAll(_OutstandingEntropySourceTasks);
                _OutstandingEntropySourceTasks.Clear();
            }

            // Start all likely async sources up front.
            var asyncTasks = asyncSources.Select(x => ReadAndAccumulate(x)).ToList();
            Logger.Trace("Reading from {0:N0} async sources.", asyncTasks.Count);

            // While the async sources are running, we do a mini-polling loop on the sync ones.
            Logger.Trace("Reading from {0:N0} sync sources.", syncSources.Count());
            var loopDelay = Config.MiniPollWaitTime;
            int loops = 1;
            do
            {
                await PollSyncSources(syncSources, asyncTasks);

                if (_ShouldStop.IsCancellationRequested)
                    break;

                if (this.EntropyPriority == EntropyPriority.High
                    && this.ShouldReseed()
                    && (asyncTasks.Any(t => t.IsCompleted) || !asyncTasks.Any()))
                {
                    // Break out of the loop early when in high priority 
                    Logger.Trace("Have gathered a minimal amount of entropy in High priority, ending entropy source loop early.");
                    break;
                }

                if ((asyncTasks.All(x => x.IsCompleted) || !asyncTasks.Any())
                    && loops >= 1)
                {
                    // Break out of the loop when all async sources have completed.
                    Logger.Trace("Async entropy sources have completed, and at least one poll of all sync sources. Ending polling loop.");
                    break;
                }

                if (asyncTasks.Any())
                {
                    Logger.Trace("Mini / synchronous pooling loop #{0} completed. Waiting for {1:N1}ms.", loops, loopDelay.TotalMilliseconds);
                    await Task.Delay(loopDelay);
                    loopDelay = new TimeSpan((long)(loopDelay.Ticks * ScalingFactorBetweenSyncPolls()));
                }
                loops = loops + 1;
            } 
            while (!_ShouldStop.IsCancellationRequested && asyncTasks.Any() && loops < 30);

            await AwaitOrParkUnfinishedAsyncTasks(asyncTasks);
        }

        private async Task PollSyncSources(IEnumerable<SourceAndMetadata> syncSources, IEnumerable<Task> asyncTasks)
        {
            foreach (var sm in syncSources)
            {
                await ReadAndAccumulate(sm);

                if (_ShouldStop.IsCancellationRequested)
                    break;

                if (this.EntropyPriority == EntropyPriority.High
                    && this.ShouldReseed()
                    && asyncTasks.Any(t => t.IsCompleted))
                    break;
            }
        }
        
        private async Task AwaitOrParkUnfinishedAsyncTasks(IEnumerable<Task> asyncTasks)
        {
            if (!asyncTasks.Any())
                return;

            var unfinishedTasks = asyncTasks.Where(x => !x.IsCompleted).ToList();
            if (this.EntropyPriority != EntropyPriority.High && unfinishedTasks.Any())
            {
                Logger.Trace("Waiting for {0:N0} slow async sources.", unfinishedTasks.Count());
                await Task.WhenAll(asyncTasks);
            }
            else if (this.EntropyPriority == EntropyPriority.High && unfinishedTasks.Any())
            {
                Logger.Trace("{0:N0} slow async sources still running; will await on next polling loop.", unfinishedTasks.Count());
                _OutstandingEntropySourceTasks.AddRange(unfinishedTasks);
            }
        }

        private async Task ReadAndAccumulate(SourceAndMetadata sm)
        {
            var maybeEntropy = await ReadFromSourceSafely(sm);
            if (maybeEntropy != null)
            {
                lock (_AccumulatorLock)
                {
                    _Accumulator.Add(new EntropyEvent(maybeEntropy, sm.Source));
                }
            }
        }

        private async Task<byte[]> ReadFromSourceSafely(SourceAndMetadata sm)
        {
            var source = sm.Source;
            try
            {
                // These may come from 3rd parties, use external hardware or do IO: anything could go wrong!
                Logger.Trace("Reading entropy from source '{0}' (of type '{1}').", sm.UniqueName, source.GetType().Name);
                
                var t = sm.Source.GetEntropyAsync(this.EntropyPriority);
                var wasSync = t.IsCompleted;
                byte[] maybeEntropy;
                
                if (wasSync)
                {
                    maybeEntropy = t.Result;
                    sm.ScoreSync();
                }
                else
                {
                    maybeEntropy = await t;
                    sm.ScoreAsync();
                }
                sm.ScoreSuccess();

                if (maybeEntropy == null || maybeEntropy.Length == 0)
                    Logger.Trace("Read {0:N0} byte(s) of entropy from source '{1}' (of type '{2}').", 0, sm.UniqueName, source.GetType().Name);
                else
                    Logger.Debug("Read {0:N0} byte(s) of entropy from source '{1}' (of type '{2}').", maybeEntropy.Length, sm.UniqueName, source.GetType().Name);

                return maybeEntropy;
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Unhandled exception from entropy source '{0}' (of type '{1}').", ex, sm.UniqueName, source.GetType().Name);
                sm.ScoreException();
                return null;
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
                this._BytesGeneratedAtLastReseed = this.BytesRequested;
                Logger.Info("Re-seeded Generator using {0:N0} bytes of entropy from {1:N0} accumulator pool(s).", seedMaterial.Length, _Accumulator.PoolCountUsedInLastSeedGeneration);
            }
            return didReseed;
        }

        private void RaiseOnReseedEvent()
        {
            var reseedCount = this.ReseedCount;

            if (this.OnReseed != null)
            {
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

            if (_OnReesedTaskSources.Any())
            {
                lock (_OnReesedTaskSources)
                {
                    Logger.Trace("Signaling WaitForNthSeed() awaiters.");
                    var toSignal = _OnReesedTaskSources.Where(x => x.seedNumber <= reseedCount).ToList();
                    foreach (var (seedNumber, source) in toSignal)
                    {
                        source.SetResult(reseedCount);
                        _OnReesedTaskSources.Remove((seedNumber, source));
                    }
                }
            }
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

        private async Task<PersistentItemCollection> TryLoadPersistentState()
        {
            if (_PersistentStateReader == null)
                return null;

            Logger.Trace("Loading persistent state from '{0}'", _PersistentStateReader.GetType().Name);
            try
            {
                var result = await _PersistentStateReader.ReadAsync();
                Logger.Debug("Loaded {0:N0} key-value-pairs from persistent state '{1}'.", result.Count, _PersistentStateReader.GetType().Name);
                return result;
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Unable to load persistent state from '{0}'.", ex, _PersistentStateReader.GetType().Name);
                return null;
            }
        }

        private void InitialiseInternalObjectsFromPersistentState(PersistentItemCollection persistentState)
        {
            Logger.Trace("Initialising internal Terninger objects from persistent state.");
            // TODO: logging.

            // this
            // _Prng
            // _Accumulator

            // Remove each namespace from collection so entropy sources cannot observe internal state.
        }

        private void InitialiseEntropySourcesFromPersistentState(PersistentItemCollection persistentState)
        {
            Logger.Trace("Initialising entropy sources from persistent state.");
            // Only entropy sources at the moment
            // After we reseed for the second time, we stop bothering with this (on the assumption any sources would be added by then).

            // Remove each namespace from collection after a source is initialised.
            // TODO: logging.
        }

        private async Task GatherAndWritePeristentStateIfRequired(WritePersistentEvent eventType)
        {
            if (_PersistentStateWriter == null)
                return;

            // If any source has updates, or we just reseeded.

            // Accumulate state from all sources.
            var persistentState = new PersistentItemCollection();

            // Always accumulate internal objects last, so anyone trying to impersonate global namespaces gets overwritten.

            // Save.
            await _PersistentStateWriter.WriteAsync(persistentState);

            // TODO: logging.
        }
        enum WritePersistentEvent
        {
            Reseed = 1,
            Periodic = 2,
            Stopping = 3,
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
            else if (Config.MaximumBytesGeneratedBeforeReseed < this.BytesRequested - _BytesGeneratedAtLastReseed)
            {
                // Generated too many bytes: must reseed.
                Logger.Trace("ShuldReseed(): true - exceeded allowed bytes generated.");
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

        private double ScalingFactorBetweenSyncPolls() => EntropyPriority == EntropyPriority.High ? 1.1
                                                     : EntropyPriority == EntropyPriority.Normal ? 1.4
                                                     : EntropyPriority == EntropyPriority.Low ? 1.9
                                                     : 2.0;     // Impossible case.

        private string WakeReason(int wakeIdx) => wakeIdx == WakeReason_SleepElapsed ? "sleep time elapsed"
                                                : wakeIdx == WakeReason_ExternalReseed ? "external reseed request"
                                                : wakeIdx == WakeReason_GeneratorStopped ? "generator stopped"
                                                : "unknown";

        public class PooledGeneratorConfig
        {
            // TODO: this class should be immutable.

            /// <summary>
            /// Minimum time between reseed events.
            /// Default: 100ms (according to Fortuna spec).
            /// </summary>
            public TimeSpan MinimumTimeBetweenReseeds { get; set; } = TimeSpan.FromMilliseconds(100);
            /// <summary>
            /// After this time, a reseed will be required.
            /// Default: 12 hours.
            /// </summary>
            public TimeSpan MaximumTimeBeforeReseed { get; set; } = TimeSpan.FromHours(12);


            /// <summary>
            /// After this many bytes of entropy are produced, a reseed will be triggered.
            /// Default: 16MB.
            /// </summary>
            public long MaximumBytesGeneratedBeforeReseed { get; set; } = 16L * 1024L * 1024L;

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
            /// Time to wait between synchronous source polling in mini-polling loop while waiting for async sources to complete.
            /// Default: 30ms
            /// </summary>
            public TimeSpan MiniPollWaitTime { get; set; } = TimeSpan.FromMilliseconds(30);


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

    public static class PooledEntropyCprngGeneratorExtensions
    {
        /// <summary>
        /// Adds initialised entropy sources to the generator. 
        /// Fluent interface.
        /// </summary>
        public static PooledEntropyCprngGenerator With(this PooledEntropyCprngGenerator pooled, IEnumerable<IEntropySource> sources)
        {
            pooled.AddInitialisedSources(sources);
            return pooled;
        }
        /// <summary>
        /// Adds an initialised entropy source to the generator. 
        /// Fluent interface.
        /// </summary>
        public static PooledEntropyCprngGenerator With(this PooledEntropyCprngGenerator pooled, IEntropySource source)
        {
            pooled.AddInitialisedSource(source);
            return pooled;
        }


        /// <summary>
        /// Starts the generator. Note that the return value is NOT immediately able to generate random numbers.
        /// Fluent interface.
        /// </summary>
        public static PooledEntropyCprngGenerator StartNoWait(this PooledEntropyCprngGenerator pooled)
        {
            pooled.Start();
            return pooled;
        }
        /// <summary>
        /// Starts the generator and waits for the first seed to become available.
        /// Fluent interface.
        /// </summary>
        public static async Task<PooledEntropyCprngGenerator> StartAndWaitForSeedAsync(this PooledEntropyCprngGenerator pooled)
        {
            await pooled.StartAndWaitForFirstSeed();
            return pooled;
        }
    }

}
