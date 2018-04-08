using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Net;

namespace MurrayGrant.Terninger.Helpers
{
    public static class WebClientHelpers
    {
        internal const string DefaultUserAgent = "Mozilla/5.0; Microsoft.NET; bitbucket.org/ligos/Terninger; unconfigured";

        static WebClientHelpers()
        {
            // TODO: these are pretty sane defaults, but I'm worried about overriding what the consuming application might set.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 100;
        }

        public static WebClient Create(TimeSpan timeout = default(TimeSpan), string userAgent = "")
        {
            var wc = new CustomWebClient(timeout);
            wc.Headers.Add("Accept", "text/html, application/xhtml+xml, */*");
            wc.Headers.Add("Accept-Encoding", "gzip, deflate");
            wc.Headers.Add("Accept-Language", "en");
            wc.Headers.Add("User-Agent", String.IsNullOrEmpty(userAgent) ? DefaultUserAgent : userAgent);
            return wc;
        }

        public class CustomWebClient : WebClient
        {
            private readonly TimeSpan _Timeout;

            public CustomWebClient() : this(TimeSpan.FromSeconds(30)) { }
            public CustomWebClient(TimeSpan timeout)
            {
                if (timeout < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Negative timeout is not permitted");
                this._Timeout = timeout == TimeSpan.Zero ? TimeSpan.FromSeconds(30) : timeout;      // Default to 30 second timeout.
            }
            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = (HttpWebRequest)base.GetWebRequest(address);
                request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                request.Timeout = unchecked((int)_Timeout.TotalMilliseconds);
                return request;
            }
        }
    }
}
