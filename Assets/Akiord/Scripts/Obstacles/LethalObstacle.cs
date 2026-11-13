using UnityEngine;

public class LethalObstacle : MonoBehaviour
{
    [SerializeField] private LethalContact lethalContact;

    public void OnPlayerContact(CharacterController player)
    {
        if (lethalContact != null)
            lethalContact.TryKillPlayer(player);
    }
}
