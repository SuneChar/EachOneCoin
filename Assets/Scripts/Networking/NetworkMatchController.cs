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
            if (NetworkServer.active)
            {
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
            GUI.Box(new Rect(10, 10, 360, 65), started ? "LAN paint test - attack and gameplay disabled" : "Waiting for all players to load...");
            if (GUI.Button(new Rect(20, 40, 160, 25), "Exit (Esc)")) network.ExitMovementTest();
        }
    }
}
