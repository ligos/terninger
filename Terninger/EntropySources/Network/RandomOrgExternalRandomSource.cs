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
    /// An entropy source which uses https://random.org as input.
    /// Either via a public interface or an API with a key.
    /// Rate limits of ~250k bits or 1000 requests per day, so use large chunks with 8 hour timouts.
    /// </summary>
    public class RandomOrgExternalRandomSource : EntropySourceWithPeriod
    {
        public override string Name { get; set; }

        private readonly string _UserAgent;
        private readonly Guid _ApiKey;
        private readonly int _BytesPerRequest;
        private readonly bool _UseDiskSourceForUnitTests;

        public RandomOrgExternalRandomSource() : this(WebClientHelpers.DefaultUserAgent, 128, TimeSpan.FromHours(8)) { }
        public RandomOrgExternalRandomSource(string userAgent, Guid apiKey) : this(userAgent, 128, apiKey, TimeSpan.FromHours(8)) { }
        public RandomOrgExternalRandomSource(string userAgent, int bytesPerRequest) : this (userAgent, bytesPerRequest, TimeSpan.FromHours(8)) { }
        public RandomOrgExternalRandomSource(string userAgent, int bytesPerRequest, Guid apiKey) : this(userAgent, bytesPerRequest, apiKey, TimeSpan.FromHours(8)) { }
        public RandomOrgExternalRandomSource(string userAgent, int bytesPerRequest, TimeSpan periodNormalPriority) : this(userAgent, bytesPerRequest, Guid.Empty, periodNormalPriority, TimeSpan.FromMinutes(2), new TimeSpan(periodNormalPriority.Ticks * 4)) { }
        public RandomOrgExternalRandomSource(string userAgent, int bytesPerRequest, Guid apiKey, TimeSpan periodNormalPriority) : this(userAgent, bytesPerRequest, apiKey, periodNormalPriority, TimeSpan.FromMinutes(2), new TimeSpan(periodNormalPriority.Ticks * 4)) { }
        public RandomOrgExternalRandomSource(string userAgent, int bytesPerRequest, Guid apiKey, TimeSpan periodNormalPriority, TimeSpan periodHighPriority, TimeSpan periodLowPriority)
            : base(periodNormalPriority, periodHighPriority, periodLowPriority)
        {
            if (bytesPerRequest < 4 || bytesPerRequest > 4096)
                throw new ArgumentOutOfRangeException(nameof(bytesPerRequest), bytesPerRequest, "Bytes per request must be between 4 and 4096");

            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? WebClientHelpers.DefaultUserAgent : userAgent;
            this._BytesPerRequest = bytesPerRequest;
            this._ApiKey = apiKey;
        }
        internal RandomOrgExternalRandomSource(bool useDiskSourceForUnitTests, Guid apiKey)
            : base(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)
        {
            this._UserAgent = WebClientHelpers.DefaultUserAgent;
            this._UseDiskSourceForUnitTests = useDiskSourceForUnitTests;
            this._ApiKey = apiKey;
        }

        protected override Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            if (_ApiKey == Guid.Empty)
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
                using (var stream = File.OpenRead("../../Online Generators/www.random.org.html"))
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

            // https://api.random.org/json-rpc/1/introduction
            // https://api.random.org/json-rpc/1/basic
            // https://api.random.org/json-rpc/1/request-builder

            // Fetch data.
            var response = "";
            var sw = Stopwatch.StartNew();
            if (!_UseDiskSourceForUnitTests)
            {
                var apiUri = new Uri("https://api.random.org/json-rpc/1/invoke");
                var wc = WebClientHelpers.Create(userAgent: _UserAgent);
                wc.Headers.Add("Content-Type", "application/json-rpc");
                var requestBody = "{\"jsonrpc\":\"2.0\",\"method\":\"generateBlobs\",\"params\":{\"apiKey\":\"" + _ApiKey.ToString("D") + "\",\"n\":1,\"size\":" + (_BytesPerRequest * 8) + ",\"format\":\"base64\"},\"id\":1}";
                try
                {
                    response = await wc.UploadStringTaskAsync(apiUri, "POST", requestBody);
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
                using (var stream = File.OpenRead("../../Online Generators/api.random.org.html"))
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
    }
}
