using System.Collections.Generic;
using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class GameStateModelTests
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
        public void StartPauseResumeAndWin_TransitionsAndReset()
        {
            var model = GameArchitecture.Interface.GetModel<IGameModel>();
            var observed = new List<GameSessionState>();
            var subscription = model.State.RegisterWithInitValue(observed.Add);
            try
            {
                Assert.That(model.State.Value, Is.EqualTo(GameSessionState.Waiting));
                Assert.That(model.TryChangeState(GameSessionState.Playing), Is.True);
                Assert.That(model.TryChangeState(GameSessionState.Paused), Is.True);
                Assert.That(model.TryChangeState(GameSessionState.Playing), Is.True);
                Assert.That(model.TryChangeState(GameSessionState.Won), Is.True);
                Assert.That(model.TryChangeState(GameSessionState.Playing), Is.False);
                model.ResetState();
                Assert.That(observed, Is.EqualTo(new[] { GameSessionState.Waiting, GameSessionState.Playing,
                    GameSessionState.Paused, GameSessionState.Playing, GameSessionState.Won, GameSessionState.Waiting }));
            }
            finally
            {
                subscription.UnRegister();
            }
        }

        [Test]
        public void Lose_RejectsFurtherTransitionsUntilReset()
        {
            var model = GameArchitecture.Interface.GetModel<IGameModel>();
            Assert.That(model.TryChangeState(GameSessionState.Playing), Is.True);
            Assert.That(model.TryChangeState(GameSessionState.Lost), Is.True);
            Assert.That(model.TryChangeState(GameSessionState.Won), Is.False);
            Assert.That(model.TryChangeState(GameSessionState.Playing), Is.False);
            Assert.That(model.State.Value, Is.EqualTo(GameSessionState.Lost));
            model.ResetState();
            Assert.That(model.State.Value, Is.EqualTo(GameSessionState.Waiting));
        }

        [Test]
        public void InvalidTransitions_DoNotChangeOrNotify()
        {
            var model = GameArchitecture.Interface.GetModel<IGameModel>();
            int notifications = 0;
            var subscription = model.State.Register(_ => notifications++);
            try
            {
                Assert.That(model.TryChangeState(GameSessionState.Paused), Is.False);
                Assert.That(model.TryChangeState(GameSessionState.Won), Is.False);
                Assert.That(model.TryChangeState(GameSessionState.Waiting), Is.False);
                Assert.That(model.TryChangeState((GameSessionState)999), Is.False);
                Assert.That(model.State.Value, Is.EqualTo(GameSessionState.Waiting));
                Assert.That(notifications, Is.Zero);
                Assert.That(model.TryChangeState(GameSessionState.Playing), Is.True);
                Assert.That(model.TryChangeState(GameSessionState.Paused), Is.True);
                Assert.That(model.TryChangeState(GameSessionState.Lost), Is.False);
                Assert.That(model.State.Value, Is.EqualTo(GameSessionState.Paused));
                Assert.That(notifications, Is.EqualTo(2));
            }
            finally
            {
                subscription.UnRegister();
            }
        }
    }
}
