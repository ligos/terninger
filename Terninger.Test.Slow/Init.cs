using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MurrayGrant.Terninger.Test.Slow
{
    [TestClass]
    public class Init
    {
        [AssemblyInitialize()]
        public static void AssemblyInit(TestContext context)
        {
            Terninger.LibLog.LogProvider.SetCurrentLogProvider(new Terninger.LibLog.ColoredConsoleLogProvider(true));
        }
    }
}
