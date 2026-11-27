using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    public sealed class ParticipantHudSlotView : MonoBehaviour
    {
        public CanvasGroup canvasGroup;
        public Image crownImage;
        public RectTransform coinGroup;
        public Image coinIconImage;
        public TMP_Text coinText;
        public RectTransform medalGroup;
        public Image medalIconImage;
        public TMP_Text medalRankText;
        public RectTransform avatarRoot;
        public Image maskImage;
        public Image basePortraitImage;
        public RectTransform overlayContainer;
        public Image statusOverlayImage;
        public Image frameImage;
        public RectTransform healthBarRect;

        public bool HasRequiredLayers => canvasGroup != null && crownImage != null && coinGroup != null &&
            coinIconImage != null && coinText != null && medalGroup != null && medalIconImage != null &&
            medalRankText != null && avatarRoot != null && maskImage != null && basePortraitImage != null &&
            overlayContainer != null && statusOverlayImage != null && frameImage != null && healthBarRect != null;
    }
}
