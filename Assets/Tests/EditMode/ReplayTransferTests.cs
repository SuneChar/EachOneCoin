using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class ReplayTransferTests
    {
        private GameObject owner;
        private Component transfer;
        private Type transferType, beginType, chunkType, endType;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            transferType = Type.GetType("EachOneCoin.Networking.NetworkReplayTransfer, Assembly-CSharp", true);
            beginType = Type.GetType("EachOneCoin.Networking.ReplayTransferBegin, Assembly-CSharp", true);
            chunkType = Type.GetType("EachOneCoin.Networking.ReplayTransferChunk, Assembly-CSharp", true);
            endType = Type.GetType("EachOneCoin.Networking.ReplayTransferEnd, Assembly-CSharp", true);
            owner = new GameObject("Replay transfer test");
            transfer = owner.AddComponent(transferType);
        }

        [TearDown]
        public void TearDown() { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }

        private void Begin(int length)
        {
            object message = Activator.CreateInstance(beginType);
            beginType.GetField("length").SetValue(message, length);
            beginType.GetField("sha256").SetValue(message, new string('0', 64));
            transferType.GetMethod("BeginReceive", Private).Invoke(transfer, new[] { message });
        }

        private object Buffer => transferType.GetField("receiving", Private).GetValue(transfer);

        [Test]
        public void Receive_RejectsOversizedTransfer()
        {
            Begin(16 * 1024 * 1024 + 1);
            Assert.That(Buffer, Is.Null);
        }

        [Test]
        public void Receive_OutOfOrderChunk_DiscardsPartialTransfer()
        {
            Begin(8);
            Assert.That(Buffer, Is.Not.Null);
            object chunk = Activator.CreateInstance(chunkType);
            chunkType.GetField("offset").SetValue(chunk, 1);
            chunkType.GetField("bytes").SetValue(chunk, new byte[] { 1 });
            transferType.GetMethod("ReceiveChunk", Private).Invoke(transfer, new[] { chunk });
            Assert.That(Buffer, Is.Null);
        }

        [Test]
        public void Receive_IncompleteEnd_DiscardsPartialTransfer()
        {
            Begin(8);
            transferType.GetMethod("EndReceive", Private).Invoke(transfer, new[] { Activator.CreateInstance(endType) });
            Assert.That(Buffer, Is.Null);
        }
    }
}
