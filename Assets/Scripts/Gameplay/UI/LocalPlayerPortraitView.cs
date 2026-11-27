using System;
using System.Collections.Generic;
using EachOneCoin;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    public sealed class LocalPlayerPortraitView : MonoBehaviour
    {
        [Header("Layout (1920x1080 Reference Canvas)")]
        [SerializeField] private Vector2 bottomLeftOffset = new Vector2(-24f, -24f);
        [SerializeField, Min(48f)] private float portraitSize = 378f;
        [SerializeField, Min(0f)] private float maskInset = 2f;
        [SerializeField] private Vector2 portraitAnchorMin = new Vector2(0.04f, 0.00f);
        [SerializeField] private Vector2 portraitAnchorMax = new Vector2(0.96f, 0.96f);

        [Header("Colors")]
        [SerializeField] private Color circleBackgroundColor = new Color(0.102f, 0.129f, 0.184f, 0.92f);
        [SerializeField, Range(0f, 1f)] private float backgroundFactionTintWeight = 0.22f;
        [SerializeField] private Color frameBaseColor = new Color(0.95f, 0.97f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float frameFactionTintWeight = 0.45f;
        [SerializeField] private Color wonFrameAccentColor = new Color(1f, 0.86f, 0.36f, 1f);
        [SerializeField] private Color respawningOverlayColor = new Color(0.05f, 0.08f, 0.14f, 0.44f);
        [SerializeField] private Color lostOverlayColor = new Color(0.14f, 0.15f, 0.18f, 0.54f);
        [SerializeField] private Color lostBackgroundColor = new Color(0.11f, 0.12f, 0.14f, 0.88f);
        [SerializeField] private Color lostFrameColor = new Color(0.45f, 0.48f, 0.53f, 0.88f);

        [Header("Hit Flash")]
        [SerializeField, Min(0.05f)] private float hitFlashDuration = 0.28f;
        [SerializeField] private Color hitFlashOverlayColor = new Color(0.96f, 0.16f, 0.16f, 0.62f);
        [SerializeField] private Color hitFlashFrameColor = new Color(1f, 0.28f, 0.24f, 1f);
        [SerializeField, Min(0f)] private float hitShakeAmplitude = 4.5f;

        private readonly List<Image> overlayImagePool = new List<Image>();
        private readonly List<Sprite> appliedOverlaySprites = new List<Sprite>();

        private HudProceduralIcons icons;
        private IAvatarAppearanceProvider appearanceProvider;
        private TMP_FontAsset cachedFont;

        private RectTransform rootRect;
        [Header("Prefab References (optional)")]
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private RectTransform circleMaskRect;
        [SerializeField] private Image circleBackgroundImage;
        [SerializeField] private Mask circleMask;
        [SerializeField] private Image basePortraitImage;
        [SerializeField] private RectTransform overlayContainer;
        [SerializeField] private Image statusOverlayImage;
        [SerializeField] private Image frameImage;

        private bool initialized;
        private bool hasParticipant;
        private AvatarAppearanceData currentAppearance;
        private Color fallbackTintColor = Color.white;
        private float hitFlashRemaining;

        public Vector2 BottomLeftOffset => bottomLeftOffset;
        public float PortraitSize => portraitSize;
        public int CurrentSlotId { get; private set; } = -1;
        public string CurrentCosmeticId { get; private set; }
        public int ActiveOverlayCount { get; private set; }
        public ParticipantStatus CurrentStatus { get; private set; } = ParticipantStatus.Active;
        public bool IsHitFlashing => hitFlashRemaining > 0f;

        public void Initialize(IAvatarAppearanceProvider provider, TMP_FontAsset font = null)
        {
            if (font != null)
            {
                cachedFont = font;
            }

            if (initialized)
            {
                if (provider != null && !ReferenceEquals(appearanceProvider, provider))
                {
                    SetAppearanceProvider(provider);
                }
                return;
            }

            if (contentRoot != null && circleMaskRect != null && circleBackgroundImage != null &&
                circleMask != null && basePortraitImage != null && overlayContainer != null &&
                statusOverlayImage != null && frameImage != null)
            {
                if (icons == null) icons = new HudProceduralIcons();
                initialized = true;
                SetAppearanceProvider(provider);
                ApplyStatusAndFlashVisuals();
                return;
            }

            rootRect = transform as RectTransform;
            if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.zero;
            rootRect.pivot = Vector2.zero;
            rootRect.anchoredPosition = bottomLeftOffset;
            rootRect.sizeDelta = new Vector2(portraitSize, portraitSize);

            if (icons == null) icons = new HudProceduralIcons();

            Sprite circleMaskSprite = icons.GetCircleMaskSprite();
            Sprite circleFrameSprite = icons.GetCircleFrameSprite();
            Sprite defaultPortraitSprite = icons.GetDefaultPortraitSprite();

            var contentObj = new GameObject("ContentRoot", typeof(RectTransform));
            contentRoot = (RectTransform)contentObj.transform;
            contentRoot.SetParent(rootRect, false);
            SetStretch(contentRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.localScale = Vector3.one;

            var maskObj = new GameObject("CircleMaskRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            circleMaskRect = (RectTransform)maskObj.transform;
            circleMaskRect.SetParent(contentRoot, false);
            float inset = Mathf.Clamp(maskInset, 0f, portraitSize * 0.25f);
            SetStretch(circleMaskRect, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));

            circleBackgroundImage = maskObj.GetComponent<Image>();
            circleBackgroundImage.sprite = circleMaskSprite;
            circleBackgroundImage.preserveAspect = true;
            circleBackgroundImage.color = circleBackgroundColor;
            circleBackgroundImage.raycastTarget = false;

            circleMask = maskObj.GetComponent<Mask>();
            circleMask.showMaskGraphic = true;

            var portraitObj = new GameObject("BasePortraitImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var portraitRect = (RectTransform)portraitObj.transform;
            portraitRect.SetParent(circleMaskRect, false);
            SetStretch(portraitRect, portraitAnchorMin, portraitAnchorMax, Vector2.zero, Vector2.zero);

            basePortraitImage = portraitObj.GetComponent<Image>();
            basePortraitImage.sprite = defaultPortraitSprite;
            basePortraitImage.enabled = defaultPortraitSprite != null;
            basePortraitImage.preserveAspect = true;
            basePortraitImage.color = Color.white;
            basePortraitImage.raycastTarget = false;

            var overlayObj = new GameObject("OverlayContainer", typeof(RectTransform));
            overlayContainer = (RectTransform)overlayObj.transform;
            overlayContainer.SetParent(circleMaskRect, false);
            SetStretch(overlayContainer, portraitAnchorMin, portraitAnchorMax, Vector2.zero, Vector2.zero);

            var statusObj = new GameObject("StatusOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var statusRect = (RectTransform)statusObj.transform;
            statusRect.SetParent(circleMaskRect, false);
            SetStretch(statusRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            statusOverlayImage = statusObj.GetComponent<Image>();
            statusOverlayImage.sprite = circleMaskSprite;
            statusOverlayImage.preserveAspect = true;
            statusOverlayImage.color = Color.clear;
            statusOverlayImage.raycastTarget = false;
            statusOverlayImage.enabled = false;

            var frameObj = new GameObject("FrameImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var frameRect = (RectTransform)frameObj.transform;
            frameRect.SetParent(contentRoot, false);
            SetStretch(frameRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            frameImage = frameObj.GetComponent<Image>();
            frameImage.sprite = circleFrameSprite;
            frameImage.enabled = circleFrameSprite != null;
            frameImage.preserveAspect = true;
            frameImage.color = frameBaseColor;
            frameImage.raycastTarget = false;

            initialized = true;
            SetAppearanceProvider(provider);
            ApplyStatusAndFlashVisuals();
        }

        public void SetAppearanceProvider(IAvatarAppearanceProvider provider)
        {
            if (ReferenceEquals(appearanceProvider, provider))
            {
                return;
            }

            if (appearanceProvider != null)
            {
                appearanceProvider.AppearanceChanged -= OnProviderAppearanceChanged;
            }

            appearanceProvider = provider;

            if (appearanceProvider != null)
            {
                appearanceProvider.AppearanceChanged += OnProviderAppearanceChanged;

                if (initialized && hasParticipant)
                {
                    AvatarAppearanceData refreshed = appearanceProvider.GetAppearance(CurrentSlotId, fallbackTintColor);
                    if (HasAppearanceChanged(refreshed))
                    {
                        ApplyAppearance(refreshed);
                        ApplyStatusAndFlashVisuals();
                    }
                }
            }
        }

        public void SetParticipant(int slotId, AvatarAppearanceData appearance, ParticipantStatus status)
        {
            if (!initialized)
            {
                Initialize(appearanceProvider, cachedFont);
            }

            bool slotChanged = !hasParticipant || CurrentSlotId != slotId;
            bool statusChanged = !hasParticipant || CurrentStatus != status;
            bool appearanceChanged = !hasParticipant || HasAppearanceChanged(appearance);

            if (!slotChanged && !statusChanged && !appearanceChanged)
            {
                return;
            }

            hasParticipant = true;
            CurrentSlotId = slotId;
            CurrentStatus = status;

            if (slotChanged || appearance.TintColor.a > 0.001f)
            {
                fallbackTintColor = appearance.TintColor.a > 0.001f ? appearance.TintColor : Color.white;
            }

            if (appearanceChanged)
            {
                ApplyAppearance(appearance);
            }

            if (appearanceChanged || statusChanged || slotChanged)
            {
                ApplyStatusAndFlashVisuals();
            }
        }

        public void TriggerHitFlash()
        {
            if (!initialized)
            {
                Initialize(appearanceProvider, cachedFont);
            }

            hitFlashRemaining = Mathf.Max(0.05f, hitFlashDuration);
            ApplyStatusAndFlashVisuals();
        }

        public void Tick(float deltaTime)
        {
            if (!initialized || hitFlashRemaining <= 0f || deltaTime <= 0f)
            {
                return;
            }

            hitFlashRemaining = Mathf.Max(0f, hitFlashRemaining - deltaTime);
            ApplyStatusAndFlashVisuals();
        }

        private void OnDestroy()
        {
            if (appearanceProvider != null)
            {
                appearanceProvider.AppearanceChanged -= OnProviderAppearanceChanged;
                appearanceProvider = null;
            }

            if (icons != null)
            {
                icons.Dispose();
                icons = null;
            }
        }

        private void OnProviderAppearanceChanged(int changedSlotId)
        {
            if (!initialized || !hasParticipant || appearanceProvider == null)
            {
                return;
            }

            if (changedSlotId != CurrentSlotId)
            {
                return;
            }

            AvatarAppearanceData updated = appearanceProvider.GetAppearance(CurrentSlotId, fallbackTintColor);
            if (HasAppearanceChanged(updated))
            {
                ApplyAppearance(updated);
                ApplyStatusAndFlashVisuals();
            }
        }

        private bool HasAppearanceChanged(AvatarAppearanceData incoming)
        {
            if (currentAppearance.BasePortrait != incoming.BasePortrait) return true;
            if (currentAppearance.FrameSprite != incoming.FrameSprite) return true;
            if (!ColorsEqual(currentAppearance.TintColor, incoming.TintColor)) return true;
            if (!string.Equals(currentAppearance.CosmeticId, incoming.CosmeticId, StringComparison.Ordinal)) return true;
            return !AreOverlaysUnchanged(incoming.OverlaySprites);
        }

        private bool AreOverlaysUnchanged(Sprite[] incoming)
        {
            int incomingNonNull = 0;
            if (incoming != null)
            {
                for (int i = 0; i < incoming.Length; i++)
                {
                    Sprite sprite = incoming[i];
                    if (sprite == null) continue;
                    if (incomingNonNull >= appliedOverlaySprites.Count) return false;
                    if (appliedOverlaySprites[incomingNonNull] != sprite) return false;
                    incomingNonNull++;
                }
            }

            return incomingNonNull == appliedOverlaySprites.Count;
        }

        private void ApplyAppearance(AvatarAppearanceData appearance)
        {
            currentAppearance = appearance;
            CurrentCosmeticId = appearance.CosmeticId;

            if (basePortraitImage != null)
            {
                Sprite portraitSprite = appearance.BasePortrait != null
                    ? appearance.BasePortrait
                    : (icons != null ? icons.GetDefaultPortraitSprite() : null);
                basePortraitImage.sprite = portraitSprite;
                basePortraitImage.enabled = portraitSprite != null;
            }

            if (frameImage != null)
            {
                Sprite frameSprite = appearance.FrameSprite != null
                    ? appearance.FrameSprite
                    : (icons != null ? icons.GetCircleFrameSprite() : null);
                frameImage.sprite = frameSprite;
                frameImage.enabled = frameSprite != null;
            }

            appliedOverlaySprites.Clear();
            int activeCount = 0;
            Sprite[] overlays = appearance.OverlaySprites;
            if (overlays != null)
            {
                for (int i = 0; i < overlays.Length; i++)
                {
                    Sprite overlaySprite = overlays[i];
                    if (overlaySprite == null) continue;

                    Image overlayImg = GetOrCreateOverlayImage(activeCount);
                    overlayImg.sprite = overlaySprite;
                    overlayImg.enabled = true;
                    if (!overlayImg.gameObject.activeSelf)
                    {
                        overlayImg.gameObject.SetActive(true);
                    }

                    appliedOverlaySprites.Add(overlaySprite);
                    activeCount++;
                }
            }

            for (int i = activeCount; i < overlayImagePool.Count; i++)
            {
                Image unused = overlayImagePool[i];
                if (unused == null) continue;
                unused.sprite = null;
                unused.enabled = false;
                if (unused.gameObject.activeSelf)
                {
                    unused.gameObject.SetActive(false);
                }
            }

            ActiveOverlayCount = activeCount;
        }

        private Image GetOrCreateOverlayImage(int index)
        {
            while (overlayImagePool.Count <= index)
            {
                var go = new GameObject(
                    "Overlay_" + overlayImagePool.Count,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(overlayContainer, false);
                SetStretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                var img = go.GetComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = Color.white;
                overlayImagePool.Add(img);
            }

            return overlayImagePool[index];
        }

        private void ApplyStatusAndFlashVisuals()
        {
            Color factionTint = ResolveTintColor(currentAppearance.TintColor);
            Color baseBgColor = ComputeBackdropColor(factionTint);
            Color basePortraitColor;
            Color overlayColor;
            Color baseFrameColor;
            bool hasBaseStatusOverlay;
            Color baseStatusOverlayColor;

            switch (CurrentStatus)
            {
                case ParticipantStatus.Respawning:
                    basePortraitColor = new Color(
                        factionTint.r * 0.65f,
                        factionTint.g * 0.68f,
                        factionTint.b * 0.74f,
                        0.76f);
                    overlayColor = new Color(0.68f, 0.72f, 0.78f, 0.76f);
                    baseFrameColor = DimColor(ComputeActiveFrameColor(factionTint), 0.66f, 0.82f);
                    hasBaseStatusOverlay = true;
                    baseStatusOverlayColor = respawningOverlayColor;
                    break;

                case ParticipantStatus.Lost:
                    float lum = factionTint.r * 0.299f + factionTint.g * 0.587f + factionTint.b * 0.114f;
                    float gray = Mathf.Lerp(0.30f, 0.54f, lum);
                    baseBgColor = lostBackgroundColor;
                    basePortraitColor = new Color(gray, gray, gray * 1.03f, 0.78f);
                    overlayColor = new Color(0.46f, 0.48f, 0.52f, 0.78f);
                    baseFrameColor = lostFrameColor;
                    hasBaseStatusOverlay = true;
                    baseStatusOverlayColor = lostOverlayColor;
                    break;

                case ParticipantStatus.Won:
                    basePortraitColor = factionTint;
                    overlayColor = Color.white;
                    baseFrameColor = Color.Lerp(ComputeActiveFrameColor(factionTint), wonFrameAccentColor, 0.45f);
                    hasBaseStatusOverlay = false;
                    baseStatusOverlayColor = Color.clear;
                    break;

                default:
                    basePortraitColor = factionTint;
                    overlayColor = Color.white;
                    baseFrameColor = ComputeActiveFrameColor(factionTint);
                    hasBaseStatusOverlay = false;
                    baseStatusOverlayColor = Color.clear;
                    break;
            }

            if (circleBackgroundImage != null)
            {
                circleBackgroundImage.color = baseBgColor;
            }

            if (basePortraitImage != null)
            {
                basePortraitImage.color = basePortraitColor;
            }

            for (int i = 0; i < ActiveOverlayCount && i < overlayImagePool.Count; i++)
            {
                Image overlayImg = overlayImagePool[i];
                if (overlayImg != null)
                {
                    overlayImg.color = overlayColor;
                }
            }

            float flashRatio = 0f;
            if (hitFlashRemaining > 0f)
            {
                float duration = Mathf.Max(0.05f, hitFlashDuration);
                flashRatio = Mathf.Clamp01(hitFlashRemaining / duration);
            }

            if (frameImage != null)
            {
                frameImage.color = flashRatio > 0f
                    ? Color.Lerp(baseFrameColor, hitFlashFrameColor, flashRatio * 0.78f)
                    : baseFrameColor;
            }

            if (statusOverlayImage != null)
            {
                if (flashRatio > 0f)
                {
                    statusOverlayImage.enabled = true;
                    if (hasBaseStatusOverlay)
                    {
                        statusOverlayImage.color = Color.Lerp(baseStatusOverlayColor, hitFlashOverlayColor, flashRatio);
                    }
                    else
                    {
                        statusOverlayImage.color = new Color(
                            hitFlashOverlayColor.r,
                            hitFlashOverlayColor.g,
                            hitFlashOverlayColor.b,
                            hitFlashOverlayColor.a * flashRatio);
                    }
                }
                else if (hasBaseStatusOverlay)
                {
                    statusOverlayImage.enabled = true;
                    statusOverlayImage.color = baseStatusOverlayColor;
                }
                else
                {
                    statusOverlayImage.color = Color.clear;
                    statusOverlayImage.enabled = false;
                }
            }

            if (contentRoot != null)
            {
                if (flashRatio > 0f && hitShakeAmplitude > 0f)
                {
                    float shakeX = Mathf.Sin((1f - flashRatio) * Mathf.PI * 5f) * hitShakeAmplitude * flashRatio;
                    contentRoot.anchoredPosition = new Vector2(shakeX, 0f);
                }
                else
                {
                    contentRoot.anchoredPosition = Vector2.zero;
                }
            }
        }

        private Color ComputeBackdropColor(Color factionTint)
        {
            Color mutedFaction = new Color(
                factionTint.r * 0.34f,
                factionTint.g * 0.34f,
                factionTint.b * 0.38f,
                circleBackgroundColor.a);
            Color blended = Color.Lerp(circleBackgroundColor, mutedFaction, backgroundFactionTintWeight);
            blended.a = circleBackgroundColor.a;
            return blended;
        }

        private Color ComputeActiveFrameColor(Color factionTint)
        {
            Color blended = Color.Lerp(frameBaseColor, factionTint, frameFactionTintWeight);
            blended.a = frameBaseColor.a;
            return blended;
        }

        private static Color DimColor(Color color, float rgbScale, float alphaScale)
        {
            return new Color(
                color.r * rgbScale,
                color.g * rgbScale,
                color.b * rgbScale,
                color.a * alphaScale);
        }

        private Color ResolveTintColor(Color tint)
        {
            if (tint.a <= 0.001f)
            {
                return fallbackTintColor.a > 0.001f ? fallbackTintColor : Color.white;
            }
            return tint;
        }

        private static bool ColorsEqual(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) &&
                   Mathf.Approximately(a.g, b.g) &&
                   Mathf.Approximately(a.b, b.b) &&
                   Mathf.Approximately(a.a, b.a);
        }

        private static void SetStretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
