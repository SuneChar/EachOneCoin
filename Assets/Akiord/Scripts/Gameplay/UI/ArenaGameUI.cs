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
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle promptsToggle;
        [SerializeField] private ControlPromptImages promptImages;
        private InputDeviceTracker inputDevice;
        private bool usingGamepad, showPrompts, settingsOpen, resultShown;
        private int lastTime = -1;
        private readonly int[] lastCoins = { -1, -1, -1, -1 };
        private readonly int[] lastHealth = { -1, -1, -1, -1 };
        private readonly int[] lastMaxHealth = { -1, -1, -1, -1 };
        private readonly ParticipantStatus[] lastStatus = new ParticipantStatus[4];
        private bool firstRefresh = true;
        private bool? lastConnected;

        private void Awake()
        {
            inputDevice = GetComponent<InputDeviceTracker>();
            if (inputDevice != null) { usingGamepad = inputDevice.UsingGamepad; inputDevice.DeviceChanged += OnDeviceChanged; }
            showPrompts = PlayerPrefs.GetInt("GameUI.ShowPrompts", 1) != 0;
            if (promptsToggle != null) promptsToggle.SetIsOnWithoutNotify(showPrompts);
            RefreshPanels();
        }

        private void OnDestroy() { if (inputDevice != null) inputDevice.DeviceChanged -= OnDeviceChanged; }
        private void OnDeviceChanged(bool gamepad) { usingGamepad = gamepad; RefreshPanels(); }

        private void Update()
        {
            if (match == null || match.Model == null) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || ((settingsOpen || match.IsPaused) && Input.GetKeyDown(KeyCode.JoystickButton1)))
            {
                if (settingsOpen) CloseSettings();
                else if (match.IsPaused) Resume();
                else Pause();
            }
            var model = match.Model;
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
                healthText.text = $"生命：{p.Health} / {p.MaxHealth}";
                healthFill.fillAmount = (float)p.Health / Mathf.Max(1, p.MaxHealth);
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
            bool connected = localPlayer.Status == ParticipantStatus.Active && match.Grid.IsConnected(0, match.Grid.WorldToCell(match.Characters[0].transform.position));
            if (changed || lastConnected != connected)
            {
                feedbackText.text = localPlayer.Status == ParticipantStatus.Active ? (connected ? "路径已连接" : "路径未连接：无法拾取金币") : StatusText(localPlayer.Status);
                lastConnected = connected;
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
                attackPromptText.text = usingGamepad ? "X  攻击" : "空格  攻击";
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
