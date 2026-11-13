using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GameUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValueText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Image healthFill;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text resultCoinsText;
    [SerializeField] private GameAudio gameAudio;
    [SerializeField] private Toggle promptsToggle;
    [SerializeField] private TMP_Text promptsText;
    private const string PromptsPreference = "GameUI.ShowPrompts";
    private bool showPrompts;
    private bool usingGamepad;
    [SerializeField, Min(1)] private int warningSeconds = 10;
    [SerializeField] private Color normalTimeColor = Color.white;
    [SerializeField] private Color warningTimeColor = new Color(1f, 0.32f, 0.24f);
    [SerializeField, Min(0.01f)] private float pulseDuration = 0.3f;
    [SerializeField, Min(1f)] private float pulseScale = 1.18f;

    private bool settingsOpen;
    private float pulseRemaining;
    private Vector3 timeBaseScale;

    private GameManager.GameState? lastState;
    private int lastTime = -1;
    private int lastCoins = -1;
    private int lastHealth = -1;

    private void Awake()
    {
        if (timeText != null) timeBaseScale = timeText.rectTransform.localScale;
        showPrompts = PlayerPrefs.GetInt(PromptsPreference, 1) != 0;
        if (promptsToggle != null) promptsToggle.SetIsOnWithoutNotify(showPrompts);
    }

    private void RefreshPanels()
    {
        var state = gameManager.State;
        startPanel.SetActive(!settingsOpen && state == GameManager.GameState.Waiting);
        hudPanel.SetActive(state == GameManager.GameState.Playing || state == GameManager.GameState.Paused);
        pausePanel.SetActive(!settingsOpen && state == GameManager.GameState.Paused);
        settingsPanel.SetActive(settingsOpen);
        resultPanel.SetActive(!settingsOpen && (state == GameManager.GameState.Won || state == GameManager.GameState.Lost));
        var activePanel = settingsOpen ? settingsPanel : state == GameManager.GameState.Waiting ? startPanel : state == GameManager.GameState.Paused ? pausePanel : state == GameManager.GameState.Won || state == GameManager.GameState.Lost ? resultPanel : null;
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            if (activePanel != null)
            {
                var first = activePanel.GetComponentInChildren<Selectable>();
                if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }
        RefreshPrompts();
    }

    public void OpenSettings()
    {
        if (gameManager.State != GameManager.GameState.Waiting && gameManager.State != GameManager.GameState.Paused) return;
        settingsOpen = true;
        volumeSlider.SetValueWithoutNotify(gameAudio.Volume);
        UpdateVolumeLabel(gameAudio.Volume);
        RefreshPanels();
    }

    public void CloseSettings()
    {
        settingsOpen = false;
        RefreshPanels();
    }

    public void SetVolume(float value)
    {
        gameAudio.SetVolume(value);
        UpdateVolumeLabel(gameAudio.Volume);
    }

    public void SetPromptsVisible(bool visible)
    {
        showPrompts = visible;
        PlayerPrefs.SetInt(PromptsPreference, visible ? 1 : 0);
        PlayerPrefs.Save();
        RefreshPrompts();
    }

    private void RefreshPrompts()
    {
        if (promptsText == null || gameManager == null) return;
        promptsText.gameObject.SetActive(showPrompts);
        if (!showPrompts) return;
        bool playing = gameManager.State == GameManager.GameState.Playing && !settingsOpen;
        string back = settingsOpen || gameManager.State == GameManager.GameState.Paused ? (usingGamepad ? "     B  返回" : "     Esc  返回") : "";
        promptsText.text = usingGamepad
            ? (playing ? "左摇杆  移动     Menu  暂停" : "左摇杆  选择     A  确认" + back)
            : (playing ? "WASD / 方向键  移动     Esc  暂停" : "方向键  选择     Enter  确认" + back);
    }

    private void UpdateVolumeLabel(float value)
    {
        volumeValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private void Update()
    {
        if (gameManager == null) return;
        bool keyboard = Input.anyKeyDown && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow));
        bool gamepad = Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.JoystickButton7);
        if (!keyboard && !Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.D) && !Input.GetKey(KeyCode.UpArrow) && !Input.GetKey(KeyCode.DownArrow) && !Input.GetKey(KeyCode.LeftArrow) && !Input.GetKey(KeyCode.RightArrow))
            gamepad |= Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.2f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.2f;
        if ((keyboard && usingGamepad) || (gamepad && !usingGamepad))
        {
            usingGamepad = !keyboard;
            RefreshPrompts();
        }
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || ((settingsOpen || gameManager.State == GameManager.GameState.Paused) && Input.GetKeyDown(KeyCode.JoystickButton1)))
        {
            if (settingsOpen) CloseSettings();
            else if (gameManager.State == GameManager.GameState.Playing) gameManager.PauseGame();
            else if (gameManager.State == GameManager.GameState.Paused) gameManager.ResumeGame();
        }
        var state = gameManager.State;
        if (lastState != state)
        {
            RefreshPanels();
            resultText.text = state == GameManager.GameState.Won ? "游戏胜利" : "游戏结束";
            if (state != GameManager.GameState.Playing)
            {
                pulseRemaining = 0f;
                timeText.rectTransform.localScale = timeBaseScale;
            }
            lastState = state;
        }
        int time = Mathf.CeilToInt(gameManager.RemainingTime);
        if (time != lastTime)
        {
            timeText.text = time.ToString();
            bool warning = time > 0 && time <= warningSeconds;
            timeText.color = warning ? warningTimeColor : normalTimeColor;
            if (warning && state == GameManager.GameState.Playing && lastTime >= 0 && time < lastTime)
            {
                pulseRemaining = pulseDuration;
                if (gameAudio != null) gameAudio.PlayCountdown();
            }
            lastTime = time;
        }
        if (pulseRemaining > 0f)
        {
            pulseRemaining = Mathf.Max(0f, pulseRemaining - Time.deltaTime);
            float progress = 1f - pulseRemaining / Mathf.Max(0.01f, pulseDuration);
            timeText.rectTransform.localScale = timeBaseScale * (1f + (pulseScale - 1f) * Mathf.Sin(progress * Mathf.PI));
        }
        if (lastCoins != gameManager.CoinsCollected)
        {
            coinText.text = $"金币：{gameManager.CoinsCollected} / {gameManager.CoinsToWin}";
            resultCoinsText.text = $"金币：{gameManager.CoinsCollected} / {gameManager.CoinsToWin}";
            lastCoins = gameManager.CoinsCollected;
        }
        if (lastHealth != gameManager.CurrentHealth)
        {
            healthText.text = $"生命：{gameManager.CurrentHealth} / {gameManager.MaxHealth}";
            healthFill.fillAmount = (float)gameManager.CurrentHealth / Mathf.Max(1, gameManager.MaxHealth);
            lastHealth = gameManager.CurrentHealth;
        }
    }
}
