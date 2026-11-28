using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    public sealed class SegmentedHealthBarView : MonoBehaviour
    {
        [Header("Appearance")]
        [SerializeField] private UnityEngine.UI.Image frameImage;
        [SerializeField] private Color frameColor = new Color(0.075f, 0.090f, 0.122f, 1f);
        [SerializeField] private Color fullColor = new Color(0.20f, 0.82f, 0.35f, 1f);
        [SerializeField] private Color dimColor = new Color(0.56f, 0.66f, 0.59f, 1f);
        [SerializeField] private Color brokenColor = new Color(0.88f, 0.24f, 0.24f, 1f);

        [Header("Layout")]
        [SerializeField, Min(0.5f)] private float frameThickness = 3f;
        [SerializeField, Min(0f)] private float segmentGap = 3f;

        private RectTransform rootRect;
        private readonly List<UnityEngine.UI.Image> segmentImages = new List<UnityEngine.UI.Image>(4);
        private readonly List<HeartVisualState> segmentStates = new List<HeartVisualState>(4);
        private bool initialized;
        private int activeSegmentCount;
        private int lastHealth = int.MinValue;
        private int lastMaxHealth = int.MinValue;
        private int lastInitialMaxHealth = int.MinValue;
        private Vector2 lastLayoutSize = new Vector2(-1f, -1f);

        public int SegmentCount => activeSegmentCount;
        public int LastHealth => lastHealth;
        public int LastMaxHealth => lastMaxHealth;
        public int LastInitialMaxHealth => lastInitialMaxHealth;
        public float FrameThickness => frameThickness;
        public float SegmentGap => segmentGap;
        public Color FrameColor => frameColor;
        public Color FullColor => fullColor;
        public Color DimColor => dimColor;
        public Color BrokenColor => brokenColor;

        public void EnsureInitialized()
        {
            if (rootRect == null)
            {
                rootRect = transform as RectTransform;
                if (rootRect == null)
                {
                    rootRect = gameObject.AddComponent<RectTransform>();
                }
            }

            if (frameImage == null)
            {
                frameImage = GetComponent<UnityEngine.UI.Image>();
                if (frameImage == null)
                {
                    frameImage = gameObject.AddComponent<UnityEngine.UI.Image>();
                }
            }

            frameImage.sprite = null;
            frameImage.preserveAspect = false;
            frameImage.raycastTarget = false;
            if (!ColorsApproximatelyEqual(frameImage.color, frameColor))
            {
                frameImage.color = frameColor;
            }

            if (initialized)
            {
                return;
            }

            segmentImages.Clear();
            segmentStates.Clear();
            int existingIndex = 0;
            while (true)
            {
                Transform existing = transform.Find("HealthSegment_" + existingIndex);
                if (existing == null)
                {
                    break;
                }

                var img = existing.GetComponent<UnityEngine.UI.Image>();
                if (img == null)
                {
                    img = existing.gameObject.AddComponent<UnityEngine.UI.Image>();
                }

                img.sprite = null;
                img.preserveAspect = false;
                img.raycastTarget = false;
                segmentImages.Add(img);
                segmentStates.Add(HeartVisualState.Full);
                existingIndex++;
            }

            initialized = true;
        }

        public void SetHealth(int current, int max, int initialMax, bool force = false)
        {
            EnsureInitialized();

            int totalSegments = Mathf.Max(1, Mathf.Max(initialMax, max));
            int clampedMax = Mathf.Clamp(max, 0, totalSegments);
            int clampedHealth = Mathf.Clamp(current, 0, clampedMax);
            Vector2 currentSize = ResolveBarSize();

            bool sizeChanged = Mathf.Abs(currentSize.x - lastLayoutSize.x) > 0.01f
                || Mathf.Abs(currentSize.y - lastLayoutSize.y) > 0.01f;
            bool countChanged = activeSegmentCount != totalSegments;
            bool valuesChanged = countChanged
                || lastHealth != clampedHealth
                || lastMaxHealth != clampedMax
                || lastInitialMaxHealth != totalSegments;

            if (!force && !valuesChanged && !sizeChanged)
            {
                return;
            }

            lastHealth = clampedHealth;
            lastMaxHealth = clampedMax;
            lastInitialMaxHealth = totalSegments;

            EnsureSegmentPool(totalSegments);

            if (force || countChanged || sizeChanged)
            {
                activeSegmentCount = totalSegments;
                LayoutSegments(totalSegments, currentSize);
            }

            for (int i = 0; i < totalSegments; i++)
            {
                HeartVisualState targetState = i < clampedHealth
                    ? HeartVisualState.Full
                    : (i < clampedMax ? HeartVisualState.Dim : HeartVisualState.Broken);

                UnityEngine.UI.Image segmentImg = segmentImages[i];
                segmentStates[i] = targetState;

                if (!segmentImg.enabled)
                {
                    segmentImg.enabled = true;
                }

                Color targetColor = ResolveSegmentColor(targetState);
                if (!ColorsApproximatelyEqual(segmentImg.color, targetColor))
                {
                    segmentImg.color = targetColor;
                }
            }

            for (int i = totalSegments; i < segmentImages.Count; i++)
            {
                UnityEngine.UI.Image unused = segmentImages[i];
                if (unused.enabled)
                {
                    unused.enabled = false;
                }
            }
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        public void RefreshLayout()
        {
            EnsureInitialized();
            if (activeSegmentCount <= 0)
            {
                return;
            }

            LayoutSegments(activeSegmentCount, ResolveBarSize());
        }

        public HeartVisualState GetSegmentState(int index)
        {
            if (index < 0 || index >= activeSegmentCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return segmentStates[index];
        }

        public Color GetSegmentColor(int index)
        {
            if (index < 0 || index >= activeSegmentCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return segmentImages[index].color;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!initialized || activeSegmentCount <= 0)
            {
                return;
            }

            Vector2 size = ResolveBarSize();
            if (Mathf.Abs(size.x - lastLayoutSize.x) > 0.01f
                || Mathf.Abs(size.y - lastLayoutSize.y) > 0.01f)
            {
                LayoutSegments(activeSegmentCount, size);
            }
        }

        private void EnsureSegmentPool(int requiredCount)
        {
            while (segmentImages.Count < requiredCount)
            {
                int newIdx = segmentImages.Count;
                var segObj = new GameObject(
                    "HealthSegment_" + newIdx,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(UnityEngine.UI.Image));
                segObj.layer = gameObject.layer;

                var rect = (RectTransform)segObj.transform;
                rect.SetParent(rootRect, false);
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);

                var img = segObj.GetComponent<UnityEngine.UI.Image>();
                img.sprite = null;
                img.preserveAspect = false;
                img.raycastTarget = false;
                img.color = fullColor;
                img.enabled = false;

                segmentImages.Add(img);
                segmentStates.Add(HeartVisualState.Full);
            }
        }

        private void LayoutSegments(int count, Vector2 barSize)
        {
            if (count <= 0 || rootRect == null)
            {
                return;
            }

            float border = Mathf.Max(0.5f, frameThickness);
            float innerWidth = Mathf.Max(4f, barSize.x - border * 2f);
            float innerHeight = Mathf.Max(2f, barSize.y - border * 2f);
            float gap = count > 1 ? Mathf.Min(Mathf.Max(0f, segmentGap), innerWidth / (count * 3f)) : 0f;
            float segmentWidth = Mathf.Max(1f, (innerWidth - gap * Mathf.Max(0, count - 1)) / count);

            for (int i = 0; i < count; i++)
            {
                RectTransform rect = segmentImages[i].rectTransform;
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(segmentWidth, innerHeight);
                rect.anchoredPosition = new Vector2(border + i * (segmentWidth + gap), 0f);
            }

            lastLayoutSize = barSize;
        }

        private Vector2 ResolveBarSize()
        {
            if (rootRect == null)
            {
                return Vector2.zero;
            }

            Rect rect = rootRect.rect;
            float width = rect.width > 0.01f ? rect.width : rootRect.sizeDelta.x;
            float height = rect.height > 0.01f ? rect.height : rootRect.sizeDelta.y;

            if (rootRect.parent is RectTransform parentRect)
            {
                float anchorSpanX = rootRect.anchorMax.x - rootRect.anchorMin.x;
                if (width <= 0.01f && anchorSpanX > 0.001f)
                {
                    float parentWidth = parentRect.rect.width > 0.01f ? parentRect.rect.width : parentRect.sizeDelta.x;
                    width = parentWidth * anchorSpanX + rootRect.sizeDelta.x;
                }

                float anchorSpanY = rootRect.anchorMax.y - rootRect.anchorMin.y;
                if (height <= 0.01f && anchorSpanY > 0.001f)
                {
                    float parentHeight = parentRect.rect.height > 0.01f ? parentRect.rect.height : parentRect.sizeDelta.y;
                    height = parentHeight * anchorSpanY + rootRect.sizeDelta.y;
                }
            }

            return new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
        }

        private Color ResolveSegmentColor(HeartVisualState state)
        {
            switch (state)
            {
                case HeartVisualState.Full:
                    return fullColor;
                case HeartVisualState.Dim:
                    return dimColor;
                default:
                    return brokenColor;
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
