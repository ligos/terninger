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
            var e2 = CheapEntropy.Get16();
            CollectionAssert.AreNotEqual(e1, e2);
        }
        [TestMethod]
        public void CheapEntropyIsDifferentOnSubsequentCalls_32Bytes()
        {
            var e1 = CheapEntropy.Get32();
            var e2 = CheapEntropy.Get32();
            CollectionAssert.AreNotEqual(e1, e2);
        }
        [TestMethod]
        public void StaticLocalIsAlmostSameOnSubsequentCalls()
        {
            var e1 = StaticLocalEntropy.Get32().GetAwaiter().GetResult();
            System.Threading.Thread.Sleep(1);
            var e2 = StaticLocalEntropy.Get32().GetAwaiter().GetResult();
            int bytesSame = 0, bytesDifferent = 0, checkedLength = Math.Min(e1.Length, e2.Length);
            for (int i = 0; i < checkedLength; i++)
            {
                if (e1[i] == e2[i])
                    bytesSame = bytesSame + 1;
                else
                    bytesDifferent = bytesDifferent + 1;
            }
            Assert.IsTrue(checkedLength - bytesDifferent <= 32);
        }
    }
}
