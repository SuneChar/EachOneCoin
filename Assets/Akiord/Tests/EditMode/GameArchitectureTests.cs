using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class GameArchitectureTests
    {
        [TearDown]
        public void TearDown()
        {
            GameArchitecture.Interface.Deinit();
        }

        [Test]
        public void Interface_RegistersAndInitializesGameModel()
        {
            var model = GameArchitecture.Interface.GetModel<IGameModel>();

            Assert.That(model, Is.Not.Null);
            Assert.That(model.Initialized, Is.True);
        }

        [Test]
        public void Interface_ReturnsSameArchitectureUntilDeinitialized()
        {
            var first = GameArchitecture.Interface;

            Assert.That(GameArchitecture.Interface, Is.SameAs(first));
            first.Deinit();
            Assert.That(GameArchitecture.Interface, Is.Not.SameAs(first));
        }
    }
}
