using UnityEngine;

namespace EachOneCoin
{
    public static class PlanarVelocity
    {
        public static Vector3 Advance(Vector3 current, Vector3 desired, float acceleration, float deceleration, float deltaTime)
        {
            current.y = 0f;
            desired.y = 0f;
            float rate = desired.sqrMagnitude < 0.000001f ? deceleration : acceleration;
            return Vector3.MoveTowards(current, desired, Mathf.Max(0f, rate) * Mathf.Max(0f, deltaTime));
        }
    }
}
