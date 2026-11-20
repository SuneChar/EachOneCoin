using UnityEngine;
using UnityEngine.Rendering;

namespace EachOneCoin.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ObstacleProximityWarning : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField, Min(0.01f)] private float warningDistance = 2.5f;
        [SerializeField, Min(0f)] private float dangerDistance = 0.5f;
        [SerializeField, Min(0.01f)] private float detectionInterval = 0.05f;
        [SerializeField, Min(0.1f)] private float detectionHeight = 1.5f;
        [SerializeField, Min(0f)] private float targetSwitchMargin = 0.15f;
        [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.1f;
        [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.2f;
        [SerializeField, Min(0.01f)] private float ringRadius = 0.7f;
        [SerializeField, Min(0f)] private float groundOffset = 0.04f;
        [SerializeField] private Color warningColor = new Color(1f, 0.85f, 0.12f, 1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.18f, 0.08f, 1f);

        private const int RingSegments = 48;
        private const int ArcSegments = 12;
        private Collider[] overlaps = new Collider[64];
        private MatchCoordinator match;
        private CharacterController controller;
        private LineRenderer ring;
        private LineRenderer arc;
        private Collider target;
        private Vector3 direction;
        private float distance;
        private float detectionTimer;
        private float opacity;
        private float pulseTime;
        private bool isLocal;
        private bool characterActive = true;
        private bool warnedCapacity;

        public void Initialize(MatchCoordinator coordinator, bool local)
        {
            match = coordinator;
            isLocal = local;
            controller = GetComponent<CharacterController>();
            if (!local) { enabled = false; return; }
            if (lineMaterial == null) { Debug.LogWarning("Obstacle warning requires a line material.", this); enabled = false; return; }
            ring = CreateLine("Obstacle Warning Ring", RingSegments + 1);
            arc = CreateLine("Obstacle Warning Direction", ArcSegments + 1);
            ClearWarning();
        }

        private LineRenderer CreateLine(string objectName, int count)
        {
            var go = new GameObject(objectName, typeof(LineRenderer));
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var line = go.GetComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.positionCount = count;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        public void SetCharacterActive(bool active)
        {
            characterActive = active;
            ClearWarning();
        }

        public void ClearWarning()
        {
            target = null;
            direction = Vector3.zero;
            opacity = 0f;
            detectionTimer = 0f;
            if (ring != null) ring.enabled = false;
            if (arc != null) arc.enabled = false;
        }

        private void LateUpdate()
        {
            if (!isLocal || match == null || match.Model == null || ring == null) return;
            if (!characterActive || controller == null || !controller.enabled || match.Model.Status != MatchStatus.Playing)
            {
                ClearWarning();
                return;
            }
            if (match.IsPaused) return;
            detectionTimer -= Time.deltaTime;
            if (detectionTimer <= 0f)
            {
                Detect();
                detectionTimer = Mathf.Max(0.01f, detectionInterval);
            }
            opacity = Mathf.MoveTowards(opacity, target != null ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, target != null ? fadeInSeconds : fadeOutSeconds));
            if (opacity <= 0f) { ring.enabled = false; arc.enabled = false; return; }
            Draw();
        }

        private void Detect()
        {
            Vector3 center = transform.TransformPoint(controller.center);
            Vector3 scale = transform.lossyScale;
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float range = radius + Mathf.Max(0.01f, warningDistance);
            int count;
            do
            {
                count = Physics.OverlapBoxNonAlloc(center, new Vector3(range, Mathf.Max(0.1f, detectionHeight) * 0.5f, range), overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
                if (count < overlaps.Length) break;
                if (overlaps.Length >= 4096)
                {
                    if (!warnedCapacity) { Debug.LogWarning("Obstacle warning query reached its maximum capacity; results may be incomplete.", this); warnedCapacity = true; }
                    break;
                }
                overlaps = new Collider[overlaps.Length * 2];
            } while (true);

            Collider nearest = null;
            Vector3 nearestDirection = Vector3.zero;
            float nearestDistance = float.PositiveInfinity;
            float previousDistance = float.PositiveInfinity;
            Vector3 previousDirection = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Collider other = overlaps[i];
                if (other == null || other.transform.IsChildOf(transform) || !match.IsHazard(other)) continue;
                Vector3 delta = other.ClosestPoint(center) - center;
                delta.y = 0f;
                float gap = Mathf.Max(0f, delta.magnitude - radius);
                if (gap > warningDistance) continue;
                if (other == target) { previousDistance = gap; previousDirection = delta; }
                if (gap < nearestDistance) { nearest = other; nearestDistance = gap; nearestDirection = delta; }
            }
            if (nearest != null && target != null && previousDistance <= nearestDistance + Mathf.Max(0f, targetSwitchMargin))
            {
                nearest = target; nearestDistance = previousDistance; nearestDirection = previousDirection;
            }
            target = nearest;
            if (target == null) return;
            distance = nearestDistance;
            if (nearestDirection.sqrMagnitude > 0.000001f) direction = nearestDirection.normalized;
            else direction = Vector3.zero;
        }

        private void Draw()
        {
            float high = Mathf.Clamp(dangerDistance, 0f, Mathf.Max(0.01f, warningDistance) - 0.001f);
            float intensity = Mathf.InverseLerp(warningDistance, high, distance);
            pulseTime += Time.deltaTime * Mathf.Lerp(1f, 3.3f, intensity);
            float pulse = 0.8f + 0.2f * Mathf.Sin(pulseTime * Mathf.PI * 2f);
            Color color = Color.Lerp(warningColor, dangerColor, intensity);
            color.a *= opacity * pulse;
            Color ringColor = color; ringColor.a *= 0.5f;
            ring.startColor = ring.endColor = ringColor;
            arc.startColor = arc.endColor = color;
            ring.widthMultiplier = Mathf.Lerp(0.035f, 0.055f, intensity);
            arc.widthMultiplier = Mathf.Lerp(0.065f, 0.1f, intensity);
            Vector3 center = transform.TransformPoint(controller.center);
            center.y = controller.bounds.min.y - controller.skinWidth + groundOffset;
            for (int i = 0; i <= RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                ring.SetPosition(i, center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * ringRadius);
            }
            ring.enabled = true;
            arc.enabled = direction.sqrMagnitude > 0.000001f;
            if (!arc.enabled) return;
            float bearing = Mathf.Atan2(direction.x, direction.z);
            for (int i = 0; i <= ArcSegments; i++)
            {
                float angle = bearing + Mathf.Deg2Rad * Mathf.Lerp(-40f, 40f, i / (float)ArcSegments);
                arc.SetPosition(i, center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * ringRadius);
            }
        }
    }
}
