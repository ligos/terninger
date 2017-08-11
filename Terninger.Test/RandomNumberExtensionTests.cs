using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;
using System.Security.Cryptography;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Generator;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class RandomNumberExtensionTests
    {
        #region Boolean Tests
        [TestMethod]
        public void Get10RandomBooleans()
        {
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // First 10 booleans pre-computed based on null seed and default generator.
            Assert.AreEqual(prng.GetRandomBoolean(), false);
            Assert.AreEqual(prng.GetRandomBoolean(), true);
            Assert.AreEqual(prng.GetRandomBoolean(), false);
            Assert.AreEqual(prng.GetRandomBoolean(), true);
            Assert.AreEqual(prng.GetRandomBoolean(), true);
            Assert.AreEqual(prng.GetRandomBoolean(), true);
            Assert.AreEqual(prng.GetRandomBoolean(), true);
            Assert.AreEqual(prng.GetRandomBoolean(), false);
            Assert.AreEqual(prng.GetRandomBoolean(), false);
            Assert.AreEqual(prng.GetRandomBoolean(), false);
        }

        [TestMethod]
        [TestCategory("Random Distribution")]
        public void RandomBooleanDistribution()
        {
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // Produces a histogram of 1000 random booleans and also writes the raw values out.
            var histogram = new int[2];
            using (var sw = new StreamWriter(nameof(RandomBooleanDistribution) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 1000; i++)
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
        #endregion

        #region Int32 Tests
        [TestMethod]
        public void Get10RandomInt32s()
        {
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // First 10 int32s pre-computed based on null seed and default generator.
            Assert.AreEqual(prng.GetRandomInt32(), 1379390326);
            Assert.AreEqual(prng.GetRandomInt32(), 808803495);
            Assert.AreEqual(prng.GetRandomInt32(), 1400012872);
            Assert.AreEqual(prng.GetRandomInt32(), 1880550281);
            Assert.AreEqual(prng.GetRandomInt32(), 440619936);
            Assert.AreEqual(prng.GetRandomInt32(), 1546239972);
            Assert.AreEqual(prng.GetRandomInt32(), 922402963);
            Assert.AreEqual(prng.GetRandomInt32(), 1060928796);
            Assert.AreEqual(prng.GetRandomInt32(), 1399216208);
            Assert.AreEqual(prng.GetRandomInt32(), 573248555);
        }

        [TestMethod]
        [TestCategory("Random Distribution")]
        public void RandomInt32Distribution_ZeroTo32()
        {
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // Produces a histogram of 10000 random int32s in the range 0..32 and also writes the raw values out.
            var histogram = new int[32];
            using (var sw = new StreamWriter(nameof(RandomInt32Distribution_ZeroTo32) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 10000; i++)
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
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // Produces a histogram of 10000 random int32s in the range 0..33 and also writes the raw values out.
            var histogram = new int[33];
            using (var sw = new StreamWriter(nameof(RandomInt32Distribution_ZeroTo33) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 10000; i++)
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
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // Produces a histogram of 10000 random int32s in the range 0..47 and also writes the raw values out.
            var histogram = new int[47];
            using (var sw = new StreamWriter(nameof(RandomInt32Distribution_ZeroTo47) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 10000; i++)
                {
                    var theInt = prng.GetRandomInt32(47);
                    Assert.IsTrue(theInt >= 0 && theInt < 47);
                    histogram[theInt] = histogram[theInt] + 1;
                    sw.WriteLine(theInt);
                }
            }
            WriteHistogramToTsv(histogram, nameof(RandomInt32Distribution_ZeroTo47) + ".txt");
        }
        #endregion

        #region Int64 Tests
        [TestMethod]
        public void Get10RandomInt64s()
        {
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // First 10 int64s pre-computed based on null seed and default generator.
            Assert.AreEqual(prng.GetRandomInt64(), 8654770453312164726L);
            Assert.AreEqual(prng.GetRandomInt64(), 7469550012384763047L);
            Assert.AreEqual(prng.GetRandomInt64(), 6346881188555292744L);
            Assert.AreEqual(prng.GetRandomInt64(), 8722054058557762441L);
            Assert.AreEqual(prng.GetRandomInt64(), 4252649559198749600L);
            Assert.AreEqual(prng.GetRandomInt64(), 8494673073619451876L);
            Assert.AreEqual(prng.GetRandomInt64(), 2284073845989752979L);
            Assert.AreEqual(prng.GetRandomInt64(), 6991606773492514076L);
            Assert.AreEqual(prng.GetRandomInt64(), 3192783877842557008L);
            Assert.AreEqual(prng.GetRandomInt64(), 2456647708210566187L);
        }

        [TestMethod]
        [TestCategory("Random Distribution")]
        public void RandomInt64Distribution_ZeroTo32()
        {
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // Produces a histogram of 10000 random int64s in the range 0..32 and also writes the raw values out.
            var histogram = new int[32];
            using (var sw = new StreamWriter(nameof(RandomInt64Distribution_ZeroTo32) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 10000; i++)
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
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // Produces a histogram of 10000 random int32s in the range 0..33 and also writes the raw values out.
            var histogram = new int[33];
            using (var sw = new StreamWriter(nameof(RandomInt64Distribution_ZeroTo33) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 10000; i++)
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
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // Produces a histogram of 10000 random int32s in the range 0..47 and also writes the raw values out.
            var histogram = new int[47];
            using (var sw = new StreamWriter(nameof(RandomInt64Distribution_ZeroTo47) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 10000; i++)
                {
                    var theLong = prng.GetRandomInt64(47);
                    Assert.IsTrue(theLong >= 0 && theLong < 47);
                    histogram[theLong] = histogram[theLong] + 1;
                    sw.WriteLine(theLong);
                }
            }
            WriteHistogramToTsv(histogram, nameof(RandomInt64Distribution_ZeroTo47) + ".txt");
        }
        #endregion

        #region Single Tests
        [TestMethod]
        public void Get10RandomSingles()
        {
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // First 10 singles pre-computed based on null seed and default generator.
            // ToString() to avoid tiny floating point variences.
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.3211643f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.1883142f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.3259659f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.4378497f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.1025898f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.3600121f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.7147637f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.2470167f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.3257804f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.1334698f.ToString());
        }
        #endregion

        #region Double Tests
        [TestMethod]
        public void Get10RandomDoubles()
        {
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // First 10 doubles pre-computed based on null seed and default generator.
            // ToString() to avoid tiny floating point variences.
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.46917604639222.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.404925117545834.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.344065118656951.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.972823497941217.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.73053659454514.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.960497150048616.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.123819891296971.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.879015762649248.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.17308116083168.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.633175139113672.ToString());
        }
        #endregion

        #region Decimal Tests
        [TestMethod]
        public void Get10RandomDecimals()
        {
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // First 10 decimals pre-computed based on null seed and default generator.
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.0081022815596536704233620207m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.1728983696172048659374440412m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.2318661609120054862373727728m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.9710220444652337970656770995m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.3223273427046060035578193842m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.7837991975655597542955880597m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.2825695350283624777156784656m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.0355142980858084371777122560m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.4660458244087857635056655073m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.5297113959815468733341487865m.ToString());
        }
        #endregion

        #region Guid Tests
        [TestMethod]
        public void Get10RandomGuids()
        {
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // First 10 guids pre-computed based on null seed and default generator.
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "5237d376-ebdf-481b-b298-101006618da8");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "30355ca7-2c29-47a9-842e-62d94c8a38c3");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "53728048-a6d0-4814-88f9-dac1e2714518");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "7016eb89-f5f4-490a-b921-c5078c51a736");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "1a4353a0-723e-4b04-9c48-945ae8c76f27");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "5c29bfe4-2427-45e3-819b-45e6a629a236");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "b6fac093-a90f-4fb2-93d7-42027ade856b");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "3f3c7d1c-2d51-4107-aec0-48e84d676eff");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "53665850-0c05-4c4f-8cb0-bad37e5fdf29");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "222b142b-c413-4217-a41b-3d7c2b95dce6");
        }
        #endregion


        #region Fuzzing Tests
        [TestMethod]
        [TestCategory("Random Fuzzing")]
        public void RandomInt32_Fuzzing()
        {
            // TODO: generate a random key and 10000 numbers and check they match required criteria.

            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // Dumps 10000 random int32s.
            using (var sw = new StreamWriter(nameof(RandomInt32_Fuzzing) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 10000; i++)
                {
                    var theInt = prng.GetRandomInt32();
                    Assert.IsTrue(theInt >= 0 && theInt <= Int32.MaxValue);
                    sw.WriteLine(theInt);
                }
            }
        }
        [TestMethod]
        [TestCategory("Random Fuzzing")]
        public void RandomInt64_Fuzzing()
        {
            // TODO: generate a random key and 10000 numbers and check they match required criteria.

            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // Dumps 10000 random int64s.
            using (var sw = new StreamWriter(nameof(RandomInt64_Fuzzing) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 10000; i++)
                {
                    var theLong = prng.GetRandomInt64();
                    Assert.IsTrue(theLong >= 0 && theLong <= Int64.MaxValue);
                    sw.WriteLine(theLong);
                }
            }
        }
        [TestMethod]
        [TestCategory("Random Fuzzing")]
        public void RandomSingle_Fuzzing()
        {
            // TODO: generate a random key and 10000 numbers and check they match required criteria.

            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // Dumps 10000 random singles.
            using (var sw = new StreamWriter(nameof(RandomSingle_Fuzzing) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 10000; i++)
                {
                    var theVal = prng.GetRandomSingle();
                    Assert.IsTrue(theVal >= 0.0f && theVal < 1.0f);
                    sw.WriteLine(theVal);
                }
            }
        }
        [TestMethod]
        [TestCategory("Random Fuzzing")]
        public void RandomDouble_Fuzzing()
        {
            // TODO: generate a random key and 10000 numbers and check they match required criteria.

            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // Dumps 10000 random doubles.
            using (var sw = new StreamWriter(nameof(RandomDouble_Fuzzing) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 10000; i++)
                {
                    var theVal = prng.GetRandomDouble();
                    Assert.IsTrue(theVal >= 0.0 && theVal < 1.0);
                    sw.WriteLine(theVal);
                }
            }
        }
        [TestMethod]
        [TestCategory("Random Fuzzing")]
        public void RandomDecimal_Fuzzing()
        {
            // TODO: generate a random key and 10000 numbers and check they match required criteria.

            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // Dumps 10000 random decimals.
            using (var sw = new StreamWriter(nameof(RandomDecimal_Fuzzing) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 10000; i++)
                {
                    var theVal = prng.GetRandomDecimal();
                    Assert.IsTrue(theVal >= 0m && theVal < 1.0m, "Decimal: " + theVal + ", i: " + i);
                    sw.WriteLine(theVal);
                }
            }
        }


        [TestMethod]
        [TestCategory("Random Fuzzing")]
        public void RandomGuid_Fuzzing()
        {
            // TODO: generate a random key and 10000 numbers and check they match required criteria.

            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // Dumps 10000 random guids.
            using (var sw = new StreamWriter(nameof(RandomGuid_Fuzzing) + ".raw.txt", false, Encoding.UTF8))
            {
                for (int i = 0; i < 10000; i++)
                {
                    var theVal = prng.GetRandomGuid();
                    sw.WriteLine(theVal);
                }
            }
        }
        #endregion

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
