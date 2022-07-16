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
    /// An entropy source which uses https://quantumnumbers.anu.edu.au as input.
    /// This requires an api key and has a free limit of 100 requests per month.
    /// As we get up to 1kB per request, we use 12 hours as the normal polling period.
    /// </summary>
    [AsyncHint(IsAsync.Always)]
    public class AnuExternalRandomSource : EntropySourceWithPeriod
    {
        public override string Name { get; set; }

        private readonly int _BytesPerRequest;
        private readonly string _UserAgent;
        private readonly string _ApiKey;
        private bool _ApiWarningEmitted;

        private readonly bool _UseDiskSourceForUnitTests;

        public AnuExternalRandomSource(string userAgent, Configuration config)
            : this(
                  userAgent:            userAgent,
                  apiKey:               config?.ApiKey,
                  bytesPerRequest:      config?.BytesPerRequest      ?? Configuration.Default.BytesPerRequest,
                  periodNormalPriority: config?.PeriodNormalPriority ?? Configuration.Default.PeriodNormalPriority,
                  periodHighPriority:   config?.PeriodHighPriority   ?? Configuration.Default.PeriodHighPriority,
                  periodLowPriority:    config?.PeriodLowPriority    ?? Configuration.Default.PeriodLowPriority
            )
        { }
        public AnuExternalRandomSource(string apiKey, string userAgent = null, int? bytesPerRequest = null, TimeSpan? periodNormalPriority = null, TimeSpan? periodHighPriority = null, TimeSpan? periodLowPriority = null)
            : base(periodNormalPriority.GetValueOrDefault(Configuration.Default.PeriodNormalPriority), 
                  periodHighPriority.GetValueOrDefault(Configuration.Default.PeriodHighPriority), 
                  periodLowPriority.GetValueOrDefault(Configuration.Default.PeriodLowPriority))
        {
            this._BytesPerRequest = bytesPerRequest.GetValueOrDefault(Configuration.Default.BytesPerRequest);
            if (_BytesPerRequest < 1 || _BytesPerRequest > 1024)
                throw new ArgumentOutOfRangeException(nameof(bytesPerRequest), bytesPerRequest, "Bytes per request must be between 1 and 1024");

            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? HttpClientHelpers.UserAgentString() : userAgent;
            this._ApiKey = apiKey;
        }
        internal AnuExternalRandomSource(bool useDiskSourceForUnitTests)
            : base(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)
        {
            this._ApiKey = "FakeApiKey";
            this._UserAgent = HttpClientHelpers.UserAgentString();
            this._UseDiskSourceForUnitTests = useDiskSourceForUnitTests;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            // https://quantumnumbers.anu.edu.au/

            if (String.IsNullOrEmpty(_ApiKey))
            {
                if (!_ApiWarningEmitted)
                    Log.Warn("No API Key supplied. Please visit https://quantumnumbers.anu.edu.au/ to obtain a free or paid API key to use this source. No entropy will be gathered from this source without an API key.");
                _ApiWarningEmitted = true;
                return null;
            }

            Log.Trace("Beginning to gather entropy.");

            // Fetch data.
            var response = "";
            var sw = Stopwatch.StartNew();
            if (!_UseDiskSourceForUnitTests)
            {
                var apiUri = new Uri($"https://api.quantumnumbers.anu.edu.au?length={_BytesPerRequest}&type=hex8&size=1");
                var hc = HttpClientHelpers.Create(userAgent: _UserAgent);
                hc.DefaultRequestHeaders.Add("x-api-key", _ApiKey);
                try
                {
                    response = await hc.GetStringAsync(apiUri);
                }
                catch (Exception ex)
                {
                    Log.Warn(ex, "Unable to GET from {0}", apiUri);
                    return null;
                }
                Log.Trace("Read {0:N0} characters of json in {1:N2}ms.", response.Length, sw.Elapsed.TotalMilliseconds);
            }
            else
            {
                using (var stream = File.OpenRead(HttpClientHelpers._BasePathToUnitTestData + "api.quantumnumbers.anu.edu.au.json"))
                {
                    response = await new StreamReader(stream).ReadToEndAsync();
                }
            }
            sw.Stop();

            // To avoid using dynamic or a Json library, we do hacky string parsing!

            // Check for error.
            response = response.Replace("\r", "").Replace("\n", "").Replace(" ", "").Trim();
            if (response.IndexOf("\"success\":true") == -1)
            {
                Log.Error("ANU API returned error result. Full result in next message.");
                Log.Error(response);
                return null;
            }
            // Locate data.
            int dataIdx = response.IndexOf("\"data\":[\"");
            if (dataIdx == -1)
            {
                Log.Error("Cannot locate random result in ANU API result: source will return nothing. Actual result in next message.");
                Log.Error(response);
                return null;
            }
            dataIdx = dataIdx + "\"data\":[\"".Length;
            int endIdx = response.IndexOf("\"]", dataIdx);
            if (endIdx == -1)
            {
                Log.Error("Cannot locate end of random result in ANU API result: source will return nothing. Actual result in next message.");
                Log.Error(response);
                return null;
            }
            Log.Trace("Parsed Json result.");

            // Trim and parse.
            var randomString = response.Substring(dataIdx, endIdx - dataIdx)
                                .Replace("\"", "")
                                .Replace(",", "")
                                .Trim();
            var randomBytes = randomString.ParseFromHexString()
                                .Concat(BitConverter.GetBytes(unchecked((uint)sw.Elapsed.Ticks)))      // Don't forget to include network timing!
                                .ToArray();
            Log.Trace("Read {0:N0} bytes of entropy (including 4 bytes of timing info).", randomBytes.Length);

            return randomBytes;
        }

        public class Configuration
        {
            public static readonly Configuration Default = new Configuration();

            /// <summary>
            /// API key required to access service.
            /// If not set, the source will be disabled.
            /// </summary>
            public string ApiKey { get; set; }

            /// <summary>
            /// Bytes returned per request / sample. 
            /// Default: 1024. Minimum: 1. Maximum: 1024.
            /// </summary>
            public int BytesPerRequest { get; set; } = 1024;

            /// <summary>
            /// Sample period at normal priority. Default: 12 hours.
            /// </summary>
            public TimeSpan PeriodNormalPriority { get; set; } = TimeSpan.FromHours(12);

            /// <summary>
            /// Sample period at high priority. Default: 2 minutes.
            /// </summary>
            public TimeSpan PeriodHighPriority { get; set; } = TimeSpan.FromMinutes(2);

            /// <summary>
            /// Sample period at low priority. Default: 48 hours.
            /// </summary>
            public TimeSpan PeriodLowPriority { get; set; } = TimeSpan.FromHours(48.0);
        }
    }
}
