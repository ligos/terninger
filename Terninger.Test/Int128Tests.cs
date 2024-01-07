using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;
using System.Security.Cryptography;

using BigMath;
using BigMathInt128 = BigMath.Int128;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class Int128Tests
    {
        [TestMethod]
        public void CheckZeroIntConversion()
        {
            var zero128 = BigMathInt128.Zero;
            var zero32 = 0;
            var zero64 = 0L;
            Assert.IsTrue(zero128 == zero32);
            Assert.IsTrue(zero128 == zero64);
        }

        [TestMethod]
        public void CheckZeroToString()
        {
            var zero128 = BigMathInt128.Zero;
            Assert.IsTrue(zero128.ToString() == "0");
        }

        public void ToStringFor32k()
        {
            var num = BigMathInt128.Zero;
            for (int i = 0; i < Int16.MaxValue + 10; i++)
            {
                Assert.IsTrue(num.ToString() == i.ToString());
                num = num + 1;
            }
        }
    }
}
