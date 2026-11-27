using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    public sealed class MatchClockAndMineView : MonoBehaviour
    {
        private const int MaxCachedWholeSeconds = 300;
        private const int MaxCachedWarningTenths = 60;
        private const string ExhaustedMineText = "已全部产出";
        private const string PendingMineText = "待定";
        private const string EmptyCountdownText = "--";
        private const string TimeCaptionLabel = "TIME";
        private const string MineCaptionLabel = "刷新点位";
        private const string CountdownCaptionLabel = "即将刷新";

        private static readonly string[] FallbackMineNames = { "南侧", "北侧", "西侧", "东侧" };
        private static readonly string[] CachedWholeSecondTexts = BuildWholeSecondTexts(MaxCachedWholeSeconds);
        private static readonly string[] CachedTenthsSecondTexts = BuildTenthsSecondTexts(MaxCachedWarningTenths);

        private enum ScheduleMode
        {
            None = -1,
            NoUpcoming = 0,
            Pending = 1,
            WarningUnknownMine = 2,
            WarningKnownMine = 3
        }

        [Header("Layout")]
        [SerializeField] private Vector2 topRightOffset = new Vector2(-20f, -20f);
        [SerializeField] private Vector2 panelSize = new Vector2(272f, 124f);

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new Color(0.082f, 0.102f, 0.141f, 0.86f);
        [SerializeField] private Color dividerColor = new Color(1f, 1f, 1f, 0.16f);
        [SerializeField] private Color captionColor = new Color(0.68f, 0.74f, 0.82f, 0.88f);
        [SerializeField] private Color timeNormalColor = new Color(0.96f, 0.98f, 1f, 1f);
        [SerializeField] private Color timeDangerColor = new Color(1f, 0.29f, 0.29f, 1f);
        [SerializeField] private Color timeDangerBrightColor = new Color(1f, 0.56f, 0.50f, 1f);
        [SerializeField] private Color mineExhaustedColor = new Color(0.54f, 0.58f, 0.64f, 0.86f);
        [SerializeField] private Color minePendingColor = new Color(0.76f, 0.80f, 0.86f, 0.95f);
        [SerializeField] private Color mineWarningColor = new Color(1f, 0.89f, 0.38f, 1f);
        [SerializeField] private Color mineWarningBrightColor = new Color(1f, 0.97f, 0.76f, 1f);
        [SerializeField] private Color countdownExhaustedColor = new Color(0.54f, 0.58f, 0.64f, 0.86f);
        [SerializeField] private Color countdownNormalColor = new Color(0.88f, 0.92f, 0.97f, 0.96f);
        [SerializeField] private Color countdownWarningColor = new Color(1f, 0.83f, 0.24f, 1f);
        [SerializeField] private Color countdownWarningBrightColor = new Color(1f, 0.95f, 0.64f, 1f);
        [SerializeField] private Color connectedDotColor = new Color(0.24f, 0.92f, 0.46f, 1f);
        [SerializeField] private Color disconnectedDotColor = new Color(1f, 0.42f, 0.24f, 1f);

        [Header("Pulse Animation")]
        [SerializeField, Min(1)] private int dangerTimeThreshold = 10;
        [SerializeField, Min(0.5f)] private float timePulseSpeed = 6.283185f;
        [SerializeField, Range(0f, 0.25f)] private float timePulseScaleAmplitude = 0.08f;
        [SerializeField, Min(0.5f)] private float warningPulseSpeed = 6.5f;
        [SerializeField, Range(0f, 0.20f)] private float warningPulseScaleAmplitude = 0.06f;

        private HudProceduralIcons icons;
        [Header("Prefab References (optional)")]
        [SerializeField] private Image connectionDotImage;
        [SerializeField] private TMP_Text timeCaptionText;
        [SerializeField] private TMP_Text timeValueText;
        [SerializeField] private TMP_Text mineCaptionText;
        [SerializeField] private TMP_Text mineValueText;
        [SerializeField] private TMP_Text countdownCaptionText;
        [SerializeField] private TMP_Text countdownValueText;

        private bool initialized;
        private bool hasTimeValue;
        private bool isTimeDanger;
        private float timePulsePhase;
        private float warningPulsePhase;
        private ScheduleMode lastScheduleMode = ScheduleMode.None;
        private int lastCountdownKey = int.MinValue;
        private bool lastConnectedState;

        public Vector2 TopRightOffset => topRightOffset;
        public Vector2 PanelSize => panelSize;
        public int LastRemainingSeconds { get; private set; } = -1;
        public string DisplayedTimeText { get; private set; } = string.Empty;
        public string DisplayedMineText { get; private set; } = string.Empty;
        public string DisplayedCountdownText { get; private set; } = string.Empty;
        public bool IsWarningHighlighted { get; private set; }
        public bool IsMineConnectedIndicator { get; private set; }

        public void Initialize(TMP_FontAsset font = null)
        {
            if (initialized)
            {
                if (font != null) ApplyFont(font);
                return;
            }

            if (connectionDotImage != null && timeCaptionText != null && timeValueText != null &&
                mineCaptionText != null && mineValueText != null && countdownCaptionText != null && countdownValueText != null)
            {
                InitializeDisplayState();
                return;
            }

            var rectTransform = transform as RectTransform;
            if (rectTransform == null) rectTransform = gameObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = topRightOffset;
            rectTransform.sizeDelta = panelSize;

            icons = new HudProceduralIcons();
            Sprite panelSprite = icons.GetRoundedPanelSprite();
            Sprite dotSprite = icons.GetStatusDotSprite();
            Sprite coinSprite = icons.GetCoinSprite();

            var backgroundImage = CreatePanelImage(rectTransform, "Background", panelSprite, backgroundColor);
            SetStretch(backgroundImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var upperRect = CreateContainer(rectTransform, "UpperSection");
            SetStretch(upperRect, new Vector2(0f, 0.48f), Vector2.one, new Vector2(12f, 2f), new Vector2(-12f, -6f));

            timeCaptionText = CreateText(upperRect, "TimeCaption", font, 11.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, captionColor, false, 11.5f, 11.5f);
            timeCaptionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            timeCaptionText.rectTransform.anchorMax = new Vector2(0f, 1f);
            timeCaptionText.rectTransform.pivot = new Vector2(0f, 1f);
            timeCaptionText.rectTransform.anchoredPosition = new Vector2(2f, -2f);
            timeCaptionText.rectTransform.sizeDelta = new Vector2(56f, 18f);
            timeCaptionText.characterSpacing = 1.5f;
            timeCaptionText.text = TimeCaptionLabel;

            timeValueText = CreateText(upperRect, "TimeValue", font, 38f, FontStyles.Normal, TextAlignmentOptions.Center, timeNormalColor, true, 22f, 40f);
            SetStretch(timeValueText.rectTransform, Vector2.zero, Vector2.one, new Vector2(44f, 0f), new Vector2(-44f, 0f));
            timeValueText.text = EmptyCountdownText;

            var horizontalDividerImage = CreateSolidImage(rectTransform, "HorizontalDivider", dividerColor);
            SetStretch(horizontalDividerImage.rectTransform, new Vector2(0f, 0.48f), new Vector2(1f, 0.48f), new Vector2(12f, -0.75f), new Vector2(-12f, 0.75f));

            var lowerRect = CreateContainer(rectTransform, "LowerSection");
            SetStretch(lowerRect, Vector2.zero, new Vector2(1f, 0.48f), new Vector2(8f, 6f), new Vector2(-8f, -4f));

            var verticalDividerImage = CreateSolidImage(lowerRect, "VerticalDivider", dividerColor);
            SetStretch(verticalDividerImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-0.75f, 4f), new Vector2(0.75f, -4f));

            var mineCellRect = CreateContainer(lowerRect, "MineCell");
            SetStretch(mineCellRect, Vector2.zero, new Vector2(0.5f, 1f), new Vector2(2f, 0f), new Vector2(-4f, 0f));

            mineCaptionText = CreateText(mineCellRect, "MineCaption", font, 11.5f, FontStyles.Normal, TextAlignmentOptions.Center, captionColor, false, 11.5f, 11.5f);
            SetStretch(mineCaptionText.rectTransform, new Vector2(0f, 0.58f), Vector2.one, Vector2.zero, Vector2.zero);
            mineCaptionText.text = MineCaptionLabel;

            var mineValueRow = CreateContainer(mineCellRect, "MineValueRow");
            SetStretch(mineValueRow, Vector2.zero, new Vector2(1f, 0.60f), Vector2.zero, Vector2.zero);

            connectionDotImage = CreateIconImage(mineValueRow, "ConnectionDot", dotSprite, new Vector2(10f, 10f), connectedDotColor);
            connectionDotImage.enabled = false;

            mineValueText = CreateText(mineValueRow, "MineValue", font, 19f, FontStyles.Normal, TextAlignmentOptions.Center, minePendingColor, true, 12f, 20f);
            SetStretch(mineValueText.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));
            mineValueText.text = PendingMineText;

            var countdownCellRect = CreateContainer(lowerRect, "CountdownCell");
            SetStretch(countdownCellRect, new Vector2(0.5f, 0f), Vector2.one, new Vector2(4f, 0f), new Vector2(-2f, 0f));

            var countdownHeaderRect = CreateContainer(countdownCellRect, "CountdownHeader");
            SetStretch(countdownHeaderRect, new Vector2(0f, 0.58f), Vector2.one, Vector2.zero, Vector2.zero);

            var coinIconImage = CreateIconImage(countdownHeaderRect, "CoinIcon", coinSprite, new Vector2(12f, 12f), Color.white);
            coinIconImage.rectTransform.anchoredPosition = new Vector2(-26f, 0f);
            coinIconImage.enabled = coinSprite != null;

            countdownCaptionText = CreateText(countdownHeaderRect, "CountdownCaption", font, 11.5f, FontStyles.Normal, TextAlignmentOptions.Center, captionColor, false, 11.5f, 11.5f);
            SetStretch(countdownCaptionText.rectTransform, Vector2.zero, Vector2.one, new Vector2(coinSprite != null ? 14f : 0f, 0f), Vector2.zero);
            countdownCaptionText.text = CountdownCaptionLabel;

            countdownValueText = CreateText(countdownCellRect, "CountdownValue", font, 20f, FontStyles.Normal, TextAlignmentOptions.Center, countdownNormalColor, true, 13f, 21f);
            SetStretch(countdownValueText.rectTransform, Vector2.zero, new Vector2(1f, 0.60f), new Vector2(4f, 0f), new Vector2(-4f, 0f));
            countdownValueText.text = EmptyCountdownText;

            InitializeDisplayState();
        }

        private void InitializeDisplayState()
        {
            DisplayedTimeText = EmptyCountdownText;
            DisplayedMineText = PendingMineText;
            DisplayedCountdownText = EmptyCountdownText;
            IsWarningHighlighted = false;
            IsMineConnectedIndicator = false;
            initialized = true;
        }

        public void SetTime(int remainingSeconds)
        {
            if (!initialized) Initialize();

            int clamped = remainingSeconds < 0 ? 0 : remainingSeconds;
            if (hasTimeValue && clamped == LastRemainingSeconds) return;

            hasTimeValue = true;
            LastRemainingSeconds = clamped;
            DisplayedTimeText = FormatWholeSeconds(clamped);
            if (timeValueText != null) timeValueText.text = DisplayedTimeText;

            bool wasDanger = isTimeDanger;
            bool danger = clamped > 0 && clamped <= dangerTimeThreshold;
            isTimeDanger = danger;

            if (danger)
            {
                if (!wasDanger) timePulsePhase = 0f;
                ApplyTimeDangerPulseVisuals();
            }
            else if (timeValueText != null)
            {
                timePulsePhase = 0f;
                timeValueText.color = timeNormalColor;
                timeValueText.rectTransform.localScale = Vector3.one;
            }
        }

        public void SetMineSchedule(bool hasUpcoming, int mineIndex, string mineName, float secondsUntilSpawn, bool isWarning, bool isConnected)
        {
            if (!initialized) Initialize();

            float clampedSeconds = float.IsNaN(secondsUntilSpawn) || float.IsInfinity(secondsUntilSpawn) || secondsUntilSpawn < 0f
                ? 0f
                : secondsUntilSpawn;

            ScheduleMode mode;
            string nextMineText;
            int countdownKey;
            bool nextWarningHighlight;
            bool nextConnectedIndicator;

            if (!hasUpcoming)
            {
                mode = ScheduleMode.NoUpcoming;
                nextMineText = ExhaustedMineText;
                countdownKey = -1;
                nextWarningHighlight = false;
                nextConnectedIndicator = false;
            }
            else if (!isWarning)
            {
                mode = ScheduleMode.Pending;
                nextMineText = PendingMineText;
                countdownKey = Mathf.CeilToInt(clampedSeconds);
                nextWarningHighlight = false;
                nextConnectedIndicator = false;
            }
            else if (mineIndex < 0)
            {
                mode = ScheduleMode.WarningUnknownMine;
                nextMineText = PendingMineText;
                countdownKey = (int)Math.Round(clampedSeconds * 10d, MidpointRounding.AwayFromZero);
                nextWarningHighlight = true;
                nextConnectedIndicator = false;
            }
            else
            {
                mode = ScheduleMode.WarningKnownMine;
                nextMineText = ResolveMineName(mineIndex, mineName);
                countdownKey = (int)Math.Round(clampedSeconds * 10d, MidpointRounding.AwayFromZero);
                nextWarningHighlight = true;
                nextConnectedIndicator = isConnected;
            }

            if (mode == lastScheduleMode &&
                countdownKey == lastCountdownKey &&
                nextConnectedIndicator == lastConnectedState &&
                string.Equals(nextMineText, DisplayedMineText, StringComparison.Ordinal))
            {
                return;
            }

            bool modeChanged = mode != lastScheduleMode;
            bool enteredWarning = nextWarningHighlight && !IsWarningHighlighted;
            bool mineTextChanged = !string.Equals(nextMineText, DisplayedMineText, StringComparison.Ordinal);
            bool countdownChanged = modeChanged || countdownKey != lastCountdownKey;

            lastScheduleMode = mode;
            lastCountdownKey = countdownKey;
            lastConnectedState = nextConnectedIndicator;
            IsWarningHighlighted = nextWarningHighlight;
            IsMineConnectedIndicator = nextConnectedIndicator;

            if (mineTextChanged || modeChanged)
            {
                DisplayedMineText = nextMineText;
                if (mineValueText != null) mineValueText.text = nextMineText;
            }

            if (countdownChanged)
            {
                string nextCountdownText;
                if (mode == ScheduleMode.NoUpcoming)
                {
                    nextCountdownText = EmptyCountdownText;
                }
                else if (mode == ScheduleMode.Pending)
                {
                    nextCountdownText = FormatWholeSeconds(countdownKey);
                }
                else
                {
                    nextCountdownText = FormatTenthsSeconds(countdownKey);
                }

                DisplayedCountdownText = nextCountdownText;
                if (countdownValueText != null) countdownValueText.text = nextCountdownText;
            }

            if (enteredWarning) warningPulsePhase = 0f;

            ApplyScheduleStaticVisuals(mode, nextMineText, nextConnectedIndicator);
        }

        public void Tick(float deltaTime)
        {
            if (!initialized || deltaTime <= 0f) return;

            if (isTimeDanger)
            {
                timePulsePhase = Mathf.Repeat(timePulsePhase + deltaTime * timePulseSpeed, Mathf.PI * 2f);
                ApplyTimeDangerPulseVisuals();
            }

            if (IsWarningHighlighted)
            {
                warningPulsePhase = Mathf.Repeat(warningPulsePhase + deltaTime * warningPulseSpeed, Mathf.PI * 2f);
                ApplyWarningPulseVisuals();
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

        private void ApplyTimeDangerPulseVisuals()
        {
            if (timeValueText == null) return;
            float pulse = 0.5f - 0.5f * Mathf.Cos(timePulsePhase);
            float scale = 1f + timePulseScaleAmplitude * pulse;
            timeValueText.rectTransform.localScale = new Vector3(scale, scale, 1f);
            timeValueText.color = Color.Lerp(timeDangerColor, timeDangerBrightColor, pulse);
        }

        private void ApplyWarningPulseVisuals()
        {
            float breath = 0.5f - 0.5f * Mathf.Cos(warningPulsePhase);
            if (countdownValueText != null)
            {
                float cScale = 1f + warningPulseScaleAmplitude * breath;
                countdownValueText.rectTransform.localScale = new Vector3(cScale, cScale, 1f);
                countdownValueText.color = Color.Lerp(countdownWarningColor, countdownWarningBrightColor, breath);
            }

            if (lastScheduleMode == ScheduleMode.WarningKnownMine)
            {
                if (mineValueText != null)
                {
                    mineValueText.color = Color.Lerp(mineWarningColor, mineWarningBrightColor, breath);
                }
                if (connectionDotImage != null && connectionDotImage.enabled)
                {
                    float dotScale = 1f + 0.14f * breath;
                    connectionDotImage.rectTransform.localScale = new Vector3(dotScale, dotScale, 1f);
                }
            }
        }

        private void ApplyScheduleStaticVisuals(ScheduleMode mode, string mineText, bool connected)
        {
            bool showDot = mode == ScheduleMode.WarningKnownMine;

            if (connectionDotImage != null)
            {
                connectionDotImage.enabled = showDot;
                if (showDot)
                {
                    connectionDotImage.color = connected ? connectedDotColor : disconnectedDotColor;
                    float approxHalfWidth = Mathf.Clamp((mineText != null ? mineText.Length : 2) * 9.5f, 14f, 36f);
                    float dotX = Mathf.Max(-52f, 6f - approxHalfWidth - 10f);
                    connectionDotImage.rectTransform.anchoredPosition = new Vector2(dotX, 0f);
                }
                else
                {
                    connectionDotImage.rectTransform.localScale = Vector3.one;
                }
            }

            if (mineValueText != null)
            {
                mineValueText.rectTransform.offsetMin = new Vector2(showDot ? 16f : 4f, 0f);
                mineValueText.rectTransform.offsetMax = new Vector2(-4f, 0f);
                mineValueText.rectTransform.localScale = Vector3.one;

                if (mode == ScheduleMode.NoUpcoming) mineValueText.color = mineExhaustedColor;
                else if (mode == ScheduleMode.WarningKnownMine) mineValueText.color = mineWarningColor;
                else mineValueText.color = minePendingColor;
            }

            if (IsWarningHighlighted)
            {
                ApplyWarningPulseVisuals();
                return;
            }

            if (countdownValueText != null)
            {
                countdownValueText.color = mode == ScheduleMode.NoUpcoming ? countdownExhaustedColor : countdownNormalColor;
                countdownValueText.rectTransform.localScale = Vector3.one;
            }
        }

        private void ApplyFont(TMP_FontAsset font)
        {
            if (font == null) return;
            ApplyFontToText(timeCaptionText, font);
            ApplyFontToText(timeValueText, font);
            ApplyFontToText(mineCaptionText, font);
            ApplyFontToText(mineValueText, font);
            ApplyFontToText(countdownCaptionText, font);
            ApplyFontToText(countdownValueText, font);
        }

        private static void ApplyFontToText(TMP_Text text, TMP_FontAsset font)
        {
            if (text == null || font == null) return;
            text.font = font;
            if (font.material != null) text.fontSharedMaterial = font.material;
            text.fontStyle = FontStyles.Normal;
            text.fontWeight = FontWeight.Regular;
        }

        private static string ResolveMineName(int mineIndex, string mineName)
        {
            if (!string.IsNullOrEmpty(mineName)) return mineName;
            if (mineIndex >= 0 && mineIndex < FallbackMineNames.Length) return FallbackMineNames[mineIndex];
            return "矿点";
        }

        private static string FormatWholeSeconds(int seconds)
        {
            if (seconds < 0) seconds = 0;
            if (seconds < CachedWholeSecondTexts.Length) return CachedWholeSecondTexts[seconds];
            return seconds.ToString(CultureInfo.InvariantCulture) + "s";
        }

        private static string FormatTenthsSeconds(int tenths)
        {
            if (tenths < 0) tenths = 0;
            if (tenths < CachedTenthsSecondTexts.Length) return CachedTenthsSecondTexts[tenths];
            int whole = tenths / 10;
            int frac = tenths % 10;
            return whole.ToString(CultureInfo.InvariantCulture) + "." + frac.ToString(CultureInfo.InvariantCulture) + "s";
        }

        private static string[] BuildWholeSecondTexts(int maxSeconds)
        {
            var values = new string[maxSeconds + 1];
            for (int i = 0; i <= maxSeconds; i++)
            {
                values[i] = i.ToString(CultureInfo.InvariantCulture) + "s";
            }
            return values;
        }

        private static string[] BuildTenthsSecondTexts(int maxTenths)
        {
            var values = new string[maxTenths + 1];
            for (int i = 0; i <= maxTenths; i++)
            {
                int whole = i / 10;
                int frac = i % 10;
                values[i] = whole.ToString(CultureInfo.InvariantCulture) + "." + frac.ToString(CultureInfo.InvariantCulture) + "s";
            }
            return values;
        }

        private static RectTransform CreateContainer(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static Image CreatePanelImage(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Image CreateSolidImage(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Image CreateIconImage(RectTransform parent, string name, Sprite sprite, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text CreateText(
            RectTransform parent,
            string name,
            TMP_FontAsset font,
            float fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment,
            Color color,
            bool autoSize,
            float minSize,
            float maxSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
                if (font.material != null) text.fontSharedMaterial = font.material;
            }
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.fontWeight = FontWeight.Regular;
            text.alignment = alignment;
            text.color = color;
            text.enableAutoSizing = autoSize;
            if (autoSize)
            {
                text.fontSizeMin = minSize;
                text.fontSizeMax = maxSize;
            }
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
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
