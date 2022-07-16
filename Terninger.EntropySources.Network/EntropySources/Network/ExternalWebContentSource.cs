using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.LibLog;

namespace MurrayGrant.Terninger.EntropySources.Network
{
    /// <summary>
    /// An entropy source which uses external web content as input, usually from social media or news services.
    /// </summary>
    [AsyncHint(IsAsync.Always)]
    public class ExternalWebContentSource : EntropySourceWithPeriod
    {
        public override string Name { get; set; }

        private IRandomNumberGenerator _Rng;

        public string SourcePath { get; private set; }
        private readonly List<Uri> _Sources = new List<Uri>();
        public int SourceCount => _Sources.Count;
        private int _NextSource;
        private bool _SourcesInitialised;

        private int _UrlsPerSample;                   // This many web requests are made per entropy request.
        public int UrlsPerSample => _UrlsPerSample;

        private readonly bool _UseRandomSourceForUnitTest;
        private readonly string _UserAgent;
        private bool _UnconfiguredUserAgentWarningEmitted;

        public ExternalWebContentSource(string userAgent, Configuration config)
            : this(
                  userAgent: userAgent,
                  sourcePath: config?.UrlFilePath,
                  urlsPerSample: config?.UrlsPerSample ?? Configuration.Default.UrlsPerSample,
                  periodNormalPriority: config?.PeriodNormalPriority ?? Configuration.Default.PeriodNormalPriority,
                  periodHighPriority: config?.PeriodHighPriority ?? Configuration.Default.PeriodHighPriority,
                  periodLowPriority: config?.PeriodLowPriority ?? Configuration.Default.PeriodLowPriority
            )
        { }
        public ExternalWebContentSource(string userAgent = null, string sourcePath = null, IEnumerable<Uri> sources = null, TimeSpan? periodNormalPriority = null, TimeSpan? periodHighPriority = null, TimeSpan? periodLowPriority = null, int? urlsPerSample = null, IRandomNumberGenerator rng = null)
            : base(periodNormalPriority.GetValueOrDefault(Configuration.Default.PeriodNormalPriority), 
                  periodHighPriority.GetValueOrDefault(Configuration.Default.PeriodHighPriority), 
                  periodLowPriority.GetValueOrDefault(Configuration.Default.PeriodLowPriority))
        {
            this._UrlsPerSample = urlsPerSample.GetValueOrDefault(Configuration.Default.UrlsPerSample);
            if (_UrlsPerSample <= 0)
                throw new ArgumentOutOfRangeException(nameof(urlsPerSample), urlsPerSample, "URLs per sample must be at least one.");

            this.SourcePath = sourcePath;
            if (sources != null)
                this._Sources.AddRange(sources);
            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? HttpClientHelpers.UserAgentString() : userAgent;
            this._Rng = rng ?? StandardRandomWrapperGenerator.StockRandom();
        }
        internal ExternalWebContentSource(bool useDiskSourceForUnitTests)
            : this(HttpClientHelpers.UserAgentString(), null, null, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 5, null)
        {
            this._UseRandomSourceForUnitTest = useDiskSourceForUnitTests;
        }

        public override void Dispose()
        {
            var asIDisposable = _Rng as IDisposable;
            if (asIDisposable != null)
            {
                asIDisposable.Dispose();
                _Rng = null;
            }
        }

