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
    /// An entropy source which uses http://qrng.ethz.ch/ as input.
    /// This has no publicised rate limit, but we use 8 hours as the normal polling period because we get 256 bytes each request.
    /// Also the underlying hardware can produce 64Mbps of entropy, which is reasonably quick.
    /// </summary>
    [AsyncHint(IsAsync.Always)]
    public class QrngEthzChExternalRandomSource : EntropySourceWithPeriod
    {
        public override string Name { get; set; }

        private string _UserAgent;

        private readonly bool _UseDiskSourceForUnitTests;
        private readonly int _BytesPerRequest;

        public QrngEthzChExternalRandomSource() : this(HttpClientHelpers.UserAgentString(), 256, TimeSpan.FromHours(8)) { }
        public QrngEthzChExternalRandomSource(string userAgent) : this (userAgent, 256, TimeSpan.FromHours(8)) { }
        public QrngEthzChExternalRandomSource(string userAgent, int bytesPerRequest) : this(userAgent, bytesPerRequest, TimeSpan.FromHours(8)) { }
        public QrngEthzChExternalRandomSource(string userAgent, int bytesPerRequest, TimeSpan periodNormalPriority) : this(userAgent, bytesPerRequest, periodNormalPriority, TimeSpan.FromMinutes(2), new TimeSpan(periodNormalPriority.Ticks * 4)) { }
        public QrngEthzChExternalRandomSource(string userAgent, int bytesPerRequest, TimeSpan periodNormalPriority, TimeSpan periodHighPriority, TimeSpan periodLowPriority)
            : base(periodNormalPriority, periodHighPriority, periodLowPriority)
        {
            if (bytesPerRequest < 4 || bytesPerRequest > 2048)      // No published maximum, but we'll be nice.
                throw new ArgumentOutOfRangeException(nameof(bytesPerRequest), bytesPerRequest, "Bytes per request must be between 4 and 2048");

            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? HttpClientHelpers.UserAgentString() : userAgent;
            this._BytesPerRequest = bytesPerRequest;
        }
        internal QrngEthzChExternalRandomSource(bool useDiskSourceForUnitTests)
            : base(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)
        {
            this._UserAgent = HttpClientHelpers.UserAgentString();
            this._UseDiskSourceForUnitTests = useDiskSourceForUnitTests;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            // http://qrng.ethz.ch/http_api/

            Log.Trace("Beginning to gather entropy.");

            // Fetch data.
            var response = "";
            var sw = Stopwatch.StartNew();
            if (!_UseDiskSourceForUnitTests)
            {
                var apiUri = new Uri("http://qrng.ethz.ch/api/randint?min=0&max=255&size=" + _BytesPerRequest);
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
                Log.Trace("Read {0:N0} characters of json in {1:N2}ms.", response.Length, sw.Elapsed.TotalMilliseconds);
            }
            else
            {
                using (var stream = File.OpenRead(HttpClientHelpers._BasePathToUnitTestData + "qrng.ethz.ch-randint.txt"))
                {
                    response = await new StreamReader(stream).ReadToEndAsync();
                }
            }
            sw.Stop();


            // To avoid using dynamic or a Json library, we do hacky string parsing!

            // Check for valid result.
            if (response.IndexOf("\"result\":") == -1)
            {
                Log.Error("qrng.ethz.ch returned unknown result. Full result in next message.");
                Log.Error(response);
                return null;
            }
            int dataIdx = response.IndexOf("[");
            if (dataIdx == -1)
            {
                Log.Error("Cannot locate random result in qrng.ethz.ch response: source will return nothing. Actual result in next message.");
                Log.Error(response);
                return null;
            }
            dataIdx = dataIdx + 1;
            int endIdx = response.IndexOf("]", dataIdx);
            if (endIdx == -1)
            {
                Log.Error("Cannot locate end of random result in qrng.ethz.ch response: source will return nothing. Actual result in next message.");
                Log.Error(response);
                return null;
            }
            Log.Trace("Parsed Json result.");

            // Trim and parse.
            var randomInts = response.Substring(dataIdx, endIdx - dataIdx).Trim();
            var numbersAndOtherJunk = randomInts.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var result = numbersAndOtherJunk
                    .Select(x => (x ?? "").Trim())
                    .Where(x => x.All(Char.IsDigit))        // Remove non-numeric junk.
                    .Select(x => Byte.Parse(x))            // Parse to byte.
                    .Concat(BitConverter.GetBytes(unchecked((uint)sw.Elapsed.Ticks)))  // Don't forget to include network timing!
                    .ToArray();
            Log.Trace("Read {0:N0} bytes of entropy (including 4 bytes of timing info).", result.Length);

            return result;
        }
    }
}
