using UnityEngine;
using UnityEngine.UI;

public class SpikeTrapProgressView : MonoBehaviour
{
    [SerializeField] private TimedSpikeTrap trap;
    [SerializeField] private Image fill;
    [SerializeField] private Color visibleColor = new Color(1f, 0.3f, 0.15f);
    [SerializeField] private Color hiddenColor = new Color(1f, 0.8f, 0.15f);

    private void LateUpdate()
    {
        if (trap == null || fill == null) return;
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMax = new Vector2(trap.RemainingFraction, 1f);
        fill.color = trap.IsVisible ? visibleColor : hiddenColor;
    }
}
