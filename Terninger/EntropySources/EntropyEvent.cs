using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// Represents a single 'packet' of entropy from a source.
    /// </summary>
    public sealed class EntropyEvent
    {
        public byte[] Entropy { get; private set; }
        public Type Source { get; private set; }

        public EntropyEvent(byte[] entropy, Type source)
        {
            this.Entropy = entropy;
            this.Source = source;
        }
    }
}
