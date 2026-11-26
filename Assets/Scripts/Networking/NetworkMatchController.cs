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
        private readonly Vector3[] inputs = new Vector3[RoomState.Capacity];
        private readonly float[] inputTimes = new float[RoomState.Capacity];
        private const double InterpolationDelay = 0.10;
        private const double MaximumBacklog = 0.5;
        private const int MaximumSnapshots = 64;
        private readonly List<MovementSnapshot> snapshots = new List<MovementSnapshot>(MaximumSnapshots);
        private double playbackTime;
        private bool playbackStarted;
        private byte humans;
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
        public int ScoreAt(int slot) => NetworkServer.active ? match.Model.Participants[slot].Coins : replicaScores[slot];

        public void Initialize(MatchCoordinator coordinator, LanNetworkManager manager, LocalPlayerInput input)
        {
            match = coordinator; network = manager; localInput = input;
        }

        public void Begin(byte occupied, bool fillNpcs)
        {
            if (started || match == null) return;
            started = true; humans = occupied;
            visible = fillNpcs ? (byte)15 : occupied;
            for (int i = 0; i < RoomState.Capacity; i++)
            {
                bool active = (visible & (1 << i)) != 0;
                match.Characters[i].SetActiveState(active);
                inputTimes[i] = float.NegativeInfinity;
            }
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
            if (connection == null) NetworkServer.SendToAll(snapshot);
            else connection.Send(snapshot);
        }

        public void BroadcastGridChange(GridPosition position, int owner)
        {
            if (!NetworkServer.active || !started) return;
            unchecked { gridVersion++; }
            NetworkServer.SendToAll(new GridChangeMessage { version = gridVersion, x = (ushort)position.X, y = (ushort)position.Y, owner = (byte)(owner + 1) });
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
            if (connection == null) NetworkServer.SendToAll(message); else connection.Send(message);
        }

        public void BroadcastCoinChange(byte operation, CoinEntry entry, byte mineMask)
        {
            if (!NetworkServer.active || !started) return;
            unchecked { coinVersion++; }
            var scores = match.Model.Participants;
            NetworkServer.SendToAll(new CoinChangeMessage { version = coinVersion, operation = operation, entry = entry,
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

        private void SendMineStatus()
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
            NetworkServer.SendToAll(new MineStatusMessage { progress = progress, elapsed = elapsed, testElapsed = match.Coins.ElapsedSeconds, connectedMask = connected, scatteredMines = match.Coins.ScatteredMineMask() });
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

        public void ReceiveInput(int slot, Vector2 direction)
        {
            if (!started || slot < 0 || slot >= RoomState.Capacity || (humans & (1 << slot)) == 0) return;
            if (float.IsNaN(direction.x) || float.IsInfinity(direction.x) || float.IsNaN(direction.y) || float.IsInfinity(direction.y)) return;
            inputs[slot] = Vector3.ClampMagnitude(new Vector3(direction.x, 0f, direction.y), 1f);
            inputTimes[slot] = Time.unscaledTime;
        }

        public void RemovePlayer(int slot)
        {
            if (slot < 0 || slot >= RoomState.Capacity || !started) return;
            humans = (byte)(humans & ~(1 << slot));
            visible = (byte)(visible & ~(1 << slot));
            inputs[slot] = Vector3.zero;
            match.Characters[slot].SetActiveState(false);
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
            if (Input.GetKeyDown(KeyCode.Escape)) { network.ExitMovementTest(); return; }
            if (!started) return;
            if (NetworkClient.isConnected && Time.unscaledTime >= nextInput)
            {
                nextInput = Time.unscaledTime + 0.05f;
                Vector3 direction = localInput.Movement;
                NetworkClient.Send(new MovementInput { direction = new Vector2(direction.x, direction.z) });
            }
            match.Coins.Tick(Time.deltaTime);
            if (NetworkServer.active)
            {
                match.Coins.BeginFrame();
                for (int i = 0; i < RoomState.Capacity; i++)
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
                    match.Characters[i].MoveWithVelocity(direction * match.MoveSpeed, Time.deltaTime);
                }
                match.Coins.AdvanceMatch(Time.deltaTime);
                if (Time.unscaledTime >= nextMineStatus) { nextMineStatus = Time.unscaledTime + 0.25f; SendMineStatus(); }
                if (Time.unscaledTime >= nextSnapshot)
                {
                    nextSnapshot = Time.unscaledTime + 0.05f;
                    NetworkServer.SendToAll(new MovementSnapshot { visible = visible, serverTime = NetworkTime.localTime,
                        p0 = match.Characters[0].transform.position, p1 = match.Characters[1].transform.position,
                        p2 = match.Characters[2].transform.position, p3 = match.Characters[3].transform.position });
                }
            }
            else if (hasSnapshot) UpdatePlayback();
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
                bool active = (mask & (1 << i)) != 0;
                match.Characters[i].SetActiveState(active);
                if (!active) continue;
                bool both = (from.visible & to.visible & (1 << i)) != 0;
                Vector3 position = both ? Vector3.Lerp(from.Position(i), to.Position(i), fraction) :
                    ((from.visible & (1 << i)) != 0 ? from.Position(i) : to.Position(i));
                match.Characters[i].ApplyNetworkPosition(position);
            }
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(10, 10, 360, 65), started ? "LAN coin test - combat disabled" : "Waiting for all players to load...");
            if (GUI.Button(new Rect(20, 40, 160, 25), "Exit (Esc)")) network.ExitMovementTest();
        }
    }
}
