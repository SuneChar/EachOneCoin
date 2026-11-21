using UnityEngine;

public abstract class ObstacleBehaviour : MonoBehaviour
{
    private bool behaviourRunning = true;
    private EachOneCoin.Gameplay.MatchCoordinator arenaMatch;

    protected bool CanRun => behaviourRunning && arenaMatch != null && arenaMatch.Model != null
        && !arenaMatch.IsPaused && arenaMatch.Model.Status == EachOneCoin.MatchStatus.Playing;

    public void BindArena(EachOneCoin.Gameplay.MatchCoordinator match)
    {
        arenaMatch = match;
    }

    public void SetRunning(bool running)
    {
        behaviourRunning = running;
    }
}
