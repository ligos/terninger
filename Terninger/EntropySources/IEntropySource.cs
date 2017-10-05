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
        /// Initialise the entropy source. Returns a task indicating if it was successful or not.
        /// </summary>
        Task<EntropySourceInitialisationResult> Initialise();

        /// <summary>
        /// Gets entropy from the source.
        /// A source may return null if there is no entropy available.
        /// There is no limit to the amount of entropy which can be returned, but more than 64kB is overkill.
        /// </summary>
        Task<byte[]> GetEntropyAsync();
    }

    public enum EntropySourceInitialisationResult
    {
        Successful = 0,
        Failure = 1,

        NotSupported = 64,
        MissingHardware = 65,
        DisabledByConfig = 66,
        NoPermission = 67,
        InvalidConfig = 68,

        PendingUserPermission = 128,
        PendingUserConfig = 129,
    }
}
