using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text timeText;
    [SerializeField] private Text coinText;
    [SerializeField] private Text healthText;
    [SerializeField] private Image healthFill;
    [SerializeField] private Text resultText;
    [SerializeField] private Text resultCoinsText;

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
            resultText.text = state == GameManager.GameState.Won ? "You Win!" : "Game Over";
            lastState = state;
        }
        int time = Mathf.CeilToInt(gameManager.RemainingTime);
        if (time != lastTime)
        {
            timeText.text = $"Time: {time}";
            lastTime = time;
        }
        if (lastCoins != gameManager.CoinsCollected)
        {
            coinText.text = $"Coins: {gameManager.CoinsCollected} / {gameManager.CoinsToWin}";
            resultCoinsText.text = $"Coins: {gameManager.CoinsCollected} / {gameManager.CoinsToWin}";
            lastCoins = gameManager.CoinsCollected;
        }
        if (lastHealth != gameManager.CurrentHealth)
        {
            healthText.text = $"HP: {gameManager.CurrentHealth} / {gameManager.MaxHealth}";
            healthFill.fillAmount = (float)gameManager.CurrentHealth / Mathf.Max(1, gameManager.MaxHealth);
            lastHealth = gameManager.CurrentHealth;
        }
    }
}
