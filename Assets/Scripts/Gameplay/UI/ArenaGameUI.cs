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
        [SerializeField] private Image staminaFill;
        [SerializeField] private TMP_Text staminaText;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle promptsToggle;
        [SerializeField] private ControlPromptImages promptImages;
        [SerializeField, Min(0.05f)] private float hitFlashDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] private float hitFrameOpacity = 0.65f;
        [SerializeField, Min(1f)] private float hitFrameThickness = 24f;
        private readonly Image[] hitFrame = new Image[4];
        private float hitFlashRemaining;
        private float hitTextRemaining;
        private InputDeviceTracker inputDevice;
        private bool usingGamepad, showPrompts, settingsOpen, resultShown;
        private int lastTime = -1;
        private readonly int[] lastCoins = { -1, -1, -1, -1 };
        private readonly int[] lastHealth = { -1, -1, -1, -1 };
        private readonly int[] lastMaxHealth = { -1, -1, -1, -1 };
        private readonly ParticipantStatus[] lastStatus = new ParticipantStatus[4];
        private bool firstRefresh = true;
        private readonly bool[] lastMineConnected = new bool[5];
        private readonly bool[] lastMineScattered = new bool[5];
        private readonly int[] lastMineProgress = { -1, -1, -1, -1, -1 };
        private float lastStamina = -1f;
        private TMP_Text networkCoinText;
        private float nextNetworkInfo;

        private void Awake()
        {
            inputDevice = GetComponent<InputDeviceTracker>();
            if (inputDevice != null) { usingGamepad = inputDevice.UsingGamepad; inputDevice.DeviceChanged += OnDeviceChanged; }
            showPrompts = PlayerPrefs.GetInt("GameUI.ShowPrompts", 1) != 0;
            if (feedbackText != null) { feedbackText.enableAutoSizing = true; feedbackText.fontSizeMin = 12f; feedbackText.fontSizeMax = 22f; }
            if (promptsToggle != null) promptsToggle.SetIsOnWithoutNotify(showPrompts);
            CreateHitFrame();
            RefreshPanels();
        }

        private void OnEnable() { if (match != null) match.ObstacleHit += OnObstacleHit; }
        private void OnDisable() { if (match != null) match.ObstacleHit -= OnObstacleHit; }
        private void OnDestroy() { if (inputDevice != null) inputDevice.DeviceChanged -= OnDeviceChanged; }

        private void OnObstacleHit(int id)
        {
            if (id != 0) return;
            hitFlashRemaining = Mathf.Max(0.05f, hitFlashDuration);
            hitTextRemaining = 0.8f;
            UpdateHitFrame();
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
                if (hudPanel != null) hudPanel.SetActive(false);
                if (pausePanel != null) pausePanel.SetActive(false);
                if (settingsPanel != null) settingsPanel.SetActive(false);
                if (resultPanel != null) resultPanel.SetActive(false);
                if (promptImages != null) promptImages.gameObject.SetActive(false);
                if (networkCoinText == null && coinText != null)
                {
                    networkCoinText = Instantiate(coinText, transform);
                    networkCoinText.name = "Network Coin Score";
                    networkCoinText.gameObject.SetActive(true);
                    var rect = networkCoinText.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(1f, 1f);
                    rect.anchoredPosition = new Vector2(-30f, -25f);
                    rect.sizeDelta = new Vector2(400f, 230f);
                }
                var network = Mirror.NetworkManager.singleton as EachOneCoin.Networking.LanNetworkManager;
                if (networkCoinText != null && network != null && match.NetworkController != null && Time.unscaledTime >= nextNetworkInfo)
                {
                    nextNetworkInfo = Time.unscaledTime + 0.25f;
                    int slot = network.LocalSlot;
                    string text = $"金币：{match.NetworkController.ScoreAt(slot)}（测试）";
                    text += $"\n血量：{match.NetworkController.HealthAt(slot)} / {match.NetworkController.MaxHealthAt(slot)}";
                    text += $"\n体力：{Mathf.CeilToInt(match.NetworkController.StaminaAt(slot))} / {Mathf.CeilToInt(match.StaminaAt(slot).Maximum)}";
                    var combatStatus = match.NetworkController.StatusAt(slot);
                    if (combatStatus == ParticipantStatus.Respawning) text += "\n复活中";
                    else if (combatStatus == ParticipantStatus.Lost) text += "\n已淘汰";
                    double elapsed = match.NetworkController.CoinTestElapsed;
                    if (elapsed >= 60d) text += "\n金币测试周期结束";
                    else if (elapsed >= 55d) text += "\n本局随机刷新结束";
                    else
                    {
                        double next = elapsed < 5d ? 5d : 5d + 10d * (System.Math.Floor((elapsed - 5d) / 10d) + 1d);
                        text += $"\n下次随机刷新：{Mathf.CeilToInt((float)(next - elapsed))}秒";
                    }
                    for (int i = 0; i < match.Model.Mines.Count; i++)
                        if (match.NetworkController.MineConnectedAt(i, slot))
                            text += $"\n{match.Coins.MineName(i)}金矿：{match.NetworkController.MineProgressAt(i, slot):F1} / 5秒";
                    networkCoinText.text = text;
                }
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || ((settingsOpen || match.IsPaused) && Input.GetKeyDown(KeyCode.JoystickButton1)))
            {
                if (settingsOpen) CloseSettings();
                else if (match.IsPaused) Resume();
                else Pause();
            }
            var model = match.Model;
            if (match.Stamina != null && match.Stamina.Current != lastStamina)
            {
                lastStamina = match.Stamina.Current;
                if (staminaFill != null)
                {
                    var rect = staminaFill.rectTransform;
                    rect.anchorMax = new Vector2(Mathf.Clamp01(lastStamina / match.Stamina.Maximum), rect.anchorMax.y);
                }
                if (staminaText != null) staminaText.text = $"体力：{Mathf.CeilToInt(lastStamina)} / {Mathf.CeilToInt(match.Stamina.Maximum)}";
            }
            bool hitTextExpired = false;
            if (!match.IsPaused)
            {
                if (hitFlashRemaining > 0f) { hitFlashRemaining = Mathf.Max(0f, hitFlashRemaining - Time.deltaTime); UpdateHitFrame(); }
                if (hitTextRemaining > 0f) { hitTextRemaining = Mathf.Max(0f, hitTextRemaining - Time.deltaTime); hitTextExpired = hitTextRemaining == 0f; }
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
                healthText.text = $"生命：{p.Health} / {p.MaxHealth}" + (hitTextRemaining > 0f ? "  上限 -1" : "");
                float healthFraction = Mathf.Clamp01((float)p.Health / match.InitialHealth);
                // Resize the sprite-less Image; fillAmount alone does not clip its quad.
                healthFill.type = Image.Type.Simple;
                var fillRect = healthFill.rectTransform;
                fillRect.anchorMax = new Vector2(healthFraction, fillRect.anchorMax.y);
                var text = new System.Text.StringBuilder();
                for (int i = 0; i < model.Participants.Count; i++)
                {
                    var player = model.Participants[i];
                    var color = ColorUtility.ToHtmlStringRGB(match.Grid.PlayerColor(i));
                    text.Append($"<color=#{color}>●</color> {(i == 0 ? "玩家" : "电脑 " + i)}  {player.Coins}/{model.CoinsToWin}  {StatusText(player.Status)}\n");
                }
                participantText.text = text.ToString();
            }
            if (hitTextExpired && !changed)
            {
                var p = model.Participants[0];
                healthText.text = $"生命：{p.Health} / {p.MaxHealth}";
            }
            var localPlayer = model.Participants[0];
            bool mineChanged = changed;
            for (int i = 0; i < model.Mines.Count; i++)
            {
                bool isConnected = match.Coins.IsConnectedToMine(i, 0);
                bool scattered = model.Mines[i].Scattered;
                int progress = Mathf.FloorToInt((float)model.Mines[i].Progress(0));
                if (lastMineConnected[i] != isConnected || lastMineScattered[i] != scattered || lastMineProgress[i] != progress) mineChanged = true;
                lastMineConnected[i] = isConnected; lastMineScattered[i] = scattered; lastMineProgress[i] = progress;
            }
            if (mineChanged)
            {
                if (localPlayer.Status != ParticipantStatus.Active) feedbackText.text = StatusText(localPlayer.Status);
                else
                {
                    var text = new System.Text.StringBuilder("矿 ");
                    for (int i = 0; i < model.Mines.Count; i++)
                    {
                        if (i > 0) text.Append("  ");
                        text.Append(match.Coins.MineName(i)[0]).Append(':');
                        if (lastMineScattered[i]) text.Append("已产出");
                        else if (lastMineConnected[i]) text.Append(lastMineProgress[i]).Append("/5");
                        else text.Append("--");
                    }
                    text.Append("\n随机金币可直接拾取");
                    feedbackText.text = text.ToString();
                }
            }
            firstRefresh = false;
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

        private void LateUpdate()
        {
            if (!usingGamepad || EventSystem.current == null || EventSystem.current.currentSelectedGameObject != null) return;
            var panel = settingsOpen ? settingsPanel : match != null && match.IsPaused ? pausePanel : resultShown ? resultPanel : null;
            if (panel == null || !panel.activeInHierarchy) return;
            var first = panel.GetComponentInChildren<Selectable>();
            if (first != null && first.IsInteractable()) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private void RefreshPanels()
        {
            bool paused = match != null && match.IsPaused;
            hudPanel.SetActive(!resultShown);
            pausePanel.SetActive(paused && !settingsOpen);
            settingsPanel.SetActive(settingsOpen);
            resultPanel.SetActive(resultShown && !settingsOpen);
            if (EventSystem.current != null)
            {
                EventSystem.current.sendNavigationEvents = usingGamepad;
                EventSystem.current.SetSelectedGameObject(null);
            }
            if (promptImages != null) promptImages.Refresh(showPrompts, usingGamepad, !paused && !resultShown, paused || settingsOpen);
            if (attackPromptText != null)
            {
                attackPromptText.gameObject.SetActive(showPrompts && !paused && !resultShown);
                attackPromptText.text = usingGamepad ? "按住 X + 右摇杆瞄准，松开攻击" : "按住空格 + 鼠标瞄准，松开攻击";
            }
        }

        public void Pause() { if (match == null || resultShown) return; match.Pause(); RefreshPanels(); }
        public void Resume() { if (match == null) return; settingsOpen = false; match.Resume(); RefreshPanels(); }
        public void OpenSettings()
        {
            if (match == null || !match.IsPaused) return;
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
        public void Restart() { if (match != null) match.Resume(); if (gameAudio != null) gameAudio.PlayRestart(); SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
        public void MainMenu() { if (match != null) match.Resume(); SceneManager.LoadScene("BootScene"); }
    }
}
