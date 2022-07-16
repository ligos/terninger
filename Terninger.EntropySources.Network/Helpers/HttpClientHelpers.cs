using System;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;

namespace MurrayGrant.Terninger.Helpers
{
    public static class HttpClientHelpers
    {
        /// <summary>
        /// The default timeout used by HttpClient instances.
        /// </summary>
        public readonly static TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        internal const string _BasePathToUnitTestData = "../../../Online Generators/";


#if NET452
        static HttpClientHelpers()
        {
            // TODO: these are pretty sane defaults, but I'm worried about overriding what the consuming application might set.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 100;
        }
#endif
        /// <summary>
        /// Creates a UserAgent string for HttpClient.
        /// It is recommended to pass a usage identifier such as a website or email address.
        /// </summary>
        public static string UserAgentString(string usageIdentifier = "unconfigured") => $"Mozilla/5.0 (Microsoft.NET; {Environment.Version}; github.com/ligos/terninger) Terninger/{usageIdentifier}";

        /// <summary>
        /// Create an HttpClient with parameters or sane defaults.
        /// </summary>
        public static HttpClient Create(TimeSpan timeout = default(TimeSpan), string userAgent = "", SslProtocols sslProtocols = SslProtocols.None, Action<HttpClientHandler> handlerCustomisation = null)
        {
            // TODO: authentication.
            // TODO: certificate overrides.

            var handler = new HttpClientHandler();
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
#if NETSTANDARD2_0
            // TLS1.0+ by default, or whatever the user provides.
            handler.SslProtocols = sslProtocols == SslProtocols.None ? SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 : sslProtocols;
#elif NET6_0
            handler.SslProtocols = sslProtocols == SslProtocols.None ? SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13 : sslProtocols;
#endif
            if (handlerCustomisation != null)
                handlerCustomisation(handler);

            var result = new HttpClient(handler);
            result.DefaultRequestHeaders.Accept.Clear();
            result.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            result.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
            result.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

            result.DefaultRequestHeaders.AcceptEncoding.Clear();
            result.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            result.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));

            result.DefaultRequestHeaders.AcceptLanguage.Clear();
            result.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName));
            result.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));

            result.DefaultRequestHeaders.UserAgent.Clear();
            result.DefaultRequestHeaders.UserAgent.ParseAdd(String.IsNullOrEmpty(userAgent) ? UserAgentString() : userAgent);
            
            result.Timeout = timeout <= TimeSpan.Zero ? DefaultTimeout : timeout;

            return result;
        }

        public static async Task<string> PostStringAsync(this HttpClient hc, Uri uri, string body, string contentType)
        {
            var content = new StringContent(body, Encoding.UTF8, contentType);
            var response = await hc.PostAsync(uri, content);
            var result = await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
            return result;
        }
    }
}
