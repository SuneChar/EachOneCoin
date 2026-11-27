using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    public sealed class MinimapPlayerMarkerView : MonoBehaviour
    {
        public Image ring;
        public Image dot;
        public Image arrow;
        public bool HasRequiredLayers => ring != null && dot != null && arrow != null;
    }
}
