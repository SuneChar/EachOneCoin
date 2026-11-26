using System;
using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class ReconnectRoomStateTests
    {
        private Type type;
        private object room;
        private object Call(string name, params object[] args) => type.GetMethod(name).Invoke(room, args);

        [SetUp]
        public void SetUp()
        {
            type = Type.GetType("EachOneCoin.Networking.RoomState, Assembly-CSharp", true);
            room = Activator.CreateInstance(type);
            Assert.That(Call("Add", 0), Is.EqualTo(0));
            Assert.That(Call("Add", 11), Is.EqualTo(1));
        }

        [Test]
        public void ConnectedToken_IsRecognizedButCannotReclaimUntilDisconnected()
        {
            var token = (string)Call("TokenAt", 1);
            Assert.That(Call("IsTokenConnected", token), Is.EqualTo(true));
            Assert.That(Call("Reclaim", 22, token, 1d), Is.EqualTo(-1));
            Call("Reserve", 11, 2d);
            Assert.That(Call("IsTokenConnected", token), Is.EqualTo(false));
            Assert.That(Call("Reclaim", 22, token, 2.1d), Is.EqualTo(1));
        }

        [Test]
        public void ReservedSeat_RejectsWrongTokenAndOtherNewPlayerCannotTakeIt()
        {
            var token = (string)Call("TokenAt", 1);
            Assert.That(Call("Reserve", 11, 10d), Is.EqualTo(true));
            Assert.That(Call("Add", 22), Is.EqualTo(2));
            Assert.That(Call("Reclaim", 33, "wrong", 11d), Is.EqualTo(-1));
            Assert.That(Call("Reclaim", 33, token, 24.99d), Is.EqualTo(1));
            Assert.That(Call("Reclaim", 44, token, 24.99d), Is.EqualTo(-1));
        }

        [Test]
        public void FailedReclaimAttempt_DoesNotExtendOriginalDeadline()
        {
            var token = (string)Call("TokenAt", 1);
            Call("Reserve", 11, 10d);
            Assert.That(Call("Reclaim", 33, token, 20d), Is.EqualTo(1));
            Call("Reserve", 33, 21d);
            Assert.That(Call("IsExpired", 1, 25d), Is.EqualTo(true));
            Assert.That(Call("Reclaim", 44, token, 25d), Is.EqualTo(-1));
        }

        [Test]
        public void CommittedReclaim_UsesNewDeadlineForLaterDisconnect()
        {
            var token = (string)Call("TokenAt", 1);
            Call("Reserve", 11, 10d);
            Assert.That(Call("Reclaim", 33, token, 20d), Is.EqualTo(1));
            Call("CommitReclaim", 1);
            Call("Reserve", 33, 30d);
            Assert.That(Call("IsExpired", 1, 44.99d), Is.EqualTo(false));
            Assert.That(Call("IsExpired", 1, 45d), Is.EqualTo(true));
        }

        [Test]
        public void Release_InvalidatesTokenAndFreesSeat()
        {
            var token = (string)Call("TokenAt", 1);
            Call("Reserve", 11, 10d);
            Call("Release", 1);
            Assert.That(Call("Reclaim", 33, token, 11d), Is.EqualTo(-1));
            Assert.That(Call("Add", 44), Is.EqualTo(1));
            Assert.That(Call("TokenAt", 1), Is.Not.EqualTo(token));
        }
    }
}
