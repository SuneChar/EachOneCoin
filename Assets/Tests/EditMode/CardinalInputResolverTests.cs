using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class CardinalInputResolverTests
    {
        private const float Threshold = 0.25f;

        [Test]
        public void PressingSecondDirection_SwitchesImmediately()
        {
            var resolver = new CardinalInputResolver();
            Assert.That(resolver.Update(false, false, true, false, 0f, 1f, Threshold), Is.EqualTo(Vector3.forward));
            Assert.That(resolver.Update(false, true, true, false, 1f, 1f, Threshold), Is.EqualTo(Vector3.right));
        }

        [Test]
        public void ReleasingLastDirection_FallsBackToStillHeldDirection()
        {
            var resolver = new CardinalInputResolver();
            Assert.That(resolver.Update(false, true, true, false, 1f, 1f, Threshold), Is.EqualTo(Vector3.right));
            Assert.That(resolver.Update(false, false, true, false, 0f, 1f, Threshold), Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void OppositeKeysOnSameAxis_CancelEachOther()
        {
            var resolver = new CardinalInputResolver();
            Assert.That(resolver.Update(true, false, false, false, -1f, 0f, Threshold), Is.EqualTo(Vector3.left));
            Assert.That(resolver.Update(true, true, false, false, 0f, 0f, Threshold), Is.EqualTo(Vector3.zero));
            Assert.That(resolver.Update(true, false, false, false, -1f, 0f, Threshold), Is.EqualTo(Vector3.left));
        }

        [Test]
        public void HoldingOppositeKeysWithAnotherAxis_KeepsRemainingAxis()
        {
            var resolver = new CardinalInputResolver();
            Assert.That(resolver.Update(false, false, true, false, 0f, 1f, Threshold), Is.EqualTo(Vector3.forward));
            Assert.That(resolver.Update(true, true, true, false, 0f, 1f, Threshold), Is.EqualTo(Vector3.forward));
            Assert.That(resolver.Update(true, true, false, false, 0f, 0f, Threshold), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Gamepad_LargerDeflectionWins_AndNewerAxisBreaksTies()
        {
            var resolver = new CardinalInputResolver();
            Assert.That(resolver.Update(false, false, false, false, 0.9f, 0.2f, Threshold), Is.EqualTo(Vector3.right));
            Assert.That(resolver.Update(false, false, false, false, 0.5f, 0.9f, Threshold), Is.EqualTo(Vector3.forward));
            Assert.That(resolver.Update(false, false, false, false, 0.9f, 0.85f, Threshold), Is.EqualTo(Vector3.forward));
            Assert.That(resolver.Update(false, false, false, false, 0.9f, 0.3f, Threshold), Is.EqualTo(Vector3.right));
        }

        [Test]
        public void KeyboardOverridesStick_AndNoInputReturnsZero()
        {
            var resolver = new CardinalInputResolver();
            Assert.That(resolver.Update(false, false, false, false, 0f, 0f, Threshold), Is.EqualTo(Vector3.zero));
            Assert.That(resolver.Update(false, false, false, true, 0.9f, 0f, Threshold), Is.EqualTo(Vector3.back));
            Assert.That(resolver.Update(false, false, false, false, 0f, 0f, Threshold), Is.EqualTo(Vector3.zero));
        }
    }
}
