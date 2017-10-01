using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;

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
        string Name { get; }

        /// <summary>
        /// Gets entropy from the source.
        /// </summary>
        Task<byte[]> GetEntropyAsync();
    }
}
