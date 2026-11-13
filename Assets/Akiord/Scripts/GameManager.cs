using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private float gameDuration = 60f;
    [SerializeField] private int coinsToWin = 10;
    [SerializeField] private PlayerController playerController;
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField] private GameAudio gameAudio;

    private float remainingTime;
    private int coinsCollected;
    private int currentHealth;
    private GameState state = GameState.Waiting;

    public enum GameState { Waiting, Playing, Won, Lost }

    public bool IsPlaying => state == GameState.Playing;
    public GameState State => state;
    public float RemainingTime => remainingTime;
    public int CoinsCollected => coinsCollected;
    public int CoinsToWin => coinsToWin;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;

    private void Awake()
    {
        remainingTime = gameDuration;
        currentHealth = Mathf.Max(1, maxHealth);
        if (playerController != null) playerController.enabled = false;
    }

    public void StartGame()
    {
        if (state != GameState.Waiting) return;
        state = GameState.Playing;
        if (gameAudio != null) gameAudio.PlayConfirm();
        if (playerController != null) playerController.enabled = true;
    }

    private void Update()
    {
        if (!IsPlaying) return;
        remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
        if (remainingTime <= 0f) EndGame(GameState.Lost);
    }

    public bool CollectCoin()
    {
        if (!IsPlaying || remainingTime <= 0f) return false;
        coinsCollected++;
        if (gameAudio != null) gameAudio.PlayCoin();
        if (coinsCollected >= coinsToWin) EndGame(GameState.Won);
        return true;
    }

    public void TakeDamage(int amount)
    {
        if (!IsPlaying || amount <= 0) return;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (currentHealth == 0) EndGame(GameState.Lost);
    }

    public void RestartGame()
    {
        if (gameAudio != null) gameAudio.PlayRestart();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void EndGame(GameState result)
    {
        state = result;
        if (gameAudio != null)
        {
            if (result == GameState.Won) gameAudio.PlayWin();
            else gameAudio.PlayLose();
        }
        if (playerController != null) playerController.enabled = false;
    }
}
