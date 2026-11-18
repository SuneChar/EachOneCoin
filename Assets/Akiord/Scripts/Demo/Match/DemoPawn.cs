using UnityEngine;

namespace EachOneCoin.Demo
{
    public sealed class DemoPawn : MonoBehaviour
    {
        public int ParticipantId { get; private set; }
        public Vector2 Position { get; private set; }

        public void Initialize(int participantId, Vector2 position)
        {
            ParticipantId = participantId;
            SetPosition(position);
        }

        public void SetPosition(Vector2 position)
        {
            Position = position;
            transform.position = new Vector3(position.x, 0.16f, position.y);
        }
    }
}
