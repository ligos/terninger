using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Diagnostics;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.LibLog;

namespace MurrayGrant.Terninger.EntropySources.Network
{
    /// <summary>
    /// An entropy source which uses https://www.fourmilab.ch/hotbits/ as input.
    /// Either a pseudorandom source or true random (requires an API key).
    /// No published rate limits, but API uses true random bits so you should be extra cautious when using it.
    /// </summary>
    public class HotbitsExternalRandomSource : EntropySourceWithPeriod
    {
        public override string Name { get; set; }

        private readonly string _UserAgent;
        private readonly string _ApiKey;
        private readonly int _BytesPerRequest;
        private readonly bool _UseDiskSourceForUnitTests;

        public HotbitsExternalRandomSource() : this(WebClientHelpers.DefaultUserAgent, 128, TimeSpan.FromHours(8)) { }
        public HotbitsExternalRandomSource(string userAgent, string apiKey) : this(userAgent, 128, TimeSpan.FromHours(8)) { }
        public HotbitsExternalRandomSource(string userAgent, int bytesPerRequest) : this (userAgent, bytesPerRequest, TimeSpan.FromHours(8)) { }
        public HotbitsExternalRandomSource(string userAgent, int bytesPerRequest, string apiKey) : this(userAgent, bytesPerRequest, apiKey, TimeSpan.FromHours(8)) { }
        public HotbitsExternalRandomSource(string userAgent, int bytesPerRequest, TimeSpan periodNormalPriority) : this(userAgent, bytesPerRequest, null, periodNormalPriority, TimeSpan.FromMinutes(2), new TimeSpan(periodNormalPriority.Ticks * 4)) { }
        public HotbitsExternalRandomSource(string userAgent, int bytesPerRequest, string apiKey, TimeSpan periodNormalPriority) : this(userAgent, bytesPerRequest, apiKey, periodNormalPriority, TimeSpan.FromMinutes(2), new TimeSpan(periodNormalPriority.Ticks * 4)) { }
        public HotbitsExternalRandomSource(string userAgent, int bytesPerRequest, string apiKey, TimeSpan periodNormalPriority, TimeSpan periodHighPriority, TimeSpan periodLowPriority)
            : base(periodNormalPriority, periodHighPriority, periodLowPriority)
        {
            if (bytesPerRequest < 4 || bytesPerRequest > 2048)      // Max of 2048 bytes based on Web UI.
                throw new ArgumentOutOfRangeException(nameof(bytesPerRequest), bytesPerRequest, "Bytes per request must be between 4 and 2048");

            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? WebClientHelpers.DefaultUserAgent : userAgent;
            this._BytesPerRequest = bytesPerRequest;
            this._ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
        }
        internal HotbitsExternalRandomSource(bool useDiskSourceForUnitTests)
            : base(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)
        {
            this._UserAgent = WebClientHelpers.DefaultUserAgent;
            this._UseDiskSourceForUnitTests = useDiskSourceForUnitTests;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            // http://www.fourmilab.ch/hotbits/
            Log.Trace("Beginning to gather entropy.");

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
                var wc = WebClientHelpers.Create(userAgent: _UserAgent);
                try
                {
                    response = await wc.DownloadStringTaskAsync(apiUri);
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
                using (var stream = File.OpenRead("../../Online Generators/hotbits.html"))
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
    }
}
