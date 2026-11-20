using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class MovementTelemetryTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject root;
        private Component recorder;
        private Type recorderType;
        private string path;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MovementTelemetryTests");
            Type matchType = Type.GetType("EachOneCoin.Gameplay.MatchCoordinator, Assembly-CSharp", true);
            Type characterType = Type.GetType("EachOneCoin.Gameplay.ArenaCharacter, Assembly-CSharp", true);
            Type inputType = Type.GetType("EachOneCoin.Gameplay.LocalPlayerInput, Assembly-CSharp", true);
            recorderType = Type.GetType("EachOneCoin.Gameplay.MovementTelemetry, Assembly-CSharp", true);
            var match = root.AddComponent(matchType);
            ((Behaviour)match).enabled = false;
            var model = new MatchModel(3, 3, new[] { new GridPosition(0, 0), new GridPosition(2, 2) }, 3);
            model.Start();
            matchType.GetProperty("Model").SetValue(match, model);
            var characters = Array.CreateInstance(characterType, 2);
            for (int i = 0; i < characters.Length; i++)
            {
                var go = new GameObject("Character " + i);
                go.transform.SetParent(root.transform);
                characters.SetValue(go.AddComponent(characterType), i);
            }
            matchType.GetProperty("Characters").SetValue(match, characters);
            var input = root.AddComponent(inputType);
            recorder = root.AddComponent(recorderType);
            ((Behaviour)recorder).enabled = false;
            recorderType.GetMethod("Initialize").Invoke(recorder, new[] { match, input });
            string id = (string)recorderType.GetField("matchId", Private).GetValue(recorder);
            path = Path.Combine(Application.persistentDataPath, "MovementTelemetry", id + ".jsonl");
        }

        [TearDown]
        public void TearDown()
        {
            if (recorder != null) recorderType.GetMethod("Close", Private).Invoke(recorder, new object[] { "interrupted" });
            UnityEngine.Object.DestroyImmediate(root);
            if (File.Exists(path)) File.Delete(path);
        }

        [Test]
        public void Close_WritesStartNpcEventAndInterruptedEnd()
        {
            recorderType.GetMethod("NpcEvent", Private).Invoke(recorder, new object[] { 1, "path_failed" });
            recorderType.GetMethod("Close", Private).Invoke(recorder, new object[] { "interrupted" });
            string[] lines = File.ReadAllLines(path);
            Assert.That(lines.Length, Is.EqualTo(3));
            Assert.That(lines[0], Does.Contain("\"type\":\"match_start\""));
            Assert.That(lines[1], Does.Contain("\"reason\":\"path_failed\""));
            Assert.That(lines[2], Does.Contain("\"reason\":\"interrupted\""));
        }

        [Test]
        public void PlanarDistance_IgnoresHeight()
        {
            var distance = (float)recorderType.GetMethod("PlanarDistance").Invoke(null, new object[] { Vector3.zero, new Vector3(3f, 50f, 4f) });
            Assert.That(distance, Is.EqualTo(5f).Within(0.0001f));
        }
    }
}
