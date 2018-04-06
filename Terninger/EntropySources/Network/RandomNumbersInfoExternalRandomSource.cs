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
    /// An entropy source which uses http://www.randomnumbers.info/ as input.
    /// This has no publicised rate limit so we use 8 hours as the normal polling period, and get large chunks.
    /// </summary>
    public class RandomNumbersInfoExternalRandomSource : EntropySourceWithPeriod
    {
        public override string Name => typeof(RandomNumbersInfoExternalRandomSource).FullName;

        private readonly string _UserAgent;
        private readonly bool _UseDiskSourceForUnitTests;

        public RandomNumbersInfoExternalRandomSource() : this(ExternalWebContentSource.DefaultUserAgent, TimeSpan.FromHours(8)) { }
        public RandomNumbersInfoExternalRandomSource(string userAgent) : this (userAgent, TimeSpan.FromHours(8)) { }
        public RandomNumbersInfoExternalRandomSource(string userAgent, TimeSpan periodNormalPriority) : this(userAgent, periodNormalPriority, TimeSpan.FromMinutes(2), new TimeSpan(periodNormalPriority.Ticks * 4)) { }
        public RandomNumbersInfoExternalRandomSource(string userAgent, TimeSpan periodNormalPriority, TimeSpan periodHighPriority, TimeSpan periodLowPriority)
            : base(periodNormalPriority, periodHighPriority, periodLowPriority)
        {
            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? ExternalWebContentSource.DefaultUserAgent : userAgent;
        }
        internal RandomNumbersInfoExternalRandomSource(bool useDiskSourceForUnitTests)
            : base(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)
        {
            this._UserAgent = ExternalWebContentSource.DefaultUserAgent;
            this._UseDiskSourceForUnitTests = useDiskSourceForUnitTests;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            Log.Trace("Beginning to gather entropy.");

            // This supports SSL, but the cert isn't valid (it's for the uni, rather than the correct domain).
            // http://www.randomnumbers.info/content/Download.htm
            // This returns HTML, which means I'm doing some hacky parsing here.
            const int rangeOfNumbers = 4096 - 1;      // 12 bits per number (1.5 bytes).
            const int numberOfNumbers = 256;

            // Fetch data.
            var response = "";
            var sw = Stopwatch.StartNew();
            if (!_UseDiskSourceForUnitTests)
            {
                var apiUri = new Uri("http://www.randomnumbers.info/cgibin/wqrng.cgi?amount=" + numberOfNumbers.ToString() + "&limit=" + rangeOfNumbers.ToString());
                var wc = new WebClient();
                wc.Headers.Add("User-Agent:" + _UserAgent);
                // TODO: Exception handling.
                response = await wc.DownloadStringTaskAsync(apiUri);
                Log.Trace("Read {0:N0} characters of html in {1:N2}ms.", response.Length, sw.Elapsed.TotalMilliseconds);
            }
            else
            {
                using (var stream = File.OpenRead("../../Online Generators/www.randomnumbers.info.html"))
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
    }
}
