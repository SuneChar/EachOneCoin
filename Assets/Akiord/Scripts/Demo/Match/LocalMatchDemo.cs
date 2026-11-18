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

        private const int Width = 11;
        private const int Height = 9;
        private const float CellSize = 1.25f;
        private const int RandomCoinCount = 3;
        private static readonly string[] ColorNames = { "Red", "Blue", "Green", "Yellow" };
        private readonly Color[] colors = { new Color(0.94f, 0.27f, 0.28f), new Color(0.25f, 0.51f, 0.95f), new Color(0.26f, 0.79f, 0.43f), new Color(0.94f, 0.7f, 0.19f) };
        private readonly GridPosition[] spawns = { new GridPosition(0, 0), new GridPosition(10, 0), new GridPosition(0, 8), new GridPosition(10, 8) };
        private readonly GridPosition[] fixedPositions = { new GridPosition(3, 2), new GridPosition(7, 2), new GridPosition(3, 6), new GridPosition(7, 6) };
        private readonly GridPosition[] obstaclePositions = { new GridPosition(5, 2), new GridPosition(5, 6), new GridPosition(3, 4), new GridPosition(7, 4) };
        private readonly List<CoinSlot> coins = new List<CoinSlot>();
        private readonly HashSet<GridPosition> obstacles = new HashSet<GridPosition>();
        private readonly DemoPawn[] pawns = new DemoPawn[4];
        private readonly GridPosition[] previousCells = new GridPosition[4];
        private readonly bool[] hasPreviousCell = new bool[4];
        private readonly Renderer[,] tileRenderers = new Renderer[Width, Height];
        private MaterialPropertyBlock propertyBlock;
        private MatchModel match;
        private Material tileMaterial;
        private int selected;
        private string feedback = "1-4: switch player | WASD / arrows: move | R: restart";
        private float feedbackTime;

        private sealed class CoinSlot
        {
            public bool IsRandom;
            public GridPosition Position;
            public GameObject Visual;
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
            foreach (var coin in coins) SpawnCoin(coin);
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
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) selected = i;
            if (match.Status != MatchStatus.Playing) return;
            match.AdvanceTime(Time.deltaTime);
            if (match.Status != MatchStatus.Playing) return;
            UpdateCoins(Time.deltaTime);
            if (feedbackTime > 0f) feedbackTime -= Time.deltaTime;
            if (match.Participants[selected].Status != ParticipantStatus.Active) return;
            var direction = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            var from = pawns[selected].Position;
            var to = from + direction * movementSpeed * Time.deltaTime;
            to.x = Mathf.Clamp(to.x, 0f, (Width - 1) * CellSize);
            to.y = Mathf.Clamp(to.y, 0f, (Height - 1) * CellSize);
            float distance = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (CellSize * 0.2f)));
            for (int step = 1; step <= steps; step++)
            {
                var point = Vector2.Lerp(from, to, (float)step / steps);
                pawns[selected].SetPosition(point);
                var cell = WorldToCell(point);
                if (!hasPreviousCell[selected] || !previousCells[selected].Equals(cell)) EnterCell(selected, cell);
                TryCollectAtCell(selected, cell);
                if (match.Participants[selected].Status != ParticipantStatus.Active) break;
            }
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
                coin.Remaining = Mathf.Max(0.1f, coinRespawnSeconds);
                feedback = "P" + (id + 1) + " collected a coin.";
                feedbackTime = 2f;
            }
        }

        private void UpdateCoins(float deltaTime)
        {
            foreach (var coin in coins)
            {
                if (coin.Visual != null) { coin.Visual.transform.Rotate(Vector3.up, 90f * deltaTime, Space.World); continue; }
                coin.Remaining -= deltaTime;
                if (coin.Remaining <= 0f) SpawnCoin(coin);
            }
        }

        private void SpawnCoin(CoinSlot coin)
        {
            if (coin.IsRandom && !TryFindRandomCell(out coin.Position)) { coin.Remaining = 1f; return; }
            if (!coin.IsRandom && IsOccupiedByCoin(coin.Position)) { coin.Remaining = 1f; return; }
            coin.Visual = coinModel != null ? Instantiate(coinModel, transform) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coin.Visual.name = coin.IsRandom ? "Random Coin" : "Fixed Coin";
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
                if (obstacles.Contains(cell) || IsSpawn(cell) || IsFixed(cell) || IsOccupiedByCoin(cell)) continue;
                candidates.Add(cell);
            }
            if (candidates.Count == 0) { position = default; return false; }
            position = candidates[Random.Range(0, candidates.Count)];
            return true;
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
            for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++)
            {
                var owner = match.Grid.OwnerAt(new GridPosition(x, y));
                Color color = owner.HasValue ? colors[owner.Value] : new Color(0.62f, 0.65f, 0.63f);
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
                GUI.Label(new Rect(20, 56 + i * 21, 370, 21), "P" + (i + 1) + " " + ColorNames[i] + "  coins " + p.Coins + "/" + match.CoinsToWin + "  health " + p.Health + "/" + p.MaxHealth + "  " + p.Status);
            }
            GUI.Box(new Rect(10, uiHeight - 65, Mathf.Min(720, uiWidth - 20), 55), feedbackTime > 0 ? feedback : "1-4: switch player | WASD / arrows: move | R: restart");
        }
    }
}
