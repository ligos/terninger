using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Helpers;
using System.Collections;

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
            result.AddRange(Process.GetCurrentProcess().ProcessName.ToLongs());
            result.AddRange(Environment.CurrentDirectory.ToLongs());
            result.Add((Environment.Is64BitOperatingSystem ? 4 : 2)
                    + (Environment.Is64BitProcess ? 8 : 16));
            result.AddRange(Environment.MachineName.ToLongs());
            result.AddRange(Environment.OSVersion.VersionString.ToLongs());
            result.Add(Environment.ProcessorCount);
            result.AddRange(Environment.SystemDirectory.ToLongs());
            result.AddRange(Environment.UserDomainName.ToLongs());
            result.AddRange(Environment.Version.ToString().ToLongs());
            var flatEnvironmentVars = String.Join(";", Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().Select(x => x.Key.ToString() + ":" + x.Value.ToString()));
            result.AddRange(flatEnvironmentVars.ToLongs());

            result.AddRange(Local.NetworkStatsSource.GetNetworkInterfaceStaticProperties());

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
