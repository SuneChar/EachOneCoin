using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    public sealed class HealthHeartSlotView : MonoBehaviour
    {
        public Image baseImage;
        public Image fadeOverlay;
        public Image flashOverlay;
        public Image crackOverlay;
        public Image ringOverlay;
        public Image[] chunkImages;
        public Image[] shardImages;

        public bool HasRequiredLayers => baseImage != null && fadeOverlay != null && flashOverlay != null &&
            crackOverlay != null && ringOverlay != null && chunkImages != null && chunkImages.Length == 3 &&
            shardImages != null && shardImages.Length == 14 &&
            System.Array.TrueForAll(chunkImages, image => image != null) &&
            System.Array.TrueForAll(shardImages, image => image != null);
    }
}
