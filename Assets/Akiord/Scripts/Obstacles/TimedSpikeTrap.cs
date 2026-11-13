using UnityEngine;

public class TimedSpikeTrap : ObstacleBehaviour
{
    [SerializeField] private GameObject visual;
    [SerializeField, Min(0.01f)] private float visibleDuration = 2f;
    [SerializeField, Min(0.01f)] private float hiddenDuration = 2f;
    [SerializeField] private bool initiallyVisible = true;

    private bool isVisible;
    private float remaining;

    public bool IsVisible => isVisible;
    public float RemainingFraction => Mathf.Clamp01(remaining / Duration);

    private void Start()
    {
        isVisible = initiallyVisible;
        remaining = Duration;
        if (visual != null) visual.SetActive(isVisible);
    }

    private float Duration => Mathf.Max(0.01f, isVisible ? visibleDuration : hiddenDuration);

    private void Update()
    {
        if (!CanRun || visual == null) return;
        remaining -= Time.deltaTime;
        if (remaining > 0f) return;
        isVisible = !isVisible;
        visual.SetActive(isVisible);
        remaining = Duration;
    }
}
