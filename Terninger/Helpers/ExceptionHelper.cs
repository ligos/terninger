using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.Helpers
{
    public static class ExceptionHelper
    {
        public static T TryAndIgnoreException<T>(Func<T> f)
        {
            return TryAndIgnoreException(f, default(T));
        }
        public static T TryAndIgnoreException<T>(Func<T> f, T fallback)
        {
            try
            {
                return f();
            }
            catch
            {
                return fallback;
            }
        }

        public static U TryAndIgnoreException<T, U>(this T x, Func<T, U> f)
        {
            return TryAndIgnoreException(x, f, default(U));
        }
        public static U TryAndIgnoreException<T, U>(this T x, Func<T, U> f, U fallback)
        {
            try
            {
                return f(x);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
