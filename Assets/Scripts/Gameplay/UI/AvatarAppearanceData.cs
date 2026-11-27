using System;
using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    [Serializable]
    public struct AvatarAppearanceData
    {
        public Sprite BasePortrait;
        public Color TintColor;
        public Sprite FrameSprite;
        public Sprite[] OverlaySprites;
        public string CosmeticId;

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
                CosmeticId = cosmeticId
            };
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
