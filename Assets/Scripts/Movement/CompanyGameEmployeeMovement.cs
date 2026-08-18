using UnityEngine;

/// <summary>
/// Movement agent for employees and future controllable actors.
/// Movement tuning is data-driven; navigation only supplies a path.
/// </summary>
public sealed class CompanyGameEmployeeMovement : MonoBehaviour
{
    [Header("Movement Policy")]
    [SerializeField] private CompanyGameEmployeeMovementSettings settings;
    [SerializeField] private float moveSpeedOverride = 0f;
    [SerializeField] private int floor;

    private CompanyGameNavigationService navigation;
    private CompanyGamePath currentPath;
    private Vector3 destination;
    private int pathIndex;
    private bool moving;
    private float currentSpeed;

    public bool IsMoving => moving;
    public Vector3 Destination => destination;
    public float MoveSpeed => moveSpeedOverride > 0f ? moveSpeedOverride : (settings != null ? settings.MoveSpeed : 2f);
    public float CurrentSpeed => currentSpeed;
    public int Floor => floor;
    public CompanyGameEmployeeMovementSettings Settings => settings;
    public CompanyGamePath CurrentPath => currentPath;
    public CompanyGamePathNode CurrentTargetNode => currentPath != null && currentPath.IsValid && pathIndex < currentPath.Nodes.Count
        ? currentPath.Nodes[pathIndex]
        : null;

    private void Awake()
    {
        navigation = new CompanyGameNavigationService(CompanyGameNavigationGraph.Instance);
    }

    private void Update()
    {
        if (!moving || currentPath == null || !currentPath.IsValid)
        {
            currentSpeed = 0f;
            return;
        }

        float deltaTime = settings != null && settings.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float targetSpeed = MoveSpeed;
        float acceleration = settings != null ? settings.Acceleration : 8f;
        float deceleration = settings != null ? settings.Deceleration : 12f;

        bool approachingFinalDestination = pathIndex >= currentPath.Nodes.Count - 1;
        float distanceToTarget = CurrentTargetNode != null
            ? Vector3.Distance(transform.position, CurrentTargetNode.transform.position)
            : Vector3.Distance(transform.position, destination);

        float slowdownDistance = Mathf.Max(0.15f, targetSpeed * targetSpeed / (2f * deceleration));
        if (approachingFinalDestination && distanceToTarget < slowdownDistance)
            targetSpeed *= Mathf.Clamp01(distanceToTarget / slowdownDistance);

        float rate = targetSpeed > currentSpeed ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * deltaTime);

        float remaining = deltaTime * currentSpeed;
        float nodeArrivalDistance = settings != null ? settings.NodeArrivalDistance : 0.06f;
        float destinationArrivalDistance = settings != null ? settings.DestinationArrivalDistance : 0.06f;

        while (remaining > 0f && pathIndex < currentPath.Nodes.Count)
        {
            CompanyGamePathNode node = currentPath.Nodes[pathIndex];
            if (node == null)
            {
                pathIndex++;
                continue;
            }

            Vector3 target = node.transform.position;
            Vector3 next = Vector3.MoveTowards(transform.position, target, remaining);
            remaining -= Vector3.Distance(transform.position, next);
            transform.position = next;

            if (Vector3.Distance(transform.position, target) <= nodeArrivalDistance)
                pathIndex++;
            else
                break;
        }

        if (pathIndex >= currentPath.Nodes.Count)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, remaining);
            if (Vector3.Distance(transform.position, destination) <= destinationArrivalDistance)
            {
                transform.position = destination;
                moving = false;
                currentPath = null;
                pathIndex = 0;
                currentSpeed = 0f;
            }
        }
    }

    public bool MoveTo(Vector3 worldPosition)
    {
        float snapDistance = settings != null ? settings.NodeSnapDistance : 2.5f;
        CompanyGamePath path = navigation.FindPath(transform.position, worldPosition, floor, snapDistance);
        if (!path.IsValid)
        {
            StopMovement();
            Debug.LogWarning($"[Company Game] No reachable route for {name}. Destination is not on a reachable navigation network.", this);
            return false;
        }

        destination = worldPosition;
        currentPath = path;
        pathIndex = path.Nodes.Count > 0 &&
                    Vector3.Distance(transform.position, path.Nodes[0].transform.position) <= (settings != null ? settings.NodeArrivalDistance : 0.06f)
            ? 1
            : 0;
        currentSpeed = 0f;
        moving = true;
        return true;
    }

    public void StopMovement()
    {
        moving = false;
        currentPath = null;
        pathIndex = 0;
        currentSpeed = 0f;
    }

    public void SetFloor(int value) => floor = value;
}
