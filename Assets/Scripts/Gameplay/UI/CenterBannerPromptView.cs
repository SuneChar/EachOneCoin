using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    public sealed class CenterBannerPromptView : MonoBehaviour
    {
        private const float MinBannerDuration = 0.05f;
        private const float DefaultBannerDuration = 1.8f;

        [Header("Layout (1920x1080 Reference Canvas)")]
        [SerializeField] private Vector2 topOffset = new Vector2(0f, -94f);
        [SerializeField, Min(320f)] private float bannerWidth = 720f;
        [SerializeField, Min(48f)] private float singleLineCardHeight = 68f;
        [SerializeField, Min(64f)] private float twoLineCardHeight = 92f;
        [SerializeField, Min(24f)] private float smallIconSize = 36f;
        [SerializeField, Min(48f)] private float popIconSize = 96f;
        [SerializeField, Min(0f)] private float popIconGap = 10f;

        [Header("Timing & Queue")]
        [SerializeField, Min(0.01f)] private float fadeInDuration = 0.14f;
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.25f;
        [SerializeField, Min(1)] private int maxQueueSize = 4;
        [SerializeField, Min(0.05f)] private float iconPopDuration = 0.36f;
        [SerializeField, Min(0.5f)] private float iconPulseSpeed = 6.5f;
        [SerializeField, Range(0f, 32f)] private float enterSlideOffsetY = 8f;
        [SerializeField, Range(0f, 24f)] private float exitSlideOffsetY = 6f;

        [Header("Colors")]
        [SerializeField] private Color cardBackgroundColor = new Color(0.075f, 0.098f, 0.141f, 0.88f);
        [SerializeField] private Color titleTextColor = new Color(1f, 0.980f, 0.922f, 1f);
        [SerializeField] private Color subtitleTextColor = new Color(0.84f, 0.89f, 0.96f, 0.94f);
        [SerializeField] private Color mineWarningAccentColor = new Color(1f, 0.84f, 0.28f, 1f);
        [SerializeField] private Color coinsSpawnedAccentColor = new Color(1f, 0.82f, 0.16f, 1f);
        [SerializeField] private Color nearVictoryAccentColor = new Color(1f, 0.62f, 0.16f, 1f);
        [SerializeField] private Color playerFinishedAccentColor = new Color(1f, 0.86f, 0.24f, 1f);
        [SerializeField] private Color customAccentColor = new Color(0.76f, 0.86f, 0.98f, 1f);
        [SerializeField] private Color iconGoldenPulseTint = new Color(1f, 0.92f, 0.56f, 1f);
        [SerializeField] private Color iconGoldenGlowColor = new Color(1f, 0.85f, 0.24f, 1f);

        private readonly List<BannerPromptRequest> pendingQueue = new List<BannerPromptRequest>(4);

        private HudProceduralIcons icons;
        private RectTransform rootRect;
        private CanvasGroup canvasGroup;
        private RectTransform contentRoot;
        private RectTransform bannerCardRect;
        private Image cardBackgroundImage;
        private Image accentGlowImage;
        private Image accentTopBarImage;
        private Image inlineIconImage;
        private TMP_Text titleText;
        private TMP_Text subtitleText;
        private RectTransform popIconContainer;
        private Image popIconGlowImage;
        private Image popIconImage;

        private bool initialized;
        private bool hasActiveBanner;
        private BannerPromptRequest activeRequest;
        private float activeElapsed;
        private float activeDuration;
        private float activeFadeIn;
        private float activeFadeOut;
        private float fadeInStartAlpha;
        private bool replacedWhileVisible;

        public Vector2 TopOffset => topOffset;
        public bool IsVisible => hasActiveBanner;
        public BannerPromptCategory ActiveCategory => hasActiveBanner ? activeRequest.Category : BannerPromptCategory.Custom;
        public string ActiveTitle => hasActiveBanner ? activeRequest.Title : string.Empty;
        public string ActiveSubtitle => hasActiveBanner ? activeRequest.Subtitle : string.Empty;
        public bool IsIconPopActive => hasActiveBanner && activeRequest.EmphasizeIconPop && activeRequest.Icon != null;
        public int QueuedCount => pendingQueue.Count;

        public void Initialize(TMP_FontAsset font = null)
        {
            if (initialized)
            {
                if (font != null) ApplyFont(font);
                return;
            }

            rootRect = transform as RectTransform;
            if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = topOffset;
            rootRect.sizeDelta = new Vector2(bannerWidth, twoLineCardHeight + popIconGap + popIconSize);

            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (icons == null) icons = new HudProceduralIcons();
            Sprite panelSprite = icons.GetRoundedPanelSprite();

            contentRoot = CreateContainer(rootRect, "ContentRoot");
            SetStretch(contentRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            contentRoot.pivot = new Vector2(0.5f, 1f);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.localScale = Vector3.one;

            bannerCardRect = CreateContainer(contentRoot, "BannerCard");
            bannerCardRect.anchorMin = new Vector2(0.5f, 1f);
            bannerCardRect.anchorMax = new Vector2(0.5f, 1f);
            bannerCardRect.pivot = new Vector2(0.5f, 1f);
            bannerCardRect.anchoredPosition = Vector2.zero;
            bannerCardRect.sizeDelta = new Vector2(bannerWidth, singleLineCardHeight);

            cardBackgroundImage = CreatePanelImage(bannerCardRect, "CardBackground", panelSprite, cardBackgroundColor);
            SetStretch(cardBackgroundImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            accentGlowImage = CreatePanelImage(
                bannerCardRect,
                "AccentGlow",
                panelSprite,
                new Color(mineWarningAccentColor.r, mineWarningAccentColor.g, mineWarningAccentColor.b, 0.14f));
            SetStretch(accentGlowImage.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            accentTopBarImage = CreateSolidImage(bannerCardRect, "AccentTopBar", mineWarningAccentColor);
            accentTopBarImage.rectTransform.anchorMin = new Vector2(0f, 1f);
            accentTopBarImage.rectTransform.anchorMax = new Vector2(1f, 1f);
            accentTopBarImage.rectTransform.pivot = new Vector2(0.5f, 1f);
            accentTopBarImage.rectTransform.offsetMin = new Vector2(16f, -3.5f);
            accentTopBarImage.rectTransform.offsetMax = new Vector2(-16f, 0f);

            inlineIconImage = CreateIconImage(bannerCardRect, "InlineIcon", null, new Vector2(smallIconSize, smallIconSize), Color.white);
            inlineIconImage.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            inlineIconImage.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            inlineIconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            inlineIconImage.rectTransform.anchoredPosition = new Vector2(22f + smallIconSize * 0.5f, 0f);
            inlineIconImage.enabled = false;

            titleText = CreateText(
                bannerCardRect,
                "TitleText",
                font,
                30f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                titleTextColor,
                true,
                18f,
                32f);

            subtitleText = CreateText(
                bannerCardRect,
                "SubtitleText",
                font,
                18f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                subtitleTextColor,
                true,
                13f,
                19f);
            subtitleText.enabled = false;

            popIconContainer = CreateContainer(contentRoot, "PopIconContainer");
            popIconContainer.anchorMin = new Vector2(0.5f, 1f);
            popIconContainer.anchorMax = new Vector2(0.5f, 1f);
            popIconContainer.pivot = new Vector2(0.5f, 0.5f);
            popIconContainer.sizeDelta = new Vector2(popIconSize, popIconSize);
            popIconContainer.anchoredPosition = new Vector2(0f, -(singleLineCardHeight + popIconGap + popIconSize * 0.5f));
            popIconContainer.localScale = Vector3.one;

            popIconGlowImage = CreateIconImage(
                popIconContainer,
                "PopIconGlow",
                null,
                new Vector2(popIconSize, popIconSize),
                new Color(iconGoldenGlowColor.r, iconGoldenGlowColor.g, iconGoldenGlowColor.b, 0.35f));
            popIconGlowImage.enabled = false;

            popIconImage = CreateIconImage(
                popIconContainer,
                "PopIcon",
                null,
                new Vector2(popIconSize, popIconSize),
                Color.white);
            popIconImage.enabled = false;

            popIconContainer.gameObject.SetActive(false);

            initialized = true;
            Clear();
        }

        public void ShowBanner(BannerPromptRequest request)
        {
            if (!initialized) Initialize();

            request = NormalizeRequest(request);
            if (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Subtitle) && request.Icon == null)
            {
                return;
            }

            if (!hasActiveBanner)
            {
                StartBanner(request, false);
                return;
            }

            if (ShouldPreemptActive(request))
            {
                StartBanner(request, true);
                return;
            }

            EnqueueRequest(request);
        }

        public void ShowMineWarning(string mineDirectionName)
        {
            if (!initialized) Initialize();

            string trimmed = string.IsNullOrWhiteSpace(mineDirectionName) ? null : mineDirectionName.Trim();
            string title = string.IsNullOrEmpty(trimmed) ? "金币即将刷新" : trimmed + "即将刷新金币";

            var request = new BannerPromptRequest
            {
                Category = BannerPromptCategory.MineWarning,
                Title = title,
                Subtitle = string.Empty,
                Icon = null,
                AccentColor = mineWarningAccentColor,
                Duration = 1.6f,
                Priority = 10,
                EmphasizeIconPop = false
            };

            ShowBanner(request);
        }

        public void ShowCoinsSpawnedPop(string mineDirectionName, int count)
        {
            if (!initialized) Initialize();

            string trimmed = string.IsNullOrWhiteSpace(mineDirectionName) ? null : mineDirectionName.Trim();
            string title = string.IsNullOrEmpty(trimmed) ? "已刷新金币！" : trimmed + "已刷新金币！";

            var request = new BannerPromptRequest
            {
                Category = BannerPromptCategory.CoinsSpawned,
                Title = title,
                Subtitle = string.Empty,
                Icon = icons != null ? icons.GetCoinSprite() : null,
                AccentColor = coinsSpawnedAccentColor,
                Duration = 1.25f,
                Priority = 15,
                EmphasizeIconPop = true
            };

            ShowBanner(request);
        }

        public void ShowPlayerNearVictory(string playerName, int coins, int coinsToWin, Color playerColor)
        {
            if (!initialized) Initialize();

            string safeName = string.IsNullOrWhiteSpace(playerName) ? "玩家" : playerName.Trim();
            int safeCoins = coins < 0 ? 0 : coins;
            int safeTarget = coinsToWin < 0 ? 0 : coinsToWin;

            string title = string.Format(
                CultureInfo.InvariantCulture,
                "{0} 已获得 {1} 枚金币，即将获胜！",
                safeName,
                safeCoins);
            string subtitle = string.Format(
                CultureInfo.InvariantCulture,
                "目标：{0} 枚",
                safeTarget);

            var request = new BannerPromptRequest
            {
                Category = BannerPromptCategory.NearVictory,
                Title = title,
                Subtitle = subtitle,
                Icon = icons != null ? icons.GetCrownSprite() : null,
                AccentColor = EnsureVisibleAccent(playerColor, nearVictoryAccentColor),
                Duration = 2.4f,
                Priority = 30,
                EmphasizeIconPop = false
            };

            ShowBanner(request);
        }

        public void ShowPlayerFinished(string playerName, int finishOrder, Color playerColor)
        {
            if (!initialized) Initialize();

            string safeName = string.IsNullOrWhiteSpace(playerName) ? "玩家" : playerName.Trim();
            int clampedOrder = finishOrder < 1 ? 1 : finishOrder;

            string title = string.Format(
                CultureInfo.InvariantCulture,
                "{0} 集齐金币，获得第 {1} 名！",
                safeName,
                clampedOrder);

            var request = new BannerPromptRequest
            {
                Category = BannerPromptCategory.PlayerFinished,
                Title = title,
                Subtitle = string.Empty,
                Icon = icons != null ? icons.GetMedalSprite(clampedOrder) : null,
                AccentColor = EnsureVisibleAccent(playerColor, playerFinishedAccentColor),
                Duration = 2.4f,
                Priority = 25,
                EmphasizeIconPop = true
            };

            ShowBanner(request);
        }

        public void ShowCustomBanner(
            string title,
            string subtitle = null,
            Sprite icon = null,
            bool emphasizeIconPop = false,
            float duration = 1.8f,
            int priority = 0,
            Color? accentColor = null)
        {
            if (!initialized) Initialize();

            var request = new BannerPromptRequest
            {
                Category = BannerPromptCategory.Custom,
                Title = title ?? string.Empty,
                Subtitle = subtitle ?? string.Empty,
                Icon = icon,
                AccentColor = accentColor.HasValue
                    ? EnsureVisibleAccent(accentColor.Value, customAccentColor)
                    : customAccentColor,
                Duration = duration > 0f ? duration : DefaultBannerDuration,
                Priority = priority,
                EmphasizeIconPop = emphasizeIconPop
            };

            ShowBanner(request);
        }

        public void Tick(float deltaTime)
        {
            if (!initialized || !hasActiveBanner) return;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;

            float remainingStep = deltaTime;
            while (hasActiveBanner)
            {
                float timeLeft = activeDuration - activeElapsed;
                if (remainingStep < timeLeft)
                {
                    activeElapsed += remainingStep;
                    UpdateActiveVisuals();
                    break;
                }

                remainingStep -= timeLeft;
                if (!TryStartNextQueuedBanner())
                {
                    HideImmediate();
                    break;
                }

                if (remainingStep <= 0f)
                {
                    break;
                }
            }
        }

        public void Clear()
        {
            pendingQueue.Clear();
            HideImmediate();
        }

        private void OnDestroy()
        {
            pendingQueue.Clear();
            if (icons != null)
            {
                icons.Dispose();
                icons = null;
            }
        }

        private BannerPromptRequest NormalizeRequest(BannerPromptRequest request)
        {
            request.Title = request.Title ?? string.Empty;
            request.Subtitle = request.Subtitle ?? string.Empty;

            if (float.IsNaN(request.Duration) || float.IsInfinity(request.Duration) || request.Duration <= 0f)
            {
                request.Duration = DefaultBannerDuration;
            }
            else if (request.Duration < MinBannerDuration)
            {
                request.Duration = MinBannerDuration;
            }

            request.AccentColor = EnsureVisibleAccent(request.AccentColor, ResolveCategoryFallbackAccent(request.Category));
            return request;
        }

        private bool ShouldPreemptActive(BannerPromptRequest incoming)
        {
            if (incoming.Priority > activeRequest.Priority)
            {
                return true;
            }

            if (activeRequest.Category == BannerPromptCategory.MineWarning &&
                incoming.Category == BannerPromptCategory.CoinsSpawned)
            {
                return true;
            }

            return false;
        }

        private void EnqueueRequest(BannerPromptRequest request)
        {
            int cap = Mathf.Max(1, maxQueueSize);
            if (pendingQueue.Count >= cap)
            {
                int lowestPriority = pendingQueue[pendingQueue.Count - 1].Priority;
                if (request.Priority <= lowestPriority)
                {
                    return;
                }

                int evictIndex = pendingQueue.Count - 1;
                for (int i = 0; i < pendingQueue.Count; i++)
                {
                    if (pendingQueue[i].Priority == lowestPriority)
                    {
                        evictIndex = i;
                        break;
                    }
                }
                pendingQueue.RemoveAt(evictIndex);
            }

            int insertIndex = pendingQueue.Count;
            for (int i = 0; i < pendingQueue.Count; i++)
            {
                if (request.Priority > pendingQueue[i].Priority)
                {
                    insertIndex = i;
                    break;
                }
            }
            pendingQueue.Insert(insertIndex, request);
        }

        private bool TryStartNextQueuedBanner()
        {
            if (pendingQueue.Count == 0) return false;

            BannerPromptRequest next = pendingQueue[0];
            pendingQueue.RemoveAt(0);
            StartBanner(next, false);
            return true;
        }

        private void StartBanner(BannerPromptRequest request, bool smoothReplace)
        {
            float currentAlpha = canvasGroup != null ? canvasGroup.alpha : 0f;

            hasActiveBanner = true;
            activeRequest = request;
            activeElapsed = 0f;
            activeDuration = Mathf.Max(MinBannerDuration, request.Duration);
            activeFadeIn = Mathf.Min(Mathf.Max(0.01f, fadeInDuration), activeDuration * 0.35f);
            activeFadeOut = Mathf.Min(Mathf.Max(0.01f, fadeOutDuration), activeDuration * 0.45f);
            replacedWhileVisible = smoothReplace && currentAlpha > 0.01f;
            fadeInStartAlpha = replacedWhileVisible ? currentAlpha : 0f;

            ApplyRequestLayoutAndContent(request);
            UpdateActiveVisuals();
        }

        private void ApplyRequestLayoutAndContent(BannerPromptRequest request)
        {
            bool hasSubtitle = !string.IsNullOrEmpty(request.Subtitle);
            bool showPopIcon = request.EmphasizeIconPop && request.Icon != null;
            bool showInlineIcon = !request.EmphasizeIconPop && request.Icon != null;

            float cardHeight = hasSubtitle ? twoLineCardHeight : singleLineCardHeight;
            if (bannerCardRect != null)
            {
                bannerCardRect.sizeDelta = new Vector2(bannerWidth, cardHeight);
            }

            Color accent = request.AccentColor;
            if (accentTopBarImage != null)
            {
                accentTopBarImage.color = new Color(accent.r, accent.g, accent.b, 0.96f);
            }
            if (accentGlowImage != null)
            {
                accentGlowImage.color = new Color(accent.r, accent.g, accent.b, 0.14f);
            }

            if (inlineIconImage != null)
            {
                inlineIconImage.sprite = showInlineIcon ? request.Icon : null;
                inlineIconImage.enabled = showInlineIcon;
                inlineIconImage.color = Color.white;
            }

            float horizontalInset = showInlineIcon ? (22f + smallIconSize + 14f) : 28f;

            if (titleText != null)
            {
                titleText.text = request.Title;
                titleText.color = titleTextColor;

                RectTransform titleRect = titleText.rectTransform;
                if (hasSubtitle)
                {
                    titleText.fontSizeMax = 30f;
                    SetStretch(
                        titleRect,
                        new Vector2(0f, 0.42f),
                        Vector2.one,
                        new Vector2(horizontalInset, 2f),
                        new Vector2(-horizontalInset, -10f));
                }
                else
                {
                    titleText.fontSizeMax = 32f;
                    SetStretch(
                        titleRect,
                        Vector2.zero,
                        Vector2.one,
                        new Vector2(horizontalInset, 8f),
                        new Vector2(-horizontalInset, -8f));
                }
            }

            if (subtitleText != null)
            {
                subtitleText.text = hasSubtitle ? request.Subtitle : string.Empty;
                subtitleText.enabled = hasSubtitle;
                subtitleText.color = subtitleTextColor;

                if (hasSubtitle)
                {
                    SetStretch(
                        subtitleText.rectTransform,
                        Vector2.zero,
                        new Vector2(1f, 0.44f),
                        new Vector2(horizontalInset, 8f),
                        new Vector2(-horizontalInset, -2f));
                }
            }

            if (popIconContainer != null)
            {
                popIconContainer.anchoredPosition = new Vector2(0f, -(cardHeight + popIconGap + popIconSize * 0.5f));
                popIconContainer.gameObject.SetActive(showPopIcon);
            }

            if (popIconImage != null)
            {
                popIconImage.sprite = showPopIcon ? request.Icon : null;
                popIconImage.enabled = showPopIcon;
                popIconImage.color = Color.white;
            }

            if (popIconGlowImage != null)
            {
                popIconGlowImage.sprite = showPopIcon ? request.Icon : null;
                popIconGlowImage.enabled = showPopIcon;
            }
        }

        private void UpdateActiveVisuals()
        {
            if (!hasActiveBanner) return;

            float fadeOutStart = Mathf.Max(activeFadeIn, activeDuration - activeFadeOut);
            float alpha;
            float offsetY = 0f;
            float cardScale = 1f;

            if (activeElapsed < activeFadeIn)
            {
                float t = Mathf.Clamp01(activeElapsed / activeFadeIn);
                float inv = 1f - t;
                float easeOut = 1f - inv * inv * inv;
                alpha = Mathf.Lerp(fadeInStartAlpha, 1f, easeOut);
                if (replacedWhileVisible)
                {
                    cardScale = Mathf.Lerp(1.045f, 1f, easeOut);
                }
                else
                {
                    offsetY = Mathf.Lerp(enterSlideOffsetY, 0f, easeOut);
                }
            }
            else if (activeElapsed < fadeOutStart)
            {
                alpha = 1f;
            }
            else
            {
                float outSpan = Mathf.Max(0.0001f, activeDuration - fadeOutStart);
                float t = Mathf.Clamp01((activeElapsed - fadeOutStart) / outSpan);
                float easeIn = t * t;
                alpha = Mathf.Lerp(1f, 0f, easeIn);
                offsetY = Mathf.Lerp(0f, exitSlideOffsetY, easeIn);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(alpha);
            }

            if (contentRoot != null)
            {
                contentRoot.anchoredPosition = new Vector2(0f, offsetY);
                contentRoot.localScale = new Vector3(cardScale, cardScale, 1f);
            }

            if (IsIconPopActive)
            {
                UpdatePopIconVisual(activeElapsed);
            }
        }

        private void UpdatePopIconVisual(float elapsed)
        {
            float popDur = Mathf.Max(0.05f, iconPopDuration);
            float peakTime = popDur * 0.45f;
            float undershootTime = popDur * 0.78f;

            float scale;
            if (elapsed < peakTime)
            {
                float u = Mathf.Clamp01(elapsed / peakTime);
                float inv = 1f - u;
                float easeOut = 1f - inv * inv * inv;
                scale = Mathf.Lerp(0.35f, 1.22f, easeOut);
            }
            else if (elapsed < undershootTime)
            {
                float u = Mathf.Clamp01((elapsed - peakTime) / Mathf.Max(0.0001f, undershootTime - peakTime));
                float smooth = u * u * (3f - 2f * u);
                scale = Mathf.Lerp(1.22f, 0.96f, smooth);
            }
            else if (elapsed < popDur)
            {
                float u = Mathf.Clamp01((elapsed - undershootTime) / Mathf.Max(0.0001f, popDur - undershootTime));
                float smooth = u * u * (3f - 2f * u);
                scale = Mathf.Lerp(0.96f, 1f, smooth);
            }
            else
            {
                float breathe = 0.5f - 0.5f * Mathf.Cos((elapsed - popDur) * iconPulseSpeed);
                scale = 1f + 0.035f * breathe;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * iconPulseSpeed);

            if (popIconContainer != null)
            {
                popIconContainer.localScale = new Vector3(scale, scale, 1f);
            }

            if (popIconImage != null)
            {
                popIconImage.color = Color.Lerp(Color.white, iconGoldenPulseTint, pulse * 0.24f);
            }

            if (popIconGlowImage != null)
            {
                float glowScale = 1.14f + 0.08f * pulse;
                popIconGlowImage.rectTransform.localScale = new Vector3(glowScale, glowScale, 1f);
                float glowAlpha = Mathf.Lerp(0.22f, 0.48f, pulse);
                popIconGlowImage.color = new Color(iconGoldenGlowColor.r, iconGoldenGlowColor.g, iconGoldenGlowColor.b, glowAlpha);
            }
        }

        private void HideImmediate()
        {
            hasActiveBanner = false;
            activeRequest = default(BannerPromptRequest);
            activeElapsed = 0f;
            activeDuration = 0f;
            activeFadeIn = 0f;
            activeFadeOut = 0f;
            fadeInStartAlpha = 0f;
            replacedWhileVisible = false;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (contentRoot != null)
            {
                contentRoot.anchoredPosition = Vector2.zero;
                contentRoot.localScale = Vector3.one;
            }

            if (titleText != null)
            {
                titleText.text = string.Empty;
            }

            if (subtitleText != null)
            {
                subtitleText.text = string.Empty;
                subtitleText.enabled = false;
            }

            if (inlineIconImage != null)
            {
                inlineIconImage.sprite = null;
                inlineIconImage.enabled = false;
            }

            if (popIconContainer != null)
            {
                popIconContainer.localScale = Vector3.one;
                popIconContainer.gameObject.SetActive(false);
            }

            if (popIconImage != null)
            {
                popIconImage.sprite = null;
                popIconImage.enabled = false;
                popIconImage.color = Color.white;
            }

            if (popIconGlowImage != null)
            {
                popIconGlowImage.sprite = null;
                popIconGlowImage.enabled = false;
                popIconGlowImage.rectTransform.localScale = Vector3.one;
            }
        }

        private void ApplyFont(TMP_FontAsset font)
        {
            if (font == null) return;
            ApplyFontToText(titleText, font);
            ApplyFontToText(subtitleText, font);
        }

        private static void ApplyFontToText(TMP_Text text, TMP_FontAsset font)
        {
            if (text == null || font == null) return;
            text.font = font;
            if (font.material != null) text.fontSharedMaterial = font.material;
            text.fontStyle = FontStyles.Normal;
            text.fontWeight = FontWeight.Regular;
        }

        private Color ResolveCategoryFallbackAccent(BannerPromptCategory category)
        {
            switch (category)
            {
                case BannerPromptCategory.MineWarning:
                    return mineWarningAccentColor;
                case BannerPromptCategory.CoinsSpawned:
                    return coinsSpawnedAccentColor;
                case BannerPromptCategory.NearVictory:
                    return nearVictoryAccentColor;
                case BannerPromptCategory.PlayerFinished:
                    return playerFinishedAccentColor;
                default:
                    return customAccentColor;
            }
        }

        private static Color EnsureVisibleAccent(Color candidate, Color fallback)
        {
            if (candidate.a <= 0.001f)
            {
                return fallback;
            }

            return new Color(candidate.r, candidate.g, candidate.b, 1f);
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
            img.type = sprite != null && sprite.border.sqrMagnitude > 0.01f ? Image.Type.Sliced : Image.Type.Simple;
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
            text.richText = true;
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
