using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.Helpers;
using System.Diagnostics;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// An entropy source which uses external random number generator services as input.
    /// </summary>
    [NetworkSource]
    public class ExternalServerRandomSource : IEntropySource
    {
        public string Name => typeof(ExternalServerRandomSource).FullName;

        private IRandomNumberGenerator _Rng;
        private List<EntropyGetter> _ServerSources;
        private int _NextSource;

        private bool _UseDiskSourceForUnitTests;

        private string _UserAgent = "Microsoft.NET; bitbucket.org/ligos/Terninger; unconfigured";

        // https://en.wikipedia.org/wiki/List_of_random_number_generators


        public ExternalServerRandomSource() : this(ExternalWebContentSource.DefaultUserAgent, Guid.Empty, "", null) { }
        public ExternalServerRandomSource(string userAgent) : this(userAgent, Guid.Empty, "", null) { }
        public ExternalServerRandomSource(string userAgent, Guid randomOrgApiKey, string hotBitsApiKey) : this(userAgent, randomOrgApiKey, hotBitsApiKey, null) { }
        public ExternalServerRandomSource(string userAgent, Guid randomOrgApiKey, string hotBitsApiKey, IRandomNumberGenerator rng) : this(userAgent, true, true, true, true, true, true, randomOrgApiKey, hotBitsApiKey, rng) { }
        public ExternalServerRandomSource(string userAgent, bool enableAnu, bool enableRandomNumbersInfo, bool enableRandomServer, bool enableBeaconNist, bool enableRandomOrg, bool enableHotBits, Guid randomOrgApiKey, string hotBitsApiKey, IRandomNumberGenerator rng)
        {
            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? ExternalWebContentSource.DefaultUserAgent : userAgent;
            var sources = new List<EntropyGetter>();
            if (enableAnu)
                sources.Add(new AnuGetter());
            if (enableRandomNumbersInfo)
                sources.Add(new RandomNumbersInfoGetter());
            if (enableRandomServer)
                sources.Add(new RandomServerGetter());
            if (enableBeaconNist)
                sources.Add(new BeaconNistGetter());
            if (enableRandomOrg && randomOrgApiKey != Guid.Empty)
                sources.Add(new RandomOrgApiGetter(randomOrgApiKey));
            else if (enableRandomOrg && randomOrgApiKey == Guid.Empty)
                sources.Add(new RandomOrgPublicGetter());
            if (enableHotBits && !String.IsNullOrWhiteSpace(hotBitsApiKey))
                sources.Add(new HotbitsGetter(hotBitsApiKey));
            else if (enableHotBits && String.IsNullOrWhiteSpace(hotBitsApiKey))
                sources.Add(new HotbitsGetter());
            this._ServerSources = sources;
            this._Rng = rng ?? StandardRandomWrapperGenerator.StockRandom();
            _ServerSources.ShuffleInPlace(_Rng);
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

        public Task<EntropySourceInitialisationResult> Initialise(IEntropySourceConfig config, Func<IRandomNumberGenerator> prngFactory)
        {
            // TODO: check for network connectivity?? Or at least a network interface.

            if (config.IsTruthy("ExternalServerRandomSource.Enabled") == false)
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.DisabledByConfig, "ExternalServerRandomSource has been disabled in entropy source configuration."));

            // Configurable UserAgent string for all web requests.
            if (config.ContainsKey("Network.UserAgent"))
                _UserAgent = config.Get("Network.UserAgent");

            // Magic unit test source of data (prefetched responses from disk).
            _UseDiskSourceForUnitTests = config.IsTruthy("ExternalServerRandomSource.UseDiskSourceForUnitTests") == true;

            // Source order is randomised (default to randomised).
            if (config.IsTruthy("ExternalServerRandomSource.RandomiseSourceOrder") != false)
                _Rng = prngFactory();

            _ServerSources = new List<EntropyGetter>();
            
            // All sources default to enabled.
            // These have no additional configuration.
            if (config.IsTruthy("ExternalServerRandomSource.AnuEnabled") != false)
                _ServerSources.Add(new AnuGetter());
            if (config.IsTruthy("ExternalServerRandomSource.RandomNumbersInfoEnabled") != false)
                _ServerSources.Add(new RandomNumbersInfoGetter());
            if (config.IsTruthy("ExternalServerRandomSource.RandomServerEnabled") != false)
                _ServerSources.Add(new RandomServerGetter());
            if (config.IsTruthy("ExternalServerRandomSource.BeaconNistEnabled") != false)
                _ServerSources.Add(new BeaconNistGetter());

            // If we have a random.org API key, we use the API version (which lets us download more randomness).
            var maybeRandomOrgApiKey = config.Get("ExternalServerRandomSource.RandomOrgApiKey");
            Guid randomOrgApiKey = Guid.Empty;
            if (!String.IsNullOrWhiteSpace(maybeRandomOrgApiKey) && !Guid.TryParse(maybeRandomOrgApiKey.Trim(), out randomOrgApiKey))
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.InvalidConfig, new ArgumentException($"ExternalServerRandomSource.RandomOrgApiKey should be a guid, but was '{maybeRandomOrgApiKey}'.")));

            if (randomOrgApiKey != Guid.Empty && config.IsTruthy("ExternalServerRandomSource.RandomOrgApiEnabled") != false)
                _ServerSources.Add(new RandomOrgApiGetter(randomOrgApiKey));
            else if (config.IsTruthy("ExternalServerRandomSource.RandomOrgApiEnabled") != false)
                _ServerSources.Add(new RandomOrgPublicGetter());

            // We also need an API key for the HotBits true random source.
            // As there is benefit in both of these, they can both be enabled.
            var hotBitsApiKey = config.Get("ExternalServerRandomSource.HotBitsApiKey");
            if (!String.IsNullOrWhiteSpace(hotBitsApiKey) && config.IsTruthy("ExternalServerRandomSource.HotBitsPseudoRandomEnabled") != false)
                _ServerSources.Add(new HotbitsGetter(hotBitsApiKey));
            if (config.IsTruthy("ExternalServerRandomSource.HotBitsTrueRandomEnabled") != false)
                _ServerSources.Add(new HotbitsGetter());


            if (_ServerSources.Count <= 0)
                // All disabled!
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.DisabledByConfig, "ExternalServerRandomSource has all source servers disabled in config."));

            // Shuffle the source so they aren't entirely predictable.
            if (_Rng != null)
                _ServerSources.ShuffleInPlace(_Rng);
            _NextSource = 0;

            return Task.FromResult(EntropySourceInitialisationResult.Successful());
        }

        public Task<byte[]> GetEntropyAsync()
        {
            // Nothing to select from.
            if (_ServerSources.Count == 0)
                return null;

            // Iterate each source in turn.
            if (_NextSource >= _ServerSources.Count)
            {
                // Shuffle every time we work through the sources; each source is returned in equal weight, but their order varies.
                _ServerSources.ShuffleInPlace(_Rng);
                _NextSource = 0;
            }

            var source = _ServerSources[_NextSource];
            _NextSource = _NextSource + 1;
            return source.GetEntropy(this);
        }

        
        
        // Basic inheritance so each getter has a LastFetch field.
        private abstract class EntropyGetter
        {
            public abstract string Name { get; }
            public abstract Task<byte[]> GetEntropy(ExternalServerRandomSource parent);

            protected DateTime NextFetchUtc;
            protected TimeSpan _TimeBeforeNextFetch = TimeSpan.FromDays(3);        // Very conservitive by default.
        }

        #region random.org (public)
        private class RandomOrgPublicGetter : EntropyGetter
        {
            public RandomOrgPublicGetter() : base()
            {
                _TimeBeforeNextFetch = TimeSpan.FromHours(12);      // No offical rate limit, but they don't like it when you suck lots of data from the API verson.
            }
            public override string Name => "https://random.org";
            public override async Task<byte[]> GetEntropy(ExternalServerRandomSource parent)
            {
                // https://random.org

                // If the source was fetched from recently, we return nothing.
                if (DateTime.UtcNow < this.NextFetchUtc)
                    return null;

                // Fetch data.
                var response = "";
                int numberOfBytes = 64;
                var sw = Stopwatch.StartNew();
                if (!parent._UseDiskSourceForUnitTests)
                {
                    var apiUri = new Uri("https://www.random.org/cgi-bin/randbyte?nbytes=" + numberOfBytes + "&format=h");
                    var wc = new WebClient();
                    wc.Headers.Add(parent._UserAgent);
                    response = await wc.DownloadStringTaskAsync(apiUri);
                }
                else
                {
                    using (var stream = File.OpenRead("../../Online Generators/www.random.org.html"))
                    {
                        response = await new StreamReader(stream).ReadToEndAsync();
                    }
                }
                sw.Stop();


                // The entire content is random hex bytes.
                // Albeit with a bunch of whitespace.
                var randomString = response.Replace("\r", "").Replace("\n", "").Replace(" ", "");

                var randomBytes = randomString.ParseFromHexString();
                NextFetchUtc = DateTime.UtcNow.Add(_TimeBeforeNextFetch);
                return randomBytes;
            }
        }
        #endregion
        #region random.org (API)
        private class RandomOrgApiGetter : EntropyGetter
        {
            private Guid _ApiKey;
            public RandomOrgApiGetter() : this(Guid.Empty) { }
            public RandomOrgApiGetter(Guid apiKey) : base()
            {
                _TimeBeforeNextFetch = TimeSpan.FromHours(12);      // Rates limits to ~250k bits or 1000 request per day. So ask for large chunks infrequently.
                _ApiKey = apiKey;
            }
            public override string Name => "https://random.org (API)";
            public override async Task<byte[]> GetEntropy(ExternalServerRandomSource parent)
            {
                // http://www.random.org/

                // https://api.random.org/json-rpc/1/introduction
                // https://api.random.org/json-rpc/1/basic
                // https://api.random.org/json-rpc/1/request-builder

                // No API key, no randomness.
                if (_ApiKey == Guid.Empty)
                    return null;

                // If the source was fetched from recently, we return nothing.
                if (DateTime.UtcNow < this.NextFetchUtc)
                    return null;

                // Fetch data.
                var response = "";
                int numberOfBytes = 128;
                var sw = Stopwatch.StartNew();
                if (!parent._UseDiskSourceForUnitTests)
                {
                    throw new NotImplementedException();
                    var apiUri = new Uri("https://www.random.org/cgi-bin/randbyte?nbytes=64&format=h");
                    var wc = new WebClient();
                    wc.Headers.Add(parent._UserAgent);
                    response = await wc.DownloadStringTaskAsync(apiUri);

                    // Request
                    // {"jsonrpc":"2.0","method":"generateBlobs","params":{"apiKey":"0a42c40b-41df-490d-a594-654313696d3f","n":1,"size":128,"format":"base64"},"id":1}
                    // Response
                    // {"jsonrpc":"2.0","result":{"random":{"data":["jjyO3rthoqEx6KwyEWL99A=="],"completionTime":"2017-11-19 09:46:44Z"},"bitsUsed":128,"bitsLeft":249872,"requestsLeft":999,"advisoryDelay":90},"id":1}


                    //var body = new
                    //{
                    //    jsonrpc = "2.0",
                    //    method = "generateBlobs",
                    //    @params = new
                    //    {
                    //        apiKey = apiKey.ToString("D"),
                    //        n = 1,
                    //        size = numberOfBytes * 8,
                    //        format = "base64",
                    //    },
                    //    id = 1,
                    //};
                    //var randomOrgApi = new Uri("https://api.random.org/json-rpc/1/invoke");
                    //var wc = new WebClient();
                    //wc.Headers.Add("Automatic", "makemeapassword@ligos.net");
                    //wc.Headers.Add("Content-Type", "application/json-rpc");
                    //wc.Headers.Add("User-Agent", "Microsoft.NET; makemeapassword.org; makemeapassword@ligos.net");
                    //var bodyAsString = JsonConvert.SerializeObject(body, Formatting.None);
                    //var rawResult = wc.UploadString(randomOrgApi, "POST", bodyAsString);

                }
                else
                {
                    throw new NotImplementedException();
                    using (var stream = File.OpenRead("../../Online Generators/www.random.org.html"))
                    {
                        response = await new StreamReader(stream).ReadToEndAsync();
                    }
                }
                sw.Stop();


                
                //dynamic jsonResult = JsonConvert.DeserializeObject(rawResult);
                //if (jsonResult.error != null)
                //    throw new Exception(String.Format("Random.org error: {0} - {1}", (string)jsonResult.error.code, (string)jsonResult.error.message));
                //var randomBase64 = (string)jsonResult.result.random.data[0];
                //var randomOrgData = Convert.FromBase64String(randomBase64);


            }
        }
        #endregion

        #region qrng.anu.edu.au
        private class AnuGetter : EntropyGetter
        {
            public AnuGetter() :base()
            {
                _TimeBeforeNextFetch = TimeSpan.FromHours(12);      // No rate limits on this, but it also returns alot of data so we query infrequently.
            }
            public override string Name => "https://qrng.anu.edu.au";
            public override async Task<byte[]> GetEntropy(ExternalServerRandomSource parent)
            {
                // http://qrng.anu.edu.au/index.php

                // If the source was fetched from recently, we return nothing.
                if (DateTime.UtcNow < this.NextFetchUtc)
                    return null;

                // Fetch data.
                // This always returns 1024 bytes!!
                var response = "";
                var sw = Stopwatch.StartNew();
                if (!parent._UseDiskSourceForUnitTests)
                {
                    var apiUri = new Uri("https://qrng.anu.edu.au/RawHex.php");
                    var wc = new WebClient();
                    wc.Headers.Add(parent._UserAgent);
                    response = await wc.DownloadStringTaskAsync(apiUri);
                }
                else
                {
                    using (var stream = File.OpenRead("../../Online Generators/qrng.anu.edu.au-RawHex.php.html"))
                    {
                        response = await new StreamReader(stream).ReadToEndAsync();
                    }
                }
                sw.Stop();


                // Locate some pretty clear boundaries around the random numbers returned.
                var startIdx = response.IndexOf("1024 bytes of randomness in hexadecimal form", StringComparison.OrdinalIgnoreCase);
                if (startIdx == -1)
                    throw new Exception("TODO LOGGING Cannot locate start string in html parsing of anu result.");
                startIdx = response.IndexOf("<td>", startIdx, StringComparison.OrdinalIgnoreCase) + "<td>".Length;
                if (startIdx == -1)
                    throw new Exception("TODO LOGGING Cannot locate start string in html parsing of anu result.");
                var endIdx = response.IndexOf("</td>", startIdx, StringComparison.OrdinalIgnoreCase);
                if (endIdx == -1)
                    throw new Exception("Cannot locate end string in html parsing of anu result.");
                var randomString = response.Substring(startIdx, endIdx - startIdx).Trim();

                var randomBytes = randomString.ParseFromHexString();
                NextFetchUtc = DateTime.UtcNow.Add(_TimeBeforeNextFetch);
                return randomBytes;
            }
        }
        #endregion

        #region www.randomserver.dyndns.org
        private class RandomServerGetter : EntropyGetter
        {
            public RandomServerGetter() : base()
            {
                _TimeBeforeNextFetch = TimeSpan.FromHours(12);      // No idea about rate limits.
            }
            public override string Name => "http://www.randomserver.dyndns.org";
            public override async Task<byte[]> GetEntropy(ExternalServerRandomSource parent)
            {
                // http://www.randomserver.dyndns.org/client/random.php

                // If the source was fetched from recently, we return nothing.
                if (DateTime.UtcNow < this.NextFetchUtc)
                    return null;

                // Fetch data.
                byte[] response;
                int numberOfBytes = 64;
                var sw = Stopwatch.StartNew();
                if (!parent._UseDiskSourceForUnitTests)
                {
                    var apiUri = new Uri("http://www.randomserver.dyndns.org/client/random.php?type=BIN&a=1&b=10&n=" + numberOfBytes + "&file=0");
                    var wc = new WebClient();
                    wc.Headers.Add(parent._UserAgent);
                    response = await wc.DownloadDataTaskAsync(apiUri);
                }
                else
                {
                    using (var stream = File.OpenRead("../../Online Generators/randomserver.dyndns.org.dat"))
                    {
                        response = new byte[64];
                        await stream.ReadAsync(response, 0, numberOfBytes);
                    }
                }
                sw.Stop();


                // This is a binary file: no further parsing or processing is needed.
                var randomBytes = response;
                NextFetchUtc = DateTime.UtcNow.Add(_TimeBeforeNextFetch);
                return randomBytes;
            }
        }
        #endregion

        #region Hotbits 
        private class HotbitsGetter : EntropyGetter
        {
            private string _ApiKey;

            public HotbitsGetter() : this("") { }
            public HotbitsGetter(string apiKey) : base()
            {
                if (String.IsNullOrWhiteSpace(apiKey))
                    _TimeBeforeNextFetch = TimeSpan.FromHours(6);      // No rate limits for pseudo random data.
                else
                    _TimeBeforeNextFetch = TimeSpan.FromHours(24);      // True random data is much more limited.
                _ApiKey = apiKey;
            }
            public override string Name => "http://www.fourmilab.ch/hotbits/ " + (String.IsNullOrWhiteSpace(this._ApiKey) ? "(pseudo random)" : "(true random)");
            public override async Task<byte[]> GetEntropy(ExternalServerRandomSource parent)
            {
                // http://www.fourmilab.ch/hotbits/

                // If the source was fetched from recently, we return nothing.
                if (DateTime.UtcNow < this.NextFetchUtc)
                    return null;

                // If we have an api key, we will ask for a smaller number of true random bits.
                int numberOfBytes;
                string pseudoSource, apiKey;
                if (String.IsNullOrWhiteSpace(_ApiKey))
                {
                    numberOfBytes = 128;
                    pseudoSource = "&pseudo=pseudo";
                    apiKey = "&apikey=";
                }
                else
                {
                    throw new NotImplementedException();
                    numberOfBytes = 64;
                    pseudoSource = "";
                    apiKey = "&apikey=" + _ApiKey;
                }

                // Fetch data.
                var response = "";
                var sw = Stopwatch.StartNew();
                if (!parent._UseDiskSourceForUnitTests)
                {
                    var apiUri = new Uri("https://www.fourmilab.ch/cgi-bin/Hotbits.api?nbytes=" + numberOfBytes + "&fmt=hex&npass=1&lpass=8&pwtype=3" + apiKey + pseudoSource);
                    var wc = new WebClient();
                    wc.Headers.Add(parent._UserAgent);
                    response = await wc.DownloadStringTaskAsync(apiUri);
                }
                else
                {
                    using (var stream = File.OpenRead("../../Online Generators/hotbits.html"))
                    {
                        response = await new StreamReader(stream).ReadToEndAsync();
                    }
                }
                sw.Stop();


                // Locate some pretty clear boundaries around the random numbers returned.
                var startIdx = response.IndexOf("<pre>", StringComparison.OrdinalIgnoreCase) + "<pre>".Length;
                if (startIdx == -1)
                    throw new Exception("TODO LOGGING Cannot locate start string in html parsing of hotbits result.");
                var endIdx = response.IndexOf("</pre>", startIdx, StringComparison.OrdinalIgnoreCase);
                if (endIdx == -1)
                    throw new Exception("Cannot locate end string in html parsing of hotbits result.");
                var randomString = response.Substring(startIdx, endIdx - startIdx).Trim().Replace("\r", "").Replace("\n", "");

                var randomBytes = randomString.ParseFromHexString();
                NextFetchUtc = DateTime.UtcNow.Add(_TimeBeforeNextFetch);
                return randomBytes;
            }
        }
        #endregion

        #region beacon.nist.gov
        private class BeaconNistGetter : EntropyGetter
        {
            public BeaconNistGetter() :base()
            {
                _TimeBeforeNextFetch = TimeSpan.FromHours(4);      // No rate limits on this.
            }
            public override string Name => "https://beacon.nist.gov/";
            public override async Task<byte[]> GetEntropy(ExternalServerRandomSource parent)
            {
                // https://beacon.nist.gov/
                // Note that this will return the same result for 60 second period.
                // We must mix in some local entropy to ensure differnt computers end up with different entropy.
                // Yes, this reduces the effectiveness of this source, but it will still contribute over time.

                // If the source was fetched from recently, we return nothing.
                if (DateTime.UtcNow < this.NextFetchUtc)
                    return null;

                // Fetch data.
                var response = "";
                var sw = Stopwatch.StartNew();
                if (!parent._UseDiskSourceForUnitTests)
                {
                    var apiUri = new Uri("https://beacon.nist.gov/rest/record/last");
                    var wc = new WebClient();
                    wc.Headers.Add(parent._UserAgent);
                    response = await wc.DownloadStringTaskAsync(apiUri);
                }
                else
                {
                    using (var stream = File.OpenRead("../../Online Generators/beacon.nist.gov-last.xml"))
                    {
                        response = await new StreamReader(stream).ReadToEndAsync();
                    }
                }
                sw.Stop();

                var localEntropy = (await StaticLocalEntropy.Get32()).Concat(CheapEntropy.Get16()).ToArray();

                // Parse out the useful parts of the response. 
                // Keeping away from XML parsing to minimise dependencies. At least for now.

                // The first two return 64 random bytes each, the signature is 256 bytes. All are hashed to 64 bytes when combined with local entropy.
                var lastOutputBytes = GetWithinXmlTags(response, "previousOutputValue").ParseFromHexString();
                var outputValueBytes = GetWithinXmlTags(response, "outputValue").ParseFromHexString();
                var signatureBytes = GetWithinXmlTags(response, "signatureValue").ParseFromHexString();

                // Mix in some local entropy.
                var hasher = SHA512.Create();
                var result = hasher.ComputeHash(lastOutputBytes.Concat(localEntropy).ToArray())
                            .Concat(hasher.ComputeHash(outputValueBytes.Concat(localEntropy).ToArray()))
                            .Concat(hasher.ComputeHash(signatureBytes.Concat(localEntropy).ToArray()))
                            .ToArray();

                NextFetchUtc = DateTime.UtcNow.Add(_TimeBeforeNextFetch);
                return result;
            }
            private string GetWithinXmlTags(string xml, string tag)
            {
                var startTag = "<" + tag + ">";
                var endTag = "</" + tag + ">";
                int startIdx = xml.IndexOf(startTag) + startTag.Length;
                int endIdx = xml.IndexOf(endTag, startIdx);
                var result = xml.Substring(startIdx, endIdx - startIdx);
                return result;
            }
        }
        #endregion

        #region www.randomnumbers.info
        private class RandomNumbersInfoGetter : EntropyGetter
        {
            public RandomNumbersInfoGetter()
            {
                _TimeBeforeNextFetch = TimeSpan.FromHours(12);      // No rate limits on this.
            }
            public override string Name => "http://www.randomnumbers.info/";
            public override async Task<byte[]> GetEntropy(ExternalServerRandomSource parent)
            {
                // This supports SSL, but the cert isn't valid (it's for the uni, rather than the correct domain).
                // http://www.randomnumbers.info/content/Download.htm
                // This returns HTML, which means I'm doing some hacky parsing here.
                const int rangeOfNumbers = 4096 - 1;      // 12 bits per number (1.5 bytes).
                const int numberOfNumbers = 256;
                const int numberOfBytes = numberOfNumbers * 2;          // We waste 4 bits per number (for a total of 512 bytes).

                // If the source was fetched from recently, we return nothing.
                if (DateTime.UtcNow < this.NextFetchUtc)
                    return null;

                // Fetch data.
                var response = "";
                if (!parent._UseDiskSourceForUnitTests)
                {
                    var apiUri = new Uri("http://www.randomnumbers.info/cgibin/wqrng.cgi?amount=" + numberOfNumbers.ToString() + "&limit=" + rangeOfNumbers.ToString());
                    var wc = new WebClient();
                    wc.Headers.Add(parent._UserAgent);
                    response = await wc.DownloadStringTaskAsync(apiUri);
                }
                else
                {
                    using (var stream = File.OpenRead("../../Online Generators/www.randomnumbers.info.html"))
                    {
                        response = await new StreamReader(stream).ReadToEndAsync();
                    }
                }
                
                // Locate some pretty clear boundaries around the random numbers returned.
                var startIdx = response.IndexOf("Download random numbers from quantum origin", StringComparison.OrdinalIgnoreCase);
                if (startIdx == -1)
                    throw new Exception("TODO LOGGING Cannot locate start string in html parsing of randomnumbers.info result.");
                startIdx = response.IndexOf("<hr>", startIdx, StringComparison.OrdinalIgnoreCase);
                if (startIdx == -1)
                    throw new Exception("TODO LOGGING Cannot locate start string in html parsing of randomnumbers.info result.");
                var endIdx = response.IndexOf("</td>", startIdx, StringComparison.OrdinalIgnoreCase);
                if (endIdx == -1)
                    throw new Exception("Cannot locate end string in html parsing of randomnumbers.info result.");
                var haystack = response.Substring(startIdx, endIdx - startIdx);
                var numbersAndOtherJunk = haystack.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);      // Numbers are space separated.
                var numbers = numbersAndOtherJunk
                                .Where(x => x.All(Char.IsDigit))        // Remove non-numeric junk.
                                .Select(x => Int16.Parse(x))            // Parse to an int16.
                                .ToList();
                var result = new byte[numberOfBytes];
                for (int i = 0; i < numbers.Count; i++)
                {
                    // Take the Int16s in the range 0..4095 (4096 possibilities) and write them into the result array.
                    // The top 4 bits will always be empty, but that doesn't matter too much as they will be hashed when added to a Pool.
                    // This means only 75% of bits are truly random, so 16 bytes is only equivalent to 12 bytes.
                    // TODO: some bit bashing to pack things more efficiently
                    var twoBytes = BitConverter.GetBytes(numbers[i]);
                    result[i * 2] = twoBytes[0];
                    result[(i * 2) + 1] = twoBytes[1];
                }

                NextFetchUtc = DateTime.UtcNow.Add(_TimeBeforeNextFetch);
                return result;
            }
        }
        #endregion
    }
}
