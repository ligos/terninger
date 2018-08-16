using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;
using System.Security.Cryptography;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Random;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class RandomNumberExtensionTests
    {
        [TestMethod]
        public void Get10RandomBooleans()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            for (int i = 0; i < 10; i++)
                prng.GetRandomBoolean();
        }
        
        [TestMethod]
        public void Get10RandomInt32s()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            for (int i = 0; i < 10; i++)
                prng.GetRandomInt32();
        }

        [TestMethod]
        public void Get10RandomInt64s()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            for (int i = 0; i < 10; i++)
                prng.GetRandomInt64();
        }

        [TestMethod]
        public void Get10RandomSingles()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            for (int i = 0; i < 10; i++)
                prng.GetRandomSingle();
        }

        [TestMethod]
        public void Get10RandomDoubles()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            for (int i = 0; i < 10; i++)
                prng.GetRandomDouble();
        }
        
        [TestMethod]
        public void Get10RandomDecimals()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            for (int i = 0; i < 10; i++)
                prng.GetRandomDecimal();
        }

        [TestMethod]
        public void Get10RandomGuids()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            for (int i = 0; i < 10; i++)
                prng.GetRandomGuid();
        }



        private void WriteHistogramToTsv(int[] histogram, string filename)
        {
            using (var sw = new StreamWriter(filename, false, Encoding.UTF8))
            {
                for (int i = 0; i < histogram.Length; i++)
                {
                    sw.WriteLine("{0}\t{1}", i, histogram[i]);
                }
            }
        }
    }
}
