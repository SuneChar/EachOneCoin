using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    /// <summary>
    /// 右上角紧凑事件播报栏（1920x1080 参考画布，位于 MatchClockAndMineView 正下方）。
    /// 采用最旧在上（index 0）、最新在下（index ActiveEntryCount - 1）的自然战报阅读顺序。
    /// 内部预建固定容量对象池，运行时零 Instantiate / Destroy 与零额外 GC 分配。
    /// </summary>
    public sealed class MatchEventFeedView : MonoBehaviour
    {
        private const float MinPanelWidth = 120f;
        private const float MinEntryHeight = 18f;
        private const float MinAccentBarWidth = 2f;
        private const float MinEntryLifetime = 0.05f;
        private const float AccentInsetRight = 2f;
        private const float AccentInsetVertical = 3f;
        private const float TextGapBeforeAccent = 6f;
        private const float TextInsetLeft = 4f;
        private const float TextInsetVertical = 0f;
        private const float EntryFontSizeMin = 12f;
        private const float EntryFontSizeMax = 16f;

        private sealed class FeedEntry
        {
            public GameObject RootObject;
            public RectTransform RootRect;
            public CanvasGroup CanvasGroup;
            public Image AccentBarImage;
            public TMP_Text MessageText;
            public string Text;
            public float RemainingTime;
            public float Alpha;
            public bool Active;
        }

        [Header("Layout (1920x1080 Reference Canvas)")]
        [SerializeField] private Vector2 topRightOffset = new Vector2(-20f, -150f);
        [SerializeField, Min(120f)] private float panelWidth = 300f;
        [SerializeField, Min(1)] private int maxVisibleEntries = 4;
        [SerializeField, Min(18f)] private float entryHeight = 26f;
        [SerializeField, Min(0f)] private float entrySpacing = 3f;
        [SerializeField, Min(2f)] private float accentBarWidth = 3f;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float entryLifetime = 4.5f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.6f;

        [Header("Colors")]
        [SerializeField] private Color textColor = new Color(0.95f, 0.97f, 1.00f, 1f);

        private RectTransform rootRect;
        private FeedEntry[] pool;
        private FeedEntry[] activeEntries;
        private FeedEntry[] freeEntries;
        private int activeCount;
        private int freeCount;
        private bool initialized;

        public Vector2 TopRightOffset => topRightOffset;
        public int MaxVisibleEntries => Mathf.Max(1, maxVisibleEntries);
        public int ActiveEntryCount => activeCount;

        public void Initialize(TMP_FontAsset font = null)
        {
            if (initialized)
            {
                if (font != null)
                {
                    ApplyFont(font);
                }
                return;
            }

            int capacity = MaxVisibleEntries;
            float width = Mathf.Max(MinPanelWidth, panelWidth);
            float height = Mathf.Max(MinEntryHeight, entryHeight);
            float spacing = Mathf.Max(0f, entrySpacing);
            float totalHeight = capacity * height + Mathf.Max(0, capacity - 1) * spacing;

            rootRect = transform as RectTransform;
            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }

            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = topRightOffset;
            rootRect.sizeDelta = new Vector2(width, totalHeight);

            pool = new FeedEntry[capacity];
            activeEntries = new FeedEntry[capacity];
            freeEntries = new FeedEntry[capacity];
            activeCount = 0;
            freeCount = capacity;

            for (int i = 0; i < capacity; i++)
            {
                FeedEntry entry = CreatePooledEntry(i, width, height, font);
                pool[i] = entry;
                freeEntries[capacity - 1 - i] = entry;
            }

            initialized = true;
        }

        public void PushFormatted(string richText, Color accentColor)
        {
            if (string.IsNullOrWhiteSpace(richText))
            {
                return;
            }

            if (!initialized)
            {
                Initialize();
            }

            FeedEntry entry;
            if (activeCount >= pool.Length)
            {
                // 已满时挤出最旧条目（顶部 index 0）并原地复用其池化节点。
                entry = activeEntries[0];
                for (int i = 1; i < activeCount; i++)
                {
                    activeEntries[i - 1] = activeEntries[i];
                }
                activeCount--;
                activeEntries[activeCount] = null;
            }
            else
            {
                entry = freeEntries[--freeCount];
                freeEntries[freeCount] = null;
            }

            float lifetime = Mathf.Max(MinEntryLifetime, entryLifetime);
            Color resolvedAccent = ResolveAccentColor(accentColor);

            entry.Active = true;
            entry.Text = richText;
            entry.RemainingTime = lifetime;
            entry.Alpha = 1f;

            if (entry.AccentBarImage != null)
            {
                entry.AccentBarImage.color = resolvedAccent;
            }

            if (entry.MessageText != null)
            {
                entry.MessageText.text = richText;
            }

            if (entry.CanvasGroup != null)
            {
                entry.CanvasGroup.alpha = 1f;
            }

            if (entry.RootObject != null && !entry.RootObject.activeSelf)
            {
                entry.RootObject.SetActive(true);
            }

            activeEntries[activeCount++] = entry;
            RefreshEntryLayout();
        }

        public void Tick(float deltaTime)
        {
            if (!initialized || activeCount == 0)
            {
                return;
            }

            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return;
            }

            int writeIndex = 0;
            bool anyRemoved = false;

            for (int i = 0; i < activeCount; i++)
            {
                FeedEntry entry = activeEntries[i];
                entry.RemainingTime -= deltaTime;

                if (entry.RemainingTime <= 0f)
                {
                    ResetEntryVisual(entry);
                    freeEntries[freeCount++] = entry;
                    anyRemoved = true;
                    continue;
                }

                float alpha = ComputeFadeAlpha(entry.RemainingTime);
                entry.Alpha = alpha;
                if (entry.CanvasGroup != null)
                {
                    entry.CanvasGroup.alpha = alpha;
                }

                activeEntries[writeIndex++] = entry;
            }

            for (int i = writeIndex; i < activeCount; i++)
            {
                activeEntries[i] = null;
            }

            if (anyRemoved)
            {
                activeCount = writeIndex;
                RefreshEntryLayout();
            }
        }

        public void Clear()
        {
            if (!initialized || pool == null)
            {
                return;
            }

            activeCount = 0;
            freeCount = 0;

            for (int i = 0; i < pool.Length; i++)
            {
                activeEntries[i] = null;
                FeedEntry entry = pool[i];
                ResetEntryVisual(entry);
                freeEntries[pool.Length - 1 - i] = entry;
                freeCount++;
            }
        }

        /// <summary>
        /// 按显示顺序返回第 <paramref name="index"/> 条活跃播报文本。
        /// 顺序约定：index 0 为最旧（最上方）活跃条目，index ActiveEntryCount - 1 为最新（最下方）活跃条目；越界返回 null。
        /// </summary>
        public string GetEntryText(int index)
        {
            if (activeEntries == null || index < 0 || index >= activeCount)
            {
                return null;
            }

            return activeEntries[index].Text;
        }

        /// <summary>
        /// 按显示顺序返回第 <paramref name="index"/> 条活跃播报的当前透明度（0..1）；越界返回 0。
        /// </summary>
        public float GetEntryAlpha(int index)
        {
            if (activeEntries == null || index < 0 || index >= activeCount)
            {
                return 0f;
            }

            return activeEntries[index].Alpha;
        }

        private FeedEntry CreatePooledEntry(int poolIndex, float width, float height, TMP_FontAsset font)
        {
            var entryObj = new GameObject("FeedEntry_" + poolIndex, typeof(RectTransform), typeof(CanvasGroup));
            var entryRect = (RectTransform)entryObj.transform;
            entryRect.SetParent(rootRect, false);
            entryRect.anchorMin = new Vector2(1f, 1f);
            entryRect.anchorMax = new Vector2(1f, 1f);
            entryRect.pivot = new Vector2(1f, 1f);
            entryRect.sizeDelta = new Vector2(width, height);
            entryRect.anchoredPosition = Vector2.zero;

            var canvasGroup = entryObj.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            float barWidth = Mathf.Max(MinAccentBarWidth, accentBarWidth);
            var accentObj = new GameObject("AccentBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var accentRect = (RectTransform)accentObj.transform;
            accentRect.SetParent(entryRect, false);
            accentRect.anchorMin = new Vector2(1f, 0f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(1f, 0.5f);
            accentRect.offsetMin = new Vector2(-(AccentInsetRight + barWidth), AccentInsetVertical);
            accentRect.offsetMax = new Vector2(-AccentInsetRight, -AccentInsetVertical);

            var accentImage = accentObj.GetComponent<Image>();
            accentImage.color = Color.white;
            accentImage.raycastTarget = false;

            var textObj = new GameObject("MessageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(Shadow));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(entryRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(TextInsetLeft, TextInsetVertical);
            textRect.offsetMax = new Vector2(-(AccentInsetRight + barWidth + TextGapBeforeAccent), -TextInsetVertical);

            var tmp = textObj.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                tmp.font = font;
                if (font.material != null)
                {
                    tmp.fontSharedMaterial = font.material;
                }
            }
            tmp.fontStyle = FontStyles.Normal;
            tmp.fontWeight = FontWeight.Regular;
            tmp.richText = true;
            tmp.enableAutoSizing = true;
            tmp.fontSize = EntryFontSizeMax;
            tmp.fontSizeMin = EntryFontSizeMin;
            tmp.fontSizeMax = EntryFontSizeMax;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.alignment = TextAlignmentOptions.MidlineRight;
            tmp.color = textColor;
            tmp.raycastTarget = false;
            tmp.text = string.Empty;

            var shadow = textObj.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            shadow.effectDistance = new Vector2(1f, -1f);

            entryObj.SetActive(false);

            return new FeedEntry
            {
                RootObject = entryObj,
                RootRect = entryRect,
                CanvasGroup = canvasGroup,
                AccentBarImage = accentImage,
                MessageText = tmp,
                Text = null,
                RemainingTime = 0f,
                Alpha = 0f,
                Active = false
            };
        }

        private void RefreshEntryLayout()
        {
            float stepY = Mathf.Max(MinEntryHeight, entryHeight) + Mathf.Max(0f, entrySpacing);
            for (int i = 0; i < activeCount; i++)
            {
                FeedEntry entry = activeEntries[i];
                if (entry == null || entry.RootRect == null)
                {
                    continue;
                }

                entry.RootRect.anchoredPosition = new Vector2(0f, -i * stepY);
                if (entry.RootRect.GetSiblingIndex() != i)
                {
                    entry.RootRect.SetSiblingIndex(i);
                }
            }
        }

        private void ApplyFont(TMP_FontAsset font)
        {
            if (font == null || pool == null)
            {
                return;
            }

            for (int i = 0; i < pool.Length; i++)
            {
                FeedEntry entry = pool[i];
                if (entry != null && entry.MessageText != null)
                {
                    entry.MessageText.font = font;
                    if (font.material != null)
                    {
                        entry.MessageText.fontSharedMaterial = font.material;
                    }
                    entry.MessageText.fontStyle = FontStyles.Normal;
                    entry.MessageText.fontWeight = FontWeight.Regular;
                }
            }
        }

        private float ComputeFadeAlpha(float remainingTime)
        {
            if (remainingTime <= 0f)
            {
                return 0f;
            }

            float lifetime = Mathf.Max(MinEntryLifetime, entryLifetime);
            float effectiveFade = Mathf.Clamp(fadeOutDuration, 0f, lifetime);
            if (effectiveFade <= 0f || remainingTime >= effectiveFade)
            {
                return 1f;
            }

            float t = Mathf.Clamp01(remainingTime / effectiveFade);
            return t * t * (3f - 2f * t);
        }

        private static Color ResolveAccentColor(Color accentColor)
        {
            if (accentColor.a <= 0f)
            {
                accentColor.a = 1f;
            }
            return accentColor;
        }

        private static void ResetEntryVisual(FeedEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            entry.Active = false;
            entry.Text = null;
            entry.RemainingTime = 0f;
            entry.Alpha = 0f;

            if (entry.MessageText != null)
            {
                entry.MessageText.text = string.Empty;
            }

            if (entry.CanvasGroup != null)
            {
                entry.CanvasGroup.alpha = 0f;
            }

            if (entry.RootObject != null && entry.RootObject.activeSelf)
            {
                entry.RootObject.SetActive(false);
            }
        }
    }
}
