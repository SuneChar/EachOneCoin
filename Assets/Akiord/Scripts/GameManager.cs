using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private float gameDuration = 60f;
    [SerializeField] private int coinsToWin = 10;
    [SerializeField] private PlayerController playerController;

    private float remainingTime;
    private int coinsCollected;
    private GameState state;

    private enum GameState { Playing, Won, Lost }

    public bool IsPlaying => state == GameState.Playing;

    private void Start()
    {
        remainingTime = gameDuration;
        state = GameState.Playing;
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
        if (coinsCollected >= coinsToWin) EndGame(GameState.Won);
        return true;
    }

    private void EndGame(GameState result)
    {
        state = result;
        if (playerController != null) playerController.enabled = false;
    }

    private void OnGUI()
    {
        var label = new GUIStyle(GUI.skin.label) { fontSize = 24, normal = { textColor = Color.white } };
        GUI.Label(new Rect(20, 15, 300, 35), $"Time: {Mathf.CeilToInt(remainingTime)}", label);
        GUI.Label(new Rect(20, 50, 300, 35), $"Coins: {coinsCollected} / {coinsToWin}", label);
        if (IsPlaying) return;
        var result = new GUIStyle(label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 80), state == GameState.Won ? "You Win!" : "Time Up!", result);
    }
}
