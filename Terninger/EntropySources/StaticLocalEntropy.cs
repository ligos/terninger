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
    /// </summary>
    public static class StaticLocalEntropy
    {
        public static Task<byte[]> Get32()
        {
            return Task.Run(() =>
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

                var digest = ByteArrayHelpers.LongsToDigestBytes(result.ToArray());
                return digest;
            });
        }
    }
}
