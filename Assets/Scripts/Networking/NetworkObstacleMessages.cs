using Mirror;
using UnityEngine;

namespace EachOneCoin.Networking
{
    public struct SpikeObstacleState
    {
        public bool active;
        public float remainingFraction;
        public float spikeHeight;
    }

    public struct ObstacleStateMessage : NetworkMessage
    {
        public uint sequence;
        public Vector3[] patrolPositions;
        public SpikeObstacleState[] spikes;
    }
}
