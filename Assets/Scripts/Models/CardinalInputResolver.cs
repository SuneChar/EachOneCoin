using UnityEngine;

namespace EachOneCoin
{
    // Resolves legacy input into a single cardinal direction. Opposite keys on the same axis
    // cancel each other out, and when both axes survive the most recently pressed axis wins,
    // so a new key changes movement immediately instead of keeping the current axis.
    public sealed class CardinalInputResolver
    {
        private const float AxisTieTolerance = 0.1f;
        private int order;
        private int leftOrder, rightOrder, upOrder, downOrder;
        private bool leftHeld, rightHeld, upHeld, downHeld;
        private int horizontalOrder, verticalOrder;
        private bool horizontalActive, verticalActive;

        // Keyboard keys are discrete: opposite keys on one axis cancel out, and the newer
        // surviving axis wins. Analog axes are continuous: the larger deflection wins, and the
        // newer axis breaks near ties.
        public Vector3 Update(bool left, bool right, bool up, bool down, float axisX, float axisZ, float axisThreshold)
        {
            Track(ref leftHeld, ref leftOrder, left);
            Track(ref rightHeld, ref rightOrder, right);
            Track(ref upHeld, ref upOrder, up);
            Track(ref downHeld, ref downOrder, down);

            // Keep analog tracking current even when a key wins, so the next stick movement
            // still registers as a fresh activation.
            Vector3 axisDirection = ResolveAxis(axisX, axisZ, axisThreshold);

            int horizontal = (right ? 1 : 0) - (left ? 1 : 0);
            int vertical = (up ? 1 : 0) - (down ? 1 : 0);
            if (horizontal != 0 && vertical != 0)
            {
                if (Mathf.Max(leftOrder, rightOrder) >= Mathf.Max(upOrder, downOrder)) vertical = 0;
                else horizontal = 0;
            }
            if (horizontal != 0) return new Vector3(horizontal, 0f, 0f);
            if (vertical != 0) return new Vector3(0f, 0f, vertical);
            return axisDirection;
        }

        private void Track(ref bool wasHeld, ref int stamp, bool held)
        {
            if (!held) stamp = 0;
            else if (!wasHeld) stamp = ++order;
            wasHeld = held;
        }

        private Vector3 ResolveAxis(float axisX, float axisZ, float axisThreshold)
        {
            float threshold = Mathf.Max(0f, axisThreshold);
            float dx = Mathf.Abs(axisX);
            float dz = Mathf.Abs(axisZ);
            bool horizontal = dx > threshold;
            bool vertical = dz > threshold;
            if (horizontal && !horizontalActive) horizontalOrder = ++order;
            if (vertical && !verticalActive) verticalOrder = ++order;
            horizontalActive = horizontal;
            verticalActive = vertical;
            if (horizontal && vertical)
            {
                if (Mathf.Abs(dx - dz) > AxisTieTolerance) horizontal = dx > dz;
                else horizontal = horizontalOrder >= verticalOrder;
            }
            if (horizontal) return axisX >= 0f ? Vector3.right : Vector3.left;
            if (vertical) return axisZ >= 0f ? Vector3.forward : Vector3.back;
            return Vector3.zero;
        }
    }
}
