using EachOneCoin.Gameplay;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace EachOneCoin.Networking
{
    public sealed class NetworkMatchController : MonoBehaviour
    {
        private MatchCoordinator match;
        private LanNetworkManager network;
        private LocalPlayerInput localInput;
        private ArenaGameUI gameUI;
        private LinearPatrolObstacle[] patrolObstacles;
        private TimedSpikeTrap[] spikeTraps;
        private uint obstacleSequence, lastObstacleSequence;
        private bool hasObstacleState;
        private float nextObstacleState;
        private readonly Vector3[] inputs = new Vector3[RoomState.Capacity];
        private readonly float[] inputTimes = new float[RoomState.Capacity];
        private readonly bool[] sprintInputs = new bool[RoomState.Capacity];
        private readonly uint[] lastAttackSequence = new uint[RoomState.Capacity];
        private readonly CombatParticipant[] replicaCombat = new CombatParticipant[RoomState.Capacity];
        private readonly double[] respawnCutoff = new double[RoomState.Capacity];
        private readonly bool[] respawnPending = new bool[RoomState.Capacity];
        private uint combatSequence, lastCombatSequence, nextAttackSequence;
        private bool hasCombatState, aiming, aimUsesGamepad;
        private Vector3 aimDirection = Vector3.forward;
        private float nextCombatState;
        public int HealthAt(int id) => NetworkServer.active ? match.Model.Participants[id].Health : replicaCombat[id].health;
        public int MaxHealthAt(int id) => NetworkServer.active ? match.Model.Participants[id].MaxHealth : replicaCombat[id].maxHealth;
        public float StaminaAt(int id) => NetworkServer.active ? match.StaminaAt(id).Current : replicaCombat[id].stamina;
        public ParticipantStatus StatusAt(int id)
        {
            if (NetworkServer.active) return match.Model.Participants[id].Status;
            if (hasMatchState && (replicaMatchState.StatusAt(id) == (byte)ParticipantStatus.Won || replicaMatchState.StatusAt(id) == (byte)ParticipantStatus.Lost))
                return (ParticipantStatus)replicaMatchState.StatusAt(id);
            return hasCombatState ? (ParticipantStatus)replicaCombat[id].status : ParticipantStatus.Active;
        }
        private MatchStateMessage replicaMatchState;
        private uint matchStateSequence;
        private bool hasMatchState, finalStateSent;
        private float nextMatchState;
        public MatchStatus CurrentMatchStatus => NetworkServer.active ? match.Model.Status : hasMatchState ? (MatchStatus)replicaMatchState.matchStatus : MatchStatus.Waiting;
        public float RemainingTime => NetworkServer.active ? match.Model.RemainingTime : hasMatchState ? replicaMatchState.remaining : 60f;
        public bool IsParticipant(int slot) => slot >= 0 && slot < RoomState.Capacity && (NetworkServer.active ? match.Model.Participants[slot].IsParticipating : hasMatchState && (replicaMatchState.participantMask & (1 << slot)) != 0);
        private const double InterpolationDelay = 0.10;
        private const double MaximumBacklog = 0.5;
        private const int MaximumSnapshots = 64;
        private readonly List<MovementSnapshot> snapshots = new List<MovementSnapshot>(MaximumSnapshots);
        private double playbackTime;
        private bool playbackStarted;
        private byte humans;
        private byte connectedHumans;
        private byte visible;
        private bool started;
        private bool hasSnapshot;
        private float nextInput;
        private float nextSnapshot;
        private uint gridVersion;
        private bool hasGridSnapshot;
        private bool gridResyncPending;
        public bool IsRunning => started;
        public bool IsSlotActive(int slot) => slot >= 0 && slot < RoomState.Capacity && (visible & (1 << slot)) != 0;
        public bool CanRespawn(int slot) => slot >= 0 && slot < RoomState.Capacity && ((humans & (1 << slot)) == 0 || (connectedHumans & (1 << slot)) != 0);
        private uint coinVersion;
        private bool hasCoinSnapshot;
        private bool coinResyncPending;
        private readonly int[] replicaScores = new int[RoomState.Capacity];
        private readonly float[] replicaMineProgress = new float[20];
        private readonly double[] replicaMineElapsed = new double[5];
        private double replicaTestElapsed;
        public double CoinTestElapsed => NetworkServer.active ? match.Coins.ElapsedSeconds : replicaTestElapsed;
        private uint replicaMineConnections;
        private float nextMineStatus;
        public float MineProgressAt(int mine, int slot) => NetworkServer.active ? (float)match.Model.Mines[mine].Progress(slot) : replicaMineProgress[mine * 4 + slot];
        public bool MineConnectedAt(int mine, int slot) => NetworkServer.active ? match.Coins.IsConnectedToMine(mine, slot) : (replicaMineConnections & (1u << (mine * 4 + slot))) != 0;
        public int ScoreAt(int slot) => NetworkServer.active ? match.Model.Participants[slot].Coins : hasMatchState ? replicaMatchState.ScoreAt(slot) : replicaScores[slot];

        public void Initialize(MatchCoordinator coordinator, LanNetworkManager manager, LocalPlayerInput input)
        {
            match = coordinator; network = manager; localInput = input;
            gameUI = FindObjectOfType<ArenaGameUI>();
            var patrols = new List<LinearPatrolObstacle>();
            var spikes = new List<TimedSpikeTrap>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                patrols.AddRange(root.GetComponentsInChildren<LinearPatrolObstacle>(true));
                spikes.AddRange(root.GetComponentsInChildren<TimedSpikeTrap>(true));
            }
            patrolObstacles = patrols.ToArray();
            spikeTraps = spikes.ToArray();
        }

        public void Begin(byte occupied, bool fillNpcs)
        {
            if (started || match == null) return;
            started = true; humans = occupied;
            visible = fillNpcs ? (byte)15 : occupied;
            connectedHumans = NetworkServer.active ? (byte)0 : occupied;
            if (NetworkServer.active) for (int i = 0; i < RoomState.Capacity; i++) if ((occupied & (1 << i)) != 0 && network.IsSeatConnected(i)) connectedHumans |= (byte)(1 << i);
            match.Rules.ConfigureParticipants(visible);
            match.Rules.StartMatch();
            for (int i = 0; i < RoomState.Capacity; i++)
            {
                bool active = (visible & (1 << i)) != 0 && StatusAt(i) == ParticipantStatus.Active;
                match.Characters[i].SetActiveState(active);
                inputTimes[i] = float.NegativeInfinity;
            }
        }

        private void SendToReadyClients<T>(T message) where T : struct, NetworkMessage
        {
            foreach (var connection in NetworkServer.connections.Values)
                if (network.IsGameplayReady(connection)) connection.Send(message);
        }

        public void SendObstacleState(NetworkConnectionToClient connection = null)
        {
            if (!NetworkServer.active || !started) return;
            var positions = new Vector3[patrolObstacles.Length];
            var spikes = new SpikeObstacleState[spikeTraps.Length];
            for (int i = 0; i < positions.Length; i++) positions[i] = patrolObstacles[i].NetworkPosition;
            for (int i = 0; i < spikes.Length; i++)
                spikes[i] = new SpikeObstacleState { active = spikeTraps[i].IsActive, remainingFraction = spikeTraps[i].RemainingFraction, spikeHeight = spikeTraps[i].NetworkSpikeHeight };
            var message = new ObstacleStateMessage { sequence = ++obstacleSequence, patrolPositions = positions, spikes = spikes };
            if (connection == null) SendToReadyClients(message); else connection.Send(message);
        }

        public void ReceiveObstacleState(ObstacleStateMessage message)
        {
            if (NetworkServer.active || match == null || !started || (hasObstacleState && message.sequence <= lastObstacleSequence) ||
                message.patrolPositions == null || message.patrolPositions.Length != patrolObstacles.Length ||
                message.spikes == null || message.spikes.Length != spikeTraps.Length) return;
            foreach (var p in message.patrolPositions)
                if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z)) return;
            foreach (var spike in message.spikes)
                if (!Finite(spike.remainingFraction) || spike.remainingFraction < 0f || spike.remainingFraction > 1f || !Finite(spike.spikeHeight)) return;
            for (int i = 0; i < patrolObstacles.Length; i++) patrolObstacles[i].ApplyNetworkPosition(message.patrolPositions[i]);
            for (int i = 0; i < spikeTraps.Length; i++) spikeTraps[i].ApplyNetworkState(message.spikes[i].active, message.spikes[i].remainingFraction, message.spikes[i].spikeHeight);
            hasObstacleState = true;
            lastObstacleSequence = message.sequence;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void SendMatchState(NetworkConnectionToClient connection = null)
        {
            if (!NetworkServer.active || match == null || !started) return;
            var players = match.Model.Participants;
            byte mask = 0;
            for (int i = 0; i < RoomState.Capacity; i++) if (players[i].IsParticipating) mask |= (byte)(1 << i);
            var message = new MatchStateMessage { sequence = ++matchStateSequence, matchStatus = (byte)match.Model.Status,
                participantMask = mask, remaining = match.Model.RemainingTime,
                status0 = (byte)players[0].Status, status1 = (byte)players[1].Status, status2 = (byte)players[2].Status, status3 = (byte)players[3].Status,
                score0 = players[0].Coins, score1 = players[1].Coins, score2 = players[2].Coins, score3 = players[3].Coins };
            if (connection == null) SendToReadyClients(message); else connection.Send(message);
        }

        public void ReceiveMatchState(MatchStateMessage message)
        {
            if (NetworkServer.active || match == null || (hasMatchState && message.sequence <= replicaMatchState.sequence)) return;
            if (message.matchStatus > (byte)MatchStatus.Finished || (message.participantMask & ~15) != 0 || message.participantMask == 0 ||
                float.IsNaN(message.remaining) || float.IsInfinity(message.remaining) || message.remaining < 0f || message.remaining > 60.01f) return;
            for (int i = 0; i < RoomState.Capacity; i++)
                if (message.StatusAt(i) > (byte)ParticipantStatus.Lost || message.ScoreAt(i) < 0 ||
                    (hasMatchState && (replicaMatchState.StatusAt(i) == (byte)ParticipantStatus.Won || replicaMatchState.StatusAt(i) == (byte)ParticipantStatus.Lost) && message.StatusAt(i) != replicaMatchState.StatusAt(i))) return;
            if (hasMatchState && replicaMatchState.matchStatus == (byte)MatchStatus.Finished && message.matchStatus != (byte)MatchStatus.Finished) return;
            replicaMatchState = message; hasMatchState = true;
            if (message.matchStatus == (byte)MatchStatus.Finished && network != null) network.ClearRecoverySession();
            if (!started) return;
            for (int i = 0; i < RoomState.Capacity; i++)
                if (StatusAt(i) == ParticipantStatus.Won || StatusAt(i) == ParticipantStatus.Lost) match.Characters[i].SetActiveState(false);
        }

        public void SendGridSnapshot(NetworkConnectionToClient connection = null)
        {
            if (!NetworkServer.active || match == null) return;
            var grid = match.Model.Grid;
            var owners = new byte[grid.Width * grid.Height];
            for (int x = 0; x < grid.Width; x++) for (int y = 0; y < grid.Height; y++)
            {
                int? owner = grid.OwnerAt(new GridPosition(x, y));
                owners[x * grid.Height + y] = owner.HasValue ? (byte)(owner.Value + 1) : (byte)0;
            }
            var snapshot = new GridSnapshotMessage { version = gridVersion, owners = owners };
            if (connection == null) SendToReadyClients(snapshot);
            else connection.Send(snapshot);
        }

        public void BroadcastGridChange(GridPosition position, int owner)
        {
            if (!NetworkServer.active || !started) return;
            unchecked { gridVersion++; }
            SendToReadyClients(new GridChangeMessage { version = gridVersion, x = (ushort)position.X, y = (ushort)position.Y, owner = (byte)(owner + 1) });
        }

        public void ReceiveGridSnapshot(GridSnapshotMessage message)
        {
            if (NetworkServer.active || match == null || (hasGridSnapshot && message.version < gridVersion)) return;
            if (!match.Model.Grid.ApplyReplicaSnapshot(message.owners)) { RequestGridSnapshot(); return; }
            gridVersion = message.version; hasGridSnapshot = true; gridResyncPending = false;
            match.Grid.Refresh(match.Model.Grid);
        }

        public void ReceiveGridChange(GridChangeMessage message)
        {
            if (NetworkServer.active || match == null) return;
            if (hasGridSnapshot && message.version <= gridVersion) return;
            if (!hasGridSnapshot || gridResyncPending || message.version != gridVersion + 1 ||
                !match.Model.Grid.ApplyReplicaOwner(new GridPosition(message.x, message.y), message.owner))
            {
                RequestGridSnapshot();
                return;
            }
            gridVersion = message.version;
            match.Grid.Refresh(match.Model.Grid);
        }

        private void RequestGridSnapshot()
        {
            if (gridResyncPending || !NetworkClient.isConnected) return;
            gridResyncPending = true;
            NetworkClient.Send(new GridSnapshotRequest());
        }

        public void SendCoinSnapshot(NetworkConnectionToClient connection = null)
        {
            if (!NetworkServer.active || match == null) return;
            var scores = match.Model.Participants;
            var message = new CoinSnapshotMessage { version = coinVersion, entries = match.Coins.CaptureReplicaEntries(),
                scatteredMines = match.Coins.ScatteredMineMask(), score0 = scores[0].Coins, score1 = scores[1].Coins,
                score2 = scores[2].Coins, score3 = scores[3].Coins };
            if (connection == null) SendToReadyClients(message); else connection.Send(message);
        }

        public void BroadcastCoinChange(byte operation, CoinEntry entry, byte mineMask)
        {
            if (!NetworkServer.active || !started) return;
            unchecked { coinVersion++; }
            var scores = match.Model.Participants;
            SendToReadyClients(new CoinChangeMessage { version = coinVersion, operation = operation, entry = entry,
                scatteredMines = mineMask, score0 = scores[0].Coins, score1 = scores[1].Coins, score2 = scores[2].Coins, score3 = scores[3].Coins });
        }

        public void ReceiveCoinSnapshot(CoinSnapshotMessage message)
        {
            if (NetworkServer.active || match == null || (hasCoinSnapshot && message.version < coinVersion)) return;
            if (!ValidScores(message.score0, message.score1, message.score2, message.score3) ||
                !match.Coins.ApplyReplicaSnapshot(message.entries, message.scatteredMines)) { RequestCoinSnapshot(); return; }
            SetScores(message.score0, message.score1, message.score2, message.score3);
            coinVersion = message.version; hasCoinSnapshot = true; coinResyncPending = false;
        }

        public void ReceiveCoinChange(CoinChangeMessage message)
        {
            if (NetworkServer.active || match == null) return;
            if (hasCoinSnapshot && message.version <= coinVersion) return;
            if (!hasCoinSnapshot || coinResyncPending || message.version != coinVersion + 1 ||
                !ValidScores(message.score0, message.score1, message.score2, message.score3) ||
                !match.Coins.ApplyReplicaChange(message.operation, message.entry, message.scatteredMines))
            { RequestCoinSnapshot(); return; }
            SetScores(message.score0, message.score1, message.score2, message.score3); coinVersion = message.version;
        }

        private static bool ValidScores(int a, int b, int c, int d) => a >= 0 && b >= 0 && c >= 0 && d >= 0;
        private void SetScores(int a, int b, int c, int d) { replicaScores[0] = a; replicaScores[1] = b; replicaScores[2] = c; replicaScores[3] = d; }
        private void RequestCoinSnapshot()
        {
            if (coinResyncPending || !NetworkClient.isConnected) return;
            coinResyncPending = true; NetworkClient.Send(new CoinSnapshotRequest());
        }

        public void SendMineStatus(NetworkConnectionToClient connection = null)
        {
            var progress = new float[20]; var elapsed = new double[5]; uint connected = 0;
            for (int i = 0; i < 5; i++)
            {
                elapsed[i] = match.Model.Mines[i].Elapsed;
                for (int j = 0; j < RoomState.Capacity; j++)
                {
                    progress[i * 4 + j] = (float)match.Model.Mines[i].Progress(j);
                    if (match.Coins.IsConnectedToMine(i, j)) connected |= 1u << (i * 4 + j);
                }
            }
            var message = new MineStatusMessage { progress = progress, elapsed = elapsed, testElapsed = match.Coins.ElapsedSeconds, connectedMask = connected, scatteredMines = match.Coins.ScatteredMineMask() };
            if (connection == null) SendToReadyClients(message); else connection.Send(message);
        }

        public void ReceiveMineStatus(MineStatusMessage message)
        {
            if (NetworkServer.active || match == null || message.progress == null || message.progress.Length != 20 || message.elapsed == null || message.elapsed.Length != 5 || (message.scatteredMines & ~31) != 0) return;
            if (double.IsNaN(message.testElapsed) || double.IsInfinity(message.testElapsed) || message.testElapsed < 0d || message.testElapsed > 60.01d) return;
            for (int i = 0; i < 20; i++) if (float.IsNaN(message.progress[i]) || float.IsInfinity(message.progress[i]) || message.progress[i] < 0f) return;
            for (int i = 0; i < 5; i++) if (double.IsNaN(message.elapsed[i]) || double.IsInfinity(message.elapsed[i]) || message.elapsed[i] < 0d) return;
            System.Array.Copy(message.progress, replicaMineProgress, 20);
            System.Array.Copy(message.elapsed, replicaMineElapsed, 5);
            replicaMineConnections = message.connectedMask;
            replicaTestElapsed = message.testElapsed;
            match.Coins.ApplyReplicaMineMask(message.scatteredMines);
        }

        private void UpdateLocalAim()
        {
            int id = network.LocalSlot;
            if (id < 0 || id >= RoomState.Capacity || StatusAt(id) != ParticipantStatus.Active)
            {
                if (aiming && id >= 0 && id < RoomState.Capacity) match.Characters[id].SetAttackAim(false, aimDirection, 0f, 0f);
                aiming = false;
                return;
            }
            if (!aiming && (localInput.KeyboardAttackDown || localInput.GamepadAttackDown))
            {
                aimUsesGamepad = !localInput.KeyboardAttackDown && localInput.GamepadAttackDown;
                aiming = true;
            }
            if (!aiming) return;
            if (localInput.TryAimDirection(aimUsesGamepad, match.Characters[id].transform.position, Camera.main, out var direction)) aimDirection = direction;
            if (!localInput.AttackHeld(aimUsesGamepad))
            {
                aiming = false;
                match.Characters[id].SetAttackAim(false, aimDirection, 0f, 0f);
                if (NetworkClient.isConnected) NetworkClient.Send(new AttackRequest { sequence = ++nextAttackSequence, direction = aimDirection });
                return;
            }
            match.Characters[id].SetAttackAim(true, aimDirection, match.Combat.AttackRange, match.Combat.AttackAngle);
        }

        public void ReceiveAttack(int slot, AttackRequest request)
        {
            if (!NetworkServer.active || !started || !IsSlotActive(slot) || (humans & (1 << slot)) == 0 || (connectedHumans & (1 << slot)) == 0 || request.sequence <= lastAttackSequence[slot]) return;
            lastAttackSequence[slot] = request.sequence;
            var d = request.direction;
            if (float.IsNaN(d.x) || float.IsInfinity(d.x) || float.IsNaN(d.y) || float.IsInfinity(d.y) || float.IsNaN(d.z) || float.IsInfinity(d.z)) return;
            d.y = 0f;
            if (d.sqrMagnitude < 0.000001f || d.sqrMagnitude > 4f) return;
            match.Combat.TryAttack(slot, d.normalized);
        }

        public void BroadcastAttack(int slot, Vector3 direction)
        {
            if (NetworkServer.active) SendToReadyClients(new AttackEvent { slot = (byte)slot, direction = direction });
        }

        public void BroadcastDamage(int slot, bool obstacle)
        {
            if (NetworkServer.active && started && slot >= 0 && slot < RoomState.Capacity)
                SendToReadyClients(new DamageEvent { slot = (byte)slot, kind = obstacle ? (byte)0 : (byte)1 });
        }

        public void ReceiveDamageEvent(DamageEvent message)
        {
            if (NetworkServer.active || !started || match == null || network == null || network.ClientResuming ||
                message.slot >= RoomState.Capacity || message.kind > 1 || message.slot != network.LocalSlot) return;
            match.NotifyNetworkDamage(message.slot, message.kind == 0);
        }

        public void ReceiveAttackEvent(AttackEvent message)
        {
            if (NetworkServer.active || match == null || message.slot >= RoomState.Capacity) return;
            match.Characters[message.slot].PlayAttack(message.direction, match.Combat.AttackRange, match.Combat.AttackAngle);
        }

        private CombatParticipant CaptureCombatParticipant(int slot)
        {
            var p = match.Model.Participants[slot];
            return new CombatParticipant { status = (byte)p.Status, health = p.Health, maxHealth = p.MaxHealth,
                stamina = match.StaminaAt(slot).Current, position = match.Characters[slot].transform.position };
        }

        public void SendCombatState(NetworkConnectionToClient connection = null)
        {
            if (!NetworkServer.active || match == null) return;
            var message = new CombatStateMessage { sequence = ++combatSequence, serverTime = NetworkTime.localTime, p0 = CaptureCombatParticipant(0),
                p1 = CaptureCombatParticipant(1), p2 = CaptureCombatParticipant(2), p3 = CaptureCombatParticipant(3) };
            if (connection == null) SendToReadyClients(message); else connection.Send(message);
        }

        public void ReceiveCombatState(CombatStateMessage message)
        {
            if (NetworkServer.active || match == null || (hasCombatState && message.sequence <= lastCombatSequence) ||
                double.IsNaN(message.serverTime) || double.IsInfinity(message.serverTime) || message.serverTime < 0d) return;
            for (int i = 0; i < RoomState.Capacity; i++)
            {
                var p = message.Participant(i);
                if (p.status > (byte)ParticipantStatus.Lost || p.health < 0 || p.maxHealth < 0 || p.health > p.maxHealth ||
                    float.IsNaN(p.stamina) || float.IsInfinity(p.stamina) || p.stamina < 0f ||
                    float.IsNaN(p.position.x) || float.IsInfinity(p.position.x) || float.IsNaN(p.position.y) || float.IsInfinity(p.position.y) ||
                    float.IsNaN(p.position.z) || float.IsInfinity(p.position.z)) return;
            }
            for (int i = 0; i < RoomState.Capacity; i++)
            {
                var p = message.Participant(i);
                bool respawned = hasCombatState && replicaCombat[i].status != (byte)ParticipantStatus.Active && p.status == (byte)ParticipantStatus.Active;
                replicaCombat[i] = p;
                if (!started) continue;
                if (respawned)
                {
                    respawnCutoff[i] = message.serverTime;
                    respawnPending[i] = true;
                    match.Characters[i].ApplyNetworkPosition(p.position);
                }
                match.Characters[i].SetActiveState((visible & (1 << i)) != 0 && StatusAt(i) == ParticipantStatus.Active);
            }
            hasCombatState = true; lastCombatSequence = message.sequence;
        }

        public void ReceiveInput(int slot, Vector2 direction, bool sprint)
        {
            if (!started || slot < 0 || slot >= RoomState.Capacity || (humans & (1 << slot)) == 0 || (connectedHumans & (1 << slot)) == 0) return;
            if (float.IsNaN(direction.x) || float.IsInfinity(direction.x) || float.IsNaN(direction.y) || float.IsInfinity(direction.y)) return;
            inputs[slot] = Vector3.ClampMagnitude(new Vector3(direction.x, 0f, direction.y), 1f);
            sprintInputs[slot] = sprint;
            inputTimes[slot] = Time.unscaledTime;
        }

        public void SuspendPlayer(int slot)
        {
            if (slot < 0 || slot >= RoomState.Capacity || !started || (humans & (1 << slot)) == 0) return;
            connectedHumans = (byte)(connectedHumans & ~(1 << slot));
            inputs[slot] = Vector3.zero; sprintInputs[slot] = false; inputTimes[slot] = float.NegativeInfinity;
            match.Characters[slot].StopNetworkInput();
        }

        public void RestorePlayer(int slot)
        {
            if (slot < 0 || slot >= RoomState.Capacity || !started || (humans & (1 << slot)) == 0) return;
            connectedHumans |= (byte)(1 << slot);
            inputs[slot] = Vector3.zero; sprintInputs[slot] = false; inputTimes[slot] = float.NegativeInfinity;
            lastAttackSequence[slot] = 0;
        }

        public void RemovePlayer(int slot)
        {
            if (slot < 0 || slot >= RoomState.Capacity || !started) return;
            humans = (byte)(humans & ~(1 << slot));
            connectedHumans = (byte)(connectedHumans & ~(1 << slot));
            visible = (byte)(visible & ~(1 << slot));
            inputs[slot] = Vector3.zero;
            sprintInputs[slot] = false;
            match.Characters[slot].SetActiveState(false);
            if (NetworkServer.active && match.Model.Status == MatchStatus.Playing)
            {
                match.Rules.Forfeit(slot);
                SendMatchState();
                SendCombatState();
            }
        }

        public void ReceiveSnapshot(MovementSnapshot snapshot)
        {
            if (!started || NetworkServer.active) return;
            if (snapshots.Count > 0 && snapshot.serverTime <= snapshots[snapshots.Count - 1].serverTime) return;
            if (snapshots.Count == MaximumSnapshots) snapshots.RemoveAt(0);
            snapshots.Add(snapshot);
            if (!hasSnapshot)
            {
                ApplySnapshot(snapshot, snapshot, 0f);
                hasSnapshot = true;
            }
        }

        private void Update()
        {
            if (network == null || match == null) return;
            if (!started) return;
            bool menuOpen = gameUI != null && gameUI.NetworkMenuOpen;
            if (!network.ClientResuming && CurrentMatchStatus == MatchStatus.Playing && StatusAt(network.LocalSlot) == ParticipantStatus.Active && NetworkClient.isConnected && Time.unscaledTime >= nextInput)
            {
                nextInput = Time.unscaledTime + 0.05f;
                Vector3 direction = menuOpen ? Vector3.zero : localInput.Movement;
                NetworkClient.Send(new MovementInput { direction = new Vector2(direction.x, direction.z), sprint = !menuOpen && localInput.SprintHeld });
            }
            if (!NetworkServer.active && CurrentMatchStatus == MatchStatus.Finished) { if (hasSnapshot) UpdatePlayback(); return; }
            if (!network.ClientResuming && !menuOpen) UpdateLocalAim();
            else if (aiming)
            {
                match.Characters[network.LocalSlot].SetAttackAim(false, aimDirection, 0f, 0f);
                aiming = false;
            }
            match.Coins.Tick(Time.deltaTime);
            if (NetworkServer.active)
            {
                float dt = Mathf.Min(Time.deltaTime, match.Model.RemainingTime);
                if (match.Model.Status == MatchStatus.Playing)
                {
                match.Coins.BeginFrame();
                match.Combat.Tick(dt);
                for (int i = 0; i < RoomState.Capacity && match.Model.Status == MatchStatus.Playing; i++)
                {
                    if ((visible & (1 << i)) == 0) continue;
                    Vector3 direction;
                    if ((humans & (1 << i)) != 0)
                        direction = Time.unscaledTime - inputTimes[i] <= 0.3f ? inputs[i] : Vector3.zero;
                    else
                    {
                        int phase = (Mathf.FloorToInt(Time.unscaledTime / 2f) + i) % 4;
                        direction = phase == 0 ? Vector3.right : phase == 1 ? Vector3.forward : phase == 2 ? Vector3.left : Vector3.back;
                    }
                    bool inputFresh = Time.unscaledTime - inputTimes[i] <= 0.3f;
                    bool active = match.Model.Participants[i].Status == ParticipantStatus.Active;
                    match.Rules.TickStamina(i, dt, active && !match.Characters[i].IsKnockedBack,
                        direction.sqrMagnitude > 0f, (humans & (1 << i)) != 0 && inputFresh && sprintInputs[i],
                        match.SprintStaminaPerSecond, match.StaminaRecoveryPerSecond);
                    float speed = match.MoveSpeed * (match.StaminaAt(i).IsSprinting ? match.SprintSpeedMultiplier : 1f);
                    match.Characters[i].MoveWithVelocity(direction * speed, dt);
                    match.Characters[i].TickAttackKnockback(dt);
                }
                match.Coins.AdvanceMatch(dt);
                match.Rules.AdvanceTime(dt);
                }
                for (int i = 0; i < RoomState.Capacity; i++)
                    if (match.Model.Participants[i].Status == ParticipantStatus.Won || match.Model.Participants[i].Status == ParticipantStatus.Lost)
                        match.Characters[i].SetActiveState(false);
                bool finished = match.Model.Status == MatchStatus.Finished;
                if ((finished && !finalStateSent) || (!finished && Time.unscaledTime >= nextMatchState))
                {
                    nextMatchState = Time.unscaledTime + 0.1f;
                    SendMatchState();
                    if (finished) { SendCombatState(); finalStateSent = true; }
                }
                if (finished) return;
                if (Time.unscaledTime >= nextObstacleState) { nextObstacleState = Time.unscaledTime + 0.1f; SendObstacleState(); }
                if (Time.unscaledTime >= nextCombatState) { nextCombatState = Time.unscaledTime + 0.1f; SendCombatState(); }
                if (Time.unscaledTime >= nextMineStatus) { nextMineStatus = Time.unscaledTime + 0.25f; SendMineStatus(); }
                if (Time.unscaledTime >= nextSnapshot)
                {
                    nextSnapshot = Time.unscaledTime + 0.05f;
                    SendMovementState();
                }
            }
            else if (hasSnapshot) UpdatePlayback();
        }

        public void SendMovementState(NetworkConnectionToClient connection = null)
        {
            if (!NetworkServer.active || !started) return;
            var message = new MovementSnapshot { visible = visible, serverTime = NetworkTime.localTime,
                p0 = match.Characters[0].transform.position, p1 = match.Characters[1].transform.position,
                p2 = match.Characters[2].transform.position, p3 = match.Characters[3].transform.position };
            if (connection == null) SendToReadyClients(message); else connection.Send(message);
        }

        private void UpdatePlayback()
        {
            if (snapshots.Count == 0) return;
            double newest = snapshots[snapshots.Count - 1].serverTime;
            if (!playbackStarted)
            {
                if (newest - snapshots[0].serverTime < InterpolationDelay) return;
                playbackTime = snapshots[0].serverTime;
                playbackStarted = true;
            }
            else playbackTime += Time.unscaledDeltaTime;
            if (newest - playbackTime > MaximumBacklog) playbackTime = newest - InterpolationDelay;
            if (playbackTime > newest) playbackTime = newest;
            while (snapshots.Count > 2 && snapshots[1].serverTime <= playbackTime) snapshots.RemoveAt(0);
            var from = snapshots[0];
            var to = snapshots.Count > 1 ? snapshots[1] : from;
            double duration = to.serverTime - from.serverTime;
            float fraction = duration > 0 ? Mathf.Clamp01((float)((playbackTime - from.serverTime) / duration)) : 0f;
            ApplySnapshot(from, to, fraction);
        }

        private void ApplySnapshot(MovementSnapshot from, MovementSnapshot to, float fraction)
        {
            byte mask = fraction >= 1f ? to.visible : from.visible;
            visible = mask;
            for (int i = 0; i < RoomState.Capacity; i++)
            {
                bool active = (mask & (1 << i)) != 0 && StatusAt(i) == ParticipantStatus.Active;
                match.Characters[i].SetActiveState(active);
                if (!active) continue;
                if (respawnPending[i])
                {
                    if (from.serverTime < respawnCutoff[i])
                    {
                        double sampleTime = from.serverTime + (to.serverTime - from.serverTime) * fraction;
                        if (to.serverTime >= respawnCutoff[i] && sampleTime >= respawnCutoff[i])
                            match.Characters[i].ApplyNetworkPosition(to.Position(i));
                        continue;
                    }
                    respawnPending[i] = false;
                }
                bool both = (from.visible & to.visible & (1 << i)) != 0;
                Vector3 position = both ? Vector3.Lerp(from.Position(i), to.Position(i), fraction) :
                    ((from.visible & (1 << i)) != 0 ? from.Position(i) : to.Position(i));
                match.Characters[i].ApplyNetworkPosition(position);
            }
        }

    }
}
