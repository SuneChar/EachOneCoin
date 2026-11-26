using System;
using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    // Coins come only from the fixed gold mines. One mine at a time delivers two coins on a fixed timetable,
    // and every delivery is announced by warning markers one and a half seconds in advance.
    public sealed class CoinSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject coinModel;
        [SerializeField] private Transform coinRoot;
        [SerializeField] private Transform warningRoot;
        private enum CoinKind { Mine, Dropped }
        private sealed class Slot { public GridPosition Position; public CoinKind Kind; public GameObject Coin; }
        private sealed class CoinEvent { public int Index; public int MineIndex; public int[] MineOrder; public GridPosition[] Cells; public GameObject[] Markers; }
        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<CoinEvent> pending = new List<CoinEvent>();
        private readonly List<GridPosition> candidates = new List<GridPosition>();
        private readonly List<GridPosition> picked = new List<GridPosition>();
        private readonly List<int> mineOrder = new List<int>();
        private readonly List<int> restMines = new List<int>();
        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };
        private static readonly GridPosition[] minePoints = { new GridPosition(10, 5), new GridPosition(10, 15), new GridPosition(5, 10), new GridPosition(15, 10) };
        private static readonly string[] MineNames = { "南侧", "北侧", "西侧", "东侧" };
        private static readonly Color BaseMineColor = new Color(0.75f, 0.45f, 0.06f);
        private static readonly Color PulseMineColor = new Color(1f, 0.93f, 0.35f);
        private const int MinRadius = 2;
        private const int MaxRadius = 5;
        private const int MinDispersion = 2;
        private MatchCoordinator match;
        private double elapsed, duration;
        private int nextWarning, nextSpawn, lastMineIndex = -1;
        private Renderer[] mineRenderers;
        private readonly bool[] minePulsing = new bool[4];
        private Material mineMaterial;
        private Material warningMaterial;
        private MaterialPropertyBlock mineBlock;
        private bool[,] mineConnected;
        private uint[] connectedRevision;

        public string MineName(int mineIndex) => MineNames[mineIndex];

        public void Initialize(MatchCoordinator coordinator)
        {
            match = coordinator;
            int count = match.Model.Participants.Count;
            mineConnected = new bool[minePoints.Length, count];
            connectedRevision = new uint[count];
            for (int i = 0; i < count; i++) connectedRevision[i] = uint.MaxValue;
            duration = match.Model.RemainingTime;
            elapsed = 0d; nextWarning = 0; nextSpawn = 0; lastMineIndex = -1;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mineMaterial = new Material(shader);
            mineMaterial.color = BaseMineColor;
            Physics.SyncTransforms();
            foreach (var point in minePoints)
                if (match.IsSpawn(point) || match.IsObstructed(point))
                    throw new InvalidOperationException($"Fixed mine cell ({point.X}, {point.Y}) is obstructed.");
            match.Rules.ConfigureMines(minePoints);
            mineBlock = new MaterialPropertyBlock();
            mineRenderers = new Renderer[minePoints.Length];
            for (int i = 0; i < minePoints.Length; i++)
            {
                var mine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mine.name = "Gold Mine " + MineNames[i];
                mine.transform.SetParent(coinRoot, false);
                mine.transform.position = match.Grid.CellToWorld(minePoints[i]) + Vector3.up * 0.2f;
                mine.transform.localScale = new Vector3(0.8f, 0.2f, 0.8f);
                mine.GetComponent<Collider>().enabled = false;
                var blocker = mine.AddComponent<BoxCollider>();
                blocker.center = new Vector3(0f, 1.5f, 0f);
                blocker.size = new Vector3(1.25f, 5f, 1.25f);
                mineRenderers[i] = mine.GetComponent<Renderer>();
                mineRenderers[i].sharedMaterial = mineMaterial;
                SetMineColor(i, BaseMineColor);
            }
            Physics.SyncTransforms();
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Coin != null) slots[i].Coin.transform.Rotate(Vector3.up, 90f * deltaTime, Space.World);
            UpdateWarningMarkers();
            UpdateMinePulse();
        }

        // The host stops ticking once the match ends, so leftover warnings are dropped here instead.
        private void LateUpdate()
        {
            if (pending.Count > 0 && (match == null || match.Model == null || match.Model.Status != MatchStatus.Playing)) ClearWarnings();
        }

        // Called once after this frame's gameplay, before the match timeout is settled.
        public void AdvanceMatch(float deltaTime)
        {
            if (deltaTime <= 0f || match.Model.Status != MatchStatus.Playing) return;
            double end = Math.Min(duration, elapsed + deltaTime);
            while (elapsed < end)
            {
                double boundary = end;
                if (nextWarning < CoinSpawnSchedule.EventCount) boundary = Math.Min(boundary, CoinSpawnSchedule.WarningStart(nextWarning));
                if (nextSpawn < CoinSpawnSchedule.EventCount) boundary = Math.Min(boundary, CoinSpawnSchedule.EventTime(nextSpawn));
                elapsed = boundary;
                if (elapsed >= duration) break;
                if (nextWarning < CoinSpawnSchedule.EventCount && elapsed >= CoinSpawnSchedule.WarningStart(nextWarning))
                {
                    var warned = PrepareEvent(nextWarning, true);
                    if (warned != null) pending.Add(warned);
                    nextWarning++;
                    continue;
                }
                if (nextSpawn < CoinSpawnSchedule.EventCount && elapsed >= CoinSpawnSchedule.EventTime(nextSpawn))
                {
                    SpawnEvent(nextSpawn);
                    nextSpawn++;
                }
            }
            if (elapsed >= duration) ClearWarnings();
        }

        // True while the given cell holds a coin or is announced as the next coin cell.
        public bool IsCoinTarget(GridPosition position) => HasCoin(position) || HasWarning(position);

        public bool HasCoin(GridPosition position)
        {
            foreach (var slot in slots)
                if (slot.Coin != null && slot.Position.Equals(position)) return true;
            return false;
        }

        // Describes the next delivery for the HUD; the mine stays unknown until its warning starts.
        public bool TryDescribeNextEvent(out int mineIndex, out float secondsUntilSpawn, out bool warningActive)
        {
            mineIndex = -1;
            secondsUntilSpawn = 0f;
            warningActive = false;
            if (match == null || nextSpawn >= CoinSpawnSchedule.EventCount) return false;
            secondsUntilSpawn = Mathf.Max(0f, (float)(CoinSpawnSchedule.EventTime(nextSpawn) - elapsed));
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].Index != nextSpawn) continue;
                mineIndex = pending[i].MineIndex;
                warningActive = true;
                break;
            }
            return true;
        }

        // A mine counts as connected when any cell next to it belongs to the participant's connected region.
        public bool IsMineConnected(int mineIndex, int participantId)
        {
            if (match == null || mineConnected == null) return false;
            if (mineIndex < 0 || mineIndex >= minePoints.Length) return false;
            if (participantId < 0 || participantId >= connectedRevision.Length) return false;
            var grid = match.Model.Grid;
            if (connectedRevision[participantId] != grid.Revision)
            {
                connectedRevision[participantId] = grid.Revision;
                for (int mine = 0; mine < minePoints.Length; mine++) mineConnected[mine, participantId] = ComputeMineConnected(mine, participantId);
            }
            return mineConnected[mineIndex, participantId];
        }

        public void TryCollect(ArenaCharacter character)
        {
            var position = character.transform.position;
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                var slot = slots[i];
                if (slot.Coin == null) continue;
                var delta = position - match.Grid.CellToWorld(slot.Position); delta.y = 0f;
                if (delta.sqrMagnitude > 0.16f) continue;
                if (!match.Rules.TryCollectCoin(character.Id, slot.Position)) continue;
                Destroy(slot.Coin);
                slots.RemoveAt(i);
                match.CoinCollected(character.Id);
            }
        }

        public void Drop(GridPosition origin, int count)
        {
            var position = match.FindSafeDrop(origin);
            for (int i = 0; i < count; i++) Spawn(position, CoinKind.Dropped);
        }

        private bool ComputeMineConnected(int mineIndex, int participantId)
        {
            var origin = match.Model.Mines[mineIndex];
            for (int d = 0; d < 4; d++)
            {
                var cell = new GridPosition(origin.X + Dx[d], origin.Y + Dy[d]);
                if (match.Model.Grid.Contains(cell) && match.Model.Grid.IsConnected(participantId, cell)) return true;
            }
            return false;
        }

        // A warned delivery keeps its own cells; a delivery whose warning was skipped is prepared here instead.
        private void SpawnEvent(int index)
        {
            var coinEvent = TakePending(index);
            if (coinEvent == null) coinEvent = PrepareEvent(index, false);
            if (coinEvent == null) return;
            int expected = coinEvent.Cells.Length;
            int spawned = 0;
            for (int i = 0; i < expected; i++)
            {
                if (!TryResolveCell(coinEvent, coinEvent.Cells[i], out var cell)) continue;
                Spawn(cell, CoinKind.Mine);
                spawned++;
            }
            DestroyMarkers(coinEvent);
            if (spawned < expected)
                Debug.LogWarning($"Mine coin event {index + 1} spawned {spawned} of {expected} coins.");
        }

        // Keeps the warned cell while it is still free, otherwise falls back to another mine that can host a coin.
        private bool TryResolveCell(CoinEvent coinEvent, GridPosition cell, out GridPosition resolved)
        {
            if (IsSafe(cell)) { resolved = cell; return true; }
            foreach (var mine in coinEvent.MineOrder)
            {
                if (PickCells(mine, picked) == 0) continue;
                resolved = picked[0];
                return true;
            }
            resolved = default;
            return false;
        }

        private CoinEvent PrepareEvent(int index, bool withMarkers)
        {
            var order = BuildMineOrder();
            int chosenMine = -1;
            GridPosition[] chosenCells = null;
            for (int i = 0; i < order.Length; i++)
            {
                int count = PickCells(order[i], picked);
                if (count == 0) continue;
                if (chosenMine < 0) { chosenMine = order[i]; chosenCells = picked.ToArray(); }
                if (count < CoinSpawnSchedule.CoinsPerEvent) continue;
                chosenMine = order[i];
                chosenCells = picked.ToArray();
                break;
            }
            if (chosenMine < 0)
            {
                Debug.LogWarning($"No safe cell around any mine for coin event {index + 1}; the event was skipped.");
                return null;
            }
            lastMineIndex = chosenMine;
            var coinEvent = new CoinEvent { Index = index, MineIndex = chosenMine, MineOrder = order, Cells = chosenCells };
            if (!withMarkers) return coinEvent;
            coinEvent.Markers = new GameObject[chosenCells.Length];
            for (int i = 0; i < chosenCells.Length; i++) coinEvent.Markers[i] = CreateWarningMarker(chosenCells[i]);
            return coinEvent;
        }

        // The primary mine never repeats the previous one; the rest keep a random order so a blocked mine can stand in.
        private int[] BuildMineOrder()
        {
            mineOrder.Clear();
            mineOrder.Add(CoinSpawnPolicy.SelectMine(minePoints.Length, lastMineIndex, UnityEngine.Random.Range(0, CoinSpawnPolicy.CandidateCount(minePoints.Length, lastMineIndex))));
            restMines.Clear();
            for (int i = 0; i < minePoints.Length; i++)
                if (i != mineOrder[0]) restMines.Add(i);
            while (restMines.Count > 0)
            {
                int pick = UnityEngine.Random.Range(0, restMines.Count);
                mineOrder.Add(restMines[pick]);
                restMines.RemoveAt(pick);
            }
            return mineOrder.ToArray();
        }

        // Prefers two separated cells in different quadrants, then two non-neighbouring cells, then a single cell.
        private int PickCells(int mineIndex, List<GridPosition> output)
        {
            output.Clear();
            var origin = match.Model.Mines[mineIndex];
            candidates.Clear();
            for (int radius = MinRadius; radius <= MaxRadius; radius++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int dy = radius - Math.Abs(dx);
                    var a = new GridPosition(origin.X + dx, origin.Y + dy);
                    if (IsSafe(a)) candidates.Add(a);
                    if (dy == 0) continue;
                    var b = new GridPosition(origin.X + dx, origin.Y - dy);
                    if (IsSafe(b)) candidates.Add(b);
                }
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < candidates.Count; i++)
                    for (int j = i + 1; j < candidates.Count; j++)
                    {
                        bool spread = pass == 0
                            ? CoinSpawnPolicy.AreDispersed(origin, candidates[i], candidates[j], MinDispersion)
                            : CoinSpawnPolicy.IsApart(candidates[i], candidates[j], MinDispersion);
                        if (!spread) continue;
                        output.Add(candidates[i]);
                        output.Add(candidates[j]);
                        return 2;
                    }
            }
            if (candidates.Count == 0) return 0;
            output.Add(candidates[0]);
            return 1;
        }

        private bool IsSafe(GridPosition position)
        {
            var grid = match.Model.Grid;
            return grid.Contains(position) && !match.IsSpawn(position) && !grid.IsBlocked(position)
                && !match.IsObstructed(position) && !HasCoin(position) && !HasWarning(position);
        }

        private bool HasWarning(GridPosition position)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                var cells = pending[i].Cells;
                for (int c = 0; c < cells.Length; c++)
                    if (cells[c].Equals(position)) return true;
            }
            return false;
        }

        private CoinEvent TakePending(int index)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].Index != index) continue;
                var coinEvent = pending[i];
                pending.RemoveAt(i);
                return coinEvent;
            }
            return null;
        }

        private void ClearWarnings()
        {
            for (int i = 0; i < pending.Count; i++) DestroyMarkers(pending[i]);
            pending.Clear();
            for (int mine = 0; mine < minePulsing.Length; mine++)
            {
                if (!minePulsing[mine]) continue;
                minePulsing[mine] = false;
                SetMineColor(mine, BaseMineColor);
            }
        }

        private void DestroyMarkers(CoinEvent coinEvent)
        {
            if (coinEvent.Markers == null) return;
            for (int i = 0; i < coinEvent.Markers.Length; i++)
                if (coinEvent.Markers[i] != null) Destroy(coinEvent.Markers[i]);
            coinEvent.Markers = null;
        }

        private GameObject CreateWarningMarker(GridPosition cell)
        {
            var marker = new GameObject("Coin Warning");
            marker.transform.SetParent(warningRoot != null ? warningRoot : coinRoot, false);
            marker.transform.position = match.Grid.CellToWorld(cell);
            AddWarningPart(marker.transform, "Coin Warning Disc", new Vector3(0f, 0.02f, 0f), new Vector3(0.85f, 0.01f, 0.85f));
            AddWarningPart(marker.transform, "Coin Warning Pillar", new Vector3(0f, 1.1f, 0f), new Vector3(0.14f, 1.1f, 0.14f));
            return marker;
        }

        // A flat disc marks the cell while the pillar keeps the spot readable from the arena camera.
        private void AddWarningPart(Transform parent, string partName, Vector3 localPosition, Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var collider = part.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = WarningMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private Material WarningMaterial()
        {
            if (warningMaterial != null) return warningMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            warningMaterial = new Material(shader);
            if (warningMaterial.HasProperty("_Surface")) warningMaterial.SetFloat("_Surface", 1f);
            warningMaterial.SetOverrideTag("RenderType", "Transparent");
            warningMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            warningMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            warningMaterial.SetInt("_ZWrite", 0);
            warningMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            warningMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            warningMaterial.color = new Color(1f, 0.42f, 0.1f, 0.65f);
            return warningMaterial;
        }

        private void UpdateWarningMarkers()
        {
            if (pending.Count == 0) return;
            float pulse = 0.7f + 0.2f * Mathf.Sin(Time.time * 9f);
            for (int i = 0; i < pending.Count; i++)
            {
                var markers = pending[i].Markers;
                if (markers == null) continue;
                for (int m = 0; m < markers.Length; m++)
                    if (markers[m] != null) markers[m].transform.GetChild(0).localScale = new Vector3(pulse, 0.01f, pulse);
            }
        }

        private void UpdateMinePulse()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
            for (int mine = 0; mine < mineRenderers.Length; mine++)
            {
                bool announcing = false;
                for (int i = 0; i < pending.Count; i++)
                    if (pending[i].MineIndex == mine) { announcing = true; break; }
                if (!announcing)
                {
                    if (!minePulsing[mine]) continue;
                    minePulsing[mine] = false;
                    SetMineColor(mine, BaseMineColor);
                    continue;
                }
                minePulsing[mine] = true;
                SetMineColor(mine, Color.Lerp(BaseMineColor, PulseMineColor, 0.25f + 0.55f * pulse));
            }
        }

        private void SetMineColor(int mine, Color color)
        {
            mineBlock.Clear();
            mineBlock.SetColor("_BaseColor", color);
            mineBlock.SetColor("_Color", color);
            mineRenderers[mine].SetPropertyBlock(mineBlock);
        }

        private void Spawn(GridPosition position, CoinKind kind)
        {
            var coin = coinModel != null ? Instantiate(coinModel, coinRoot) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coin.name = kind == CoinKind.Mine ? "Mine Coin" : "Dropped Coin";
            coin.transform.SetParent(coinRoot);
            coin.transform.position = match.Grid.CellToWorld(position) + Vector3.up * 0.45f;
            if (coinModel == null) coin.transform.localScale = Vector3.one * 0.35f;
            foreach (var collider in coin.GetComponentsInChildren<Collider>()) collider.enabled = false;
            GroundShadow.Create(coin, 0.55f);
            slots.Add(new Slot { Position = position, Kind = kind, Coin = coin });
        }

        private void OnDestroy()
        {
            if (mineMaterial != null) Destroy(mineMaterial);
            if (warningMaterial != null) Destroy(warningMaterial);
        }
    }
}
