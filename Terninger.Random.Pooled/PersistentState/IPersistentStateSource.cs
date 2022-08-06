using System;
using System.Collections.Generic;

namespace MurrayGrant.Terninger.PersistentState
{
    /// <summary>
    /// An object which can generate and receive persistent state.
    /// For example, an entropy source can implement this interface to load and save arbitrary state.
    /// </summary>
    /// <remarks>
    /// Note that any persisted state may be observed by an attacker and should not be considered secure.
    /// </remarks>
    public interface IPersistentStateSource
    {
        /// <summary>
        /// Initialises the object using the supplied persistent state dictionary.
        /// This is called once during initialisation of the PooledEntropyCprngGenerator.
        /// </summary>
        void Initialise(IDictionary<string, NamespacedPersistentItem> state);

        /// <summary>
        /// Returns true if the source wants to save state.
        /// False if there is no state to save at this time.
        /// </summary>
        bool HasUpdates { get; }

        /// <summary>
        /// Returns a state dictionary containing all current state.
        /// A dictionary should be returned even if no changes have been made to internal state since last call.
        /// This is called repeatedly by the PooledEntropyCprngGenerator to save state.
        /// </summary>
        IEnumerable<NamespacedPersistentItem> GetCurrentState(PersistentEventType eventType);
    }

    /// <summary>
    /// The type of event causing persistent state to be written.
    /// </summary>
    public enum PersistentEventType
    {
        /// <summary>
        /// A reseed has just occurred.
        /// </summary>
        Reseed = 1,

        /// <summary>
        /// A regular periodic interval for writing state.
        /// </summary>
        Periodic = 2,

        /// <summary>
        /// The pooled generator is stopping. This is the last opportunity to write persistent state.
        /// </summary>
        Stopping = 3,
    }
}
