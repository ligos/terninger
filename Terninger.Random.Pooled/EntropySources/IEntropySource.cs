using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Random;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// Interface to getting entropy from system events.
    /// See 9.5.1 of Fortuna spec.
    /// </summary>
    public interface IEntropySource : IDisposable
    {
        /// <summary>
        /// A unique name of the entropy source. Eg: Type name.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets entropy from the source.
        /// A source may return null if there is no entropy available.
        /// There is no limit to the amount of entropy which can be returned, but more than 4kB is overkill.
        /// Entropy Priority indicates how aggressively the source should read entropy.
        /// </summary>
        Task<byte[]> GetEntropyAsync(EntropyPriority priority);
    }
}
