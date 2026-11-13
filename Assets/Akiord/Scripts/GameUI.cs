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

    private GameManager.GameState? lastState;
    private int lastTime = -1;
    private int lastCoins = -1;
    private int lastHealth = -1;

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
            lastState = state;
        }
        int time = Mathf.CeilToInt(gameManager.RemainingTime);
        if (time != lastTime)
        {
            timeText.text = $"时间：{time}";
            lastTime = time;
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
