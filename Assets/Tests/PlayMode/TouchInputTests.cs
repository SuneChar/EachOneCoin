using System;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class TouchInputTests
    {
        [Test]
        public void CancelTouchInput_ClearsHeldStateWithoutGeneratingAttackRelease()
        {
            var type = Type.GetType("EachOneCoin.Gameplay.LocalPlayerInput, Assembly-CSharp", true);
            var root = new GameObject("TouchInputTest");
            try
            {
                var input = root.AddComponent(type);
                type.GetProperty("TouchMovement").SetValue(input, Vector2.right);
                type.GetProperty("TouchAim").SetValue(input, Vector2.up);
                type.GetProperty("TouchAttackHeld").SetValue(input, true);
                type.GetProperty("TouchSprintHeld").SetValue(input, true);
                int releases = (int)type.GetProperty("TouchAttackReleaseCount").GetValue(input);
                type.GetMethod("CancelTouchInput").Invoke(input, null);
                Assert.That(type.GetProperty("TouchMovement").GetValue(input), Is.EqualTo(Vector2.zero));
                Assert.That(type.GetProperty("TouchAim").GetValue(input), Is.EqualTo(Vector2.zero));
                Assert.That(type.GetProperty("TouchAttackHeld").GetValue(input), Is.False);
                Assert.That(type.GetProperty("TouchSprintHeld").GetValue(input), Is.False);
                Assert.That(type.GetProperty("TouchAttackReleaseCount").GetValue(input), Is.EqualTo(releases));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void QuickTap_IsRetainedUntilRead_AndCancellationDiscardsPendingPress()
        {
            var type = Type.GetType("EachOneCoin.Gameplay.LocalPlayerInput, Assembly-CSharp", true);
            var root = new GameObject("TouchInputTest");
            try
            {
                var input = root.AddComponent(type);
                type.GetMethod("BeginTouchAttack").Invoke(input, null);
                type.GetMethod("ReleaseTouchAttack").Invoke(input, null);
                Assert.That(type.GetMethod("ConsumeTouchAttackPress").Invoke(input, null), Is.True);
                Assert.That(type.GetProperty("TouchAttackReleaseCount").GetValue(input), Is.Not.EqualTo(type.GetProperty("TouchAttackReleaseAtPress").GetValue(input)));
                Assert.That(type.GetMethod("ConsumeTouchAttackPress").Invoke(input, null), Is.False);
                type.GetMethod("BeginTouchAttack").Invoke(input, null);
                type.GetMethod("CancelTouchAttack").Invoke(input, null);
                Assert.That(type.GetMethod("ConsumeTouchAttackPress").Invoke(input, null), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReleaseTouchAttack_ClearsHeldStateAndRecordsRelease()
        {
            var type = Type.GetType("EachOneCoin.Gameplay.LocalPlayerInput, Assembly-CSharp", true);
            var root = new GameObject("TouchInputTest");
            try
            {
                var input = root.AddComponent(type);
                type.GetProperty("TouchAttackHeld").SetValue(input, true);
                type.GetMethod("ReleaseTouchAttack").Invoke(input, null);
                Assert.That(type.GetProperty("TouchAttackHeld").GetValue(input), Is.False);
                Assert.That(type.GetProperty("TouchAttackReleaseCount").GetValue(input), Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
