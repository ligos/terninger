using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;

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
            Assert.AreEqual(prng.GetRandomBoolean(), false);
            Assert.AreEqual(prng.GetRandomBoolean(), false);
            Assert.AreEqual(prng.GetRandomBoolean(), false);
            Assert.AreEqual(prng.GetRandomBoolean(), true);
            Assert.AreEqual(prng.GetRandomBoolean(), true);
            Assert.AreEqual(prng.GetRandomBoolean(), true);
            Assert.AreEqual(prng.GetRandomBoolean(), true);
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
                    sw.WriteLine(b);
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
            Assert.AreEqual(prng.GetRandomInt32(), 676360365);
            Assert.AreEqual(prng.GetRandomInt32(), 1023835137);
            Assert.AreEqual(prng.GetRandomInt32(), 749283119);
            Assert.AreEqual(prng.GetRandomInt32(), 2065228089);
            Assert.AreEqual(prng.GetRandomInt32(), 1214441829);
            Assert.AreEqual(prng.GetRandomInt32(), 1388754928);
            Assert.AreEqual(prng.GetRandomInt32(), 1759670182);
            Assert.AreEqual(prng.GetRandomInt32(), 1053759929);
            Assert.AreEqual(prng.GetRandomInt32(), 2102486681);
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
            Assert.AreEqual(prng.GetRandomInt64(), 2788216454113161389L);
            Assert.AreEqual(prng.GetRandomInt64(), 6417094100205861889L);
            Assert.AreEqual(prng.GetRandomInt64(), 8597945428310828847L);
            Assert.AreEqual(prng.GetRandomInt64(), 3788444191187067193L);
            Assert.AreEqual(prng.GetRandomInt64(), 6070709392463030629L);
            Assert.AreEqual(prng.GetRandomInt64(), 3628414389813950448L);
            Assert.AreEqual(prng.GetRandomInt64(), 199927630569566118L);
            Assert.AreEqual(prng.GetRandomInt64(), 8480828181623806393L);
            Assert.AreEqual(prng.GetRandomInt64(), 4996107029703648921L);
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
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.6574774f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.7383802f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.1744561f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.4808484f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.2827593f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.8233447f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.9097052f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.2453476f.ToString());
            Assert.AreEqual(prng.GetRandomSingle().ToString(), 0.4895233f.ToString());
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
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.151149516845466.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.847871368224355.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.966095555614321.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.205371971121255.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.829093815591829.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.196696738205698.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.0108380985701701.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.959746616949641.ToString());
            Assert.AreEqual(prng.GetRandomDouble().ToString(), 0.27083950477874.ToString());
        }
        #endregion

        #region Decimal Tests
        [TestMethod]
        public void Get10RandomDecimals()
        {
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // First 10 decimals pre-computed based on null seed and default generator.
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.0081022815596536704233620207m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.1480592500720429064388445555m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.1489736693675262188569017973m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.0485231652742247921345617727m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.6416446104436163547437517015m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.5836767421679579593851251028m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.7789878883901745069531929859m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.9034659559424787395351562491m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.7322017876011287533343642467m.ToString());
            Assert.AreEqual(prng.GetRandomDecimal().ToString(), 0.0751568266059615199055138280m.ToString());
        }
        #endregion

        #region Guid Tests
        [TestMethod]
        public void Get10RandomGuids()
        {
            var prng = new BlockCypherCprngGenerator(new byte[32]);
            // First 10 guids pre-computed based on null seed and default generator.
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "5237d376-ebdf-481b-f298-101006618da8");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "a85070ad-bc17-46b1-4b4c-2f319ecfce98");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "bd067c01-1915-490e-d32b-3181f77d3b42");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "2ca9272f-09d0-4752-3566-63705c348c44");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "7b18e139-41eb-4493-3367-2225c7c86743");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "4862e965-7e07-443f-0ea8-abbcf732c9f3");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "d2c6b7f0-b7a9-425a-e4bf-3b96a2872763");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "e8e26fa6-491e-42c6-e4bd-3aff50e11db0");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "3ecf19b9-f44c-45b1-9ee8-db6d5095f87f");
            Assert.AreEqual(prng.GetRandomGuid().ToString(), "7d516699-bcdf-4555-44f5-99d8eba05a0b");
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
                    Assert.IsTrue(theVal >= 0.0f && theVal <= 1.0f);
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
                    Assert.IsTrue(theVal >= 0.0 && theVal <= 1.0);
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
                    Assert.IsTrue(theVal >= 0m && theVal <= 1.0m, "Decimal: " + theVal + ", i: " + i);
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
