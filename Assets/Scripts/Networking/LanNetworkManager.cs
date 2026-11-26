using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace EachOneCoin.Networking
{
    public struct RoomSnapshot : NetworkMessage
    {
        public byte occupiedMask;
        public byte ownSlot;
        public bool fillNpcs;
    }

    public struct RoomSnapshotRequest : NetworkMessage { }

    public sealed class LanNetworkManager : NetworkManager
    {
        private RoomState room;
        public RoomSnapshot Snapshot { get; private set; }
        public bool HasSnapshot { get; private set; }
        public string Status { get; private set; } = "未连接";
        private bool leaving;
        private float connectDeadline;
        private readonly HashSet<int> movementReady = new HashSet<int>();
        public bool MatchStarted { get; private set; }
        public bool MovementRunning { get; private set; }
        public byte MatchOccupied { get; private set; }
        public bool MatchFillNpcs { get; private set; }
        public int LocalSlot => HasSnapshot ? Snapshot.ownSlot : 0;
        public int ServerSlot(int connectionId) => room != null ? room.SlotOf(connectionId) : -1;

        public void HostRoom()
        {
            if (NetworkClient.active || NetworkServer.active) return;
            leaving = false;
            Status = "正在创建房间…";
            StartHost();
        }

        public void JoinRoom(string address)
        {
            if (NetworkClient.active || NetworkServer.active) return;
            if (string.IsNullOrWhiteSpace(address)) { Status = "请输入房主 IP 地址"; return; }
            leaving = false;
            networkAddress = address.Trim();
            Status = "正在连接…";
            connectDeadline = Time.realtimeSinceStartup + 12f;
            StartClient();
            StartCoroutine(CheckTimeout());
        }

        public void LeaveRoom()
        {
            leaving = true;
            if (NetworkServer.active && NetworkClient.active) StopHost();
            else if (NetworkClient.active) StopClient();
            else if (NetworkServer.active) StopServer();
            HasSnapshot = false;
            MatchStarted = false;
            MovementRunning = false;
            movementReady.Clear();
            Status = "已退出房间";
        }

        public void SetNpcFill(bool enabled)
        {
            if (!NetworkServer.active || room == null || MatchStarted) return;
            room.SetNpcFill(enabled);
            BroadcastSnapshot();
        }

        private System.Collections.IEnumerator CheckTimeout()
        {
            while (NetworkClient.active && !NetworkClient.isConnected && !NetworkServer.active && Time.realtimeSinceStartup < connectDeadline)
                yield return null;
            if (NetworkClient.active && !NetworkClient.isConnected && !NetworkServer.active && Time.realtimeSinceStartup >= connectDeadline)
            {
                leaving = true;
                StopClient();
                Status = "连接超时，请检查 IP、防火墙和房主状态";
            }
        }

        public override void OnStartServer()
        {
            room = new RoomState();
            NetworkServer.RegisterHandler<RoomSnapshotRequest>((conn, _) => SendSnapshot(conn));
            NetworkServer.RegisterHandler<MovementReady>(OnMovementReady);
            NetworkServer.RegisterHandler<CoinSnapshotRequest>((conn, _) =>
            {
                if (!MatchStarted || ServerSlot(conn.connectionId) < 0) return;
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.SendCoinSnapshot(conn);
            });
            NetworkServer.RegisterHandler<GridSnapshotRequest>((conn, _) =>
            {
                if (!MatchStarted || ServerSlot(conn.connectionId) < 0) return;
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.SendGridSnapshot(conn);
            });
            NetworkServer.RegisterHandler<MovementInput>((conn, msg) =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (MovementRunning && controller != null) controller.ReceiveInput(ServerSlot(conn.connectionId), msg.direction);
            });
        }

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<RoomSnapshot>(message => { Snapshot = message; HasSnapshot = true; });
            NetworkClient.RegisterHandler<MovementBegin>(message =>
            {
                MatchOccupied = message.occupied; MatchFillNpcs = message.fillNpcs; MovementRunning = true;
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.Begin(message.occupied, message.fillNpcs);
            });
            NetworkClient.RegisterHandler<MineStatusMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.ReceiveMineStatus(message);
            });
            NetworkClient.RegisterHandler<CoinSnapshotMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.ReceiveCoinSnapshot(message);
            });
            NetworkClient.RegisterHandler<CoinChangeMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.ReceiveCoinChange(message);
            });
            NetworkClient.RegisterHandler<GridSnapshotMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.ReceiveGridSnapshot(message);
            });
            NetworkClient.RegisterHandler<GridChangeMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.ReceiveGridChange(message);
            });
            NetworkClient.RegisterHandler<MovementSnapshot>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.ReceiveSnapshot(message);
            });
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            if (MatchStarted || room.Add(conn.connectionId) < 0) { conn.Disconnect(); return; }
            BroadcastSnapshot();
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            int slot = ServerSlot(conn.connectionId);
            room?.Remove(conn.connectionId);
            movementReady.Remove(conn.connectionId);
            var controller = FindObjectOfType<NetworkMatchController>();
            if (controller != null) controller.RemovePlayer(slot);
            base.OnServerDisconnect(conn);
            if (MatchStarted && !MovementRunning) TryBeginMovement();
            BroadcastSnapshot();
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Status = NetworkServer.active ? "房间已创建" : "已连接房间";
            NetworkClient.Send(new RoomSnapshotRequest());
        }

        public override void OnClientDisconnect()
        {
            HasSnapshot = false;
            if (!leaving)
            {
                Status = "连接已断开，房间可能已关闭或已满";
                if (SceneManager.GetActiveScene().name == "ArenaScene") StartCoroutine(ReturnToMenu());
            }
        }

        public override void OnClientError(TransportError error, string reason)
        {
            if (!leaving) Status = "连接失败：" + error + "（请检查地址及防火墙）";
        }

        public void StartMovementTest()
        {
            if (!NetworkServer.active || room == null || MatchStarted) return;
            MatchStarted = true;
            MovementRunning = false;
            movementReady.Clear();
            ServerChangeScene("ArenaScene");
            StartCoroutine(LoadingTimeout());
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            if (SceneManager.GetActiveScene().name == "ArenaScene") StartCoroutine(SendReadyWhenInitialized());
        }

        private System.Collections.IEnumerator SendReadyWhenInitialized()
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (FindObjectOfType<NetworkMatchController>() == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (NetworkClient.isConnected && FindObjectOfType<NetworkMatchController>() != null) NetworkClient.Send(new MovementReady());
            else if (NetworkClient.active) ExitMovementTest();
        }

        private void OnMovementReady(NetworkConnectionToClient conn, MovementReady message)
        {
            if (!MatchStarted || MovementRunning || ServerSlot(conn.connectionId) < 0) return;
            movementReady.Add(conn.connectionId);
            TryBeginMovement();
        }

        private void TryBeginMovement()
        {
            if (!MatchStarted || MovementRunning || room == null) return;
            foreach (var conn in NetworkServer.connections.Values)
                if (conn.isAuthenticated && !movementReady.Contains(conn.connectionId)) return;
            byte mask = 0;
            for (int i = 0; i < RoomState.Capacity; i++) if (room.IsOccupied(i)) mask |= (byte)(1 << i);
            MatchOccupied = mask; MatchFillNpcs = room.FillEmptyWithNpcs; MovementRunning = true;
            var controller = FindObjectOfType<NetworkMatchController>();
            if (controller != null) { controller.Begin(mask, MatchFillNpcs); controller.SendGridSnapshot(); controller.SendCoinSnapshot(); }
            NetworkServer.SendToAll(new MovementBegin { occupied = mask, fillNpcs = MatchFillNpcs });
        }

        private System.Collections.IEnumerator LoadingTimeout()
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (MatchStarted && !MovementRunning && Time.realtimeSinceStartup < deadline) yield return null;
            if (MatchStarted && !MovementRunning) { Status = "加载超时，房间已关闭"; ExitMovementTest(); }
        }

        public void ExitMovementTest()
        {
            LeaveRoom();
            if (SceneManager.GetActiveScene().name != "BootScene") SceneManager.LoadScene("BootScene");
        }

        private System.Collections.IEnumerator ReturnToMenu()
        {
            yield return null;
            MatchStarted = false; MovementRunning = false;
            if (SceneManager.GetActiveScene().name != "BootScene") SceneManager.LoadScene("BootScene");
        }

        public override void OnStopServer() { room = null; }

        private void BroadcastSnapshot()
        {
            if (room == null) return;
            foreach (var conn in NetworkServer.connections.Values)
                if (conn != null && conn.isAuthenticated) SendSnapshot(conn);
        }

        private void SendSnapshot(NetworkConnectionToClient conn)
        {
            if (room == null) return;
            int slot = room.SlotOf(conn.connectionId);
            if (slot < 0) return;
            byte mask = 0;
            for (int i = 0; i < RoomState.Capacity; i++) if (room.IsOccupied(i)) mask |= (byte)(1 << i);
            conn.Send(new RoomSnapshot { occupiedMask = mask, ownSlot = (byte)slot, fillNpcs = room.FillEmptyWithNpcs });
        }
    }
}
