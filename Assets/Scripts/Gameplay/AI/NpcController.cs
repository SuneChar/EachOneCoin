using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class NpcController : MonoBehaviour
    {
        private enum Personality { Explorer, TreasureHunter, Challenger }
        private readonly Personality[] personalities = new Personality[4];
        private readonly bool[] explorationPaths = new bool[4];
        private readonly float[] windup = new float[4];
        private readonly float[] chaseElapsed = new float[4];
        private readonly float[] chaseCooldown = new float[4];
        private readonly bool[] chasing = new bool[4];
        private readonly Vector3[] windupDirection = new Vector3[4];
        [SerializeField, Min(0.1f)] private float windupSeconds = 0.6f;
        private readonly List<GridPosition>[] paths = { new List<GridPosition>(), new List<GridPosition>(), new List<GridPosition>(), new List<GridPosition>() };
        private readonly GridPosition[] pathOrigins = new GridPosition[4];
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
        public void AssignPersonalities(byte npcMask)
        {
            var choices = new[] { Personality.Explorer, Personality.TreasureHunter, Personality.Challenger };
            for (int i = choices.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = choices[i]; choices[i] = choices[j]; choices[j] = temp;
            }
            int next = 0;
            for (int i = 0; i < personalities.Length; i++)
                if ((npcMask & (1 << i)) != 0) personalities[i] = choices[next++ % choices.Length];
        }
        public byte PersonalityCode(int id) => (byte)((int)personalities[id] + 1);
        public static string PersonalityName(byte code)
        {
            switch (code)
            {
                case 1: return "拓荒者";
                case 2: return "寻宝者";
                case 3: return "挑战者";
                default: return "NPC";
            }
        }
        public void ResetPath(int id) { paths[id].Clear(); pathIndices[id] = 0; retries[id] = 0f; blockedSeconds[id] = 0f; explorationPaths[id] = false; CancelWindup(id); }
        public bool IsWindingUp(int id) => windup[id] > 0f;
        public Vector3 WindupDirection(int id) => windupDirection[id];
        internal bool HasWaypoint(int id) => pathIndices[id] < paths[id].Count;

        public void Tick(int id, float deltaTime)
        {
            if (match.Model.Participants[id].Status != ParticipantStatus.Active) { ResetPath(id); return; }
            var character = match.Characters[id];
            if (character.IsKnockedBack) { ResetPath(id); return; }
            chaseCooldown[id] = Mathf.Max(0f, chaseCooldown[id] - deltaTime);
            if (chasing[id])
            {
                chaseElapsed[id] += Mathf.Max(0f, deltaTime);
                if (chaseElapsed[id] >= 5f)
                {
                    chasing[id] = false; chaseElapsed[id] = 0f; chaseCooldown[id] = 3f; ResetPath(id);
                }
            }
            if (TickAttack(id, deltaTime)) { character.MoveWithVelocity(Vector3.zero, deltaTime); return; }
            retries[id] = Mathf.Max(0f, retries[id] - deltaTime);
            var cell = match.Grid.WorldToCell(character.transform.position);
            bool returning = !match.Model.Grid.IsConnected(id, cell);
            var path = paths[id];
            if (HasWaypoint(id) && (returning != returningPaths[id] || (!returning && !explorationPaths[id] && !match.Coins.IsCoinTarget(path[path.Count - 1])))) ResetPath(id);
            bool continuedExploration = false;
        ChoosePath:
            if (!HasWaypoint(id))
            {
                if (retries[id] > 0f) { character.MoveWithVelocity(Vector3.zero, deltaTime); return; }
                pathOrigins[id] = cell;
                bool found = FindPath(id, returning, path);
                explorationPaths[id] = false;
                if (!returning && (!found || personalities[id] != Personality.TreasureHunter))
                {
                    var alternative = new List<GridPosition>();
                    if (FindAlternativePath(id, alternative) && (!found || (personalities[id] == Personality.Challenger && chasing[id]) || (personalities[id] == Personality.Explorer && Random.value < 0.55f)))
                    {
                        path.Clear(); path.AddRange(alternative); found = true; explorationPaths[id] = true;
                    }
                }
                if (!found)
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
            Vector3 previous = match.Grid.CellToWorld(pathIndices[id] > 0 ? path[pathIndices[id] - 1] : pathOrigins[id]);
            Vector3 segment = target - previous;
            bool passed = segment.sqrMagnitude > 0.01f && Vector3.Dot(character.transform.position - target, segment) >= 0f;
            if (offset.sqrMagnitude <= ArrivalRadius * ArrivalRadius || passed)
            {
                pathIndices[id]++;
                blockedSeconds[id] = 0f;
                if (!HasWaypoint(id))
                {
                    // Exploration is continuous: pick the next goal now rather than inserting a stop frame.
                    if (explorationPaths[id] && !continuedExploration)
                    {
                        explorationPaths[id] = false;
                        continuedExploration = true;
                        cell = match.Grid.WorldToCell(character.transform.position);
                        returning = !match.Model.Grid.IsConnected(id, cell);
                        goto ChoosePath;
                    }
                    character.MoveWithVelocity(Vector3.zero, deltaTime); return;
                }
                target = match.Grid.CellToWorld(path[pathIndices[id]]);
                target.y = character.transform.position.y;
                offset = target - character.transform.position;
            }

            float speed = match.MoveSpeed;
            bool needsBraking = pathIndices[id] == path.Count - 1 && !explorationPaths[id];
            if (pathIndices[id] < path.Count - 1)
            {
                var next = match.Grid.CellToWorld(path[pathIndices[id] + 1]);
                var incoming = target - match.Grid.CellToWorld(pathIndices[id] > 0 ? path[pathIndices[id] - 1] : pathOrigins[id]);
                var outgoing = next - target;
                // CellToWorld is on the ground, while target was raised to the character's height.
                // Turning is a planar decision; including that height makes a straight line look bent.
                incoming.y = 0f;
                outgoing.y = 0f;
                needsBraking = Vector3.Dot(incoming.normalized, outgoing.normalized) < 0.99f;
            }
            if (needsBraking) speed = Mathf.Min(speed, Mathf.Sqrt(2f * match.MovementDeceleration * Mathf.Max(0f, offset.magnitude - ArrivalRadius * 0.5f)) / character.RegionSpeedMultiplier);
            Vector3 desired = offset.normalized * speed;
            // Do not switch axes just to correct a harmless residual offset near a waypoint.
            // AdvanceCardinal clears the old axis immediately, which otherwise looks like a stop.
            Vector3 segmentStart = match.Grid.CellToWorld(pathIndices[id] > 0 ? path[pathIndices[id] - 1] : pathOrigins[id]);
            Vector3 travel = target - segmentStart;
            if (Mathf.Abs(travel.x) > 0.5f && Mathf.Abs(offset.z) <= ArrivalRadius)
                desired = new Vector3(Mathf.Sign(offset.x) * speed, 0f, 0f);
            else if (Mathf.Abs(travel.z) > 0.5f && Mathf.Abs(offset.x) <= ArrivalRadius)
                desired = new Vector3(0f, 0f, Mathf.Sign(offset.z) * speed);
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

        public void CancelAllWindups()
        {
            for (int i = 0; i < windup.Length; i++) CancelWindup(i);
        }

        private void CancelWindup(int id)
        {
            if (windup[id] <= 0f) return;
            windup[id] = 0f;
            match.Characters[id].SetAttackAim(false, Vector3.zero, 0f, 0f);
            if (match.IsNetworkMovementTest && Mirror.NetworkServer.active && match.NetworkController != null) match.NetworkController.SendCombatState();
        }

        private bool TickAttack(int id, float deltaTime)
        {
            if (windup[id] > 0f)
            {
                windup[id] -= Mathf.Max(0f, deltaTime);
                if (windup[id] <= 0f)
                {
                    windup[id] = 0f;
                    match.Characters[id].SetAttackAim(false, Vector3.zero, 0f, 0f);
                    match.Combat.TryAttack(id, windupDirection[id]);
                    if (match.IsNetworkMovementTest && Mirror.NetworkServer.active && match.NetworkController != null) match.NetworkController.SendCombatState();
                }
                return true;
            }
            if (!match.Combat.CanAttack(id)) return false;
            Vector3 origin = match.Characters[id].transform.position;
            Vector3 direction = Vector3.zero;
            float nearestSquared = match.Combat.AttackRange * match.Combat.AttackRange;
            bool found = false;
            for (int i = 0; i < match.Characters.Length; i++)
            {
                if (i == id || !match.Model.Participants[i].IsParticipating || match.Model.Participants[i].Status != ParticipantStatus.Active ||
                    (match.IsNetworkMovementTest && !match.NetworkController.IsSlotActive(i))) continue;
                Vector3 delta = match.Characters[i].transform.position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > nearestSquared) continue;
                nearestSquared = delta.sqrMagnitude; direction = delta; found = true;
            }
            if (!found) return false;
            windupDirection[id] = direction.sqrMagnitude < 0.000001f ? Vector3.forward : direction.normalized;
            windup[id] = Mathf.Max(0.1f, windupSeconds);
            match.Characters[id].SetAttackAim(true, windupDirection[id], match.Combat.AttackRange, match.Combat.AttackAngle);
            if (match.IsNetworkMovementTest && Mirror.NetworkServer.active && match.NetworkController != null) match.NetworkController.SendCombatState();
            return true;
        }

        private bool FindAlternativePath(int id, List<GridPosition> path)
        {
            var start = match.Grid.WorldToCell(match.Characters[id].transform.position);
            var visited = new bool[ArenaGridView.Size, ArenaGridView.Size];
            var parent = new GridPosition[ArenaGridView.Size, ArenaGridView.Size];
            var distance = new int[ArenaGridView.Size, ArenaGridView.Size];
            var queue = new Queue<GridPosition>();
            queue.Enqueue(start); visited[start.X, start.Y] = true;
            GridPosition best = start; float bestScore = float.NegativeInfinity;
            Vector3 opponent = Vector3.zero; float nearest = float.PositiveInfinity; bool hasOpponent = false;
            if (personalities[id] == Personality.Challenger && chaseCooldown[id] <= 0f)
                for (int i = 0; i < match.Characters.Length; i++)
                {
                    if (i == id || !match.Model.Participants[i].IsParticipating || match.Model.Participants[i].Status != ParticipantStatus.Active || (match.IsNetworkMovementTest && !match.NetworkController.IsSlotActive(i))) continue;
                    var p = match.Characters[i].transform.position; float d = (p - match.Characters[id].transform.position).sqrMagnitude;
                    if (d < nearest && d <= 36f) { nearest = d; opponent = p; hasOpponent = true; }
                }
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue(); int dist = distance[cell.X, cell.Y];
                if (dist > 0 && dist <= 6)
                {
                    int? owner = match.Model.Grid.OwnerAt(cell);
                    // Prefer a modest multi-cell excursion instead of repeatedly stopping at adjacent cells.
                    float score = (owner == id ? 0f : 4f) + Mathf.Min(dist, 4) * 0.55f - Mathf.Max(0, dist - 4) * 0.3f + Random.value * 0.2f;
                    Vector3 travel = match.Grid.CellToWorld(cell) - match.Grid.CellToWorld(start);
                    Vector3 velocity = match.Characters[id].MovementVelocity;
                    if (velocity.sqrMagnitude > 0.01f) score += Vector3.Dot(travel.normalized, velocity.normalized) * 0.5f;
                    if (hasOpponent) score += 6f - Vector3.Distance(match.Grid.CellToWorld(cell), opponent);
                    if (score > bestScore) { bestScore = score; best = cell; }
                }
                if (dist >= 6) continue;
                for (int i = 0; i < 4; i++)
                {
                    var candidate = new GridPosition(cell.X + Dx[i], cell.Y + Dy[i]);
                    if (!match.Model.Grid.Contains(candidate) || visited[candidate.X, candidate.Y] || match.IsOtherSpawn(id, candidate) || match.IsBlocked(candidate)) continue;
                    visited[candidate.X, candidate.Y] = true; parent[candidate.X, candidate.Y] = cell; distance[candidate.X, candidate.Y] = dist + 1; queue.Enqueue(candidate);
                }
            }
            if (personalities[id] == Personality.Challenger)
            {
                chasing[id] = hasOpponent;
                if (!hasOpponent) chaseElapsed[id] = 0f;
            }
            if (float.IsNegativeInfinity(bestScore)) return false;
            path.Clear();
            while (!best.Equals(start)) { path.Add(best); best = parent[best.X, best.Y]; }
            path.Reverse(); return path.Count > 0;
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
                if (returning ? cell.Equals(match.Spawns[id]) : match.Coins.IsCoinTarget(cell))
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
