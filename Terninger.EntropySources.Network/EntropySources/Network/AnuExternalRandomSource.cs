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
    /// An entropy source which uses https://qrng.anu.edu.au as input.
    /// This has no publicised rate limit (and boasts gigabit/sec entropy), but we use 12 hours as the normal polling period because we get 1kB each request.
    /// </summary>
    public class AnuExternalRandomSource : EntropySourceWithPeriod
    {
        public override string Name { get; set; }

        private string _UserAgent;

        private readonly bool _UseDiskSourceForUnitTests;

        public AnuExternalRandomSource() : this(HttpClientHelpers.DefaultUserAgent, TimeSpan.FromHours(12)) { }
        public AnuExternalRandomSource(string userAgent) : this (userAgent, TimeSpan.FromHours(12)) { }
        public AnuExternalRandomSource(string userAgent, TimeSpan periodNormalPriority) : this(userAgent, periodNormalPriority, TimeSpan.FromMinutes(2), new TimeSpan(periodNormalPriority.Ticks * 4)) { }
        public AnuExternalRandomSource(string userAgent, TimeSpan periodNormalPriority, TimeSpan periodHighPriority, TimeSpan periodLowPriority)
            : base(periodNormalPriority, periodHighPriority, periodLowPriority)
        {
            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? HttpClientHelpers.DefaultUserAgent : userAgent;
        }
        internal AnuExternalRandomSource(bool useDiskSourceForUnitTests)
            : base(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)
        {
            this._UserAgent = HttpClientHelpers.DefaultUserAgent;
            this._UseDiskSourceForUnitTests = useDiskSourceForUnitTests;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            // http://qrng.anu.edu.au/index.php

            Log.Trace("Beginning to gather entropy.");

            // Fetch data.
            // This always returns 1024 bytes!!
            var response = "";
            var sw = Stopwatch.StartNew();
            if (!_UseDiskSourceForUnitTests)
            {
                var apiUri = new Uri("https://qrng.anu.edu.au/RawHex.php");
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
                using (var stream = File.OpenRead("../../Online Generators/qrng.anu.edu.au-RawHex.php.html"))
                {
                    response = await new StreamReader(stream).ReadToEndAsync();
                }
            }
            sw.Stop();


            // Locate some pretty clear boundaries around the random numbers returned.
            var startIdxString = "1024 bytes of randomness in hexadecimal form";
            var startIdx = response.IndexOf(startIdxString, StringComparison.OrdinalIgnoreCase);
            if (startIdx == -1)
            {
                Log.Error("Cannot locate start string in html of anu result: source will return nothing. Looking for '{0}', actual result in next message.", startIdxString);
                Log.Error(response);
                return null;
            }

            var startIdxString2 = "<td>";
            startIdx = response.IndexOf(startIdxString2, startIdx, StringComparison.OrdinalIgnoreCase) + startIdxString2.Length;
            if (startIdx == -1)
            {
                Log.Error("Cannot locate start string in html of anu result: source will return nothing. Looking for '{0}', actual result in next message.", startIdxString2);
                Log.Error(response);
                return null;
            }

            var endIdxString = "</td>";
            var endIdx = response.IndexOf(endIdxString, startIdx, StringComparison.OrdinalIgnoreCase);
            if (endIdx == -1)
            {
                Log.Error("Cannot locate end string in html of anu result: source will return nothing. Looking for '{0}', actual result in next message.", endIdxString);
                Log.Error(response);
                return null;
            }
            Log.Trace("Parsed beginning and end of useful entropy.");

            // Trim and parse.
            var randomString = response.Substring(startIdx, endIdx - startIdx).Trim();
            var randomBytes = randomString.ParseFromHexString()
                                .Concat(BitConverter.GetBytes(unchecked((uint)sw.Elapsed.Ticks)))      // Don't forget to include network timing!
                                .ToArray();
            Log.Trace("Read {0:N0} bytes of entropy (including 4 bytes of timing info).", randomBytes.Length);

            return randomBytes;
        }
    }
}
