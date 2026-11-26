using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class MatchCoordinator : MonoBehaviour
    {
        [SerializeField] private ArenaGridView grid;
        [SerializeField] private ArenaCharacter playerTemplate;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private CoinSpawner coins;
        [SerializeField] private CombatController combat;
        [SerializeField] private NpcController npcs;
        [SerializeField] private LocalPlayerInput localInput;
        [SerializeField] private GameAudio gameAudio;
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
        private readonly Collider[] overlap = new Collider[64];
        public MatchModel Model { get; private set; }
        public ArenaGridView Grid => grid;
        public CoinSpawner Coins => coins;
        public CombatController Combat => combat;
        public NpcController Npcs => npcs;
        public ArenaCharacter[] Characters { get; private set; }
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
        public bool IsPaused { get; private set; }
        public bool IsNetworkMovementTest { get; private set; }
        public EachOneCoin.Networking.NetworkMatchController NetworkController { get; private set; }
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

        public void Pause()
        {
            if (IsPaused || Model == null || Model.Status != MatchStatus.Playing) return;
            CancelAim();
            timeScaleBeforePause = Time.timeScale;
            IsPaused = true;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = timeScaleBeforePause;
        }

        private void OnDestroy()
        {
            Resume();
            if (session != null) session.ClearMatch(Model);
        }

        private void Start()
        {
            var network = Mirror.NetworkManager.singleton as EachOneCoin.Networking.LanNetworkManager;
            IsNetworkMovementTest = network != null && Mirror.NetworkClient.active && network.HasSnapshot;
            grid.Initialize();
            Spawns = new GridPosition[4];
            for (int i = 0; i < Spawns.Length; i++) Spawns[i] = grid.WorldToCell(spawnPoints[i].position);
            session = GameArchitecture.Interface.GetModel<MatchSessionModel>();
            matchSystem = GameArchitecture.Interface.GetSystem<MatchSystem>();
            Model = session.CreateMatch(ArenaGridView.Size, ArenaGridView.Size, Spawns, Mathf.Max(1, initialHealth), Mathf.Max(0.1f, maxStamina), IsNetworkMovementTest ? int.MaxValue : 10);
            Rules.StartMatch();
            Stamina = session.Stamina;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var obstacle in root.GetComponentsInChildren<ObstacleBehaviour>(true)) obstacle.BindArena(this);
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
            }
            grid.Refresh(Model.Grid);
            coins.Initialize(this); grid.Refresh(Model.Grid); combat.Initialize(this); npcs.Initialize(this);
            if (IsNetworkMovementTest)
            {
                for (int i = 0; i < Characters.Length; i++)
                    foreach (var camera in Characters[i].GetComponentsInChildren<Camera>(true)) camera.gameObject.SetActive(i == network.LocalSlot);
                foreach (var root in gameObject.scene.GetRootGameObjects())
                    foreach (var obstacle in root.GetComponentsInChildren<ObstacleBehaviour>(true)) obstacle.enabled = false;
                NetworkController = gameObject.AddComponent<EachOneCoin.Networking.NetworkMatchController>();
                NetworkController.Initialize(this, network, localInput);
                return;
            }
            if (System.Environment.GetEnvironmentVariable("EACHONECOIN_MOVEMENT_TELEMETRY") == "1" ||
                System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--movement-telemetry"))
            {
                Telemetry = gameObject.AddComponent<MovementTelemetry>();
                Telemetry.Initialize(this, localInput);
            }
        }

        private void Update()
        {
            if (IsNetworkMovementTest || IsPaused || Model == null || Model.Status != MatchStatus.Playing) return;
            coins.BeginFrame();
            coins.Tick(Time.deltaTime);
            combat.Tick(Time.deltaTime);
            bool playerActive = Model.Participants[0].Status == ParticipantStatus.Active;
            Vector3 movement = playerActive ? PlanarVelocity.Cardinal(localInput.Movement, Characters[0].MovementVelocity).normalized : Vector3.zero;
            Rules.TickStamina(Time.deltaTime, playerActive && !Characters[0].IsKnockedBack,
                movement.sqrMagnitude > 0f, localInput.SprintHeld, Mathf.Max(0f, sprintStaminaPerSecond), Mathf.Max(0f, staminaRecoveryPerSecond));
            if (playerActive)
            {
                float speed = moveSpeed * (Stamina.IsSprinting ? Mathf.Max(1f, sprintSpeedMultiplier) : 1f);
                Characters[0].MoveWithVelocity(movement * speed, Time.deltaTime);
                if (Model.Participants[0].Status == ParticipantStatus.Active) UpdateAim();
                else CancelAim();
            }
            for (int i = 1; i < Characters.Length && Model.Status == MatchStatus.Playing; i++) npcs.Tick(i, Time.deltaTime);
            for (int i = 0; i < Characters.Length && Model.Status == MatchStatus.Playing; i++) Characters[i].TickAttackKnockback(Time.deltaTime);
            coins.AdvanceMatch(Time.deltaTime);
            Rules.AdvanceTime(Time.deltaTime);
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

        public void CoinCollected(int id) { if (id == 0 && gameAudio != null) gameAudio.PlayCoin(); }
        public void PlayFootstep() { if (gameAudio != null) gameAudio.PlayFootstep(); }
        public bool HitObstacle(int id)
        {
            if (IsPaused || !Rules.TryObstacleContact(id)) return false;
            if (id == 0 && gameAudio != null) gameAudio.PlayObstacleHit();
            ObstacleHit?.Invoke(id);
            if (Model.Participants[id].Status == ParticipantStatus.Lost) Characters[id].SetActiveState(false);
            return true;
        }

        public bool IsHazard(Collider collider)
        {
            if (collider == null) return false;
            var disguised = collider.GetComponentInParent<DisguisedObstacle>();
            if (disguised != null) return !disguised.Consumed;
            var spike = collider.GetComponentInParent<TimedSpikeTrap>();
            if (spike != null && !spike.IsActive) return false;
            return collider.GetComponentInParent<LethalContact>() != null || collider.GetComponentInParent<LethalObstacle>() != null || spike != null;
        }

        public bool IsBlocked(GridPosition cell)
        {
            if (Model != null && Model.Grid.Contains(cell) && Model.Grid.IsBlocked(cell)) return true;
            if (coins != null && coins.IsRevealedObstacle(cell)) return true;
            int count = Physics.OverlapSphereNonAlloc(grid.CellToWorld(cell) + Vector3.up * 0.5f, 0.42f, overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (overlap[i] != null && overlap[i].GetComponentInParent<DisguisedObstacle>() != null) continue;
                if (IsHazard(overlap[i])) return true;
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
            if (!IsSpawn(origin) && !IsBlocked(origin)) return origin;
            for (int d = 1; d < ArenaGridView.Size * 2; d++) for (int x = 0; x < ArenaGridView.Size; x++) for (int y = 0; y < ArenaGridView.Size; y++)
            {
                var p = new GridPosition(x, y);
                if (Mathf.Abs(x - origin.X) + Mathf.Abs(y - origin.Y) == d && !IsSpawn(p) && !IsBlocked(p)) return p;
            }
            return origin;
        }

    }
}
