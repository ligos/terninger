using System;
using System.Collections.Generic;
using System.Linq;

namespace MurrayGrant.Terninger.EntropySources
{
    public class EntropySourceComparer 
        : IEqualityComparer<IEntropySource>, IComparer<IEntropySource>
    {
        public static readonly EntropySourceComparer Value = new EntropySourceComparer();

        /// <summary>
        /// Equals is based on reference equality.
        /// </summary>
        public bool Equals(IEntropySource x, IEntropySource y) => Object.ReferenceEquals(x, y);

        /// <summary>
        /// GetHasCode is based on Name and Type.
        /// </summary>
        public int GetHashCode(IEntropySource obj) => obj == null ? typeof(IEntropySource).GetHashCode() : (obj.Name ?? "").GetHashCode() ^ obj.GetType().GetHashCode();

        /// <summary>
        /// Sorting is based on Name.
        /// </summary>
        public int Compare(IEntropySource x, IEntropySource y) 
        {
            if (x == null && y == null)
                return 0;
            if (x == null && y != null)
                return 1;
            if (x != null && y == null)
                return -1;
            return String.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }


    }
}
