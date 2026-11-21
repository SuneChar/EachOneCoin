using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class CombatController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float attackCooldown = 0.8f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.1f;
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0)] private int droppedCoins = 1;
        [SerializeField, Min(0.1f)] private float respawnSeconds = 2f;
        private readonly float[] cooldowns = new float[4];
        private readonly float[] respawns = new float[4];
        private MatchCoordinator match;

        public void Initialize(MatchCoordinator coordinator) { match = coordinator; }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < match.Characters.Length; i++)
            {
                cooldowns[i] = Mathf.Max(0f, cooldowns[i] - deltaTime);
                if (match.Model.Participants[i].Status != ParticipantStatus.Respawning) continue;
                respawns[i] -= deltaTime;
                if (respawns[i] > 0f || !match.Model.TryRespawn(i)) continue;
                match.Characters[i].SetActiveState(true);
                match.Characters[i].Teleport(match.Grid.CellToWorld(match.Spawns[i]));
            }
        }

        public void Attack(int attacker)
        {
            if (match.Model.Status != MatchStatus.Playing || match.Model.Participants[attacker].Status != ParticipantStatus.Active || cooldowns[attacker] > 0f) return;
            cooldowns[attacker] = Mathf.Max(0.1f, attackCooldown);
            int target = -1; float nearest = Mathf.Max(0.1f, attackRange);
            for (int i = 0; i < match.Characters.Length; i++)
            {
                if (i == attacker || match.Model.Participants[i].Status != ParticipantStatus.Active) continue;
                var delta = match.Characters[attacker].transform.position - match.Characters[i].transform.position; delta.y = 0f;
                float distance = delta.magnitude;
                if (distance <= nearest) { nearest = distance; target = i; }
            }
            if (target < 0 || !match.Model.TryDamage(target, Mathf.Max(1, damage), Mathf.Max(0, droppedCoins), out int dropped)) return;
            if (match.Model.Participants[target].Status != ParticipantStatus.Respawning) return;
            match.Coins.Drop(match.Grid.WorldToCell(match.Characters[target].transform.position), dropped);
            respawns[target] = Mathf.Max(0.1f, respawnSeconds);
            match.Characters[target].SetActiveState(false);
            match.Npcs.ResetPath(target);
        }
    }
}
