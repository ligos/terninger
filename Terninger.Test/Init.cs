using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class Init
    {
        [AssemblyInitialize()]
        public static void AssemblyInit(TestContext context)
        {
            Terninger.LibLog.LogProvider.SetCurrentLogProvider(new Terninger.LibLog.ColoredConsoleLogProvider(LibLog.LogLevel.Trace, line => System.Diagnostics.Trace.WriteLine(line)));
        }
    }
}
