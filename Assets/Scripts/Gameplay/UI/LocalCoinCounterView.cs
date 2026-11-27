using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    public sealed class LocalCoinCounterView : MonoBehaviour
    {
        private const int CachedDigitCount = 100;
        private static readonly string[] TwoDigitStrings = BuildTwoDigitStrings();

        private enum AnimationMode
        {
            None,
            GainPulse,
            LossFlash
        }

        [Header("Sprites")]
        [SerializeField] private Sprite coinSpriteOverride;

        [Header("Layout (1920x1080 Reference Canvas)")]
        [SerializeField] private Vector2 topOffset = new Vector2(0f, -18f);
        [SerializeField] private Vector2 panelSize = new Vector2(168f, 66f);
        [SerializeField, Min(16f)] private float coinIconSize = 44f;
        [SerializeField, Min(4f)] private float horizontalPadding = 18f;
        [SerializeField, Min(0f)] private float iconTextSpacing = 10f;

        [Header("Colors")]
        [SerializeField] private Color panelBackgroundColor = new Color(0.075f, 0.098f, 0.137f, 0.84f);
        [SerializeField] private Color normalTextColor = new Color(1f, 0.969f, 0.878f, 1f);
        [SerializeField] private Color gainHighlightTextColor = new Color(1f, 0.863f, 0.302f, 1f);
        [SerializeField] private Color lossFlashTextColor = new Color(1f, 0.314f, 0.275f, 1f);
        [SerializeField] private Color lossFlashPanelColor = new Color(0.451f, 0.094f, 0.106f, 0.90f);

        [Header("Animation")]
        [SerializeField, Min(0.05f)] private float gainPulseDuration = 0.28f;
        [SerializeField, Range(1.02f, 1.50f)] private float gainPulsePeakScale = 1.18f;
        [SerializeField, Min(0.05f)] private float lossFlashDuration = 0.32f;
        [SerializeField, Min(0f)] private float lossShakeAmplitude = 5f;

        private HudProceduralIcons icons;
        private RectTransform rootRect;
        private RectTransform capsuleRoot;
        private Image backgroundImage;
        private Image coinIconImage;
        private TMP_Text countText;

        private bool initialized;
        private bool hasAppliedState;
        private AnimationMode activeAnimation;
        private float animationTimer;

        public Vector2 TopOffset => topOffset;
        public int CurrentCoins { get; private set; }
        public int TargetCoins { get; private set; }
        public string DisplayedText { get; private set; } = "00";
        public bool IsAnimating => activeAnimation != AnimationMode.None;

        public void Initialize(TMP_FontAsset font = null)
        {
            if (initialized)
            {
                if (font != null && countText != null)
                {
                    if (countText.font != font)
                    {
                        countText.font = font;
                    }
                    if (font.material != null)
                    {
                        countText.fontSharedMaterial = font.material;
                    }
                }
                RefreshCoinSprite();
                return;
            }

            rootRect = transform as RectTransform;
            if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = topOffset;
            rootRect.sizeDelta = panelSize;

            if (icons == null) icons = new HudProceduralIcons();

            var capsuleObj = new GameObject("CapsuleRoot", typeof(RectTransform));
            capsuleRoot = (RectTransform)capsuleObj.transform;
            capsuleRoot.SetParent(rootRect, false);
            capsuleRoot.anchorMin = Vector2.zero;
            capsuleRoot.anchorMax = Vector2.one;
            capsuleRoot.pivot = new Vector2(0.5f, 0.5f);
            capsuleRoot.offsetMin = Vector2.zero;
            capsuleRoot.offsetMax = Vector2.zero;
            capsuleRoot.anchoredPosition = Vector2.zero;
            capsuleRoot.localScale = Vector3.one;

            var bgObj = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var bgRect = (RectTransform)bgObj.transform;
            bgRect.SetParent(capsuleRoot, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            backgroundImage = bgObj.GetComponent<Image>();
            Sprite panelSprite = icons.GetRoundedPanelSprite();
            backgroundImage.sprite = panelSprite;
            backgroundImage.type = panelSprite != null && panelSprite.border.sqrMagnitude > 0.01f
                ? Image.Type.Sliced
                : Image.Type.Simple;
            backgroundImage.color = panelBackgroundColor;
            backgroundImage.raycastTarget = false;

            var iconObj = new GameObject("CoinIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var iconRect = (RectTransform)iconObj.transform;
            iconRect.SetParent(capsuleRoot, false);
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(coinIconSize, coinIconSize);
            iconRect.anchoredPosition = new Vector2(horizontalPadding + coinIconSize * 0.5f, 0f);
            coinIconImage = iconObj.GetComponent<Image>();
            coinIconImage.color = Color.white;
            coinIconImage.preserveAspect = true;
            coinIconImage.raycastTarget = false;
            RefreshCoinSprite();

            var textObj = new GameObject("CountText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(capsuleRoot, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(horizontalPadding + coinIconSize + iconTextSpacing, 4f);
            textRect.offsetMax = new Vector2(-horizontalPadding, -4f);

            var tmp = textObj.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                tmp.font = font;
                if (font.material != null) tmp.fontSharedMaterial = font.material;
            }
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Normal;
            tmp.fontWeight = FontWeight.Regular;
            tmp.fontSize = 40f;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 22f;
            tmp.fontSizeMax = 42f;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.color = normalTextColor;
            tmp.raycastTarget = false;
            countText = tmp;

            CurrentCoins = 0;
            TargetCoins = 0;
            hasAppliedState = false;
            DisplayedText = FormatCoins(0);
            countText.text = DisplayedText;

            ResetVisualToIdle();
            initialized = true;
        }

        public void SetCoins(int coins, int coinsToWin, bool animate = true)
        {
            if (!initialized) Initialize();

            int clampedCoins = Mathf.Max(0, coins);
            int clampedTarget = Mathf.Max(0, coinsToWin);

            if (hasAppliedState && clampedCoins == CurrentCoins && clampedTarget == TargetCoins)
            {
                if (!animate && activeAnimation != AnimationMode.None)
                {
                    ResetVisualToIdle();
                }
                return;
            }

            int previousCoins = CurrentCoins;
            CurrentCoins = clampedCoins;
            TargetCoins = clampedTarget;
            hasAppliedState = true;

            string formatted = FormatCoins(clampedCoins);
            if (!string.Equals(DisplayedText, formatted, System.StringComparison.Ordinal))
            {
                DisplayedText = formatted;
                if (countText != null)
                {
                    countText.text = formatted;
                }
            }

            if (!animate)
            {
                ResetVisualToIdle();
                return;
            }

            if (clampedCoins > previousCoins)
            {
                StartGainPulse();
            }
            else if (clampedCoins < previousCoins)
            {
                StartLossFlash();
            }
        }

        public void Tick(float deltaTime)
        {
            if (!initialized || activeAnimation == AnimationMode.None || deltaTime <= 0f) return;

            animationTimer += deltaTime;
            if (activeAnimation == AnimationMode.GainPulse)
            {
                float dur = Mathf.Max(0.05f, gainPulseDuration);
                if (animationTimer >= dur)
                {
                    ResetVisualToIdle();
                }
                else
                {
                    UpdateGainPulseVisual(animationTimer / dur);
                }
            }
            else if (activeAnimation == AnimationMode.LossFlash)
            {
                float dur = Mathf.Max(0.05f, lossFlashDuration);
                if (animationTimer >= dur)
                {
                    ResetVisualToIdle();
                }
                else
                {
                    UpdateLossFlashVisual(animationTimer / dur);
                }
            }
        }

        private void OnDestroy()
        {
            if (icons != null)
            {
                icons.Dispose();
                icons = null;
            }
        }

        private void RefreshCoinSprite()
        {
            if (coinIconImage == null) return;
            coinIconImage.sprite = coinSpriteOverride != null
                ? coinSpriteOverride
                : (icons != null ? icons.GetCoinSprite() : null);
        }

        private void StartGainPulse()
        {
            activeAnimation = AnimationMode.GainPulse;
            animationTimer = 0f;
            UpdateGainPulseVisual(0f);
        }

        private void StartLossFlash()
        {
            activeAnimation = AnimationMode.LossFlash;
            animationTimer = 0f;
            UpdateLossFlashVisual(0f);
        }

        private void UpdateGainPulseVisual(float progress)
        {
            float p = Mathf.Clamp01(progress);
            float peak = Mathf.Max(1.02f, gainPulsePeakScale);
            float scale;
            if (p < 0.32f)
            {
                float u = p / 0.32f;
                float easeOut = 1f - (1f - u) * (1f - u);
                scale = Mathf.Lerp(1.04f, peak, easeOut);
            }
            else if (p < 0.68f)
            {
                float u = (p - 0.32f) / 0.36f;
                float smooth = u * u * (3f - 2f * u);
                scale = Mathf.Lerp(peak, 0.96f, smooth);
            }
            else
            {
                float u = (p - 0.68f) / 0.32f;
                float smooth = u * u * (3f - 2f * u);
                scale = Mathf.Lerp(0.96f, 1f, smooth);
            }

            if (capsuleRoot != null)
            {
                capsuleRoot.anchoredPosition = Vector2.zero;
                capsuleRoot.localScale = new Vector3(scale, scale, 1f);
            }
            if (coinIconImage != null)
            {
                float iconScale = 1f + (scale - 1f) * 0.35f;
                coinIconImage.rectTransform.localScale = new Vector3(iconScale, iconScale, 1f);
            }
            if (countText != null)
            {
                float highlight = Mathf.Sin(p * Mathf.PI);
                countText.color = Color.Lerp(normalTextColor, gainHighlightTextColor, highlight);
            }
            if (backgroundImage != null)
            {
                backgroundImage.color = panelBackgroundColor;
            }
        }

        private void UpdateLossFlashVisual(float progress)
        {
            float p = Mathf.Clamp01(progress);
            float decay = 1f - p;
            float pulse = 0.55f + 0.45f * Mathf.Cos(p * Mathf.PI * 3f);
            float flash = Mathf.Clamp01(decay * pulse);

            if (capsuleRoot != null)
            {
                float shakeX = Mathf.Sin(p * Mathf.PI * 6f) * lossShakeAmplitude * decay;
                float scale = 1f - 0.04f * Mathf.Sin(p * Mathf.PI);
                capsuleRoot.anchoredPosition = new Vector2(shakeX, 0f);
                capsuleRoot.localScale = new Vector3(scale, scale, 1f);
            }
            if (coinIconImage != null)
            {
                coinIconImage.rectTransform.localScale = Vector3.one;
            }
            if (countText != null)
            {
                countText.color = Color.Lerp(normalTextColor, lossFlashTextColor, flash);
            }
            if (backgroundImage != null)
            {
                backgroundImage.color = Color.Lerp(panelBackgroundColor, lossFlashPanelColor, flash * 0.85f);
            }
        }

        private void ResetVisualToIdle()
        {
            activeAnimation = AnimationMode.None;
            animationTimer = 0f;

            if (capsuleRoot != null)
            {
                capsuleRoot.anchoredPosition = Vector2.zero;
                capsuleRoot.localScale = Vector3.one;
            }
            if (coinIconImage != null)
            {
                coinIconImage.rectTransform.localScale = Vector3.one;
            }
            if (countText != null)
            {
                countText.color = normalTextColor;
            }
            if (backgroundImage != null)
            {
                backgroundImage.color = panelBackgroundColor;
            }
        }

        private static string FormatCoins(int value)
        {
            int nonNegative = value < 0 ? 0 : value;
            if (nonNegative < CachedDigitCount)
            {
                return TwoDigitStrings[nonNegative];
            }
            return nonNegative.ToString("D2", CultureInfo.InvariantCulture);
        }

        private static string[] BuildTwoDigitStrings()
        {
            var cache = new string[CachedDigitCount];
            for (int i = 0; i < CachedDigitCount; i++)
            {
                cache[i] = i.ToString("D2", CultureInfo.InvariantCulture);
            }
            return cache;
        }
    }
}
