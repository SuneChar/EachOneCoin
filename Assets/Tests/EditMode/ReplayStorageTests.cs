using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class ReplayStorageTests
    {
        private Type dataType, frameType, participantType, coinType, spikeType, eventType, storageType;
        private MethodInfo validate;

        [SetUp]
        public void SetUp()
        {
            dataType = Type.GetType("EachOneCoin.Gameplay.Replay.ReplayData, Assembly-CSharp", true);
            frameType = Type.GetType("EachOneCoin.Gameplay.Replay.ReplayFrame, Assembly-CSharp", true);
            participantType = Type.GetType("EachOneCoin.Gameplay.Replay.ReplayParticipant, Assembly-CSharp", true);
            coinType = Type.GetType("EachOneCoin.Gameplay.Replay.ReplayCoin, Assembly-CSharp", true);
            spikeType = Type.GetType("EachOneCoin.Gameplay.Replay.ReplaySpike, Assembly-CSharp", true);
            eventType = Type.GetType("EachOneCoin.Gameplay.Replay.ReplayEvent, Assembly-CSharp", true);
            storageType = Type.GetType("EachOneCoin.Gameplay.Replay.ReplayStorage, Assembly-CSharp", true);
            validate = storageType.GetMethod("Validate");
        }

        private object ValidData()
        {
            object data = Activator.CreateInstance(dataType);
            dataType.GetField("formatVersion").SetValue(data, 1);
            dataType.GetField("gameVersion").SetValue(data, Application.version);
            dataType.GetField("recordedUtc").SetValue(data, DateTime.UtcNow.ToString("o"));
            dataType.GetField("duration").SetValue(data, 60f);
            dataType.GetField("coinsToWin").SetValue(data, 10);
            dataType.GetField("matchId").SetValue(data, Guid.NewGuid().ToString("N"));
            dataType.GetField("complete").SetValue(data, true);
            object frame = Activator.CreateInstance(frameType);
            frameType.GetField("matchStatus").SetValue(frame, 2);
            frameType.GetField("warningMine").SetValue(frame, -1);
            frameType.GetField("warningX").SetValue(frame, new ushort[0]);
            frameType.GetField("warningY").SetValue(frame, new ushort[0]);
            frameType.GetField("participants").SetValue(frame, Array.CreateInstance(participantType, 4));
            frameType.GetField("gridOwners").SetValue(frame, new byte[441]);
            frameType.GetField("coins").SetValue(frame, Array.CreateInstance(coinType, 0));
            frameType.GetField("patrolPositions").SetValue(frame, new Vector3[0]);
            frameType.GetField("spikes").SetValue(frame, Array.CreateInstance(spikeType, 0));
            Array frames = Array.CreateInstance(frameType, 1);
            frames.SetValue(frame, 0);
            dataType.GetField("frames").SetValue(data, frames);
            dataType.GetField("events").SetValue(data, Array.CreateInstance(eventType, 0));
            return data;
        }

        private bool Validate(object data, string version)
        {
            object[] args = { data, version, null };
            return (bool)validate.Invoke(null, args);
        }

        [Test]
        public void Validate_CompleteCompatibleRecording_Accepts()
        {
            Assert.That(Validate(ValidData(), Application.version), Is.True);
        }

        [Test]
        public void Validate_IncompleteOrIncompatibleRecording_Rejects()
        {
            object data = ValidData();
            Assert.That(Validate(data, "different-version"), Is.False);
            dataType.GetField("complete").SetValue(data, false);
            Assert.That(Validate(data, Application.version), Is.False);
        }

        [Test]
        public void Validate_JsonRoundTrip_PreservesCompleteRecording()
        {
            object original = ValidData();
            string json = JsonUtility.ToJson(original);
            object copy = JsonUtility.FromJson(json, dataType);
            Assert.That(Validate(copy, Application.version), Is.True);
            Assert.That(dataType.GetField("matchId").GetValue(copy), Is.EqualTo(dataType.GetField("matchId").GetValue(original)));
        }

        [Test]
        public void Validate_NonFiniteFrameTime_Rejects()
        {
            object data = ValidData();
            Array frames = (Array)dataType.GetField("frames").GetValue(data);
            frameType.GetField("time").SetValue(frames.GetValue(0), float.NaN);
            Assert.That(Validate(data, Application.version), Is.False);
        }

        [Test]
        public void Validate_MissingFrameState_Rejects()
        {
            object data = ValidData();
            Array frames = (Array)dataType.GetField("frames").GetValue(data);
            frameType.GetField("gridOwners").SetValue(frames.GetValue(0), new byte[1]);
            Assert.That(Validate(data, Application.version), Is.False);
        }
    }
}
