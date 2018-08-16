using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        /// <param name="hotBitsApiKey">API key for true random source at https://www.fourmilab.ch/hotbits </param>
        /// <param name="randomOrgApiKey">API for https://api.random.org </param>
        public static IEnumerable<IEntropySource> All(string userAgent = null, string hotBitsApiKey = null, Guid? randomOrgApiKey = null) => new IEntropySource[]
        {
            new PingStatsSource(),
            new ExternalWebContentSource(userAgent),
            new AnuExternalRandomSource(userAgent),
            new BeaconNistExternalRandomSource(userAgent),
            new HotbitsExternalRandomSource(userAgent, hotBitsApiKey),
            new RandomNumbersInfoExternalRandomSource(userAgent),
            new RandomOrgExternalRandomSource(userAgent, randomOrgApiKey.GetValueOrDefault()),
        };
    }
}
