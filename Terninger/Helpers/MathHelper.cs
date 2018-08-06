using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace MurrayGrant.Terninger.Helpers
{
    public static class MathHelper
    {
        // RivRem() doesn't exist in netstandard1.3
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DivRem (int a, int b, out int result)
        {
#if NETSTANDARD2_0 || NET452
            return Math.DivRem(a, b, out result);
#else
            result = a % b;
            return a / b;
#endif
        }
    }
}
