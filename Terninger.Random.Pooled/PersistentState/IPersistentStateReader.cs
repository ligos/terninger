using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.PersistentState
{
    /// <summary>
    /// An object which can read persistent state from a store.
    /// </summary>
    public interface IPersistentStateReader
    {
        /// <summary>
        /// Details of the location where state will be read from.
        /// </summary>
        public string Location { get; }

        /// <summary>
        /// Reads all items from the underlying store.
        /// </summary>
        Task<PersistentItemCollection> ReadAsync();
    }
}
