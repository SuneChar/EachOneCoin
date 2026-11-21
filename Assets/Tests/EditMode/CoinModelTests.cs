using System.Collections.Generic;
using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class CoinModelTests
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

        [Test]
        public void Coins_StartAtZero()
        {
            Assert.That(GameArchitecture.Interface.GetModel<IGameModel>().Coins.Value, Is.Zero);
        }

        [Test]
        public void CollectCommand_IncrementsAndReturnsCoinCount()
        {
            var architecture = GameArchitecture.Interface;

            Assert.That(architecture.SendCommand(new CollectCoinCommand()), Is.EqualTo(1));
            Assert.That(architecture.SendCommand(new CollectCoinCommand()), Is.EqualTo(2));
            Assert.That(architecture.GetModel<IGameModel>().Coins.Value, Is.EqualTo(2));
        }

        [Test]
        public void ResetCoins_NotifiesSubscribersAndClearsCount()
        {
            var architecture = GameArchitecture.Interface;
            var model = architecture.GetModel<IGameModel>();
            var observed = new List<int>();
            var subscription = model.Coins.RegisterWithInitValue(observed.Add);
            try
            {
                architecture.SendCommand(new CollectCoinCommand());
                model.ResetCoins();
                Assert.That(model.Coins.Value, Is.Zero);
                Assert.That(observed, Is.EqualTo(new[] { 0, 1, 0 }));
            }
            finally
            {
                subscription.UnRegister();
            }
        }

        [Test]
        public void ReinitializedArchitecture_DoesNotRetainCoins()
        {
            GameArchitecture.Interface.SendCommand(new CollectCoinCommand());
            GameArchitecture.Interface.Deinit();

            Assert.That(GameArchitecture.Interface.GetModel<IGameModel>().Coins.Value, Is.Zero);
        }
    }
}
