using UnityEngine;
using UnityEngine.Serialization;

namespace EachOneCoin.Gameplay
{
    public sealed class MatchCoordinator : MonoBehaviour
    {
        [SerializeField] private ArenaGridView grid;
        [SerializeField] private ArenaMapDefinition mapDefinition;
        [SerializeField] private ArenaCharacter playerTemplate;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private CoinSpawner coins;
        [SerializeField] private CombatController combat;
        [SerializeField] private NpcController npcs;
        [SerializeField] private LocalPlayerInput localInput;
        [SerializeField] private GameAudio gameAudio;
        [FormerlySerializedAs("singlePlayerConfig")]
        [SerializeField, Tooltip("单机及联机房主使用；客户端接收房主规则。")]
        private ArenaMatchConfig matchConfig;
        [SerializeField] private CharacterAppearanceCatalog appearanceCatalog;
        [SerializeField, Min(0.1f)] private float moveSpeed = 10f;
        [SerializeField, Min(0.1f)] private float movementAcceleration = 35f;
        [SerializeField, Min(0.1f)] private float movementDeceleration = 35f;
        [SerializeField, Min(0.01f)] private float ownRegionSpeedMultiplier = 1.2f;
        [SerializeField, Min(0.01f)] private float enemyRegionSpeedMultiplier = 0.8f;
        [SerializeField, Min(0.1f)] private float maxStamina = 100f;
        [SerializeField, Min(0f)] private float sprintStaminaPerSecond = 25f;
        [SerializeField, Min(0f)] private float staminaRecoveryPerSecond = 20f;
        [SerializeField, Min(1f)] private float sprintSpeedMultiplier = 1.5f;
        [SerializeField, Min(1)] private int initialHealth = 3;
        [SerializeField, Min(0f)] private float obstacleInvulnerabilitySeconds = 1f;
        [SerializeField, Min(0f)] private float obstacleKnockbackDistance = 0.6f;
        [SerializeField, Min(0.01f)] private float obstacleKnockbackSeconds = 0.18f;
        [SerializeField] private bool hideHealthBarOnViewTarget = false;
        private bool isCustomSinglePlayerMap;
        private readonly Collider[] overlap = new Collider[64];
        public MatchModel Model { get; private set; }
        public ArenaGridView Grid => grid;
        public ArenaMapDefinition MapDefinition => mapDefinition;
        public CoinSpawner Coins => coins;
        public CombatController Combat => combat;
        public NpcController Npcs => npcs;
        public ArenaCharacter[] Characters { get; private set; }
        public TMPro.TMP_FontAsset NameplateFont { get; private set; }
        public GridPosition[] Spawns { get; private set; }
        public float MoveSpeed => moveSpeed;
        public float MovementAcceleration => Mathf.Max(0.1f, movementAcceleration);
        public float MovementDeceleration => Mathf.Max(0.1f, movementDeceleration);
        public float OwnRegionSpeedMultiplier => Mathf.Max(0.01f, ownRegionSpeedMultiplier);
        public float EnemyRegionSpeedMultiplier => Mathf.Max(0.01f, enemyRegionSpeedMultiplier);
        public StaminaModel Stamina { get; private set; }
        public StaminaModel StaminaAt(int id) => session.StaminaAt(id);
        public float SprintStaminaPerSecond => Mathf.Max(0f, sprintStaminaPerSecond);
        public float StaminaRecoveryPerSecond => Mathf.Max(0f, staminaRecoveryPerSecond);
        public float SprintSpeedMultiplier => Mathf.Max(1f, sprintSpeedMultiplier);
        public float ObstacleInvulnerabilitySeconds => Mathf.Max(0f, obstacleInvulnerabilitySeconds);
        public float ObstacleKnockbackDistance => Mathf.Max(0f, obstacleKnockbackDistance);
        public float ObstacleKnockbackSeconds => Mathf.Max(0.01f, obstacleKnockbackSeconds);
        public int InitialHealth => Mathf.Max(1, initialHealth);
        public event System.Action<int> ObstacleHit;
        public event System.Action<int> AttackHit;
        public event System.Action<MatchFeedEvent> FeedEventRaised;
        public event System.Action<int> ParticipantAppearanceChanged;
        public CharacterAppearanceCatalog AppearanceCatalog => appearanceCatalog;
        public int AppearanceCatalogCount
        {
            get
            {
                if (appearanceCatalog != null && appearanceCatalog.Count > 0) return appearanceCatalog.Count;
                if (playerTemplate != null && playerTemplate.FallbackAppearanceCount > 0) return playerTemplate.FallbackAppearanceCount;
                if (Characters != null && Characters.Length > 0 && Characters[0] != null && Characters[0].FallbackAppearanceCount > 0)
                    return Characters[0].FallbackAppearanceCount;
                return 4;
            }
        }
        public bool IsPaused { get; private set; }
        public bool IsNetworkMovementTest { get; private set; }
        public bool IsReplay { get; private set; }
        public bool NetworkRulesValid { get; private set; } = true;
        public float InitialDurationSeconds { get; private set; } = MatchModel.DefaultDurationSeconds;
        public EachOneCoin.Networking.NetworkMatchController NetworkController { get; private set; }
        public int CurrentViewSlot { get; private set; }
        private readonly bool[] nearVictoryAnnounced = new bool[4];
        private readonly string[] participantNames = new string[4];
        private readonly TMPro.TMP_Text[] nameplateLabels = new TMPro.TMP_Text[4];
        private readonly int[] participantAppearanceIndices = { 0, 1, 2, 3 };
        private readonly CharacterAppearanceProfile[] participantAppearances = new CharacterAppearanceProfile[4];
        private readonly int[] lastOverheadHealth = { -1, -1, -1, -1 };
        private readonly int[] lastOverheadMaxHealth = { -1, -1, -1, -1 };
        private readonly int[] lastOverheadInitialMaxHealth = { -1, -1, -1, -1 };
        private float timeScaleBeforePause = 1f;
        private MatchSessionModel session;
        private MatchSystem matchSystem;
        private bool aiming;
        private bool aimUsesGamepad;
        private Vector3 aimDirection = Vector3.forward;
        public MatchSystem Rules
        {
            get
            {
                if (session == null || Model == null || !ReferenceEquals(session.Match, Model) || matchSystem == null)
                    throw new System.InvalidOperationException("The coordinator is not bound to the current match session.");
                return matchSystem;
            }
        }
        internal MovementTelemetry Telemetry { get; private set; }
        internal Replay.ReplayRecorder Recorder { get; private set; }

