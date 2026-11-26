using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class NpcController : MonoBehaviour
    {
        private readonly List<GridPosition>[] paths = { new List<GridPosition>(), new List<GridPosition>(), new List<GridPosition>(), new List<GridPosition>() };
        private readonly int[] pathIndices = new int[4];
        private readonly bool[] returningPaths = new bool[4];
        private readonly float[] retries = new float[4];
        private readonly float[] blockedSeconds = new float[4];
        private const float ArrivalRadius = 0.15f;
        private const float BlockedTimeout = 0.75f;
        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };
        private MatchCoordinator match;

        public void Initialize(MatchCoordinator coordinator) { match = coordinator; }
        public void ResetPath(int id) { paths[id].Clear(); pathIndices[id] = 0; retries[id] = 0f; blockedSeconds[id] = 0f; }
        internal bool HasWaypoint(int id) => pathIndices[id] < paths[id].Count;

        public void Tick(int id, float deltaTime)
        {
            if (match.Model.Participants[id].Status != ParticipantStatus.Active) return;
            AttackNearby(id);
            var character = match.Characters[id];
            if (character.IsKnockedBack) { ResetPath(id); return; }
            retries[id] = Mathf.Max(0f, retries[id] - deltaTime);
            var cell = match.Grid.WorldToCell(character.transform.position);
            bool returning = !match.Model.Grid.IsConnected(id, cell);
            var path = paths[id];
            if (HasWaypoint(id) && (returning != returningPaths[id] || (!returning && !match.Coins.HasCoin(path[path.Count - 1])))) ResetPath(id);
            if (!HasWaypoint(id))
            {
                if (retries[id] > 0f) { character.MoveWithVelocity(Vector3.zero, deltaTime); return; }
                if (!FindPath(id, returning, path))
                {
                    match.Telemetry?.NpcEvent(id, "path_failed"); retries[id] = 0.25f;
                    character.MoveWithVelocity(Vector3.zero, deltaTime); return;
                }
                returningPaths[id] = returning;
                pathIndices[id] = 0;
            }

            if (HasWaypoint(id) && match.IsBlocked(path[pathIndices[id]]))
            {
                ResetPath(id); retries[id] = 0.25f;
                character.MoveWithVelocity(Vector3.zero, deltaTime); return;
            }
            Vector3 target = match.Grid.CellToWorld(path[pathIndices[id]]);
            target.y = character.transform.position.y;
            Vector3 offset = target - character.transform.position;
            if (offset.sqrMagnitude <= ArrivalRadius * ArrivalRadius)
            {
                pathIndices[id]++;
                blockedSeconds[id] = 0f;
                if (!HasWaypoint(id)) { character.MoveWithVelocity(Vector3.zero, deltaTime); return; }
                target = match.Grid.CellToWorld(path[pathIndices[id]]);
                target.y = character.transform.position.y;
                offset = target - character.transform.position;
            }

            float speed = match.MoveSpeed;
            bool needsBraking = pathIndices[id] == path.Count - 1;
            if (!needsBraking)
            {
                var next = match.Grid.CellToWorld(path[pathIndices[id] + 1]);
                var incoming = target - match.Grid.CellToWorld(pathIndices[id] > 0 ? path[pathIndices[id] - 1] : cell);
                var outgoing = next - target;
                needsBraking = Vector3.Dot(incoming.normalized, outgoing.normalized) < 0.99f;
            }
            if (needsBraking) speed = Mathf.Min(speed, Mathf.Sqrt(2f * match.MovementDeceleration * Mathf.Max(0f, offset.magnitude - ArrivalRadius * 0.5f)) / character.RegionSpeedMultiplier);
            Vector3 desired = offset.normalized * speed;
            Vector3 before = character.transform.position;
            character.MoveWithVelocity(desired, deltaTime);
            float moved = (character.transform.position - before).magnitude;
            if (desired.sqrMagnitude > 0.04f && moved < 0.01f * deltaTime)
                blockedSeconds[id] += deltaTime;
            else blockedSeconds[id] = 0f;
            if (blockedSeconds[id] >= BlockedTimeout)
            {
                match.Telemetry?.NpcEvent(id, "blocked_retry"); ResetPath(id); retries[id] = 0.25f;
            }
        }

        private void AttackNearby(int id)
        {
            Vector3 origin = match.Characters[id].transform.position;
            Vector3 direction = Vector3.zero;
            float nearestSquared = match.Combat.AttackRange * match.Combat.AttackRange;
            bool found = false;
            for (int i = 0; i < match.Characters.Length; i++)
            {
                if (i == id || match.Model.Participants[i].Status != ParticipantStatus.Active) continue;
                Vector3 delta = match.Characters[i].transform.position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > nearestSquared) continue;
                nearestSquared = delta.sqrMagnitude;
                direction = delta;
                found = true;
            }
            if (!found) return;
            if (direction.sqrMagnitude < 0.000001f) direction = Vector3.forward;
            match.Combat.Attack(id, direction);
        }

        private bool FindPath(int id, bool returning, List<GridPosition> path)
        {
            path.Clear();
            var start = match.Grid.WorldToCell(match.Characters[id].transform.position);
            var visited = new bool[ArenaGridView.Size, ArenaGridView.Size];
            var parent = new GridPosition[ArenaGridView.Size, ArenaGridView.Size];
            var queue = new Queue<GridPosition>();
            queue.Enqueue(start); visited[start.X, start.Y] = true;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (returning ? cell.Equals(match.Spawns[id]) : match.Coins.HasCoin(cell))
                {
                    while (!cell.Equals(start)) { path.Add(cell); cell = parent[cell.X, cell.Y]; }
                    path.Reverse();
                    if (path.Count == 0) path.Add(start);
                    return true;
                }
                for (int i = 0; i < 4; i++)
                {
                    var candidate = new GridPosition(cell.X + Dx[i], cell.Y + Dy[i]);
                    if (!match.Model.Grid.Contains(candidate) || visited[candidate.X, candidate.Y] || match.IsOtherSpawn(id, candidate) || match.IsBlocked(candidate)) continue;
                    visited[candidate.X, candidate.Y] = true; parent[candidate.X, candidate.Y] = cell; queue.Enqueue(candidate);
                }
            }
            return false;
        }
    }
}
