using System;

namespace EachOneCoin
{
    public sealed class StaminaModel
    {
        public float Maximum { get; }
        public float Current { get; private set; }
        public bool IsSprinting { get; private set; }
        private bool exhaustedUntilRelease;

        public StaminaModel(float maximum)
        {
            if (float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0f) throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = maximum;
            Current = maximum;
        }

        public void Tick(float deltaTime, bool active, bool moving, bool sprintHeld, float drainPerSecond, float recoveryPerSecond)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (float.IsNaN(drainPerSecond) || float.IsInfinity(drainPerSecond) || drainPerSecond < 0f) throw new ArgumentOutOfRangeException(nameof(drainPerSecond));
            if (float.IsNaN(recoveryPerSecond) || float.IsInfinity(recoveryPerSecond) || recoveryPerSecond < 0f) throw new ArgumentOutOfRangeException(nameof(recoveryPerSecond));
            if (!sprintHeld) exhaustedUntilRelease = false;
            IsSprinting = active && moving && sprintHeld && !exhaustedUntilRelease && Current > 0f;
            if (!active) return;
            if (IsSprinting)
            {
                Current = Math.Max(0f, Current - drainPerSecond * deltaTime);
                if (Current <= 0f) { IsSprinting = false; exhaustedUntilRelease = true; }
            }
            else Current = Math.Min(Maximum, Current + recoveryPerSecond * deltaTime);
        }
    }
}
