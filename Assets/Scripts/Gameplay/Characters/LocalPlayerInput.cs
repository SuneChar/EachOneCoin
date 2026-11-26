using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class LocalPlayerInput : MonoBehaviour
    {
        private const float AxisThreshold = 0.25f;
        private CardinalInputResolver direction = new CardinalInputResolver();
        private int sampledFrame = -1;
        private Vector3 sampledMovement;
        private bool focusReset;
        private bool suppressGamepadAttack, suppressGamepadSprint;

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            direction = new CardinalInputResolver();
            focusReset = true;
            sampledFrame = -1;
            SuppressGamepadActionsUntilReleased();
        }

        public void SuppressGamepadActionsUntilReleased()
        {
            suppressGamepadAttack = true;
            suppressGamepadSprint = true;
        }

        private static bool GamepadActionAllowed(ref bool suppressed, KeyCode key)
        {
            if (suppressed && !Input.GetKey(key)) suppressed = false;
            return !suppressed;
        }

        // Sampled once per frame and cached so callers reading it twice never advance the
        // resolver state. Directions are unit vectors on a single axis.
        public Vector3 Movement
        {
            get
            {
                if (!Application.isFocused)
                {
                    if (!focusReset) direction = new CardinalInputResolver();
                    focusReset = true;
                    sampledFrame = -1;
                    return Vector3.zero;
                }
                focusReset = false;
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

        public bool KeyboardAttackDown => Application.isFocused && Input.GetKeyDown(KeyCode.Space);
        public bool GamepadAttackDown => Application.isFocused && GamepadActionAllowed(ref suppressGamepadAttack, KeyCode.JoystickButton0) && Input.GetKeyDown(KeyCode.JoystickButton0);
        public bool AttackHeld(bool gamepad) => Application.isFocused && (gamepad
            ? GamepadActionAllowed(ref suppressGamepadAttack, KeyCode.JoystickButton0) && Input.GetKey(KeyCode.JoystickButton0)
            : Input.GetKey(KeyCode.Space));
        public Vector2 GamepadAim => Application.isFocused ? new Vector2(Input.GetAxisRaw("GamepadLeftHorizontal"), Input.GetAxisRaw("GamepadLeftVertical")) : Vector2.zero;

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
        public bool SprintHeld => Application.isFocused && (Input.GetKey(KeyCode.LeftShift) ||
            (GamepadActionAllowed(ref suppressGamepadSprint, KeyCode.JoystickButton1) && Input.GetKey(KeyCode.JoystickButton1)));
    }
}
