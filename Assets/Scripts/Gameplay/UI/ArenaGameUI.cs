using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    [DefaultExecutionOrder(-100)]
    public sealed class ArenaGameUI : MonoBehaviour
    {
        private static readonly string[] FallbackMineNames = { "南侧", "北侧", "西侧", "东侧" };

        [SerializeField] private MatchCoordinator match;
        [SerializeField] private GameAudio gameAudio;
        [SerializeField] private GameObject hudPanel, pausePanel, settingsPanel, resultPanel;
        [SerializeField] private TMP_Text timeText, coinText, healthText, resultText, resultCoinsText, participantText, feedbackText, attackPromptText, volumeValueText;
        [SerializeField] private Image healthFill;
        [SerializeField] private HealthHeartsView healthHeartsView;
        [SerializeField] private Image staminaFill;
        [SerializeField] private TMP_Text staminaText;
        [SerializeField] private ArcStaminaBarView arcStaminaBarView;
        [SerializeField] private ArenaMinimapView minimapView;
        [SerializeField] private LocalPlayerPortraitView localPortraitView;
        [SerializeField] private ParticipantHudStripView participantStripView;
        [SerializeField] private LocalCoinCounterView localCoinCounterView;
        [SerializeField] private CenterBannerPromptView centerBannerView;
        [SerializeField] private MatchClockAndMineView clockAndMineView;
        [SerializeField] private MatchEventFeedView eventFeedView;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle promptsToggle;
        [SerializeField] private ControlPromptImages promptImages;
        [SerializeField, Min(0.05f)] private float hitFlashDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] private float hitFrameOpacity = 0.65f;
        [SerializeField, Min(1f)] private float hitFrameThickness = 24f;
        private readonly Image[] hitFrame = new Image[4];
        private readonly ParticipantHudSnapshot[] snapshotBuffer = new ParticipantHudSnapshot[4];
        private IAvatarAppearanceProvider avatarProvider = new DefaultAvatarAppearanceProvider();
        private Func<int, string> participantNameResolver;
        private Func<int, Color> participantColorResolver;
        private Func<int, string> mineNameResolver;
        private float hitFlashRemaining;
        private InputDeviceTracker inputDevice;
        private bool usingGamepad, showPrompts, settingsOpen, resultShown;
        private int lastTime = -1;
        private readonly int[] lastCoins = { -1, -1, -1, -1 };
        private readonly int[] lastFinishOrder = { -1, -1, -1, -1 };
        private readonly int[] lastHealth = { -1, -1, -1, -1 };
        private readonly int[] lastMaxHealth = { -1, -1, -1, -1 };
        private readonly bool[] lastParticipating = new bool[4];
        private readonly ParticipantStatus[] lastStatus = new ParticipantStatus[4];
        private int lastLocalSlot = -1;
        private int lastNetworkSyncedHealth = -1;
        private int lastNetworkSyncedMaxHealth = -1;
        private bool firstRefresh = true;
        private bool avatarAppearanceDirty;
        private bool lastUpcomingWarning;
        private int lastUpcomingMine = int.MaxValue;
        private bool networkMenuOpen;
        private bool heartsInitialized;
        private bool legacyHealthHidden;
        private bool staminaBarInitialized;
        private bool legacyStaminaHidden;
        private bool legacyHudTextsHidden;
        private bool minimapInitialized;
        private bool localPortraitInitialized;
        private bool participantStripInitialized;
        private bool localCoinCounterInitialized;
        private bool centerBannerInitialized;
        private bool clockAndMineInitialized;
        private bool eventFeedInitialized;
        private bool networkStatusBannerInitialized;
        private bool leftHudAdjusted;
        private bool networkHealthSynced;
        private bool networkStaminaSynced;
        private GameObject networkStatusBannerRoot;
        private TMP_Text networkStatusBannerText;
        private HudProceduralIcons statusBannerIcons;
        private bool networkStatusStateInitialized;
        private bool lastBannerResuming;
        private bool lastBannerSpectating;
        private int lastBannerSpectateSlot = -2;
        private ParticipantStatus lastBannerLocalStatus;
        private MatchStatus lastBannerMatchStatus;
        private bool lastBannerResultShown;
        private string lastNetworkStatusMessage = string.Empty;
        private Camera cachedMainCamera;
        public TMPro.TMP_FontAsset NameplateFont
        {
            get
            {
                if (timeText != null && timeText.font != null) return timeText.font;
                if (coinText != null && coinText.font != null) return coinText.font;
                if (participantText != null && participantText.font != null) return participantText.font;
                if (feedbackText != null && feedbackText.font != null) return feedbackText.font;
                if (attackPromptText != null && attackPromptText.font != null) return attackPromptText.font;
                if (resultText != null && resultText.font != null) return resultText.font;
                return null;
            }
        }
        public bool NetworkMenuOpen => networkMenuOpen || settingsOpen;

        public void SetAvatarAppearanceProvider(IAvatarAppearanceProvider provider)
        {
            IAvatarAppearanceProvider next = provider ?? new DefaultAvatarAppearanceProvider();
            if (ReferenceEquals(avatarProvider, next)) return;
            if (avatarProvider != null) avatarProvider.AppearanceChanged -= OnAvatarAppearanceChanged;
            avatarProvider = next;
            if (isActiveAndEnabled && avatarProvider != null) avatarProvider.AppearanceChanged += OnAvatarAppearanceChanged;
            if (localPortraitView != null) localPortraitView.SetAppearanceProvider(avatarProvider);
            avatarAppearanceDirty = true;
        }

        private void Awake()
        {
            inputDevice = GetComponent<InputDeviceTracker>();
            if (inputDevice != null) { usingGamepad = inputDevice.UsingGamepad; inputDevice.DeviceChanged += OnDeviceChanged; }
            showPrompts = PlayerPrefs.GetInt("GameUI.ShowPrompts", 1) != 0;
            if (feedbackText != null) { feedbackText.enableAutoSizing = true; feedbackText.fontSizeMin = 12f; feedbackText.fontSizeMax = 22f; }
            if (attackPromptText != null)
            {
                var rect = attackPromptText.rectTransform;
                rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 300f), Mathf.Max(rect.sizeDelta.y, 60f));
                rect.anchoredPosition += new Vector2(0f, 33f);
            }
            if (promptsToggle != null) promptsToggle.SetIsOnWithoutNotify(showPrompts);
            CreateHitFrame();
            HideLegacyHealthUI();
            legacyHealthHidden = true;
            HideLegacyStaminaUI();
            legacyStaminaHidden = true;
            HideLegacyHudTexts();
            legacyHudTextsHidden = true;
            AdjustPauseButtonLayout();
            RefreshPanels();
        }

        private void OnEnable()
        {
            if (match != null)
            {
                match.ObstacleHit += OnObstacleHit;
                match.AttackHit += OnAttackHit;
                match.FeedEventRaised += OnFeedEventRaised;
            }
            if (avatarProvider != null)
            {
                avatarProvider.AppearanceChanged += OnAvatarAppearanceChanged;
            }
        }

        private void OnDisable()
        {
            if (match != null)
            {
                match.ObstacleHit -= OnObstacleHit;
                match.AttackHit -= OnAttackHit;
                match.FeedEventRaised -= OnFeedEventRaised;
            }
            if (avatarProvider != null)
            {
                avatarProvider.AppearanceChanged -= OnAvatarAppearanceChanged;
            }
        }

        private void OnDestroy()
        {
            if (inputDevice != null) inputDevice.DeviceChanged -= OnDeviceChanged;
            if (avatarProvider != null) avatarProvider.AppearanceChanged -= OnAvatarAppearanceChanged;
            if (statusBannerIcons != null)
            {
                statusBannerIcons.Dispose();
                statusBannerIcons = null;
            }
        }

        private void OnAvatarAppearanceChanged(int slotId)
        {
            avatarAppearanceDirty = true;
        }

        private void OnObstacleHit(int id)
        {
            if (!IsLocalParticipant(id)) return;
            hitFlashRemaining = Mathf.Max(0.05f, hitFlashDuration);
            UpdateHitFrame();
            if (localPortraitView != null) localPortraitView.TriggerHitFlash();
        }

        private void OnAttackHit(int id)
        {
            if (!IsLocalParticipant(id)) return;
            hitFlashRemaining = Mathf.Max(0.05f, hitFlashDuration);
            UpdateHitFrame();
            if (localPortraitView != null) localPortraitView.TriggerHitFlash();
        }

        private void OnFeedEventRaised(MatchFeedEvent evt)
        {
            if (resultShown) return;
            if (match != null && match.IsNetworkMovementTest)
            {
                var network = Mirror.NetworkManager.singleton as EachOneCoin.Networking.LanNetworkManager;
                if (network != null && network.ClientResuming) return;
            }

            EnsureEventFeed();
            EnsureCenterBanner();

            if (eventFeedView != null)
            {
                string formatted = MatchFeedEvent.Format(
                    evt,
                    GetParticipantNameResolver(),
                    GetParticipantColorResolver(),
                    GetMineNameResolver());
                Color accentColor = MatchFeedEvent.ResolveAccentColor(evt, GetParticipantColorResolver());
                eventFeedView.PushFormatted(formatted, accentColor);
            }

            if (centerBannerView != null)
            {
                switch (evt.Type)
                {
                    case MatchFeedEventType.CoinSpawned:
                        centerBannerView.ShowCoinsSpawnedPop(ResolveMineName(evt.MineIndex), evt.Value);
                        break;

                    case MatchFeedEventType.NearVictory:
                        int coinsToWin = match != null && match.Model != null ? match.Model.CoinsToWin : 10;
                        centerBannerView.ShowPlayerNearVictory(
                            ResolveParticipantName(evt.PrimarySlot),
                            evt.Value,
                            coinsToWin,
                            ResolveParticipantColor(evt.PrimarySlot));
                        break;

                    case MatchFeedEventType.PlayerWon:
                        centerBannerView.ShowPlayerFinished(
                            ResolveParticipantName(evt.PrimarySlot),
                            evt.Value,
                            ResolveParticipantColor(evt.PrimarySlot));
                        break;
                }
            }
        }

        private bool IsLocalParticipant(int id)
        {
            if (match == null || !match.IsNetworkMovementTest) return id == 0;
            var network = Mirror.NetworkManager.singleton as EachOneCoin.Networking.LanNetworkManager;
            return network != null && id == network.LocalSlot;
        }

        private void CreateHitFrame()
        {
            var frame = new GameObject("Obstacle Hit Frame", typeof(RectTransform));
            var rect = (RectTransform)frame.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            for (int i = 0; i < hitFrame.Length; i++)
            {
                var edge = new GameObject("Edge " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var edgeRect = (RectTransform)edge.transform;
                edgeRect.SetParent(rect, false);
                if (i == 0) { edgeRect.anchorMin = new Vector2(0f, 1f); edgeRect.anchorMax = Vector2.one; edgeRect.pivot = new Vector2(0.5f, 1f); edgeRect.sizeDelta = new Vector2(0f, hitFrameThickness); }
                else if (i == 1) { edgeRect.anchorMin = Vector2.zero; edgeRect.anchorMax = new Vector2(1f, 0f); edgeRect.pivot = new Vector2(0.5f, 0f); edgeRect.sizeDelta = new Vector2(0f, hitFrameThickness); }
                else if (i == 2) { edgeRect.anchorMin = Vector2.zero; edgeRect.anchorMax = new Vector2(0f, 1f); edgeRect.pivot = new Vector2(0f, 0.5f); edgeRect.sizeDelta = new Vector2(hitFrameThickness, 0f); }
                else { edgeRect.anchorMin = new Vector2(1f, 0f); edgeRect.anchorMax = Vector2.one; edgeRect.pivot = new Vector2(1f, 0.5f); edgeRect.sizeDelta = new Vector2(hitFrameThickness, 0f); }
                edgeRect.anchoredPosition = Vector2.zero;
                hitFrame[i] = edge.GetComponent<Image>();
                hitFrame[i].raycastTarget = false;
            }
            UpdateHitFrame();
        }

        private void UpdateHitFrame()
        {
            float alpha = hitFrameOpacity * Mathf.Clamp01(hitFlashRemaining / Mathf.Max(0.05f, hitFlashDuration));
            foreach (var edge in hitFrame) if (edge != null) edge.color = new Color(1f, 0f, 0f, alpha);
        }
        private void OnDeviceChanged(bool gamepad) { usingGamepad = gamepad; RefreshPanels(); }

        private void Update()
        {
            if (match == null || match.Model == null) return;
            if (match.IsNetworkMovementTest)
            {
                if (hitFlashRemaining > 0f) { hitFlashRemaining = Mathf.Max(0f, hitFlashRemaining - Time.deltaTime); UpdateHitFrame(); }
                UpdateNetworkUI();
                return;
            }
            if (Application.isFocused && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || ((settingsOpen || match.IsPaused) && Input.GetKeyDown(KeyCode.JoystickButton1))))
            {
                if (settingsOpen) CloseSettings();
                else if (match.IsPaused) Resume();
                else Pause();
            }
            var model = match.Model;
            EnsureHudViews(false);
            if (minimapView != null && (!match.IsPaused || firstRefresh))
            {
                byte visibleMask = 0;
                for (int i = 0; i < model.Participants.Count; i++)
                {
                    var participant = model.Participants[i];
                    if (participant.IsParticipating && participant.Status == ParticipantStatus.Active)
                        visibleMask |= (byte)(1 << i);
                }
                minimapView.Refresh(0, visibleMask, Time.unscaledTime);
            }
            if (arcStaminaBarView != null && match.Stamina != null)
            {
                Transform localTarget = match.Characters != null && match.Characters.Length > 0 && match.Characters[0] != null
                    ? match.Characters[0].transform
                    : null;
                arcStaminaBarView.SetTarget(localTarget);
                bool canShowStamina = !match.IsPaused && model.Status == MatchStatus.Playing && model.Participants[0].Status == ParticipantStatus.Active;
                arcStaminaBarView.SetGameplayVisible(canShowStamina);
                arcStaminaBarView.SetStamina(match.Stamina.Current, match.Stamina.Maximum, match.Stamina.IsExhausted, false, firstRefresh);
                if (!match.IsPaused) arcStaminaBarView.TickVisual(Time.deltaTime, Time.unscaledTime);
            }
            if (!match.IsPaused)
            {
                if (hitFlashRemaining > 0f) { hitFlashRemaining = Mathf.Max(0f, hitFlashRemaining - Time.deltaTime); UpdateHitFrame(); }
                if (healthHeartsView != null) healthHeartsView.Tick(Time.deltaTime);
                TickHudViews(Time.deltaTime);
            }
            if (model.Status == MatchStatus.Finished && !resultShown)
            {
                resultShown = true;
                if (centerBannerView != null) centerBannerView.Clear();
                var local = model.Participants[0];
                if (resultText != null) resultText.text = local.Status == ParticipantStatus.Won ? "游戏胜利" : "游戏结束";
                if (resultCoinsText != null) resultCoinsText.text = $"金币：{local.Coins} / {model.CoinsToWin}";
                if (gameAudio != null) { if (local.Status == ParticipantStatus.Won) gameAudio.PlayWin(); else gameAudio.PlayLose(); }
                RefreshPanels();
            }
            int time = Mathf.CeilToInt(model.RemainingTime);
            if (time != lastTime)
            {
                if (lastTime > time && time > 0 && time <= 10 && gameAudio != null) gameAudio.PlayCountdown();
                lastTime = time;
            }
            if (clockAndMineView != null) clockAndMineView.SetTime(time);

            int participantCount = Mathf.Min(snapshotBuffer.Length, model.Participants.Count);
            bool changed = firstRefresh || avatarAppearanceDirty || lastLocalSlot != 0;
            for (int i = 0; i < snapshotBuffer.Length; i++)
            {
                bool participating = i < participantCount && model.Participants[i].IsParticipating;
                int coins = i < participantCount ? model.Participants[i].Coins : 0;
                int finishOrder = i < participantCount ? model.Participants[i].FinishOrder : 0;
                int health = i < participantCount ? model.Participants[i].Health : 0;
                int maxHealth = i < participantCount ? model.Participants[i].MaxHealth : 0;
                ParticipantStatus status = i < participantCount ? model.Participants[i].Status : ParticipantStatus.Lost;

                if (lastParticipating[i] != participating ||
                    lastCoins[i] != coins ||
                    lastFinishOrder[i] != finishOrder ||
                    lastHealth[i] != health ||
                    lastMaxHealth[i] != maxHealth ||
                    lastStatus[i] != status)
                {
                    changed = true;
                }

                lastParticipating[i] = participating;
                lastCoins[i] = coins;
                lastFinishOrder[i] = finishOrder;
                lastHealth[i] = health;
                lastMaxHealth[i] = maxHealth;
                lastStatus[i] = status;
            }

            if (changed)
            {
                int initialMaxHealth = match.InitialHealth;
                for (int i = 0; i < snapshotBuffer.Length; i++)
                {
                    if (i < participantCount)
                    {
                        var player = model.Participants[i];
                        Color color = ResolveParticipantColor(i);
                        snapshotBuffer[i] = new ParticipantHudSnapshot
                        {
                            SlotId = i,
                            DisplayName = ResolveParticipantName(i),
                            IsParticipating = player.IsParticipating,
                            IsLocal = i == 0,
                            Coins = player.Coins,
                            FinishOrder = player.FinishOrder,
                            Health = player.Health,
                            MaxHealth = player.MaxHealth,
                            InitialMaxHealth = initialMaxHealth,
                            Status = player.Status,
                            Color = color,
                            Appearance = ResolveAvatarAppearance(i, color)
                        };
                    }
                    else
                    {
                        snapshotBuffer[i] = new ParticipantHudSnapshot
                        {
                            SlotId = i,
                            IsParticipating = false
                        };
                    }
                }

                if (participantStripView != null) participantStripView.Refresh(snapshotBuffer);
                if (participantCount > 0)
                {
                    var localSnapshot = snapshotBuffer[0];
                    if (localPortraitView != null) localPortraitView.SetParticipant(0, localSnapshot.Appearance, localSnapshot.Status);
                    if (healthHeartsView != null) healthHeartsView.SetHealth(localSnapshot.Health, localSnapshot.MaxHealth, !firstRefresh);
                    if (localCoinCounterView != null) localCoinCounterView.SetCoins(localSnapshot.Coins, model.CoinsToWin, !firstRefresh);
                }

                lastLocalSlot = 0;
                avatarAppearanceDirty = false;
            }

            UpdateMineScheduleAndWarning(0, firstRefresh);
            firstRefresh = false;
        }

        private void UpdateNetworkUI()
        {
            var network = Mirror.NetworkManager.singleton as EachOneCoin.Networking.LanNetworkManager;
            var state = match.NetworkController;
            if (network == null || state == null || network.LocalSlot < 0 || network.LocalSlot >= 4) return;
            if (Application.isFocused && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || (NetworkMenuOpen && Input.GetKeyDown(KeyCode.JoystickButton1))))
            {
                if (settingsOpen) CloseSettings();
                else if (networkMenuOpen) Resume();
                else Pause();
            }

            int slot = network.LocalSlot;
            EnsureHudViews(true);

            int time = Mathf.CeilToInt(state.RemainingTime);
            if (time != lastTime)
            {
                if (lastTime > time && time > 0 && time <= 10 && gameAudio != null) gameAudio.PlayCountdown();
                lastTime = time;
            }
            if (clockAndMineView != null) clockAndMineView.SetTime(time);

            int localCoins = state.ScoreAt(slot);
            int localHealth = state.HealthAt(slot);
            int localMaxHealth = state.MaxHealthAt(slot);
            ParticipantStatus localStatus = state.StatusAt(slot);
            if (!firstRefresh && !Mirror.NetworkServer.active && !network.ClientResuming && localCoins > lastCoins[slot] && gameAudio != null)
                gameAudio.PlayCoin();

            if (localCoinCounterView != null)
                localCoinCounterView.SetCoins(localCoins, match.Model.CoinsToWin, !firstRefresh && !network.ClientResuming);

            bool waitingFirstReplica = !Mirror.NetworkServer.active && !networkHealthSynced && localMaxHealth == 0 && localStatus == ParticipantStatus.Active;
            if (!waitingFirstReplica)
            {
                bool isInitialSync = !networkHealthSynced;
                bool healthChanged = isInitialSync || lastNetworkSyncedHealth != localHealth || lastNetworkSyncedMaxHealth != localMaxHealth;
                lastNetworkSyncedHealth = localHealth;
                lastNetworkSyncedMaxHealth = localMaxHealth;
                networkHealthSynced = true;
                if (healthChanged && healthHeartsView != null)
                    healthHeartsView.SetHealth(localHealth, localMaxHealth, !isInitialSync && !network.ClientResuming);
            }

            if (minimapView != null)
            {
                byte visibleMask = 0;
                for (int i = 0; i < 4; i++)
                    if (state.IsSlotActive(i) && state.StatusAt(i) == ParticipantStatus.Active)
                        visibleMask |= (byte)(1 << i);
                int focusSlot = state.IsSpectating && state.SpectateSlot >= 0 ? state.SpectateSlot : slot;
                minimapView.Refresh(focusSlot, visibleMask, Time.unscaledTime);
            }

            if (arcStaminaBarView != null)
            {
                Transform localTarget = match.Characters != null && slot < match.Characters.Length && match.Characters[slot] != null
                    ? match.Characters[slot].transform
                    : null;
                arcStaminaBarView.SetTarget(localTarget);
                bool canShowStamina = !NetworkMenuOpen && !network.ClientResuming && !state.IsSpectating &&
                                      state.CurrentMatchStatus == MatchStatus.Playing &&
                                      localStatus == ParticipantStatus.Active;
                arcStaminaBarView.SetGameplayVisible(canShowStamina);
                if (!waitingFirstReplica)
                {
                    float stamina = state.StaminaAt(slot);
                    float maxStamina = match.StaminaAt(slot).Maximum;
                    bool exhausted = Mirror.NetworkServer.active
                        ? match.StaminaAt(slot).IsExhausted
                        : stamina <= 0.01f;
                    bool immediateSync = !networkStaminaSynced || network.ClientResuming;
                    networkStaminaSynced = true;
                    arcStaminaBarView.SetStamina(stamina, maxStamina, exhausted, !Mirror.NetworkServer.active, immediateSync);
                }
                arcStaminaBarView.TickVisual(Time.deltaTime, Time.unscaledTime);
            }

            int initialMaxHealth = match.InitialHealth;
            bool participantsChanged = firstRefresh || avatarAppearanceDirty || lastLocalSlot != slot;
            for (int i = 0; i < snapshotBuffer.Length; i++)
            {
                bool participating = state.IsParticipant(i);
                int pCoins = state.ScoreAt(i);
                int pOrder = state.FinishOrderAt(i);
                ParticipantStatus pStatus = state.StatusAt(i);
                int pHealth = state.HealthAt(i);
                int pMaxHealth = state.MaxHealthAt(i);
                if (!Mirror.NetworkServer.active && !networkHealthSynced && pMaxHealth == 0 && pStatus == ParticipantStatus.Active)
                {
                    pHealth = initialMaxHealth;
                    pMaxHealth = initialMaxHealth;
                }

                if (lastParticipating[i] != participating ||
                    lastCoins[i] != pCoins ||
                    lastFinishOrder[i] != pOrder ||
                    lastHealth[i] != pHealth ||
                    lastMaxHealth[i] != pMaxHealth ||
                    lastStatus[i] != pStatus)
                {
                    participantsChanged = true;
                }

                lastParticipating[i] = participating;
                lastCoins[i] = pCoins;
                lastFinishOrder[i] = pOrder;
                lastHealth[i] = pHealth;
                lastMaxHealth[i] = pMaxHealth;
                lastStatus[i] = pStatus;
            }

            if (participantsChanged)
            {
                for (int i = 0; i < snapshotBuffer.Length; i++)
                {
                    Color color = ResolveParticipantColor(i);
                    snapshotBuffer[i] = new ParticipantHudSnapshot
                    {
                        SlotId = i,
                        DisplayName = ResolveParticipantName(i),
                        IsParticipating = lastParticipating[i],
                        IsLocal = i == slot,
                        Coins = lastCoins[i],
                        FinishOrder = lastFinishOrder[i],
                        Health = lastHealth[i],
                        MaxHealth = lastMaxHealth[i],
                        InitialMaxHealth = initialMaxHealth,
                        Status = lastStatus[i],
                        Color = color,
                        Appearance = ResolveAvatarAppearance(i, color)
                    };
                }

                if (participantStripView != null) participantStripView.Refresh(snapshotBuffer);
                if (localPortraitView != null)
                    localPortraitView.SetParticipant(slot, snapshotBuffer[slot].Appearance, snapshotBuffer[slot].Status);

                lastLocalSlot = slot;
                avatarAppearanceDirty = false;
            }

            UpdateMineScheduleAndWarning(slot, firstRefresh || network.ClientResuming);

            if (!resultShown && state.CurrentMatchStatus == MatchStatus.Finished)
            {
                resultShown = true; networkMenuOpen = false; settingsOpen = false;
                if (centerBannerView != null) centerBannerView.Clear();
                if (resultText != null) resultText.text = localStatus == ParticipantStatus.Won ? "游戏胜利" : "游戏结束";
                if (resultCoinsText != null) resultCoinsText.text = BuildNetworkRanking(state, slot);
                if (gameAudio != null) { if (localStatus == ParticipantStatus.Won) gameAudio.PlayWin(); else gameAudio.PlayLose(); }
                RefreshPanels();
            }

            UpdateNetworkStatusBanner(network, state, slot);

            if (!match.IsPaused)
            {
                if (healthHeartsView != null) healthHeartsView.Tick(Time.deltaTime);
                TickHudViews(Time.deltaTime);
            }

            if (resultShown && network.LocalReturned)
            {
                if (resultText != null) resultText.text = "已返回房间，等待其他玩家";
                var exit = resultPanel != null ? resultPanel.transform.Find("MainMenuButton") : null;
                if (exit != null) { var label = exit.GetComponentInChildren<TMP_Text>(true); if (label != null) label.text = "退出房间"; }
                var waiting = new System.Text.StringBuilder();
                for (int i = 0; i < 4; i++) if ((network.Snapshot.occupiedMask & (1 << i)) != 0)
                    waiting.Append("席位 ").Append(i + 1).Append((network.ReturnedMask & (1 << i)) != 0 ? "：已返回\n" : "：查看结算中\n");
                if (resultCoinsText != null) resultCoinsText.text = waiting.ToString();
            }
            firstRefresh = false;
        }

        private void UpdateMineScheduleAndWarning(int localSlot, bool suppressBanner)
        {
            int upcomingMine = -1;
            float upcomingSeconds = 0f;
            bool upcomingWarning = false;
            bool hasUpcoming = match != null && match.Coins != null &&
                match.Coins.TryDescribeNextEvent(out upcomingMine, out upcomingSeconds, out upcomingWarning);
            bool upcomingConnected = hasUpcoming && upcomingMine >= 0 && localSlot >= 0 &&
                match.Coins.IsMineConnected(upcomingMine, localSlot);
            string mineName = hasUpcoming && upcomingMine >= 0 ? ResolveMineName(upcomingMine) : null;

            if (clockAndMineView != null)
            {
                clockAndMineView.SetMineSchedule(hasUpcoming, upcomingMine, mineName, upcomingSeconds, upcomingWarning, upcomingConnected);
            }

            bool mineRevealed = hasUpcoming && upcomingWarning && upcomingMine >= 0 &&
                (!lastUpcomingWarning || lastUpcomingMine != upcomingMine);
            if (!suppressBanner && !resultShown && mineRevealed && centerBannerView != null)
            {
                centerBannerView.ShowMineWarning(mineName);
            }

            lastUpcomingWarning = hasUpcoming && upcomingWarning;
            lastUpcomingMine = hasUpcoming ? upcomingMine : -1;
        }

        private void UpdateNetworkStatusBanner(
            EachOneCoin.Networking.LanNetworkManager network,
            EachOneCoin.Networking.NetworkMatchController state,
            int localSlot)
        {
            if (networkStatusBannerRoot == null || networkStatusBannerText == null) return;

            bool resuming = network.ClientResuming;
            bool spectating = state.IsSpectating;
            int spectateSlot = spectating ? state.SpectateSlot : -1;
            ParticipantStatus localStatus = state.StatusAt(localSlot);
            MatchStatus matchStatus = state.CurrentMatchStatus;

            if (networkStatusStateInitialized &&
                lastBannerResuming == resuming &&
                lastBannerSpectating == spectating &&
                lastBannerSpectateSlot == spectateSlot &&
                lastBannerLocalStatus == localStatus &&
                lastBannerMatchStatus == matchStatus &&
                lastBannerResultShown == resultShown)
            {
                return;
            }

            networkStatusStateInitialized = true;
            lastBannerResuming = resuming;
            lastBannerSpectating = spectating;
            lastBannerSpectateSlot = spectateSlot;
            lastBannerLocalStatus = localStatus;
            lastBannerMatchStatus = matchStatus;
            lastBannerResultShown = resultShown;

            string message = null;
            if (!resultShown && matchStatus != MatchStatus.Finished)
            {
                if (resuming)
                {
                    message = "正在恢复对局";
                }
                else if (spectating)
                {
                    message = spectateSlot < 0
                        ? "已淘汰，等待其他参与者结束"
                        : "已淘汰 · 观战 " + ResolveParticipantName(spectateSlot) + "（Tab / RB 切换）";
                }
                else if (matchStatus == MatchStatus.Playing &&
                         (localStatus == ParticipantStatus.Won || localStatus == ParticipantStatus.Lost))
                {
                    message = localStatus == ParticipantStatus.Won
                        ? "已获胜，其他玩家继续对局"
                        : "已淘汰，其他玩家继续对局";
                }
            }

            bool visible = !string.IsNullOrEmpty(message);
            if (visible && !string.Equals(lastNetworkStatusMessage, message, StringComparison.Ordinal))
            {
                lastNetworkStatusMessage = message;
                networkStatusBannerText.text = message;
            }
            if (networkStatusBannerRoot.activeSelf != visible)
            {
                networkStatusBannerRoot.SetActive(visible);
            }
        }

        private void TickHudViews(float deltaTime)
        {
            if (localPortraitView != null) localPortraitView.Tick(deltaTime);
            if (participantStripView != null) participantStripView.Tick(deltaTime);
            if (localCoinCounterView != null) localCoinCounterView.Tick(deltaTime);
            if (centerBannerView != null) centerBannerView.Tick(deltaTime);
            if (clockAndMineView != null) clockAndMineView.Tick(deltaTime);
            if (eventFeedView != null) eventFeedView.Tick(deltaTime);
        }

        private AvatarAppearanceData ResolveAvatarAppearance(int slot, Color fallbackColor)
        {
            if (avatarProvider == null) avatarProvider = new DefaultAvatarAppearanceProvider();
            return avatarProvider.GetAppearance(slot, fallbackColor);
        }

        private string ResolveParticipantName(int slot)
        {
            if (match != null) return match.GetParticipantDisplayName(slot);
            return slot >= 0 ? "玩家" + (slot + 1) : "未知玩家";
        }

        private Color ResolveParticipantColor(int slot)
        {
            if (match != null && match.Grid != null && slot >= 0 && slot < 4)
                return match.Grid.PlayerColor(slot);
            return Color.white;
        }

        private string ResolveMineName(int mineIndex)
        {
            if (mineIndex >= 0 && mineIndex < FallbackMineNames.Length)
            {
                if (match != null && match.Coins != null) return match.Coins.MineName(mineIndex);
                return FallbackMineNames[mineIndex];
            }
            return "矿点";
        }

        private Func<int, string> GetParticipantNameResolver()
        {
            if (participantNameResolver == null) participantNameResolver = ResolveParticipantName;
            return participantNameResolver;
        }

        private Func<int, Color> GetParticipantColorResolver()
        {
            if (participantColorResolver == null) participantColorResolver = ResolveParticipantColor;
            return participantColorResolver;
        }

        private Func<int, string> GetMineNameResolver()
        {
            if (mineNameResolver == null) mineNameResolver = ResolveMineName;
            return mineNameResolver;
        }

        private static string BuildNetworkRanking(EachOneCoin.Networking.NetworkMatchController state, int localSlot)
        {
            var slots = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 4; i++) if (state.IsParticipant(i)) slots.Add(i);
            slots.Sort((a, b) =>
            {
                int ao = state.FinishOrderAt(a), bo = state.FinishOrderAt(b);
                if (ao > 0 && bo > 0) return ao.CompareTo(bo);
                if (ao > 0) return -1;
                if (bo > 0) return 1;
                int score = state.ScoreAt(b).CompareTo(state.ScoreAt(a));
                return score != 0 ? score : a.CompareTo(b);
            });
            var text = new System.Text.StringBuilder("最终排名\n");
            int rank = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                int id = slots[i];
                if (i == 0 || state.FinishOrderAt(id) > 0 || state.FinishOrderAt(slots[i - 1]) > 0 || state.ScoreAt(id) != state.ScoreAt(slots[i - 1])) rank = i + 1;
                text.Append(rank).Append(". ").Append(id == localSlot ? "你" : state.IsHuman(id) ? "席位 " + (id + 1) : "NPC " + (id + 1));
                text.Append("  金币 ").Append(state.ScoreAt(id)).Append("  ").Append(StatusText(state.StatusAt(id))).Append('\n');
            }
            return text.ToString();
        }

        private static string StatusText(ParticipantStatus status)
        {
            switch (status)
            {
                case ParticipantStatus.Respawning: return "复活中";
                case ParticipantStatus.Won: return "已获胜";
                case ParticipantStatus.Lost: return "已失败";
                default: return "比赛中";
            }
        }

        private void EnsureHealthHearts(int initialMaxHealth)
        {
            if (!legacyHealthHidden)
            {
                legacyHealthHidden = true;
                HideLegacyHealthUI();
            }
            if (heartsInitialized && healthHeartsView != null) return;
            if (healthHeartsView == null && hudPanel != null)
            {
                var container = new GameObject("HealthHearts", typeof(RectTransform));
                var rt = (RectTransform)container.transform;
                rt.SetParent(hudPanel.transform, false);
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                healthHeartsView = container.AddComponent<HealthHeartsView>();
                rt.anchoredPosition = healthHeartsView.TopLeftPosition;
            }
            if (healthHeartsView != null)
            {
                healthHeartsView.Initialize(initialMaxHealth);
                heartsInitialized = true;
                AdjustLeftHudBelowHearts();
            }
        }

        private void EnsureHudViews(bool includeNetworkBanner)
        {
            if (!legacyHudTextsHidden)
            {
                legacyHudTextsHidden = true;
                HideLegacyHudTexts();
            }
            int initialHealth = match != null ? match.InitialHealth : 3;
            EnsureHealthHearts(initialHealth);
            EnsureArcStaminaBar();
            EnsureMinimap();
            EnsureLocalPortrait();
            EnsureParticipantStrip();
            EnsureLocalCoinCounter();
            EnsureClockAndMine();
            EnsureEventFeed();
            if (includeNetworkBanner)
            {
                EnsureNetworkStatusBanner();
            }
            EnsureCenterBanner();
            if (centerBannerView != null && hudPanel != null && centerBannerView.transform.parent == hudPanel.transform)
            {
                int lastIndex = hudPanel.transform.childCount - 1;
                if (centerBannerView.transform.GetSiblingIndex() != lastIndex)
                {
                    centerBannerView.transform.SetAsLastSibling();
                }
            }
        }

        private void HideLegacyHudTexts()
        {
            if (timeText != null) timeText.gameObject.SetActive(false);
            if (coinText != null) coinText.gameObject.SetActive(false);
            if (participantText != null) participantText.gameObject.SetActive(false);
            if (feedbackText != null) feedbackText.gameObject.SetActive(false);
        }

        private void AdjustPauseButtonLayout()
        {
            var pauseButton = hudPanel != null ? hudPanel.transform.Find("PauseButton") : null;
            if (pauseButton == null) return;
            var selectable = pauseButton.GetComponent<Selectable>();
            if (selectable != null)
            {
                var navigation = selectable.navigation;
                navigation.mode = Navigation.Mode.None;
                selectable.navigation = navigation;
            }
            if (pauseButton is RectTransform pauseRect)
            {
                pauseRect.anchoredPosition = new Vector2(-376f, -65f);
            }
        }

        private void AdjustLeftHudBelowHearts()
        {
            if (leftHudAdjusted || healthHeartsView == null) return;
            leftHudAdjusted = true;

            bool coinVisible = coinText != null && coinText.gameObject.activeSelf;
            bool feedbackVisible = feedbackText != null && feedbackText.gameObject.activeSelf;
            if (!coinVisible) return;

            var heartsRect = healthHeartsView.transform as RectTransform;
            var coinRect = coinText.rectTransform;
            if (heartsRect == null || coinRect == null) return;

            // Visible heart tip sits around 86% down from the top of the heart slot.
            float heartsVisualBottom = heartsRect.anchoredPosition.y - healthHeartsView.HeartSize * 0.88f;
            float coinTop = coinRect.anchoredPosition.y + coinRect.sizeDelta.y * (1f - coinRect.pivot.y);
            const float minGap = 18f;
            float shiftDown = coinTop - (heartsVisualBottom - minGap);
            if (shiftDown <= 1f) return;

            coinRect.anchoredPosition -= new Vector2(0f, shiftDown);
            if (feedbackVisible)
                feedbackText.rectTransform.anchoredPosition -= new Vector2(0f, shiftDown);

            Transform staminaRoot = null;
            if (staminaFill != null && staminaFill.transform.parent != null &&
                (hudPanel == null || staminaFill.transform.parent.gameObject != hudPanel) &&
                staminaFill.transform.parent.name.IndexOf("Stamina", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                staminaRoot = staminaFill.transform.parent;
            }
            if (staminaRoot != null && staminaRoot.gameObject.activeSelf && staminaRoot is RectTransform staminaRect)
                staminaRect.anchoredPosition -= new Vector2(0f, shiftDown);
        }

        private void HideLegacyHealthUI()
        {
            Transform legacyRoot = null;
            if (healthFill != null && healthFill.transform.parent != null &&
                (hudPanel == null || healthFill.transform.parent.gameObject != hudPanel) &&
                healthFill.transform.parent.name.IndexOf("Health", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                legacyRoot = healthFill.transform.parent;
            }
            if (legacyRoot != null)
            {
                legacyRoot.gameObject.SetActive(false);
                return;
            }
            if (healthFill != null) healthFill.gameObject.SetActive(false);
            if (healthText != null) healthText.gameObject.SetActive(false);
        }

        private void EnsureArcStaminaBar()
        {
            if (!legacyStaminaHidden)
            {
                legacyStaminaHidden = true;
                HideLegacyStaminaUI();
            }
            if (staminaBarInitialized && arcStaminaBarView != null) return;
            if (arcStaminaBarView == null && hudPanel != null)
            {
                var container = new GameObject("ArcStaminaBar", typeof(RectTransform));
                var rt = (RectTransform)container.transform;
                rt.SetParent(hudPanel.transform, false);
                arcStaminaBarView = container.AddComponent<ArcStaminaBarView>();
            }
            if (arcStaminaBarView != null)
            {
                arcStaminaBarView.Initialize();
                staminaBarInitialized = true;
            }
        }

        private void HideLegacyStaminaUI()
        {
            Transform legacyRoot = null;
            if (staminaFill != null && staminaFill.transform.parent != null &&
                (hudPanel == null || staminaFill.transform.parent.gameObject != hudPanel) &&
                staminaFill.transform.parent.name.IndexOf("Stamina", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                legacyRoot = staminaFill.transform.parent;
            }
            if (legacyRoot != null)
            {
                legacyRoot.gameObject.SetActive(false);
                return;
            }
            if (staminaFill != null) staminaFill.gameObject.SetActive(false);
            if (staminaText != null) staminaText.gameObject.SetActive(false);
        }

        private void EnsureMinimap()
        {
            if (minimapInitialized && minimapView != null) return;
            if (minimapView == null && hudPanel != null)
            {
                var container = new GameObject("ArenaMinimap", typeof(RectTransform));
                var rt = (RectTransform)container.transform;
                rt.SetParent(hudPanel.transform, false);
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                minimapView = container.AddComponent<ArenaMinimapView>();
                rt.anchoredPosition = minimapView.BottomRightOffset;
            }
            if (minimapView != null && match != null && match.Model != null)
            {
                minimapView.Initialize(match);
                minimapInitialized = true;
            }
        }

        private void EnsureLocalPortrait()
        {
            if (localPortraitInitialized && localPortraitView != null) return;
            if (localPortraitView == null && hudPanel != null)
            {
                var container = new GameObject("LocalPlayerPortrait", typeof(RectTransform));
                var rt = (RectTransform)container.transform;
                rt.SetParent(hudPanel.transform, false);
                localPortraitView = container.AddComponent<LocalPlayerPortraitView>();
            }
            if (localPortraitView != null)
            {
                if (avatarProvider == null) avatarProvider = new DefaultAvatarAppearanceProvider();
                localPortraitView.Initialize(avatarProvider, NameplateFont);
                localPortraitInitialized = true;
            }
        }

        private void EnsureParticipantStrip()
        {
            if (participantStripInitialized && participantStripView != null) return;
            float mapSize = minimapView != null ? minimapView.MapSize : 368f;
            Vector2 mapOffset = minimapView != null ? minimapView.BottomRightOffset : new Vector2(-12f, 12f);
            Vector2 stripOffset = new Vector2(mapOffset.x, mapOffset.y + mapSize + 8f);
            if (participantStripView == null && hudPanel != null)
            {
                var container = new GameObject("ParticipantHudStrip", typeof(RectTransform));
                var rt = (RectTransform)container.transform;
                rt.SetParent(hudPanel.transform, false);
                participantStripView = container.AddComponent<ParticipantHudStripView>();
            }
            if (participantStripView != null)
            {
                participantStripView.Initialize(4, mapSize, stripOffset, NameplateFont);
                participantStripInitialized = true;
            }
        }

        private void EnsureLocalCoinCounter()
        {
            if (localCoinCounterInitialized && localCoinCounterView != null) return;
            if (localCoinCounterView == null && hudPanel != null)
            {
                var container = new GameObject("LocalCoinCounter", typeof(RectTransform));
                var rt = (RectTransform)container.transform;
                rt.SetParent(hudPanel.transform, false);
                localCoinCounterView = container.AddComponent<LocalCoinCounterView>();
            }
            if (localCoinCounterView != null)
            {
                localCoinCounterView.Initialize(NameplateFont);
                localCoinCounterInitialized = true;
            }
        }

        private void EnsureClockAndMine()
        {
            if (clockAndMineInitialized && clockAndMineView != null) return;
            if (clockAndMineView == null && hudPanel != null)
            {
                var container = new GameObject("MatchClockAndMine", typeof(RectTransform));
                var rt = (RectTransform)container.transform;
                rt.SetParent(hudPanel.transform, false);
                clockAndMineView = container.AddComponent<MatchClockAndMineView>();
            }
            if (clockAndMineView != null)
            {
                clockAndMineView.Initialize(NameplateFont);
                clockAndMineInitialized = true;
            }
        }

        private void EnsureEventFeed()
        {
            if (eventFeedInitialized && eventFeedView != null) return;
            if (eventFeedView == null && hudPanel != null)
            {
                var container = new GameObject("MatchEventFeed", typeof(RectTransform));
                var rt = (RectTransform)container.transform;
                rt.SetParent(hudPanel.transform, false);
                eventFeedView = container.AddComponent<MatchEventFeedView>();
            }
            if (eventFeedView != null)
            {
                eventFeedView.Initialize(NameplateFont);
                eventFeedInitialized = true;
            }
        }

        private void EnsureCenterBanner()
        {
            if (centerBannerInitialized && centerBannerView != null) return;
            if (centerBannerView == null && hudPanel != null)
            {
                var container = new GameObject("CenterBannerPrompt", typeof(RectTransform));
                var rt = (RectTransform)container.transform;
                rt.SetParent(hudPanel.transform, false);
                centerBannerView = container.AddComponent<CenterBannerPromptView>();
            }
            if (centerBannerView != null)
            {
                centerBannerView.Initialize(NameplateFont);
                if (hudPanel != null && centerBannerView.transform.parent == hudPanel.transform)
                {
                    centerBannerView.transform.SetAsLastSibling();
                }
                centerBannerInitialized = true;
            }
        }

        private void EnsureNetworkStatusBanner()
        {
            if (networkStatusBannerInitialized && networkStatusBannerRoot != null) return;
            if (hudPanel == null) return;

            var bannerObj = new GameObject("NetworkStatusBanner", typeof(RectTransform));
            var rt = (RectTransform)bannerObj.transform;
            rt.SetParent(hudPanel.transform, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 128f);
            rt.sizeDelta = new Vector2(560f, 44f);

            if (statusBannerIcons == null) statusBannerIcons = new HudProceduralIcons();
            Sprite panelSprite = statusBannerIcons.GetRoundedPanelSprite();

            var bgObj = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var bgRect = (RectTransform)bgObj.transform;
            bgRect.SetParent(rt, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var bgImage = bgObj.GetComponent<Image>();
            bgImage.sprite = panelSprite;
            bgImage.type = panelSprite != null && panelSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            bgImage.color = new Color(0.075f, 0.098f, 0.141f, 0.84f);
            bgImage.raycastTarget = false;

            var textObj = new GameObject("StatusText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(rt, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 4f);
            textRect.offsetMax = new Vector2(-18f, -4f);

            var tmp = textObj.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset font = NameplateFont;
            if (font != null)
            {
                tmp.font = font;
                if (font.material != null) tmp.fontSharedMaterial = font.material;
            }
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Normal;
            tmp.fontWeight = FontWeight.Regular;
            tmp.fontSize = 20f;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 13f;
            tmp.fontSizeMax = 20f;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.color = new Color(0.95f, 0.97f, 1f, 1f);
            tmp.raycastTarget = false;
            tmp.text = string.Empty;

            bannerObj.SetActive(false);
            networkStatusBannerRoot = bannerObj;
            networkStatusBannerText = tmp;
            networkStatusBannerInitialized = true;
        }

        private Camera ResolveActiveCamera()
        {
            if (cachedMainCamera != null && cachedMainCamera.isActiveAndEnabled) return cachedMainCamera;
            cachedMainCamera = Camera.main;
            return cachedMainCamera;
        }

        private void LateUpdate()
        {
            GamepadNavigation.EnsureFocus(CurrentNavigationPanel(), usingGamepad);
            if (arcStaminaBarView != null)
            {
                arcStaminaBarView.UpdateScreenPosition(ResolveActiveCamera());
            }
        }

        private GameObject CurrentNavigationPanel()
        {
            if (settingsOpen) return settingsPanel;
            if (resultShown) return resultPanel;
            if (match != null && (match.IsPaused || networkMenuOpen)) return pausePanel;
            return null;
        }

        private void RefreshPanels()
        {
            bool paused = match != null && (match.IsPaused || networkMenuOpen);
            bool network = match != null && match.IsNetworkMovementTest;
            var pauseRestart = pausePanel.transform.Find("RestartButton");
            var resultRestart = resultPanel.transform.Find("RestartButton");
            if (pauseRestart != null) pauseRestart.gameObject.SetActive(!network);
            if (resultRestart != null) resultRestart.gameObject.SetActive(!network);
            var resultMenu = resultPanel.transform.Find("MainMenuButton");
            if (resultMenu != null && network)
            {
                var label = resultMenu.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "返回房间";
            }
            hudPanel.SetActive(!resultShown);
            pausePanel.SetActive(paused && !settingsOpen);
            settingsPanel.SetActive(settingsOpen);
            resultPanel.SetActive(resultShown && !settingsOpen);
            if (EventSystem.current != null)
            {
                EventSystem.current.sendNavigationEvents = usingGamepad && Application.isFocused;
                EventSystem.current.SetSelectedGameObject(null);
                GamepadNavigation.Configure(CurrentNavigationPanel());
                GamepadNavigation.EnsureFocus(CurrentNavigationPanel(), usingGamepad);
            }
            if (promptImages != null) promptImages.Refresh(showPrompts, usingGamepad, !paused && !resultShown, paused || settingsOpen);
            if (attackPromptText != null)
            {
                attackPromptText.gameObject.SetActive(showPrompts && !paused && !resultShown);
                attackPromptText.text = usingGamepad ? "按住 A + 左摇杆瞄准，松开攻击\n按住 B 冲刺" : "按住空格 + 鼠标瞄准，松开攻击\n按住 Shift 冲刺";
            }
        }

        public void Pause() { if (match == null || resultShown) return; if (match.IsNetworkMovementTest) networkMenuOpen = true; else match.Pause(); RefreshPanels(); }
        public void Resume() { if (match == null) return; match.SuppressGamepadActionsUntilReleased(); settingsOpen = false; networkMenuOpen = false; match.Resume(); RefreshPanels(); }
        public void OpenSettings()
        {
            if (match == null || (!match.IsPaused && !networkMenuOpen)) return;
            settingsOpen = true;
            volumeSlider.SetValueWithoutNotify(gameAudio.Volume);
            UpdateVolumeLabel(gameAudio.Volume);
            RefreshPanels();
        }
        public void CloseSettings() { settingsOpen = false; RefreshPanels(); }
        public void SetVolume(float value) { gameAudio.SetVolume(value); UpdateVolumeLabel(gameAudio.Volume); }
        private void UpdateVolumeLabel(float value) { volumeValueText.text = $"{Mathf.RoundToInt(value * 100f)}%"; }
        public void SetPromptsVisible(bool value)
        {
            showPrompts = value;
            PlayerPrefs.SetInt("GameUI.ShowPrompts", value ? 1 : 0);
            PlayerPrefs.Save();
            RefreshPanels();
        }
        public void Restart() { if (match != null && match.IsNetworkMovementTest) { MainMenu(); return; } if (match != null) match.Resume(); if (gameAudio != null) gameAudio.PlayRestart(); SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
        public void MainMenu()
        {
            if (match != null && match.IsNetworkMovementTest)
            {
                var network = Mirror.NetworkManager.singleton as EachOneCoin.Networking.LanNetworkManager;
                if (network != null)
                {
                    if (resultShown && !network.LocalReturned) { network.RequestReturnToRoom(); return; }
                    network.ExitMovementTest();
                    return;
                }
            }
            if (match != null) match.Resume();
            SceneManager.LoadScene("BootScene");
        }
    }
}
