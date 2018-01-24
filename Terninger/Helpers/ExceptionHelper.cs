using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MurrayGrant.Terninger.Helpers
{
    public static class ExceptionHelper
    {
        public static U TryAndIgnoreException<T, U>(this T x, Func<T, U> f)
        {
            try
            {
                return f(x);
            }
            catch
            {
                return default(U);
            }
        }
    }
}
