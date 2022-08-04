using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.PersistentState
{
    /// <summary>
    /// An object which can write persistent state to a store.
    /// </summary>
    public interface IPersistentStateWriter
    {
        /// <summary>
        /// Details of the location where state will be written to.
        /// </summary>
        public string Location { get; }

        /// <summary>
        /// Writes all items to the store.
        /// </summary>
        Task WriteAsync(PersistentItemCollection items);
    }
}
