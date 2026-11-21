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