        public void BeginReplayRecording(bool network, System.Action<string> onSaved = null)
        {
            if (Recorder != null) return;
            if (isCustomSinglePlayerMap || !ArenaMapFingerprint.IsFixedProtocolCompatible(mapDefinition))
            {
                Debug.LogWarning("自定义地图暂不支持录像，已跳过录像录制。", this);
                return;
            }
            Recorder = gameObject.AddComponent<Replay.ReplayRecorder>();
            try { Recorder.Initialize(this, network, onSaved); }
            catch (System.Exception e) { Debug.LogWarning("录像初始化失败：" + e.Message); Destroy(Recorder); Recorder = null; }
        }

        public void Pause()
        {
            if (IsPaused || Model == null || Model.Status != MatchStatus.Playing) return;
            CancelAim();
            timeScaleBeforePause = Time.timeScale;
            IsPaused = true;
            Time.timeScale = 0f;
        }

        public void SuppressGamepadActionsUntilReleased()
        {
            if (localInput != null) localInput.SuppressGamepadActionsUntilReleased();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = timeScaleBeforePause;
        }

        private void OnDestroy()
        {
            if (coins != null) coins.MineCoinsSpawned -= OnMineCoinsSpawned;
            Resume();
            if (session != null) session.ClearMatch(Model);
        }

        private bool ResolveMatchRules(bool isNetwork, bool isHost, out int coinsToWin, out float durationSeconds)
        {
            coinsToWin = MatchModel.DefaultCoinsToWin;
            durationSeconds = MatchModel.DefaultDurationSeconds;
            if (isNetwork && !isHost) return true;
            if (matchConfig == null) return !isNetwork;
            if (matchConfig.TryGetRules(out int configuredCoins, out float configuredDuration))
            {
                var rules = new EachOneCoin.Networking.MatchRulesMessage
                {
                    protocolVersion = EachOneCoin.Networking.MatchRulesProtocol.Version,
                    coinsToWin = configuredCoins,
                    durationSeconds = configuredDuration,
                    mapFingerprint = isNetwork && mapDefinition != null ? ArenaMapFingerprint.Compute(mapDefinition) : null
                };
                if (isNetwork && !EachOneCoin.Networking.MatchRulesProtocol.IsValid(rules)) return false;
                coinsToWin = configuredCoins;
                durationSeconds = configuredDuration;
                return true;
            }
            if (isNetwork) return false;
            Debug.LogWarning("单机对局配置无效，已回退为 60 秒、10 枚金币。", matchConfig);
            return true;
        }

        public bool ApplyAuthoritativeRulesBeforeStart(int coinsToWin, float durationSeconds)
        {
            if (!IsNetworkMovementTest || Mirror.NetworkServer.active || Model == null ||
                Model.Status != MatchStatus.Waiting || coins == null) return false;
            if (!EachOneCoin.Networking.MatchRulesProtocol.IsValid(new EachOneCoin.Networking.MatchRulesMessage
                { protocolVersion = EachOneCoin.Networking.MatchRulesProtocol.Version, coinsToWin = coinsToWin, durationSeconds = durationSeconds,
                  mapFingerprint = mapDefinition != null ? ArenaMapFingerprint.Compute(mapDefinition) : null })) return false;
            if (!coins.ConfigureReplicaDurationBeforeStart(durationSeconds)) return false;
            if (!Rules.ConfigureRulesBeforeStart(coinsToWin, durationSeconds)) return false;
            InitialDurationSeconds = durationSeconds;
            return true;
        }

