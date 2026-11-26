using System;

namespace EachOneCoin
{
    public sealed class StaminaModel
    {
        public float Maximum { get; }
        public float Current { get; internal set; }
        public bool IsSprinting { get; internal set; }
        public bool IsExhausted => ExhaustedUntilRelease;
        internal bool ExhaustedUntilRelease { get; set; }

        public StaminaModel(float maximum)
        {
            if (float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0f) throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = maximum;
            Current = maximum;
        }

    }
}
