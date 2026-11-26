using Mirror;
using UnityEngine;

namespace EachOneCoin.Networking
{
    public struct AttackRequest : NetworkMessage { public uint sequence; public Vector3 direction; }
    public struct AttackEvent : NetworkMessage { public byte slot; public Vector3 direction; }
    public struct CombatParticipant
    {
        public byte status;
        public int health, maxHealth;
        public float stamina;
        public Vector3 position;
    }
    public struct CombatStateMessage : NetworkMessage
    {
        public uint sequence;
        public CombatParticipant p0, p1, p2, p3;
        public CombatParticipant Participant(int slot) => slot == 0 ? p0 : slot == 1 ? p1 : slot == 2 ? p2 : p3;
    }
}
