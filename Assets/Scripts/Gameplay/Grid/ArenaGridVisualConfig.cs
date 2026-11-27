using UnityEngine;

namespace EachOneCoin.Gameplay
{
    [CreateAssetMenu(menuName = "EachOneCoin/Grid/Visual Config", fileName = "ArenaGridVisualConfig")]
    public sealed class ArenaGridVisualConfig : ScriptableObject
    {
        [SerializeField] private Color[] playerColors =
        {
            new Color(0.94f, 0.27f, 0.28f),
            new Color(0.25f, 0.51f, 0.95f),
            new Color(0.26f, 0.79f, 0.43f),
            new Color(0.94f, 0.7f, 0.19f)
        };
        [SerializeField, Range(0.2f, 1f)] private float disconnectedBrightness = 0.65f;
        [SerializeField, Range(0.1f, 1f)] private float tileBrightness = 0.85f;
        [SerializeField, Range(0f, 1f)] private float paintOpacity = 0.8f;

        public float DisconnectedBrightness => disconnectedBrightness;
        public float TileBrightness => tileBrightness;
        public float PaintOpacity => paintOpacity;

        public Color PlayerColor(int id, Color fallback)
        {
            return playerColors != null && id >= 0 && id < playerColors.Length ? playerColors[id] : fallback;
        }
    }
}
