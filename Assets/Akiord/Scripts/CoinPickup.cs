using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    private bool collected;

    private void OnTriggerEnter(Collider other)
    {
        if (collected || !other.CompareTag("Player") || gameManager == null) return;
        if (!gameManager.CollectCoin()) return;
        collected = true;
        gameObject.SetActive(false);
    }
}
