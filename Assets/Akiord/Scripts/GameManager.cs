using EachOneCoin;
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
    private float timeScaleBeforePause = 1f;

    public enum GameState { Waiting, Playing, Won, Lost, Paused }

    public bool IsPlaying => State == GameState.Playing;
    public GameState State
    {
        get
        {
            switch (GameArchitecture.Interface.GetModel<IGameModel>().State.Value)
            {
                case GameSessionState.Waiting: return GameState.Waiting;
                case GameSessionState.Playing: return GameState.Playing;
                case GameSessionState.Won: return GameState.Won;
                case GameSessionState.Lost: return GameState.Lost;
                case GameSessionState.Paused: return GameState.Paused;
                default: throw new System.InvalidOperationException("Unknown game session state.");
            }
        }
    }
    public float RemainingTime => remainingTime;
    public int CoinsCollected => GameArchitecture.Interface.GetModel<IGameModel>().Coins.Value;
    public int CoinsToWin => coinsToWin;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => GameArchitecture.Interface.GetModel<IGameModel>().Health.Value;

    private void Awake()
    {
        var model = GameArchitecture.Interface.GetModel<IGameModel>();
        model.ResetState();
        model.ResetCoins();
        model.ResetHealth(maxHealth);
        remainingTime = gameDuration;
        if (playerController != null) playerController.enabled = false;
    }

    private void Start()
    {
        StartGame();
    }

    public void StartGame()
    {
        if (State != GameState.Waiting) return;
        if (!GameArchitecture.Interface.GetModel<IGameModel>().TryChangeState(GameSessionState.Playing)) return;
        if (gameAudio != null) gameAudio.PlayConfirm();
        if (playerController != null) playerController.enabled = true;
    }

    public void PauseGame()
    {
        if (!IsPlaying) return;
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        if (!GameArchitecture.Interface.GetModel<IGameModel>().TryChangeState(GameSessionState.Paused))
        {
            Time.timeScale = timeScaleBeforePause;
            return;
        }
        if (playerController != null) playerController.enabled = false;
    }

    public void ResumeGame()
    {
        if (State != GameState.Paused) return;
        Time.timeScale = timeScaleBeforePause;
        if (!GameArchitecture.Interface.GetModel<IGameModel>().TryChangeState(GameSessionState.Playing)) return;
        if (playerController != null) playerController.enabled = true;
    }

    private void OnDestroy()
    {
        if (State == GameState.Paused) Time.timeScale = timeScaleBeforePause;
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
        int coinsCollected = GameArchitecture.Interface.SendCommand(new CollectCoinCommand());
        if (gameAudio != null) gameAudio.PlayCoin();
        if (coinsCollected >= coinsToWin) EndGame(GameState.Won);
        return true;
    }

    public void TakeDamage(int amount)
    {
        if (!IsPlaying || amount <= 0) return;
        int currentHealth = GameArchitecture.Interface.GetModel<IGameModel>().TakeDamage(amount);
        if (currentHealth == 0) EndGame(GameState.Lost);
    }

    public void ReturnToMainMenu()
    {
        if (State != GameState.Paused) return;
        Time.timeScale = timeScaleBeforePause;
        SceneManager.LoadScene("BootScene");
    }

    public void RestartGame()
    {
        if (State == GameState.Paused) Time.timeScale = timeScaleBeforePause;
        if (gameAudio != null) gameAudio.PlayRestart();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void EndGame(GameState result)
    {
        var next = result == GameState.Won ? GameSessionState.Won : GameSessionState.Lost;
        if (!GameArchitecture.Interface.GetModel<IGameModel>().TryChangeState(next)) return;
        if (gameAudio != null)
        {
            if (result == GameState.Won) gameAudio.PlayWin();
            else gameAudio.PlayLose();
        }
        if (playerController != null) playerController.enabled = false;
    }
}
