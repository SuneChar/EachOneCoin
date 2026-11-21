using UnityEngine;

public class PlayerDirectionIndicator : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Transform arrow;

    private void LateUpdate()
    {
        if (playerController == null || arrow == null) return;
        Vector3 direction = playerController.LastMoveDirection;
        if (direction.sqrMagnitude > 0f)
            arrow.localRotation = Quaternion.LookRotation(direction, Vector3.up);
    }
}
