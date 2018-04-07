using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;
using System.Security.Cryptography;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Generator;

namespace MurrayGrant.Terninger.Test.Slow
{
    [TestClass]
    public class RandomNumberExtensionTests
    {
        [TestMethod]
        [TestCategory("Random Distribution")]
        public void RandomBooleanDistribution()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            // Produces a histogram of 10000 random booleans and also writes the raw values out.
            var histogram = new int[2];
            using (var sw = new StreamWriter(nameof(RandomBooleanDistribution) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 10000; i++)
                {
                    var b = prng.GetRandomBoolean();
                    if (b)
                        histogram[1] = histogram[1] + 1;
                    else
                        histogram[0] = histogram[0] + 1;
                    sw.WriteLine(b ? 1 : 0);
                }
            }
            WriteHistogramToTsv(histogram, nameof(RandomBooleanDistribution) + ".txt");
        }

        [TestMethod]
        [TestCategory("Random Distribution")]
        public void RandomInt32Distribution_ZeroTo32()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            // Produces a histogram of 50000 random int32s in the range 0..32 and also writes the raw values out.
            var histogram = new int[32];
            using (var sw = new StreamWriter(nameof(RandomInt32Distribution_ZeroTo32) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 50000; i++)
                {
                    var theInt = prng.GetRandomInt32(32);
                    Assert.IsTrue(theInt >= 0 && theInt < 32);
                    histogram[theInt] = histogram[theInt] + 1;
                    sw.WriteLine(theInt);
                }
            }
            WriteHistogramToTsv(histogram, nameof(RandomInt32Distribution_ZeroTo32) + ".txt");
        }
        [TestMethod]
        [TestCategory("Random Distribution")]
        public void RandomInt32Distribution_ZeroTo33()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            // Produces a histogram of 50000 random int32s in the range 0..33 and also writes the raw values out.
            var histogram = new int[33];
            using (var sw = new StreamWriter(nameof(RandomInt32Distribution_ZeroTo33) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 50000; i++)
                {
                    var theInt = prng.GetRandomInt32(33);
                    Assert.IsTrue(theInt >= 0 && theInt < 33);
                    histogram[theInt] = histogram[theInt] + 1;
                    sw.WriteLine(theInt);
                }
            }
            WriteHistogramToTsv(histogram, nameof(RandomInt32Distribution_ZeroTo33) + ".txt");
        }
        [TestMethod]
        [TestCategory("Random Distribution")]
        public void RandomInt32Distribution_ZeroTo47()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            // Produces a histogram of 50000 random int32s in the range 0..47 and also writes the raw values out.
            var histogram = new int[47];
            using (var sw = new StreamWriter(nameof(RandomInt32Distribution_ZeroTo47) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 50000; i++)
                {
                    var theInt = prng.GetRandomInt32(47);
                    Assert.IsTrue(theInt >= 0 && theInt < 47);
                    histogram[theInt] = histogram[theInt] + 1;
                    sw.WriteLine(theInt);
                }
            }
            WriteHistogramToTsv(histogram, nameof(RandomInt32Distribution_ZeroTo47) + ".txt");
        }

        [TestMethod]
        [TestCategory("Random Distribution")]
        public void RandomInt64Distribution_ZeroTo32()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            // Produces a histogram of 50000 random int64s in the range 0..32 and also writes the raw values out.
            var histogram = new int[32];
            using (var sw = new StreamWriter(nameof(RandomInt64Distribution_ZeroTo32) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 50000; i++)
                {
                    var theLong = prng.GetRandomInt64(32);
                    Assert.IsTrue(theLong >= 0 && theLong < 32);
                    histogram[theLong] = histogram[theLong] + 1;
                    sw.WriteLine(theLong);
                }
            }
            WriteHistogramToTsv(histogram, nameof(RandomInt64Distribution_ZeroTo32) + ".txt");
        }
        [TestMethod]
        [TestCategory("Random Distribution")]
        public void RandomInt64Distribution_ZeroTo33()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            // Produces a histogram of 50000 random int32s in the range 0..33 and also writes the raw values out.
            var histogram = new int[33];
            using (var sw = new StreamWriter(nameof(RandomInt64Distribution_ZeroTo33) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 50000; i++)
                {
                    var theLong = prng.GetRandomInt64(33);
                    Assert.IsTrue(theLong >= 0 && theLong < 33);
                    histogram[theLong] = histogram[theLong] + 1;
                    sw.WriteLine(theLong);
                }
            }
            WriteHistogramToTsv(histogram, nameof(RandomInt64Distribution_ZeroTo33) + ".txt");
        }
        [TestMethod]
        [TestCategory("Random Distribution")]
        public void RandomInt64Distribution_ZeroTo47()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            // Produces a histogram of 50000 random int32s in the range 0..47 and also writes the raw values out.
            var histogram = new int[47];
            using (var sw = new StreamWriter(nameof(RandomInt64Distribution_ZeroTo47) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 50000; i++)
                {
                    var theLong = prng.GetRandomInt64(47);
                    Assert.IsTrue(theLong >= 0 && theLong < 47);
                    histogram[theLong] = histogram[theLong] + 1;
                    sw.WriteLine(theLong);
                }
            }
            WriteHistogramToTsv(histogram, nameof(RandomInt64Distribution_ZeroTo47) + ".txt");
        }


        [TestMethod]
        [TestCategory("Fuzzing")]
        public void RandomInt32_Fuzzing()
        {
            var prng = CypherBasedPrngGenerator.CreateWithCheapKey();
            // Dumps 100000 random int32s.
            using (var sw = new StreamWriter(nameof(RandomInt32_Fuzzing) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 100000; i++)
                {
                    var theInt = prng.GetRandomInt32();
                    Assert.IsTrue(theInt >= 0 && theInt <= Int32.MaxValue);
                    sw.WriteLine(theInt);
                }
            }
        }
        [TestMethod]
        [TestCategory("Fuzzing")]
        public void RandomInt64_Fuzzing()
        {
            var prng = CypherBasedPrngGenerator.CreateWithCheapKey();
            // Dumps 100000 random int64s.
            using (var sw = new StreamWriter(nameof(RandomInt64_Fuzzing) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 100000; i++)
                {
                    var theLong = prng.GetRandomInt64();
                    Assert.IsTrue(theLong >= 0 && theLong <= Int64.MaxValue);
                    sw.WriteLine(theLong);
                }
            }
        }
        [TestMethod]
        [TestCategory("Fuzzing")]
        public void RandomSingle_Fuzzing()
        {
            var prng = CypherBasedPrngGenerator.CreateWithCheapKey();
            // Dumps 100000 random singles.
            using (var sw = new StreamWriter(nameof(RandomSingle_Fuzzing) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 100000; i++)
                {
                    var theVal = prng.GetRandomSingle();
                    Assert.IsTrue(theVal >= 0.0f && theVal < 1.0f);
                    sw.WriteLine(theVal);
                }
            }
        }
        [TestMethod]
        [TestCategory("Fuzzing")]
        public void RandomDouble_Fuzzing()
        {
            var prng = CypherBasedPrngGenerator.CreateWithCheapKey();
            // Dumps 100000 random doubles.
            using (var sw = new StreamWriter(nameof(RandomDouble_Fuzzing) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 100000; i++)
                {
                    var theVal = prng.GetRandomDouble();
                    Assert.IsTrue(theVal >= 0.0 && theVal < 1.0);
                    sw.WriteLine(theVal);
                }
            }
        }
        [TestMethod]
        [TestCategory("Fuzzing")]
        public void RandomDecimal_Fuzzing()
        {
            var prng = CypherBasedPrngGenerator.CreateWithCheapKey();
            // Dumps 100000 random decimals.
            using (var sw = new StreamWriter(nameof(RandomDecimal_Fuzzing) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 100000; i++)
                {
                    var theVal = prng.GetRandomDecimal();
                    Assert.IsTrue(theVal >= 0m && theVal < 1.0m, "Decimal: " + theVal + ", i: " + i);
                    sw.WriteLine(theVal);
                }
            }
        }

        [TestMethod]
        [TestCategory("Fuzzing")]
        public void RandomGuid_Fuzzing()
        {
            var prng = new CypherBasedPrngGenerator(new byte[32]);
            // Dumps 100000 random guids.
            using (var sw = new StreamWriter(nameof(RandomGuid_Fuzzing) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 100000; i++)
                {
                    var theVal = prng.GetRandomGuid();
                    sw.WriteLine(theVal);
                }
            }
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
