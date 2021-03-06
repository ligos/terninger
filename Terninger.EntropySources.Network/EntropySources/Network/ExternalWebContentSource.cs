﻿using System;
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

        private List<Uri> _Sources;
        public int SourceCount => _Sources.Count;
        private int _NextSource;

        private int _ServersPerSample = 4;                   // This many web requests are made per entropy request.
        public int ServersPerSample => _ServersPerSample;

        private bool _UseRandomSourceForUnitTest;
        private string _UserAgent;


        public ExternalWebContentSource() : this(HttpClientHelpers.UserAgentString(), null, TimeSpan.FromMinutes(15.0), 5) { }
        public ExternalWebContentSource(string userAgent) : this(userAgent, null, TimeSpan.FromMinutes(15.0), 5) { }
        public ExternalWebContentSource(string userAgent, IEnumerable<Uri> sources) : this(userAgent, sources, TimeSpan.FromMinutes(5.0), 5) { }
        public ExternalWebContentSource(string userAgent, IEnumerable<Uri> sources, TimeSpan periodNormalPriority) : this(userAgent, sources, periodNormalPriority, 4) { }
        public ExternalWebContentSource(string userAgent, IEnumerable<Uri> sources, TimeSpan periodNormalPriority, int serversPerSample) : this(userAgent, sources, periodNormalPriority, TimeSpan.FromSeconds(10), new TimeSpan(periodNormalPriority.Ticks * 5), serversPerSample, null) { }
        public ExternalWebContentSource(string userAgent, IEnumerable<Uri> sources, TimeSpan periodNormalPriority, TimeSpan periodHighPriority, TimeSpan periodLowPriority, int serversPerSample, IRandomNumberGenerator rng)
            : base(periodNormalPriority, periodHighPriority, periodLowPriority)
        {
            if (serversPerSample <= 0)
                throw new ArgumentOutOfRangeException(nameof(serversPerSample), serversPerSample, "Servers per sample must be at least one.");

            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? HttpClientHelpers.UserAgentString() : userAgent;
            this._Sources = (sources ?? LoadInternalServerList()).ToList();
            if (_Sources.Count <= 0)
                throw new ArgumentOutOfRangeException(nameof(sources), sources, "At least one source URL must be provided.");

            this._ServersPerSample = serversPerSample > 0 ? serversPerSample : 4;
            this._ServersPerSample = Math.Min(_ServersPerSample, _Sources.Count);
            this._Rng = rng ?? StandardRandomWrapperGenerator.StockRandom();
            _Sources.ShuffleInPlace(_Rng);
        }
        internal ExternalWebContentSource(bool useDiskSourceForUnitTests)
            : this(HttpClientHelpers.UserAgentString(), null, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 5, null)
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


        public static async Task<List<Uri>> LoadInternalServerListAsync()
        {
            var log = LibLog.LogProvider.For<ExternalWebContentSource>();
            log.Debug("Loading internal source URL list...");

            var sources = new List<Uri>();
            using (var stream = typeof(ExternalWebContentSource).Assembly.GetManifestResourceStream(typeof(ExternalWebContentSource), "ExternalWebServerList.txt"))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
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
            log.Debug("Loaded {0:N0} source URLs from internal list.", sources.Count);
            return sources;
        }
        public static List<Uri> LoadInternalServerList()
        {
            return LoadInternalServerListAsync().GetAwaiter().GetResult();
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            // Note that many of these servers will have similar content and it is publicly accessible.
            // We must mix in some local entropy to ensure differnt computers end up with different entropy.
            // Yes, this reduces the effectiveness of this source, but it will still contribute over time.
            var localEntropy = (await StaticLocalEntropy.Get32()).Concat(CheapEntropy.Get16()).ToArray();

            // Select the servers we will fetch from.
            var serversToSample = new List<ServerFetcher>(_ServersPerSample);
            for (int i = 0; i < _ServersPerSample; i++)
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
                response = _Rng.GetRandomBytes(_ServersPerSample * 32);
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
    }
}

