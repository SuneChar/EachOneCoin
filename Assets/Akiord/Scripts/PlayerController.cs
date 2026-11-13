using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private float speed;
    private CharacterController characterController;

    private void Start()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            characterController = player.GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (characterController == null) return;
        Vector3 direction = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        characterController.Move(Vector3.ClampMagnitude(direction, 1f) * speed * Time.deltaTime);
    }
}
