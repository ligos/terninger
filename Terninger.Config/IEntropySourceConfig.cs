using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace MurrayGrant.Terninger.Config
{
    /// <summary>
    /// Interface for entropy sources to access configuration variables.
    /// </summary>
    public interface IEntropySourceConfig
    {
        /// <summary>
        /// Returns the value of a named configuration item.
        /// Null represents the value does not exist in the config source.
        /// Empty string represents a value set to empty.
        /// </summary>
        string Get(string name);

        /// <summary>
        /// Returns true if the named configuration item is defined in the config source, false otherwise.
        /// </summary>
        bool ContainsKey(string name);
    }
}
