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
            bool ignored = false;
            return TryAndIgnoreException(f, default(T), ref ignored);
        }

        public static T TryAndIgnoreException<T>(Func<T> f, ref bool didFail)
        {
            return TryAndIgnoreException(f, default(T), ref didFail);
        }

        public static T TryAndIgnoreException<T>(Func<T> f, T fallback)
        {
            bool ignored = false;
            return TryAndIgnoreException(f, fallback, ref ignored);
        }
        public static T TryAndIgnoreException<T>(Func<T> f, T fallback, ref bool didFail)
        {
            try
            {
                return f();
            }
            catch
            {
                didFail = true;
                return fallback;
            }
        }

        public static U TryAndIgnoreException<T, U>(this T x, Func<T, U> f)
        {
            bool ignored = false;
            return TryAndIgnoreException(x, f, default(U), ref ignored);
        }
        public static U TryAndIgnoreException<T, U>(this T x, Func<T, U> f, ref bool didFail)
        {
            return TryAndIgnoreException(x, f, default(U), ref didFail);
        }
        public static U TryAndIgnoreException<T, U>(this T x, Func<T, U> f, U fallback)
        {
            bool ignored = false;
            return TryAndIgnoreException(x, f, fallback, ref ignored);
        }
        public static U TryAndIgnoreException<T, U>(this T x, Func<T, U> f, U fallback, ref bool didFail)
        {
            try
            {
                return f(x);
            }
            catch
            {
                didFail = true;
                return fallback;
            }
        }
    }
}
