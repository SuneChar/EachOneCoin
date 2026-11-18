using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class LocalPlayerInput : MonoBehaviour
    {
        public Vector3 Movement => new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")).normalized;
        public bool AttackPressed => Input.GetKeyDown(KeyCode.Space);
    }
}
