using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Diagnostics;

using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.LibLog;

namespace MurrayGrant.Terninger.EntropySources.Network
{
    /// <summary>
    /// An entropy source which uses https://www.fourmilab.ch/hotbits/ as input.
    /// Either a pseudorandom source (without API key) or true random (requires an API key).
    /// No published rate limits, but API uses true random bits so you should be extra cautious when using it.
    /// </summary>
    [AsyncHint(IsAsync.Always)]
    public class HotbitsExternalRandomSource : EntropySourceWithPeriod
    {
        public override string Name { get; set; }

        private readonly string _UserAgent;
        private readonly string _ApiKey;
        private readonly int _BytesPerRequest;
        private readonly bool _UseDiskSourceForUnitTests;
        private bool _ApiWarningEmitted;

        public HotbitsExternalRandomSource(string userAgent, Configuration config)
            : this(
                  userAgent:            userAgent,
                  apiKey:               config?.ApiKey,
                  bytesPerRequest:      config?.BytesPerRequest      ?? Configuration.Default.BytesPerRequest,
                  periodNormalPriority: config?.PeriodNormalPriority ?? Configuration.Default.PeriodNormalPriority,
                  periodHighPriority:   config?.PeriodHighPriority   ?? Configuration.Default.PeriodHighPriority,
                  periodLowPriority:    config?.PeriodLowPriority    ?? Configuration.Default.PeriodLowPriority
            )
        { }

        public HotbitsExternalRandomSource(string userAgent = null, int? bytesPerRequest = null, string apiKey = null, TimeSpan? periodNormalPriority = null, TimeSpan? periodHighPriority = null, TimeSpan? periodLowPriority = null)
            : base(periodNormalPriority.GetValueOrDefault(Configuration.Default.PeriodNormalPriority),
                  periodHighPriority.GetValueOrDefault(Configuration.Default.PeriodHighPriority),
                  periodLowPriority.GetValueOrDefault(Configuration.Default.PeriodLowPriority))
        {
            this._BytesPerRequest = bytesPerRequest.GetValueOrDefault(Configuration.Default.BytesPerRequest);
            if (_BytesPerRequest < 4 || _BytesPerRequest > 2048)      // Max of 2048 bytes based on Web UI.
                throw new ArgumentOutOfRangeException(nameof(bytesPerRequest), bytesPerRequest, "Bytes per request must be between 4 and 2048");

            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? HttpClientHelpers.UserAgentString() : userAgent;
            this._ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
        }
        internal HotbitsExternalRandomSource(bool useDiskSourceForUnitTests)
            : base(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)
        {
            this._UserAgent = HttpClientHelpers.UserAgentString();
            this._UseDiskSourceForUnitTests = useDiskSourceForUnitTests;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            // http://www.fourmilab.ch/hotbits/
            Log.Trace("Beginning to gather entropy.");

            if (String.IsNullOrEmpty(_ApiKey))
            {
                if (!_ApiWarningEmitted)
                    Log.Warn("No API Key supplied. Please visit https://www.fourmilab.ch/hotbits/ to obtain a free API key to use true random data from this source.");
                _ApiWarningEmitted = true;
            }

            string pseudoSource, apiKey;
            if (String.IsNullOrWhiteSpace(_ApiKey))
            {
                pseudoSource = "&pseudo=pseudo";
                apiKey = "&apikey=";
            }
            else
            {
                pseudoSource = "";
                apiKey = "&apikey=" + _ApiKey;
            }

            // Fetch data.
            var response = "";
            var sw = Stopwatch.StartNew();
            if (!_UseDiskSourceForUnitTests)
            {
                var apiUri = new Uri("https://www.fourmilab.ch/cgi-bin/Hotbits.api?nbytes=" + _BytesPerRequest + "&fmt=hex&npass=1&lpass=8&pwtype=3" + apiKey + pseudoSource);
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
                using (var stream = File.OpenRead(HttpClientHelpers._BasePathToUnitTestData + "hotbits.html"))
                {
                    response = await new StreamReader(stream).ReadToEndAsync();
                }
            }
            sw.Stop();


            // Locate some pretty clear boundaries around the random numbers returned.
            var startIdx = response.IndexOf("<pre>", StringComparison.OrdinalIgnoreCase) + "<pre>".Length;
            if (startIdx == -1)
            {
                Log.Error("Cannot locate start string in html of hotbits result: source will return nothing. Actual result in next message.");
                Log.Error(response);
                return null;
            }
            var endIdx = response.IndexOf("</pre>", startIdx, StringComparison.OrdinalIgnoreCase);
            if (endIdx == -1)
            {
                Log.Error("Cannot locate end string in html of hotbits result: source will return nothing. Actual result in next message.");
                Log.Error(response);
                return null;
            }
            var randomString = response.Substring(startIdx, endIdx - startIdx).Trim().Replace("\r", "").Replace("\n", "");
            Log.Trace("Parsed beginning and end of useful entropy.");

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
            /// If not set, the source will use a pseudorandom source rather than true random.
            /// </summary>
            public string ApiKey { get; set; }

            /// <summary>
            /// Bytes returned per request / sample.
            /// Default: 128. Minimum: 4. Maximum: 2048.
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
