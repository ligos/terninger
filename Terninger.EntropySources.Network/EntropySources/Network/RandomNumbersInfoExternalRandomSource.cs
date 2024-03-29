﻿using System;
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
    /// An entropy source which uses http://www.randomnumbers.info/ as input.
    /// This has no publicised rate limit so we use 8 hours as the normal polling period, and get chunks of 256 numbers (386 bytes of entropy).
    /// Each number requested is between 0 and 4095, and is equivelent to 12 bits or 1.5 bytes of entropy.
    /// </summary>
    [AsyncHint(IsAsync.Always)]
    public class RandomNumbersInfoExternalRandomSource : EntropySourceWithPeriod
    {
        public override string Name { get; set; }

        private readonly int _NumberOfNumbers;
        private readonly string _UserAgent;
        private bool _UnconfiguredUserAgentWarningEmitted;
        private readonly bool _UseDiskSourceForUnitTests;

        public RandomNumbersInfoExternalRandomSource(string userAgent, Configuration config)
            : this(
                  userAgent: userAgent,
                  numberOfNumbers: config?.NumbersPerRequest ?? Configuration.Default.NumbersPerRequest,
                  periodNormalPriority: config?.PeriodNormalPriority ?? Configuration.Default.PeriodNormalPriority,
                  periodHighPriority: config?.PeriodHighPriority ?? Configuration.Default.PeriodHighPriority,
                  periodLowPriority: config?.PeriodLowPriority ?? Configuration.Default.PeriodLowPriority
            )
        { }
        public RandomNumbersInfoExternalRandomSource(string userAgent = null, int? numberOfNumbers = null, TimeSpan? periodNormalPriority = null, TimeSpan? periodHighPriority = null, TimeSpan? periodLowPriority = null)
            : base(periodNormalPriority.GetValueOrDefault(Configuration.Default.PeriodNormalPriority),
                  periodHighPriority.GetValueOrDefault(Configuration.Default.PeriodHighPriority),
                  periodLowPriority.GetValueOrDefault(Configuration.Default.PeriodLowPriority))
        {
            this._NumberOfNumbers = numberOfNumbers.GetValueOrDefault(Configuration.Default.NumbersPerRequest);
            if (_NumberOfNumbers < 1 || _NumberOfNumbers > 1000)
                throw new ArgumentOutOfRangeException(nameof(numberOfNumbers), numberOfNumbers, "Between 1 and 1000 numbers are allowed");

            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? HttpClientHelpers.UserAgentString() : userAgent;
        }
        internal RandomNumbersInfoExternalRandomSource(bool useDiskSourceForUnitTests)
            : base(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)
        {
            this._UserAgent = HttpClientHelpers.UserAgentString();
            this._UseDiskSourceForUnitTests = useDiskSourceForUnitTests;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            Log.Trace("Beginning to gather entropy.");

            if (_UserAgent.Contains("Terninger/unconfigured"))
            {
                if (!_UnconfiguredUserAgentWarningEmitted)
                    Log.Warn("No user agent is configured. Please be polite to web services and set a unique user agent identifier for your usage of Terninger.");
                _UnconfiguredUserAgentWarningEmitted = true;
            }

            // This supports SSL, but the cert isn't valid (it's for the uni, rather than the correct domain).
            // http://www.randomnumbers.info/content/Download.htm
            // This returns HTML, which means I'm doing some hacky parsing here.
            const int rangeOfNumbers = 4096 - 1;      // 12 bits per number (1.5 bytes).

            // Fetch data.
            var response = "";
            var sw = Stopwatch.StartNew();
            if (!_UseDiskSourceForUnitTests)
            {
                var apiUri = new Uri("http://www.randomnumbers.info/cgibin/wqrng.cgi?amount=" + _NumberOfNumbers.ToString() + "&limit=" + rangeOfNumbers.ToString());
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
                using (var stream = File.OpenRead(HttpClientHelpers._BasePathToUnitTestData + "www.randomnumbers.info.html"))
                {
                    response = await new StreamReader(stream).ReadToEndAsync();
                }
            }
            sw.Stop();

            // Locate some pretty clear boundaries around the random numbers returned.
            var startIdxString = "Download random numbers from quantum origin";
            var startIdx = response.IndexOf(startIdxString, StringComparison.OrdinalIgnoreCase);
            if (startIdx == -1)
            {
                Log.Error("Cannot locate start string in html of randomnumbers.info result: source will return nothing. Looking for '{0}'. Actual result in next message.", startIdxString);
                Log.Error(response);
                return null;
            }
            var hrIdxString = "<hr>";
            startIdx = response.IndexOf(hrIdxString, startIdx, StringComparison.OrdinalIgnoreCase);
            if (startIdx == -1)
            {
                Log.Error("Cannot locate start string in html of randomnumbers.info result: source will return nothing. Looking for '{0}'. Actual result in next message.", hrIdxString);
                Log.Error(response);
                return null;
            }
            var endIdxString = "</td>";
            var endIdx = response.IndexOf("</td>", startIdx, StringComparison.OrdinalIgnoreCase);
            if (endIdx == -1)
            {
                Log.Error("Cannot locate end string in html of randomnumbers.info result: source will return nothing. Looking for '{0}'. Actual result in next message.", endIdxString);
                Log.Error(response);
                return null;
            }
            Log.Trace("Parsed beginning and end of useful entropy.");

            var haystack = response.Substring(startIdx, endIdx - startIdx);
            var numbersAndOtherJunk = haystack.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);      // Numbers are space separated.
            var numbers = numbersAndOtherJunk
                            .Where(x => x.All(Char.IsDigit))        // Remove non-numeric junk.
                            .Select(x => Int16.Parse(x))            // Parse to an int16.
                            .ToList();
            var result = new byte[(numbers.Count() * 2) + sizeof(uint)];
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
            var timingBytes = BitConverter.GetBytes(unchecked((uint)sw.Elapsed.Ticks));
            result[result.Length - 4] = timingBytes[0];
            result[result.Length - 3] = timingBytes[1];
            result[result.Length - 2] = timingBytes[2];
            result[result.Length - 1] = timingBytes[3];

            Log.Trace("Read {0:N0} bytes of entropy (including 4 bytes of timing info).", result.Length);

            return result;
        }

        public class Configuration
        {
            public static readonly Configuration Default = new Configuration();

            /// <summary>
            /// Numbers (0..4095) returned per request / sample. 
            /// Default: 256. Minimum: 1. Maximum: 1000.
            /// </summary>
            public int NumbersPerRequest { get; set; } = 256;

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
