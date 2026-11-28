using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    public static class AvatarPortraitVisualRenderer
    {
        public static readonly Vector2 PortraitAnchorMin = new Vector2(0.04f, 0.00f);
        public static readonly Vector2 PortraitAnchorMax = new Vector2(0.96f, 0.96f);

        public static void AlignOverlayContainer(RectTransform overlayContainer)
        {
            if (overlayContainer == null)
            {
                return;
            }

            overlayContainer.anchorMin = PortraitAnchorMin;
            overlayContainer.anchorMax = PortraitAnchorMax;
            overlayContainer.pivot = new Vector2(0.5f, 0.5f);
            overlayContainer.offsetMin = Vector2.zero;
            overlayContainer.offsetMax = Vector2.zero;
            overlayContainer.localScale = Vector3.one;
            overlayContainer.localRotation = Quaternion.identity;
        }

        public static Sprite ResolveBasePortrait(AvatarAppearanceData appearance, HudProceduralIcons icons)
        {
            if (appearance.BasePortrait != null)
            {
                return appearance.BasePortrait;
            }

            return icons != null ? icons.GetDefaultPortraitSprite() : null;
        }

        public static Sprite ResolveFrameSprite(AvatarAppearanceData appearance, HudProceduralIcons icons)
        {
            if (appearance.FrameSprite != null)
            {
                return appearance.FrameSprite;
            }

            return icons != null ? icons.GetCircleFrameSprite() : null;
        }

        public static bool HasValidOverlayLayers(AvatarAppearanceData appearance)
        {
            PortraitOverlayLayer[] layers = appearance.OverlayLayers;
            if (layers == null || layers.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].Sprite != null)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool AreOverlaysEqual(AvatarAppearanceData a, AvatarAppearanceData b)
        {
            bool aHasLayers = HasValidOverlayLayers(a);
            bool bHasLayers = HasValidOverlayLayers(b);
            if (aHasLayers != bHasLayers)
            {
                return false;
            }

            if (aHasLayers)
            {
                return AreValidOverlayLayersEqual(a.OverlayLayers, b.OverlayLayers);
            }

            return AreValidOverlaySpritesEqual(a.OverlaySprites, b.OverlaySprites);
        }

        public static int ApplyOverlays(
            RectTransform overlayContainer,
            List<Image> pool,
            AvatarAppearanceData appearance,
            Color overlayColor,
            Color factionStatusColor,
            bool deactivateUnusedGameObjects)
        {
            if (overlayContainer == null || pool == null)
            {
                return 0;
            }

            int activeCount = 0;

            if (HasValidOverlayLayers(appearance))
            {
                PortraitOverlayLayer[] layers = appearance.OverlayLayers;
                for (int i = 0; i < layers.Length; i++)
                {
                    PortraitOverlayLayer layer = layers[i];
                    if (layer.Sprite == null)
                    {
                        continue;
                    }

                    Image overlayImg = GetOrCreateOverlayImage(overlayContainer, pool, activeCount);
                    RectTransform rt = overlayImg.rectTransform;
                    float halfW = Mathf.Max(0f, layer.NormalizedSize.x) * 0.5f;
                    float halfH = Mathf.Max(0f, layer.NormalizedSize.y) * 0.5f;
                    rt.anchorMin = new Vector2(layer.NormalizedCenter.x - halfW, layer.NormalizedCenter.y - halfH);
                    rt.anchorMax = new Vector2(layer.NormalizedCenter.x + halfW, layer.NormalizedCenter.y + halfH);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.localScale = Vector3.one;
                    rt.localRotation = Quaternion.Euler(0f, 0f, layer.RotationZ);

                    if (overlayImg.sprite != layer.Sprite)
                    {
                        overlayImg.sprite = layer.Sprite;
                    }
                    if (overlayImg.preserveAspect)
                    {
                        overlayImg.preserveAspect = false;
                    }
                    overlayImg.raycastTarget = false;
                    overlayImg.maskable = true;

                    Color targetColor = layer.UseFactionTint ? factionStatusColor : overlayColor;
                    if (!ColorsApproximatelyEqual(overlayImg.color, targetColor))
                    {
                        overlayImg.color = targetColor;
                    }

                    if (!overlayImg.enabled)
                    {
                        overlayImg.enabled = true;
                    }
                    if (!overlayImg.gameObject.activeSelf)
                    {
                        overlayImg.gameObject.SetActive(true);
                    }

                    activeCount++;
                }
            }
            else if (appearance.OverlaySprites != null && appearance.OverlaySprites.Length > 0)
            {
                Sprite[] sprites = appearance.OverlaySprites;
                for (int i = 0; i < sprites.Length; i++)
                {
                    Sprite overlaySprite = sprites[i];
                    if (overlaySprite == null)
                    {
                        continue;
                    }

                    Image overlayImg = GetOrCreateOverlayImage(overlayContainer, pool, activeCount);
                    RectTransform rt = overlayImg.rectTransform;
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.localScale = Vector3.one;
                    rt.localRotation = Quaternion.identity;

                    if (overlayImg.sprite != overlaySprite)
                    {
                        overlayImg.sprite = overlaySprite;
                    }
                    if (!overlayImg.preserveAspect)
                    {
                        overlayImg.preserveAspect = true;
                    }
                    overlayImg.raycastTarget = false;
                    overlayImg.maskable = true;

                    if (!ColorsApproximatelyEqual(overlayImg.color, overlayColor))
                    {
                        overlayImg.color = overlayColor;
                    }

                    if (!overlayImg.enabled)
                    {
                        overlayImg.enabled = true;
                    }
                    if (!overlayImg.gameObject.activeSelf)
                    {
                        overlayImg.gameObject.SetActive(true);
                    }

                    activeCount++;
                }
            }

            for (int i = activeCount; i < pool.Count; i++)
            {
                Image unused = pool[i];
                if (unused == null)
                {
                    continue;
                }

                if (deactivateUnusedGameObjects)
                {
                    unused.sprite = null;
                    if (unused.enabled)
                    {
                        unused.enabled = false;
                    }
                    if (unused.gameObject.activeSelf)
                    {
                        unused.gameObject.SetActive(false);
                    }
                }
                else
                {
                    if (unused.enabled)
                    {
                        unused.enabled = false;
                    }
                }
            }

            return activeCount;
        }

        public static void ApplyOverlayColors(
            List<Image> pool,
            int activeCount,
            AvatarAppearanceData appearance,
            Color overlayColor,
            Color factionStatusColor)
        {
            if (pool == null || activeCount <= 0)
            {
                return;
            }

            int limit = activeCount < pool.Count ? activeCount : pool.Count;

            if (HasValidOverlayLayers(appearance))
            {
                PortraitOverlayLayer[] layers = appearance.OverlayLayers;
                int poolIndex = 0;
                for (int i = 0; i < layers.Length && poolIndex < limit; i++)
                {
                    if (layers[i].Sprite == null)
                    {
                        continue;
                    }

                    Image overlayImg = pool[poolIndex];
                    if (overlayImg != null)
                    {
                        Color targetColor = layers[i].UseFactionTint ? factionStatusColor : overlayColor;
                        if (!ColorsApproximatelyEqual(overlayImg.color, targetColor))
                        {
                            overlayImg.color = targetColor;
                        }
                    }

                    poolIndex++;
                }

                return;
            }

            for (int i = 0; i < limit; i++)
            {
                Image overlayImg = pool[i];
                if (overlayImg != null && !ColorsApproximatelyEqual(overlayImg.color, overlayColor))
                {
                    overlayImg.color = overlayColor;
                }
            }
        }

        private static Image GetOrCreateOverlayImage(RectTransform overlayContainer, List<Image> pool, int index)
        {
            while (pool.Count <= index)
            {
                int nextIndex = pool.Count;
                string childName = "Overlay_" + nextIndex;
                Transform existing = overlayContainer.Find(childName);
                Image img;
                if (existing != null)
                {
                    img = existing.GetComponent<Image>();
                    if (img == null)
                    {
                        img = existing.gameObject.AddComponent<Image>();
                    }
                }
                else
                {
                    var go = new GameObject(
                        childName,
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image));
                    go.layer = overlayContainer.gameObject.layer;
                    var rt = (RectTransform)go.transform;
                    rt.SetParent(overlayContainer, false);
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    img = go.GetComponent<Image>();
                }

                img.raycastTarget = false;
                img.maskable = true;
                img.color = Color.white;
                pool.Add(img);
            }

            Image slotImg = pool[index];
            if (slotImg == null)
            {
                string childName = "Overlay_" + index;
                var go = new GameObject(
                    childName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                go.layer = overlayContainer.gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(overlayContainer, false);
                slotImg = go.GetComponent<Image>();
                slotImg.raycastTarget = false;
                slotImg.maskable = true;
                pool[index] = slotImg;
            }

            return slotImg;
        }

        private static bool AreValidOverlayLayersEqual(PortraitOverlayLayer[] a, PortraitOverlayLayer[] b)
        {
            int ia = 0;
            int ib = 0;
            int lenA = a != null ? a.Length : 0;
            int lenB = b != null ? b.Length : 0;

            while (true)
            {
                while (ia < lenA && a[ia].Sprite == null) ia++;
                while (ib < lenB && b[ib].Sprite == null) ib++;

                bool endA = ia >= lenA;
                bool endB = ib >= lenB;
                if (endA && endB) return true;
                if (endA != endB) return false;

                if (!a[ia].EqualsLayer(b[ib])) return false;
                ia++;
                ib++;
            }
        }

        private static bool AreValidOverlaySpritesEqual(Sprite[] a, Sprite[] b)
        {
            int ia = 0;
            int ib = 0;
            int lenA = a != null ? a.Length : 0;
            int lenB = b != null ? b.Length : 0;

            while (true)
            {
                while (ia < lenA && a[ia] == null) ia++;
                while (ib < lenB && b[ib] == null) ib++;

                bool endA = ia >= lenA;
                bool endB = ib >= lenB;
                if (endA && endB) return true;
                if (endA != endB) return false;

                if (a[ia] != b[ib]) return false;
                ia++;
                ib++;
            }
        }

        private static bool ColorsApproximatelyEqual(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.002f
                && Mathf.Abs(a.g - b.g) < 0.002f
                && Mathf.Abs(a.b - b.b) < 0.002f
                && Mathf.Abs(a.a - b.a) < 0.002f;
        }
    }
}
