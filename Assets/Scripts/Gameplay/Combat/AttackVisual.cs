using UnityEngine;
using UnityEngine.Rendering;

namespace EachOneCoin.Gameplay
{
    // Runtime-only, collider-free visuals; the same sector dimensions are used by combat.
    public sealed class AttackVisual : MonoBehaviour
    {
        private const int Segments = 32;
        private const float SwingDuration = 0.22f;
        private MatchCoordinator match;
        private Transform aim;
        private Transform swing;
        private Mesh mesh;
        private Material material;
        private MeshRenderer swingRenderer;
        private MaterialPropertyBlock block;
        private float configuredRange = -1f;
        private float configuredAngle = -1f;
        private float swingRemaining;
        private Vector3 swingDirection;
        private Transform characterVisual;
        private Vector3 visualPosition;
        private Vector3 visualOffset;

        public void Initialize(MatchCoordinator coordinator, Transform visual)
        {
            match = coordinator;
            characterVisual = visual;
            if (visual != null) visualPosition = visual.localPosition;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            material = new Material(shader);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent + 10;
            mesh = new Mesh { name = "Attack Sector" };
            block = new MaterialPropertyBlock();
            aim = CreateSector("Attack Aim", new Color(0.1f, 0.8f, 1f, 0.3f), out _);
            swing = CreateSector("Attack Swing", new Color(1f, 0.85f, 0.35f, 0.65f), out swingRenderer);
            aim.gameObject.SetActive(false);
            swing.gameObject.SetActive(false);
        }

        private Transform CreateSector(string label, Color color, out MeshRenderer renderer)
        {
            var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform.parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetColor(renderer, color);
            return go.transform;
        }

        private void SetColor(Renderer renderer, Color color)
        {
            block.Clear();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        private void Configure(float range, float angle)
        {
            if (Mathf.Approximately(configuredRange, range) && Mathf.Approximately(configuredAngle, angle)) return;
            configuredRange = range;
            configuredAngle = angle;
            var vertices = new Vector3[Segments + 2];
            var triangles = new int[Segments * 3];
            for (int i = 0; i <= Segments; i++)
            {
                float radians = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, (float)i / Segments) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Sin(radians) * range, 0f, Mathf.Cos(radians) * range);
                if (i == Segments) continue;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        public void SetAim(bool visible, Vector3 direction, float range, float angle)
        {
            if (aim == null) return;
            if (visible)
            {
                Configure(range, angle);
                Place(aim, direction, 0.06f);
            }
            aim.gameObject.SetActive(visible);
        }

        public void Play(Vector3 direction, float range, float angle)
        {
            Configure(range, angle);
            swingDirection = direction;
            swingRemaining = SwingDuration;
            Place(swing, direction, 0.07f);
            swing.localScale = Vector3.one;
            SetColor(swingRenderer, new Color(1f, 0.85f, 0.35f, 0.65f));
            swing.gameObject.SetActive(true);
            if (characterVisual != null)
                visualOffset = characterVisual.parent.InverseTransformDirection(direction.normalized) * 0.12f;
        }

        public void Clear()
        {
            if (aim != null) aim.gameObject.SetActive(false);
            if (swing != null) swing.gameObject.SetActive(false);
            swingRemaining = 0f;
            if (characterVisual != null) characterVisual.localPosition = visualPosition;
        }

        private void Place(Transform target, Vector3 direction, float height)
        {
            Vector3 position = transform.position;
            target.position = new Vector3(position.x, height, position.z);
            if (direction.sqrMagnitude > 0.000001f) target.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private void LateUpdate()
        {
            if (aim != null && aim.gameObject.activeSelf) Place(aim, aim.forward, 0.06f);
            if (swingRemaining <= 0f || match == null || match.IsPaused) return;
            swingRemaining = Mathf.Max(0f, swingRemaining - Time.deltaTime);
            float progress = 1f - swingRemaining / SwingDuration;
            Place(swing, swingDirection, 0.07f);
            SetColor(swingRenderer, new Color(1f, 0.85f, 0.35f, 0.65f * (1f - progress)));
            if (characterVisual != null) characterVisual.localPosition = visualPosition + visualOffset * Mathf.Sin(progress * Mathf.PI);
            if (swingRemaining <= 0f) { swing.gameObject.SetActive(false); if (characterVisual != null) characterVisual.localPosition = visualPosition; }
        }

        private void OnDestroy()
        {
            if (aim != null) Destroy(aim.gameObject);
            if (swing != null) Destroy(swing.gameObject);
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
