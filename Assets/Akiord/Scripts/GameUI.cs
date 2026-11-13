using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Image healthFill;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text resultCoinsText;
    [SerializeField] private GameAudio gameAudio;
    [SerializeField, Min(1)] private int warningSeconds = 10;
    [SerializeField] private Color normalTimeColor = Color.white;
    [SerializeField] private Color warningTimeColor = new Color(1f, 0.32f, 0.24f);
    [SerializeField, Min(0.01f)] private float pulseDuration = 0.3f;
    [SerializeField, Min(1f)] private float pulseScale = 1.18f;

    private float pulseRemaining;
    private Vector3 timeBaseScale;

    private GameManager.GameState? lastState;
    private int lastTime = -1;
    private int lastCoins = -1;
    private int lastHealth = -1;

    private void Awake()
    {
        if (timeText != null) timeBaseScale = timeText.rectTransform.localScale;
    }

    private void Update()
    {
        if (gameManager == null) return;
        var state = gameManager.State;
        if (lastState != state)
        {
            startPanel.SetActive(state == GameManager.GameState.Waiting);
            hudPanel.SetActive(state == GameManager.GameState.Playing);
            resultPanel.SetActive(state == GameManager.GameState.Won || state == GameManager.GameState.Lost);
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
