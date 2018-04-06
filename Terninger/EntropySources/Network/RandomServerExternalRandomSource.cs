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
    /// An entropy source which uses http://www.randomserver.dyndns.org as input.
    /// This has no publicised rate limit so we use 12 hours as the normal polling period.
    /// </summary>
    public class RandomServerExternalRandomSource : EntropySourceWithPeriod
    {
        public override string Name => typeof(RandomServerExternalRandomSource).FullName;

        private readonly string _UserAgent;
        private readonly int _BytesPerRequest;
        private readonly bool _UseDiskSourceForUnitTests;

        public RandomServerExternalRandomSource() : this(ExternalWebContentSource.DefaultUserAgent, 64, TimeSpan.FromHours(12)) { }
        public RandomServerExternalRandomSource(string userAgent) : this (userAgent, 64, TimeSpan.FromHours(12)) { }
        public RandomServerExternalRandomSource(string userAgent, int bytesPerRequest) : this(userAgent, bytesPerRequest, TimeSpan.FromHours(12)) { }
        public RandomServerExternalRandomSource(string userAgent, int bytesPerRequest, TimeSpan periodNormalPriority) : this(userAgent, bytesPerRequest, periodNormalPriority, TimeSpan.FromMinutes(2), new TimeSpan(periodNormalPriority.Ticks * 4)) { }
        public RandomServerExternalRandomSource(string userAgent, int bytesPerRequest, TimeSpan periodNormalPriority, TimeSpan periodHighPriority, TimeSpan periodLowPriority)
            : base(periodNormalPriority, periodHighPriority, periodLowPriority)
        {
            // TODO: work out if these are reasonable limits.
            if (bytesPerRequest < 4 || bytesPerRequest > 4096)
                throw new ArgumentOutOfRangeException(nameof(bytesPerRequest), bytesPerRequest, "Bytes per request must be between 4 and 4096");

            this._UserAgent = String.IsNullOrWhiteSpace(userAgent) ? ExternalWebContentSource.DefaultUserAgent : userAgent;
            this._BytesPerRequest = bytesPerRequest;
        }
        internal RandomServerExternalRandomSource(bool useDiskSourceForUnitTests)
            : base(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)
        {
            this._UserAgent = ExternalWebContentSource.DefaultUserAgent;
            this._UseDiskSourceForUnitTests = useDiskSourceForUnitTests;
        }

        protected override async Task<byte[]> GetInternalEntropyAsync(EntropyPriority priority)
        {
            // http://www.randomserver.dyndns.org/client/random.php

            Log.Trace("Beginning to gather entropy.");

            // Fetch data.
            byte[] response;
            var sw = Stopwatch.StartNew();
            if (!_UseDiskSourceForUnitTests)
            {
                var apiUri = new Uri("http://www.randomserver.dyndns.org/client/random.php?type=BIN&a=1&b=10&n=" + _BytesPerRequest + "&file=0");
                var wc = new WebClient();
                wc.Headers.Add("User-Agent:" + _UserAgent);
                // TODO: exception handling.
                response = await wc.DownloadDataTaskAsync(apiUri);
                Log.Trace("Read {0:N0} bytes in {1:N2}ms.", response.Length, sw.Elapsed.TotalMilliseconds);
            }
            else
            {
                using (var stream = File.OpenRead("../../Online Generators/randomserver.dyndns.org.dat"))
                {
                    response = new byte[_BytesPerRequest];
                    await stream.ReadAsync(response, 0, response.Length);
                }
            }
            sw.Stop();


            // This is a binary file: no further parsing or processing is needed.
            var randomBytes = response
                                .Concat(BitConverter.GetBytes(unchecked((uint)sw.Elapsed.Ticks)))      // Don't forget to include network timing!
                                .ToArray();
            Log.Trace("Read {0:N0} bytes of entropy (including 4 bytes of timing info).", randomBytes.Length);

            return randomBytes;
        }
    }
}
