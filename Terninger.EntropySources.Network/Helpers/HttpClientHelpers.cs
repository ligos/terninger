using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;

namespace MurrayGrant.Terninger.Helpers
{
    public static class HttpClientHelpers
    {
        internal const string DefaultUserAgent = "Mozilla/5.0; Microsoft.NET; bitbucket.org/ligos/Terninger; unconfigured";
        internal const string _BasePathToUnitTestData = "../../../Online Generators/";

#if NET452
        static HttpClientHelpers()
        {
            // TODO: these are pretty sane defaults, but I'm worried about overriding what the consuming application might set.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 100;
        }
#endif

        public static HttpClient Create(TimeSpan timeout = default(TimeSpan), string userAgent = "", SslProtocols sslProtocols = SslProtocols.None, Action<HttpClientHandler> handlerCustomisation = null)
        {
            // TODO: authentication.
            // TODO: certificate overrides.

            var handler = new HttpClientHandler();
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
#if !NET452
            // TLS1.0+ by default, or 
            handler.SslProtocols = sslProtocols == SslProtocols.None ? SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 : sslProtocols;
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
            result.DefaultRequestHeaders.UserAgent.ParseAdd(String.IsNullOrEmpty(userAgent) ? DefaultUserAgent : userAgent);

            result.Timeout = timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : timeout;

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
