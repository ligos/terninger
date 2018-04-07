using System;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.EntropySources;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class EntropyTests
    {
        [TestMethod]
        public void CheapEntropyIsDifferentOnSubsequentCalls_16Bytes()
        {
            var e1 = CheapEntropy.Get16();
            System.Threading.Thread.Sleep(1);
            var e2 = CheapEntropy.Get16();
            CollectionAssert.AreNotEqual(e1, e2);
        }
        [TestMethod]
        public void CheapEntropyIsDifferentOnSubsequentCalls_32Bytes()
        {
            var e1 = CheapEntropy.Get32();
            System.Threading.Thread.Sleep(1);
            var e2 = CheapEntropy.Get32();
            CollectionAssert.AreNotEqual(e1, e2);
        }
        [TestMethod]
        public void StaticLocalIsSameOnSubsequentCalls()
        {
            var e1 = StaticLocalEntropy.Get32().GetAwaiter().GetResult();
            var e2 = StaticLocalEntropy.Get32().GetAwaiter().GetResult();
            CollectionAssert.AreEqual(e1, e2);
        }
    }
}
