using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace EachOneCoin.Tests
{
    public class TestArchitectureIsolationTests
    {
        private ArenaGameplayIntegrationTests pendingArenaFixture;

        [UnityTearDown]
        public IEnumerator CleanupPendingArenaFixture()
        {
            if (pendingArenaFixture == null) yield break;
            yield return pendingArenaFixture.TearDown();
            pendingArenaFixture = null;
        }
        [Test]
        public void IndependentArchitecture_DoesNotReplaceOrClearGameSession()
        {
            var gameSession = GameArchitecture.Interface.GetModel<MatchSessionModel>();
            if (gameSession.Match != null) Assert.Ignore("An active game session must not be replaced by the isolation test.");
            MatchModel sentinel = null;
            try
            {
                sentinel = gameSession.CreateMatch(2, 2, new[] { new GridPosition(0, 0) }, 3, 100f);
                var testSession = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>();
                testSession.CreateMatch(2, 2, new[] { new GridPosition(0, 0) }, 3, 100f);
                var rules = TestMatchArchitecture.Interface.GetSystem<MatchSystem>();
                rules.StartMatch();
                Assert.That(rules.TryCollectCoin(0, new GridPosition(0, 0)), Is.True);
                TestMatchArchitecture.Interface.Deinit();
                Assert.That(gameSession.Match, Is.SameAs(sentinel));
                Assert.That(sentinel.Status, Is.EqualTo(MatchStatus.Waiting));
                Assert.That(sentinel.Participants[0].Coins, Is.Zero);
            }
            finally
            {
                TestMatchArchitecture.Interface.Deinit();
                gameSession.ClearMatch(sentinel);
            }
        }

        [Test]
        public void ArenaSetup_WithOccupiedGameSessionFailsBeforeChangingState()
        {
            var session = GameArchitecture.Interface.GetModel<MatchSessionModel>();
            if (session.Match != null) Assert.Ignore("An active game session must not be replaced by the isolation test.");
            MatchModel sentinel = null;
            float originalTimeScale = UnityEngine.Time.timeScale;
            try
            {
                sentinel = session.CreateMatch(2, 2, new[] { new GridPosition(0, 0) }, 3, 100f);
                var fixture = new ArenaGameplayIntegrationTests();
                var setup = fixture.SetUp();
                Assert.Throws<InvalidOperationException>(() => setup.MoveNext());
                var teardown = fixture.TearDown();
                Assert.That(teardown.MoveNext(), Is.False);
                Assert.That(session.Match, Is.SameAs(sentinel));
                Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(originalTimeScale));
            }
            finally { session.ClearMatch(sentinel); }
        }

        [UnityTest]
        public IEnumerator ArenaSetup_ReusedFixtureWithOccupiedSessionDoesNotRestoreOldState()
        {
            var session = GameArchitecture.Interface.GetModel<MatchSessionModel>();
            if (session.Match != null) Assert.Ignore("An active game session must not be replaced by the isolation test.");
            var fixture = new ArenaGameplayIntegrationTests();
            pendingArenaFixture = fixture;
            float originalTimeScale = UnityEngine.Time.timeScale;
            MatchModel sentinel = null;
            try
            {
                yield return fixture.SetUp();
                yield return CleanupPendingArenaFixture();
                sentinel = session.CreateMatch(2, 2, new[] { new GridPosition(0, 0) }, 3, 100f);
                float currentTimeScale = originalTimeScale == 0.375f ? 0.625f : 0.375f;
                UnityEngine.Time.timeScale = currentTimeScale;
                var setup = fixture.SetUp();
                Assert.Throws<InvalidOperationException>(() => setup.MoveNext());
                var teardown = fixture.TearDown();
                Assert.That(teardown.MoveNext(), Is.False);
                Assert.That(session.Match, Is.SameAs(sentinel));
                Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(currentTimeScale));
            }
            finally
            {
                session.ClearMatch(sentinel);
                UnityEngine.Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator CleanupPendingArenaFixture_AfterSetupUnloadsSceneAndClearsSession()
        {
            var session = GameArchitecture.Interface.GetModel<MatchSessionModel>();
            if (session.Match != null) Assert.Ignore("An active game session must not be replaced by the isolation test.");
            var fixture = new ArenaGameplayIntegrationTests();
            pendingArenaFixture = fixture;
            yield return fixture.SetUp();
            Assert.That(session.Match, Is.Not.Null);
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/ArenaScene.unity").isLoaded, Is.True);
            yield return CleanupPendingArenaFixture();
            Assert.That(session.Match, Is.Null);
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/ArenaScene.unity").isLoaded, Is.False);
            Assert.That(pendingArenaFixture, Is.Null);
        }

        [Test]
        public void SessionGuard_RejectsOccupiedSessionWithoutChangingIt()
        {
            var session = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>();
            try
            {
                var match = session.CreateMatch(2, 2, new[] { new GridPosition(0, 0) }, 3, 100f);
                var stamina = session.Stamina;
                Assert.Throws<InvalidOperationException>(() => TestSessionGuard.EnsureAvailable(session));
                Assert.That(session.Match, Is.SameAs(match));
                Assert.That(session.Stamina, Is.SameAs(stamina));
                Assert.That(match.Status, Is.EqualTo(MatchStatus.Waiting));
            }
            finally { TestMatchArchitecture.Interface.Deinit(); }
        }
    }
}
