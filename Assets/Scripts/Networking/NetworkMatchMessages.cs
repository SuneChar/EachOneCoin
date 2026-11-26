using Mirror;
using UnityEngine;

namespace EachOneCoin.Networking
{
    public struct MovementReady : NetworkMessage { }
    public struct ReturnRoomRequest : NetworkMessage { }
    public struct ReturnRoomState : NetworkMessage { public byte returnedMask; }
    public struct MovementBegin : NetworkMessage { public byte occupied; public bool fillNpcs; }
    public struct MovementInput : NetworkMessage { public Vector2 direction; public bool sprint; }
    public struct MovementSnapshot : NetworkMessage
    {
        public byte visible;
        public double serverTime;
        public Vector3 p0, p1, p2, p3;
        public Vector3 Position(int i) { return i == 0 ? p0 : i == 1 ? p1 : i == 2 ? p2 : p3; }
    }
}
