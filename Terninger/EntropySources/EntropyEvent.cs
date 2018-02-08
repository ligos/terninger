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
        public IEntropySource Source { get; private set; }

        public EntropyEvent(byte[] entropy, IEntropySource source)
        {
            this.Entropy = entropy;
            this.Source = source;
        }

        internal IEnumerable<byte[]> ToChunks(int maxLength)
        {
            // For small amounts of entropy, we can return immediately.
            if (Entropy.Length <= maxLength)
            {
                yield return Entropy;
                yield break;
            }

            // Break up the entropy into chunks.
            int c = 0;
            var buf = new byte[maxLength];      // PERF: one allocation, assumes the result of this is consumed immediately.
            while ((c+1) * maxLength <= Entropy.Length)
            {
                Buffer.BlockCopy(Entropy, c * maxLength, buf, 0, buf.Length);

                c = c + 1;
                yield return buf;
            }

            // And any final chunk.
            buf = new byte[Entropy.Length - (c * maxLength)];
            if (buf.Length > 0)
            {
                Buffer.BlockCopy(Entropy, c * maxLength, buf, 0, buf.Length);
                yield return buf;
            }
        }
    }
}
