using System;
using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    [Serializable]
    public struct PortraitOverlayLayer
    {
        public Sprite Sprite;
        public Vector2 NormalizedCenter;
        public Vector2 NormalizedSize;
        public float RotationZ;
        public bool UseFactionTint;

        public static PortraitOverlayLayer CreateFullRect(Sprite sprite, bool useFactionTint = false)
        {
            return new PortraitOverlayLayer
            {
                Sprite = sprite,
                NormalizedCenter = new Vector2(0.5f, 0.5f),
                NormalizedSize = new Vector2(1f, 1f),
                RotationZ = 0f,
                UseFactionTint = useFactionTint
            };
        }

        public bool EqualsLayer(PortraitOverlayLayer other)
        {
            return Sprite == other.Sprite
                && Mathf.Approximately(NormalizedCenter.x, other.NormalizedCenter.x)
                && Mathf.Approximately(NormalizedCenter.y, other.NormalizedCenter.y)
                && Mathf.Approximately(NormalizedSize.x, other.NormalizedSize.x)
                && Mathf.Approximately(NormalizedSize.y, other.NormalizedSize.y)
                && Mathf.Approximately(RotationZ, other.RotationZ)
                && UseFactionTint == other.UseFactionTint;
        }
    }

    [Serializable]
    public struct AvatarAppearanceData
    {
        public Sprite BasePortrait;
        public Color TintColor;
        public Sprite FrameSprite;
        public Sprite[] OverlaySprites;
        public string CosmeticId;
        public PortraitOverlayLayer[] OverlayLayers;

        public static AvatarAppearanceData CreateDefault(
            Color tintColor,
            Sprite basePortrait = null,
            Sprite frameSprite = null,
            string cosmeticId = null)
        {
            return new AvatarAppearanceData
            {
                BasePortrait = basePortrait,
                TintColor = tintColor,
                FrameSprite = frameSprite,
                OverlaySprites = null,
                CosmeticId = cosmeticId,
                OverlayLayers = null
            };
        }

        public static AvatarAppearanceData FromProfile(
            CharacterAppearanceProfile profile,
            Color factionColor)
        {
            return FromProfile(profile, factionColor, null);
        }

        public static AvatarAppearanceData FromProfile(
            CharacterAppearanceProfile profile,
            Color factionColor,
            Sprite defaultPortrait)
        {
            if (profile == null)
            {
                return CreateDefault(factionColor, defaultPortrait);
            }

            string cosmeticId = string.IsNullOrWhiteSpace(profile.AppearanceId)
                ? null
                : profile.AppearanceId;

            return new AvatarAppearanceData
            {
                BasePortrait = profile.PortraitBaseOverride != null ? profile.PortraitBaseOverride : defaultPortrait,
                TintColor = factionColor,
                FrameSprite = profile.PortraitFrameOverride,
                OverlaySprites = null,
                CosmeticId = cosmeticId,
                OverlayLayers = profile.PortraitOverlayLayers
            };
        }

        public static bool ContentEquals(AvatarAppearanceData a, AvatarAppearanceData b)
        {
            if (a.BasePortrait != b.BasePortrait) return false;
            if (a.FrameSprite != b.FrameSprite) return false;
            if (!ColorsEqual(a.TintColor, b.TintColor)) return false;
            if (!string.Equals(a.CosmeticId, b.CosmeticId, StringComparison.Ordinal)) return false;
            return AvatarPortraitVisualRenderer.AreOverlaysEqual(a, b);
        }

        private static bool ColorsEqual(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r)
                && Mathf.Approximately(a.g, b.g)
                && Mathf.Approximately(a.b, b.b)
                && Mathf.Approximately(a.a, b.a);
        }
    }

    public interface IAvatarAppearanceProvider
    {
        AvatarAppearanceData GetAppearance(int slotId, Color fallbackColor);
        event Action<int> AppearanceChanged;
    }

    public sealed class DefaultAvatarAppearanceProvider : IAvatarAppearanceProvider
    {
        private const string DefaultPortraitResourcePath = "UI/HUD/PortraitDefault";

        private readonly Dictionary<int, AvatarAppearanceData> overrides = new Dictionary<int, AvatarAppearanceData>();
        private Sprite cachedDefaultPortrait;
        private bool defaultPortraitLoaded;

        public event Action<int> AppearanceChanged;

        public DefaultAvatarAppearanceProvider()
        {
        }

        public DefaultAvatarAppearanceProvider(Sprite defaultPortrait)
        {
            if (defaultPortrait != null)
            {
                cachedDefaultPortrait = defaultPortrait;
                defaultPortraitLoaded = true;
            }
        }

        public AvatarAppearanceData GetAppearance(int slotId, Color fallbackColor)
        {
            if (overrides.TryGetValue(slotId, out AvatarAppearanceData customAppearance))
            {
                return customAppearance;
            }

            return AvatarAppearanceData.CreateDefault(fallbackColor, ResolveDefaultPortrait());
        }

        public void SetOverride(int slotId, AvatarAppearanceData appearance)
        {
            overrides[slotId] = appearance;
            AppearanceChanged?.Invoke(slotId);
        }

        public void ClearOverride(int slotId)
        {
            if (overrides.Remove(slotId))
            {
                AppearanceChanged?.Invoke(slotId);
            }
        }

        private Sprite ResolveDefaultPortrait()
        {
            if (!defaultPortraitLoaded)
            {
                defaultPortraitLoaded = true;
                cachedDefaultPortrait = Resources.Load<Sprite>(DefaultPortraitResourcePath);
            }

            return cachedDefaultPortrait;
        }
    }
}
