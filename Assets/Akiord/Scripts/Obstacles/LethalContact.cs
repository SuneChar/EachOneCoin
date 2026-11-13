using UnityEngine;

public class LethalContact : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private TimedSpikeTrap spikeTrap;

    private void OnTriggerEnter(Collider other)
    {
        TryKillPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryKillPlayer(other);
    }

    public void TryKillPlayer(Collider other)
    {
        if (gameManager == null || !gameManager.IsPlaying) return;
        if (spikeTrap != null && !spikeTrap.IsVisible) return;
        if (!other.CompareTag("Player") || other.GetComponent<CharacterController>() == null) return;
        gameManager.TakeDamage(gameManager.CurrentHealth);
    }
}
