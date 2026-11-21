using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerHazardCollision : MonoBehaviour
{
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var obstacle = hit.collider.GetComponent<LethalObstacle>();
        if (obstacle != null)
            obstacle.OnPlayerContact(hit.controller);
    }
}
