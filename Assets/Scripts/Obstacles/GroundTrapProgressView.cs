using UnityEngine;

public class GroundTrapProgressView : MonoBehaviour
{
    [SerializeField] private TimedSpikeTrap trap;
    [SerializeField] private LineRenderer background;
    [SerializeField] private LineRenderer progress;
    [SerializeField] private Vector2 size = new Vector2(0.9f, 0.9f);
    [SerializeField, Min(0f)] private float cornerRadius = 0.12f;
    [SerializeField] private Color activeColor = new Color(1f, 0.3f, 0.15f);
    [SerializeField] private Color inactiveColor = new Color(1f, 0.8f, 0.15f);

    private const int SegmentsPerCorner = 6;
    private Vector3[] points;
    private float[] distances;
    private float totalLength;

    private void Awake()
    {
        if (background == null || progress == null) return;
        float halfX = Mathf.Max(0.01f, size.x * 0.5f);
        float halfZ = Mathf.Max(0.01f, size.y * 0.5f);
        float radius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(halfX, halfZ));
        points = new Vector3[4 * (SegmentsPerCorner + 1) + 1];
        distances = new float[points.Length];
        for (int corner = 0; corner < 4; corner++)
        {
            float cx = (corner == 0 || corner == 3 ? 1f : -1f) * (halfX - radius);
            float cz = (corner < 2 ? 1f : -1f) * (halfZ - radius);
            for (int i = 0; i <= SegmentsPerCorner; i++)
            {
                float angle = (corner * 90f + i * 90f / SegmentsPerCorner) * Mathf.Deg2Rad;
                points[corner * (SegmentsPerCorner + 1) + i] = new Vector3(cx + radius * Mathf.Cos(angle), 0f, cz + radius * Mathf.Sin(angle));
            }
        }
        points[points.Length - 1] = points[0];
        for (int i = 1; i < points.Length; i++)
            distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        totalLength = distances[distances.Length - 1];
        background.positionCount = points.Length;
        background.SetPositions(points);
    }

    private void LateUpdate()
    {
        if (trap == null || background == null || progress == null || points == null) return;
        Color currentColor = trap.IsActive ? activeColor : inactiveColor;
        Color nextColor = trap.IsActive ? inactiveColor : activeColor;
        background.startColor = currentColor;
        background.endColor = currentColor;
        progress.startColor = nextColor;
        progress.endColor = nextColor;
        float length = (1f - trap.RemainingFraction) * totalLength;
        progress.enabled = length > 0f;
        if (!progress.enabled) return;
        int end = 1;
        while (end < distances.Length - 1 && distances[end] < length) end++;
        progress.positionCount = end + 1;
        for (int i = 0; i < end; i++) progress.SetPosition(i, points[i]);
        float segmentLength = distances[end] - distances[end - 1];
        float t = segmentLength > 0f ? (length - distances[end - 1]) / segmentLength : 1f;
        progress.SetPosition(end, Vector3.Lerp(points[end - 1], points[end], t));
    }
}
