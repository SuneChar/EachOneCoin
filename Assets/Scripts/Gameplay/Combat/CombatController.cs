using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class CombatController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float attackCooldown = 0.8f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.1f;
        [SerializeField, Range(1f, 360f)] private float attackAngle = 100f;
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0)] private int droppedCoins = 1;
        [SerializeField, Min(0.1f)] private float respawnSeconds = 2f;
        [SerializeField, Min(0.01f)] private float attackKnockbackSeconds = 0.35f;
        private const float AttackKnockbackDistance = 6f;
        private readonly float[] cooldowns = new float[4];
        private readonly float[] respawns = new float[4];
        private MatchCoordinator match;
        public float AttackRange => Mathf.Max(0.1f, attackRange);
        public float AttackAngle => Mathf.Clamp(attackAngle, 1f, 360f);

        public void Initialize(MatchCoordinator coordinator) { match = coordinator; }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < match.Characters.Length; i++)
            {
                if (match.IsNetworkMovementTest && !match.NetworkController.IsSlotActive(i)) continue;
                cooldowns[i] = Mathf.Max(0f, cooldowns[i] - deltaTime);
                if (match.Model.Participants[i].Status != ParticipantStatus.Respawning) continue;
                respawns[i] = Mathf.Max(0f, respawns[i] - deltaTime);
                if (respawns[i] > 0f || (match.IsNetworkMovementTest && !match.NetworkController.CanRespawn(i)) || !match.Rules.TryRespawn(i)) continue;
                match.Characters[i].SetActiveState(true);
                match.Characters[i].Teleport(match.Grid.CellToWorld(match.Spawns[i]));
                match.NotifyRespawned(i);
            }
        }

        public bool CanAttack(int attacker) => match != null && attacker >= 0 && attacker < cooldowns.Length && match.Model.Status == MatchStatus.Playing && match.Model.Participants[attacker].Status == ParticipantStatus.Active && cooldowns[attacker] <= 0f && (!match.IsNetworkMovementTest || (Mirror.NetworkServer.active && match.NetworkController.IsSlotActive(attacker)));

        public void Attack(int attacker, Vector3 aimDirection) => TryAttack(attacker, aimDirection);

        public bool TryAttack(int attacker, Vector3 aimDirection)
        {
            if (match.IsNetworkMovementTest && !Mirror.NetworkServer.active) return false;
            if (match.Model.Status != MatchStatus.Playing || attacker < 0 || attacker >= match.Characters.Length || match.Model.Participants[attacker].Status != ParticipantStatus.Active || cooldowns[attacker] > 0f || (match.IsNetworkMovementTest && !match.NetworkController.IsSlotActive(attacker))) return false;
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude < 0.000001f) return false;
            aimDirection.Normalize();
            cooldowns[attacker] = Mathf.Max(0.1f, attackCooldown);
            var attackerCharacter = match.Characters[attacker];
            attackerCharacter.PlayAttack(aimDirection, AttackRange, AttackAngle);
            if (match.Recorder != null) match.Recorder.RecordAttack(attacker, aimDirection);
            if (match.IsNetworkMovementTest) match.NetworkController.BroadcastAttack(attacker, aimDirection);
            Vector3 origin = attackerCharacter.transform.position;
            for (int target = 0; target < match.Characters.Length; target++)
            {
                if (target == attacker || match.Model.Participants[target].Status != ParticipantStatus.Active || (match.IsNetworkMovementTest && !match.NetworkController.IsSlotActive(target))) continue;
                Vector3 direction = match.Characters[target].transform.position - origin;
                direction.y = 0f;
                if (!IsWithinAttack(direction, aimDirection, AttackRange, AttackAngle)) continue;
                if (!match.Rules.TryDamage(target, Mathf.Max(1, damage), Mathf.Max(0, droppedCoins), out int dropped)) continue;
                bool knockedOut = match.Model.Participants[target].Status == ParticipantStatus.Respawning;
                match.NotifyAttackHit(attacker, target, dropped, knockedOut);
                if (!knockedOut)
                {
                    if (direction.sqrMagnitude < 0.000001f) direction = aimDirection;
                    match.Characters[target].StartAttackKnockback(direction.normalized * AttackKnockbackDistance, Mathf.Max(0.01f, attackKnockbackSeconds));
                    match.Npcs.ResetPath(target);
                    continue;
                }
                match.Coins.Drop(match.Grid.WorldToCell(match.Characters[target].transform.position), dropped);
                respawns[target] = Mathf.Max(0.1f, respawnSeconds);
                match.Characters[target].SetActiveState(false);
                match.Npcs.ResetPath(target);
            }
            return true;
        }

        public static bool IsWithinAttack(Vector3 offset, Vector3 direction, float range, float angle)
        {
            offset.y = 0f;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.000001f || range < 0f || offset.sqrMagnitude > range * range + 0.000001f) return false;
            if (offset.sqrMagnitude < 0.000001f || angle >= 360f) return true;
            return Vector3.Dot(offset.normalized, direction.normalized) >= Mathf.Cos(Mathf.Clamp(angle, 0f, 360f) * 0.5f * Mathf.Deg2Rad) - 0.000001f;
        }
    }
}
