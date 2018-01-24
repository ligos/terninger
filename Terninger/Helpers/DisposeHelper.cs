using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace MurrayGrant.Terninger.Helpers
{
    public static class DisposeHelper
    {
        /// <summary>
        /// Calls dispose() and ignores any ObjectDisposedExceptions.
        /// </summary>
        /// <param name="obj"></param>
        public static void TryDispose(this IDisposable obj)
        {
            if (obj != null)
            {
                try { obj.Dispose(); } catch (ObjectDisposedException) { }
            }
        }
        public static void TryDisposeAndNull(ref IDisposable obj)
        {
            if (obj != null)
            {
                try { obj.Dispose(); } catch (ObjectDisposedException) { }
                obj = null;
            }
        }
    }
}