        public static Task<IReadOnlyCollection<Uri>> LoadInternalUrlListAsync()
        {
            var log = LibLog.LogProvider.For<ExternalWebContentSource>();
            log.Debug("Loading internal source URL list...");
            using (var stream = typeof(ExternalWebContentSource).Assembly.GetManifestResourceStream(typeof(ExternalWebContentSource), "ExternalWebServerList.txt"))
            {
                return LoadUrlListAsync(stream);
            }
        }
        public static Task<IReadOnlyCollection<Uri>> LoadUrlListAsync(string path)
        {
            var log = LibLog.LogProvider.For<ExternalWebContentSource>();
            log.Debug("Loading source URL list from '{0}'...", path);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 32 * 1024, FileOptions.SequentialScan))
            {
                return LoadUrlListAsync(stream);
            }
        }
        public static async Task<IReadOnlyCollection<Uri>> LoadUrlListAsync(Stream stream)
        {
            var log = LibLog.LogProvider.For<ExternalWebContentSource>();

            var sources = new List<Uri>();
            using (var reader = new StreamReader(stream, Encoding.UTF8, false, 32 * 1024, true))
            {
                int lineNum = 0;
                while (!reader.EndOfStream)
                {
                    var l = await reader.ReadLineAsync();
                    lineNum = lineNum + 1;

                    if (String.IsNullOrWhiteSpace(l))
                        continue;
                    if (l.StartsWith("#"))
                        continue;
                    try
                    {
                        sources.Add(new Uri(l.Trim()));
                        log.Trace("Read URL {0} on line {1}", sources.Last(), lineNum);
                    }
                    catch (UriFormatException)
                    {
                        log.Warn("Unable to parse URL for {0}: {1} (line {2:N0})", nameof(ExternalWebContentSource), l, lineNum);
                    }

                }
            }
            log.Debug("Loaded {0:N0} source URLs.", sources.Count);
            return sources;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            if (_UserAgent.Contains("Terninger/unconfigured"))
            {
                if (!_UnconfiguredUserAgentWarningEmitted)
                    Log.Warn("No user agent is configured. Please be polite to web services and set a unique user agent identifier for your usage of Terninger.");
                _UnconfiguredUserAgentWarningEmitted = true;
            }

            if (!_SourcesInitialised && !_UseRandomSourceForUnitTest)
            {
                Log.Debug("Initialising source list.");
                try
                {
                    if (!String.IsNullOrEmpty(SourcePath))
                        _Sources.AddRange(await LoadUrlListAsync(SourcePath));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unable to open URL Source File '{0}'.", SourcePath);
                }
                if (String.IsNullOrEmpty(SourcePath) && _Sources.Count == 0)
                    _Sources.AddRange(await LoadInternalUrlListAsync());
                if (_Sources.Count == 0)
                    Log.Error("No URLs are available. This entropy source will be disabled.");

                this._UrlsPerSample = Math.Min(_UrlsPerSample, _Sources.Count);
                _Sources.ShuffleInPlace(_Rng);
                _SourcesInitialised = true;
            }
            if (_Sources.Count == 0)
            {
                return null;
            }

            // Note that many of these servers will have similar content and it is publicly accessible.
            // We must mix in some local entropy to ensure differnt computers end up with different entropy.
            // Yes, this reduces the effectiveness of this source, but it will still contribute over time.
            var localEntropy = (await StaticLocalEntropy.Get32()).Concat(CheapEntropy.Get16()).ToArray();

            // Select the servers we will fetch from.
            var serversToSample = new List<ServerFetcher>(_UrlsPerSample);
            for (int i = 0; i < _UrlsPerSample; i++)
            {
                if (_NextSource >= _Sources.Count)
                    _NextSource = 0;
                serversToSample.Add(new ServerFetcher(_Sources[_NextSource], _UserAgent, localEntropy));
                _NextSource = _NextSource + 1;
            }
            if (!serversToSample.Any())
                return null;


            byte[] response;
            if (!_UseRandomSourceForUnitTest)
            {
                // Now fetch from the servers and use the contents, and time to derive entropy.
                var responses = await Task.WhenAll(serversToSample.Select(x => x.ResetAndRun()));
                response = responses.SelectMany(x => x ?? new byte[0]).ToArray();
            }
            else
            {
                // For unit tests, we just get random bytes.
                response = _Rng.GetRandomBytes(_UrlsPerSample * 32);
            }

            return response;
        }

        private class ServerFetcher
        {
            private static readonly ILog Log = LibLog.LogProvider.For<ExternalWebContentSource>();

            public ServerFetcher(Uri url, string userAgent, byte[] staticEntropy)
            {
                this.Url = url;
                this.UserAgent = userAgent;
                this.StaticEntropy = staticEntropy;
            }
            public readonly Uri Url;
            public readonly string UserAgent;
            public readonly byte[] StaticEntropy;
            public int Failures { get; private set; }

            public async Task<byte[]> ResetAndRun()
            {
                // TODO: timeout.
                var hash = SHA256.Create();
                var hc = HttpClientHelpers.Create(userAgent: UserAgent);
                var sw = Stopwatch.StartNew();
                try
                {
                    var responseBytes = await hc.GetByteArrayAsync(Url);
                    sw.Stop();
                    Log.Trace("GET from '{0}' in {1:N2}ms, received {2:N0} bytes", Url, sw.Elapsed.TotalMilliseconds, responseBytes.Length);
                    var result = hash.ComputeHash(
                                    responseBytes
                                    .Concat(BitConverter.GetBytes(sw.ElapsedTicks))
                                    .Concat(StaticEntropy)
                                    .ToArray()
                                );
                    return result;
                }
                catch (Exception ex)
                {
                    Log.WarnException("Exception when trying to GET from {0}", ex, Url);
                    return null;
                }
            }
        }

        public class Configuration
        {
            public static readonly Configuration Default = new Configuration();

            /// <summary>
            /// Number of URLs to sample from the URL list.
            /// Default: 4. Minimum: 1. Maximum: 100.
            /// </summary>
            public int UrlsPerSample { get; set; } = 4;

            /// <summary>
            /// Path to file containing URL list.
            /// If left blank, an internal list is used.
            /// </summary>
            public string UrlFilePath { get; set; }

            /// <summary>
            /// Sample period at normal priority. Default: 15 minutes.
            /// </summary>
            public TimeSpan PeriodNormalPriority { get; set; } = TimeSpan.FromMinutes(15);

            /// <summary>
            /// Sample period at high priority. Default: 5 minutes.
            /// </summary>
            public TimeSpan PeriodHighPriority { get; set; } = TimeSpan.FromMinutes(5);

            /// <summary>
            /// Sample period at low priority. Default: 1 hour.
            /// </summary>
            public TimeSpan PeriodLowPriority { get; set; } = TimeSpan.FromHours(1);
        }
    }
}

