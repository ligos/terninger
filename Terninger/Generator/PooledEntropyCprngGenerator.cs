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

using BigMath;

namespace MurrayGrant.Terninger.Generator
{
    public class PooledEntropyCprngGenerator : IDisposableRandomNumberGenerator
    {
        // The main random number generator for Fortuna and Terniner.
        // As specified in sections TODO

        // The PRNG based on a cypher or other crypto primitive, as specifeid in section 9.4.
        private readonly IReseedableRandomNumberGenerator _Prng;
        private readonly object _PrngLock = new object();
        // The entropy accumulator, as specified in section 9.5.
        private readonly EntropyAccumulator _Accumulator;
        private readonly object _AccumulatorLock = new object();
        // Multiple entropy sources, as specified in section 9.5.1.
        private readonly List<IEntropySource> _EntropySources;
        public int SourceCount => this._EntropySources.Count;
        private readonly List<IEntropySource> _InitalisedEntropySources;
        public int LiveSourceCount => this._InitalisedEntropySources.Count;

        // A thread used to schedule reading from entropy sources.
        // TODO: make an interface so we don't need to own a real thread (eg: instead use WinForms timers).
        private readonly Thread _SchedulerThread;

        private bool _Disposed = false;

        public int MaxRequestBytes => _Prng.MaxRequestBytes;
 
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
        private bool _RunningCoreLoop = false;

        /// <summary>
        /// Event is raised after each time the generator is reseeded.
        /// </summary>
        public event EventHandler<PooledEntropyCprngGenerator> OnReseed;


        public PooledEntropyCprngGenerator(IEnumerable<IEntropySource> sources) 
            : this(sources, new EntropyAccumulator(), new CypherBasedPrngGenerator(new byte[32])) { }
        public PooledEntropyCprngGenerator(IEnumerable<IEntropySource> sources, EntropyAccumulator accumulator)
            : this(sources, accumulator, new CypherBasedPrngGenerator(new byte[32])) { }
        public PooledEntropyCprngGenerator(IEnumerable<IEntropySource> sources, IReseedableRandomNumberGenerator prng)
            : this(sources, new EntropyAccumulator(), prng) { }

        /// <summary>
        /// Initialise the CPRNG with the given PRNG, accumulator, entropy sources and thread.
        /// This does not start the generator.
        /// </summary>
        public PooledEntropyCprngGenerator(IEnumerable<IEntropySource> sources, EntropyAccumulator accumulator, IReseedableRandomNumberGenerator prng) 
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            if (accumulator == null) throw new ArgumentNullException(nameof(accumulator));
            if (prng == null) throw new ArgumentNullException(nameof(prng));

            this._Prng = prng;      // Note that this is unkeyed at this point (or keyed with an all null key).
            this._Accumulator = accumulator;
            this._EntropySources = new List<IEntropySource>(sources);
            this._InitalisedEntropySources = new List<IEntropySource>(_EntropySources.Count);
            this._SchedulerThread = new Thread(ThreadLoop, 256 * 1024);
            _SchedulerThread.Name = "Terninger Worker Thread";      // TODO: add some kind of way to tell different instances apart.
            _SchedulerThread.IsBackground = true;
            this.EntropyPriority = EntropyPriority.High;        // A new generator must reseed as quickly as possible.
            _AllSignals = new[] { _WakeSignal, _ShouldStop.Token.WaitHandle };

            if (_EntropySources.Count < 2)
                throw new ArgumentException($"At least 2 entropy sources are required. Only {_EntropySources.Count} were provided.", nameof(sources));
        }


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

        // Methods to get random bytes, as part of IRandomNumberGenerator.
        public void FillWithRandomBytes(byte[] toFill) => FillWithRandomBytes(toFill, 0, toFill.Length);
        public void FillWithRandomBytes(byte[] toFill, int offset, int count)
        {
            if (this.ReseedCount == 0)
                throw new InvalidOperationException("The random number generator has not accumulated enough entropy to be used. Please wait until ReSeedCount > 1, or await StartAndWaitForFirstSeed().");

            lock (_PrngLock)
            {
                _Prng.FillWithRandomBytes(toFill, offset, count);
            }
            this.BytesRequested = this.BytesRequested + count;
        }




        public void Start()
        {
            this._SchedulerThread.Start();
        }

        public async Task StartAndWaitForInitialisation()
        {
            // TODO: work out how to do this without polling.
            this.Start();
            while (!_RunningCoreLoop)
                await Task.Delay(100);
        }
        public Task StartAndWaitForFirstSeed() => StartAndWaitForNthSeed(1);
        public Task StartAndWaitForNthSeed(Int128 seedNumber)
        {
            if (this.ReseedCount >= seedNumber)
                return Task.FromResult(0);
            this.Start();
            return WaitForNthSeed(seedNumber);
        }

