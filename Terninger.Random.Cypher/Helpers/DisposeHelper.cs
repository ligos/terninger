using System;


namespace MurrayGrant.Terninger.Helpers
{
    public static class DisposeHelper
    {
        /// <summary>
        /// Calls dispose() and ignores any Exceptions.
        /// </summary>
        /// <param name="obj"></param>
        public static void TryDispose(this IDisposable obj)
        {
            if (obj != null)
            {
                try { obj.Dispose(); } catch (Exception) { }
            }
        }
        public static void TryDisposeAndNull(ref IDisposable obj)
        {
            if (obj != null)
            {
                try { obj.Dispose(); } catch (Exception) { }
                obj = null;
            }
        }

        public static void ThrowIfDisposed(this IDisposable obj, bool isDisposed)
        {
            if (isDisposed)
                throw new ObjectDisposedException(obj?.GetType()?.Name ?? "");
        }
    }
}
