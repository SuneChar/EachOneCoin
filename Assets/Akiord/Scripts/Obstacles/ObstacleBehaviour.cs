using UnityEngine;

public abstract class ObstacleBehaviour : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    private bool behaviourRunning = true;
    private EachOneCoin.Gameplay.MatchCoordinator arenaMatch;

    protected bool CanRun => behaviourRunning && (arenaMatch != null
        ? arenaMatch.Model != null && arenaMatch.Model.Status == EachOneCoin.MatchStatus.Playing
        : gameManager != null && gameManager.IsPlaying);

    public void BindArena(EachOneCoin.Gameplay.MatchCoordinator match)
    {
        arenaMatch = match;
    }

    public void SetRunning(bool running)
    {
        behaviourRunning = running;
    }
}
