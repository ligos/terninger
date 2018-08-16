using System;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Helpers;
using MurrayGrant.Terninger.Random;

namespace MurrayGrant.Terninger.Test
{
    [TestClass]
    public class CypherCounterTests
    {
        [TestMethod]
        public void DefaultCounterIsZero()
        {
            var cc = new CypherCounter(16);
            Assert.AreEqual("00000000000000000000000000000000", cc.GetCounter().ToHexString());
        }
        [TestMethod]
        public void IncrementOnce()
        {
            var cc = new CypherCounter(16);
            cc.Increment();
            Assert.AreEqual("01000000000000000000000000000000", cc.GetCounter().ToHexString());
        }
        [TestMethod]
        public void IncrementTwice()
        {
            var cc = new CypherCounter(16);
            cc.Increment();
            cc.Increment();
            Assert.AreEqual("02000000000000000000000000000000", cc.GetCounter().ToHexString());
        }

        [TestMethod]
        public void IncrementInitialiseFromArray()
        {
            var cc = new CypherCounter(16, "02000000000000000000000000000000".ParseFromHexString());
            Assert.AreEqual("02000000000000000000000000000000", cc.GetCounter().ToHexString());
        }

        [TestMethod]
        public void OverflowFirstInt32()
        {
            var cc = new CypherCounter(16, "ffffffff000000000000000000000000".ParseFromHexString());
            cc.Increment();
            Assert.AreEqual("00000000010000000000000000000000", cc.GetCounter().ToHexString());
        }
        [TestMethod]
        public void OverflowFirstInt64()
        {
            var cc = new CypherCounter(16, "ffffffffffffffff0000000000000000".ParseFromHexString());
            cc.Increment();
            Assert.AreEqual("00000000000000000100000000000000", cc.GetCounter().ToHexString());
        }
        [TestMethod]
        public void OverflowSecondInt64()
        {
            var cc = new CypherCounter(16, "ffffffffffffffffffffffffffffffff".ParseFromHexString());
            cc.Increment();
            Assert.AreEqual("00000000000000000000000000000000", cc.GetCounter().ToHexString());
        }

        [TestMethod]
        public void Default32ByteCounterIsZero()
        {
            var cc = new CypherCounter(32);
            Assert.AreEqual("0000000000000000000000000000000000000000000000000000000000000000", cc.GetCounter().ToHexString());
        }
        [TestMethod]
        public void Overflow32ByteChunk1()
        {
            var cc = new CypherCounter(32, "ffffffffffffffff000000000000000000000000000000000000000000000000".ParseFromHexString());
            cc.Increment();
            Assert.AreEqual("0000000000000000010000000000000000000000000000000000000000000000", cc.GetCounter().ToHexString());
        }
        [TestMethod]
        public void Overflow32ByteChunk2()
        {
            var cc = new CypherCounter(32, "ffffffffffffffffffffffffffffffff00000000000000000000000000000000".ParseFromHexString());
            cc.Increment();
            Assert.AreEqual("0000000000000000000000000000000001000000000000000000000000000000", cc.GetCounter().ToHexString());
        }
        [TestMethod]
        public void Overflow32ByteChunk3()
        {
            var cc = new CypherCounter(32, "ffffffffffffffffffffffffffffffffffffffffffffffff0000000000000000".ParseFromHexString());
            cc.Increment();
            Assert.AreEqual("0000000000000000000000000000000000000000000000000100000000000000", cc.GetCounter().ToHexString());
        }
        [TestMethod]
        public void Overflow32ByteChunk4()
        {
            var cc = new CypherCounter(32, "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff".ParseFromHexString());
            cc.Increment();
            Assert.AreEqual("0000000000000000000000000000000000000000000000000000000000000000", cc.GetCounter().ToHexString());
        }
        [TestMethod]
        public void Overflow8ByteChunk1()
        {
            var cc = new CypherCounter(8, "ffffffffffffffff".ParseFromHexString());
            cc.Increment();
            Assert.AreEqual("0000000000000000", cc.GetCounter().ToHexString());
        }

        [TestMethod]
        public void EightByteCounterSupported()
        {
            var cc = new CypherCounter(8);
            Assert.AreEqual("0000000000000000", cc.GetCounter().ToHexString());
        }
    }
}
