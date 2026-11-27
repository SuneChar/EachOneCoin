using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class ArcStaminaBarView : MonoBehaviour
    {
        private const string ResourceFolder = "UI/Stamina/";

        // Matches StaminaArc_*.png (160x360) where the inner fill spans y in [20, 340],
        // so 20% / 40% / 60% / 80% stamina aligns flush with the 4 horizontal tier ticks.
        public const float FillMin = 20f / 360f;
        public const float FillMax = 340f / 360f;

        [Header("Arc Sprites (leave empty to auto-load from Resources/UI/Stamina)")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite fillSprite;
        [SerializeField] private Sprite trailSprite;
        [SerializeField] private Sprite ticksFrameSprite;

        [Header("Layout & Character Follow")]
        [SerializeField] private Vector2 barSize = new Vector2(68f, 153f);
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.9f, 0f);
        [SerializeField] private Vector2 screenOffset = new Vector2(74f, 6f);

        [Header("Colors & Tiers")]
        [SerializeField] private Color normalColor = new Color(0.573f, 0.925f, 0.361f, 1f);
        [SerializeField] private Color trailColor = new Color(1f, 0.976f, 0.882f, 0.94f);
        [SerializeField] private Color dangerColor = new Color(0.961f, 0.298f, 0.251f, 1f);
        [SerializeField, Range(0.05f, 0.5f)] private float lowStaminaFraction = 0.20f;

        [Header("Auto-Hide & Smoothing")]
        [SerializeField] private bool autoHideWhenFull = true;
        [SerializeField, Min(0f)] private float fullHideDelay = 0.80f;
        [SerializeField, Min(0.5f)] private float fadeInSpeed = 8.5f;
        [SerializeField, Min(0.5f)] private float fadeOutSpeed = 3.5f;
        [SerializeField, Min(0f)] private float trailHoldDuration = 0.12f;
        [SerializeField, Min(0.05f)] private float trailDrainSpeed = 0.18f;
        [SerializeField, Min(0.2f)] private float trailCatchupRate = 1.95f;
        [SerializeField, Min(1f)] private float networkSmoothSpeed = 12f;
        [SerializeField, Min(1f)] private float dangerPulseSpeed = 16f;

        private readonly List<Sprite> runtimeCreatedSprites = new List<Sprite>();
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Canvas rootCanvas;
        [Header("Prefab References (optional)")]
        [SerializeField] private UnityEngine.UI.Image backgroundImage;
        [SerializeField] private UnityEngine.UI.Image trailImage;
        [SerializeField] private UnityEngine.UI.Image fillImage;
        [SerializeField] private UnityEngine.UI.Image ticksFrameImage;

        private Transform targetTransform;
        private bool initialized;
        private bool spritesLoaded;
        private bool hasState;
        private bool gameplayVisible = true;
        private bool useSmoothValue;
        private bool isExhausted;

        private float targetRatio = 1f;
        private float displayedRatio = 1f;
        private float trailRatio = 1f;
        private float trailHoldTimer;
        private float fullIdleTimer;
        private float currentAlpha;

        public float NormalizedStamina => targetRatio;
        public float DisplayedRatio => displayedRatio;
        public float TrailRatio => trailRatio;
        public float CurrentAlpha => currentAlpha;
        public bool IsDangerState => isExhausted || displayedRatio < lowStaminaFraction;
        public bool IsHiddenByFull => autoHideWhenFull && !isExhausted && targetRatio >= 0.999f && fullIdleTimer >= fullHideDelay;
        public float MainFillAmount => fillImage != null ? fillImage.fillAmount : 0f;
        public float TrailFillAmount => trailImage != null ? trailImage.fillAmount : 0f;

        public void Initialize()
        {
            if (initialized) return;

            rectTransform = transform as RectTransform;
            if (rectTransform == null) rectTransform = gameObject.AddComponent<RectTransform>();
            bool hasPrefabLayers = backgroundImage != null && trailImage != null && fillImage != null && ticksFrameImage != null;
            if (!hasPrefabLayers)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = barSize;
            }

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            rootCanvas = GetComponentInParent<Canvas>();

            EnsureSpritesLoaded();

            if (!hasPrefabLayers)
            {
                backgroundImage = CreateLayer("Background", backgroundSprite, false);
                trailImage = CreateLayer("TrailFill", trailSprite != null ? trailSprite : fillSprite, true);
                fillImage = CreateLayer("MainFill", fillSprite, true);
                ticksFrameImage = CreateLayer("TicksFrame", ticksFrameSprite, false);
            }

            trailImage.color = trailColor;
            fillImage.color = normalColor;

            targetRatio = 1f;
            displayedRatio = 1f;
            trailRatio = 1f;
            fullIdleTimer = autoHideWhenFull ? fullHideDelay : 0f;
            currentAlpha = autoHideWhenFull ? 0f : 1f;
            canvasGroup.alpha = currentAlpha;

            ApplyFillVisuals(0f);
            initialized = true;
        }

        public void SetTarget(Transform characterTransform)
        {
            targetTransform = characterTransform;
        }

        public void SetGameplayVisible(bool visible)
        {
            if (!initialized) Initialize();
            if (gameplayVisible == visible) return;
            gameplayVisible = visible;
            if (!visible)
            {
                currentAlpha = 0f;
                if (canvasGroup != null) canvasGroup.alpha = 0f;
            }
        }

        public void SetStamina(float current, float max, bool exhausted, bool smooth = false, bool immediate = false)
        {
            if (!initialized) Initialize();

            float ratio = max > 0.0001f ? Mathf.Clamp01(current / max) : 0f;
            float previousTarget = targetRatio;

            targetRatio = ratio;
            isExhausted = exhausted;
            useSmoothValue = smooth;

            if (!hasState || immediate)
            {
                hasState = true;
                displayedRatio = ratio;
                trailRatio = ratio;
                trailHoldTimer = 0f;
                bool startFullHidden = autoHideWhenFull && !exhausted && ratio >= 0.999f;
                fullIdleTimer = startFullHidden ? fullHideDelay : 0f;
                currentAlpha = gameplayVisible ? (startFullHidden ? 0f : 1f) : 0f;
                if (canvasGroup != null) canvasGroup.alpha = currentAlpha;
                ApplyFillVisuals(0f);
                return;
            }

            if (ratio < previousTarget - 0.0005f)
            {
                if (trailRatio <= displayedRatio + 0.002f)
                {
                    trailHoldTimer = trailHoldDuration;
                }
                fullIdleTimer = 0f;
            }
            else if (exhausted || ratio < 0.999f)
            {
                fullIdleTimer = 0f;
            }

            if (!smooth)
            {
                displayedRatio = ratio;
                if (trailRatio < displayedRatio) trailRatio = displayedRatio;
                ApplyFillVisuals(Time.unscaledTime);
            }
        }

        public void TickVisual(float deltaTime, float pulseTime)
        {
            if (!initialized || deltaTime <= 0f) return;

            if (!gameplayVisible)
            {
                currentAlpha = 0f;
                if (canvasGroup != null) canvasGroup.alpha = 0f;
                return;
            }

            if (useSmoothValue)
            {
                float blend = 1f - Mathf.Exp(-networkSmoothSpeed * deltaTime);
                displayedRatio = Mathf.Abs(targetRatio - displayedRatio) < 0.0005f
                    ? targetRatio
                    : Mathf.Lerp(displayedRatio, targetRatio, blend);
            }
            else
            {
                displayedRatio = targetRatio;
            }

            if (trailRatio < displayedRatio)
            {
                trailRatio = displayedRatio;
                trailHoldTimer = 0f;
            }
            else if (trailRatio > displayedRatio)
            {
                float moveDt = deltaTime;
                if (trailHoldTimer > 0f)
                {
                    if (moveDt <= trailHoldTimer)
                    {
                        trailHoldTimer -= moveDt;
                        moveDt = 0f;
                    }
                    else
                    {
                        moveDt -= trailHoldTimer;
                        trailHoldTimer = 0f;
                    }
                }
                if (moveDt > 0f)
                {
                    float lag = trailRatio - displayedRatio;
                    float speed = Mathf.Max(trailDrainSpeed, lag * trailCatchupRate);
                    trailRatio = Mathf.MoveTowards(trailRatio, displayedRatio, speed * moveDt);
                }
            }

            bool isFullIdle = autoHideWhenFull && !isExhausted && targetRatio >= 0.999f && displayedRatio >= 0.995f && trailRatio <= displayedRatio + 0.002f;
            if (isFullIdle)
            {
                fullIdleTimer += deltaTime;
                if (fullIdleTimer >= fullHideDelay)
                {
                    currentAlpha = Mathf.MoveTowards(currentAlpha, 0f, fadeOutSpeed * deltaTime);
                }
            }
            else
            {
                fullIdleTimer = 0f;
                currentAlpha = Mathf.MoveTowards(currentAlpha, 1f, fadeInSpeed * deltaTime);
            }

            if (canvasGroup != null) canvasGroup.alpha = currentAlpha;
            ApplyFillVisuals(pulseTime);
        }

        public void UpdateScreenPosition(Camera activeCamera)
        {
            if (!initialized || canvasGroup == null) return;
            if (!gameplayVisible || currentAlpha <= 0.001f || targetTransform == null || activeCamera == null)
            {
                canvasGroup.alpha = 0f;
                return;
            }

            Vector3 screenPos = activeCamera.WorldToScreenPoint(targetTransform.position + worldOffset);
            if (screenPos.z <= 0f)
            {
                canvasGroup.alpha = 0f;
                return;
            }

            var parentRect = rectTransform.parent as RectTransform;
            if (parentRect == null) return;

            if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
            Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, uiCamera, out Vector2 localPoint))
            {
                rectTransform.anchoredPosition = localPoint + screenOffset;
                canvasGroup.alpha = currentAlpha;
            }
        }

        public static float MapRatioToFillAmount(float ratio)
        {
            float clamped = Mathf.Clamp01(ratio);
            if (clamped <= 0.0001f) return 0f;
            return Mathf.Lerp(FillMin, FillMax, clamped);
        }

        private void ApplyFillVisuals(float pulseTime)
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = MapRatioToFillAmount(displayedRatio);
                if (IsDangerState)
                {
                    float pulse = 0.68f + 0.32f * Mathf.Sin(pulseTime * dangerPulseSpeed);
                    Color c = dangerColor;
                    c.a *= pulse;
                    fillImage.color = c;
                }
                else
                {
                    fillImage.color = normalColor;
                }
            }

            if (trailImage != null)
            {
                bool showTrail = trailRatio > displayedRatio + 0.002f;
                trailImage.enabled = showTrail;
                trailImage.fillAmount = showTrail ? MapRatioToFillAmount(trailRatio) : 0f;
                trailImage.color = trailColor;
            }
        }

        private UnityEngine.UI.Image CreateLayer(string layerName, Sprite sprite, bool filledVertical)
        {
            var go = new GameObject(layerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(rectTransform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.preserveAspect = false;
            if (filledVertical)
            {
                img.type = UnityEngine.UI.Image.Type.Filled;
                img.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
                img.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
                img.fillAmount = 1f;
            }
            return img;
        }

        private void EnsureSpritesLoaded()
        {
            if (spritesLoaded) return;
            spritesLoaded = true;

            backgroundSprite = ResolveSprite(backgroundSprite, ResourceFolder + "StaminaArc_Background");
            fillSprite = ResolveSprite(fillSprite, ResourceFolder + "StaminaArc_Fill");
            trailSprite = ResolveSprite(trailSprite, ResourceFolder + "StaminaArc_Trail");
            ticksFrameSprite = ResolveSprite(ticksFrameSprite, ResourceFolder + "StaminaArc_TicksFrame");
        }

        private Sprite ResolveSprite(Sprite existing, string resourcePath)
        {
            if (existing != null) return existing;
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null) return sprite;

            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) return null;

            Sprite created = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            created.name = tex.name + "_RuntimeSprite";
            runtimeCreatedSprites.Add(created);
            return created;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < runtimeCreatedSprites.Count; i++)
            {
                Sprite sprite = runtimeCreatedSprites[i];
                if (sprite == null) continue;
                if (Application.isPlaying) Destroy(sprite);
                else DestroyImmediate(sprite);
            }
            runtimeCreatedSprites.Clear();
        }
    }
}
