using System;
using System.Collections.Generic;
using System.Text;

namespace MurrayGrant.Terninger.Helpers
{
    public class Unit
    {
        public static readonly Unit Value = new Unit();

        private Unit() { }
    }

    public static class UnitHelpers
    {
        public static Unit ToUnit(Action a)
        {
            a();
            return Unit.Value;
        }
        public static Unit ToUnit<T>(Action<T> a, T arg1)
        {
            a(arg1);
            return Unit.Value;
        }
    }
}
