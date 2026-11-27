using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    // Minimal, borderless pure-2D functional minimap driven directly by GridModel, CoinSpawner,
    // obstacles, and character transforms (zero extra Camera / RenderTexture overhead).
    public sealed class ArenaMinimapView : MonoBehaviour
    {
        private const int MaxPlayers = 4;
        private const int MaxMines = 4;
        private const int MaxCoinMarkers = 32;
        private const int MaxWarningMarkers = 8;
        private const int GridSize = ArenaGridView.Size;

        [Header("Prefab References (optional)")]
        [SerializeField] private UnityEngine.UI.RawImage gridRawImage;
        [SerializeField] private UnityEngine.UI.Image markerTemplate;
        [SerializeField] private MinimapPlayerMarkerView playerMarkerTemplate;

        [Header("Layout (No Frame)")]
        [SerializeField, Min(64f)] private float mapSize = 368f;
        [SerializeField] private Vector2 bottomRightOffset = new Vector2(-12f, 12f);

        [Header("Functional Layers")]
        [SerializeField] private bool showTerritory = true;
        [SerializeField] private bool showSpawns = true;
        [SerializeField] private bool showMines = true;
        [SerializeField] private bool showCoins = true;
        [SerializeField] private bool showObstacles = true;
        [SerializeField] private bool showPlayers = true;

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new Color(0.102f, 0.125f, 0.157f, 0.85f);
        [SerializeField] private Color staticObstacleColor = new Color(0.361f, 0.384f, 0.431f, 0.95f);
        [SerializeField, Range(0.2f, 1f)] private float connectedBrightness = 0.88f;
        [SerializeField, Range(0.1f, 0.8f)] private float disconnectedBrightness = 0.40f;
        [SerializeField] private Color mineIdleColor = new Color(0.804f, 0.549f, 0.125f, 1f);
        [SerializeField] private Color mineWarningColor = new Color(1f, 0.925f, 0.373f, 1f);
        [SerializeField] private Color coinColor = new Color(1f, 0.878f, 0.227f, 1f);
        [SerializeField] private Color spikeActiveColor = new Color(1f, 0.361f, 0.243f, 0.96f);
        [SerializeField] private Color spikeInactiveColor = new Color(0.608f, 0.647f, 0.698f, 0.55f);
        [SerializeField] private Color patrolObstacleColor = new Color(1f, 0.439f, 0.251f, 0.96f);

        private sealed class PlayerMarker
        {
            public RectTransform Root;
            public UnityEngine.UI.Image Ring;
            public UnityEngine.UI.Image Dot;
            public UnityEngine.UI.Image FacingArrow;
        }

        private readonly List<Texture2D> runtimeTextures = new List<Texture2D>();
        private readonly List<Sprite> runtimeSprites = new List<Sprite>();
        private readonly Color32[] pixelBuffer = new Color32[GridSize * GridSize];
        private readonly bool[][,] connectedBuffers =
        {
            new bool[GridSize, GridSize],
            new bool[GridSize, GridSize],
            new bool[GridSize, GridSize],
            new bool[GridSize, GridSize]
        };
        private readonly bool[,] staticObstacleMask = new bool[GridSize, GridSize];
        private readonly List<GridPosition> coinScratch = new List<GridPosition>(MaxCoinMarkers);
        private readonly List<GridPosition> warningScratch = new List<GridPosition>(MaxWarningMarkers);
        private readonly Color[] playerColors =
        {
            new Color(0.94f, 0.27f, 0.28f, 1f),
            new Color(0.25f, 0.51f, 0.95f, 1f),
            new Color(0.26f, 0.79f, 0.43f, 1f),
            new Color(0.94f, 0.70f, 0.19f, 1f)
        };

        private MatchCoordinator match;
        private GridModel boundGrid;
        private RectTransform mapRect;
        private Texture2D gridTexture;

        [Header("Marker Sprites (optional)")]
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Sprite ringSprite;
        [SerializeField] private Sprite diamondSprite;
        [SerializeField] private Sprite squareOutlineSprite;
        [SerializeField] private Sprite crossSprite;
        [SerializeField] private Sprite triangleSprite;
        [SerializeField] private Sprite solidSprite;

        private UnityEngine.UI.Image[] spawnMarkers;
        private UnityEngine.UI.Image[] mineMarkers;
        private UnityEngine.UI.Image[] minePulseRings;
        private UnityEngine.UI.Image[] coinMarkers;
        private UnityEngine.UI.Image[] warningMarkers;
        private UnityEngine.UI.Image[] spikeMarkers;
        private UnityEngine.UI.Image[] patrolMarkers;
        private PlayerMarker[] playerMarkers;

        private TimedSpikeTrap[] spikeTraps;
        private LinearPatrolObstacle[] patrolObstacles;

        private bool initialized;
        private uint lastGridRevision = uint.MaxValue;
        private bool lastRenderedTerritory;
        private bool lastRenderedObstacles;
        private int lastFocusedSlot = -1;

        public Vector2 BottomRightOffset => bottomRightOffset;
        public float MapSize => mapSize;
        public int TextureRefreshCount { get; private set; }
        public int FocusedPlayerSlot => lastFocusedSlot;
        public int ActiveCoinMarkerCount { get; private set; }
        public int ActiveWarningMarkerCount { get; private set; }

        public static Vector2 WorldToNormalized(Vector3 worldPos)
        {
            float half = GridSize * 0.5f;
            float u = Mathf.Clamp01((worldPos.x + half) / GridSize);
            float v = Mathf.Clamp01((worldPos.z + half) / GridSize);
            return new Vector2(u, v);
        }

        public static Vector2 CellToNormalized(GridPosition cell)
        {
            float u = Mathf.Clamp01((cell.X + 0.5f) / GridSize);
            float v = Mathf.Clamp01((cell.Y + 0.5f) / GridSize);
            return new Vector2(u, v);
        }

        public void Initialize(MatchCoordinator coordinator)
        {
            if (coordinator == null || coordinator.Model == null) return;
            match = coordinator;
            if (coordinator.Grid != null)
            {
                for (int i = 0; i < MaxPlayers; i++) playerColors[i] = coordinator.Grid.PlayerColor(i);
            }

            ScanSceneObstacles(coordinator);
            SetupCore(coordinator.Model.Grid, coordinator.Spawns, coordinator.Model.Mines);
        }

        // Lightweight entry point for EditMode tests without needing a full ArenaScene.
        public void InitializeStandalone(GridModel grid, IReadOnlyList<GridPosition> spawns, IReadOnlyList<GridPosition> mines, Color[] customColors = null)
        {
            match = null;
            spikeTraps = System.Array.Empty<TimedSpikeTrap>();
            patrolObstacles = System.Array.Empty<LinearPatrolObstacle>();
            System.Array.Clear(staticObstacleMask, 0, staticObstacleMask.Length);
            if (customColors != null)
            {
                int count = Mathf.Min(MaxPlayers, customColors.Length);
                for (int i = 0; i < count; i++) playerColors[i] = customColors[i];
            }
            SetupCore(grid, spawns, mines);
        }

        public void Refresh(int focusSlot, byte visiblePlayerMask, float pulseTime)
        {
            if (!initialized || boundGrid == null) return;

            RefreshGridTextureIfNeeded(boundGrid);
            RefreshSpawnsVisibility();

            int warningMineMask = 0;
            if (match != null && match.Coins != null)
            {
                match.Coins.CopyActiveCoins(coinScratch);
                match.Coins.CopyWarningCells(warningScratch);
                for (int i = 0; i < MaxMines; i++)
                    if (match.Coins.IsMineWarning(i)) warningMineMask |= 1 << i;
            }
            else
            {
                coinScratch.Clear();
                warningScratch.Clear();
            }

            ApplyDynamicMarkers(coinScratch, warningScratch, warningMineMask, pulseTime);
            RefreshDynamicObstacles();

            if (match != null && match.Characters != null)
            {
                for (int i = 0; i < MaxPlayers; i++)
                {
                    bool visible = showPlayers &&
                                   i < match.Characters.Length &&
                                   match.Characters[i] != null &&
                                   (visiblePlayerMask & (1 << i)) != 0;
                    Vector3 worldPos = visible ? match.Characters[i].transform.position : Vector3.zero;
                    Vector3 facing = visible ? match.Characters[i].FacingDirection : Vector3.forward;
                    ApplyPlayerMarker(i, visible, worldPos, facing, i == focusSlot);
                }
            }

            UpdateFocusLayerOrder(focusSlot);
        }

        public void RefreshGridOnly()
        {
            if (!initialized || boundGrid == null) return;
            RefreshGridTextureIfNeeded(boundGrid);
        }

        public void ApplyDynamicMarkers(IReadOnlyList<GridPosition> coins, IReadOnlyList<GridPosition> warnings, int warningMineMask, float pulseTime)
        {
            if (!initialized) return;

            float minePulse = 0.5f + 0.5f * Mathf.Sin(pulseTime * 12f);
            if (mineMarkers != null)
            {
                int mineCount = boundGrid != null && match != null && match.Model != null ? match.Model.Mines.Count : mineMarkers.Length;
                for (int i = 0; i < mineMarkers.Length; i++)
                {
                    bool active = showMines && i < mineCount && mineMarkers[i].gameObject.activeSelf;
                    mineMarkers[i].enabled = active;
                    if (!active)
                    {
                        minePulseRings[i].enabled = false;
                        continue;
                    }

                    bool isWarn = (warningMineMask & (1 << i)) != 0;
                    mineMarkers[i].color = isWarn ? Color.Lerp(mineIdleColor, mineWarningColor, 0.45f + 0.55f * minePulse) : mineIdleColor;
                    minePulseRings[i].enabled = isWarn;
                    if (isWarn)
                    {
                        float cellPx = mapSize / GridSize;
                        float ringDiameter = cellPx * (1.35f + 0.65f * minePulse);
                        minePulseRings[i].rectTransform.sizeDelta = new Vector2(ringDiameter, ringDiameter);
                        Color rc = mineWarningColor;
                        rc.a = 0.40f + 0.55f * minePulse;
                        minePulseRings[i].color = rc;
                    }
                }
            }

            int activeCoins = 0;
            if (coinMarkers != null)
            {
                int count = showCoins && coins != null ? Mathf.Min(coins.Count, coinMarkers.Length) : 0;
                activeCoins = count;
                for (int i = 0; i < coinMarkers.Length; i++)
                {
                    bool active = i < count;
                    coinMarkers[i].enabled = active;
                    if (!active) continue;
                    SetNormalizedPosition(coinMarkers[i].rectTransform, CellToNormalized(coins[i]));
                }
            }
            ActiveCoinMarkerCount = activeCoins;

            int activeWarnings = 0;
            if (warningMarkers != null)
            {
                int count = showCoins && warnings != null ? Mathf.Min(warnings.Count, warningMarkers.Length) : 0;
                activeWarnings = count;
                float warnPulse = 0.5f + 0.5f * Mathf.Sin(pulseTime * 16f);
                float cellPx = mapSize / GridSize;
                float warnDiameter = Mathf.Max(6f, cellPx * (0.62f + 0.24f * warnPulse));
                Color warnCol = mineWarningColor;
                warnCol.a = 0.55f + 0.45f * warnPulse;

                for (int i = 0; i < warningMarkers.Length; i++)
                {
                    bool active = i < count;
                    warningMarkers[i].enabled = active;
                    if (!active) continue;
                    var rt = warningMarkers[i].rectTransform;
                    SetNormalizedPosition(rt, CellToNormalized(warnings[i]));
                    rt.sizeDelta = new Vector2(warnDiameter, warnDiameter);
                    warningMarkers[i].color = warnCol;
                }
            }
            ActiveWarningMarkerCount = activeWarnings;
        }

        public void ApplyPlayerMarkerState(int index, bool visible, Vector3 worldPos, Vector3 facingDirection, bool focused)
        {
            if (!initialized || index < 0 || index >= MaxPlayers) return;
            ApplyPlayerMarker(index, showPlayers && visible, worldPos, facingDirection, focused);
            if (focused) UpdateFocusLayerOrder(index);
        }

        public Color32 GetMapPixel(int x, int y)
        {
            if (x < 0 || x >= GridSize || y < 0 || y >= GridSize) return default;
            return pixelBuffer[y * GridSize + x];
        }

        public bool IsPlayerMarkerVisible(int index)
        {
            if (playerMarkers == null || index < 0 || index >= playerMarkers.Length) return false;
            return playerMarkers[index].Root != null && playerMarkers[index].Root.gameObject.activeSelf;
        }

        private void SetupCore(GridModel grid, IReadOnlyList<GridPosition> spawns, IReadOnlyList<GridPosition> mines)
        {
            boundGrid = grid;
            if (initialized)
            {
                lastGridRevision = uint.MaxValue;
                PositionStaticMarkers(spawns, mines);
                RefreshGridTextureIfNeeded(boundGrid);
                return;
            }

            mapRect = transform as RectTransform;
            if (mapRect == null) mapRect = gameObject.AddComponent<RectTransform>();
            mapRect.anchoredPosition = bottomRightOffset;
            mapRect.sizeDelta = new Vector2(mapSize, mapSize);

            BuildProceduralSprites();

            gridTexture = new Texture2D(GridSize, GridSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "ArenaMinimapGridTexture"
            };
            runtimeTextures.Add(gridTexture);

            if (gridRawImage == null)
            {
                var bgObj = new GameObject("GridLayer", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.RawImage));
                var bgRect = (RectTransform)bgObj.transform;
                bgRect.SetParent(mapRect, false);
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
                gridRawImage = bgObj.GetComponent<UnityEngine.UI.RawImage>();
            }
            gridRawImage.texture = gridTexture;
            gridRawImage.raycastTarget = false;

            float cellPx = mapSize / GridSize;

            // Layer 1: Spawn cell white outlines
            spawnMarkers = new UnityEngine.UI.Image[MaxPlayers];
            for (int i = 0; i < MaxPlayers; i++)
            {
                spawnMarkers[i] = CreateMarkerImage("Spawn_" + i, squareOutlineSprite, cellPx, new Color(1f, 1f, 1f, 0.88f));
                spawnMarkers[i].gameObject.SetActive(false);
            }

            // Layer 2: Dynamic obstacle markers (spikes & patrols)
            int spikeCount = spikeTraps != null ? spikeTraps.Length : 0;
            spikeMarkers = new UnityEngine.UI.Image[spikeCount];
            for (int i = 0; i < spikeCount; i++)
            {
                spikeMarkers[i] = CreateMarkerImage("Spike_" + i, crossSprite, Mathf.Max(6f, cellPx * 0.72f), spikeInactiveColor);
                if (spikeTraps[i] != null)
                    SetNormalizedPosition(spikeMarkers[i].rectTransform, WorldToNormalized(spikeTraps[i].transform.position));
            }

            int patrolCount = patrolObstacles != null ? patrolObstacles.Length : 0;
            patrolMarkers = new UnityEngine.UI.Image[patrolCount];
            for (int i = 0; i < patrolCount; i++)
            {
                patrolMarkers[i] = CreateMarkerImage("Patrol_" + i, solidSprite, Mathf.Max(6f, cellPx * 0.72f), patrolObstacleColor);
            }

            // Layer 3: Fixed gold mines + warning pulse rings
            minePulseRings = new UnityEngine.UI.Image[MaxMines];
            mineMarkers = new UnityEngine.UI.Image[MaxMines];
            for (int i = 0; i < MaxMines; i++)
            {
                minePulseRings[i] = CreateMarkerImage("MinePulse_" + i, ringSprite, cellPx * 1.5f, mineWarningColor);
                minePulseRings[i].enabled = false;
                mineMarkers[i] = CreateMarkerImage("Mine_" + i, diamondSprite, Mathf.Max(7f, cellPx * 0.90f), mineIdleColor);
                mineMarkers[i].gameObject.SetActive(false);
            }

            // Layer 4: Active coins & 1.5s warning rings
            coinMarkers = new UnityEngine.UI.Image[MaxCoinMarkers];
            for (int i = 0; i < MaxCoinMarkers; i++)
            {
                coinMarkers[i] = CreateMarkerImage("Coin_" + i, circleSprite, Mathf.Max(5f, cellPx * 0.58f), coinColor);
                coinMarkers[i].enabled = false;
            }

            warningMarkers = new UnityEngine.UI.Image[MaxWarningMarkers];
            for (int i = 0; i < MaxWarningMarkers; i++)
            {
                warningMarkers[i] = CreateMarkerImage("Warning_" + i, ringSprite, Mathf.Max(6f, cellPx * 0.72f), mineWarningColor);
                warningMarkers[i].enabled = false;
            }

            // Layer 5: 4 Players (Ring + Dot + Facing Arrow)
            playerMarkers = new PlayerMarker[MaxPlayers];
            for (int i = 0; i < MaxPlayers; i++)
            {
                playerMarkers[i] = CreatePlayerMarker(i, cellPx);
            }

            initialized = true;
            lastGridRevision = uint.MaxValue;
            PositionStaticMarkers(spawns, mines);
            RefreshGridTextureIfNeeded(boundGrid);
        }

        private void PositionStaticMarkers(IReadOnlyList<GridPosition> spawns, IReadOnlyList<GridPosition> mines)
        {
            if (spawnMarkers != null)
            {
                int count = spawns != null ? Mathf.Min(spawns.Count, spawnMarkers.Length) : 0;
                for (int i = 0; i < spawnMarkers.Length; i++)
                {
                    bool active = i < count;
                    spawnMarkers[i].gameObject.SetActive(active);
                    if (active) SetNormalizedPosition(spawnMarkers[i].rectTransform, CellToNormalized(spawns[i]));
                }
            }

            if (mineMarkers != null)
            {
                int count = mines != null ? Mathf.Min(mines.Count, mineMarkers.Length) : 0;
                for (int i = 0; i < mineMarkers.Length; i++)
                {
                    bool active = i < count;
                    mineMarkers[i].gameObject.SetActive(active);
                    minePulseRings[i].gameObject.SetActive(active);
                    if (active)
                    {
                        Vector2 pos = CellToNormalized(mines[i]);
                        SetNormalizedPosition(mineMarkers[i].rectTransform, pos);
                        SetNormalizedPosition(minePulseRings[i].rectTransform, pos);
                    }
                }
            }
        }

        private void ScanSceneObstacles(MatchCoordinator coordinator)
        {
            System.Array.Clear(staticObstacleMask, 0, staticObstacleMask.Length);
            var spikes = new List<TimedSpikeTrap>();
            var patrols = new List<LinearPatrolObstacle>();
            var roots = coordinator.gameObject.scene.GetRootGameObjects();

            for (int r = 0; r < roots.Length; r++)
            {
                spikes.AddRange(roots[r].GetComponentsInChildren<TimedSpikeTrap>(true));
                patrols.AddRange(roots[r].GetComponentsInChildren<LinearPatrolObstacle>(true));

                var lethalObstacles = roots[r].GetComponentsInChildren<LethalObstacle>(true);
                for (int i = 0; i < lethalObstacles.Length; i++)
                {
                    var lo = lethalObstacles[i];
                    if (lo == null || lo.GetComponentInParent<TimedSpikeTrap>() != null || lo.GetComponentInParent<LinearPatrolObstacle>() != null)
                        continue;
                    if (coordinator.Grid != null)
                    {
                        GridPosition cell = coordinator.Grid.WorldToCell(lo.transform.position);
                        if (cell.X >= 0 && cell.X < GridSize && cell.Y >= 0 && cell.Y < GridSize)
                            staticObstacleMask[cell.X, cell.Y] = true;
                    }
                }
            }

            spikeTraps = spikes.ToArray();
            patrolObstacles = patrols.ToArray();
        }

        private void RefreshGridTextureIfNeeded(GridModel grid)
        {
            if (grid == null || gridTexture == null) return;
            if (grid.Revision == lastGridRevision &&
                showTerritory == lastRenderedTerritory &&
                showObstacles == lastRenderedObstacles)
            {
                return;
            }

            if (grid.Revision != lastGridRevision && showTerritory)
            {
                for (int p = 0; p < MaxPlayers; p++)
                    grid.CopyConnectedTo(p, connectedBuffers[p]);
            }

            Color32 bg32 = backgroundColor;
            Color32 obs32 = staticObstacleColor;

            for (int y = 0; y < GridSize; y++)
            {
                int rowOffset = y * GridSize;
                for (int x = 0; x < GridSize; x++)
                {
                    if (showObstacles && staticObstacleMask[x, y])
                    {
                        pixelBuffer[rowOffset + x] = obs32;
                        continue;
                    }

                    var cell = new GridPosition(x, y);
                    if (!showTerritory || grid.IsBlocked(cell))
                    {
                        pixelBuffer[rowOffset + x] = bg32;
                        continue;
                    }

                    int? owner = grid.OwnerAt(cell);
                    if (!owner.HasValue || owner.Value < 0 || owner.Value >= MaxPlayers)
                    {
                        pixelBuffer[rowOffset + x] = bg32;
                        continue;
                    }

                    int id = owner.Value;
                    bool isConnected = connectedBuffers[id][x, y];
                    float brightness = isConnected ? connectedBrightness : disconnectedBrightness;
                    Color baseCol = playerColors[id];
                    pixelBuffer[rowOffset + x] = new Color(
                        baseCol.r * brightness,
                        baseCol.g * brightness,
                        baseCol.b * brightness,
                        0.92f);
                }
            }

            gridTexture.SetPixels32(pixelBuffer);
            gridTexture.Apply(false, false);

            lastGridRevision = grid.Revision;
            lastRenderedTerritory = showTerritory;
            lastRenderedObstacles = showObstacles;
            TextureRefreshCount++;
        }

        private void RefreshSpawnsVisibility()
        {
            if (spawnMarkers == null) return;
            for (int i = 0; i < spawnMarkers.Length; i++)
                if (spawnMarkers[i].gameObject.activeSelf)
                    spawnMarkers[i].enabled = showSpawns;
        }

        private void RefreshDynamicObstacles()
        {
            if (spikeTraps != null && spikeMarkers != null)
            {
                for (int i = 0; i < spikeTraps.Length && i < spikeMarkers.Length; i++)
                {
                    TimedSpikeTrap trap = spikeTraps[i];
                    bool active = showObstacles && trap != null && trap.gameObject.activeInHierarchy;
                    spikeMarkers[i].enabled = active;
                    if (!active) continue;
                    spikeMarkers[i].color = trap.IsActive ? spikeActiveColor : spikeInactiveColor;
                }
            }

            if (patrolObstacles != null && patrolMarkers != null)
            {
                for (int i = 0; i < patrolObstacles.Length && i < patrolMarkers.Length; i++)
                {
                    LinearPatrolObstacle patrol = patrolObstacles[i];
                    bool active = showObstacles && patrol != null && patrol.gameObject.activeInHierarchy;
                    patrolMarkers[i].enabled = active;
                    if (!active) continue;
                    SetNormalizedPosition(patrolMarkers[i].rectTransform, WorldToNormalized(patrol.transform.position));
                }
            }
        }

        private void ApplyPlayerMarker(int index, bool visible, Vector3 worldPos, Vector3 facingDirection, bool focused)
        {
            PlayerMarker marker = playerMarkers[index];
            if (marker.Root.gameObject.activeSelf != visible)
                marker.Root.gameObject.SetActive(visible);
            if (!visible) return;

            SetNormalizedPosition(marker.Root, WorldToNormalized(worldPos));

            float cellPx = mapSize / GridSize;
            float dotSize = focused ? Mathf.Max(8f, cellPx * 0.84f) : Mathf.Max(6.5f, cellPx * 0.68f);
            float ringSize = focused ? dotSize + 4.5f : dotSize + 2.8f;

            marker.Dot.rectTransform.sizeDelta = new Vector2(dotSize, dotSize);
            marker.Dot.color = playerColors[index];

            marker.Ring.rectTransform.sizeDelta = new Vector2(ringSize, ringSize);
            marker.Ring.color = focused ? Color.white : new Color(0.95f, 0.97f, 1f, 0.72f);

            marker.FacingArrow.enabled = focused;
            if (focused)
            {
                Vector2 dir = new Vector2(facingDirection.x, facingDirection.z);
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
                else dir.Normalize();

                float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                float arrowDist = ringSize * 0.5f + 2.5f;
                marker.FacingArrow.rectTransform.anchoredPosition = dir * arrowDist;
                marker.FacingArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            }
        }

        private void UpdateFocusLayerOrder(int focusSlot)
        {
            if (focusSlot == lastFocusedSlot) return;
            lastFocusedSlot = focusSlot;
            if (playerMarkers != null && focusSlot >= 0 && focusSlot < playerMarkers.Length)
                playerMarkers[focusSlot].Root.SetAsLastSibling();
        }

        private void SetNormalizedPosition(RectTransform rt, Vector2 normalized)
        {
            rt.anchoredPosition = new Vector2(normalized.x * mapSize, normalized.y * mapSize);
        }

        private UnityEngine.UI.Image CreateMarkerImage(string objectName, Sprite sprite, float size, Color color)
        {
            var go = markerTemplate != null
                ? Instantiate(markerTemplate.gameObject, mapRect)
                : new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.name = objectName;
            go.SetActive(true);
            var rt = (RectTransform)go.transform;
            rt.SetParent(mapRect, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private PlayerMarker CreatePlayerMarker(int index, float cellPx)
        {
            if (playerMarkerTemplate != null && playerMarkerTemplate.HasRequiredLayers)
            {
                var template = Instantiate(playerMarkerTemplate, mapRect);
                template.name = "PlayerMarker_" + index;
                var templateRoot = (RectTransform)template.transform;
                templateRoot.anchorMin = templateRoot.anchorMax = Vector2.zero;
                templateRoot.pivot = new Vector2(0.5f, 0.5f);
                templateRoot.sizeDelta = new Vector2(cellPx, cellPx);
                template.ring.sprite = circleSprite;
                template.dot.sprite = circleSprite;
                template.arrow.sprite = triangleSprite;
                template.ring.color = Color.white;
                template.dot.color = playerColors[index];
                template.arrow.color = Color.white;
                template.ring.rectTransform.sizeDelta = new Vector2(cellPx, cellPx);
                template.dot.rectTransform.sizeDelta = new Vector2(cellPx * 0.72f, cellPx * 0.72f);
                float arrowSize = Mathf.Max(6f, cellPx * 0.55f);
                template.arrow.rectTransform.sizeDelta = new Vector2(arrowSize, arrowSize);
                template.arrow.enabled = false;
                template.gameObject.SetActive(false);
                return new PlayerMarker { Root = templateRoot, Ring = template.ring, Dot = template.dot, FacingArrow = template.arrow };
            }

            var rootObj = new GameObject("PlayerMarker_" + index, typeof(RectTransform));
            var root = (RectTransform)rootObj.transform;
            root.SetParent(mapRect, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(cellPx, cellPx);

            var ring = CreateChildIcon(root, "Ring", circleSprite, cellPx, Color.white);
            var dot = CreateChildIcon(root, "Dot", circleSprite, cellPx * 0.72f, playerColors[index]);
            var arrow = CreateChildIcon(root, "Arrow", triangleSprite, Mathf.Max(6f, cellPx * 0.55f), Color.white);
            arrow.enabled = false;

            rootObj.SetActive(false);
            return new PlayerMarker { Root = root, Ring = ring, Dot = dot, FacingArrow = arrow };
        }

        private static UnityEngine.UI.Image CreateChildIcon(RectTransform parent, string objectName, Sprite sprite, float size, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private void BuildProceduralSprites()
        {
            if (circleSprite == null) circleSprite = CreateProceduralSprite("MiniCircle", 32, (x, y, s) =>
            {
                float dx = x + 0.5f - s * 0.5f;
                float dy = y + 0.5f - s * 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float r = s * 0.46f;
                return Mathf.Clamp01(r - dist + 0.75f);
            });

            if (ringSprite == null) ringSprite = CreateProceduralSprite("MiniRing", 32, (x, y, s) =>
            {
                float dx = x + 0.5f - s * 0.5f;
                float dy = y + 0.5f - s * 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float outer = s * 0.46f;
                float inner = s * 0.30f;
                float aOut = Mathf.Clamp01(outer - dist + 0.75f);
                float aIn = Mathf.Clamp01(dist - inner + 0.75f);
                return Mathf.Min(aOut, aIn);
            });

            if (diamondSprite == null) diamondSprite = CreateProceduralSprite("MiniDiamond", 32, (x, y, s) =>
            {
                float dx = Mathf.Abs(x + 0.5f - s * 0.5f);
                float dy = Mathf.Abs(y + 0.5f - s * 0.5f);
                float r = s * 0.44f;
                return Mathf.Clamp01(r - (dx + dy) + 0.8f);
            });

            if (squareOutlineSprite == null) squareOutlineSprite = CreateProceduralSprite("MiniSquareOutline", 32, (x, y, s) =>
            {
                int b = 4;
                bool onEdge = x < b || x >= s - b || y < b || y >= s - b;
                return onEdge ? 1f : 0f;
            });

            if (crossSprite == null) crossSprite = CreateProceduralSprite("MiniCross", 32, (x, y, s) =>
            {
                float nx = (x + 0.5f) / s - 0.5f;
                float ny = (y + 0.5f) / s - 0.5f;
                if (Mathf.Abs(nx) > 0.42f || Mathf.Abs(ny) > 0.42f) return 0f;
                float d1 = Mathf.Abs(nx - ny);
                float d2 = Mathf.Abs(nx + ny);
                float d = Mathf.Min(d1, d2);
                return Mathf.Clamp01((0.14f - d) * s * 0.65f);
            });

            if (triangleSprite == null) triangleSprite = CreateProceduralSprite("MiniTriangle", 32, (x, y, s) =>
            {
                // Right-pointing equilateral-ish arrow inside [0..1]
                float u = (x + 0.5f) / s;
                float v = Mathf.Abs((y + 0.5f) / s - 0.5f);
                if (u < 0.12f || u > 0.92f) return 0f;
                float halfH = 0.42f * (0.92f - u) / 0.80f;
                return Mathf.Clamp01((halfH - v) * s * 0.8f);
            });

            if (solidSprite == null) solidSprite = CreateProceduralSprite("MiniSolid", 4, (x, y, s) => 1f);
        }

        private Sprite CreateProceduralSprite(string namePrefix, int size, System.Func<int, int, int, float> alphaFunc)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = namePrefix + "_Tex"
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(alphaFunc(x, y, size));
                    pixels[row + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            runtimeTextures.Add(tex);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = namePrefix + "_Sprite";
            runtimeSprites.Add(sprite);
            return sprite;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < runtimeSprites.Count; i++)
            {
                if (runtimeSprites[i] == null) continue;
                if (Application.isPlaying) Destroy(runtimeSprites[i]);
                else DestroyImmediate(runtimeSprites[i]);
            }
            runtimeSprites.Clear();

            for (int i = 0; i < runtimeTextures.Count; i++)
            {
                if (runtimeTextures[i] == null) continue;
                if (Application.isPlaying) Destroy(runtimeTextures[i]);
                else DestroyImmediate(runtimeTextures[i]);
            }
            runtimeTextures.Clear();
        }
    }
}
