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
    /// An entropy source which uses https://beacon.nist.gov/ as input.
    /// No published rate limits, but produces new output every 60 seconds and records it.
    /// </summary>
    [AsyncHint(IsAsync.Always)]
    public class BeaconNistExternalRandomSource : EntropySourceWithPeriod
    {
        public override string Name { get; set; }

        private readonly string _UserAgent;
        private bool _UnconfiguredUserAgentWarningEmitted;
        private readonly bool _UseDiskSourceForUnitTests;

        public BeaconNistExternalRandomSource(string userAgent, Configuration config)
            : this(
                  userAgent:            userAgent,
                  periodNormalPriority: config?.PeriodNormalPriority ?? Configuration.Default.PeriodNormalPriority,
                  periodHighPriority:   config?.PeriodHighPriority   ?? Configuration.Default.PeriodHighPriority,
                  periodLowPriority:    config?.PeriodLowPriority    ?? Configuration.Default.PeriodLowPriority
            )
        { }
        public BeaconNistExternalRandomSource(string userAgent = null, TimeSpan? periodNormalPriority = null, TimeSpan? periodHighPriority = null, TimeSpan? periodLowPriority = null)
            : base(periodNormalPriority.GetValueOrDefault(Configuration.Default.PeriodNormalPriority), 
                  periodHighPriority.GetValueOrDefault(Configuration.Default.PeriodHighPriority),
                  periodLowPriority.GetValueOrDefault(Configuration.Default.PeriodLowPriority))
        {
            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? HttpClientHelpers.UserAgentString() : userAgent;
        }
        internal BeaconNistExternalRandomSource(bool useDiskSourceForUnitTests)
            : base(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)
        {
            this._UserAgent = HttpClientHelpers.UserAgentString();
            this._UseDiskSourceForUnitTests = useDiskSourceForUnitTests;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            // https://beacon.nist.gov/
            // Note that this will return the same result for 60 second period.
            // We must mix in some local entropy to ensure differnt computers end up with different entropy.
            // Yes, this reduces the effectiveness of this source, but it will still contribute over time.

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
                var apiUri = new Uri("https://beacon.nist.gov/rest/record/last");
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
                using (var stream = File.OpenRead(HttpClientHelpers._BasePathToUnitTestData + "beacon.nist.gov-last.xml"))
                {
                    response = await new StreamReader(stream).ReadToEndAsync();
                }
            }
            sw.Stop();

            var localEntropy = (await StaticLocalEntropy.Get32()).Concat(CheapEntropy.Get16())
                                .Concat(BitConverter.GetBytes((uint)sw.Elapsed.Ticks))
                                .ToArray();
            Log.Trace("Got {0:N0} bytes of local entropy to mix.", localEntropy.Length);

            // Parse out the useful parts of the response. 
            // Keeping away from XML parsing to minimise dependencies. At least for now.

            // The first two return 64 random bytes each, the signature is 256 bytes. All are hashed to 64 bytes when combined with local entropy.
            var lastOutputBytes = GetWithinXmlTags(response, "previousOutputValue").ParseFromHexString();
            var outputValueBytes = GetWithinXmlTags(response, "outputValue").ParseFromHexString();
            var signatureBytes = GetWithinXmlTags(response, "signatureValue").ParseFromHexString();
            Log.Trace("Got {0:N0} output bytes, {1:N0} last output bytes, {2:N0} signature bytes.", outputValueBytes.Length, lastOutputBytes.Length, signatureBytes.Length);

            // Mix in some local entropy.
            var hasher = SHA512.Create();
            var result = hasher.ComputeHash(lastOutputBytes.Concat(localEntropy).ToArray())
                        .Concat(hasher.ComputeHash(outputValueBytes.Concat(localEntropy).ToArray()))
                        .Concat(hasher.ComputeHash(signatureBytes.Concat(localEntropy).ToArray()))
                        .Concat(BitConverter.GetBytes(unchecked((uint)sw.Elapsed.Ticks)))      // Don't forget to include network timing!
                        .ToArray();
            Log.Trace("Read {0:N0} bytes of entropy.", result.Length);

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

        public class Configuration
        {
            public static readonly Configuration Default = new Configuration();

            /// <summary>
            /// Sample period at normal priority. Default: 4 hours.
            /// </summary>
            public TimeSpan PeriodNormalPriority { get; set; } = TimeSpan.FromHours(4);

            /// <summary>
            /// Sample period at high priority. Default: 2 minutes.
            /// </summary>
            public TimeSpan PeriodHighPriority { get; set; } = TimeSpan.FromMinutes(2);

            /// <summary>
            /// Sample period at low priority. Default: 16 hours.
            /// </summary>
            public TimeSpan PeriodLowPriority { get; set; } = TimeSpan.FromHours(16);
        }
    }
}