        private async Task WaitForNthSeed(Int128 seedNumber)
        {
            // TODO: work out how to do this without polling.
            while (this.ReseedCount < seedNumber)
                await Task.Delay(100);
        }

        public void RequestStop()
        {
            _ShouldStop.Cancel();
        }
        public async Task Stop()
        {
            _ShouldStop.Cancel();
            await Task.Delay(1);
            // TODO: work out how to do this without polling.
            while (this._SchedulerThread.IsAlive)
                await Task.Delay(100);
        }

        public void StartReseed()
        {
            Reseed();
        }
        public Task Reseed() {
            this.EntropyPriority = EntropyPriority.High;
            this._WakeSignal.Set();
            // TODO: wake the event loop.
            return WaitForNthSeed(this.ReseedCount + 1);
        }


        private void ThreadLoop()
        {
            // TODO: I don't want no config gumph in here!
            var emptyConfig = new EntropySourceConfigFromDictionary(Enumerable.Empty<string>());
            
            // Start initialising sources.
            // TODO: initialise N in parallel.
            foreach (var source in _EntropySources)
            {
                var initResult = source.Initialise(emptyConfig, EntropySourcePrngFactory).GetAwaiter().GetResult();
                if (initResult.IsSuccessful)
                    _InitalisedEntropySources.Add(source);
                // TODO: log unsuccessful initialisations.
            }


            while (!_ShouldStop.IsCancellationRequested)
            {
                _RunningCoreLoop = true;

                // TODO: start reading as soon as the first source is initialised.
                // Poll all initialised sources.
                foreach (var source in _InitalisedEntropySources)
                {
                    // TODO: read up to N in parallel.
                    var maybeEntropy = source.GetEntropyAsync().GetAwaiter().GetResult();
                    if (maybeEntropy != null)
                    {
                        lock (_AccumulatorLock)
                        {
                            _Accumulator.Add(new EntropyEvent(maybeEntropy, source));
                        }
                    }
                    // TODO: randomise the order of entropy sources to prevent one always being first or last (which can potentially bias the accumulator).
                    if (_ShouldStop.IsCancellationRequested)
                        break;
                }

                // Determine if we should re-seed.
                if (this.ShouldReseed())
                {
                    byte[] seedMaterial;
                    lock (_AccumulatorLock)
                    {
                        seedMaterial = _Accumulator.NextSeed();
                    }
                    lock (this._PrngLock)
                    {
                        this._Prng.Reseed(seedMaterial);
                    }
                    if (this.EntropyPriority == EntropyPriority.High)
                        this.EntropyPriority = EntropyPriority.Normal;
                    this.OnReseed?.Invoke(this, this);
                }

                // Wait for some period of time before polling again.
                var sleepTime = WaitTimeBetweenPolls();
                if (sleepTime > TimeSpan.Zero)
                {
                    // The thread should be woken on cancellation or external signal.
                    int wakeIdx = WaitHandle.WaitAny(_AllSignals, sleepTime);
                    var wasTimeout = wakeIdx == WaitHandle.WaitTimeout;
                }
            }
        }

        private bool ShouldReseed()
        {
            if (this._ShouldStop.IsCancellationRequested)
                return false;
            else if (this.EntropyPriority == EntropyPriority.High)
                // TODO: configure how much entropy we need to accumulate before reseed.
                return this._Accumulator.PoolZeroEntropyBytesSinceLastSeed > 48;
            else if (this.EntropyPriority == EntropyPriority.Low)
                // TODO: use priority, rate of consumption and date / time to determine when to reseed.
                return this._Accumulator.MinPoolEntropyBytesSinceLastSeed > 256;
            else
                // TODO: use priority, rate of consumption and date / time to determine when to reseed.
                return this._Accumulator.MinPoolEntropyBytesSinceLastSeed > 96;
        }

        // TODO: work out how often to poll based on minimum and rate entropy is being consumed.
        private TimeSpan WaitTimeBetweenPolls() => EntropyPriority == EntropyPriority.High ? TimeSpan.Zero
                                                 : EntropyPriority == EntropyPriority.Normal ? TimeSpan.FromSeconds(5)
                                                 : EntropyPriority == EntropyPriority.Low ? TimeSpan.FromSeconds(30)
                                                 : TimeSpan.FromSeconds(1);     // Impossible case.

        // TODO: use a PRNG with 128bit seed.
        private IRandomNumberGenerator EntropySourcePrngFactory() => new StandardRandomWrapperGenerator();
    }
}
