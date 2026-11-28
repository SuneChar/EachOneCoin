using EachOneCoin.Gameplay;
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
        public byte appearance0, appearance1, appearance2, appearance3;
        public byte AppearanceAt(int slot) => slot == 0 ? appearance0 : slot == 1 ? appearance1 : slot == 2 ? appearance2 : appearance3;
    }

    public struct RoomSnapshotRequest : NetworkMessage { }

    public sealed class LanNetworkManager : NetworkManager
    {
        private RoomState room;
        public RoomSnapshot Snapshot { get; private set; }
        public bool HasSnapshot { get; private set; }
        public string Status { get; private set; } = "未连接";
        public int AppearanceCatalogCount { get; private set; } = RoomState.DefaultAppearanceCount;
        public byte LocalPreferredAppearanceIndex { get; private set; }
        private readonly byte[] matchAppearances = { 0, 1, 2, 3 };
        private readonly byte[] humanRequestedAppearances = new byte[RoomState.Capacity];
        public byte MatchAppearanceAt(int slot) => slot >= 0 && slot < RoomState.Capacity ? matchAppearances[slot] : (byte)0;
        public byte ServerAppearance(int slot) => room != null ? room.AppearanceAt(slot) : RoomState.UnspecifiedAppearance;
        private bool leaving;
        private float connectDeadline;
        private float oldConnectionRetryDeadline;
        private readonly HashSet<int> movementReady = new HashSet<int>();
        private readonly MatchRulesHandshake pendingRules = new MatchRulesHandshake();
        private readonly HashSet<int> reconnecting = new HashSet<int>();
        private readonly HashSet<int> voluntaryLeaving = new HashSet<int>();
        private readonly HashSet<int> returnedConnections = new HashSet<int>();
        private bool returningToRoom;
        public bool MatchFinished { get; private set; }
        public byte ReturnedMask { get; private set; }
        public bool LocalReturned => HasSnapshot && (ReturnedMask & (1 << LocalSlot)) != 0;
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

        public void ConfigureAppearanceCatalog(int catalogCount)
        {
            if (catalogCount <= 0) return;
            AppearanceCatalogCount = catalogCount;
            if (room != null) room.ConfigureAppearances(AppearanceCatalogCount, true);
        }

        public void SetLocalPreferredAppearance(byte index)
        {
            LocalPreferredAppearanceIndex = index < AppearanceCatalogCount ? index : (byte)0;
            if (NetworkClient.isConnected && !MatchStarted) RequestSelectAppearance(LocalPreferredAppearanceIndex);
        }

        public void RequestSelectAppearance(byte index)
        {
            if (!NetworkClient.isConnected || MatchStarted) return;
            byte sanitized = index < AppearanceCatalogCount ? index : (byte)0;
            NetworkClient.Send(new SelectAppearanceRequest { appearanceIndex = sanitized });
        }

        public byte AuthenticateConnection(NetworkConnectionToClient conn, bool resume, string token, byte requestedAppearance, out byte slot, out string issued, out bool resumed)
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
                id = room.AddWithAppearance(conn.connectionId, requestedAppearance);
                if (id < 0) return 1;
            }
            slot = (byte)id; issued = room.TokenAt(id); return 0;
        }

        public void OnAuthenticationAccepted(byte slot, string token, bool resumed, byte occupied, bool fillNpcs, float secondsRemaining,
            byte appearance0, byte appearance1, byte appearance2, byte appearance3)
        {
            Snapshot = new RoomSnapshot
            {
                ownSlot = slot,
                occupiedMask = occupied,
                fillNpcs = fillNpcs,
                appearance0 = appearance0,
                appearance1 = appearance1,
                appearance2 = appearance2,
                appearance3 = appearance3
            };
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
            movementReady.Clear(); pendingRules.Clear(); reconnecting.Clear();
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
            room.ConfigureAppearances(AppearanceCatalogCount, true);
            StartCoroutine(ExpireReservations());
            NetworkServer.RegisterHandler<SelectAppearanceRequest>((conn, msg) =>
            {
                if (MatchStarted || room == null || ServerSlot(conn.connectionId) < 0) return;
                if (room.SetAppearance(conn.connectionId, msg.appearanceIndex)) BroadcastSnapshot();
            });
            NetworkServer.RegisterHandler<VoluntaryLeaveRequest>((conn, _) =>
            {
                int slot = ServerSlot(conn.connectionId);
                if (slot < 0 || slot == 0) return;
                voluntaryLeaving.Add(conn.connectionId);
                movementReady.Remove(conn.connectionId); pendingRules.Remove(conn.connectionId); reconnecting.Remove(conn.connectionId);
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null) controller.RemovePlayer(slot);
                room.Remove(conn.connectionId);
                conn.Send(new VoluntaryLeaveAcknowledged());
                BroadcastSnapshot();
            });
            NetworkServer.RegisterHandler<ReturnRoomRequest>((conn, _) =>
            {
                if (!MatchFinished || ServerSlot(conn.connectionId) < 0) return;
                returnedConnections.Add(conn.connectionId);
                BroadcastReturnState();
                TryReturnEveryone();
            });
            NetworkServer.RegisterHandler<RoomSnapshotRequest>((conn, _) => SendSnapshot(conn));
            NetworkServer.RegisterHandler<MovementReady>(OnMovementReady);
            NetworkServer.RegisterHandler<MatchRulesAcknowledged>(OnMatchRulesAcknowledged);
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
            NetworkClient.RegisterHandler<ResumeComplete>(_ =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller == null || !controller.HasAuthoritativeRules) { RejectLocalRules(); return; }
                ClientResuming = false; IsReconnectAttempt = false; Status = "已恢复对局";
            });
            NetworkClient.RegisterHandler<ResumeRejected>(_ => OnAuthenticationRejected(2));
            NetworkClient.RegisterHandler<ReturnRoomState>(message => ReturnedMask = message.returnedMask);
            NetworkClient.RegisterHandler<RoomSnapshot>(message => { Snapshot = message; HasSnapshot = true; });
            NetworkClient.RegisterHandler<MatchRulesMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller == null || !controller.ReceiveAuthoritativeRules(message)) { RejectLocalRules(); return; }
                NetworkClient.Send(new MatchRulesAcknowledged { protocolVersion = message.protocolVersion, coinsToWin = message.coinsToWin, durationSeconds = message.durationSeconds });
            });
            NetworkClient.RegisterHandler<MatchRulesRejected>(_ => RejectLocalRules());
            NetworkClient.RegisterHandler<MovementBegin>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller == null || !controller.HasAuthoritativeRules) { RejectLocalRules(); return; }
                MatchOccupied = message.occupied; MatchFillNpcs = message.fillNpcs; MovementRunning = true;
                matchAppearances[0] = message.appearance0;
                matchAppearances[1] = message.appearance1;
                matchAppearances[2] = message.appearance2;
                matchAppearances[3] = message.appearance3;
                controller.SetAuthoritativeAppearances(message.appearance0, message.appearance1, message.appearance2, message.appearance3);
                controller.Begin(message.occupied, message.fillNpcs);
            });
            NetworkClient.RegisterHandler<MatchStateMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null && controller.HasAuthoritativeRules) controller.ReceiveMatchState(message);
            });
            NetworkClient.RegisterHandler<CombatStateMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null && controller.HasAuthoritativeRules) controller.ReceiveCombatState(message);
            });
            NetworkClient.RegisterHandler<AttackEvent>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null && controller.HasAuthoritativeRules) controller.ReceiveAttackEvent(message);
            });
            NetworkClient.RegisterHandler<DamageEvent>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null && controller.HasAuthoritativeRules) controller.ReceiveDamageEvent(message);
            });
            NetworkClient.RegisterHandler<MatchFeedEventMessage>(OnFeedEvent);
            NetworkClient.RegisterHandler<MineStatusMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null && controller.HasAuthoritativeRules) controller.ReceiveMineStatus(message);
            });
            NetworkClient.RegisterHandler<CoinSnapshotMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null && controller.HasAuthoritativeRules) controller.ReceiveCoinSnapshot(message);
            });
            NetworkClient.RegisterHandler<CoinChangeMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null && controller.HasAuthoritativeRules) controller.ReceiveCoinChange(message);
            });
            NetworkClient.RegisterHandler<GridSnapshotMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null && controller.HasAuthoritativeRules) controller.ReceiveGridSnapshot(message);
            });
            NetworkClient.RegisterHandler<GridChangeMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null && controller.HasAuthoritativeRules) controller.ReceiveGridChange(message);
            });
            NetworkClient.RegisterHandler<ObstacleStateMessage>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null && controller.HasAuthoritativeRules) controller.ReceiveObstacleState(message);
            });
            NetworkClient.RegisterHandler<MovementSnapshot>(message =>
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null && controller.HasAuthoritativeRules) controller.ReceiveSnapshot(message);
            });
        }

        private void OnFeedEvent(MatchFeedEventMessage message)
        {
            var controller = FindObjectOfType<NetworkMatchController>();
            if (controller != null && controller.HasAuthoritativeRules) controller.ReceiveFeedEvent(message);
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
            returnedConnections.Remove(conn.connectionId);
            movementReady.Remove(conn.connectionId); pendingRules.Remove(conn.connectionId); reconnecting.Remove(conn.connectionId);
            var controller = FindObjectOfType<NetworkMatchController>();
            if (slot >= 0)
            {
                if (!voluntary && MatchStarted && !MatchFinished && slot != 0 && room.Reserve(conn.connectionId, Time.realtimeSinceStartupAsDouble))
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
            if (MatchFinished) { BroadcastReturnState(); TryReturnEveryone(); }
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

        public void MarkMatchFinished()
        {
            if (!NetworkServer.active || MatchFinished) return;
            MatchFinished = true;
            returnedConnections.Clear();
            BroadcastReturnState();
        }

        public void RequestReturnToRoom()
        {
            if (NetworkClient.isConnected && !LocalReturned) NetworkClient.Send(new ReturnRoomRequest());
        }

        private void BroadcastReturnState()
        {
            byte mask = 0;
            foreach (int id in returnedConnections)
            {
                int slot = ServerSlot(id);
                if (slot >= 0) mask |= (byte)(1 << slot);
            }
            ReturnedMask = mask;
            foreach (var conn in NetworkServer.connections.Values)
                if (conn != null && conn.isAuthenticated) conn.Send(new ReturnRoomState { returnedMask = mask });
        }

        private void TryReturnEveryone()
        {
            if (!NetworkServer.active || !MatchFinished || returningToRoom) return;
            foreach (var conn in NetworkServer.connections.Values)
                if (conn != null && conn.isAuthenticated && ServerSlot(conn.connectionId) >= 0 && !returnedConnections.Contains(conn.connectionId)) return;
            returningToRoom = true;
            StartCoroutine(ReturnEveryoneToRoom());
        }

        private System.Collections.IEnumerator ReturnEveryoneToRoom()
        {
            yield return new WaitForSecondsRealtime(.2f);
            if (!NetworkServer.active || room == null) { returningToRoom = false; yield break; }
            for (int slot = 1; slot < RoomState.Capacity; slot++) if (room.IsReserved(slot)) room.Release(slot);
            MatchStarted = false; MovementRunning = false; MatchFinished = false;
            MatchOccupied = 0; MatchFillNpcs = false; ReturnedMask = 0;
            returnedConnections.Clear(); movementReady.Clear(); pendingRules.Clear(); reconnecting.Clear(); returningToRoom = false;
            ServerChangeScene("BootScene");
            BroadcastSnapshot();
        }

        public void StartMovementTest()
        {
            if (!NetworkServer.active || room == null || MatchStarted) return;
            foreach (var conn in NetworkServer.connections.Values)
                if (!conn.isAuthenticated) { Status = "有连接正在验证，请稍后开始"; return; }
            MatchStarted = true;
            MovementRunning = false;
            movementReady.Clear();
            pendingRules.Clear();
            ServerChangeScene("ArenaScene");
            StartCoroutine(LoadingTimeout());
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            if (SceneManager.GetActiveScene().name == "ArenaScene") StartCoroutine(SendReadyWhenInitialized());
            else if (SceneManager.GetActiveScene().name == "BootScene")
            {
                MatchStarted = false; MovementRunning = false; MatchFinished = false; ReturnedMask = 0;
                if (NetworkClient.isConnected) NetworkClient.Send(new RoomSnapshotRequest());
            }
        }

        private System.Collections.IEnumerator SendReadyWhenInitialized()
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (FindObjectOfType<NetworkMatchController>() == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (!NetworkClient.isConnected || FindObjectOfType<NetworkMatchController>() == null)
            {
                if (NetworkClient.active) ExitMovementTest();
                yield break;
            }

            NetworkClient.Send(new MovementReady { protocolVersion = MatchRulesProtocol.Version });
            deadline = Time.realtimeSinceStartup + 15f;
            while (NetworkClient.isConnected && Time.realtimeSinceStartup < deadline)
            {
                var controller = FindObjectOfType<NetworkMatchController>();
                if (controller != null && controller.HasAuthoritativeRules) yield break;
                yield return null;
            }
            if (NetworkClient.active && !leaving) RejectLocalRules();
        }

        private void RejectLocalRules()
        {
            Debug.LogWarning("无法确认房主规则或联机协议版本不匹配，已退出对局。");
            ExitMovementTest();
            Status = "联机规则无效或版本不匹配";
        }

        private void RejectRules(NetworkConnectionToClient connection)
        {
            if (connection == null) return;
            pendingRules.Remove(connection.connectionId);
            if (NetworkServer.active && connection.connectionId == 0)
            {
                Debug.LogWarning("房主对局配置无效或超出联机范围，已取消对局。");
                ExitMovementTest();
                Status = "房主对局规则无效";
                return;
            }
            voluntaryLeaving.Add(connection.connectionId);
            connection.Send(new MatchRulesRejected());
            StartCoroutine(DisconnectRejectedResume(connection));
        }

        private void OnMovementReady(NetworkConnectionToClient conn, MovementReady message)
        {
            int slot = ServerSlot(conn.connectionId);
            if (!MatchStarted || slot < 0 || (MovementRunning && !reconnecting.Contains(conn.connectionId))) return;
            if (message.protocolVersion != MatchRulesProtocol.Version) { RejectRules(conn); return; }
            if (reconnecting.Contains(conn.connectionId) && room.IsReclaimExpired(slot, Time.realtimeSinceStartupAsDouble))
            {
                RejectResume(conn);
                return;
            }
            var controller = FindObjectOfType<NetworkMatchController>();
            if (controller == null || !controller.TryGetAuthoritativeRules(out var rules))
            {
                Debug.LogWarning("房主对局规则不可用，已取消对局。");
                ExitMovementTest();
                Status = "房主对局规则无效";
                return;
            }
            if (!pendingRules.Offer(conn.connectionId, rules)) { RejectRules(conn); return; }
            conn.Send(rules);
        }

        private void OnMatchRulesAcknowledged(NetworkConnectionToClient conn, MatchRulesAcknowledged message)
        {
            if (!MatchStarted || !pendingRules.IsPending(conn.connectionId)) return;
            if (!pendingRules.Confirm(conn.connectionId, message)) { RejectRules(conn); return; }
            CompleteMovementReady(conn);
        }

        private void CompleteMovementReady(NetworkConnectionToClient conn)
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
                conn.Send(new MovementBegin
                {
                    occupied = MatchOccupied,
                    fillNpcs = MatchFillNpcs,
                    appearance0 = matchAppearances[0],
                    appearance1 = matchAppearances[1],
                    appearance2 = matchAppearances[2],
                    appearance3 = matchAppearances[3]
                });
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
                byte visibleMask = MatchFillNpcs ? (byte)15 : mask;
                int catalogCount = controller.AppearanceCatalogCount;
                for (int i = 0; i < RoomState.Capacity; i++) humanRequestedAppearances[i] = room.AppearanceAt(i);
                CosmeticAssignmentPolicy.ResolveMatchAppearances(catalogCount, mask, visibleMask, humanRequestedAppearances, matchAppearances, room.AllowDuplicateAppearances);
                controller.SetAuthoritativeAppearances(matchAppearances[0], matchAppearances[1], matchAppearances[2], matchAppearances[3]);
                controller.Begin(mask, MatchFillNpcs);
                var beginMessage = new MovementBegin
                {
                    occupied = mask,
                    fillNpcs = MatchFillNpcs,
                    appearance0 = matchAppearances[0],
                    appearance1 = matchAppearances[1],
                    appearance2 = matchAppearances[2],
                    appearance3 = matchAppearances[3]
                };
                foreach (var conn in new List<NetworkConnectionToClient>(NetworkServer.connections.Values))
                {
                    if (!conn.isAuthenticated || !movementReady.Contains(conn.connectionId)) continue;
                    int slot = ServerSlot(conn.connectionId);
                    if (reconnecting.Contains(conn.connectionId) && room.IsReclaimExpired(slot, Time.realtimeSinceStartupAsDouble)) { RejectResume(conn); continue; }
                    controller.SendGridSnapshot(conn); controller.SendCoinSnapshot(conn); controller.SendCombatState(conn); controller.SendMatchState(conn);
                    conn.Send(beginMessage);
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

        public override void OnStopServer() { room = null; pendingRules.Clear(); reconnecting.Clear(); voluntaryLeaving.Clear(); returnedConnections.Clear(); MatchFinished = false; returningToRoom = false; ReturnedMask = 0; }

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
            conn.Send(new RoomSnapshot
            {
                occupiedMask = mask,
                ownSlot = (byte)slot,
                fillNpcs = room.FillEmptyWithNpcs,
                appearance0 = room.AppearanceAt(0),
                appearance1 = room.AppearanceAt(1),
                appearance2 = room.AppearanceAt(2),
                appearance3 = room.AppearanceAt(3)
            });
        }
    }
}
