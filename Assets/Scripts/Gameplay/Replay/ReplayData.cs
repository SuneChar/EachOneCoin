using System;
using UnityEngine;

namespace EachOneCoin.Gameplay.Replay
{
    // Replay records are independent of Mirror messages and live Unity objects.
    [Serializable]
    public sealed class ReplayData
    {
        public const int CurrentFormat = 2;
        public int formatVersion;
        public string mapFingerprint;
        public string gameVersion;
        public string matchId;
        public string recordedUtc;
        public bool networkMatch;
        public bool complete;
        public int coinsToWin;
        public float duration;
        public ReplayFrame[] frames;
        public ReplayEvent[] events;
    }

    [Serializable]
    public sealed class ReplayFrame
    {
        public float time;
        public float remaining;
        public int matchStatus;
        public ReplayParticipant[] participants;
        public byte[] gridOwners;
        public ReplayCoin[] coins;
        public Vector3[] patrolPositions;
        public ReplaySpike[] spikes;
        public double mineElapsed;
        public int nextMineEvent, warningMine;
        public ushort[] warningX, warningY;
    }

    [Serializable]
    public struct ReplayParticipant
    {
        public bool participating;
        public bool visible;
        public string displayName;
        public Vector3 position;
        public Vector3 facing;
        public bool aiming;
        public Vector3 aimDirection;
        public int health, maxHealth, coins, finishOrder, status;
        public float stamina;
    }

    [Serializable]
    public struct ReplayCoin
    {
        public uint id;
        public byte kind;
        public int x, y;
    }

    [Serializable]
    public struct ReplaySpike
    {
        public bool active;
        public float remainingFraction, height;
    }

    [Serializable]
    public struct ReplayEvent
    {
        public float time;
        public int sequence;
        public string type;
        public int actor, target, value, detail;
        public Vector3 direction;
    }
}
