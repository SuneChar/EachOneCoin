using System;
using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class CoinSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject coinModel;
        [SerializeField] private Transform coinRoot;
        // Retained for existing scene data; the new schedule no longer uses per-slot warnings.
        [SerializeField] private Transform warningRoot;
        [SerializeField, Min(0.1f)] private float respawnSeconds = 2f;
        [SerializeField, Min(0.1f)] private float warningSeconds = 1f;
        private enum CoinKind { Random, Mine, Dropped, DisguisedObstacle }
        private sealed class Slot { public GridPosition Position; public CoinKind Kind; public GameObject Coin; public DisguisedObstacle Obstacle; }
        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<GridPosition> candidates = new List<GridPosition>();
        private readonly GridPosition[] minePoints = { new GridPosition(10, 10), new GridPosition(10, 5), new GridPosition(10, 15), new GridPosition(5, 10), new GridPosition(15, 10) };
        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };
        private MatchCoordinator match;
        private bool[][] connected;
        private bool[][] frameStartConnected;
        private bool[][,] connectedCells;
        private uint[] connectedRevision;
        private bool[] connectedCacheValid;
        private double elapsed, duration, nextRandom, nextMine;
        private Renderer[] mineRenderers;
        private Material mineMaterial;
        private MaterialPropertyBlock mineBlock;
        private static readonly string[] MineNames = { "中央", "南侧", "北侧", "西侧", "东侧" };
        public bool IsConnectedToMine(int mineIndex, int id) => connected != null && mineIndex >= 0 && mineIndex < connected.Length && id >= 0 && id < connected[mineIndex].Length && connected[mineIndex][id];
        public string MineName(int mineIndex) => MineNames[mineIndex];

        public void Initialize(MatchCoordinator coordinator)
        {
            match = coordinator;
            int count = match.Model.Participants.Count;
            connected = new bool[minePoints.Length][];
            frameStartConnected = new bool[minePoints.Length][];
            for (int i = 0; i < minePoints.Length; i++) { connected[i] = new bool[count]; frameStartConnected[i] = new bool[count]; }
            connectedCells = new bool[count][,];
            connectedRevision = new uint[count];
            connectedCacheValid = new bool[count];
            for (int i = 0; i < count; i++) connectedCells[i] = new bool[ArenaGridView.Size, ArenaGridView.Size];
            duration = match.Model.RemainingTime;
            elapsed = 0d; nextRandom = 5d; nextMine = 10d;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mineMaterial = new Material(shader);
            mineMaterial.color = new Color(1f, 0.66f, 0.05f);
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
                RefreshMineVisual(i);
            }
            Physics.SyncTransforms();
        }

        public void Tick(float deltaTime)
        {
            foreach (var slot in slots)
                if (slot.Coin != null) slot.Coin.transform.Rotate(Vector3.up, 90f * deltaTime, Space.World);
            UpdateDisguisedObstacleVisibility();
        }

        // Sample before movement so a connection made this frame cannot receive time from before it existed.
        public void BeginFrame()
        {
            UpdateConnectivity();
            for (int mine = 0; mine < connected.Length; mine++)
                Array.Copy(connected[mine], frameStartConnected[mine], connected[mine].Length);
        }

        // Called once after this frame's gameplay, before the match timeout is settled.
        public void AdvanceMatch(float deltaTime)
        {
            if (deltaTime <= 0f || match.Model.Status != MatchStatus.Playing) return;
            double end = Math.Min(duration, elapsed + deltaTime);
            while (elapsed < end)
            {
                double boundary = Math.Min(end, Math.Min(nextMine, nextRandom));
                UpdateConnectivity();
                for (int mine = 0; mine < connected.Length; mine++)
                {
                    for (int i = 0; i < connected[mine].Length; i++) connected[mine][i] &= frameStartConnected[mine][i];
                    if (match.Model.Mines[mine].Advance(boundary - elapsed, connected[mine]))
                    {
                        ScatterMineCoins(match.Model.Mines[mine].Position);
                        RefreshMineVisual(mine);
                    }
                }
                elapsed = boundary;
                if (elapsed >= duration) break;
                if (elapsed >= nextMine)
                {
                    for (int mine = 0; mine < match.Model.Mines.Count; mine++)
                    {
                        var cycle = match.Model.Mines[mine];
                        cycle.BeginCycle(cycle.Position);
                        RefreshMineVisual(mine);
                    }
                    nextMine += MineCycleModel.CycleSeconds;
                    UpdateConnectivity();
                    for (int mine = 0; mine < connected.Length; mine++)
                        Array.Copy(connected[mine], frameStartConnected[mine], connected[mine].Length);
                }
                if (elapsed >= nextRandom) { ReplaceRandomCoin(); nextRandom += 10d; }
            }
            UpdateConnectivity();
        }

        public void NotifyGridChanged()
        {
            if (match == null || connected == null) return;
            UpdateConnectivity();
            for (int mine = 0; mine < connected.Length; mine++)
                for (int i = 0; i < connected[mine].Length; i++)
                {
                    if (connected[mine][i]) continue;
                    frameStartConnected[mine][i] = false;
                    match.Model.Mines[mine].ResetProgress(i);
                }
        }

        private void UpdateConnectivity()
        {
            for (int i = 0; i < connectedCells.Length; i++)
            {
                for (int mine = 0; mine < connected.Length; mine++) connected[mine][i] = false;
                if (match.Model.Participants[i].Status != ParticipantStatus.Active) continue;
                if (!connectedCacheValid[i] || connectedRevision[i] != match.Model.Grid.Revision)
                {
                    match.Model.Grid.CopyConnectedTo(i, connectedCells[i]);
                    connectedRevision[i] = match.Model.Grid.Revision;
                    connectedCacheValid[i] = true;
                }
                for (int mine = 0; mine < connected.Length; mine++)
                {
                    var p = match.Model.Mines[mine].Position;
                    for (int d = 0; d < 4; d++)
                    {
                        int x = p.X + Dx[d], y = p.Y + Dy[d];
                        if (x >= 0 && y >= 0 && x < ArenaGridView.Size && y < ArenaGridView.Size && connectedCells[i][x, y]) { connected[mine][i] = true; break; }
                    }
                }
            }
        }

        private void RefreshMineVisual(int mine)
        {
            Color color = match.Model.Mines[mine].Scattered ? new Color(0.3f, 1f, 0.35f) : new Color(0.75f, 0.45f, 0.06f);
            mineBlock.Clear();
            mineBlock.SetColor("_BaseColor", color);
            mineBlock.SetColor("_Color", color);
            mineRenderers[mine].SetPropertyBlock(mineBlock);
        }

        private bool IsSafe(GridPosition p)
        {
            return match.Model.Grid.Contains(p) && !match.IsSpawn(p) && !match.Model.Grid.IsBlocked(p) && !match.IsObstructed(p) && !HasCoin(p);
        }

        public void UpdateDisguisedObstacleVisibility()
        {
            if (match == null || match.Characters == null) return;
            foreach (var slot in slots)
            {
                var obstacle = slot.Obstacle;
                if (obstacle == null || obstacle.Consumed) continue;
                bool near = false;
                for (int i = 0; i < match.Characters.Length; i++)
                {
                    if (match.Model.Participants[i].Status != ParticipantStatus.Active) continue;
                    var cell = match.Grid.WorldToCell(match.Characters[i].transform.position);
                    if (Math.Abs(cell.X - slot.Position.X) + Math.Abs(cell.Y - slot.Position.Y) <= 2) { near = true; break; }
                }
                obstacle.SetRevealed(near);
            }
        }

        public bool IsRevealedObstacle(GridPosition position)
        {
            foreach (var slot in slots)
                if (slot.Obstacle != null && !slot.Obstacle.Consumed && slot.Obstacle.Revealed && slot.Position.Equals(position)) return true;
            return false;
        }

        public void RemoveDisguisedObstacle(DisguisedObstacle obstacle)
        {
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (slots[i].Obstacle != obstacle) continue;
                slots.RemoveAt(i);
                Destroy(obstacle.gameObject);
                return;
            }
        }

        private void ReplaceRandomCoin()
        {
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (slots[i].Kind != CoinKind.Random && slots[i].Kind != CoinKind.DisguisedObstacle) continue;
                if (slots[i].Obstacle != null) slots[i].Obstacle.Expire();
                if (slots[i].Coin != null) Destroy(slots[i].Coin);
                slots.RemoveAt(i);
            }
            candidates.Clear();
            for (int x = 0; x < ArenaGridView.Size; x++) for (int y = 0; y < ArenaGridView.Size; y++)
            {
                var p = new GridPosition(x, y);
                if (IsSafe(p)) candidates.Add(p);
            }
            if (candidates.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, candidates.Count);
                Spawn(candidates[index], CoinKind.Random);
                candidates.RemoveAt(index);
            }
            if (candidates.Count > 0) Spawn(candidates[UnityEngine.Random.Range(0, candidates.Count)], CoinKind.DisguisedObstacle);
            UpdateDisguisedObstacleVisibility();
        }

        private void ScatterMineCoins(GridPosition origin)
        {
            int remaining = 3;
            for (int radius = 1; radius < ArenaGridView.Size * 2 && remaining > 0; radius++)
            {
                candidates.Clear();
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int dy = radius - Math.Abs(dx);
                    var a = new GridPosition(origin.X + dx, origin.Y + dy);
                    if (IsSafe(a)) candidates.Add(a);
                    if (dy == 0) continue;
                    var b = new GridPosition(origin.X + dx, origin.Y - dy);
                    if (IsSafe(b)) candidates.Add(b);
                }
                while (candidates.Count > 0 && remaining > 0)
                {
                    int index = UnityEngine.Random.Range(0, candidates.Count);
                    Spawn(candidates[index], CoinKind.Mine);
                    candidates.RemoveAt(index);
                    remaining--;
                }
            }
            if (remaining > 0) Debug.LogWarning("Not enough safe cells for all mine coins.");
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
                if (slot.Kind == CoinKind.DisguisedObstacle)
                {
                    if (slot.Obstacle != null) character.ContactDisguisedObstacle(slot.Obstacle);
                    continue;
                }
                bool collected = slot.Kind == CoinKind.Random ? match.Rules.TryCollectRandomCoin(character.Id) : match.Rules.TryCollectCoin(character.Id, slot.Position);
                if (!collected) continue;
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

        public bool HasCoin(GridPosition position)
        {
            foreach (var slot in slots)
                if (slot.Coin != null && slot.Position.Equals(position) && (slot.Obstacle == null || (!slot.Obstacle.Consumed && !slot.Obstacle.Revealed))) return true;
            return false;
        }

        public bool IsMineApproach(GridPosition position)
        {
            foreach (var mine in match.Model.Mines)
                if (!mine.Scattered && Math.Abs(position.X - mine.Position.X) + Math.Abs(position.Y - mine.Position.Y) == 1) return true;
            return false;
        }

        private void Spawn(GridPosition position, CoinKind kind)
        {
            var coin = coinModel != null ? Instantiate(coinModel, coinRoot) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coin.name = kind == CoinKind.Random ? "Random Coin" : kind == CoinKind.Mine ? "Mine Coin" : kind == CoinKind.DisguisedObstacle ? "Disguised Obstacle" : "Dropped Coin";
            coin.transform.SetParent(coinRoot);
            coin.transform.position = match.Grid.CellToWorld(position) + Vector3.up * 0.45f;
            if (coinModel == null) coin.transform.localScale = Vector3.one * 0.35f;
            foreach (var collider in coin.GetComponentsInChildren<Collider>()) collider.enabled = false;
            GroundShadow.Create(coin, 0.55f);
            DisguisedObstacle obstacle = null;
            if (kind == CoinKind.DisguisedObstacle)
            {
                obstacle = coin.AddComponent<DisguisedObstacle>();
                obstacle.Initialize(this, position);
            }
            slots.Add(new Slot { Position = position, Kind = kind, Coin = coin, Obstacle = obstacle });
        }

        private void OnDestroy()
        {
            if (mineMaterial != null) Destroy(mineMaterial);
        }
    }
}
