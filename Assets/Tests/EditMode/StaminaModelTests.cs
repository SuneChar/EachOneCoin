using System;
using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class StaminaModelTests
    {
        private MatchSystem rules;
        private StaminaModel stamina;

        [SetUp]
        public void SetUp()
        {
            TestMatchArchitecture.Interface.Deinit();
            var session = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>();
            session.CreateMatch(2, 2, new[] { new GridPosition(0, 0) }, 3, 100f);
            stamina = session.Stamina;
            rules = TestMatchArchitecture.Interface.GetSystem<MatchSystem>();
        }

        [TearDown]
        public void TearDown() => TestMatchArchitecture.Interface.Deinit();

        [Test]
        public void MovingWithSprint_DrainsStaminaAndEnablesSprint()
        {
            rules.TickStamina(1f, true, true, true, 25f, 20f);
            Assert.That(stamina.Current, Is.EqualTo(75f));
            Assert.That(stamina.IsSprinting, Is.True);
        }

        [Test]
        public void NotMoving_DoesNotDrainAndRecoversToMaximum()
        {
            rules.TickStamina(2f, true, true, true, 25f, 20f);
            rules.TickStamina(10f, true, false, true, 25f, 20f);
            Assert.That(stamina.Current, Is.EqualTo(100f));
            Assert.That(stamina.IsSprinting, Is.False);
        }

        [Test]
        public void Exhaustion_RequiresReleaseEvenAfterRecovery()
        {
            rules.TickStamina(4f, true, true, true, 25f, 20f);
            Assert.That(stamina.Current, Is.Zero);
            Assert.That(stamina.IsSprinting, Is.False);
            rules.TickStamina(1f, true, true, true, 25f, 20f);
            Assert.That(stamina.Current, Is.EqualTo(20f));
            Assert.That(stamina.IsSprinting, Is.False);
            rules.TickStamina(0f, true, true, false, 25f, 20f);
            rules.TickStamina(0.1f, true, true, true, 25f, 20f);
            Assert.That(stamina.IsSprinting, Is.True);
            Assert.That(stamina.Current, Is.EqualTo(17.5f));
        }

        [Test]
        public void TickStamina_RejectsInvalidInputsAndClearedSession()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.TickStamina(float.NaN, true, true, true, 25f, 20f));
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.TickStamina(1f, true, true, true, -1f, 20f));
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.TickStamina(1f, true, true, true, 25f, float.PositiveInfinity));
            var session = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>();
            session.ClearMatch(session.Match);
            Assert.Throws<InvalidOperationException>(() => rules.TickStamina(1f, true, true, true, 25f, 20f));
        }

        [Test]
        public void Inactive_FreezesStaminaAndDisablesSprint()
        {
            rules.TickStamina(1f, true, true, true, 25f, 20f);
            rules.TickStamina(10f, false, true, true, 25f, 20f);
            Assert.That(stamina.Current, Is.EqualTo(75f));
            Assert.That(stamina.IsSprinting, Is.False);
        }
    }
}
