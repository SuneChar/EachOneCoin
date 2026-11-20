using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class StaminaModelTests
    {
        [Test]
        public void MovingWithSprint_DrainsStaminaAndEnablesSprint()
        {
            var stamina = new StaminaModel(100f);
            stamina.Tick(1f, true, true, true, 25f, 20f);
            Assert.That(stamina.Current, Is.EqualTo(75f));
            Assert.That(stamina.IsSprinting, Is.True);
        }

        [Test]
        public void NotMoving_DoesNotDrainAndRecoversToMaximum()
        {
            var stamina = new StaminaModel(100f);
            stamina.Tick(2f, true, true, true, 25f, 20f);
            stamina.Tick(10f, true, false, true, 25f, 20f);
            Assert.That(stamina.Current, Is.EqualTo(100f));
            Assert.That(stamina.IsSprinting, Is.False);
        }

        [Test]
        public void Exhaustion_RequiresReleaseEvenAfterRecovery()
        {
            var stamina = new StaminaModel(100f);
            stamina.Tick(4f, true, true, true, 25f, 20f);
            Assert.That(stamina.Current, Is.Zero);
            Assert.That(stamina.IsSprinting, Is.False);
            stamina.Tick(1f, true, true, true, 25f, 20f);
            Assert.That(stamina.Current, Is.EqualTo(20f));
            Assert.That(stamina.IsSprinting, Is.False);
            stamina.Tick(0f, true, true, false, 25f, 20f);
            stamina.Tick(0.1f, true, true, true, 25f, 20f);
            Assert.That(stamina.IsSprinting, Is.True);
            Assert.That(stamina.Current, Is.EqualTo(17.5f));
        }

        [Test]
        public void Inactive_FreezesStaminaAndDisablesSprint()
        {
            var stamina = new StaminaModel(100f);
            stamina.Tick(1f, true, true, true, 25f, 20f);
            stamina.Tick(10f, false, true, true, 25f, 20f);
            Assert.That(stamina.Current, Is.EqualTo(75f));
            Assert.That(stamina.IsSprinting, Is.False);
        }
    }
}
