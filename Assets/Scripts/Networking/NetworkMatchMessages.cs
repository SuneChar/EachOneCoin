using Mirror;
using UnityEngine;

namespace EachOneCoin.Networking
{
    public struct MovementReady : NetworkMessage { public ushort protocolVersion; }
    public struct MatchRulesMessage : NetworkMessage { public ushort protocolVersion; public int coinsToWin; public float durationSeconds; public string mapFingerprint; }
    public struct MatchRulesAcknowledged : NetworkMessage { public ushort protocolVersion; public int coinsToWin; public float durationSeconds; public string mapFingerprint; }
    public struct MatchRulesRejected : NetworkMessage { }

    public static class MatchRulesProtocol
    {
        public const ushort Version = 2;
        public const int MaximumCoinsToWin = 10000;
        public const float MaximumDurationSeconds = 86400f;

        public static bool IsValid(MatchRulesMessage rules)
        {
            return rules.protocolVersion == Version && rules.coinsToWin > 0 && rules.coinsToWin <= MaximumCoinsToWin &&
                !float.IsNaN(rules.durationSeconds) && !float.IsInfinity(rules.durationSeconds) &&
                rules.durationSeconds > 0f && rules.durationSeconds <= MaximumDurationSeconds && ValidFingerprint(rules.mapFingerprint);
        }

        public static bool Matches(MatchRulesMessage rules, MatchRulesAcknowledged acknowledgement)
        {
            return IsValid(rules) && acknowledgement.protocolVersion == rules.protocolVersion &&
                acknowledgement.coinsToWin == rules.coinsToWin && acknowledgement.durationSeconds == rules.durationSeconds &&
                acknowledgement.mapFingerprint == rules.mapFingerprint;
        }

        private static bool ValidFingerprint(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
    }
    public struct SelectAppearanceRequest : NetworkMessage { public byte appearanceIndex; }
    public struct ReturnRoomRequest : NetworkMessage { }
    public struct ReturnRoomState : NetworkMessage { public byte returnedMask; }
    public struct MovementBegin : NetworkMessage
    {
        public byte occupied;
        public bool fillNpcs;
        public byte appearance0, appearance1, appearance2, appearance3;
        public byte AppearanceAt(int slot) => slot == 0 ? appearance0 : slot == 1 ? appearance1 : slot == 2 ? appearance2 : appearance3;
    }
    public struct MovementInput : NetworkMessage { public Vector2 direction; public bool sprint; }
    public struct MovementSnapshot : NetworkMessage
    {
        public byte visible;
        public double serverTime;
        public Vector3 p0, p1, p2, p3;
        public Vector3 Position(int i) { return i == 0 ? p0 : i == 1 ? p1 : i == 2 ? p2 : p3; }
    }
}
