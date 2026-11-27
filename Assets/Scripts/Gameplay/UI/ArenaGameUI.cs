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
        [SerializeField] private MatchCoordinator match;
        [SerializeField] private GameAudio gameAudio;
        [SerializeField] private GameObject hudPanel, pausePanel, settingsPanel, resultPanel;
        [SerializeField] private TMP_Text timeText, coinText, healthText, resultText, resultCoinsText, participantText, feedbackText, attackPromptText, volumeValueText;
        [SerializeField] private Image healthFill;
        [SerializeField] private HealthHeartsView healthHeartsView;
        [SerializeField] private Image staminaFill;
        [SerializeField] private TMP_Text staminaText;
        [SerializeField] private ArcStaminaBarView arcStaminaBarView;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle promptsToggle;
        [SerializeField] private ControlPromptImages promptImages;
        [SerializeField, Min(0.05f)] private float hitFlashDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] private float hitFrameOpacity = 0.65f;
        [SerializeField, Min(1f)] private float hitFrameThickness = 24f;
        private readonly Image[] hitFrame = new Image[4];
        private float hitFlashRemaining;
        private InputDeviceTracker inputDevice;
        private bool usingGamepad, showPrompts, settingsOpen, resultShown;
        private int lastTime = -1;
        private readonly int[] lastCoins = { -1, -1, -1, -1 };
        private readonly int[] lastHealth = { -1, -1, -1, -1 };
        private readonly int[] lastMaxHealth = { -1, -1, -1, -1 };
        private readonly ParticipantStatus[] lastStatus = new ParticipantStatus[4];
        private bool firstRefresh = true;
        private bool lastUpcomingWarning;
        private bool lastUpcomingConnected;
        private int lastUpcomingMine = int.MaxValue;
        private int lastUpcomingSeconds = -1;
        private bool networkMenuOpen;
        private bool heartsInitialized;
        private bool legacyHealthHidden;
        private bool staminaBarInitialized;
        private bool legacyStaminaHidden;
        private bool leftHudAdjusted;
        private bool networkHealthSynced;
        private bool networkStaminaSynced;
        private Camera cachedMainCamera;
        public TMPro.TMP_FontAsset NameplateFont => timeText != null ? timeText.font : null;
        public bool NetworkMenuOpen => networkMenuOpen || settingsOpen;

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
            var pauseButton = hudPanel != null ? hudPanel.transform.Find("PauseButton") : null;
            var selectable = pauseButton != null ? pauseButton.GetComponent<Selectable>() : null;
            if (selectable != null)
            {
                var navigation = selectable.navigation;
                navigation.mode = Navigation.Mode.None;
                selectable.navigation = navigation;
            }
            RefreshPanels();
        }

        private void OnEnable() { if (match != null) { match.ObstacleHit += OnObstacleHit; match.AttackHit += OnAttackHit; } }
        private void OnDisable() { if (match != null) { match.ObstacleHit -= OnObstacleHit; match.AttackHit -= OnAttackHit; } }
        private void OnDestroy() { if (inputDevice != null) inputDevice.DeviceChanged -= OnDeviceChanged; }

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
            EnsureHealthHearts(match.InitialHealth);
            EnsureArcStaminaBar();
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
            }
            if (model.Status == MatchStatus.Finished && !resultShown)
            {
                resultShown = true;
                var local = model.Participants[0];
                resultText.text = local.Status == ParticipantStatus.Won ? "游戏胜利" : "游戏结束";
                resultCoinsText.text = $"金币：{local.Coins} / {model.CoinsToWin}";
                if (gameAudio != null) { if (local.Status == ParticipantStatus.Won) gameAudio.PlayWin(); else gameAudio.PlayLose(); }
                RefreshPanels();
            }
            int time = Mathf.CeilToInt(model.RemainingTime);
            if (time != lastTime)
            {
                timeText.text = time.ToString();
                if (lastTime > time && time > 0 && time <= 10 && gameAudio != null) gameAudio.PlayCountdown();
                lastTime = time;
            }
            bool changed = firstRefresh;
            for (int i = 0; i < model.Participants.Count; i++)
            {
                var p = model.Participants[i];
                if (lastCoins[i] != p.Coins || lastHealth[i] != p.Health || lastMaxHealth[i] != p.MaxHealth || lastStatus[i] != p.Status) changed = true;
                lastCoins[i] = p.Coins; lastHealth[i] = p.Health; lastMaxHealth[i] = p.MaxHealth; lastStatus[i] = p.Status;
            }
            if (changed)
            {
                var p = model.Participants[0];
                coinText.text = $"金币：{p.Coins} / {model.CoinsToWin}";
                if (healthHeartsView != null) healthHeartsView.SetHealth(p.Health, p.MaxHealth, !firstRefresh);
                var text = new System.Text.StringBuilder();
                for (int i = 0; i < model.Participants.Count; i++)
                {
                    var player = model.Participants[i];
                    var color = ColorUtility.ToHtmlStringRGB(match.Grid.PlayerColor(i));
                    text.Append($"<color=#{color}>●</color> {(i == 0 ? "玩家" : "电脑 " + i)}  {player.Coins}/{model.CoinsToWin}  {StatusText(player.Status)}\n");
                }
                participantText.text = text.ToString();
            }
            var localPlayer = model.Participants[0];
            bool mineChanged = changed;
            int upcomingMine = -1;
            float upcomingSeconds = 0f;
            bool upcomingWarning = false;
            bool hasUpcoming = match.Coins != null && match.Coins.TryDescribeNextEvent(out upcomingMine, out upcomingSeconds, out upcomingWarning);
            bool upcomingConnected = hasUpcoming && upcomingMine >= 0 && match.Coins.IsMineConnected(upcomingMine, 0);
            int upcomingCountdown = hasUpcoming ? Mathf.CeilToInt(upcomingSeconds) : -1;
            if (lastUpcomingMine != upcomingMine || lastUpcomingWarning != upcomingWarning || lastUpcomingSeconds != upcomingCountdown)
            {
                mineChanged = true;
                lastUpcomingMine = upcomingMine; lastUpcomingWarning = upcomingWarning; lastUpcomingSeconds = upcomingCountdown;
            }
            if (lastUpcomingConnected != upcomingConnected)
            {
                mineChanged = true;
                lastUpcomingConnected = upcomingConnected;
            }
            if (mineChanged)
            {
                if (localPlayer.Status != ParticipantStatus.Active) feedbackText.text = StatusText(localPlayer.Status);
                else
                {
                    var text = new System.Text.StringBuilder(hasUpcoming ? "下一批金币：" : "金币已全部产出");
                    if (hasUpcoming)
                    {
                        if (upcomingMine >= 0) text.Append(match.Coins.MineName(upcomingMine)[0]).Append("矿 ").Append(upcomingConnected ? "已连通 " : "未连通 ");
                        else text.Append("任一矿 ");
                        text.Append(upcomingCountdown).Append('s');
                        if (upcomingWarning) text.Append("（预警中）");
                    }
                    text.Append("\n预警圈出现后 ").Append(CoinSpawnSchedule.WarningSeconds.ToString("0.#")).Append(" 秒生成金币，需涂色连通才能拾取");
                    feedbackText.text = text.ToString();
                }
            }
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
            int time = Mathf.CeilToInt(state.RemainingTime);
            if (time != lastTime)
            {
                if (lastTime > time && time > 0 && time <= 10 && gameAudio != null) gameAudio.PlayCountdown();
                lastTime = time;
                timeText.text = time.ToString();
            }
            EnsureHealthHearts(match.InitialHealth);
            int coins = state.ScoreAt(slot), health = state.HealthAt(slot), maxHealth = state.MaxHealthAt(slot);
            if (!firstRefresh && !Mirror.NetworkServer.active && !network.ClientResuming && coins > lastCoins[slot] && gameAudio != null) gameAudio.PlayCoin();
            lastCoins[slot] = coins;
            coinText.text = $"金币：{coins} / {match.Model.CoinsToWin}";
            bool waitingFirstReplica = !Mirror.NetworkServer.active && !networkHealthSynced && maxHealth == 0 && state.StatusAt(slot) == ParticipantStatus.Active;
            if (!waitingFirstReplica)
            {
                bool isInitialSync = !networkHealthSynced;
                bool healthChanged = isInitialSync || lastHealth[slot] != health || lastMaxHealth[slot] != maxHealth;
                lastHealth[slot] = health;
                lastMaxHealth[slot] = maxHealth;
                networkHealthSynced = true;
                if (healthChanged && healthHeartsView != null)
                    healthHeartsView.SetHealth(health, maxHealth, !isInitialSync && !network.ClientResuming);
            }
            if (healthHeartsView != null) healthHeartsView.Tick(Time.deltaTime);
            EnsureArcStaminaBar();
            if (arcStaminaBarView != null)
            {
                Transform localTarget = match.Characters != null && slot < match.Characters.Length && match.Characters[slot] != null
                    ? match.Characters[slot].transform
                    : null;
                arcStaminaBarView.SetTarget(localTarget);
                bool canShowStamina = !NetworkMenuOpen && !network.ClientResuming && !state.IsSpectating &&
                                      state.CurrentMatchStatus == MatchStatus.Playing &&
                                      state.StatusAt(slot) == ParticipantStatus.Active;
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
            var participants = new System.Text.StringBuilder();
            for (int i = 0; i < 4; i++) if (state.IsParticipant(i))
            {
                var color = ColorUtility.ToHtmlStringRGB(match.Grid.PlayerColor(i));
                participants.Append($"<color=#{color}>●</color> {(i == slot ? "你" : "席位 " + (i + 1))}  {state.ScoreAt(i)}/{match.Model.CoinsToWin}  {StatusText(state.StatusAt(i))}\n");
            }
            participantText.text = participants.ToString();
            var info = new System.Text.StringBuilder();
            if (network.ClientResuming) info.Append("正在恢复对局\n");
            bool upcoming = match.Coins.TryDescribeNextEvent(out int mine, out float seconds, out bool warning);
            if (upcoming)
            {
                info.Append("下一批金币：");
                if (mine >= 0) info.Append(match.Coins.MineName(mine)[0]).Append("矿 ").Append(match.Coins.IsMineConnected(mine, slot) ? "已连通 " : "未连通 ");
                else info.Append("任一矿 ");
                info.Append(Mathf.CeilToInt(seconds)).Append('s');
                if (warning) info.Append("（预警中）");
            }
            else info.Append("金币已全部产出");
            info.Append("\n预警圈出现后 ").Append(CoinSpawnSchedule.WarningSeconds.ToString("0.#")).Append(" 秒生成金币，需涂色连通才能拾取");
            var status = state.StatusAt(slot);
            if (status != ParticipantStatus.Active) info.Append("\n").Append(StatusText(status)).Append(state.CurrentMatchStatus == MatchStatus.Playing ? "，其他玩家继续" : "");
            if (state.IsSpectating)
            {
                int target = state.SpectateSlot;
                info.Append(target < 0 ? "\n等待其他参与者结束" : "\n观战 " + (state.IsHuman(target) ? "席位 " : "NPC ") + (target + 1) + "（Tab / RB 切换）");
            }
            feedbackText.text = info.ToString();
            if (!resultShown && state.CurrentMatchStatus == MatchStatus.Finished)
            {
                resultShown = true; networkMenuOpen = false; settingsOpen = false;
                resultText.text = status == ParticipantStatus.Won ? "游戏胜利" : "游戏结束";
                resultCoinsText.text = BuildNetworkRanking(state, slot);
                if (gameAudio != null) { if (status == ParticipantStatus.Won) gameAudio.PlayWin(); else gameAudio.PlayLose(); }
                RefreshPanels();
            }
            if (resultShown && network.LocalReturned)
            {
                resultText.text = "已返回房间，等待其他玩家";
                var exit = resultPanel.transform.Find("MainMenuButton");
                if (exit != null) { var label = exit.GetComponentInChildren<TMP_Text>(true); if (label != null) label.text = "退出房间"; }
                var waiting = new System.Text.StringBuilder();
                for (int i = 0; i < 4; i++) if ((network.Snapshot.occupiedMask & (1 << i)) != 0)
                    waiting.Append("席位 ").Append(i + 1).Append((network.ReturnedMask & (1 << i)) != 0 ? "：已返回\n" : "：查看结算中\n");
                resultCoinsText.text = waiting.ToString();
            }
            firstRefresh = false;
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

        private void AdjustLeftHudBelowHearts()
        {
            if (leftHudAdjusted || healthHeartsView == null || coinText == null) return;
            leftHudAdjusted = true;

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
            if (feedbackText != null)
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
