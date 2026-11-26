using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class LocalPlayerInput : MonoBehaviour
    {
        private const float AxisThreshold = 0.25f;
        private readonly CardinalInputResolver direction = new CardinalInputResolver();
        private int sampledFrame = -1;
        private Vector3 sampledMovement;

        // Sampled once per frame and cached so callers reading it twice never advance the
        // resolver state. Directions are unit vectors on a single axis.
        public Vector3 Movement
        {
            get
            {
                if (sampledFrame == Time.frameCount) return sampledMovement;
                sampledFrame = Time.frameCount;
                sampledMovement = direction.Update(
                    Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow),
                    Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow),
                    Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow),
                    Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow),
                    Input.GetAxisRaw("Horizontal"),
                    Input.GetAxisRaw("Vertical"),
                    AxisThreshold);
                return sampledMovement;
            }
        }

        public bool KeyboardAttackDown => Input.GetKeyDown(KeyCode.Space);
        public bool GamepadAttackDown => Input.GetKeyDown(KeyCode.JoystickButton2);
        public bool AttackHeld(bool gamepad) => Input.GetKey(gamepad ? KeyCode.JoystickButton2 : KeyCode.Space);
        public Vector2 GamepadAim => new Vector2(Input.GetAxisRaw("GamepadRightHorizontal"), -Input.GetAxisRaw("GamepadRightVertical"));

        public bool TryAimDirection(bool gamepad, Vector3 origin, Camera camera, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (camera == null) return false;
            if (gamepad)
            {
                Vector2 stick = GamepadAim;
                if (stick.sqrMagnitude < 0.0625f) return false;
                Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
                direction = right * stick.x + forward * stick.y;
            }
            else
            {
                Ray ray = camera.ScreenPointToRay(Input.mousePosition);
                var plane = new Plane(Vector3.up, origin);
                if (!plane.Raycast(ray, out float distance)) return false;
                direction = ray.GetPoint(distance) - origin;
                direction.y = 0f;
            }
            if (direction.sqrMagnitude < 0.0001f) return false;
            direction.Normalize();
            return true;
        }
        public bool SprintHeld => Input.GetKey(KeyCode.LeftShift);
    }
}
