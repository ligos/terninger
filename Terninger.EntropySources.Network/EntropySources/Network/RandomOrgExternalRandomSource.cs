using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.LibLog;

namespace MurrayGrant.Terninger.EntropySources.Network
{
    /// <summary>
    /// An entropy source which uses https://random.org as input.
    /// Either via a public interface or an API with a free key.
    /// Rate limits of ~250k bits or 1000 requests per day, so use large chunks with 8 hour timouts.
    /// </summary>
    [AsyncHint(IsAsync.Always)]
    public class RandomOrgExternalRandomSource : EntropySourceWithPeriod
    {
        public override string Name { get; set; }

        private readonly string _UserAgent;
        private readonly string _ApiKey;
        private readonly int _BytesPerRequest;
        private readonly bool _UseDiskSourceForUnitTests;

        public RandomOrgExternalRandomSource(string userAgent, Configuration config)
            : this(
                  userAgent:            userAgent,
                  apiKey:               config?.ApiKey,
                  bytesPerRequest:      config?.BytesPerRequest      ?? Configuration.Default.BytesPerRequest,
                  periodNormalPriority: config?.PeriodNormalPriority ?? Configuration.Default.PeriodNormalPriority,
                  periodHighPriority:   config?.PeriodHighPriority   ?? Configuration.Default.PeriodHighPriority,
                  periodLowPriority:    config?.PeriodLowPriority    ?? Configuration.Default.PeriodLowPriority
            )
        { }
        public RandomOrgExternalRandomSource(string userAgent = null, int? bytesPerRequest = null, string apiKey = null, TimeSpan? periodNormalPriority = null, TimeSpan? periodHighPriority = null, TimeSpan? periodLowPriority = null)
            : base(periodNormalPriority.GetValueOrDefault(Configuration.Default.PeriodNormalPriority), 
                  periodHighPriority.GetValueOrDefault(Configuration.Default.PeriodHighPriority), 
                  periodLowPriority.GetValueOrDefault(Configuration.Default.PeriodLowPriority))
        {
            this._BytesPerRequest = bytesPerRequest.GetValueOrDefault(Configuration.Default.BytesPerRequest);
            if (bytesPerRequest < 4 || bytesPerRequest > 4096)
                throw new ArgumentOutOfRangeException(nameof(bytesPerRequest), bytesPerRequest, "Bytes per request must be between 4 and 4096");

            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? HttpClientHelpers.UserAgentString() : userAgent;
            this._ApiKey = apiKey;
        }
        internal RandomOrgExternalRandomSource(bool useDiskSourceForUnitTests, string apiKey)
            : base(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)
        {
            this._UserAgent = HttpClientHelpers.UserAgentString();
            this._UseDiskSourceForUnitTests = useDiskSourceForUnitTests;
            this._ApiKey = apiKey;
        }

        protected override Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            if (String.IsNullOrEmpty(_ApiKey))
                return GetPublicEntropyAsync(priority);
            else
                return GetApiEntropyAsync(priority);
        }

