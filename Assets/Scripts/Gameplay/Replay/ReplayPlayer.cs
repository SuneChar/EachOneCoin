using System;
using EachOneCoin.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EachOneCoin.Gameplay.Replay
{
    [DefaultExecutionOrder(-50)]
    public sealed class ReplayPlayer : MonoBehaviour
    {
        private MatchCoordinator match;
        private ReplayData data;
        private LinearPatrolObstacle[] patrols;
        private TimedSpikeTrap[] spikes;
        private readonly ReplayParticipantState[] participantBuffer = new ReplayParticipantState[4];
        private ReplayCoin[] lastCoins;
        private int frameIndex, eventIndex;
        private float elapsed;
        private bool paused, failed;
        private float previousTimeScale;
        private ReplayControlsView controls;

        public void Initialize(MatchCoordinator coordinator, ReplayData recording)
        {
            match = coordinator; data = recording;
            previousTimeScale = Time.timeScale;
            var patrolList = new System.Collections.Generic.List<LinearPatrolObstacle>();
            var spikeList = new System.Collections.Generic.List<TimedSpikeTrap>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                patrolList.AddRange(root.GetComponentsInChildren<LinearPatrolObstacle>(true));
                spikeList.AddRange(root.GetComponentsInChildren<TimedSpikeTrap>(true));
                if (controls == null) controls = root.GetComponentInChildren<ReplayControlsView>(true);
            }
            patrols = patrolList.ToArray(); spikes = spikeList.ToArray();
            if (controls != null) { controls.Bind(this); controls.Refresh(elapsed, paused, failed); }
            else Debug.LogWarning("录像播放控件未配置。");
            ApplyFrame(data.frames[0]);
            ApplyEvents();
        }

        private void Update()
        {
            if (failed || data == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (!paused) elapsed = Mathf.Min(data.frames[data.frames.Length - 1].time, elapsed + Time.unscaledDeltaTime);
            while (frameIndex + 1 < data.frames.Length && data.frames[frameIndex + 1].time <= elapsed)
            {
                frameIndex++;
                if (!ApplyFrame(data.frames[frameIndex])) return;
            }
            ApplyEvents();
            var a = data.frames[frameIndex];
            var b = data.frames[Math.Min(frameIndex + 1, data.frames.Length - 1)];
            float t = b.time > a.time ? Mathf.Clamp01((elapsed - a.time) / (b.time - a.time)) : 0f;
            for (int i = 0; i < 4; i++)
            {
                // A status change may be a teleport; never interpolate across it.
                bool continuous = a.participants[i].status == b.participants[i].status && a.participants[i].visible == b.participants[i].visible;
                Vector3 position = continuous ? Vector3.Lerp(a.participants[i].position, b.participants[i].position, t) : a.participants[i].position;
                match.Characters[i].ApplyReplayPosition(position, a.participants[i].facing);
            }
            if (controls != null) controls.Refresh(elapsed, paused, failed);
            match.Coins.Tick(paused ? 0f : Time.unscaledDeltaTime);
        }

        private bool ApplyFrame(ReplayFrame frame)
        {
            if (frame.participants == null || frame.participants.Length != 4 || frame.patrolPositions == null || frame.patrolPositions.Length != patrols.Length || frame.spikes == null || frame.spikes.Length != spikes.Length)
                return Fail();
            for (int i = 0; i < 4; i++)
            {
                var p = frame.participants[i];
                participantBuffer[i] = new ReplayParticipantState { participating = p.participating, health = p.health, maxHealth = p.maxHealth,
                    coins = p.coins, finishOrder = p.finishOrder, status = p.status, stamina = p.stamina };
            }
            if (!match.Rules.ApplyReplayState(frame.remaining, frame.matchStatus, participantBuffer)) return Fail();
            uint revision = match.Model.Grid.Revision;
            if (!match.Model.Grid.ApplyReplicaSnapshot(frame.gridOwners)) return Fail();
            if (revision != match.Model.Grid.Revision) match.Grid.Refresh(match.Model.Grid);
            for (int i = 0; i < 4; i++)
            {
                var p = frame.participants[i];
                match.SetReplayParticipantName(i, p.displayName);
                match.Characters[i].SetActiveState(p.visible);
                match.Characters[i].SetAttackAim(p.visible && p.aiming, p.aimDirection, match.Combat.AttackRange, match.Combat.AttackAngle);
                match.Characters[i].ApplyReplayPosition(p.position, p.facing);
            }
            if (!CoinsEqual(lastCoins, frame.coins))
            {
                var entries = new CoinEntry[frame.coins.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    var coin = frame.coins[i];
                    if (coin.x < 0 || coin.x >= ArenaGridView.Size || coin.y < 0 || coin.y >= ArenaGridView.Size) return Fail();
                    entries[i] = new CoinEntry { id = coin.id, kind = coin.kind, x = (ushort)coin.x, y = (ushort)coin.y };
                }
                if (!match.Coins.ApplyReplicaSnapshot(entries)) return Fail();
                lastCoins = frame.coins;
            }
            var schedule = new MineStatusMessage { elapsed = frame.mineElapsed, nextEvent = frame.nextMineEvent, warningMine = frame.warningMine,
                warningX = frame.warningX, warningY = frame.warningY };
            if (!match.Coins.ApplyReplicaSchedule(schedule)) return Fail();
            for (int i = 0; i < patrols.Length; i++) patrols[i].ApplyNetworkPosition(frame.patrolPositions[i]);
            for (int i = 0; i < spikes.Length; i++) spikes[i].ApplyNetworkState(frame.spikes[i].active, frame.spikes[i].remainingFraction, frame.spikes[i].height);
            return true;
        }

        private static bool CoinsEqual(ReplayCoin[] a, ReplayCoin[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i].id != b[i].id || a[i].kind != b[i].kind || a[i].x != b[i].x || a[i].y != b[i].y) return false;
            return true;
        }

        private void ApplyEvents()
        {
            while (eventIndex < data.events.Length && data.events[eventIndex].time <= elapsed)
            {
                var evt = data.events[eventIndex++];
                if (evt.type == "attack" && evt.actor >= 0 && evt.actor < 4)
                    match.Characters[evt.actor].PlayAttack(evt.direction, match.Combat.AttackRange, match.Combat.AttackAngle);
                else if (evt.type == "feed" && evt.detail >= 0 && evt.detail <= (int)MatchFeedEventType.PlayerRespawned)
                    match.RaiseNetworkFeedEvent(new MatchFeedEvent { Type = (MatchFeedEventType)evt.detail, PrimarySlot = evt.actor,
                        SecondarySlot = evt.target, Value = evt.value, MineIndex = (int)evt.direction.x, Reason = (EliminationReason)(int)evt.direction.y });
            }
        }

        public void TogglePause()
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : previousTimeScale;
            if (controls != null) controls.Refresh(elapsed, paused, failed);
        }

        private void OnDestroy() { Time.timeScale = previousTimeScale; }

        private bool Fail() { failed = true; if (controls != null) controls.Refresh(elapsed, paused, true); Debug.LogWarning("录像状态无效，已停止播放。"); return false; }
    }
}
