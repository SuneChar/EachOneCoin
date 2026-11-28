using System;
using System.Collections.Generic;
using System.Globalization;
using EachOneCoin;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    public enum ParticipantHudLayoutMode
    {
        Horizontal = 0,
        VerticalRightAligned = 1
    }

    public sealed class ParticipantHudStripView : MonoBehaviour
    {
        private const int DefaultCapacity = 4;
        private const int CachedCoinStringCount = 100;
        private const int CachedRankStringCount = 16;
        private const float ReferenceStripHeight = 154f;
        private const float ReferenceHealthBarHeight = 12f;
        private const float HealthBarFrameThickness = 2f;
        private const float HealthBarSegmentGap = 2f;
        private const float HealthBarOuterRimOffsetRatio = 0.004f;

        private static readonly string[] CachedTwoDigitStrings = BuildTwoDigitStrings();
        private static readonly string[] CachedRankStrings = BuildRankStrings();

        [Header("Prefab Template (optional)")]
        [SerializeField] private ParticipantHudSlotView slotTemplate;

        [Header("Layout (1920x1080 Reference Canvas)")]
        [SerializeField] private Vector2 bottomRightOffset = new Vector2(-12f, 388f);
        [SerializeField, Min(120f)] private float stripWidth = 368f;
        [SerializeField, Min(80f)] private float stripHeight = 154f;
        [SerializeField, Min(0f)] private float horizontalPadding = 4f;
        [SerializeField, Min(0f)] private float slotSpacing = 6f;

        [Header("Vertical Compact Layout")]
        [SerializeField, Min(120f)] private float verticalStripWidth = 180f;
        [SerializeField, Min(48f)] private float verticalRowHeight = 70f;
        [SerializeField, Min(32f)] private float verticalAvatarSize = 60f;
        [SerializeField, Min(6f)] private float verticalHealthBarHeight = 12f;
        [SerializeField, Min(0f)] private float verticalSlotSpacing = 6f;
        [SerializeField, Min(0f)] private float verticalPadding = 4f;
        [SerializeField, Min(48f)] private float verticalMetricWidth = 68f;
        [SerializeField, Min(0f)] private float verticalAvatarMetricGap = 8f;
        [SerializeField, Min(0f)] private float verticalMetricCrownGap = 6f;
        [SerializeField] private Vector2 verticalCrownSize = new Vector2(32f, 25f);

        [Header("Colors")]
        [SerializeField] private Color avatarBackdropColor = new Color(0.118f, 0.149f, 0.204f, 0.95f);
        [SerializeField] private Color localFrameHighlightColor = new Color(1f, 0.98f, 0.88f, 1f);
        [SerializeField] private Color coinTextColor = new Color(1f, 0.969f, 0.878f, 1f);
        [SerializeField] private Color lostCoinTextColor = new Color(0.58f, 0.60f, 0.64f, 0.85f);
        [SerializeField] private Color medalRankTextColor = new Color(0.14f, 0.09f, 0.03f, 1f);
        [SerializeField] private Color wonFrameAccentColor = new Color(1f, 0.878f, 0.341f, 1f);
        [SerializeField] private Color respawningOverlayColor = new Color(0.05f, 0.07f, 0.11f, 0.42f);
        [SerializeField] private Color lostOverlayColor = new Color(0.08f, 0.08f, 0.10f, 0.48f);

        [Header("Health Bar")]
        [SerializeField] private Color healthBarFrameColor = new Color(0.075f, 0.090f, 0.122f, 1f);
        [SerializeField] private Color healthFullColor = new Color(0.20f, 0.82f, 0.35f, 1f);
        [SerializeField] private Color healthDimColor = new Color(0.56f, 0.66f, 0.59f, 1f);
        [SerializeField] private Color healthBrokenColor = new Color(0.88f, 0.24f, 0.24f, 1f);

        [Header("Status & Animation")]
        [SerializeField, Range(0.2f, 1f)] private float respawningSlotAlpha = 0.64f;
        [SerializeField, Range(0.2f, 1f)] private float lostSlotAlpha = 0.72f;
        [SerializeField, Min(0.5f)] private float crownBobSpeed = 3.8f;
        [SerializeField, Min(0f)] private float crownBobAmplitude = 1.8f;
        [SerializeField, Range(0f, 0.12f)] private float crownPulseScale = 0.035f;

        private sealed class ParticipantSlot
        {
            public RectTransform Root;
            public CanvasGroup CanvasGroup;

            public RectTransform CrownRect;
            public Image CrownImage;
            public Vector2 CrownBasePosition;

            public RectTransform TopMetricRect;
            public RectTransform CoinGroup;
            public Image CoinIconImage;
            public TMP_Text CoinText;

            public RectTransform MedalGroup;
            public Image MedalIconImage;
            public TMP_Text MedalRankText;

            public RectTransform AvatarRoot;
            public Image MaskImage;
            public Image BasePortraitImage;
            public RectTransform OverlayContainer;
            public readonly List<Image> OverlayImages = new List<Image>();
            public Image StatusOverlayImage;
            public Image FrameImage;

            public RectTransform HealthBarRect;
            public readonly List<Image> HealthSegmentImages = new List<Image>();
            public readonly List<HeartVisualState> HealthSegmentStates = new List<HeartVisualState>();

            public Vector2 HorizontalCrownBasePosition;
            public Vector2 HorizontalCrownSize;
            public Vector2 HorizontalMetricAnchoredPos;
            public Vector2 HorizontalMetricSizeDelta;
            public Vector2 HorizontalAvatarAnchoredPos;

            public bool HasAppliedState;
            public bool IsVisible;
            public bool IsLocal;
            public bool IsCrownVisible;
            public bool IsMedalVisible;
            public int LastCoins = int.MinValue;
            public int LastFinishOrder = int.MinValue;
            public int LastHealth = int.MinValue;
            public int LastMaxHealth = int.MinValue;
            public int LastInitialMaxHealth = int.MinValue;
            public ParticipantStatus LastStatus;
            public Color LastParticipantColor;
            public Sprite LastBasePortrait;
            public Color LastTintColor;
            public Sprite LastFrameSprite;
            public AvatarAppearanceData LastAppearance;
            public bool HasAppliedAppearance;
            public string CosmeticId;
            public int ActiveOverlayCount;
            public int ActiveHeartCount;
            public string DisplayedCoinText = string.Empty;
            public string DisplayedMedalRankText = string.Empty;
        }

        private RectTransform rootRect;
        private Canvas cachedRootCanvas;
        private float lastCanvasScaleFactor = -1f;
        private Vector2 lastCanvasPixelSize = new Vector2(-1f, -1f);
        private HudProceduralIcons icons;
        private TMP_FontAsset cachedFont;
        private ParticipantSlot[] slots;
        private bool[] slotPopulated;
        private float cachedSlotWidth;
        private float cachedAvatarSize;
        private bool initialized;
        private bool hasRefreshed;
        private int lastVisibleMask = -1;
        private float crownAnimTime;

        public Vector2 BottomRightOffset => bottomRightOffset;
        public float StripWidth => stripWidth;
        public float StripHeight => stripHeight;
        public float VerticalStripWidth => verticalStripWidth;
        public float CurrentStripWidth => LayoutMode == ParticipantHudLayoutMode.VerticalRightAligned ? verticalStripWidth : stripWidth;
        public float CurrentStripHeight => LayoutMode == ParticipantHudLayoutMode.VerticalRightAligned ? ComputeVerticalStripHeight() : stripHeight;
        public ParticipantHudLayoutMode LayoutMode { get; private set; } = ParticipantHudLayoutMode.Horizontal;
        public int Capacity => slots != null ? slots.Length : 0;
        public byte LastLeaderMask { get; private set; }

        public void SetLayoutMode(ParticipantHudLayoutMode mode)
        {
            if (LayoutMode == mode)
            {
                return;
            }

            LayoutMode = mode;
            if (!initialized)
            {
                return;
            }

            ConfigureRootRect();
            LayoutSlots();
        }

        public void SetLayoutMode(ParticipantHudLayoutMode mode, float horizontalWidth, Vector2 anchorOffset)
        {
            bool modeChanged = LayoutMode != mode;
            bool widthChanged = false;
            if (mode == ParticipantHudLayoutMode.Horizontal && horizontalWidth > 0f)
            {
                float clampedWidth = Mathf.Max(64f, horizontalWidth);
                widthChanged = Mathf.Abs(clampedWidth - stripWidth) > 0.01f;
                stripWidth = clampedWidth;
            }

            bool offsetChanged = (anchorOffset - bottomRightOffset).sqrMagnitude > 0.0001f;
            bottomRightOffset = anchorOffset;
            LayoutMode = mode;

            if (!initialized)
            {
                return;
            }

            if (modeChanged || widthChanged || offsetChanged)
            {
                ConfigureRootRect();
                LayoutSlots();
            }
        }

        public void Initialize(int capacity, float width, Vector2 bottomRightOffset, TMP_FontAsset font = null)
        {
            int targetCapacity = Mathf.Max(1, capacity);
            if (width > 0f)
            {
                stripWidth = Mathf.Max(64f, width);
            }
            this.bottomRightOffset = bottomRightOffset;
            if (font != null)
            {
                cachedFont = font;
            }

            if (icons == null)
            {
                icons = new HudProceduralIcons();
            }

            ConfigureRootRect();

            if (initialized && slots != null && slots.Length == targetCapacity)
            {
                LayoutSlots();
                if (font != null)
                {
                    ApplyFontToSlots(font);
                }
                return;
            }

            ClearBuiltSlots();

            slots = new ParticipantSlot[targetCapacity];
            slotPopulated = new bool[targetCapacity];
            cachedSlotWidth = ComputeSlotWidth(targetCapacity);
            cachedAvatarSize = ComputeAvatarSize(cachedSlotWidth);

            for (int i = 0; i < targetCapacity; i++)
            {
                slots[i] = BuildSlot(i, cachedSlotWidth, cachedAvatarSize);
                HideSlot(slots[i]);
            }

            LayoutSlots();
            LastLeaderMask = 0;
            crownAnimTime = 0f;
            initialized = true;
        }

        public void InitializeStandalone(int capacity, TMP_FontAsset font = null)
        {
            Initialize(capacity, stripWidth, bottomRightOffset, font);
        }

        public static byte ComputeLeaderMask(IReadOnlyList<ParticipantHudSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                return 0;
            }

            int maxCoins = 0;
            for (int i = 0; i < snapshots.Count; i++)
            {
                ParticipantHudSnapshot snapshot = snapshots[i];
                if (!snapshot.IsParticipating || snapshot.Status == ParticipantStatus.Won || snapshot.Coins <= 0)
                {
                    continue;
                }

                if (snapshot.Coins > maxCoins)
                {
                    maxCoins = snapshot.Coins;
                }
            }

            if (maxCoins <= 0)
            {
                return 0;
            }

            bool useSlotId = CanIndexBySlotId(snapshots, 8);
            byte mask = 0;
            for (int i = 0; i < snapshots.Count; i++)
            {
                ParticipantHudSnapshot snapshot = snapshots[i];
                if (!snapshot.IsParticipating || snapshot.Status == ParticipantStatus.Won || snapshot.Coins != maxCoins)
                {
                    continue;
                }

                int bitIndex = useSlotId ? snapshot.SlotId : i;
                if (bitIndex >= 0 && bitIndex < 8)
                {
                    mask |= (byte)(1 << bitIndex);
                }
            }

            return mask;
        }

        public void Refresh(IReadOnlyList<ParticipantHudSnapshot> snapshots)
        {
            if (!initialized)
            {
                int fallbackCapacity = snapshots != null && snapshots.Count > 0
                    ? snapshots.Count
                    : DefaultCapacity;
                InitializeStandalone(fallbackCapacity, cachedFont);
            }

            if (slots == null || slots.Length == 0)
            {
                return;
            }

            Array.Clear(slotPopulated, 0, slotPopulated.Length);

            if (snapshots == null || snapshots.Count == 0)
            {
                LastLeaderMask = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    HideSlot(slots[i]);
                }
                SyncVerticalLayoutIfVisibilityChanged();
                return;
            }

            byte leaderMask = ComputeLeaderMask(snapshots);
            LastLeaderMask = leaderMask;

            bool useSlotIdForDisplay = CanIndexBySlotId(snapshots, slots.Length);
            bool useSlotIdForLeader = CanIndexBySlotId(snapshots, 8);

            for (int i = 0; i < snapshots.Count; i++)
            {
                ParticipantHudSnapshot snapshot = snapshots[i];
                int slotIndex = useSlotIdForDisplay ? snapshot.SlotId : i;
                if (slotIndex < 0 || slotIndex >= slots.Length)
                {
                    continue;
                }

                slotPopulated[slotIndex] = true;

                if (!snapshot.IsParticipating)
                {
                    HideSlot(slots[slotIndex]);
                    continue;
                }

                int leaderBitIndex = useSlotIdForLeader ? snapshot.SlotId : i;
                bool isLeader = snapshot.Status != ParticipantStatus.Won
                    && leaderBitIndex >= 0
                    && leaderBitIndex < 8
                    && (leaderMask & (1 << leaderBitIndex)) != 0;

                ApplySlotSnapshot(slots[slotIndex], snapshot, isLeader);
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (!slotPopulated[i])
                {
                    HideSlot(slots[i]);
                }
            }

            SyncVerticalLayoutIfVisibilityChanged();
        }

        public void Tick(float deltaTime)
        {
            if (!initialized || slots == null || deltaTime <= 0f)
            {
                return;
            }

            SyncHealthBarsIfCanvasMetricsChanged();

            float speed = Mathf.Max(0.5f, crownBobSpeed);
            float period = (Mathf.PI * 2f) / speed;
            crownAnimTime += deltaTime;
            if (crownAnimTime >= period * 16f)
            {
                crownAnimTime = Mathf.Repeat(crownAnimTime, period);
            }

            for (int i = 0; i < slots.Length; i++)
            {
                ParticipantSlot slot = slots[i];
                if (!slot.IsVisible || !slot.IsCrownVisible || slot.CrownRect == null)
                {
                    continue;
                }

                float phase = crownAnimTime * speed + i * 0.65f;
                float wave = Mathf.Sin(phase);
                slot.CrownRect.anchoredPosition = new Vector2(
                    slot.CrownBasePosition.x,
                    slot.CrownBasePosition.y + wave * crownBobAmplitude);
                float scale = 1f + wave * crownPulseScale;
                slot.CrownRect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        public bool IsSlotVisible(int index)
        {
            return IsValidSlotIndex(index) && slots[index].IsVisible;
        }

        public bool IsCrownVisible(int index)
        {
            return IsValidSlotIndex(index) && slots[index].IsVisible && slots[index].IsCrownVisible;
        }

        public bool IsMedalVisible(int index)
        {
            return IsValidSlotIndex(index) && slots[index].IsVisible && slots[index].IsMedalVisible;
        }

        public string GetMedalRankText(int index)
        {
            if (!IsValidSlotIndex(index) || !slots[index].IsVisible || !slots[index].IsMedalVisible)
            {
                return string.Empty;
            }
            return slots[index].DisplayedMedalRankText;
        }

        public string GetCoinText(int index)
        {
            if (!IsValidSlotIndex(index) || !slots[index].IsVisible || slots[index].IsMedalVisible)
            {
                return string.Empty;
            }
            return slots[index].DisplayedCoinText;
        }

        public HeartVisualState GetMiniHeartState(int slotIndex, int heartIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            }

            ParticipantSlot slot = slots[slotIndex];
            if (heartIndex < 0 || heartIndex >= slot.ActiveHeartCount)
            {
                throw new ArgumentOutOfRangeException(nameof(heartIndex));
            }

            return slot.HealthSegmentStates[heartIndex];
        }

        public int GetMiniHeartCount(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex) || !slots[slotIndex].IsVisible)
            {
                return 0;
            }
            return slots[slotIndex].ActiveHeartCount;
        }

        public Color GetHealthSegmentColor(int slotIndex, int segmentIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            }

            ParticipantSlot slot = slots[slotIndex];
            if (segmentIndex < 0 || segmentIndex >= slot.ActiveHeartCount)
            {
                throw new ArgumentOutOfRangeException(nameof(segmentIndex));
            }

            return slot.HealthSegmentImages[segmentIndex].color;
        }

        public string GetSlotCosmeticId(int index)
        {
            if (!IsValidSlotIndex(index) || !slots[index].IsVisible)
            {
                return null;
            }
            return slots[index].CosmeticId;
        }

        public int GetSlotOverlayCount(int index)
        {
            if (!IsValidSlotIndex(index) || !slots[index].IsVisible)
            {
                return 0;
            }
            return slots[index].ActiveOverlayCount;
        }

        private void OnRectTransformDimensionsChange()
        {
            SyncHealthBarsIfCanvasMetricsChanged();
        }

        private void OnTransformParentChanged()
        {
            cachedRootCanvas = null;
            lastCanvasScaleFactor = -1f;
            lastCanvasPixelSize = new Vector2(-1f, -1f);
            SyncHealthBarsIfCanvasMetricsChanged();
        }

        private void OnDestroy()
        {
            if (icons != null)
            {
                icons.Dispose();
                icons = null;
            }
        }

        private bool IsValidSlotIndex(int index)
        {
            return slots != null && index >= 0 && index < slots.Length;
        }

        private void ConfigureRootRect()
        {
            rootRect = transform as RectTransform;
            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }

            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = bottomRightOffset;
            rootRect.sizeDelta = LayoutMode == ParticipantHudLayoutMode.VerticalRightAligned
                ? new Vector2(verticalStripWidth, ComputeVerticalStripHeight())
                : new Vector2(stripWidth, stripHeight);
        }

        private int CountVisibleSlots()
        {
            if (slots == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].IsVisible)
                {
                    count++;
                }
            }

            return count;
        }

        private int ComputeVisibleSlotMask()
        {
            if (slots == null)
            {
                return 0;
            }

            int mask = 0;
            int limit = slots.Length < 31 ? slots.Length : 31;
            for (int i = 0; i < limit; i++)
            {
                if (slots[i] != null && slots[i].IsVisible)
                {
                    mask |= (1 << i);
                }
            }

            return mask;
        }

        private float ComputeVerticalStripHeight()
        {
            int visibleCount = CountVisibleSlots();
            int rowCount = visibleCount > 0
                ? visibleCount
                : (hasRefreshed ? 0 : (slots != null ? slots.Length : DefaultCapacity));

            if (rowCount <= 0)
            {
                return 0f;
            }

            return verticalPadding * 2f
                + rowCount * verticalRowHeight
                + Mathf.Max(0, rowCount - 1) * verticalSlotSpacing;
        }

        private void SyncVerticalLayoutIfVisibilityChanged()
        {
            hasRefreshed = true;
            int currentVisibleMask = ComputeVisibleSlotMask();
            if (currentVisibleMask == lastVisibleMask)
            {
                return;
            }

            lastVisibleMask = currentVisibleMask;
            if (LayoutMode == ParticipantHudLayoutMode.VerticalRightAligned)
            {
                ConfigureRootRect();
                LayoutVerticalSlots();
            }
        }

        private float ComputeSlotWidth(int capacity)
        {
            int safeCapacity = Mathf.Max(1, capacity);
            float innerWidth = Mathf.Max(24f, stripWidth - horizontalPadding * 2f);
            float totalGap = slotSpacing * Mathf.Max(0, safeCapacity - 1);
            return Mathf.Max(24f, (innerWidth - totalGap) / safeCapacity);
        }

        private float ComputeAvatarSize(float slotWidth)
        {
            float scaleY = stripHeight / ReferenceStripHeight;
            return Mathf.Min(71.5f * scaleY, Mathf.Max(20f, slotWidth - 6f));
        }

        private Vector2 ComputeHealthBarSize(float slotWidth, float avatarSize)
        {
            float scaleY = stripHeight / ReferenceStripHeight;
            float barWidth = Mathf.Max(16f, Mathf.Min(slotWidth - 8f, avatarSize * 0.81f));
            float barHeight = Mathf.Max(6f, ReferenceHealthBarHeight * scaleY);
            return new Vector2(barWidth, barHeight);
        }

        private Vector2 ComputeVerticalHealthBarSize(float avatarSize)
        {
            float barWidth = Mathf.Max(16f, avatarSize * 0.81f);
            float barHeight = Mathf.Max(6f, verticalHealthBarHeight);
            return new Vector2(barWidth, barHeight);
        }

        private float ResolveCanvasScaleFactor(out Canvas rootCanvas)
        {
            if (cachedRootCanvas == null)
            {
                Canvas parentCanvas = GetComponentInParent<Canvas>();
                cachedRootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
            }

            rootCanvas = cachedRootCanvas;
            if (rootCanvas != null && rootCanvas.scaleFactor > 0.0001f)
            {
                return rootCanvas.scaleFactor;
            }

            return 1f;
        }

        private void RecordCanvasMetrics(float scaleFactor, Canvas rootCanvas)
        {
            lastCanvasScaleFactor = scaleFactor;
            lastCanvasPixelSize = rootCanvas != null ? rootCanvas.pixelRect.size : Vector2.zero;
        }

        private void SyncHealthBarsIfCanvasMetricsChanged()
        {
            if (!initialized || slots == null || slots.Length == 0)
            {
                return;
            }

            float scaleFactor = ResolveCanvasScaleFactor(out Canvas rootCanvas);
            Vector2 pixelSize = rootCanvas != null ? rootCanvas.pixelRect.size : Vector2.zero;
            bool scaleChanged = Mathf.Abs(scaleFactor - lastCanvasScaleFactor) > 0.0005f;
            bool sizeChanged = (pixelSize - lastCanvasPixelSize).sqrMagnitude > 0.25f;
            if (!scaleChanged && !sizeChanged)
            {
                return;
            }

            RecordCanvasMetrics(scaleFactor, rootCanvas);

            Vector2 desiredBarSize;
            float desiredOffsetY;
            if (LayoutMode == ParticipantHudLayoutMode.VerticalRightAligned)
            {
                float rowHeight = Mathf.Max(48f, verticalRowHeight);
                float avatarSize = Mathf.Clamp(verticalAvatarSize, 24f, rowHeight - 6f);
                desiredBarSize = ComputeVerticalHealthBarSize(avatarSize);
                desiredOffsetY = avatarSize * HealthBarOuterRimOffsetRatio;
            }
            else
            {
                float slotWidth = ComputeSlotWidth(slots.Length);
                float avatarSize = ComputeAvatarSize(slotWidth);
                desiredBarSize = ComputeHealthBarSize(slotWidth, avatarSize);
                desiredOffsetY = avatarSize * HealthBarOuterRimOffsetRatio;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                ParticipantSlot slot = slots[i];
                if (slot == null || slot.HealthBarRect == null)
                {
                    continue;
                }

                ApplyPixelSnappedHealthBar(slot, desiredBarSize, desiredOffsetY, scaleFactor, rootCanvas);
            }
        }

        private void ApplyPixelSnappedHealthBar(
            ParticipantSlot slot,
            Vector2 desiredBarSize,
            float desiredOffsetY,
            float scaleFactor,
            Canvas rootCanvas)
        {
            RectTransform barRect = slot.HealthBarRect;
            if (barRect == null)
            {
                return;
            }

            float safeScale = scaleFactor > 0.0001f ? scaleFactor : 1f;
            int minSegments = slot.ActiveHeartCount > 0 ? slot.ActiveHeartCount : 3;
            int borderPx = Mathf.Max(1, Mathf.RoundToInt(HealthBarFrameThickness * safeScale));
            int gapPx = minSegments > 1 ? Mathf.Max(1, Mathf.RoundToInt(HealthBarSegmentGap * safeScale)) : 0;
            int minWidthPx = borderPx * 2 + gapPx * Mathf.Max(0, minSegments - 1) + minSegments;
            int barWidthPx = Mathf.Max(minWidthPx, Mathf.RoundToInt(desiredBarSize.x * safeScale));
            int barHeightPx = Mathf.Max(borderPx * 2 + 2, Mathf.RoundToInt(desiredBarSize.y * safeScale));

            float snappedWidth = barWidthPx / safeScale;
            float snappedHeight = barHeightPx / safeScale;

            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.sizeDelta = new Vector2(snappedWidth, snappedHeight);
            barRect.anchoredPosition = new Vector2(0f, desiredOffsetY);

            if (rootCanvas != null && rootCanvas.transform is RectTransform canvasRect)
            {
                Vector3 worldBottomLeft = barRect.TransformPoint(new Vector3(-snappedWidth * 0.5f, -snappedHeight * 0.5f, 0f));
                Vector3 canvasBottomLeft = canvasRect.InverseTransformPoint(worldBottomLeft);
                Rect cRect = canvasRect.rect;
                float screenLeftPx = (canvasBottomLeft.x - cRect.xMin) * safeScale;
                float screenBottomPx = (canvasBottomLeft.y - cRect.yMin) * safeScale;
                float snappedLeftPx = Mathf.Round(screenLeftPx);
                float snappedBottomPx = Mathf.Round(screenBottomPx);
                float deltaCanvasX = (snappedLeftPx - screenLeftPx) / safeScale;
                float deltaCanvasY = (snappedBottomPx - screenBottomPx) / safeScale;
                barRect.anchoredPosition = new Vector2(deltaCanvasX, desiredOffsetY + deltaCanvasY);
            }

            if (slot.ActiveHeartCount > 0)
            {
                LayoutHealthSegments(slot, slot.ActiveHeartCount);
            }
        }

        private void LayoutSlots()
        {
            if (slots == null || slots.Length == 0)
            {
                return;
            }

            if (LayoutMode == ParticipantHudLayoutMode.VerticalRightAligned)
            {
                LayoutVerticalSlots();
            }
            else
            {
                LayoutHorizontalSlots();
            }
        }

        private void LayoutHorizontalSlots()
        {
            float newSlotWidth = ComputeSlotWidth(slots.Length);
            float newAvatarSize = ComputeAvatarSize(newSlotWidth);
            cachedSlotWidth = newSlotWidth;
            cachedAvatarSize = newAvatarSize;

            Vector2 barSize = ComputeHealthBarSize(newSlotWidth, newAvatarSize);
            float barOffsetY = newAvatarSize * HealthBarOuterRimOffsetRatio;
            float scaleFactor = ResolveCanvasScaleFactor(out Canvas rootCanvas);
            RecordCanvasMetrics(scaleFactor, rootCanvas);

            for (int i = 0; i < slots.Length; i++)
            {
                ParticipantSlot slot = slots[i];
                if (slot == null || slot.Root == null)
                {
                    continue;
                }

                float x = horizontalPadding + i * (newSlotWidth + slotSpacing);
                slot.Root.anchorMin = new Vector2(0f, 0f);
                slot.Root.anchorMax = new Vector2(0f, 1f);
                slot.Root.pivot = new Vector2(0f, 0.5f);
                slot.Root.anchoredPosition = new Vector2(x, 0f);
                slot.Root.sizeDelta = new Vector2(newSlotWidth, 0f);

                if (slot.CrownRect != null)
                {
                    slot.CrownRect.anchorMin = new Vector2(0.5f, 1f);
                    slot.CrownRect.anchorMax = new Vector2(0.5f, 1f);
                    slot.CrownRect.pivot = new Vector2(0.5f, 0.5f);
                    slot.CrownBasePosition = slot.HorizontalCrownBasePosition;
                    slot.CrownRect.anchoredPosition = slot.CrownBasePosition;
                    slot.CrownRect.sizeDelta = slot.HorizontalCrownSize;
                }

                if (slot.TopMetricRect != null)
                {
                    slot.TopMetricRect.anchorMin = new Vector2(0f, 1f);
                    slot.TopMetricRect.anchorMax = new Vector2(1f, 1f);
                    slot.TopMetricRect.pivot = new Vector2(0.5f, 1f);
                    slot.TopMetricRect.anchoredPosition = slot.HorizontalMetricAnchoredPos;
                    slot.TopMetricRect.sizeDelta = slot.HorizontalMetricSizeDelta;
                }

                if (slot.AvatarRoot != null)
                {
                    slot.AvatarRoot.anchorMin = new Vector2(0.5f, 1f);
                    slot.AvatarRoot.anchorMax = new Vector2(0.5f, 1f);
                    slot.AvatarRoot.pivot = new Vector2(0.5f, 0.5f);
                    slot.AvatarRoot.anchoredPosition = slot.HorizontalAvatarAnchoredPos;
                    slot.AvatarRoot.sizeDelta = new Vector2(newAvatarSize, newAvatarSize);
                }

                if (slot.HealthBarRect != null)
                {
                    ApplyPixelSnappedHealthBar(slot, barSize, barOffsetY, scaleFactor, rootCanvas);
                }
            }
        }

        private void LayoutVerticalSlots()
        {
            float rowHeight = Mathf.Max(48f, verticalRowHeight);
            float avatarSize = Mathf.Clamp(verticalAvatarSize, 24f, rowHeight - 6f);
            float avatarOffsetY = Mathf.Clamp((rowHeight - avatarSize) * 0.3f, 0f, 6f);
            Vector2 barSize = ComputeVerticalHealthBarSize(avatarSize);
            float barOffsetY = avatarSize * HealthBarOuterRimOffsetRatio;
            float scaleFactor = ResolveCanvasScaleFactor(out Canvas rootCanvas);
            RecordCanvasMetrics(scaleFactor, rootCanvas);

            float avatarLeftX = -avatarSize;
            float metricWidth = Mathf.Max(48f, verticalMetricWidth);
            float metricRightX = avatarLeftX - Mathf.Max(0f, verticalAvatarMetricGap);
            float metricCenterX = metricRightX - metricWidth * 0.5f;
            float metricLeftX = metricRightX - metricWidth;

            Vector2 crownSize = new Vector2(
                Mathf.Max(16f, verticalCrownSize.x),
                Mathf.Max(14f, verticalCrownSize.y));
            float crownRightX = metricLeftX - Mathf.Max(0f, verticalMetricCrownGap);
            float crownCenterX = crownRightX - crownSize.x * 0.5f;

            int visibleCount = CountVisibleSlots();
            bool packVisibleOnly = visibleCount > 0;
            int visibleRowIndex = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                ParticipantSlot slot = slots[i];
                if (slot == null || slot.Root == null)
                {
                    continue;
                }

                int rowIndex = packVisibleOnly
                    ? (slot.IsVisible ? visibleRowIndex++ : i)
                    : i;
                float rowTopY = -(verticalPadding + rowIndex * (rowHeight + verticalSlotSpacing));

                slot.Root.anchorMin = new Vector2(1f, 1f);
                slot.Root.anchorMax = new Vector2(1f, 1f);
                slot.Root.pivot = new Vector2(1f, 1f);
                slot.Root.anchoredPosition = new Vector2(0f, rowTopY);
                slot.Root.sizeDelta = new Vector2(verticalStripWidth, rowHeight);

                if (slot.AvatarRoot != null)
                {
                    slot.AvatarRoot.anchorMin = new Vector2(1f, 0.5f);
                    slot.AvatarRoot.anchorMax = new Vector2(1f, 0.5f);
                    slot.AvatarRoot.pivot = new Vector2(0.5f, 0.5f);
                    slot.AvatarRoot.anchoredPosition = new Vector2(-avatarSize * 0.5f, avatarOffsetY);
                    slot.AvatarRoot.sizeDelta = new Vector2(avatarSize, avatarSize);
                }

                if (slot.HealthBarRect != null)
                {
                    ApplyPixelSnappedHealthBar(slot, barSize, barOffsetY, scaleFactor, rootCanvas);
                }

                if (slot.TopMetricRect != null)
                {
                    float metricHeight = Mathf.Max(20f, slot.HorizontalMetricSizeDelta.y);
                    slot.TopMetricRect.anchorMin = new Vector2(1f, 0.5f);
                    slot.TopMetricRect.anchorMax = new Vector2(1f, 0.5f);
                    slot.TopMetricRect.pivot = new Vector2(0.5f, 0.5f);
                    slot.TopMetricRect.anchoredPosition = new Vector2(metricCenterX, 0f);
                    slot.TopMetricRect.sizeDelta = new Vector2(metricWidth, metricHeight);
                }

                if (slot.CrownRect != null)
                {
                    slot.CrownRect.anchorMin = new Vector2(1f, 0.5f);
                    slot.CrownRect.anchorMax = new Vector2(1f, 0.5f);
                    slot.CrownRect.pivot = new Vector2(0.5f, 0.5f);
                    slot.CrownBasePosition = new Vector2(crownCenterX, 0f);
                    slot.CrownRect.anchoredPosition = slot.CrownBasePosition;
                    slot.CrownRect.sizeDelta = crownSize;
                }
            }
        }

        private ParticipantSlot BuildSlot(int index, float slotWidth, float avatarSize)
        {
            if (slotTemplate != null && slotTemplate.HasRequiredLayers)
                return BuildPrefabSlot(index);

            float scaleY = stripHeight / ReferenceStripHeight;
            Sprite circleMaskSprite = icons.GetCircleMaskSprite();
            Sprite circleFrameSprite = icons.GetCircleFrameSprite();

            var slot = new ParticipantSlot();

            var rootObj = new GameObject("ParticipantSlot_" + index, typeof(RectTransform), typeof(CanvasGroup));
            slot.Root = (RectTransform)rootObj.transform;
            slot.Root.SetParent(rootRect, false);
            slot.CanvasGroup = rootObj.GetComponent<CanvasGroup>();
            slot.CanvasGroup.alpha = 1f;
            slot.CanvasGroup.interactable = false;
            slot.CanvasGroup.blocksRaycasts = false;

            // Layer 1: Leader Crown (topmost, directly above coin count)
            slot.CrownImage = CreateImage(slot.Root, "CrownIcon", icons.GetCrownSprite(), Color.white);
            slot.CrownRect = slot.CrownImage.rectTransform;
            slot.CrownRect.anchorMin = new Vector2(0.5f, 1f);
            slot.CrownRect.anchorMax = new Vector2(0.5f, 1f);
            slot.CrownRect.pivot = new Vector2(0.5f, 0.5f);
            slot.HorizontalCrownBasePosition = new Vector2(0f, -15f * scaleY);
            slot.HorizontalCrownSize = new Vector2(36f * scaleY, 28f * scaleY);
            slot.CrownBasePosition = slot.HorizontalCrownBasePosition;
            slot.CrownRect.anchoredPosition = slot.CrownBasePosition;
            slot.CrownRect.sizeDelta = slot.HorizontalCrownSize;
            slot.CrownImage.gameObject.SetActive(false);

            // Layer 2: Coin count (not won) OR Medal + rank number (won)
            var metricObj = new GameObject("TopMetricLayer", typeof(RectTransform));
            var metricRect = (RectTransform)metricObj.transform;
            metricRect.SetParent(slot.Root, false);
            metricRect.anchorMin = new Vector2(0f, 1f);
            metricRect.anchorMax = new Vector2(1f, 1f);
            metricRect.pivot = new Vector2(0.5f, 1f);
            metricRect.anchoredPosition = new Vector2(0f, -31f * scaleY);
            metricRect.sizeDelta = new Vector2(0f, 28f * scaleY);
            slot.TopMetricRect = metricRect;
            slot.HorizontalMetricAnchoredPos = metricRect.anchoredPosition;
            slot.HorizontalMetricSizeDelta = metricRect.sizeDelta;

            // Layer 2a: Coin Group
            var coinGroupObj = new GameObject("CoinGroup", typeof(RectTransform));
            slot.CoinGroup = (RectTransform)coinGroupObj.transform;
            slot.CoinGroup.SetParent(metricRect, false);
            SetStretch(slot.CoinGroup, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            float coinIconSize = 20f * scaleY;
            slot.CoinIconImage = CreateImage(slot.CoinGroup, "CoinIcon", icons.GetCoinSprite(), Color.white);
            RectTransform coinIconRect = slot.CoinIconImage.rectTransform;
            coinIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            coinIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            coinIconRect.pivot = new Vector2(0.5f, 0.5f);
            coinIconRect.sizeDelta = new Vector2(coinIconSize, coinIconSize);
            coinIconRect.anchoredPosition = new Vector2(-16f, 0f);

            slot.CoinText = CreateText(
                slot.CoinGroup,
                "CoinText",
                20f * scaleY,
                12f,
                21f * scaleY,
                TextAlignmentOptions.MidlineLeft,
                coinTextColor);
            RectTransform coinTextRect = slot.CoinText.rectTransform;
            coinTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            coinTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            coinTextRect.pivot = new Vector2(0.5f, 0.5f);
            coinTextRect.sizeDelta = new Vector2(38f, 26f * scaleY);
            coinTextRect.anchoredPosition = new Vector2(15f, 0f);

            var coinShadow = slot.CoinText.gameObject.AddComponent<Shadow>();
            coinShadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            coinShadow.effectDistance = new Vector2(1f, -1f);

            // Layer 2b: Medal Group (Gold / Silver / Bronze + centered rank digit)
            var medalGroupObj = new GameObject("MedalGroup", typeof(RectTransform));
            slot.MedalGroup = (RectTransform)medalGroupObj.transform;
            slot.MedalGroup.SetParent(metricRect, false);
            SetStretch(slot.MedalGroup, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            float medalSize = 28f * scaleY;
            slot.MedalIconImage = CreateImage(slot.MedalGroup, "MedalIcon", icons.GetMedalSprite(1), Color.white);
            RectTransform medalIconRect = slot.MedalIconImage.rectTransform;
            medalIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            medalIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            medalIconRect.pivot = new Vector2(0.5f, 0.5f);
            medalIconRect.sizeDelta = new Vector2(medalSize, medalSize);
            medalIconRect.anchoredPosition = Vector2.zero;

            slot.MedalRankText = CreateText(
                slot.MedalGroup,
                "MedalRankText",
                16f * scaleY,
                10f,
                17f * scaleY,
                TextAlignmentOptions.Center,
                medalRankTextColor);
            RectTransform rankTextRect = slot.MedalRankText.rectTransform;
            rankTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            rankTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            rankTextRect.pivot = new Vector2(0.5f, 0.5f);
            rankTextRect.sizeDelta = new Vector2(medalSize * 0.62f, medalSize * 0.62f);
            rankTextRect.anchoredPosition = new Vector2(0f, -medalSize * 0.07f);
            slot.MedalGroup.gameObject.SetActive(false);

            // Layer 3: Circular Avatar Frame (reserved cosmetic overlay pool)
            var avatarObj = new GameObject("AvatarRoot", typeof(RectTransform));
            slot.AvatarRoot = (RectTransform)avatarObj.transform;
            slot.AvatarRoot.SetParent(slot.Root, false);
            slot.AvatarRoot.anchorMin = new Vector2(0.5f, 1f);
            slot.AvatarRoot.anchorMax = new Vector2(0.5f, 1f);
            slot.AvatarRoot.pivot = new Vector2(0.5f, 0.5f);
            slot.HorizontalAvatarAnchoredPos = new Vector2(0f, -100f * scaleY);
            slot.AvatarRoot.anchoredPosition = slot.HorizontalAvatarAnchoredPos;
            slot.AvatarRoot.sizeDelta = new Vector2(avatarSize, avatarSize);

            var maskObj = new GameObject("AvatarMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            var maskRect = (RectTransform)maskObj.transform;
            maskRect.SetParent(slot.AvatarRoot, false);
            SetStretch(maskRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            slot.MaskImage = maskObj.GetComponent<Image>();
            slot.MaskImage.sprite = circleMaskSprite;
            slot.MaskImage.color = avatarBackdropColor;
            slot.MaskImage.preserveAspect = true;
            slot.MaskImage.raycastTarget = false;
            var maskComponent = maskObj.GetComponent<Mask>();
            maskComponent.showMaskGraphic = true;

            slot.BasePortraitImage = CreateImage(maskRect, "BasePortrait", icons.GetDefaultPortraitSprite(), Color.white);
            SetStretch(slot.BasePortraitImage.rectTransform, new Vector2(0.04f, 0.00f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero);

            var overlayObj = new GameObject("OverlayContainer", typeof(RectTransform));
            slot.OverlayContainer = (RectTransform)overlayObj.transform;
            slot.OverlayContainer.SetParent(maskRect, false);
            AvatarPortraitVisualRenderer.AlignOverlayContainer(slot.OverlayContainer);

            slot.StatusOverlayImage = CreateImage(maskRect, "StatusOverlay", circleMaskSprite, respawningOverlayColor);
            SetStretch(slot.StatusOverlayImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            slot.StatusOverlayImage.enabled = false;

            slot.FrameImage = CreateImage(slot.AvatarRoot, "FrameImage", circleFrameSprite, Color.white);
            SetStretch(slot.FrameImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Layer 4: Segmented Rectangular Health Bar centered on the outer bottom rim of FrameImage
            Image healthBarBg = CreateSolidImage(slot.AvatarRoot, "HealthBarRoot", healthBarFrameColor);
            slot.HealthBarRect = healthBarBg.rectTransform;
            slot.HealthBarRect.anchorMin = new Vector2(0.5f, 0f);
            slot.HealthBarRect.anchorMax = new Vector2(0.5f, 0f);
            slot.HealthBarRect.pivot = new Vector2(0.5f, 0.5f);
            slot.HealthBarRect.anchoredPosition = new Vector2(0f, avatarSize * HealthBarOuterRimOffsetRatio);
            slot.HealthBarRect.sizeDelta = ComputeHealthBarSize(slotWidth, avatarSize);

            return slot;
        }

        private ParticipantSlot BuildPrefabSlot(int index)
        {
            var template = Instantiate(slotTemplate, rootRect);
            template.name = "ParticipantSlot_" + index;
            float scaleY = stripHeight / ReferenceStripHeight;
            if (!Mathf.Approximately(scaleY, 1f))
            {
                foreach (var rect in template.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rect == template.transform) continue;
                    rect.anchoredPosition *= scaleY;
                    rect.sizeDelta *= scaleY;
                }
            }
            RectTransform topMetricRect = template.coinGroup != null
                ? template.coinGroup.parent as RectTransform
                : (template.medalGroup != null ? template.medalGroup.parent as RectTransform : null);
            Vector2 crownBasePos = template.crownImage.rectTransform.anchoredPosition;
            Vector2 crownSize = template.crownImage.rectTransform.sizeDelta;
            var slot = new ParticipantSlot
            {
                Root = (RectTransform)template.transform,
                CanvasGroup = template.canvasGroup,
                CrownImage = template.crownImage,
                CrownRect = template.crownImage.rectTransform,
                CrownBasePosition = crownBasePos,
                TopMetricRect = topMetricRect,
                CoinGroup = template.coinGroup,
                CoinIconImage = template.coinIconImage,
                CoinText = template.coinText,
                MedalGroup = template.medalGroup,
                MedalIconImage = template.medalIconImage,
                MedalRankText = template.medalRankText,
                AvatarRoot = template.avatarRoot,
                MaskImage = template.maskImage,
                BasePortraitImage = template.basePortraitImage,
                OverlayContainer = template.overlayContainer,
                StatusOverlayImage = template.statusOverlayImage,
                FrameImage = template.frameImage,
                HealthBarRect = template.healthBarRect,
                HorizontalCrownBasePosition = crownBasePos,
                HorizontalCrownSize = crownSize,
                HorizontalMetricAnchoredPos = topMetricRect != null ? topMetricRect.anchoredPosition : new Vector2(0f, -31f * scaleY),
                HorizontalMetricSizeDelta = topMetricRect != null ? topMetricRect.sizeDelta : new Vector2(0f, 28f * scaleY),
                HorizontalAvatarAnchoredPos = template.avatarRoot != null ? template.avatarRoot.anchoredPosition : new Vector2(0f, -100f * scaleY)
            };
            slot.MaskImage.sprite = icons.GetCircleMaskSprite();
            slot.StatusOverlayImage.sprite = icons.GetCircleMaskSprite();
            slot.FrameImage.sprite = icons.GetCircleFrameSprite();
            slot.BasePortraitImage.sprite = icons.GetDefaultPortraitSprite();
            AvatarPortraitVisualRenderer.AlignOverlayContainer(slot.OverlayContainer);
            slot.CoinIconImage.sprite = icons.GetCoinSprite();
            slot.CrownImage.sprite = icons.GetCrownSprite();
            slot.MedalIconImage.sprite = icons.GetMedalSprite(1);
            if (cachedFont != null)
            {
                slot.CoinText.font = cachedFont;
                slot.MedalRankText.font = cachedFont;
            }
            return slot;
        }

        private void ApplySlotSnapshot(ParticipantSlot slot, ParticipantHudSnapshot snapshot, bool isLeader)
        {
            bool firstOrShown = !slot.HasAppliedState || !slot.IsVisible;
            if (!slot.IsVisible)
            {
                slot.IsVisible = true;
                slot.Root.gameObject.SetActive(true);
            }

            bool statusChanged = firstOrShown || slot.LastStatus != snapshot.Status;
            bool localChanged = firstOrShown || slot.IsLocal != snapshot.IsLocal;
            bool colorChanged = firstOrShown || !ColorsApproximatelyEqual(slot.LastParticipantColor, snapshot.Color);

            slot.LastStatus = snapshot.Status;
            slot.IsLocal = snapshot.IsLocal;
            slot.LastParticipantColor = snapshot.Color;

            // Layer 1: Leader crown (never shown on Won participants)
            bool showCrown = isLeader && snapshot.Status != ParticipantStatus.Won;
            if (firstOrShown || slot.IsCrownVisible != showCrown)
            {
                slot.IsCrownVisible = showCrown;
                slot.CrownImage.gameObject.SetActive(showCrown);
                if (showCrown)
                {
                    slot.CrownImage.sprite = icons.GetCrownSprite();
                    slot.CrownRect.anchoredPosition = slot.CrownBasePosition;
                    slot.CrownRect.localScale = Vector3.one;
                }
            }

            // Layer 2: Coin count or Won medal + rank number
            bool isWon = snapshot.Status == ParticipantStatus.Won;
            bool modeChanged = firstOrShown || slot.IsMedalVisible != isWon;
            if (modeChanged)
            {
                slot.IsMedalVisible = isWon;
                slot.MedalGroup.gameObject.SetActive(isWon);
                slot.CoinGroup.gameObject.SetActive(!isWon);
            }

            if (isWon)
            {
                int normalizedOrder = snapshot.FinishOrder > 0 ? snapshot.FinishOrder : 1;
                if (modeChanged || slot.LastFinishOrder != normalizedOrder)
                {
                    slot.LastFinishOrder = normalizedOrder;
                    slot.MedalIconImage.sprite = icons.GetMedalSprite(normalizedOrder);
                    string rankText = FormatRank(normalizedOrder);
                    slot.DisplayedMedalRankText = rankText;
                    slot.MedalRankText.text = rankText;
                }
                slot.DisplayedCoinText = string.Empty;
                slot.LastCoins = snapshot.Coins;
            }
            else
            {
                int clampedCoins = snapshot.Coins < 0 ? 0 : snapshot.Coins;
                if (modeChanged || slot.LastCoins != clampedCoins)
                {
                    slot.LastCoins = clampedCoins;
                    string coinStr = FormatCoins(clampedCoins);
                    slot.DisplayedCoinText = coinStr;
                    slot.CoinText.text = coinStr;
                }
                slot.DisplayedMedalRankText = string.Empty;
                slot.LastFinishOrder = snapshot.FinishOrder;
            }

            // Layer 3: Avatar appearance & cosmetic overlays
            ApplyAvatarAppearance(slot, snapshot, firstOrShown, statusChanged, localChanged, colorChanged);

            // Layer 4: Embedded segmented rectangular health bar
            ApplyHealthSegments(slot, snapshot, firstOrShown);

            // Overall status feedback (CanvasGroup alpha, avatar status overlay, coin dim)
            if (firstOrShown || statusChanged || localChanged || colorChanged)
            {
                ApplySlotStatusVisuals(slot, snapshot);
            }

            slot.HasAppliedState = true;
        }

        private void ApplyAvatarAppearance(
            ParticipantSlot slot,
            ParticipantHudSnapshot snapshot,
            bool force,
            bool statusChanged,
            bool localChanged,
            bool colorChanged)
        {
            AvatarAppearanceData appearance = snapshot.Appearance;
            slot.CosmeticId = appearance.CosmeticId;

            Sprite resolvedPortrait = AvatarPortraitVisualRenderer.ResolveBasePortrait(appearance, icons);

            Color rawTint = appearance.TintColor.a > 0.001f
                ? appearance.TintColor
                : (snapshot.Color.a > 0.001f ? snapshot.Color : Color.white);
            Color statusTintedColor = ResolveStatusTintedColor(rawTint, snapshot.Status);

            bool portraitChanged = force || slot.LastBasePortrait != resolvedPortrait;
            bool tintChanged = force || !ColorsApproximatelyEqual(slot.LastTintColor, rawTint);

            if (portraitChanged)
            {
                slot.LastBasePortrait = resolvedPortrait;
                slot.BasePortraitImage.sprite = resolvedPortrait;
            }

            if (portraitChanged || tintChanged || statusChanged)
            {
                slot.LastTintColor = rawTint;
                slot.BasePortraitImage.color = statusTintedColor;
            }

            Sprite resolvedFrame = AvatarPortraitVisualRenderer.ResolveFrameSprite(appearance, icons);

            if (force || slot.LastFrameSprite != resolvedFrame)
            {
                slot.LastFrameSprite = resolvedFrame;
                slot.FrameImage.sprite = resolvedFrame;
            }

            if (force || statusChanged || localChanged || colorChanged || tintChanged)
            {
                Color accent = snapshot.Color.a > 0.001f ? snapshot.Color : rawTint;
                slot.FrameImage.color = ResolveFrameColor(accent, snapshot.Status, snapshot.IsLocal);
            }

            bool overlaysChanged = force
                || !slot.HasAppliedAppearance
                || !AvatarPortraitVisualRenderer.AreOverlaysEqual(slot.LastAppearance, appearance);
            Color overlayColor = ResolveOverlayColor(snapshot.Status);

            if (overlaysChanged)
            {
                slot.ActiveOverlayCount = AvatarPortraitVisualRenderer.ApplyOverlays(
                    slot.OverlayContainer,
                    slot.OverlayImages,
                    appearance,
                    overlayColor,
                    statusTintedColor,
                    false);
                slot.LastAppearance = appearance;
                slot.HasAppliedAppearance = true;
            }
            else if (statusChanged || tintChanged)
            {
                AvatarPortraitVisualRenderer.ApplyOverlayColors(
                    slot.OverlayImages,
                    slot.ActiveOverlayCount,
                    appearance,
                    overlayColor,
                    statusTintedColor);
            }
        }

        private void ApplyHealthSegments(
            ParticipantSlot slot,
            ParticipantHudSnapshot snapshot,
            bool force)
        {
            int totalSegments = Mathf.Max(1, Mathf.Max(snapshot.InitialMaxHealth, snapshot.MaxHealth));
            int clampedMax = Mathf.Clamp(snapshot.MaxHealth, 0, totalSegments);
            int clampedHealth = Mathf.Clamp(snapshot.Health, 0, clampedMax);

            bool countChanged = force || slot.ActiveHeartCount != totalSegments;
            bool valuesChanged = countChanged
                || slot.LastHealth != clampedHealth
                || slot.LastMaxHealth != clampedMax
                || slot.LastInitialMaxHealth != totalSegments;

            if (!valuesChanged)
            {
                return;
            }

            slot.LastHealth = clampedHealth;
            slot.LastMaxHealth = clampedMax;
            slot.LastInitialMaxHealth = totalSegments;

            EnsureHealthSegmentPool(slot, totalSegments);

            if (countChanged)
            {
                slot.ActiveHeartCount = totalSegments;
                LayoutHealthSegments(slot, totalSegments);
            }

            for (int i = 0; i < totalSegments; i++)
            {
                HeartVisualState targetState = i < clampedHealth
                    ? HeartVisualState.Full
                    : (i < clampedMax ? HeartVisualState.Dim : HeartVisualState.Broken);

                Image segmentImg = slot.HealthSegmentImages[i];
                slot.HealthSegmentStates[i] = targetState;

                if (!segmentImg.enabled)
                {
                    segmentImg.enabled = true;
                }

                Color segmentColor = ResolveHealthSegmentColor(targetState);
                if (!ColorsApproximatelyEqual(segmentImg.color, segmentColor))
                {
                    segmentImg.color = segmentColor;
                }
            }

            for (int i = totalSegments; i < slot.HealthSegmentImages.Count; i++)
            {
                Image unused = slot.HealthSegmentImages[i];
                if (unused.enabled)
                {
                    unused.enabled = false;
                }
            }
        }

        private void ApplySlotStatusVisuals(ParticipantSlot slot, ParticipantHudSnapshot snapshot)
        {
            ParticipantStatus status = snapshot.Status;
            bool isLost = status == ParticipantStatus.Lost;
            bool isRespawning = status == ParticipantStatus.Respawning;

            if (slot.CanvasGroup != null)
            {
                slot.CanvasGroup.alpha = isRespawning
                    ? respawningSlotAlpha
                    : (isLost ? lostSlotAlpha : 1f);
            }

            if (slot.StatusOverlayImage != null)
            {
                if (isRespawning)
                {
                    slot.StatusOverlayImage.enabled = true;
                    slot.StatusOverlayImage.color = respawningOverlayColor;
                }
                else if (isLost)
                {
                    slot.StatusOverlayImage.enabled = true;
                    slot.StatusOverlayImage.color = lostOverlayColor;
                }
                else
                {
                    slot.StatusOverlayImage.enabled = false;
                }
            }

            if (slot.CoinIconImage != null)
            {
                slot.CoinIconImage.color = isLost
                    ? new Color(0.55f, 0.55f, 0.58f, 0.85f)
                    : (isRespawning ? new Color(0.82f, 0.82f, 0.85f, 0.90f) : Color.white);
            }

            if (slot.CoinText != null)
            {
                slot.CoinText.color = isLost ? lostCoinTextColor : coinTextColor;
            }
        }

        private void HideSlot(ParticipantSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            if (slot.Root != null && (slot.IsVisible || slot.Root.gameObject.activeSelf))
            {
                slot.Root.gameObject.SetActive(false);
            }

            slot.IsVisible = false;
            slot.IsCrownVisible = false;
            slot.IsMedalVisible = false;
            slot.DisplayedCoinText = string.Empty;
            slot.DisplayedMedalRankText = string.Empty;
            slot.CosmeticId = null;
            slot.HasAppliedAppearance = false;
            slot.ActiveOverlayCount = 0;
            slot.ActiveHeartCount = 0;
        }

        private void EnsureHealthSegmentPool(ParticipantSlot slot, int requiredCount)
        {
            while (slot.HealthSegmentImages.Count < requiredCount)
            {
                int newIdx = slot.HealthSegmentImages.Count;
                Image segmentImg = CreateSolidImage(slot.HealthBarRect, "HealthSegment_" + newIdx, healthFullColor);
                RectTransform rect = segmentImg.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                segmentImg.enabled = false;
                slot.HealthSegmentImages.Add(segmentImg);
                slot.HealthSegmentStates.Add(HeartVisualState.Full);
            }
        }

        private void LayoutHealthSegments(ParticipantSlot slot, int count)
        {
            if (count <= 0 || slot.HealthBarRect == null)
            {
                return;
            }

            float scaleFactor = ResolveCanvasScaleFactor(out _);
            float safeScale = scaleFactor > 0.0001f ? scaleFactor : 1f;
            Vector2 barSize = slot.HealthBarRect.sizeDelta;

            int borderPx = Mathf.Max(1, Mathf.RoundToInt(HealthBarFrameThickness * safeScale));
            int barWidthPx = Mathf.Max(borderPx * 2 + count, Mathf.RoundToInt(barSize.x * safeScale));
            int barHeightPx = Mathf.Max(borderPx * 2 + 2, Mathf.RoundToInt(barSize.y * safeScale));
            int maxGapPx = count > 1 ? Mathf.Max(0, (barWidthPx - borderPx * 2 - count) / (count - 1)) : 0;
            int gapPx = count > 1 ? Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(HealthBarSegmentGap * safeScale)), maxGapPx) : 0;
            int usableWidthPx = Mathf.Max(count, barWidthPx - borderPx * 2 - gapPx * Mathf.Max(0, count - 1));
            int innerHeightPx = Mathf.Max(2, barHeightPx - borderPx * 2);

            float yStart = borderPx / safeScale;
            float segHeight = innerHeightPx / safeScale;

            for (int i = 0; i < count; i++)
            {
                int xStartPx = borderPx + i * gapPx + (i * usableWidthPx) / count;
                int xEndPx = borderPx + i * gapPx + ((i + 1) * usableWidthPx) / count;
                int segWidthPx = Mathf.Max(1, xEndPx - xStartPx);

                RectTransform rect = slot.HealthSegmentImages[i].rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(xStartPx / safeScale, yStart);
                rect.sizeDelta = new Vector2(segWidthPx / safeScale, segHeight);
            }
        }

        private void ApplyFontToSlots(TMP_FontAsset font)
        {
            if (slots == null || font == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                ParticipantSlot slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (slot.CoinText != null)
                {
                    slot.CoinText.font = font;
                    if (font.material != null)
                    {
                        slot.CoinText.fontSharedMaterial = font.material;
                    }
                    slot.CoinText.fontStyle = FontStyles.Normal;
                    slot.CoinText.fontWeight = FontWeight.Regular;
                }
                if (slot.MedalRankText != null)
                {
                    slot.MedalRankText.font = font;
                    if (font.material != null)
                    {
                        slot.MedalRankText.fontSharedMaterial = font.material;
                    }
                    slot.MedalRankText.fontStyle = FontStyles.Normal;
                    slot.MedalRankText.fontWeight = FontWeight.Regular;
                }
            }
        }

        private void ClearBuiltSlots()
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].Root != null)
                {
                    GameObject go = slots[i].Root.gameObject;
                    if (Application.isPlaying)
                    {
                        Destroy(go);
                    }
                    else
                    {
                        DestroyImmediate(go);
                    }
                }
            }

            slots = null;
            slotPopulated = null;
            initialized = false;
            hasRefreshed = false;
            lastVisibleMask = -1;
        }

        private Image CreateSolidImage(RectTransform parent, string objectName, Color color)
        {
            Image img = CreateImage(parent, objectName, null, color);
            img.preserveAspect = false;
            return img;
        }

        private Image CreateImage(RectTransform parent, string objectName, Sprite sprite, Color color)
        {
            var obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            var img = obj.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        private TMP_Text CreateText(
            RectTransform parent,
            string objectName,
            float fontSize,
            float fontSizeMin,
            float fontSizeMax,
            TextAlignmentOptions alignment,
            Color color)
        {
            var obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            var tmp = obj.GetComponent<TextMeshProUGUI>();
            if (cachedFont != null)
            {
                tmp.font = cachedFont;
                if (cachedFont.material != null)
                {
                    tmp.fontSharedMaterial = cachedFont.material;
                }
            }
            tmp.alignment = alignment;
            tmp.fontStyle = FontStyles.Normal;
            tmp.fontWeight = FontWeight.Regular;
            tmp.fontSize = fontSize;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = fontSizeMin;
            tmp.fontSizeMax = Mathf.Max(fontSizeMin, fontSizeMax);
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void SetStretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private Color ResolveHealthSegmentColor(HeartVisualState state)
        {
            switch (state)
            {
                case HeartVisualState.Full:
                    return healthFullColor;
                case HeartVisualState.Dim:
                    return healthDimColor;
                default:
                    return healthBrokenColor;
            }
        }

        private Color ResolveFrameColor(Color accent, ParticipantStatus status, bool isLocal)
        {
            if (status == ParticipantStatus.Lost)
            {
                return DesaturateAndDarken(accent, 0.50f);
            }

            if (status == ParticipantStatus.Won)
            {
                return Color.Lerp(wonFrameAccentColor, accent, 0.30f);
            }

            Color baseFrame = isLocal
                ? Color.Lerp(localFrameHighlightColor, accent, 0.24f)
                : Color.Lerp(new Color(0.84f, 0.88f, 0.93f, 1f), accent, 0.62f);

            if (status == ParticipantStatus.Respawning)
            {
                return DarkenColor(baseFrame, 0.68f);
            }

            return baseFrame;
        }

        private static Color ResolveStatusTintedColor(Color baseColor, ParticipantStatus status)
        {
            if (status == ParticipantStatus.Lost)
            {
                return DesaturateAndDarken(baseColor, 0.52f);
            }

            if (status == ParticipantStatus.Respawning)
            {
                return DarkenColor(baseColor, 0.68f);
            }

            return baseColor;
        }

        private static Color ResolveOverlayColor(ParticipantStatus status)
        {
            if (status == ParticipantStatus.Lost)
            {
                return new Color(0.52f, 0.52f, 0.54f, 0.85f);
            }

            if (status == ParticipantStatus.Respawning)
            {
                return new Color(0.72f, 0.74f, 0.78f, 0.90f);
            }

            return Color.white;
        }

        private static Color DarkenColor(Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
        }

        private static Color DesaturateAndDarken(Color color, float brightness)
        {
            float luminance = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            float gray = Mathf.Clamp01(luminance * brightness + 0.10f * brightness);
            return new Color(gray, gray, gray, color.a);
        }

        private static bool ColorsApproximatelyEqual(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.002f
                && Mathf.Abs(a.g - b.g) < 0.002f
                && Mathf.Abs(a.b - b.b) < 0.002f
                && Mathf.Abs(a.a - b.a) < 0.002f;
        }

        private static bool CanIndexBySlotId(IReadOnlyList<ParticipantHudSnapshot> snapshots, int maxSlots)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                return false;
            }

            int limit = maxSlots < 31 ? maxSlots : 31;
            int seenMask = 0;
            for (int i = 0; i < snapshots.Count; i++)
            {
                int slotId = snapshots[i].SlotId;
                if (slotId < 0 || slotId >= limit)
                {
                    return false;
                }

                int bit = 1 << slotId;
                if ((seenMask & bit) != 0)
                {
                    return false;
                }

                seenMask |= bit;
            }

            return true;
        }

        private static string FormatCoins(int value)
        {
            int clamped = value < 0 ? 0 : value;
            if (clamped < CachedCoinStringCount)
            {
                return CachedTwoDigitStrings[clamped];
            }

            return clamped.ToString("D2", CultureInfo.InvariantCulture);
        }

        private static string FormatRank(int finishOrder)
        {
            int order = finishOrder > 0 ? finishOrder : 1;
            if (order <= CachedRankStringCount)
            {
                return CachedRankStrings[order - 1];
            }

            return order.ToString(CultureInfo.InvariantCulture);
        }

        private static string[] BuildTwoDigitStrings()
        {
            var result = new string[CachedCoinStringCount];
            for (int i = 0; i < CachedCoinStringCount; i++)
            {
                result[i] = i.ToString("D2", CultureInfo.InvariantCulture);
            }
            return result;
        }

        private static string[] BuildRankStrings()
        {
            var result = new string[CachedRankStringCount];
            for (int i = 0; i < CachedRankStringCount; i++)
            {
                result[i] = (i + 1).ToString(CultureInfo.InvariantCulture);
            }
            return result;
        }
    }
}
