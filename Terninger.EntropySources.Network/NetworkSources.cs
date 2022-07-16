using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Network;


// For unit testing; some sources expose an internal constructor to opt into a mock / fake source.
[assembly: InternalsVisibleTo("Terninger.Test"),
           InternalsVisibleTo("Terninger.Test.Slow")]

namespace MurrayGrant.Terninger
{
    /// <summary>
    /// Sources which actively generate network activity to derive entropy.
    /// Eg: ping, DNS, http requests.
    /// </summary>
    public static class NetworkSources
    {
        /// <summary>
        /// An additional set of sources which gather entropy from external network sources such as ping timings, web content and 3rd party entropy generators.
        /// </summary>
        /// <param name="userAgent">A user agent string to include in web requests. Highly recommended to identify yourself in case of problems. See MurrayGrant.Terninger.Helpers.WebClientHelpers.DefaultUserAgent for an example.</param>
        /// <param name="anuApiKey">API key for true random source at https://quantumnumbers.anu.edu.au </param>
        /// <param name="hotBitsApiKey">API key for true random source at https://www.fourmilab.ch/hotbits </param>
        /// <param name="randomOrgApiKey">API for https://api.random.org </param>
        public static IEnumerable<IEntropySource> All(string userAgent = null, string anuApiKey = null, string hotBitsApiKey = null, Guid? randomOrgApiKey = null)
            => new IEntropySource[]
        {
            new PingStatsSource(),
            new ExternalWebContentSource(userAgent),
            new AnuExternalRandomSource(anuApiKey, userAgent: userAgent),
            new BeaconNistExternalRandomSource(userAgent: userAgent),
            new HotbitsExternalRandomSource(userAgent: userAgent, apiKey: hotBitsApiKey),
            new RandomNumbersInfoExternalRandomSource(userAgent),
            new RandomOrgExternalRandomSource(userAgent, randomOrgApiKey.GetValueOrDefault()),
        };

        /// <summary>
        /// Create a user-agent string to use with HTTP requests.
        /// </summary>
        /// <param name="usageIdentifier">An email address, website, or other identifying mark to include.</param>
        /// <exception cref="System.Exception">May throw if the usageIdentifier has invalid characters.</exception>
        public static string UserAgent(string usageIdentifier)
        {
            var id = (usageIdentifier ?? "unconfigured").Replace("@", ".AT.");
            var ua = HttpClientHelpers.UserAgentString(id);
            var http = HttpClientHelpers.Create(userAgent: ua);
            http.Dispose();
            return ua;
        }
    }
}
