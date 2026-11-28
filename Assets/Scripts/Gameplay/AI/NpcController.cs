using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class NpcController : MonoBehaviour
    {
        public enum Difficulty { Normal = 0, Easy = 1, Hard = 2 }
        private enum Personality { Explorer, TreasureHunter, Challenger }
        [SerializeField] private Difficulty difficulty = Difficulty.Normal;
        private readonly Personality[] personalities = new Personality[4];
        private readonly bool[] explorationPaths = new bool[4];
        private readonly float[] windup = new float[4];
        private readonly int[] reactionTarget = { -1, -1, -1, -1 };
        private readonly float[] reactionElapsed = new float[4];
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
        private readonly float[] progressDistance = { -1f, -1f, -1f, -1f };
        // 0 follows the path axis; 1/2 commits to correcting X/Z until aligned.
        private readonly int[] alignmentAxis = new int[4];
        private const float AlignmentExit = 0.1f;
        private const float ProgressEpsilon = 0.02f;
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
        private float ReactionSeconds => difficulty == Difficulty.Easy ? 0.3f : 0f;
        private float WindupDuration => Mathf.Max(difficulty == Difficulty.Hard ? 0.3f : 0.1f, windupSeconds * (difficulty == Difficulty.Easy ? 1.5f : difficulty == Difficulty.Hard ? 0.7f : 1f));
        private float ExplorerExplorationChance => difficulty == Difficulty.Easy ? 0.7f : difficulty == Difficulty.Hard ? 0.35f : 0.55f;
        private void ClearReaction(int id) { reactionTarget[id] = -1; reactionElapsed[id] = 0f; }
        private void ResetWaypointTracking(int id) { blockedSeconds[id] = 0f; progressDistance[id] = -1f; alignmentAxis[id] = 0; }
        public void ResetPath(int id) { paths[id].Clear(); pathIndices[id] = 0; retries[id] = 0f; ResetWaypointTracking(id); explorationPaths[id] = false; CancelWindup(id); }
        public bool IsWindingUp(int id) => windup[id] > 0f;
        public Vector3 WindupDirection(int id) => windupDirection[id];
        internal bool HasWaypoint(int id) => pathIndices[id] < paths[id].Count;

        public void Tick(int id, float deltaTime)
        {
            if (match.Model.Participants[id].Status != ParticipantStatus.Active) { ResetPath(id); ClearReaction(id); return; }
            var character = match.Characters[id];
            if (character.IsKnockedBack) { ResetPath(id); ClearReaction(id); return; }
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
                    if (FindAlternativePath(id, alternative) && (!found || (personalities[id] == Personality.Challenger && chasing[id]) || (personalities[id] == Personality.Explorer && Random.value < ExplorerExplorationChance)))
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
                ResetWaypointTracking(id);
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
            segment.y = 0f;
            float lateral = Mathf.Abs(segment.x) > 0.5f ? Mathf.Abs(offset.z) : Mathf.Abs(offset.x);
            bool passed = segment.sqrMagnitude > 0.01f && lateral <= ArrivalRadius && Vector3.Dot(character.transform.position - target, segment) >= 0f;
            if (offset.sqrMagnitude <= ArrivalRadius * ArrivalRadius || passed)
            {
                pathIndices[id]++;
                ResetWaypointTracking(id);
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
            Vector3 segmentStart = match.Grid.CellToWorld(pathIndices[id] > 0 ? path[pathIndices[id] - 1] : pathOrigins[id]);
            Vector3 travel = target - segmentStart;
            int mainAxis = Mathf.Abs(travel.x) > 0.5f ? 1 : Mathf.Abs(travel.z) > 0.5f ? 2 : 0;
            int correction = alignmentAxis[id];
            if (correction != 0 && Mathf.Abs(correction == 1 ? offset.x : offset.z) <= AlignmentExit) correction = 0;
            if (correction == 0)
            {
                if (mainAxis == 1 && Mathf.Abs(offset.z) > ArrivalRadius) correction = 2;
                else if (mainAxis == 2 && Mathf.Abs(offset.x) > ArrivalRadius) correction = 1;
                else if (mainAxis == 0) correction = Mathf.Abs(offset.x) >= Mathf.Abs(offset.z) ? 1 : 2;
            }
            alignmentAxis[id] = correction;
            int axis = correction != 0 ? correction : mainAxis;
            Vector3 desired = axis == 1 ? new Vector3(Mathf.Sign(offset.x) * speed, 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(offset.z) * speed);
            float beforeDistance = Mathf.Abs(offset.x) + Mathf.Abs(offset.z);
            if (progressDistance[id] < 0f) progressDistance[id] = beforeDistance;
            character.MoveWithVelocity(desired, deltaTime);
            Vector3 remaining = target - character.transform.position;
            float remainingDistance = Mathf.Abs(remaining.x) + Mathf.Abs(remaining.z);
            if (remainingDistance <= progressDistance[id] - ProgressEpsilon)
            {
                progressDistance[id] = remainingDistance;
                blockedSeconds[id] = 0f;
            }
            else if (desired.sqrMagnitude > 0.04f) blockedSeconds[id] += Mathf.Max(0f, deltaTime);
            if (blockedSeconds[id] >= BlockedTimeout)
            {
                match.Telemetry?.NpcEvent(id, "blocked_retry"); ResetPath(id); retries[id] = 0.25f;
            }
        }

        public void CancelAllWindups()
        {
            for (int i = 0; i < windup.Length; i++) { CancelWindup(i); ClearReaction(i); }
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
            if (!match.Combat.CanAttack(id)) { ClearReaction(id); return false; }
            Vector3 origin = match.Characters[id].transform.position;
            Vector3 direction = Vector3.zero;
            float nearestSquared = match.Combat.AttackRange * match.Combat.AttackRange;
            int target = -1;
            for (int i = 0; i < match.Characters.Length; i++)
            {
                if (i == id || !match.Model.Participants[i].IsParticipating || match.Model.Participants[i].Status != ParticipantStatus.Active ||
                    (match.IsNetworkMovementTest && !match.NetworkController.IsSlotActive(i))) continue;
                Vector3 delta = match.Characters[i].transform.position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > nearestSquared) continue;
                nearestSquared = delta.sqrMagnitude; direction = delta; target = i;
            }
            if (target < 0) { ClearReaction(id); return false; }
            if (reactionTarget[id] != target) { reactionTarget[id] = target; reactionElapsed[id] = 0f; }
            if (reactionElapsed[id] < ReactionSeconds)
            {
                reactionElapsed[id] += Mathf.Max(0f, deltaTime);
                if (reactionElapsed[id] < ReactionSeconds) return false;
            }
            ClearReaction(id);
            windupDirection[id] = direction.sqrMagnitude < 0.000001f ? Vector3.forward : direction.normalized;
            windup[id] = WindupDuration;
            match.Characters[id].SetAttackAim(true, windupDirection[id], match.Combat.AttackRange, match.Combat.AttackAngle);
            if (match.IsNetworkMovementTest && Mirror.NetworkServer.active && match.NetworkController != null) match.NetworkController.SendCombatState();
            return true;
        }

        private bool FindAlternativePath(int id, List<GridPosition> path)
        {
            var start = match.Grid.WorldToCell(match.Characters[id].transform.position);
            var visited = new bool[match.Model.Grid.Width, match.Model.Grid.Height];
            var parent = new GridPosition[match.Model.Grid.Width, match.Model.Grid.Height];
            var distance = new int[match.Model.Grid.Width, match.Model.Grid.Height];
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
                    if (!match.Model.Grid.Contains(candidate) || visited[candidate.X, candidate.Y] || match.IsOtherSpawn(id, candidate) || !match.CanTraverse(cell, candidate) || match.IsBlocked(candidate)) continue;
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
            var visited = new bool[match.Model.Grid.Width, match.Model.Grid.Height];
            var parent = new GridPosition[match.Model.Grid.Width, match.Model.Grid.Height];
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
                    if (!match.Model.Grid.Contains(candidate) || visited[candidate.X, candidate.Y] || match.IsOtherSpawn(id, candidate) || !match.CanTraverse(cell, candidate) || match.IsBlocked(candidate)) continue;
                    visited[candidate.X, candidate.Y] = true; parent[candidate.X, candidate.Y] = cell; queue.Enqueue(candidate);
                }
            }
            return false;
        }
    }
}
