using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class CoinWarningMarkerView : MonoBehaviour
    {
        [SerializeField] private Transform pulseDisc;
        [SerializeField] private Renderer[] warningRenderers;

        public Transform PulseDisc => pulseDisc;
        public bool IsConfigured => pulseDisc != null && warningRenderers != null && warningRenderers.Length > 0;

        public void SetMaterial(Material material)
        {
            if (warningRenderers == null) return;
            for (int i = 0; i < warningRenderers.Length; i++)
                if (warningRenderers[i] != null) warningRenderers[i].sharedMaterial = material;
        }
    }
}