        private void Start()
        {
            var network = Mirror.NetworkManager.singleton as EachOneCoin.Networking.LanNetworkManager;
            var replay = Replay.ReplaySelection.Pending;
            Replay.ReplaySelection.Pending = null;
            IsReplay = replay != null;
            IsNetworkMovementTest = !IsReplay && network != null && Mirror.NetworkClient.active && network.HasSnapshot;
            if (!IsReplay && !IsNetworkMovementTest && ArenaMapSelection.Selected != null)
            {
                isCustomSinglePlayerMap = ArenaMapSelection.Selected != mapDefinition;
                mapDefinition = ArenaMapSelection.Selected;
            }
            if (IsNetworkMovementTest && !ArenaMapFingerprint.IsFixedProtocolCompatible(mapDefinition))
            {
                Debug.LogWarning("当前地图不支持联机，已取消进入对局。", this);
                enabled = false;
                network.RejectUnsupportedMap();
                return;
            }
            if (IsReplay && !Replay.ReplayStorage.MatchesMap(replay, mapDefinition, out string replayMapError))
            {
                Debug.LogWarning("无法播放录像：" + replayMapError, this);
                enabled = false;
                return;
            }
            if (mapDefinition != null && !ArenaMapValidator.TryValidate(mapDefinition, out var mapError))
            {
                Debug.LogError("竞技场地图无效或尺寸尚不受支持：" + mapError, mapDefinition);
                enabled = false;
                return;
            }
            if (mapDefinition != null)
            {
                if (mapDefinition.Width > SystemInfo.maxTextureSize || mapDefinition.Height > SystemInfo.maxTextureSize)
                {
                    Debug.LogError("地图尺寸超出当前设备小地图纹理限制。", mapDefinition);
                    enabled = false;
                    return;
                }
                try
                {
                    ArenaMapBuilder.BuildTiles(mapDefinition, gameObject.scene);
                    ArenaMapBuilder.BuildObstacles(mapDefinition, gameObject.scene);
                    ArenaMapBuilder.ResizeBoundary(mapDefinition, gameObject.scene);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, mapDefinition);
                    enabled = false;
                    return;
                }
            }
            if (mapDefinition != null) grid.ConfigureMap(mapDefinition);
            grid.Initialize();
            Spawns = new GridPosition[4];
            for (int i = 0; i < Spawns.Length; i++)
                Spawns[i] = mapDefinition != null ? new GridPosition(mapDefinition.Spawns[i].x, mapDefinition.Spawns[i].y) : grid.WorldToCell(spawnPoints[i].position);
            session = GameArchitecture.Interface.GetModel<MatchSessionModel>();
            matchSystem = GameArchitecture.Interface.GetSystem<MatchSystem>();
            NetworkRulesValid = ResolveMatchRules(IsNetworkMovementTest, Mirror.NetworkServer.active, out int coinsToWin, out float durationSeconds);
            if (IsReplay) { coinsToWin = replay.coinsToWin; durationSeconds = replay.duration; }
            InitialDurationSeconds = durationSeconds;
            Model = session.CreateMatch(grid.Width, grid.Height, Spawns, Mathf.Max(1, initialHealth), Mathf.Max(0.1f, maxStamina), coinsToWin, durationSeconds);
            if (mapDefinition != null)
            {
                var blocked = new System.Collections.Generic.List<GridPosition>();
                for (int x = 0; x < mapDefinition.Width; x++) for (int y = 0; y < mapDefinition.Height; y++)
                    if (mapDefinition.Cells[x * mapDefinition.Height + y].terrain == ArenaTerrain.Wall || mapDefinition.Cells[x * mapDefinition.Height + y].terrain == ArenaTerrain.Hole) blocked.Add(new GridPosition(x, y));
                Rules.ConfigureBlockedCells(blocked);
                var edges = new System.Collections.Generic.List<GridEdge>();
                for (int x = 0; x < mapDefinition.Width; x++) for (int y = 0; y < mapDefinition.Height; y++)
                {
                    var from = new GridPosition(x, y);
                    if (Model.Grid.IsBlocked(from)) continue;
                    if (x + 1 < mapDefinition.Width)
                    {
                        var to = new GridPosition(x + 1, y);
                        if (!Model.Grid.IsBlocked(to) && !ArenaMapTopology.CanTraverse(mapDefinition, from, to)) edges.Add(new GridEdge(from, to));
                    }
                    if (y + 1 < mapDefinition.Height)
                    {
                        var to = new GridPosition(x, y + 1);
                        if (!Model.Grid.IsBlocked(to) && !ArenaMapTopology.CanTraverse(mapDefinition, from, to)) edges.Add(new GridEdge(from, to));
                    }
                }
                Rules.ConfigureBlockedEdges(edges);
            }
            if (!IsNetworkMovementTest && !IsReplay) Rules.StartMatch();
            Stamina = session.Stamina;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var obstacle in root.GetComponentsInChildren<ObstacleBehaviour>(true)) obstacle.BindArena(this);
                if (NameplateFont == null)
                {
                    var ui = root.GetComponentInChildren<ArenaGameUI>(true);
                    if (ui != null) NameplateFont = ui.NameplateFont;
                }
            }
            Characters = new ArenaCharacter[4];
            Characters[0] = playerTemplate;
            for (int i = 1; i < Characters.Length; i++)
            {
                Characters[i] = Instantiate(playerTemplate, playerTemplate.transform.parent);
                Characters[i].name = "NPC " + i;
                if (!IsNetworkMovementTest)
                    foreach (var camera in Characters[i].GetComponentsInChildren<Camera>(true)) camera.gameObject.SetActive(false);
            }
            for (int i = 0; i < Characters.Length; i++)
            {
                Characters[i].Initialize(this, i, grid.PlayerColor(i));
                Characters[i].Teleport(grid.CellToWorld(Spawns[i]));
                participantAppearanceIndices[i] = i;
                participantAppearances[i] = ResolveAppearanceProfile(i, i);
            }
            if (appearanceCatalog == null)
            {
                Debug.LogWarning("MatchCoordinator 尚未绑定 DefaultAppearanceCatalog.asset，已回退至角色预制体默认装扮（可能缺少“无配饰”及头像配饰叠层）。请在 Unity 菜单执行 EachOneCoin/Setup/Apply All 4 Tasks Assets (1+4+2+3) 完成装配。", this);
            }
            grid.Refresh(Model.Grid);
            coins.Initialize(this, mapDefinition != null ? mapDefinition.Mines : null); grid.Refresh(Model.Grid); combat.Initialize(this); npcs.Initialize(this);
            for (int i = 0; i < nearVictoryAnnounced.Length; i++) nearVictoryAnnounced[i] = false;
            for (int i = 0; i < participantNames.Length; i++) participantNames[i] = null;
            for (int i = 0; i < nameplateLabels.Length; i++) nameplateLabels[i] = null;
            if (coins != null)
            {
                coins.MineCoinsSpawned -= OnMineCoinsSpawned;
                coins.MineCoinsSpawned += OnMineCoinsSpawned;
            }
            if (!IsNetworkMovementTest && !IsReplay)
            {
                npcs.AssignPersonalities(14);
                participantNames[0] = "玩家1";
                Characters[0].SetDisplayName(participantNames[0]);
                for (int i = 1; i < Characters.Length; i++)
                {
                    participantNames[i] = NpcController.PersonalityName(npcs.PersonalityCode(i));
                    Characters[i].SetDisplayName(participantNames[i]);
                }
                ApplySinglePlayerAppearances();
                CurrentViewSlot = 0;
                RefreshOverheadHealthBarVisibility();
                SyncOverheadHealthBars(true);
            }
            if (IsNetworkMovementTest)
            {
                for (int i = 0; i < Characters.Length; i++)
                    foreach (var camera in Characters[i].GetComponentsInChildren<Camera>(true)) camera.gameObject.SetActive(i == network.LocalSlot);
                if (!Mirror.NetworkServer.active)
                    foreach (var root in gameObject.scene.GetRootGameObjects())
                        foreach (var obstacle in root.GetComponentsInChildren<ObstacleBehaviour>(true)) obstacle.SetRunning(false);
                ApplyNetworkInitialAppearances(network);
                NetworkController = gameObject.AddComponent<EachOneCoin.Networking.NetworkMatchController>();
                NetworkController.Initialize(this, network, localInput);
                CurrentViewSlot = network.LocalSlot;
                RefreshOverheadHealthBarVisibility();
                SyncOverheadHealthBars(true);
                return;
            }
            if (IsReplay)
            {
                foreach (var root in gameObject.scene.GetRootGameObjects())
                    foreach (var obstacle in root.GetComponentsInChildren<ObstacleBehaviour>(true)) obstacle.SetRunning(false);
                gameObject.AddComponent<Replay.ReplayPlayer>().Initialize(this, replay);
                return;
            }
            if (!IsNetworkMovementTest) BeginReplayRecording(false);
            if (System.Environment.GetEnvironmentVariable("EACHONECOIN_MOVEMENT_TELEMETRY") == "1" ||
                System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--movement-telemetry"))
            {
                Telemetry = gameObject.AddComponent<MovementTelemetry>();
                Telemetry.Initialize(this, localInput);
            }
        }

        public bool TryGetReplayAim(int slot, out Vector3 direction)
        {
            if (IsNetworkMovementTest && NetworkController != null) return NetworkController.TryGetReplayAim(slot, out direction);
            direction = aimDirection;
            return slot == 0 && aiming;
        }

        public void SetReplayParticipantName(int slot, string name)
        {
            if (!IsReplay || slot < 0 || slot >= participantNames.Length) return;
            participantNames[slot] = name;
            Characters[slot].SetDisplayName(name);
        }

        public void SetViewTarget(int slot)
        {
            CurrentViewSlot = slot;
            if (Characters == null) return;
            for (int i = 0; i < Characters.Length; i++)
                foreach (var camera in Characters[i].GetComponentsInChildren<Camera>(true)) camera.gameObject.SetActive(i == slot);
            RefreshOverheadHealthBarVisibility();
        }

        private void LateUpdate()
        {
            if (Model == null || Characters == null) return;
            SyncOverheadHealthBars(false);
        }

        private int ResolveHiddenHealthBarSlot()
        {
            return hideHealthBarOnViewTarget ? CurrentViewSlot : -1;
        }

        private void RefreshOverheadHealthBarVisibility()
        {
            if (Characters == null) return;
            int hiddenSlot = ResolveHiddenHealthBarSlot();
            for (int i = 0; i < Characters.Length; i++)
            {
                if (Characters[i] != null) Characters[i].SetOverheadHealthBarVisible(i != hiddenSlot);
            }
        }

        private void SyncOverheadHealthBars(bool force)
        {
            if (Model == null || Characters == null) return;
            int initialMax = InitialHealth;
            bool useNetworkReplica = IsNetworkMovementTest && !Mirror.NetworkServer.active;
            bool replicaReady = useNetworkReplica && NetworkController != null && NetworkController.HasCombatState;
            int count = Mathf.Min(Characters.Length, Model.Participants.Count);
            for (int i = 0; i < count; i++)
            {
                if (Characters[i] == null) continue;
                int health;
                int maxHealth;
                if (useNetworkReplica)
                {
                    if (replicaReady)
                    {
                        health = NetworkController.HealthAt(i);
                        maxHealth = NetworkController.MaxHealthAt(i);
                    }
                    else
                    {
                        health = initialMax;
                        maxHealth = initialMax;
                    }
                }
                else
                {
                    var participant = Model.Participants[i];
                    health = participant.Health;
                    maxHealth = participant.MaxHealth;
                }

                if (!force &&
                    lastOverheadHealth[i] == health &&
                    lastOverheadMaxHealth[i] == maxHealth &&
                    lastOverheadInitialMaxHealth[i] == initialMax)
                {
                    continue;
                }

                lastOverheadHealth[i] = health;
                lastOverheadMaxHealth[i] = maxHealth;
                lastOverheadInitialMaxHealth[i] = initialMax;
                Characters[i].SetOverheadHealth(health, maxHealth, initialMax, force);
            }
        }

        public int GetParticipantAppearanceIndex(int slot)
        {
            if (slot < 0 || slot >= participantAppearanceIndices.Length) return 0;
            return participantAppearanceIndices[slot];
        }

        public CharacterAppearanceProfile GetParticipantAppearance(int slot)
        {
            if (slot < 0 || slot >= participantAppearances.Length) return null;
            if (participantAppearances[slot] != null) return participantAppearances[slot];
            return ResolveAppearanceProfile(participantAppearanceIndices[slot], slot);
        }

        public string GetParticipantAppearanceId(int slot)
        {
            var profile = GetParticipantAppearance(slot);
            if (profile != null && !string.IsNullOrEmpty(profile.AppearanceId)) return profile.AppearanceId;
            int index = GetParticipantAppearanceIndex(slot);
            if (appearanceCatalog != null)
            {
                string catalogId = appearanceCatalog.GetId(index);
                if (!string.IsNullOrEmpty(catalogId)) return catalogId;
            }
            return "slot_" + index;
        }

        public void ApplyParticipantAppearance(int slot, int appearanceIndex)
        {
            if (slot < 0 || slot >= participantAppearanceIndices.Length) return;
            int count = AppearanceCatalogCount;
            byte requested = appearanceIndex >= 0 && appearanceIndex <= byte.MaxValue
                ? (byte)appearanceIndex
                : CosmeticAssignmentPolicy.UnspecifiedIndex;
            byte sanitized = CosmeticAssignmentPolicy.SanitizeIndex(requested, count, slot);
            var profile = ResolveAppearanceProfile(sanitized, slot);
            participantAppearanceIndices[slot] = sanitized;
            participantAppearances[slot] = profile;
            if (Characters != null && slot < Characters.Length && Characters[slot] != null)
            {
                Characters[slot].ApplyAppearance(profile, sanitized);
            }
            ParticipantAppearanceChanged?.Invoke(slot);
        }

        private void ApplySinglePlayerAppearances()
        {
            int count = AppearanceCatalogCount;
            string savedId = CosmeticPreferenceStore.LoadSelectedAppearanceId();
            int localIndex = ResolveAppearanceIndexForId(savedId, 0);
            var humanRequested = new byte[]
            {
                (byte)Mathf.Clamp(localIndex, 0, byte.MaxValue),
                CosmeticAssignmentPolicy.UnspecifiedIndex,
                CosmeticAssignmentPolicy.UnspecifiedIndex,
                CosmeticAssignmentPolicy.UnspecifiedIndex
            };
            var resolved = new byte[4];
            CosmeticAssignmentPolicy.ResolveMatchAppearances(count, 1, 15, humanRequested, resolved, true);
            for (int i = 0; i < resolved.Length; i++)
            {
                ApplyParticipantAppearance(i, resolved[i]);
            }
        }

        private void ApplyNetworkInitialAppearances(EachOneCoin.Networking.LanNetworkManager network)
        {
            if (network == null || !network.HasSnapshot || Characters == null) return;
            var snapshot = network.Snapshot;
            int catalogCount = AppearanceCatalogCount;
            int count = Mathf.Min(Characters.Length, participantAppearanceIndices.Length);
            for (int i = 0; i < count; i++)
            {
                byte snapAppearance = snapshot.AppearanceAt(i);
                if (snapAppearance < catalogCount)
                {
                    ApplyParticipantAppearance(i, snapAppearance);
                }
            }
        }

        private int ResolveAppearanceIndexForId(string appearanceId, int fallbackIndex)
        {
            if (appearanceCatalog != null && appearanceCatalog.Count > 0)
            {
                return appearanceCatalog.ResolveIndexOrDefault(appearanceId, fallbackIndex);
            }
            var template = Characters != null && Characters.Length > 0 && Characters[0] != null ? Characters[0] : playerTemplate;
            int count = AppearanceCatalogCount;
            if (template != null && !string.IsNullOrWhiteSpace(appearanceId))
            {
                string trimmed = appearanceId.Trim();
                int fallbackCount = template.FallbackAppearanceCount;
                for (int i = 0; i < fallbackCount; i++)
                {
                    var profile = template.GetFallbackProfile(i);
                    if (profile != null && string.Equals(profile.AppearanceId, trimmed, System.StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
            }
            return count > 0 && fallbackIndex >= 0 && fallbackIndex < count ? fallbackIndex : 0;
        }

        private CharacterAppearanceProfile ResolveAppearanceProfile(int appearanceIndex, int fallbackSlot)
        {
            if (appearanceCatalog != null && appearanceCatalog.Count > 0)
            {
                var fromCatalog = appearanceCatalog.GetProfile(appearanceIndex);
                if (fromCatalog != null) return fromCatalog;
            }
            var template = Characters != null && Characters.Length > 0 && Characters[0] != null ? Characters[0] : playerTemplate;
            if (template != null)
            {
                var fallback = template.GetFallbackProfile(appearanceIndex);
                if (fallback != null) return fallback;
                return template.GetFallbackProfile(fallbackSlot);
            }
            return null;
        }

        private void Update()
        {
            if (IsReplay || IsNetworkMovementTest || IsPaused || Model == null || Model.Status != MatchStatus.Playing) return;
            coins.Tick(Time.deltaTime);
            combat.Tick(Time.deltaTime);
            bool playerActive = Model.Participants[0].Status == ParticipantStatus.Active;
            Vector3 movement = playerActive ? localInput.Movement : Vector3.zero;
            Rules.TickStamina(Time.deltaTime, playerActive && !Characters[0].IsKnockedBack,
                movement.sqrMagnitude > 0f, localInput.SprintHeld, Mathf.Max(0f, sprintStaminaPerSecond), Mathf.Max(0f, staminaRecoveryPerSecond));
            if (playerActive)
            {
                float speed = moveSpeed * (Stamina.IsSprinting ? Mathf.Max(1f, sprintSpeedMultiplier) : 1f);
                Characters[0].MoveWithVelocity(movement * speed, Time.deltaTime);
                if (Application.isFocused && Model.Participants[0].Status == ParticipantStatus.Active) UpdateAim();
                else CancelAim();
            }
            for (int i = 1; i < Characters.Length && Model.Status == MatchStatus.Playing; i++) npcs.Tick(i, Time.deltaTime);
            for (int i = 0; i < Characters.Length && Model.Status == MatchStatus.Playing; i++) Characters[i].TickAttackKnockback(Time.deltaTime);
            coins.AdvanceMatch(Time.deltaTime);
            Rules.AdvanceTime(Time.deltaTime);
            if (Model.Status != MatchStatus.Playing) npcs.CancelAllWindups();
            RefreshStates();
        }

        private void RefreshStates()
        {
            if (Model.Participants[0].Status != ParticipantStatus.Active) CancelAim();
            for (int i = 0; i < Characters.Length; i++)
            {
                var status = Model.Participants[i].Status;
                if (status == ParticipantStatus.Won || status == ParticipantStatus.Lost) Characters[i].SetActiveState(false);
            }
        }

        private void UpdateAim()
        {
            if (!aiming && (localInput.KeyboardAttackDown || localInput.GamepadAttackDown))
            {
                aimUsesGamepad = !localInput.KeyboardAttackDown && localInput.GamepadAttackDown;
                aiming = true;
            }
            if (!aiming) return;
            if (localInput.TryAimDirection(aimUsesGamepad, Characters[0].transform.position, Camera.main, out Vector3 direction)) aimDirection = direction;
            if (!localInput.AttackHeld(aimUsesGamepad))
            {
                CancelAim();
                combat.Attack(0, aimDirection);
                return;
            }
            Characters[0].SetAttackAim(true, aimDirection, combat.AttackRange, combat.AttackAngle);
        }

        private void CancelAim()
        {
            aiming = false;
            if (Characters != null && Characters.Length > 0 && Characters[0] != null)
                Characters[0].SetAttackAim(false, aimDirection, 0f, 0f);
        }

        private bool IsFeedAuthority => !IsNetworkMovementTest || Mirror.NetworkServer.active;

        private void EmitFeedEvent(MatchFeedEvent evt)
        {
            FeedEventRaised?.Invoke(evt);
            if (IsNetworkMovementTest && Mirror.NetworkServer.active && NetworkController != null)
                NetworkController.BroadcastFeedEvent(evt);
        }

        public void RaiseNetworkFeedEvent(MatchFeedEvent feedEvent)
        {
            FeedEventRaised?.Invoke(feedEvent);
        }

        public string GetParticipantDisplayName(int id)
        {
            if (id < 0) return "未知玩家";
            if (IsNetworkMovementTest)
            {
                if (NetworkController != null && id < participantNames.Length)
                {
                    if (NetworkController.IsHuman(id)) return "玩家" + (id + 1);
                    if (Mirror.NetworkServer.active && npcs != null)
                    {
                        string serverNpcName = NpcController.PersonalityName(npcs.PersonalityCode(id));
                        participantNames[id] = serverNpcName;
                        return serverNpcName;
                    }
                    string nameplateName = TryReadCharacterDisplayName(id);
                    if (!string.IsNullOrEmpty(nameplateName))
                    {
                        participantNames[id] = nameplateName;
                        return nameplateName;
                    }
                    if (!string.IsNullOrEmpty(participantNames[id])) return participantNames[id];
                    return npcs != null ? NpcController.PersonalityName(npcs.PersonalityCode(id)) : "NPC";
                }
                string fallbackName = TryReadCharacterDisplayName(id);
                if (!string.IsNullOrEmpty(fallbackName)) return fallbackName;
                return "玩家" + (id + 1);
            }

            if (id == 0) return "玩家1";
            if (id < participantNames.Length && !string.IsNullOrEmpty(participantNames[id])) return participantNames[id];
            string characterName = TryReadCharacterDisplayName(id);
            if (!string.IsNullOrEmpty(characterName)) return characterName;
            if (npcs != null && id < participantNames.Length) return NpcController.PersonalityName(npcs.PersonalityCode(id));
            return "电脑 " + id;
        }

        private string TryReadCharacterDisplayName(int id)
        {
            if (Characters == null || id < 0 || id >= Characters.Length || Characters[id] == null) return null;
            if (id < nameplateLabels.Length && nameplateLabels[id] == null)
            {
                var labelTransform = Characters[id].transform.Find("Character Nameplate/Name");
                if (labelTransform != null) nameplateLabels[id] = labelTransform.GetComponent<TMPro.TMP_Text>();
            }
            if (id < nameplateLabels.Length && nameplateLabels[id] != null)
            {
                string text = nameplateLabels[id].text;
                if (!string.IsNullOrEmpty(text)) return text;
            }
            return null;
        }

        private void OnMineCoinsSpawned(int mineIndex, int count)
        {
            if (count <= 0 || !IsFeedAuthority) return;
            EmitFeedEvent(MatchFeedEvent.CreateCoinSpawned(mineIndex, count));
        }

        public void CoinCollected(int id)
        {
            if (id == 0 && gameAudio != null) gameAudio.PlayCoin();
            if (!IsFeedAuthority) return;
            EmitFeedEvent(MatchFeedEvent.CreateCoinCollected(id, 1));
            if (Model == null || id < 0 || id >= Model.Participants.Count) return;
            var p = Model.Participants[id];
            if (p.Status == ParticipantStatus.Won)
            {
                EmitFeedEvent(MatchFeedEvent.CreatePlayerWon(id, p.FinishOrder));
            }
            else if (Model.CoinsToWin >= 3 && p.Coins >= Model.CoinsToWin - 2 && id < nearVictoryAnnounced.Length && !nearVictoryAnnounced[id])
            {
                nearVictoryAnnounced[id] = true;
                EmitFeedEvent(MatchFeedEvent.CreateNearVictory(id, p.Coins));
            }
        }

        public void PlayFootstep() { if (gameAudio != null) gameAudio.PlayFootstep(); }

        public bool HitObstacle(int id)
        {
            if (IsPaused || !Rules.TryObstacleContact(id)) return false;
            if (id == 0 && gameAudio != null) gameAudio.PlayObstacleHit();
            ObstacleHit?.Invoke(id);
            if (IsNetworkMovementTest && Mirror.NetworkServer.active && NetworkController != null)
                NetworkController.BroadcastDamage(id, true);
            if (Model.Participants[id].Status == ParticipantStatus.Lost)
            {
                if (Characters != null && id >= 0 && id < Characters.Length && Characters[id] != null)
                    Characters[id].SetActiveState(false);
                if (IsFeedAuthority)
                    EmitFeedEvent(MatchFeedEvent.CreatePlayerEliminated(id, EliminationReason.Obstacle));
            }
            return true;
        }

        public void NotifyAttackHit(int id)
        {
            NotifyAttackHit(-1, id, 0, false);
        }

        public void NotifyAttackHit(int attackerId, int targetId, int droppedCoins, bool knockedOut)
        {
            AttackHit?.Invoke(targetId);
            if (IsNetworkMovementTest && Mirror.NetworkServer.active && NetworkController != null)
                NetworkController.BroadcastDamage(targetId, false);
            if (knockedOut && IsFeedAuthority)
                EmitFeedEvent(MatchFeedEvent.CreatePlayerKnockedDown(targetId, attackerId, droppedCoins));
        }

        public void NotifyRespawned(int id)
        {
            if (IsFeedAuthority)
                EmitFeedEvent(MatchFeedEvent.CreatePlayerRespawned(id));
        }

        public void NotifyNetworkDamage(int id, bool obstacle)
        {
            if (obstacle)
            {
                if (gameAudio != null) gameAudio.PlayObstacleHit();
                ObstacleHit?.Invoke(id);
            }
            else AttackHit?.Invoke(id);
        }

        public bool IsHazard(Collider collider)
        {
            if (collider == null) return false;
            var spike = collider.GetComponentInParent<TimedSpikeTrap>();
            if (spike != null && !spike.IsActive) return false;
            return collider.GetComponentInParent<LethalContact>() != null || collider.GetComponentInParent<LethalObstacle>() != null || spike != null;
        }

        public bool CanTraverse(GridPosition from, GridPosition to, bool allowHole = false)
        {
            return mapDefinition == null ? Model.Grid.Contains(to) : ArenaMapTopology.CanTraverse(mapDefinition, from, to, allowHole);
        }

        public bool IsHole(GridPosition cell)
        {
            return mapDefinition != null && cell.X >= 0 && cell.Y >= 0 && cell.X < mapDefinition.Width && cell.Y < mapDefinition.Height &&
                   mapDefinition.Cells[cell.X * mapDefinition.Height + cell.Y].terrain == ArenaTerrain.Hole;
        }

        public float TerrainSpeedMultiplier(GridPosition cell)
        {
            if (mapDefinition == null || cell.X < 0 || cell.Y < 0 || cell.X >= mapDefinition.Width || cell.Y >= mapDefinition.Height) return 1f;
            return mapDefinition.Cells[cell.X * mapDefinition.Height + cell.Y].terrain == ArenaTerrain.Slow ? mapDefinition.SlowMultiplier : 1f;
        }

        public bool IsBlocked(GridPosition cell)
        {
            if (Model != null && Model.Grid.Contains(cell) && Model.Grid.IsBlocked(cell)) return true;
            int count = Physics.OverlapSphereNonAlloc(grid.CellToWorld(cell) + Vector3.up * 0.5f, 0.42f, overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (IsHazard(overlap[i]) || (overlap[i] != null && overlap[i].GetComponentInParent<ArenaWall>() != null)) return true;
            }
            return false;
        }

        public bool IsObstructed(GridPosition cell)
        {
            int count = Physics.OverlapSphereNonAlloc(grid.CellToWorld(cell) + Vector3.up * 0.5f, 0.42f, overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var collider = overlap[i];
                if (collider == null || !collider.enabled) continue;
                if (IsHazard(collider) || !collider.isTrigger) return true;
            }
            return false;
        }

        public bool IsSpawn(GridPosition cell) { foreach (var spawn in Spawns) if (spawn.Equals(cell)) return true; return false; }
        public bool IsOtherSpawn(int id, GridPosition cell) { for (int i = 0; i < Spawns.Length; i++) if (i != id && Spawns[i].Equals(cell)) return true; return false; }
        public GridPosition FindSafeDrop(GridPosition origin)
        {
            if (!Model.Grid.Contains(origin)) return origin;
            var visited = new bool[Model.Grid.Width, Model.Grid.Height];
            var queue = new System.Collections.Generic.Queue<GridPosition>();
            queue.Enqueue(origin);
            visited[origin.X, origin.Y] = true;
            var dx = new[] { 1, -1, 0, 0 };
            var dy = new[] { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (!IsSpawn(cell) && !IsBlocked(cell)) return cell;
                for (int i = 0; i < 4; i++)
                {
                    var next = new GridPosition(cell.X + dx[i], cell.Y + dy[i]);
                    if (!Model.Grid.Contains(next) || visited[next.X, next.Y]) continue;
                    visited[next.X, next.Y] = true;
                    queue.Enqueue(next);
                }
            }
            return origin;
        }

    }
}
