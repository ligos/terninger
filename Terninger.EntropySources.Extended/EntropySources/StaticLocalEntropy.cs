using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Helpers;
using System.Collections;
using MurrayGrant.Terninger.LibLog;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// Sources of entropy which are local but change infrequently (eg: hostname, network address, memory, CPU).
    /// Can be used to differentiate external events which are similar (eg: the content of a news website). 
    /// </summary>
    public static class StaticLocalEntropy
    {
        internal static long[] GetLongsForDigest()
        {
            var result = new List<long>();
            result.AddRange(ExceptionHelper.TryAndIgnoreException(() => Process.GetCurrentProcess().ProcessName.ToLongs(), Enumerable.Empty<long>()));
            result.AddRange(ExceptionHelper.TryAndIgnoreException(() => Environment.CurrentDirectory.ToLongs(), Enumerable.Empty<long>()));
            result.Add((Environment.Is64BitOperatingSystem ? 4 : 2)
                    + (Environment.Is64BitProcess ? 8 : 16));
            result.AddRange(ExceptionHelper.TryAndIgnoreException(() => Environment.MachineName.ToLongs(), Enumerable.Empty<long>()));
            result.AddRange(ExceptionHelper.TryAndIgnoreException(() => Environment.OSVersion.VersionString.ToLongs(), Enumerable.Empty<long>()));
            result.Add(Environment.ProcessorCount);
            result.AddRange(ExceptionHelper.TryAndIgnoreException(() => Environment.SystemDirectory.ToLongs(), Enumerable.Empty<long>()));
            result.AddRange(ExceptionHelper.TryAndIgnoreException(() => Environment.UserDomainName.ToLongs(), Enumerable.Empty<long>()));
            result.AddRange(ExceptionHelper.TryAndIgnoreException(() => Environment.Version.ToString().ToLongs(), Enumerable.Empty<long>()));
            result.AddRange(ExceptionHelper.TryAndIgnoreException(() => String.Join(";", Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().Select(x => x.Key.ToString() + ":" + x.Value.ToString())).ToLongs(), Enumerable.Empty<long>()));

            // The DHCP lifetime listed in here ticks down slowly, making this change slowly over time.
            result.AddRange(ExceptionHelper.TryAndIgnoreException(() => Local.NetworkStatsSource.GetNetworkInterfaceStaticProperties(), Enumerable.Empty<long>()));

            return result.ToArray();
        }
        public static Task<byte[]> Get32()
        {
            return Task.Run(() =>
            {
                var entropy = GetLongsForDigest();
                var digest = ByteArrayHelpers.LongsToDigestBytes(entropy.ToArray());
                return digest;
            });
        }
    }
}
