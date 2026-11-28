using System;
using System.Collections.Generic;
using EachOneCoin.Networking;
using UnityEngine;

namespace EachOneCoin.Gameplay.Replay
{
    // Runs after gameplay so each sample sees the final authoritative state of the frame.
    [DefaultExecutionOrder(9000)]
    public sealed class ReplayRecorder : MonoBehaviour
    {
        private const float SampleInterval = 0.05f;
        private const int MaximumFrames = 10000;
        private const int MaximumEvents = 30000;
        private readonly List<ReplayFrame> frames = new List<ReplayFrame>();
        private readonly List<ReplayEvent> events = new List<ReplayEvent>();
        private MatchCoordinator match;
        private LinearPatrolObstacle[] patrols;
        private TimedSpikeTrap[] spikes;
        private bool networkMatch, closed;
        private float startTime, nextSample;
        private int sequence;
        private Action<string> onSaved;
        public string SavedPath { get; private set; }

        public void Initialize(MatchCoordinator coordinator, bool network, Action<string> saved)
        {
            match = coordinator;
            networkMatch = network;
            onSaved = saved;
            var patrolList = new List<LinearPatrolObstacle>();
            var spikeList = new List<TimedSpikeTrap>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                patrolList.AddRange(root.GetComponentsInChildren<LinearPatrolObstacle>(true));
                spikeList.AddRange(root.GetComponentsInChildren<TimedSpikeTrap>(true));
            }
            patrols = patrolList.ToArray(); spikes = spikeList.ToArray();
            startTime = Time.time;
            match.FeedEventRaised += OnFeed;
            Capture(0f);
        }

        public void RecordAttack(int slot, Vector3 direction)
        {
            AddEvent(new ReplayEvent { type = "attack", actor = slot, direction = direction });
        }

        private void OnFeed(MatchFeedEvent evt)
        {
            AddEvent(new ReplayEvent { type = "feed", actor = evt.PrimarySlot, target = evt.SecondarySlot,
                value = evt.Value, detail = (int)evt.Type, direction = new Vector3(evt.MineIndex, (int)evt.Reason, 0f) });
        }

        private void AddEvent(ReplayEvent evt)
        {
            if (closed) return;
            if (events.Count >= MaximumEvents) { Abort("录像事件过多"); return; }
            evt.time = Mathf.Max(0f, Time.time - startTime);
            evt.sequence = sequence++;
            events.Add(evt);
        }

        private void LateUpdate()
        {
            if (closed || match == null || !match.isActiveAndEnabled || match.Model == null || match.IsPaused) return;
            try
            {
                float time = Mathf.Max(0f, Time.time - startTime);
                bool finished = match.Model.Status == MatchStatus.Finished;
                if (finished || time >= nextSample)
                {
                    Capture(time);
                    nextSample = time + SampleInterval;
                }
                if (finished) Finish();
            }
            catch (Exception e)
            {
                if (closed) Debug.LogWarning("录像保存或分发失败：" + e.Message);
                else Abort("采样或保存失败：" + e.Message);
            }
        }

        private void Capture(float time)
        {
            if (closed) return;
            if (frames.Count >= MaximumFrames) { Abort("录像帧数过多"); return; }
            var model = match.Model;
            var frame = new ReplayFrame { time = time, remaining = model.RemainingTime, matchStatus = (int)model.Status,
                participants = new ReplayParticipant[model.Participants.Count], gridOwners = new byte[model.Grid.Width * model.Grid.Height],
                patrolPositions = new Vector3[patrols.Length], spikes = new ReplaySpike[spikes.Length] };
            for (int i = 0; i < frame.participants.Length; i++)
            {
                var p = model.Participants[i]; var character = match.Characters[i];
                bool npcAim = match.Npcs.IsWindingUp(i);
                bool humanAim = match.TryGetReplayAim(i, out var humanDirection);
                frame.participants[i] = new ReplayParticipant { participating = p.IsParticipating,
                    visible = p.IsParticipating && p.Status == ParticipantStatus.Active && (!networkMatch || match.NetworkController.IsSlotActive(i)),
                    displayName = match.GetParticipantDisplayName(i), position = character.transform.position, facing = character.FacingDirection,
                    aiming = npcAim || humanAim, aimDirection = npcAim ? match.Npcs.WindupDirection(i) : humanDirection,
                    health = p.Health, maxHealth = p.MaxHealth, coins = p.Coins, finishOrder = p.FinishOrder,
                    status = (int)p.Status, stamina = match.StaminaAt(i).Current };
            }
            for (int x = 0; x < model.Grid.Width; x++) for (int y = 0; y < model.Grid.Height; y++)
            {
                int? owner = model.Grid.OwnerAt(new GridPosition(x, y));
                frame.gridOwners[x * model.Grid.Height + y] = owner.HasValue ? (byte)(owner.Value + 1) : (byte)0;
            }
            var entries = match.Coins.CaptureReplicaEntries();
            frame.coins = new ReplayCoin[entries.Length];
            for (int i = 0; i < entries.Length; i++) frame.coins[i] = new ReplayCoin { id = entries[i].id, kind = entries[i].kind, x = entries[i].x, y = entries[i].y };
            for (int i = 0; i < patrols.Length; i++) frame.patrolPositions[i] = patrols[i].NetworkPosition;
            for (int i = 0; i < spikes.Length; i++) frame.spikes[i] = new ReplaySpike { active = spikes[i].IsActive, remainingFraction = spikes[i].RemainingFraction, height = spikes[i].NetworkSpikeHeight };
            var schedule = match.Coins.CaptureSchedule();
            frame.mineElapsed = schedule.elapsed; frame.nextMineEvent = schedule.nextEvent; frame.warningMine = schedule.warningMine;
            frame.warningX = schedule.warningX; frame.warningY = schedule.warningY;
            frames.Add(frame);
        }

        private void Finish()
        {
            if (closed) return;
            closed = true;
            match.FeedEventRaised -= OnFeed;
            var data = new ReplayData { formatVersion = ReplayData.CurrentFormat, gameVersion = Application.version, matchId = Guid.NewGuid().ToString("N"),
                recordedUtc = DateTime.UtcNow.ToString("o"), networkMatch = networkMatch, complete = true,
                coinsToWin = match.Model.CoinsToWin, duration = match.InitialDurationSeconds,
                frames = frames.ToArray(), events = events.ToArray() };
            if (ReplayStorage.TrySave(data, out var path, out var error)) { SavedPath = path; onSaved?.Invoke(path); }
            else Debug.LogWarning("录像保存失败：" + error);
        }

        private void Abort(string reason)
        {
            if (closed) return;
            closed = true;
            if (match != null) match.FeedEventRaised -= OnFeed;
            Debug.LogWarning("录像已停止：" + reason);
        }

        private void OnDestroy()
        {
            if (match != null) match.FeedEventRaised -= OnFeed;
        }
    }
}
