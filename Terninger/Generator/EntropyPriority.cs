using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.Generator
{
    /// <summary>
    /// Represents how aggressively the scheduler is trying to read entropy.
    /// </summary>
    public enum EntropyPriority
    {
        /// <summary>
        /// The generator has not been used in considerable time, only minimal entropy is being read.
        /// Entropy sources may avoid or delay retrieving data in this state.
        /// </summary>
        Low = 1,

        /// <summary>
        /// The generator has been used recently, entropy is being read at a normal rate.
        /// </summary>
        Normal = 2,

        /// <summary>
        /// The generator requires new seed material immediately, entropy is being read at the fastest possible rate.
        /// Entropy sources may ignore limits or timeouts to retreive additional data in this state.
        /// </summary>
        High = 3,

    }
}
