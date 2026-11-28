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
        private const float MinimapStripGap = 8f;
        private const float MinimapTriggerThreshold = 0.5f;

        [SerializeField] private MatchCoordinator match;
        [SerializeField] private GameAudio gameAudio;
        [SerializeField] private GameObject hudPanel, pausePanel, settingsPanel, resultPanel;
        [SerializeField] private TMP_Text resultText, resultCoinsText, volumeValueText;
        [SerializeField] private HealthHeartsView healthHeartsView;
        [SerializeField] private ArcStaminaBarView arcStaminaBarView;
        [SerializeField] private ArenaMinimapView minimapView;
        [SerializeField] private ParticipantHudStripView participantStripView;
        [SerializeField] private LocalCoinCounterView localCoinCounterView;
        [SerializeField] private CenterBannerPromptView centerBannerView;
        [SerializeField] private MatchClockAndMineView clockAndMineView;
        [SerializeField] private MatchEventFeedView eventFeedView;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle promptsToggle;
        [SerializeField] private Toggle joystickModeToggle;
        [SerializeField] private ControlPromptImages promptImages;
        [SerializeField, Min(0.05f)] private float hitFlashDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] private float hitFrameOpacity = 0.65f;
        [SerializeField, Min(1f)] private float hitFrameThickness = 24f;
        [SerializeField] private Image[] hitFrame = new Image[4];
        [SerializeField] private bool enableGamepadMinimapToggle = true;
        private readonly ParticipantHudSnapshot[] snapshotBuffer = new ParticipantHudSnapshot[4];
        private IAvatarAppearanceProvider avatarProvider = new DefaultAvatarAppearanceProvider();
        private MatchAvatarAppearanceProvider ownedMatchAvatarProvider;
        private bool externalAvatarProviderAssigned;
        private Func<int, string> participantNameResolver;
        private Func<int, Color> participantColorResolver;
        private Func<int, string> mineNameResolver;
        [SerializeField] private bool showTouchControlsInEditor = true;
        private TMP_Text joystickModeLabel;
        private float hitFlashRemaining;
        private InputDeviceTracker inputDevice;
        [SerializeField] private TouchGameplayControls touchControls;
        private bool usingGamepad, showPrompts, settingsOpen, resultShown;
        private int lastTime = -1;
        private readonly int[] lastCoins = { -1, -1, -1, -1 };
        private readonly int[] lastFinishOrder = { -1, -1, -1, -1 };
        private readonly int[] lastHealth = { -1, -1, -1, -1 };
        private readonly int[] lastMaxHealth = { -1, -1, -1, -1 };
        private readonly bool[] lastParticipating = new bool[4];
        private readonly ParticipantStatus[] lastStatus = new ParticipantStatus[4];
        private int lastLocalSlot = -1;
        private int lastObservedHealthSlot = -1;
        private int lastPortraitSlot = -1;
        private int lastNetworkSyncedHealth = -1;
        private int lastNetworkSyncedMaxHealth = -1;
        private bool firstRefresh = true;
        private bool avatarAppearanceDirty;
        private bool lastUpcomingWarning;
        private int lastUpcomingMine = int.MaxValue;
        private bool networkMenuOpen;
        private bool heartsInitialized;
        private bool staminaBarInitialized;
        private bool minimapInitialized;
        private MinimapSizeMode minimapSizeMode = MinimapSizeMode.Compact;
        private bool minimapHoldActive;
        private bool participantStripInitialized;
        private bool localCoinCounterInitialized;
        private bool centerBannerInitialized;
        private bool clockAndMineInitialized;
        private bool eventFeedInitialized;
        private bool networkStatusBannerInitialized;
        private bool replayUiAdjusted;
        private bool networkHealthSynced;
        private bool networkStaminaSynced;
        [SerializeField] private GameObject networkStatusBannerRoot;
        [SerializeField] private TMP_Text networkStatusBannerText;
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
                if (resultText != null && resultText.font != null) return resultText.font;
                return null;
            }
        }
        public bool NetworkMenuOpen => networkMenuOpen || settingsOpen;
        public MinimapSizeMode CurrentMinimapSizeMode => minimapSizeMode;

        public void SetMinimapSizeMode(MinimapSizeMode mode)
        {
            minimapSizeMode = mode;
            SyncMinimapAndParticipantStripLayout();
        }

        public bool ToggleMinimapSize()
        {
            if (IsMinimapToggleBlocked())
            {
                return false;
            }

            if (minimapView == null && participantStripView == null)
            {
                return false;
            }

            MinimapSizeMode nextMode = minimapSizeMode == MinimapSizeMode.Expanded
                ? MinimapSizeMode.Compact
                : MinimapSizeMode.Expanded;
            SetMinimapSizeMode(nextMode);
            return true;
        }

        private bool IsMinimapToggleBlocked()
        {
            if (resultShown || settingsOpen || networkMenuOpen)
            {
                return true;
            }

            return match != null && match.IsPaused;
        }

        private void HandleMinimapToggleInput()
        {
            bool available = Application.isFocused && !IsMinimapToggleBlocked();
            bool toggle = available && (Input.GetKeyDown(KeyCode.M)
                || (enableGamepadMinimapToggle && Input.GetKeyDown(KeyCode.JoystickButton6)));
            // The combined trigger axis is mapped to its positive side for RT; verify per controller.
            bool hold = available && (Input.GetKey(KeyCode.CapsLock)
                || (enableGamepadMinimapToggle && Input.GetAxisRaw("GamepadAxis3") > MinimapTriggerThreshold));
            ProcessMinimapInput(available, toggle, hold);
        }

        private void ProcessMinimapInput(bool available, bool toggle, bool hold)
        {
            if (!available)
            {
                if (minimapHoldActive)
                {
                    minimapHoldActive = false;
                    SetMinimapSizeMode(MinimapSizeMode.Compact);
                }
                return;
            }

            if (hold)
            {
                if (!minimapHoldActive || minimapSizeMode != MinimapSizeMode.Expanded)
                    SetMinimapSizeMode(MinimapSizeMode.Expanded);
                minimapHoldActive = true;
                return;
            }

            if (minimapHoldActive)
            {
                minimapHoldActive = false;
                SetMinimapSizeMode(MinimapSizeMode.Compact);
                return;
            }

            if (toggle) ToggleMinimapSize();
        }

        private void SyncMinimapAndParticipantStripLayout()
        {
            if (minimapView != null)
            {
                minimapView.SetSizeMode(minimapSizeMode);
            }

            if (participantStripView != null)
            {
                float currentMapSize = minimapView != null
                    ? minimapView.MapSize
                    : (minimapSizeMode == MinimapSizeMode.Compact ? 210f : 368f);
                float expandedMapSize = minimapView != null ? minimapView.ExpandedMapSize : 368f;
                Vector2 mapOffset = minimapView != null ? minimapView.TopLeftOffset : new Vector2(12f, -12f);
                Vector2 stripOffset = new Vector2(mapOffset.x + currentMapSize + MinimapStripGap, mapOffset.y);
                participantStripView.SetTopLeftAnchor(true);
                participantStripView.SetLayoutMode(ParticipantHudLayoutMode.Horizontal, expandedMapSize, stripOffset);
            }
        }

        public void SetAvatarAppearanceProvider(IAvatarAppearanceProvider provider)
        {
            externalAvatarProviderAssigned = provider != null;
            IAvatarAppearanceProvider next = provider ?? new DefaultAvatarAppearanceProvider();
            if (ownedMatchAvatarProvider != null && !ReferenceEquals(ownedMatchAvatarProvider, next))
            {
                ownedMatchAvatarProvider.Dispose();
                ownedMatchAvatarProvider = null;
            }
            BindAvatarAppearanceProvider(next);
        }

        private void BindAvatarAppearanceProvider(IAvatarAppearanceProvider next)
        {
            if (ReferenceEquals(avatarProvider, next)) return;
            if (avatarProvider != null) avatarProvider.AppearanceChanged -= OnAvatarAppearanceChanged;
            avatarProvider = next;
            if (isActiveAndEnabled && avatarProvider != null) avatarProvider.AppearanceChanged += OnAvatarAppearanceChanged;
            avatarAppearanceDirty = true;
        }

        private void EnsureMatchAvatarProvider()
        {
            if (externalAvatarProviderAssigned || ownedMatchAvatarProvider != null || match == null)
            {
                return;
            }

            ownedMatchAvatarProvider = new MatchAvatarAppearanceProvider(match);
            BindAvatarAppearanceProvider(ownedMatchAvatarProvider);
        }

        private void Awake()
        {
            inputDevice = GetComponent<InputDeviceTracker>();
            if (WantsTouchControls() && match != null && match.LocalInput != null && touchControls != null)
            {
                touchControls.Initialize(match, this, hudPanel.transform, NameplateFont);
                InitializeJoystickModeOption();
            }
            else if (joystickModeToggle != null) joystickModeToggle.gameObject.SetActive(false);
            if (inputDevice != null) { usingGamepad = inputDevice.UsingGamepad; inputDevice.DeviceChanged += OnDeviceChanged; }
            showPrompts = PlayerPrefs.GetInt("GameUI.ShowPrompts", 1) != 0;
            if (promptsToggle != null) promptsToggle.SetIsOnWithoutNotify(showPrompts);
            CreateHitFrame();
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
            ProcessMinimapInput(false, false, false);
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
            if (ownedMatchAvatarProvider != null)
            {
                ownedMatchAvatarProvider.Dispose();
                ownedMatchAvatarProvider = null;
            }
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
        // The real device uses Application.platform. Play Mode in the Editor always reports the
        // host platform, so the Android build target is used as the preview switch instead.
        private bool WantsTouchControls()
        {
            if (Application.platform == RuntimePlatform.Android) return true;
#if UNITY_EDITOR
            return showTouchControlsInEditor &&
                UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android;
#else
            return false;
#endif
        }

        // The row is authored in the settings panel; only its initial state and label are handled here.
        private void InitializeJoystickModeOption()
        {
            if (touchControls == null || joystickModeToggle == null) return;
            joystickModeToggle.gameObject.SetActive(true);
            joystickModeLabel = joystickModeToggle.GetComponentInChildren<TMP_Text>(true);
            joystickModeToggle.SetIsOnWithoutNotify(touchControls.JoystickMode == TouchJoystickMode.Floating);
            RefreshJoystickModeLabel();
        }

        // Bound as a persistent scene listener, matching the prompts toggle next to it.
        public void OnJoystickModeChanged(bool floating)
        {
            if (touchControls != null)
                touchControls.SetJoystickMode(floating ? TouchJoystickMode.Floating : TouchJoystickMode.Fixed);
            RefreshJoystickModeLabel();
        }

        private void RefreshJoystickModeLabel()
        {
            if (joystickModeLabel == null || touchControls == null) return;
            joystickModeLabel.text = TouchSettings.JoystickModeLabel(touchControls.JoystickMode);
        }


        private void OnObstacleHit(int id)
        {
            if (!IsLocalParticipant(id)) return;
            hitFlashRemaining = Mathf.Max(0.05f, hitFlashDuration);
            UpdateHitFrame();
        }

        private void OnAttackHit(int id)
        {
            if (!IsLocalParticipant(id)) return;
            hitFlashRemaining = Mathf.Max(0.05f, hitFlashDuration);
            UpdateHitFrame();
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
            for (int i = 0; i < hitFrame.Length; i++)
            {
                if (hitFrame[i] == null) continue;
                var edgeRect = hitFrame[i].rectTransform;
                var size = edgeRect.sizeDelta;
                if (i < 2) size.y = hitFrameThickness;
                else size.x = hitFrameThickness;
                edgeRect.sizeDelta = size;
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
            if (match.IsReplay && !replayUiAdjusted)
            {
                var pauseButton = hudPanel != null ? hudPanel.transform.Find("PauseButton") : null;
                if (pauseButton != null) pauseButton.gameObject.SetActive(false);
                if (promptImages != null) promptImages.gameObject.SetActive(false);
                replayUiAdjusted = true;
            }
            if (match.IsNetworkMovementTest)
            {
                if (hitFlashRemaining > 0f) { hitFlashRemaining = Mathf.Max(0f, hitFlashRemaining - Time.deltaTime); UpdateHitFrame(); }
                UpdateNetworkUI();
                return;
            }
            if (!match.IsReplay && Application.isFocused && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || ((settingsOpen || match.IsPaused) && Input.GetKeyDown(KeyCode.JoystickButton1))))
            {
                if (settingsOpen) CloseSettings();
                else if (match.IsPaused) Resume();
                else Pause();
            }
            var model = match.Model;
            EnsureHudViews(false);
            HandleMinimapToggleInput();
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
            if (!match.IsReplay && model.Status == MatchStatus.Finished && !resultShown)
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
                    if (healthHeartsView != null) healthHeartsView.SetHealth(localSnapshot.Health, localSnapshot.MaxHealth, !firstRefresh);
                    if (localCoinCounterView != null) localCoinCounterView.SetCoins(localSnapshot.Coins, model.CoinsToWin, !firstRefresh);
                }

                lastLocalSlot = 0;
                lastPortraitSlot = 0;
                avatarAppearanceDirty = false;
            }

            UpdateMineScheduleAndWarning(0, firstRefresh);
            firstRefresh = false;
        }

        private static int ResolveObservedHudSlot(int localSlot, bool isSpectating, int spectateSlot)
        {
            if (isSpectating && spectateSlot >= 0 && spectateSlot < 4)
            {
                return spectateSlot;
            }

            return localSlot;
        }

        private void SyncObservedNetworkHealth(
            int observedSlot,
            int observedHealth,
            int observedMaxHealth,
            ParticipantStatus observedStatus,
            bool isServer,
            bool clientResuming)
        {
            bool waitingFirstReplica = !isServer && !networkHealthSynced && observedMaxHealth == 0 && observedStatus == ParticipantStatus.Active;
            if (waitingFirstReplica)
            {
                return;
            }

            bool isInitialSync = !networkHealthSynced;
            bool slotSwitched = lastObservedHealthSlot != observedSlot;
            bool healthChanged = isInitialSync || slotSwitched || lastNetworkSyncedHealth != observedHealth || lastNetworkSyncedMaxHealth != observedMaxHealth;
            lastObservedHealthSlot = observedSlot;
            lastNetworkSyncedHealth = observedHealth;
            lastNetworkSyncedMaxHealth = observedMaxHealth;
            networkHealthSynced = true;
            if (healthChanged && healthHeartsView != null)
            {
                bool animate = !isInitialSync && !slotSwitched && !clientResuming;
                healthHeartsView.SetHealth(observedHealth, observedMaxHealth, animate);
            }
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
            HandleMinimapToggleInput();

            int focusSlot = ResolveObservedHudSlot(slot, state.IsSpectating, state.SpectateSlot);

            int time = Mathf.CeilToInt(state.RemainingTime);
            if (time != lastTime)
            {
                if (lastTime > time && time > 0 && time <= 10 && gameAudio != null) gameAudio.PlayCountdown();
                lastTime = time;
            }
            if (clockAndMineView != null) clockAndMineView.SetTime(time);

            int localCoins = state.ScoreAt(slot);
            ParticipantStatus localStatus = state.StatusAt(slot);
            if (!firstRefresh && !Mirror.NetworkServer.active && !network.ClientResuming && localCoins > lastCoins[slot] && gameAudio != null)
                gameAudio.PlayCoin();

            if (localCoinCounterView != null)
                localCoinCounterView.SetCoins(localCoins, match.Model.CoinsToWin, !firstRefresh && !network.ClientResuming);

            int observedHealth = state.HealthAt(focusSlot);
            int observedMaxHealth = state.MaxHealthAt(focusSlot);
            ParticipantStatus observedStatus = state.StatusAt(focusSlot);
            bool waitingFirstReplica = !Mirror.NetworkServer.active && !networkHealthSynced && observedMaxHealth == 0 && observedStatus == ParticipantStatus.Active;
            SyncObservedNetworkHealth(
                focusSlot,
                observedHealth,
                observedMaxHealth,
                observedStatus,
                Mirror.NetworkServer.active,
                network.ClientResuming);

            if (minimapView != null)
            {
                byte visibleMask = 0;
                for (int i = 0; i < 4; i++)
                    if (state.IsSlotActive(i) && state.StatusAt(i) == ParticipantStatus.Active)
                        visibleMask |= (byte)(1 << i);
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
            bool participantsChanged = firstRefresh || avatarAppearanceDirty || lastLocalSlot != slot || lastPortraitSlot != focusSlot;
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

                lastLocalSlot = slot;
                lastPortraitSlot = focusSlot;
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
            }
        }

        private void EnsureHudViews(bool includeNetworkBanner)
        {
            int initialHealth = match != null ? match.InitialHealth : 3;
            EnsureHealthHearts(initialHealth);
            EnsureArcStaminaBar();
            EnsureMinimap();
            EnsureMatchAvatarProvider();
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

        private void EnsureArcStaminaBar()
        {
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

        private void EnsureMinimap()
        {
            if (minimapInitialized && minimapView != null) return;
            if (minimapView == null && hudPanel != null)
            {
                var container = new GameObject("ArenaMinimap", typeof(RectTransform));
                var rt = (RectTransform)container.transform;
                rt.SetParent(hudPanel.transform, false);
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                minimapView = container.AddComponent<ArenaMinimapView>();
                rt.anchoredPosition = minimapView.TopLeftOffset;
            }
            if (minimapView != null && match != null && match.Model != null)
            {
                minimapView.Initialize(match);
                minimapView.SetSizeMode(minimapSizeMode);
                minimapInitialized = true;
            }
        }

        private void EnsureParticipantStrip()
        {
            if (participantStripInitialized && participantStripView != null) return;
            float mapSize = minimapView != null ? minimapView.ExpandedMapSize : 368f;
            Vector2 mapOffset = minimapView != null ? minimapView.TopLeftOffset : new Vector2(12f, -12f);
            Vector2 stripOffset = new Vector2(mapOffset.x + mapSize + MinimapStripGap, mapOffset.y);
            if (participantStripView == null && hudPanel != null)
            {
                var container = new GameObject("ParticipantHudStrip", typeof(RectTransform));
                var rt = (RectTransform)container.transform;
                rt.SetParent(hudPanel.transform, false);
                participantStripView = container.AddComponent<ParticipantHudStripView>();
            }
            if (participantStripView != null)
            {
                participantStripView.SetTopLeftAnchor(true);
                participantStripView.Initialize(4, mapSize, stripOffset, NameplateFont);
                participantStripInitialized = true;
                SyncMinimapAndParticipantStripLayout();
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
            if (networkStatusBannerInitialized && networkStatusBannerRoot != null && networkStatusBannerText != null) return;
            if (networkStatusBannerRoot != null && networkStatusBannerText != null)
            {
                networkStatusBannerInitialized = true;
                return;
            }
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
            if (promptImages != null) promptImages.Refresh(showPrompts && Application.platform != RuntimePlatform.Android, usingGamepad, !paused && !resultShown, paused || settingsOpen);
        }

        public void Pause() { if (match == null || resultShown) return; if (touchControls != null) touchControls.Cancel(); if (match.IsReplay) { var replay = match.GetComponent<Replay.ReplayPlayer>(); if (replay != null) replay.TogglePause(); return; } if (match.IsNetworkMovementTest) networkMenuOpen = true; else match.Pause(); RefreshPanels(); }
        public void Resume() { if (match == null) return; match.SuppressGamepadActionsUntilReleased(); settingsOpen = false; networkMenuOpen = false; match.Resume(); RefreshPanels(); }
        public void OpenSettings()
        {
            if (match == null || (!match.IsPaused && !networkMenuOpen)) return;
            settingsOpen = true;
            volumeSlider.SetValueWithoutNotify(gameAudio.Volume);
            UpdateVolumeLabel(gameAudio.Volume);
            if (joystickModeToggle != null && touchControls != null)
                joystickModeToggle.SetIsOnWithoutNotify(touchControls.JoystickMode == TouchJoystickMode.Floating);
            RefreshJoystickModeLabel();
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
