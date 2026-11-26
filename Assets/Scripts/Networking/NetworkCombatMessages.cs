using Mirror;
using UnityEngine;

namespace EachOneCoin.Networking
{
    public struct AttackRequest : NetworkMessage { public uint sequence; public Vector3 direction; }
    public struct AttackEvent : NetworkMessage { public byte slot; public Vector3 direction; }
    public struct DamageEvent : NetworkMessage { public byte slot; public byte kind; }
    public struct CombatParticipant
    {
        public byte status;
        // Zero for human or unknown; 1-3 identify the server-assigned NPC personality.
        public byte npcPersonality;
        public int health, maxHealth;
        public float stamina;
        public Vector3 position;
        public bool npcWindup;
        public Vector3 npcWindupDirection;
    }
    public struct CombatStateMessage : NetworkMessage
    {
        public uint sequence;
        public double serverTime;
        public CombatParticipant p0, p1, p2, p3;
        public CombatParticipant Participant(int slot) => slot == 0 ? p0 : slot == 1 ? p1 : slot == 2 ? p2 : p3;
    }
}
