using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class NpcController : MonoBehaviour
    {
        private readonly GridPosition[] waypoints = new GridPosition[4];
        private readonly bool[] hasWaypoint = new bool[4];
        private readonly float[] retries = new float[4];
        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };
        private MatchCoordinator match;

        public void Initialize(MatchCoordinator coordinator) { match = coordinator; }
        public void ResetPath(int id) { hasWaypoint[id] = false; retries[id] = 0f; }

        public void Tick(int id, float deltaTime)
        {
            if (match.Model.Participants[id].Status != ParticipantStatus.Active) return;
            match.Combat.Attack(id);
            retries[id] = Mathf.Max(0f, retries[id] - deltaTime);
            if (!hasWaypoint[id])
            {
                if (retries[id] > 0f) return;
                if (!FindNext(id, out waypoints[id])) { retries[id] = 0.25f; return; }
                hasWaypoint[id] = true;
            }
            var character = match.Characters[id];
            var target = match.Grid.CellToWorld(waypoints[id]);
            target.y = character.transform.position.y;
            Vector3 delta = Vector3.MoveTowards(character.transform.position, target, match.MoveSpeed * deltaTime) - character.transform.position;
            var before = character.transform.position;
            character.Move(delta);
            if ((character.transform.position - target).sqrMagnitude < 0.0001f) hasWaypoint[id] = false;
            else if (delta.sqrMagnitude > 0.0001f && (character.transform.position - before).sqrMagnitude < 0.000001f) { hasWaypoint[id] = false; retries[id] = 0.25f; }
        }

        private bool FindNext(int id, out GridPosition next)
        {
            var start = match.Grid.WorldToCell(match.Characters[id].transform.position);
            bool returning = !match.Model.Grid.IsConnected(id, start);
            var visited = new bool[ArenaGridView.Size, ArenaGridView.Size];
            var parent = new GridPosition[ArenaGridView.Size, ArenaGridView.Size];
            var queue = new Queue<GridPosition>();
            queue.Enqueue(start); visited[start.X, start.Y] = true;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (returning ? cell.Equals(match.Spawns[id]) : match.Coins.HasCoin(cell))
                {
                    while (!cell.Equals(start) && !parent[cell.X, cell.Y].Equals(start)) cell = parent[cell.X, cell.Y];
                    next = cell; return true;
                }
                for (int i = 0; i < 4; i++)
                {
                    var candidate = new GridPosition(cell.X + Dx[i], cell.Y + Dy[i]);
                    if (!match.Model.Grid.Contains(candidate) || visited[candidate.X, candidate.Y] || match.IsOtherSpawn(id, candidate) || match.IsBlocked(candidate)) continue;
                    visited[candidate.X, candidate.Y] = true; parent[candidate.X, candidate.Y] = cell; queue.Enqueue(candidate);
                }
            }
            next = default; return false;
        }
    }
}
