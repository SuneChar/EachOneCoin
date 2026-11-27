using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    public sealed class MatchEventFeedEntryView : MonoBehaviour
    {
        public CanvasGroup canvasGroup;
        public Image accentBarImage;
        public TMP_Text messageText;

        public bool HasRequiredLayers => canvasGroup != null && accentBarImage != null && messageText != null;
    }
}
