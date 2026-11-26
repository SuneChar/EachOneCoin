using UnityEngine;

namespace EachOneCoin
{
    public static class PlanarVelocity
    {
        // Preserve the active axis when both are requested; otherwise choose the larger input, with horizontal winning ties.
        public static Vector3 Cardinal(Vector3 value, Vector3 preferred)
        {
            value.y = 0f;
            float x = Mathf.Abs(value.x);
            float z = Mathf.Abs(value.z);
            if (x < 0.000001f && z < 0.000001f) return Vector3.zero;
            bool horizontal = x >= z;
            if (x >= 0.000001f && z >= 0.000001f)
            {
                if (Mathf.Abs(preferred.x) > 0.000001f && Mathf.Abs(preferred.z) < 0.000001f) horizontal = true;
                else if (Mathf.Abs(preferred.z) > 0.000001f && Mathf.Abs(preferred.x) < 0.000001f) horizontal = false;
            }
            return horizontal ? new Vector3(Mathf.Sign(value.x) * value.magnitude, 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(value.z) * value.magnitude);
        }

        public static Vector3 AdvanceCardinal(Vector3 current, Vector3 desired, float acceleration, float deceleration, float deltaTime)
        {
            desired = Cardinal(desired, Vector3.zero);
            if (deltaTime > 0f && desired.sqrMagnitude > 0.000001f)
            {
                if (desired.x != 0f) current.z = 0f;
                else current.x = 0f;
            }
            return Advance(current, desired, acceleration, deceleration, deltaTime);
        }

        public static Vector3 Advance(Vector3 current, Vector3 desired, float acceleration, float deceleration, float deltaTime)
        {
            current.y = 0f;
            desired.y = 0f;
            float rate = desired.sqrMagnitude < 0.000001f ? deceleration : acceleration;
            return Vector3.MoveTowards(current, desired, Mathf.Max(0f, rate) * Mathf.Max(0f, deltaTime));
        }
    }
}
