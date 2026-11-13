using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private float speed;
    [SerializeField] private Transform visualRoot;
    [SerializeField, Min(0.01f)] private float turnDuration = 0.15f;
    private CharacterController characterController;
    private Vector3 visualScale;
    private int facing = 1;
    private int desiredFacing = 1;
    private float turnTime;
    private bool turning;
    private bool turnFlipped;

    public Vector3 LastMoveDirection { get; private set; } = Vector3.forward;

    private void Start()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            characterController = player.GetComponent<CharacterController>();
        if (visualRoot != null)
        {
            visualScale = visualRoot.localScale;
            visualScale.x = Mathf.Abs(visualScale.x);
        }
    }

    private void Update()
    {
        if (characterController == null) return;
        Vector3 direction = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        if (direction.sqrMagnitude > 0f)
            LastMoveDirection = direction.normalized;
        if (direction.x != 0f)
            desiredFacing = direction.x < 0f ? -1 : 1;
        UpdateTurning(Time.deltaTime);
        characterController.Move(Vector3.ClampMagnitude(direction, 1f) * speed * Time.deltaTime);
    }

    private void UpdateTurning(float deltaTime)
    {
        if (visualRoot == null) return;
        if (!turning && facing != desiredFacing)
        {
            turning = true;
            turnFlipped = false;
            turnTime = 0f;
        }
        if (!turning) return;

        float duration = Mathf.Max(0.01f, turnDuration);
        turnTime = Mathf.Min(turnTime + deltaTime, duration);
        float progress = turnTime / duration;
        if (!turnFlipped && progress >= 0.5f)
        {
            facing = desiredFacing;
            turnFlipped = true;
        }
        float width = Mathf.Max(0.01f, Mathf.Abs(1f - 2f * progress));
        visualRoot.localScale = new Vector3(visualScale.x * facing * width, visualScale.y, visualScale.z);
        if (progress >= 1f) turning = false;
    }

    private void OnDisable()
    {
        if (visualRoot == null || visualScale.x == 0f) return;
        facing = desiredFacing;
        turning = false;
        visualRoot.localScale = new Vector3(visualScale.x * facing, visualScale.y, visualScale.z);
    }
}
