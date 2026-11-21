using UnityEngine;

public class TimedSpikeTrap : ObstacleBehaviour
{
    [SerializeField] private GameObject visual;
    [SerializeField, Min(0.01f)] private float visibleDuration = 2f;
    [SerializeField, Min(0.01f)] private float hiddenDuration = 2f;
    [SerializeField] private bool initiallyVisible = true;
    [SerializeField] private Transform spikes;
    [SerializeField, Min(0.01f)] private float transitionDuration = 0.25f;
    [SerializeField] private float raisedOffset = 0.15f;
    [SerializeField] private float retractedOffset = 0.02f;

    private bool isVisible;
    private float remaining;
    private Vector3 basePosition;
    private Vector3 animationFrom;
    private Vector3 animationTo;
    private float animationElapsed;
    private bool animating;

    public bool IsActive => isVisible;
    public bool IsVisible => IsActive;
    public float RemainingFraction => Mathf.Clamp01(remaining / Duration);

    private void Start()
    {
        isVisible = initiallyVisible;
        remaining = Duration;
        if (visual != null) visual.SetActive(true);
        if (spikes != null)
        {
            basePosition = spikes.localPosition;
            spikes.localPosition = PositionForState(isVisible);
        }
    }

    private float Duration => Mathf.Max(0.01f, isVisible ? visibleDuration : hiddenDuration);

    private void Update()
    {
        if (!CanRun) return;
        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            isVisible = !isVisible;
            remaining = Duration;
            if (spikes != null)
            {
                animationFrom = spikes.localPosition;
                animationTo = PositionForState(isVisible);
                animationElapsed = 0f;
                animating = true;
            }
        }
        if (!animating || spikes == null) return;
        animationElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(animationElapsed / Mathf.Max(0.01f, transitionDuration));
        spikes.localPosition = Vector3.Lerp(animationFrom, animationTo, t);
        if (t >= 1f) animating = false;
    }

    private Vector3 PositionForState(bool active)
    {
        return basePosition + Vector3.up * (active ? raisedOffset : retractedOffset);
    }
}
