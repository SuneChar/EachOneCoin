using System;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    [CreateAssetMenu(menuName = "EachOneCoin/Characters/Appearance Profile", fileName = "CharacterAppearance")]
    public sealed class CharacterAppearanceProfile : ScriptableObject
    {
        [SerializeField] private string appearanceId;
        [SerializeField] private string accessorySlotName;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite previewIcon;

        [Header("Portrait UI Visuals")]
        [SerializeField] private Sprite portraitBaseOverride;
        [SerializeField] private Sprite portraitFrameOverride;
        [SerializeField] private PortraitOverlayLayer[] portraitOverlayLayers = Array.Empty<PortraitOverlayLayer>();

        public string AppearanceId => appearanceId;
        public string AccessorySlotName => accessorySlotName;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? (appearanceId ?? string.Empty) : displayName;
        public Sprite PreviewIcon => previewIcon;
        public bool IsEmptyOutfit => string.IsNullOrWhiteSpace(accessorySlotName);

        public Sprite PortraitBaseOverride => portraitBaseOverride;
        public Sprite PortraitFrameOverride => portraitFrameOverride;
        public PortraitOverlayLayer[] PortraitOverlayLayers => portraitOverlayLayers;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (portraitOverlayLayers == null) return;
            for (int i = 0; i < portraitOverlayLayers.Length; i++)
            {
                PortraitOverlayLayer layer = portraitOverlayLayers[i];
                bool modified = false;

                if (float.IsNaN(layer.NormalizedCenter.x) || float.IsInfinity(layer.NormalizedCenter.x))
                {
                    layer.NormalizedCenter.x = 0.5f;
                    modified = true;
                }
                if (float.IsNaN(layer.NormalizedCenter.y) || float.IsInfinity(layer.NormalizedCenter.y))
                {
                    layer.NormalizedCenter.y = 0.5f;
                    modified = true;
                }
                if (float.IsNaN(layer.NormalizedSize.x) || float.IsInfinity(layer.NormalizedSize.x) || layer.NormalizedSize.x < 0f)
                {
                    layer.NormalizedSize.x = 0f;
                    modified = true;
                }
                if (float.IsNaN(layer.NormalizedSize.y) || float.IsInfinity(layer.NormalizedSize.y) || layer.NormalizedSize.y < 0f)
                {
                    layer.NormalizedSize.y = 0f;
                    modified = true;
                }
                if (float.IsNaN(layer.RotationZ) || float.IsInfinity(layer.RotationZ))
                {
                    layer.RotationZ = 0f;
                    modified = true;
                }

                if (modified)
                {
                    portraitOverlayLayers[i] = layer;
                }
            }
        }
#endif
    }
}
