using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
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
            dataType.GetField("formatVersion").SetValue(data, (int)dataType.GetField("CurrentFormat").GetRawConstantValue());
            dataType.GetField("mapFingerprint").SetValue(data, new string('a', 64));
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
        public void Validate_LegacyRecordingWithoutMapFingerprint_Rejects()
        {
            object data = ValidData();
            dataType.GetField("formatVersion").SetValue(data, 1);
            dataType.GetField("mapFingerprint").SetValue(data, null);
            object[] args = { data, Application.version, null };
            Assert.That((bool)validate.Invoke(null, args), Is.False);
            Assert.That((string)args[2], Does.Contain("地图"));
        }

        [Test]
        public void Validate_MalformedMapFingerprint_Rejects()
        {
            object data = ValidData();
            dataType.GetField("mapFingerprint").SetValue(data, "invalid");
            Assert.That(Validate(data, Application.version), Is.False);
        }

        [Test]
        public void MatchesMap_RejectsChangedMapBeforePlayback()
        {
            var type = Type.GetType("EachOneCoin.Gameplay.ArenaMapDefinition, Assembly-CSharp", true);
            var map = ScriptableObject.CreateInstance(type);
            try
            {
                var serialized = new SerializedObject(map);
                serialized.FindProperty("mapId").stringValue = "replay-test";
                serialized.FindProperty("defaultTilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/ArenaTile.prefab");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var fingerprint = Type.GetType("EachOneCoin.Gameplay.ArenaMapFingerprint, Assembly-CSharp", true).GetMethod("Compute");
                object data = ValidData();
                dataType.GetField("mapFingerprint").SetValue(data, fingerprint.Invoke(null, new object[] { map }));
                var matches = storageType.GetMethod("MatchesMap");
                object[] args = { data, map, null };
                Assert.That(matches.Invoke(null, args), Is.EqualTo(true), args[2] as string);
                serialized.Update();
                serialized.FindProperty("mapId").stringValue = "changed-map";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                args = new object[] { data, map, null };
                Assert.That(matches.Invoke(null, args), Is.EqualTo(false));
            }
            finally { UnityEngine.Object.DestroyImmediate(map); }
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
