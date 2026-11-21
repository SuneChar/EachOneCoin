using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class PlanarVelocityTests
    {
        [Test]
        public void ReversingDirection_DoesNotInstantlyReverse()
        {
            var next = PlanarVelocity.Advance(Vector3.right * 5f, Vector3.left * 5f, 35f, 35f, 0.02f);
            Assert.That(next.x, Is.EqualTo(4.3f).Within(0.0001f));
        }

        [Test]
        public void StartingAndTurning_AreBoundedByAcceleration()
        {
            var start = PlanarVelocity.Advance(Vector3.zero, Vector3.right * 5f, 10f, 30f, 0.1f);
            var turn = PlanarVelocity.Advance(Vector3.right * 5f, Vector3.forward * 5f, 10f, 30f, 0.1f);
            Assert.That(start.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That((turn - Vector3.right * 5f).magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(turn.magnitude, Is.LessThanOrEqualTo(5f));
        }

        [Test]
        public void Stopping_UsesDecelerationWithoutOvershoot()
        {
            var next = PlanarVelocity.Advance(Vector3.right * 5f, Vector3.zero, 10f, 20f, 0.1f);
            Assert.That(next.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(PlanarVelocity.Advance(next, Vector3.zero, 10f, 20f, 10f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void PauseDelta_DoesNotAdvance_AndVerticalVelocityIsIgnored()
        {
            var next = PlanarVelocity.Advance(new Vector3(2f, 9f, 1f), Vector3.forward * 5f, 35f, 35f, 0f);
            Assert.That(next, Is.EqualTo(new Vector3(2f, 0f, 1f)));
        }
    }
}