        private async Task<byte[]> GetPublicEntropyAsync(EntropyPriority priority)
        {
            // https://random.org

            Log.Trace("Beginning to gather entropy.");

            // Fetch data.
            var response = "";
            var sw = Stopwatch.StartNew();
            if (!_UseDiskSourceForUnitTests)
            {
                var apiUri = new Uri("https://www.random.org/cgi-bin/randbyte?nbytes=" + _BytesPerRequest + "&format=h");
                var hc = HttpClientHelpers.Create(userAgent: _UserAgent);
                try
                {
                    response = await hc.GetStringAsync(apiUri);
                }
                catch (Exception ex)
                {
                    Log.Warn(ex, "Unable to GET from {0}", apiUri);
                    return null;
                }
                Log.Trace("Read {0:N0} characters of html in {1:N2}ms.", response.Length, sw.Elapsed.TotalMilliseconds);
            }
            else
            {
                using (var stream = File.OpenRead(HttpClientHelpers._BasePathToUnitTestData + "www.random.org.html"))
                {
                    response = await new StreamReader(stream).ReadToEndAsync();
                }
            }
            sw.Stop();


            // The entire content is random hex bytes.
            // Albeit with a bunch of whitespace.
            var randomString = response.Replace("\r", "").Replace("\n", "").Replace(" ", "");

            var randomBytes = randomString.ParseFromHexString()
                                .Concat(BitConverter.GetBytes(unchecked((uint)sw.Elapsed.Ticks)))      // Don't forget to include network timing!
                                .ToArray();
            Log.Trace("Read {0:N0} bytes of entropy (including 4 bytes of timing info).", randomBytes.Length);

            return randomBytes;
        }
        private async Task<byte[]> GetApiEntropyAsync(EntropyPriority priority)
        {
            // http://www.random.org/

            // https://api.random.org/json-rpc/2/introduction
            // https://api.random.org/json-rpc/2/basic
            // https://api.random.org/json-rpc/2/request-builder

            // Fetch data.
            var response = "";
            var sw = Stopwatch.StartNew();
            if (!_UseDiskSourceForUnitTests)
            {
                var apiUri = new Uri("https://api.random.org/json-rpc/2/invoke");
                var hc = HttpClientHelpers.Create(userAgent: _UserAgent);
                var requestBody = "{\"jsonrpc\":\"2.0\",\"method\":\"generateBlobs\",\"params\":{\"apiKey\":\"" + _ApiKey + "\",\"n\":1,\"size\":" + (_BytesPerRequest * 8) + ",\"format\":\"base64\"},\"id\":1}";
                try
                {
                    response = await hc.PostStringAsync(apiUri, requestBody, "application/json");
                }
                catch (Exception ex)
                {
                    Log.Warn(ex, "Unable to POST to {0} with body {1}", apiUri, requestBody);
                    return null;
                }
                Log.Trace("Read {0:N0} characters of html in {1:N2}ms.", response.Length, sw.Elapsed.TotalMilliseconds);
            }
            else
            {
                using (var stream = File.OpenRead(HttpClientHelpers._BasePathToUnitTestData + "api.random.org.html"))
                {
                    response = await new StreamReader(stream).ReadToEndAsync();
                }
            }
            sw.Stop();

            // To avoid using dynamic or a Json library, we do hacky string parsing!

            // Check for error.
            if (response.IndexOf("\"error\":") != -1)
            {
                Log.Error("Random.org API returned error result. Full result in next message.");
                Log.Error(response);
                return null;
            }
            int dataIdx = response.IndexOf("\"data\":[\"");
            if (dataIdx == -1)
            {
                Log.Error("Cannot locate random result in random.org API result: source will return nothing. Actual result in next message.");
                Log.Error(response);
                return null;
            }
            dataIdx = dataIdx + "\"data\":[\"".Length;
            int endIdx = response.IndexOf("\"]", dataIdx);
            if (endIdx == -1)
            {
                Log.Error("Cannot locate end of random result in random.org API result: source will return nothing. Actual result in next message.");
                Log.Error(response);
                return null;
            }
            Log.Trace("Parsed Json result.");

            // Trim and parse.
            var randomString = response.Substring(dataIdx, endIdx - dataIdx).Trim();
            var randomBytes = Convert.FromBase64String(randomString)
                    .Concat(BitConverter.GetBytes(unchecked((uint)sw.Elapsed.Ticks)))      // Don't forget to include network timing!
                    .ToArray();
            Log.Trace("Read {0:N0} bytes of entropy (including 4 bytes of timing info).", randomBytes.Length);

            return randomBytes;
        }

        public class Configuration
        {
            public static readonly Configuration Default = new Configuration();

            /// <summary>
            /// Optional API key for the service.
            /// </summary>
            public string ApiKey { get; set; }

            /// <summary>
            /// Bytes returned per request / sample. 
            /// Default: 128. Minimum: 4. Maximum: 4096.
            /// </summary>
            public int BytesPerRequest { get; set; } = 128;

            /// <summary>
            /// Sample period at normal priority. Default: 8 hours.
            /// </summary>
            public TimeSpan PeriodNormalPriority { get; set; } = TimeSpan.FromHours(8);

            /// <summary>
            /// Sample period at high priority. Default: 2 minutes.
            /// </summary>
            public TimeSpan PeriodHighPriority { get; set; } = TimeSpan.FromMinutes(2);

            /// <summary>
            /// Sample period at low priority. Default: 32 hours.
            /// </summary>
            public TimeSpan PeriodLowPriority { get; set; } = TimeSpan.FromHours(32);
        }
    }
}
