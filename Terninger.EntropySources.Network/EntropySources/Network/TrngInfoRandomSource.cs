using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Diagnostics;

using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.LibLog;

namespace MurrayGrant.Terninger.EntropySources.Network
{
    /// <summary>
    /// An entropy source which uses https://trng.info/ as input.
    /// No published rate limits, but says it uses a hardward RNG with at least 4Mbps entropy rate (0.5MB/sec).
    /// Requires origin header to pretend we're coming from the website, so not making too many requests by default.
    /// </summary>
    [AsyncHint(IsAsync.Always)]
    public class TrngInfoRandomSource : EntropySourceWithPeriod
    {
        public override string Name { get; set; }

        private readonly int _BytesPerRequest;
        private readonly string _UserAgent;
        private bool _UnconfiguredUserAgentWarningEmitted;
        private readonly bool _UseDiskSourceForUnitTests;

        public TrngInfoRandomSource(string userAgent, Configuration config)
            : this(
                  userAgent:            userAgent,
                  bytesPerRequest:      config?.BytesPerRequest      ?? Configuration.Default.BytesPerRequest,
                  periodNormalPriority: config?.PeriodNormalPriority ?? Configuration.Default.PeriodNormalPriority,
                  periodHighPriority:   config?.PeriodHighPriority   ?? Configuration.Default.PeriodHighPriority,
                  periodLowPriority:    config?.PeriodLowPriority    ?? Configuration.Default.PeriodLowPriority
            )
        { }
        public TrngInfoRandomSource(string userAgent = null, int? bytesPerRequest = null, TimeSpan ? periodNormalPriority = null, TimeSpan? periodHighPriority = null, TimeSpan? periodLowPriority = null)
            : base(periodNormalPriority.GetValueOrDefault(Configuration.Default.PeriodNormalPriority), 
                  periodHighPriority.GetValueOrDefault(Configuration.Default.PeriodHighPriority),
                  periodLowPriority.GetValueOrDefault(Configuration.Default.PeriodLowPriority))
        {
            this._BytesPerRequest = bytesPerRequest.GetValueOrDefault(Configuration.Default.BytesPerRequest);
            if (_BytesPerRequest < 1 || _BytesPerRequest > 1000)
                throw new ArgumentOutOfRangeException(nameof(bytesPerRequest), bytesPerRequest, "Bytes per request must be between 1 and 1000");

            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? HttpClientHelpers.UserAgentString() : userAgent;
        }
        internal TrngInfoRandomSource(bool useDiskSourceForUnitTests)
            : base(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)
        {
            this._UserAgent = HttpClientHelpers.UserAgentString();
            this._UseDiskSourceForUnitTests = useDiskSourceForUnitTests;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            // https://trng.info/
            // Minimum 4Mbps (0.5MB/sec) entropy from hardware source.

            Log.Trace("Beginning to gather entropy.");

            if (_UserAgent.Contains("Terninger/unconfigured"))
            {
                if (!_UnconfiguredUserAgentWarningEmitted)
                    Log.Warn("No user agent is configured. Please be polite to web services and set a unique user agent identifier for your usage of Terninger.");
                _UnconfiguredUserAgentWarningEmitted = true;
            }

            // Fetch data.
            var response = "";
            var sw = Stopwatch.StartNew();
            if (!_UseDiskSourceForUnitTests)
            {
                var apiUri = new Uri("https://qpsy.de/quantis/random/bytes?device_id=0&length=" + _BytesPerRequest);
                var hc = HttpClientHelpers.Create(userAgent: _UserAgent);
                hc.DefaultRequestHeaders.Add("Origin", "https://trng.info");  // Pretend the request is from https://trng.info
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
                using (var stream = File.OpenRead(HttpClientHelpers._BasePathToUnitTestData + "trng.info.json"))
                {
                    response = await new StreamReader(stream).ReadToEndAsync();
                }
            }
            sw.Stop();

            // Parse out the useful parts of the response. 
            // Keeping away from JSON parsing to minimise dependencies. At least for now.
            var randomnessBytes = GetJsonValue(response, "data").ParseFromHexString()
                                    .Concat(BitConverter.GetBytes(unchecked((uint)sw.Elapsed.Ticks)))      // Don't forget to include network timing!
                                    .ToArray();
            Log.Trace("Read {0:N0} bytes of entropy.", randomnessBytes.Length);

            return randomnessBytes;
        }
        private string GetJsonValue(string json, string key)
        {
            var startTag = "\"" + key + "\":\"";
            var endTag = "\"";
            int startIdx = json.IndexOf(startTag) + startTag.Length;
            int endIdx = json.IndexOf(endTag, startIdx) - 1;
            var result = json.Substring(startIdx, endIdx - startIdx);
            return result;
        }

        public class Configuration
        {
            public static readonly Configuration Default = new Configuration();

            /// <summary>
            /// Bytes returned per request / sample. 
            /// Default: 128. Minimum: 1. Maximum: 1000.
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
