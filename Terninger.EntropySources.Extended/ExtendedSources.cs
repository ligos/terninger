using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.EntropySources.Local;

// For unit testing; some sources internal methods to assist testing.
[assembly: InternalsVisibleTo("Terninger.Test"),
           InternalsVisibleTo("Terninger.Test.Slow")]

namespace MurrayGrant.Terninger
{
    /// <summary>
    /// Additional local and cross platform sources.
    /// </summary>
    public class ExtendedSources
    {
        /// <summary>
        /// Additional entropy sources, local to this computer.
        /// </summary>
        public static IEnumerable<IEntropySource> All() => new IEntropySource[]
        {
            new NetworkStatsSource(),
            new ProcessStatsSource(),
        };
    }
}
