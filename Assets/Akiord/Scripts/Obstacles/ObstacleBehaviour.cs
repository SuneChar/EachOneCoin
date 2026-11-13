using UnityEngine;

public abstract class ObstacleBehaviour : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    private bool behaviourRunning = true;

    protected bool CanRun => behaviourRunning && gameManager != null && gameManager.IsPlaying;

    public void SetRunning(bool running)
    {
        behaviourRunning = running;
    }
}
