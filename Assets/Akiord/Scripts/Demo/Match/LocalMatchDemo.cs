using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Demo
{
    // Local test harness only: number keys switch control between four participants.
    public sealed class LocalMatchDemo : MonoBehaviour
    {
        [SerializeField] private GameObject characterModel;
        [SerializeField] private GameObject coinModel;
        [SerializeField] private GameObject obstacleModel;
        [SerializeField, Min(0.1f)] private float movementSpeed = 3f;
        [SerializeField, Min(1)] private int initialHealth = 3;
        [SerializeField, Min(0.1f)] private float coinRespawnSeconds = 2f;
        [SerializeField, Min(0.1f)] private float coinWarningSeconds = 1f;
        [SerializeField, Range(0.2f, 1f)] private float disconnectedBrightness = 0.65f;
        [SerializeField, Min(0.1f)] private float respawnSeconds = 2f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 0.8f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.1f;
        [SerializeField, Min(1)] private int attackDamage = 1;
        [SerializeField, Min(0)] private int coinsDroppedOnDeath = 1;

        private const int Width = 11;
        private const int Height = 9;
        private const float CellSize = 1.25f;
        private const int RandomCoinCount = 3;
        private static readonly string[] ColorNames = { "Red", "Blue", "Green", "Yellow" };
        private static readonly int[] NeighbourX = { 1, -1, 0, 0 };
        private static readonly int[] NeighbourY = { 0, 0, 1, -1 };
        private readonly Color[] colors = { new Color(0.94f, 0.27f, 0.28f), new Color(0.25f, 0.51f, 0.95f), new Color(0.26f, 0.79f, 0.43f), new Color(0.94f, 0.7f, 0.19f) };
        private readonly GridPosition[] spawns = { new GridPosition(0, 0), new GridPosition(10, 0), new GridPosition(0, 8), new GridPosition(10, 8) };
        private readonly GridPosition[] fixedPositions = { new GridPosition(3, 2), new GridPosition(7, 2), new GridPosition(3, 6), new GridPosition(7, 6) };
        private readonly GridPosition[] obstaclePositions = { new GridPosition(5, 2), new GridPosition(5, 6), new GridPosition(3, 4), new GridPosition(7, 4) };
        private readonly List<CoinSlot> coins = new List<CoinSlot>();
        private readonly HashSet<GridPosition> obstacles = new HashSet<GridPosition>();
        private readonly DemoPawn[] pawns = new DemoPawn[4];
        private readonly GridPosition[] previousCells = new GridPosition[4];
        private readonly bool[] hasPreviousCell = new bool[4];
        private readonly float[] respawnTimers = new float[4];
        private readonly float[] attackTimers = new float[4];
        private readonly float[] npcRetryTimers = new float[4];
        private readonly GridPosition[] npcWaypoints = new GridPosition[4];
        private readonly bool[] hasNpcWaypoint = new bool[4];
        private readonly Renderer[,] tileRenderers = new Renderer[Width, Height];
        private readonly bool[][,] connectedCells = { new bool[Width, Height], new bool[Width, Height], new bool[Width, Height], new bool[Width, Height] };
        private MaterialPropertyBlock propertyBlock;
        private MatchModel match;
        private Material tileMaterial;
        private int selected;
        private string feedback = "1-4: switch player | WASD: move | Space: attack | R: restart";
        private float feedbackTime;

        private sealed class CoinSlot
        {
            public bool IsRandom;
            public bool IsDropped;
            public GridPosition Position;
            public GameObject Visual;
            public GameObject Warning;
            public float Remaining;
        }

        private void Start()
        {
            propertyBlock = new MaterialPropertyBlock();
            match = new MatchModel(Width, Height, spawns, Mathf.Max(1, initialHealth));
            match.Start();
            CreateBoard();
            CreatePawns();
            CreateObstacles();
            foreach (var position in fixedPositions) coins.Add(new CoinSlot { Position = position });
            for (int i = 0; i < RandomCoinCount; i++) coins.Add(new CoinSlot { IsRandom = true });
            foreach (var coin in coins) BeginCoinWarning(coin);
            RefreshBoard();
            for (int i = 0; i < pawns.Length; i++) EnterCell(i, spawns[i]);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
                return;
            }
            if (match == null) return;
            for (int i = 0; i < 4; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && selected != i)
                {
                    hasNpcWaypoint[selected] = false;
                    selected = i;
                    hasNpcWaypoint[selected] = false;
                }
            if (match.Status != MatchStatus.Playing) return;
            match.AdvanceTime(Time.deltaTime);
            if (match.Status != MatchStatus.Playing) { RefreshPawnVisibility(); return; }
            UpdateCoins(Time.deltaTime);
            UpdateRespawns(Time.deltaTime);
            for (int i = 0; i < pawns.Length; i++)
            {
                attackTimers[i] = Mathf.Max(0f, attackTimers[i] - Time.deltaTime);
                npcRetryTimers[i] = Mathf.Max(0f, npcRetryTimers[i] - Time.deltaTime);
            }
            if (feedbackTime > 0f) feedbackTime -= Time.deltaTime;
            if (match.Participants[selected].Status == ParticipantStatus.Active)
            {
                var direction = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
                MovePawn(selected, pawns[selected].Position + direction * movementSpeed * Time.deltaTime);
                if (Input.GetKeyDown(KeyCode.Space)) TryAttack(selected);
            }
            for (int i = 0; i < pawns.Length && match.Status == MatchStatus.Playing; i++)
                if (i != selected && match.Participants[i].Status == ParticipantStatus.Active) UpdateNpc(i, Time.deltaTime);
            RefreshPawnVisibility();
        }

        private void MovePawn(int id, Vector2 to)
        {
            var from = pawns[id].Position;
            to.x = Mathf.Clamp(to.x, 0f, (Width - 1) * CellSize);
            to.y = Mathf.Clamp(to.y, 0f, (Height - 1) * CellSize);
            float distance = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (CellSize * 0.2f)));
            for (int step = 1; step <= steps; step++)
            {
                var point = Vector2.Lerp(from, to, (float)step / steps);
                pawns[id].SetPosition(point);
                var cell = WorldToCell(point);
                if (!hasPreviousCell[id] || !previousCells[id].Equals(cell)) EnterCell(id, cell);
                TryCollectAtCell(id, cell);
                if (match.Participants[id].Status != ParticipantStatus.Active) break;
            }
        }

        private void TryAttack(int attacker)
        {
            if (match.Status != MatchStatus.Playing || match.Participants[attacker].Status != ParticipantStatus.Active || attackTimers[attacker] > 0f) return;
            attackTimers[attacker] = Mathf.Max(0.1f, attackCooldown);
            int target = -1;
            float closest = Mathf.Max(0.1f, attackRange);
            for (int i = 0; i < pawns.Length; i++)
            {
                if (i == attacker || match.Participants[i].Status != ParticipantStatus.Active) continue;
                float distance = Vector2.Distance(pawns[attacker].Position, pawns[i].Position);
                if (distance <= closest) { closest = distance; target = i; }
            }
            if (target < 0) return;
            if (!match.TryDamage(target, Mathf.Max(1, attackDamage), Mathf.Max(0, coinsDroppedOnDeath), out int dropped)) return;
            feedback = "P" + (attacker + 1) + " hit P" + (target + 1) + ".";
            feedbackTime = 2f;
            if (match.Participants[target].Status != ParticipantStatus.Respawning) return;
            var dropCell = FindDropCell(WorldToCell(pawns[target].Position));
            for (int i = 0; i < dropped; i++)
            {
                var coin = new CoinSlot { IsDropped = true, Position = dropCell };
                coins.Add(coin);
                SpawnCoin(coin);
            }
            respawnTimers[target] = Mathf.Max(0.1f, respawnSeconds);
            hasNpcWaypoint[target] = false;
            pawns[target].gameObject.SetActive(false);
        }

        private GridPosition FindDropCell(GridPosition origin)
        {
            if (!IsSpawn(origin) && !obstacles.Contains(origin)) return origin;
            for (int distance = 1; distance < Width + Height; distance++)
                for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++)
                {
                    var candidate = new GridPosition(x, y);
                    if (Mathf.Abs(x - origin.X) + Mathf.Abs(y - origin.Y) == distance && !IsSpawn(candidate) && !obstacles.Contains(candidate)) return candidate;
                }
            return origin;
        }

        private void UpdateRespawns(float deltaTime)
        {
            for (int i = 0; i < pawns.Length; i++)
            {
                if (match.Participants[i].Status != ParticipantStatus.Respawning) continue;
                respawnTimers[i] -= deltaTime;
                if (respawnTimers[i] > 0f || !match.TryRespawn(i)) continue;
                var spawn = CellToWorld(spawns[i]);
                pawns[i].SetPosition(new Vector2(spawn.x, spawn.z));
                hasPreviousCell[i] = false;
                hasNpcWaypoint[i] = false;
                pawns[i].gameObject.SetActive(true);
                EnterCell(i, spawns[i]);
            }
        }

        private void RefreshPawnVisibility()
        {
            for (int i = 0; i < pawns.Length; i++)
            {
                bool active = match.Participants[i].Status == ParticipantStatus.Active;
                if (pawns[i].gameObject.activeSelf != active) pawns[i].gameObject.SetActive(active);
            }
        }

        private void UpdateNpc(int id, float deltaTime)
        {
            TryAttack(id);
            if (!hasNpcWaypoint[id])
            {
                if (npcRetryTimers[id] > 0f) return;
                if (!TryFindNpcWaypoint(id, out npcWaypoints[id])) { npcRetryTimers[id] = 0.25f; return; }
                hasNpcWaypoint[id] = true;
            }
            var world = CellToWorld(npcWaypoints[id]);
            var waypoint = new Vector2(world.x, world.z);
            MovePawn(id, Vector2.MoveTowards(pawns[id].Position, waypoint, movementSpeed * deltaTime));
            if ((pawns[id].Position - waypoint).sqrMagnitude < 0.0001f) hasNpcWaypoint[id] = false;
        }

        // Search from the current tile to the closest coin, or back to spawn if cut off.
        // Other players' protected spawns and hazardous cells are avoided.
        private bool TryFindNpcWaypoint(int id, out GridPosition waypoint)
        {
            var start = WorldToCell(pawns[id].Position);
            bool returning = !match.Grid.IsConnected(id, start);
            var visited = new bool[Width, Height];
            var parent = new GridPosition[Width, Height];
            var queue = new Queue<GridPosition>();
            queue.Enqueue(start);
            visited[start.X, start.Y] = true;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                bool isGoal = returning ? cell.Equals(spawns[id]) : IsOccupiedByCoin(cell);
                if (isGoal)
                {
                    while (!cell.Equals(start) && !parent[cell.X, cell.Y].Equals(start)) cell = parent[cell.X, cell.Y];
                    waypoint = cell;
                    return true;
                }
                for (int direction = 0; direction < 4; direction++)
                {
                    var next = new GridPosition(cell.X + NeighbourX[direction], cell.Y + NeighbourY[direction]);
                    if (!match.Grid.Contains(next) || visited[next.X, next.Y] || obstacles.Contains(next) || IsOtherSpawn(id, next)) continue;
                    visited[next.X, next.Y] = true;
                    parent[next.X, next.Y] = cell;
                    queue.Enqueue(next);
                }
            }
            waypoint = default;
            return false;
        }

        private bool IsOtherSpawn(int id, GridPosition position)
        {
            for (int i = 0; i < spawns.Length; i++) if (i != id && spawns[i].Equals(position)) return true;
            return false;
        }

        private void EnterCell(int id, GridPosition cell)
        {
            previousCells[id] = cell;
            hasPreviousCell[id] = true;
            if (match.TryPaint(id, cell)) RefreshBoard();
            if (obstacles.Contains(cell) && match.TryObstacleContact(id))
            {
                feedback = "P" + (id + 1) + " hit an obstacle (-1 maximum health).";
                feedbackTime = 3f;
            }
        }

        private void TryCollectAtCell(int id, GridPosition cell)
        {
            foreach (var coin in coins)
            {
                if (coin.Visual == null || !coin.Position.Equals(cell)) continue;
                // Contact is checked in world space; sharing a tile alone is not enough.
                var center = CellToWorld(cell);
                if (Vector2.Distance(pawns[id].Position, new Vector2(center.x, center.z)) > 0.38f) continue;
                if (!match.TryCollectCoin(id, cell))
                {
                    feedback = "Path disconnected: reconnect this tile to your spawn.";
                    feedbackTime = 1.5f;
                    continue;
                }
                Destroy(coin.Visual);
                coin.Visual = null;
                if (coin.IsDropped) coin.Remaining = float.PositiveInfinity;
                else coin.Remaining = Mathf.Max(0f, coinRespawnSeconds - Mathf.Max(0.1f, coinWarningSeconds));
                feedback = "P" + (id + 1) + " collected a coin.";
                feedbackTime = 2f;
            }
        }

        private void UpdateCoins(float deltaTime)
        {
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                var coin = coins[i];
                if (coin.Visual != null) { coin.Visual.transform.Rotate(Vector3.up, 90f * deltaTime, Space.World); continue; }
                if (coin.IsDropped) { coins.RemoveAt(i); continue; }
                coin.Remaining -= deltaTime;
                if (coin.Warning != null)
                {
                    float pulse = 0.7f + 0.2f * Mathf.Sin(Time.time * 9f);
                    coin.Warning.transform.localScale = new Vector3(pulse, 0.025f, pulse);
                    if (coin.Remaining <= 0f) SpawnCoin(coin);
                }
                else if (coin.Remaining <= 0f) BeginCoinWarning(coin);
            }
        }

        private void BeginCoinWarning(CoinSlot coin)
        {
            if (coin.IsRandom && !TryFindRandomCell(out coin.Position)) { coin.Remaining = 1f; return; }
            if (!coin.IsRandom && IsReservedByCoin(coin.Position)) { coin.Remaining = 1f; return; }
            coin.Warning = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coin.Warning.name = "Coin Spawn Warning";
            coin.Warning.transform.SetParent(transform);
            coin.Warning.transform.position = CellToWorld(coin.Position) + Vector3.up * 0.09f;
            coin.Warning.transform.localScale = new Vector3(0.7f, 0.025f, 0.7f);
            var renderer = coin.Warning.GetComponent<Renderer>();
            renderer.sharedMaterial = tileMaterial;
            propertyBlock.Clear();
            propertyBlock.SetColor("_BaseColor", new Color(1f, 0.8f, 0.1f));
            propertyBlock.SetColor("_Color", new Color(1f, 0.8f, 0.1f));
            renderer.SetPropertyBlock(propertyBlock);
            DisableColliders(coin.Warning);
            coin.Remaining = Mathf.Max(0.1f, coinWarningSeconds);
        }

        private void SpawnCoin(CoinSlot coin)
        {
            if (coin.Warning != null) { Destroy(coin.Warning); coin.Warning = null; }
            coin.Visual = coinModel != null ? Instantiate(coinModel, transform) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coin.Visual.name = coin.IsDropped ? "Dropped Coin" : coin.IsRandom ? "Random Coin" : "Fixed Coin";
            coin.Visual.transform.position = CellToWorld(coin.Position) + Vector3.up * 0.45f;
            if (coinModel == null) { coin.Visual.transform.SetParent(transform); coin.Visual.transform.localScale = Vector3.one * 0.35f; }
            DisableColliders(coin.Visual);
        }

        private bool TryFindRandomCell(out GridPosition position)
        {
            var candidates = new List<GridPosition>();
            for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++)
            {
                var cell = new GridPosition(x, y);
                if (obstacles.Contains(cell) || IsSpawn(cell) || IsFixed(cell) || IsReservedByCoin(cell)) continue;
                candidates.Add(cell);
            }
            if (candidates.Count == 0) { position = default; return false; }
            position = candidates[Random.Range(0, candidates.Count)];
            return true;
        }

        private bool IsReservedByCoin(GridPosition position)
        {
            foreach (var coin in coins) if ((coin.Visual != null || coin.Warning != null) && coin.Position.Equals(position)) return true;
            return false;
        }

        private bool IsOccupiedByCoin(GridPosition position)
        {
            foreach (var coin in coins) if (coin.Visual != null && coin.Position.Equals(position)) return true;
            return false;
        }
        private bool IsSpawn(GridPosition position) { foreach (var p in spawns) if (p.Equals(position)) return true; return false; }
        private bool IsFixed(GridPosition position) { foreach (var p in fixedPositions) if (p.Equals(position)) return true; return false; }
        private static GridPosition WorldToCell(Vector2 p) => new GridPosition(Mathf.Clamp(Mathf.RoundToInt(p.x / CellSize), 0, Width - 1), Mathf.Clamp(Mathf.RoundToInt(p.y / CellSize), 0, Height - 1));
        private static Vector3 CellToWorld(GridPosition p) => new Vector3(p.X * CellSize, 0f, p.Y * CellSize);

        private void CreateBoard()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            tileMaterial = new Material(shader);
            for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "Tile " + x + "," + y;
                tile.transform.SetParent(transform);
                tile.transform.position = CellToWorld(new GridPosition(x, y));
                tile.transform.localScale = new Vector3(CellSize * 0.96f, 0.12f, CellSize * 0.96f);
                tileRenderers[x, y] = tile.GetComponent<Renderer>();
                tileRenderers[x, y].sharedMaterial = tileMaterial;
                DisableColliders(tile);
            }
        }

        private void RefreshBoard()
        {
            for (int i = 0; i < connectedCells.Length; i++) match.Grid.CopyConnectedTo(i, connectedCells[i]);
            for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++)
            {
                var owner = match.Grid.OwnerAt(new GridPosition(x, y));
                Color color = owner.HasValue ? colors[owner.Value] : new Color(0.62f, 0.65f, 0.63f);
                if (owner.HasValue && !connectedCells[owner.Value][x, y])
                {
                    float brightness = Mathf.Clamp(disconnectedBrightness, 0.2f, 1f);
                    color = new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
                }
                propertyBlock.Clear();
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                tileRenderers[x, y].SetPropertyBlock(propertyBlock);
            }
        }

        private void CreatePawns()
        {
            for (int i = 0; i < pawns.Length; i++)
            {
                var root = new GameObject("Player " + (i + 1));
                root.transform.SetParent(transform);
                pawns[i] = root.AddComponent<DemoPawn>();
                var pos = CellToWorld(spawns[i]);
                pawns[i].Initialize(i, new Vector2(pos.x, pos.z));
                var visual = characterModel != null ? Instantiate(characterModel, root.transform) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                if (characterModel == null) { visual.transform.SetParent(root.transform); visual.transform.localScale = Vector3.one * 0.4f; }
                visual.transform.localPosition = Vector3.zero;
                DisableColliders(visual);
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "Player Color";
                marker.transform.SetParent(root.transform, false);
                marker.transform.localPosition = new Vector3(0f, -0.06f, 0f);
                marker.transform.localScale = new Vector3(0.75f, 0.04f, 0.75f);
                var markerRenderer = marker.GetComponent<Renderer>();
                markerRenderer.sharedMaterial = tileMaterial;
                propertyBlock.Clear(); propertyBlock.SetColor("_BaseColor", colors[i]); propertyBlock.SetColor("_Color", colors[i]); markerRenderer.SetPropertyBlock(propertyBlock);
                DisableColliders(marker);
            }
        }

        private void CreateObstacles()
        {
            foreach (var pos in obstaclePositions)
            {
                obstacles.Add(pos);
                var visual = obstacleModel != null ? Instantiate(obstacleModel, transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Obstacle";
                visual.transform.position = CellToWorld(pos) + Vector3.up * 0.1f;
                if (obstacleModel == null) { visual.transform.SetParent(transform); visual.transform.localScale = Vector3.one * 0.5f; }
                DisableColliders(visual);
            }
        }

        private static void DisableColliders(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>()) collider.enabled = false;
        }

        private void OnDestroy()
        {
            if (tileMaterial != null) Destroy(tileMaterial);
        }

        private void OnGUI()
        {
            if (match == null) return;
            float scale = Mathf.Max(1f, Screen.width / 1280f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float uiWidth = Screen.width / scale;
            float uiHeight = Screen.height / scale;
            GUI.Box(new Rect(10, 10, 390, 155), "LOCAL DEMO - not networked");
            GUI.Label(new Rect(20, 34, 370, 22), "Time: " + Mathf.CeilToInt(match.RemainingTime) + "    Selected: P" + (selected + 1));
            for (int i = 0; i < match.Participants.Count; i++)
            {
                var p = match.Participants[i];
                GUI.Label(new Rect(20, 56 + i * 21, 370, 21), "P" + (i + 1) + (i == selected ? " YOU " : " NPC ") + ColorNames[i] + "  coins " + p.Coins + "/" + match.CoinsToWin + "  health " + p.Health + "/" + p.MaxHealth + "  " + p.Status);
            }
            GUI.Box(new Rect(10, uiHeight - 65, Mathf.Min(720, uiWidth - 20), 55), feedbackTime > 0 ? feedback : "1-4: switch player | WASD: move | Space: attack | R: restart");
        }
    }
}
