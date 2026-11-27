using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class ArenaGridView : MonoBehaviour
    {
        [SerializeField] private Transform tileRoot;
        [SerializeField] private Transform overlayRoot;
        [SerializeField] private ArenaGridVisualConfig visualConfig;
        [SerializeField, Range(0.2f, 1f)] private float disconnectedBrightness = 0.65f;
        [SerializeField, Range(0.1f, 1f)] private float tileBrightness = 0.55f;
        [SerializeField, Range(0f, 1f)] private float paintOpacity = 0.55f;
        public const int Size = 21;
        private readonly Renderer[,] tiles = new Renderer[Size, Size];
        private readonly int[,] displayedState = new int[Size, Size];
        private Mesh overlayMesh;
        private Material overlayMaterial;
        private readonly bool[][,] connected = { new bool[Size, Size], new bool[Size, Size], new bool[Size, Size], new bool[Size, Size] };
        private readonly Color[] colors = { new Color(0.94f, 0.27f, 0.28f), new Color(0.25f, 0.51f, 0.95f), new Color(0.26f, 0.79f, 0.43f), new Color(0.94f, 0.7f, 0.19f) };
        private MaterialPropertyBlock block;

        public void Initialize()
        {
            block = new MaterialPropertyBlock();
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            overlayMaterial = new Material(shader);
            if (overlayMaterial.HasProperty("_Surface")) overlayMaterial.SetFloat("_Surface", 1f);
            overlayMaterial.SetOverrideTag("RenderType", "Transparent");
            overlayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            overlayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            overlayMaterial.SetInt("_ZWrite", 0);
            overlayMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            overlayMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            overlayMesh = new Mesh { name = "Arena Color Overlay" };
            overlayMesh.vertices = new[] { new Vector3(-0.48f, 0, -0.48f), new Vector3(-0.48f, 0, 0.48f), new Vector3(0.48f, 0, 0.48f), new Vector3(0.48f, 0, -0.48f) };
            overlayMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            overlayMesh.RecalculateNormals();
            foreach (Transform tile in tileRoot)
            {
                var cell = WorldToCell(tile.position);
                var original = tile.GetComponentInChildren<Renderer>();
                if (original == null) continue;
                var overlay = new GameObject("Color Overlay " + cell.X + "," + cell.Y, typeof(MeshFilter), typeof(MeshRenderer));
                overlay.transform.SetParent(overlayRoot, false);
                overlay.transform.position = new Vector3(tile.position.x, original.bounds.max.y + 0.015f, tile.position.z);
                overlay.GetComponent<MeshFilter>().sharedMesh = overlayMesh;
                var renderer = overlay.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = overlayMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
                tiles[cell.X, cell.Y] = renderer;
                displayedState[cell.X, cell.Y] = -1;
            }
        }

        private void OnDestroy()
        {
            if (overlayMesh != null) Destroy(overlayMesh);
            if (overlayMaterial != null) Destroy(overlayMaterial);
        }

        public GridPosition WorldToCell(Vector3 world) => new GridPosition(Mathf.Clamp(Mathf.FloorToInt(world.x + Size / 2f), 0, Size - 1), Mathf.Clamp(Mathf.FloorToInt(world.z + Size / 2f), 0, Size - 1));
        public Vector3 CellToWorld(GridPosition cell) => new Vector3(cell.X - Size / 2f + 0.5f, 0f, cell.Y - Size / 2f + 0.5f);
        public Color PlayerColor(int id) => visualConfig != null ? visualConfig.PlayerColor(id, colors[id]) : colors[id];
        public bool IsConnected(int id, GridPosition cell) => connected[id][cell.X, cell.Y];

        public void Refresh(GridModel grid)
        {
            for (int i = 0; i < colors.Length; i++) grid.CopyConnectedTo(i, connected[i]);
            float brightness = visualConfig != null ? visualConfig.TileBrightness : tileBrightness;
            float disconnected = visualConfig != null ? visualConfig.DisconnectedBrightness : disconnectedBrightness;
            float opacity = visualConfig != null ? visualConfig.PaintOpacity : paintOpacity;
            for (int x = 0; x < Size; x++) for (int y = 0; y < Size; y++)
            {
                var renderer = tiles[x, y];
                if (renderer == null) continue;
                int? owner = grid.IsBlocked(new GridPosition(x, y)) ? null : grid.OwnerAt(new GridPosition(x, y));
                int state = owner.HasValue ? owner.Value * 2 + (connected[owner.Value][x, y] ? 1 : 0) : -1;
                if (state == displayedState[x, y]) continue;
                displayedState[x, y] = state;
                renderer.enabled = owner.HasValue;
                Color color = owner.HasValue ? PlayerColor(owner.Value) * brightness : Color.white;
                if (owner.HasValue && !connected[owner.Value][x, y]) color *= disconnected;
                color.a = Mathf.Clamp01(opacity);
                block.Clear();
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
