using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.Helpers;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// An entropy source which uses external web content as input, usually from social media or news services.
    /// </summary>
    [NetworkSource]
    public class ExternalWebContentSource : IEntropySource
    {
        public string Name => typeof(ExternalWebContentSource).FullName;

        private IRandomNumberGenerator _Rng;
        private DateTime _NextSampleTimestamp;

        private List<Uri> _Sources;
        public int SourceCount => _Sources.Count;
        private int _NextSource;

        private int _ServersPerSample = 4;                   // This many web requests are made per entropy request.
        public int ServersPerSample => _ServersPerSample;
        private double _DownloadDelayMinutes = 60.0;                // 60 minutes delay after we fetched from all servers, by default.
        public double DownloadDelayMinutes => _DownloadDelayMinutes;

        private bool _UseRandomSourceForUnitTest;

        internal const string DefaultUserAgent = "Microsoft.NET; bitbucket.org/ligos/Terninger; unconfigured";
        private string _UserAgent;


        public ExternalWebContentSource() : this(DefaultUserAgent, 60.0, null, 4, null) { }
        public ExternalWebContentSource(string userAgent) : this(userAgent, 60.0, null, 4, null) { }
        public ExternalWebContentSource(string userAgent, double downloadDelayMinutes) : this(userAgent, downloadDelayMinutes, null, 4, null) { }
        public ExternalWebContentSource(string userAgent, double downloadDelayMinutes, IEnumerable<Uri> sources, int serversPerSample, IRandomNumberGenerator rng)
        {
            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? DefaultUserAgent : userAgent;
            this._DownloadDelayMinutes = downloadDelayMinutes >= 0.0 ? downloadDelayMinutes : 60.0;
            this._Sources = (sources ?? LoadInternalServerList()).ToList();
            this._ServersPerSample = serversPerSample > 0 ? serversPerSample : 4;
            this._ServersPerSample = Math.Min(_ServersPerSample, _Sources.Count);
            this._Rng = rng ?? StandardRandomWrapperGenerator.StockRandom();
            _Sources.ShuffleInPlace(_Rng);
        }

        public void Dispose()
        {
            var asIDisposable = _Rng as IDisposable;
            if (asIDisposable != null)
            {
                asIDisposable.Dispose();
                _Rng = null;
            }
        }

        public async Task<EntropySourceInitialisationResult> Initialise(IEntropySourceConfig config, Func<IRandomNumberGenerator> prngFactory)
        {
            // TODO: check for network connectivity?? Or at least a network interface.

            if (config.IsTruthy("ExternalWebContentSource.Enabled") == false)
                return EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.DisabledByConfig, "ExternalWebContentSource has been disabled in entropy source configuration.");

            // Configurable UserAgent string for all web requests.
            if (config.ContainsKey("Network.UserAgent"))
                _UserAgent = config.Get("Network.UserAgent");


            config.TryParseAndSetDouble("ExternalWebContentSource.DownloadDelayMinutes", ref _DownloadDelayMinutes);
            if (_DownloadDelayMinutes < 0.0 || _DownloadDelayMinutes > 1440.0)
                return EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, new ArgumentOutOfRangeException("ExternalWebContentSource.PeriodMinutes", _DownloadDelayMinutes, "Config item ExternalWebContentSource.DownloadDelayMinutes must be between 0 and 1440 (1 day)"));

            config.TryParseAndSetInt32("ExternalWebContentSource.ServersPerSample", ref _ServersPerSample);
            if (_ServersPerSample <= 0 || _ServersPerSample > 1000)
                return EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, new ArgumentOutOfRangeException("ExternalWebContentSource.ServersPerSample", _ServersPerSample, "Config item ExternalWebContentSource.ServersPerSample must be between 1 and 1000"));


            // Magic unit test source of data (random number generator).
            _UseRandomSourceForUnitTest = config.IsTruthy("ExternalWebContentSource.UseRandomSourceForUnitTests") == true;

            // Load the list of servers.
            var sources = await LoadInternalServerListAsync();
            // TODO: allow this to be configured from any text file.
            if (sources.Count == 0)
                return EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, "No URLs were read from supplied server list file: ExternalWebServerList.txt");
            if (sources.Count < _ServersPerSample)
                return EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, $"ExternalWebContentSource.ServersPerSample is {_ServersPerSample}, but only {sources.Count} servers were read from supplied server list file: ExternalWebServerList.txt");

            _Rng = prngFactory();
            _Sources = sources;
            _Sources.ShuffleInPlace(_Rng);      // Shuffle the order of servers so it is not entirely predictible.
            _NextSampleTimestamp = DateTime.UtcNow;

            return EntropySourceInitialisationResult.Successful();
        }

        private async Task<List<Uri>> LoadInternalServerListAsync()
        {
            var sources = new List<Uri>();
            using (var stream = typeof(ExternalWebContentSource).Assembly.GetManifestResourceStream(typeof(ExternalWebContentSource), "ExternalWebServerList.txt"))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                while (!reader.EndOfStream)
                {
                    var l = await reader.ReadLineAsync();
                    if (String.IsNullOrWhiteSpace(l))
                        continue;
                    if (l.StartsWith("#"))
                        continue;
                    try
                    {
                        sources.Add(new Uri(l.Trim()));
                    }
                    catch (UriFormatException)
                    {
                        // TODO: logging.
                    }

                }
            }
            return sources;
        }
        private List<Uri> LoadInternalServerList()
        {
            return LoadInternalServerListAsync().GetAwaiter().GetResult();
        }

        public async Task<byte[]> GetEntropyAsync()
        {
            // If the source was fetched from recently, we return nothing.
            if (DateTime.UtcNow < this._NextSampleTimestamp)
                return null;

            // Nothing to select from.
            if (_ServersPerSample == 0 || _Sources.Count == 0)
                return null;

            // Note that many of these servers will have similar content and it is publicly accessible.
            // We must mix in some local entropy to ensure differnt computers end up with different entropy.
            // Yes, this reduces the effectiveness of this source, but it will still contribute over time.
            var localEntropy = (await StaticLocalEntropy.Get32()).Concat(CheapEntropy.Get16()).ToArray();

            // Select the servers we will fetch from.
            var serversToSample = new List<ServerFetcher>(_ServersPerSample);
            for (int i = 0; i < _ServersPerSample; i++)
            {
                if (_NextSource >= _Sources.Count)
                {
                    // We fetch from all servers listed, then delay. Rather than delaying between each sample.
                    _NextSource = 0;
                    _NextSampleTimestamp = DateTime.UtcNow.Add(TimeSpan.FromMinutes(_DownloadDelayMinutes));
                    break;
                }
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
                response = responses.SelectMany(x => x).ToArray();
            }
            else
            {
                // For unit tests, we just get random bytes.
                response = _Rng.GetRandomBytes(_ServersPerSample * 32);
            }

            _NextSampleTimestamp = DateTime.UtcNow;
            return response;
        }

        private class ServerFetcher
        {
            public ServerFetcher(Uri url, string userAgent, byte[] staticEntropy)
            {
                this.Url = url;
                this.UserAgent = userAgent;
                this.StaticEntropy = staticEntropy;
            }
            public readonly Uri Url;
            public readonly string UserAgent;
            public readonly byte[] StaticEntropy;

            public Task<byte[]> ResetAndRun()
            {
                // TODO: exception handling??
                // TODO: timeout.
                var hash = SHA256.Create();
                var wc = new WebClient();
                wc.Headers.Add("User-Agent:" + UserAgent);
                var sw = Stopwatch.StartNew();
                return wc.DownloadDataTaskAsync(Url)
                            .ContinueWith(x => {
                                sw.Stop();
                                // TODO: logging of response size and time?
                                var result = hash.ComputeHash(
                                                x.Result
                                                .Concat(BitConverter.GetBytes(sw.ElapsedTicks))
                                                .Concat(StaticEntropy)
                                                .ToArray()
                                            );
                                return result;
                            }
                            , TaskContinuationOptions.ExecuteSynchronously);
            }

        }

    }
}

