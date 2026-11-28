using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LinearPatrolObstacle : ObstacleBehaviour
{
    [SerializeField] private Vector3 endOffset = new Vector3(3f, 0f, 0f);
    [SerializeField, Min(0f)] private float speed = 1.5f;

    private Rigidbody body;
    private Vector3 startPoint;
    private Vector3 endPoint;
    private bool headingToEnd = true;
    private bool hasReplicaPosition;
    private Vector3 replicaPosition;
    public Vector3 NetworkPosition => body.position;

    public void ApplyNetworkPosition(Vector3 position)
    {
        replicaPosition = position;
        if (!hasReplicaPosition)
        {
            body.position = position;
            hasReplicaPosition = true;
        }
    }

    private void Update()
    {
        if (hasReplicaPosition) body.position = Vector3.Lerp(body.position, replicaPosition, 1f - Mathf.Exp(-12f * Time.deltaTime));
    }

    public void ConfigureSpeedBeforeActivation(float value)
    {
        speed = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        startPoint = body.position;
        endPoint = startPoint + endOffset;
    }

    private void FixedUpdate()
    {
        if (!CanRun || speed <= 0f || startPoint == endPoint) return;
        Vector3 target = headingToEnd ? endPoint : startPoint;
        Vector3 nextPosition = Vector3.MoveTowards(body.position, target, speed * Time.fixedDeltaTime);
        body.MovePosition(nextPosition);
        if (nextPosition == target) headingToEnd = !headingToEnd;
    }
}
