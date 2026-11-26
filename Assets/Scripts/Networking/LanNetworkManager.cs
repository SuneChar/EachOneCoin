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
        private float oldConnectionRetryDeadline;
        private readonly HashSet<int> movementReady = new HashSet<int>();
        private readonly HashSet<int> reconnecting = new HashSet<int>();
        private readonly HashSet<int> voluntaryLeaving = new HashSet<int>();
        private bool leaveAcknowledged;
        private bool recoveryStorageAvailable = true;
        private string savedAddress;
        public bool IsReconnectAttempt { get; private set; }
        public string ReconnectToken { get; private set; }
        public bool HasRecoverySession => !string.IsNullOrEmpty(ReconnectToken) && !string.IsNullOrWhiteSpace(savedAddress);
        public bool ClientResuming { get; private set; }
        private float clientResumeDeadline;
        public float ClientResumeSeconds => ClientResuming ? Mathf.Max(0f, clientResumeDeadline - Time.realtimeSinceStartup) : 0f;
        public float ServerRemaining(int slot) => room != null ? (float)room.RemainingSeconds(slot, Time.realtimeSinceStartupAsDouble) : 0f;
        public bool IsGameplayReady(NetworkConnectionToClient conn) => conn != null && conn.isAuthenticated && movementReady.Contains(conn.connectionId) && !reconnecting.Contains(conn.connectionId);
        public bool IsSeatConnected(int slot) => room != null && room.IsConnected(slot);
        public bool CurrentFillNpcs => room != null && room.FillEmptyWithNpcs;
        public byte HumanSeatMask
        {
            get { if (MatchStarted && MovementRunning) return MatchOccupied; byte mask = 0; if (room != null) for (int i = 0; i < RoomState.Capacity; i++) if (room.IsOccupied(i)) mask |= (byte)(1 << i); return mask; }
        }

        public override void Awake()
        {
            base.Awake();
            if (ReconnectSessionStore.TryLoad(out var saved)) { savedAddress = saved.address; ReconnectToken = saved.token; Status = "发现可尝试恢复的对局"; }
        }
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
            IsReconnectAttempt = false; ClientResuming = false;
            Status = "正在创建房间…";
            StartHost();
        }

        public void JoinRoom(string address)
        {
            if (NetworkClient.active || NetworkServer.active) return;
            if (string.IsNullOrWhiteSpace(address)) { Status = "请输入房主 IP 地址"; return; }
            leaving = false;
            IsReconnectAttempt = false;
            ClientResuming = false;
            networkAddress = address.Trim();
            Status = "正在连接…";
            connectDeadline = Time.realtimeSinceStartup + 12f;
            StartClient();
            StartCoroutine(CheckTimeout());
        }

        public void Reconnect()
        {
            if (NetworkClient.active || NetworkServer.active || !HasRecoverySession) return;
            leaving = false; IsReconnectAttempt = true; ClientResuming = false;
            networkAddress = savedAddress;
            Status = "正在恢复原席位…";
            connectDeadline = Time.realtimeSinceStartup + 12f;
            StartClient(); StartCoroutine(CheckTimeout());
        }

        public byte AuthenticateConnection(NetworkConnectionToClient conn, bool resume, string token, out byte slot, out string issued, out bool resumed)
        {
            slot = 0; issued = null; resumed = false;
            if (room == null || leaving) return 1;
            int id;
            if (resume)
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (!MatchStarted || (controller != null && controller.CurrentMatchStatus == MatchStatus.Finished)) return 2;
                id = room.Reclaim(conn.connectionId, token, Time.realtimeSinceStartupAsDouble);
                if (id < 0) return room.IsTokenConnected(token) ? (byte)3 : (byte)2;
                resumed = true; reconnecting.Add(conn.connectionId);
            }
            else
            {
                if (MatchStarted) return 1;
                id = room.Add(conn.connectionId);
                if (id < 0) return 1;
            }
            slot = (byte)id; issued = room.TokenAt(id); return 0;
        }

        public void OnAuthenticationAccepted(byte slot, string token, bool resumed, byte occupied, bool fillNpcs, float secondsRemaining)
        {
            Snapshot = new RoomSnapshot { ownSlot = slot, occupiedMask = occupied, fillNpcs = fillNpcs };
            HasSnapshot = true; ClientResuming = resumed;
            oldConnectionRetryDeadline = 0f;
            clientResumeDeadline = Time.realtimeSinceStartup + (resumed && !float.IsNaN(secondsRemaining) && !float.IsInfinity(secondsRemaining) ? Mathf.Clamp(secondsRemaining, 0f, (float)RoomState.ReconnectSeconds) : 0f);
            if (!NetworkServer.active)
            {
                savedAddress = networkAddress; ReconnectToken = token;
                recoveryStorageAvailable = ReconnectSessionStore.Save(savedAddress, token);
                if (!recoveryStorageAvailable) Status = "已连接，但无法保存重启恢复信息";
            }
        }

        public void OnAuthenticationRejected(byte code)
        {
            if (code == 3)
            {
                Status = "旧连接尚未释放，正在重试…";
                if (oldConnectionRetryDeadline <= 0f) oldConnectionRetryDeadline = Time.realtimeSinceStartup + (float)RoomState.ReconnectSeconds;
                StartCoroutine(RetryAfterOldConnection());
                return;
            }
            Status = code == 2 ? "重连已失效、超时或对局已结束" : "房间已满或正在对局";
            if (code == 2) ClearRecoverySession();
        }

        private System.Collections.IEnumerator RetryAfterOldConnection()
        {
            yield return new WaitForSecondsRealtime(1f);
            while (NetworkClient.active && !leaving && Time.realtimeSinceStartup < oldConnectionRetryDeadline) yield return null;
            if (leaving || !HasRecoverySession) yield break;
            if (Time.realtimeSinceStartup >= oldConnectionRetryDeadline) { Status = "旧连接未及时释放，请重新加入房间"; yield break; }
            if (!NetworkClient.active && !NetworkServer.active) Reconnect();
        }

        public void ClearRecoverySession()
        {
            savedAddress = null; ReconnectToken = null; IsReconnectAttempt = false; ClientResuming = false; oldConnectionRetryDeadline = 0f;
            ReconnectSessionStore.Clear();
        }

        public void LeaveRoom()
        {
            if (leaving) return;
            leaving = true;
            ClearRecoverySession();
            if (NetworkServer.active && NetworkClient.active)
            {
                foreach (var conn in NetworkServer.connections.Values)
                    if (conn.connectionId != 0 && conn.isAuthenticated) conn.Send(new HostClosing());
                StartCoroutine(FinishHostLeave());
                return;
            }
            if (!NetworkServer.active && NetworkClient.isConnected)
            {
                leaveAcknowledged = false;
                NetworkClient.Send(new VoluntaryLeaveRequest());
                StartCoroutine(FinishVoluntaryLeave());
                return;
            }
            FinishLeave();
        }

        private System.Collections.IEnumerator FinishHostLeave()
        {
            yield return new WaitForSecondsRealtime(.25f);
            FinishLeave();
        }

        private System.Collections.IEnumerator FinishVoluntaryLeave()
        {
            float deadline = Time.realtimeSinceStartup + 1f;
            while (NetworkClient.active && !leaveAcknowledged && Time.realtimeSinceStartup < deadline) yield return null;
            FinishLeave();
        }

        private void FinishLeave()
        {
            if (NetworkServer.active && NetworkClient.active) StopHost();
            else if (NetworkClient.active) StopClient();
            else if (NetworkServer.active) StopServer();
            HasSnapshot = false;
            MatchStarted = false;
            MovementRunning = false;
            movementReady.Clear(); reconnecting.Clear();
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
            StartCoroutine(ExpireReservations());
            NetworkServer.RegisterHandler<VoluntaryLeaveRequest>((conn, _) =>
            {
                int slot = ServerSlot(conn.connectionId);
                if (slot < 0 || slot == 0) return;
                voluntaryLeaving.Add(conn.connectionId);
                movementReady.Remove(conn.connectionId); reconnecting.Remove(conn.connectionId);
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.RemovePlayer(slot);
                room.Remove(conn.connectionId);
                conn.Send(new VoluntaryLeaveAcknowledged());
                BroadcastSnapshot();
            });
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
            NetworkServer.RegisterHandler<AttackRequest>((conn, msg) =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (MovementRunning && IsGameplayReady(conn) && controller != null) controller.ReceiveAttack(ServerSlot(conn.connectionId), msg);
            });
            NetworkServer.RegisterHandler<MovementInput>((conn, msg) =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (MovementRunning && IsGameplayReady(conn) && controller != null) controller.ReceiveInput(ServerSlot(conn.connectionId), msg.direction, msg.sprint);
            });
        }

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<HostClosing>(_ => { ClearRecoverySession(); Status = "房主已结束房间"; });
            NetworkClient.RegisterHandler<VoluntaryLeaveAcknowledged>(_ => leaveAcknowledged = true);
            NetworkClient.RegisterHandler<ResumeComplete>(_ => { ClientResuming = false; IsReconnectAttempt = false; Status = "已恢复对局"; });
            NetworkClient.RegisterHandler<ResumeRejected>(_ => OnAuthenticationRejected(2));
            NetworkClient.RegisterHandler<RoomSnapshot>(message => { Snapshot = message; HasSnapshot = true; });
            NetworkClient.RegisterHandler<MovementBegin>(message =>
            {
                MatchOccupied = message.occupied; MatchFillNpcs = message.fillNpcs; MovementRunning = true;
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.Begin(message.occupied, message.fillNpcs);
            });
            NetworkClient.RegisterHandler<MatchStateMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.ReceiveMatchState(message);
            });
            NetworkClient.RegisterHandler<CombatStateMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.ReceiveCombatState(message);
            });
            NetworkClient.RegisterHandler<AttackEvent>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.ReceiveAttackEvent(message);
            });
            NetworkClient.RegisterHandler<DamageEvent>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.ReceiveDamageEvent(message);
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
            NetworkClient.RegisterHandler<ObstacleStateMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.ReceiveObstacleState(message);
            });
            NetworkClient.RegisterHandler<MovementSnapshot>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.ReceiveSnapshot(message);
            });
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            if (room == null || room.SlotOf(conn.connectionId) < 0) { conn.Disconnect(); return; }
            BroadcastSnapshot();
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            int slot = ServerSlot(conn.connectionId);
            bool voluntary = voluntaryLeaving.Remove(conn.connectionId);
            movementReady.Remove(conn.connectionId); reconnecting.Remove(conn.connectionId);
            var controller = FindObjectOfType<NetworkMatchController>();
            if (slot >= 0)
            {
                if (!voluntary && MatchStarted && slot != 0 && room.Reserve(conn.connectionId, Time.realtimeSinceStartupAsDouble))
                {
                    if (controller != null) controller.SuspendPlayer(slot);
                }
                else
                {
                    room.Remove(conn.connectionId);
                    if (controller != null) controller.RemovePlayer(slot);
                }
            }
            base.OnServerDisconnect(conn);
            if (MatchStarted && !MovementRunning) TryBeginMovement();
            BroadcastSnapshot();
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Status = ClientResuming ? "已连接，正在恢复对局…" : NetworkServer.active ? "房间已创建" : "已连接房间";
            if (!NetworkServer.active && !recoveryStorageAvailable) Status += "（无法保存重启恢复信息）";
            NetworkClient.Send(new RoomSnapshotRequest());
        }

        public override void OnClientDisconnect()
        {
            HasSnapshot = false;
            if (!leaving)
            {
                if (!Status.StartsWith("重连已失效") && !Status.StartsWith("房间已满") && !Status.StartsWith("房主已结束") && !Status.StartsWith("旧连接尚未释放"))
                    Status = HasRecoverySession ? "连接已断开，可尝试重连" : "连接已断开，房间可能已关闭或已满";
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
            foreach (var conn in NetworkServer.connections.Values)
                if (!conn.isAuthenticated) { Status = "有连接正在验证，请稍后开始"; return; }
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
            int slot = ServerSlot(conn.connectionId);
            if (!MatchStarted || slot < 0) return;
            if (reconnecting.Contains(conn.connectionId) && room.IsReclaimExpired(slot, Time.realtimeSinceStartupAsDouble))
            {
                RejectResume(conn);
                return;
            }
            if (MovementRunning)
            {
                if (!reconnecting.Contains(conn.connectionId)) return;
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller == null || controller.CurrentMatchStatus == MatchStatus.Finished) { RejectResume(conn); return; }
                conn.Send(new MovementBegin { occupied = MatchOccupied, fillNpcs = MatchFillNpcs });
                controller.SendGridSnapshot(conn);
                controller.SendCoinSnapshot(conn);
                controller.SendCombatState(conn);
                controller.SendMatchState(conn);
                controller.SendMineStatus(conn);
                controller.SendMovementState(conn);
                controller.SendObstacleState(conn);
                conn.Send(new ResumeComplete());
                room.CommitReclaim(slot);
                reconnecting.Remove(conn.connectionId); movementReady.Add(conn.connectionId);
                controller.RestorePlayer(slot);
                return;
            }
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
            if (controller != null)
            {
                controller.Begin(mask, MatchFillNpcs);
                foreach (var conn in new List<NetworkConnectionToClient>(NetworkServer.connections.Values))
                {
                    if (!conn.isAuthenticated || !movementReady.Contains(conn.connectionId)) continue;
                    int slot = ServerSlot(conn.connectionId);
                    if (reconnecting.Contains(conn.connectionId) && room.IsReclaimExpired(slot, Time.realtimeSinceStartupAsDouble)) { RejectResume(conn); continue; }
                    controller.SendGridSnapshot(conn); controller.SendCoinSnapshot(conn); controller.SendCombatState(conn); controller.SendMatchState(conn);
                    conn.Send(new MovementBegin { occupied = mask, fillNpcs = MatchFillNpcs });
                    controller.SendObstacleState(conn);
                    if (reconnecting.Remove(conn.connectionId)) { room.CommitReclaim(slot); conn.Send(new ResumeComplete()); }
                }
            }
        }

        private void RejectResume(NetworkConnectionToClient connection)
        {
            if (connection == null) return;
            connection.Send(new ResumeRejected());
            StartCoroutine(DisconnectRejectedResume(connection));
        }

        private System.Collections.IEnumerator DisconnectRejectedResume(NetworkConnectionToClient connection)
        {
            yield return new WaitForSecondsRealtime(.3f);
            if (NetworkServer.active && connection != null && NetworkServer.connections.TryGetValue(connection.connectionId, out var current) && ReferenceEquals(current, connection)) connection.Disconnect();
        }

        private System.Collections.IEnumerator ExpireReservations()
        {
            var wait = new WaitForSecondsRealtime(.25f);
            while (NetworkServer.active && room != null)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (reconnecting.Count > 0)
                {
                    foreach (int id in new List<int>(reconnecting))
                    {
                        int pendingSlot = ServerSlot(id);
                        if (pendingSlot < 0 || !room.IsReclaimExpired(pendingSlot, now)) continue;
                        room.Release(pendingSlot); reconnecting.Remove(id); movementReady.Remove(id);
                        var pendingController = FindObjectOfType<NetworkMatchController>();
                        if (pendingController != null) pendingController.RemovePlayer(pendingSlot);
                        if (NetworkServer.connections.TryGetValue(id, out var pendingConnection)) RejectResume(pendingConnection);
                    }
                }
                for (int slot = 1; slot < RoomState.Capacity; slot++)
                {
                    if (!room.IsExpired(slot, now)) continue;
                    room.Release(slot);
                    var controller = FindObjectOfType<NetworkMatchController>();
                    if (controller != null) controller.RemovePlayer(slot);
                    BroadcastSnapshot();
                }
                yield return wait;
            }
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
            yield return null;
            if (!leaving && HasRecoverySession && !NetworkClient.active && !NetworkServer.active) Reconnect();
        }

        public override void OnStopServer() { room = null; reconnecting.Clear(); voluntaryLeaving.Clear(); }

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
