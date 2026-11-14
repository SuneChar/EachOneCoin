using System.Collections.Generic;
using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class HealthModelTests
    {
        [SetUp]
        public void SetUp()
        {
            GameArchitecture.Interface.Deinit();
        }

        [TearDown]
        public void TearDown()
        {
            GameArchitecture.Interface.Deinit();
        }

        [TestCase(100, 100)]
        [TestCase(0, 1)]
        [TestCase(-10, 1)]
        public void ResetHealth_InitializesAtLeastOne(int maximum, int expected)
        {
            var model = GameArchitecture.Interface.GetModel<IGameModel>();
            model.ResetHealth(maximum);
            Assert.That(model.Health.Value, Is.EqualTo(expected));
        }

        [Test]
        public void TakeDamage_ClampsAtZeroAndIgnoresNonpositiveDamage()
        {
            var model = GameArchitecture.Interface.GetModel<IGameModel>();
            model.ResetHealth(100);
            Assert.That(model.TakeDamage(0), Is.EqualTo(100));
            Assert.That(model.TakeDamage(-5), Is.EqualTo(100));
            Assert.That(model.TakeDamage(25), Is.EqualTo(75));
            Assert.That(model.TakeDamage(int.MaxValue), Is.Zero);
            Assert.That(model.TakeDamage(1), Is.Zero);
        }

        [Test]
        public void ResetHealth_RestoresHealthAndNotifiesSubscribers()
        {
            var model = GameArchitecture.Interface.GetModel<IGameModel>();
            model.ResetHealth(100);
            var observed = new List<int>();
            var subscription = model.Health.RegisterWithInitValue(observed.Add);
            try
            {
                model.TakeDamage(40);
                model.ResetHealth(100);
                Assert.That(observed, Is.EqualTo(new[] { 100, 60, 100 }));
            }
            finally
            {
                subscription.UnRegister();
            }
        }
    }
}
