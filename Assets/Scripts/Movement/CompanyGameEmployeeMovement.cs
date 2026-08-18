using UnityEngine;

/// <summary>
/// Generic movement controller for employees and future agents.
/// It consumes the pathfinding service and never edits the node graph itself.
/// </summary>
public sealed class CompanyGameEmployeeMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private int floor;
    [SerializeField] private bool useUnscaledTime;

    private CompanyGamePathfindingService pathfinding;
    private CompanyGamePath currentPath;
    private Vector3 destination;
    private int pathIndex;
    private bool moving;

    public bool IsMoving => moving;
    public Vector3 Destination => destination;
    public float MoveSpeed => Mathf.Max(0.01f, moveSpeed);

    private void Awake()
    {
        pathfinding = new CompanyGamePathfindingService();
    }

    private void Update()
    {
        if (!moving || currentPath == null || !currentPath.IsValid)
            return;

        float delta = (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * MoveSpeed;

        while (delta > 0f && pathIndex < currentPath.Nodes.Count)
        {
            CompanyGamePathNode node = currentPath.Nodes[pathIndex];
            if (node == null)
            {
                pathIndex++;
                continue;
            }

            Vector3 target = node.transform.position;
            Vector3 next = Vector3.MoveTowards(transform.position, target, delta);
            float used = Vector3.Distance(transform.position, next);
            transform.position = next;
            delta -= used;

            if (Vector3.Distance(transform.position, target) <= 0.01f)
                pathIndex++;
            else
                break;
        }

        if (pathIndex >= currentPath.Nodes.Count)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, delta);
            if (Vector3.Distance(transform.position, destination) <= 0.01f)
            {
                transform.position = destination;
                moving = false;
                currentPath = null;
            }
        }
    }

    public bool MoveTo(Vector3 worldPosition)
    {
        pathfinding.RefreshGraph();
        CompanyGamePath path = pathfinding.FindPath(transform.position, worldPosition, floor);

        if (!path.IsValid)
        {
            moving = false;
            currentPath = null;
            Debug.LogWarning($"[Company Game] No movement route found for {name}.", this);
            return false;
        }

        destination = worldPosition;
        currentPath = path;
        pathIndex = path.Nodes.Count > 0 && path.Nodes[0] != null &&
                     Vector3.Distance(transform.position, path.Nodes[0].transform.position) <= 0.01f
            ? 1
            : 0;
        moving = true;
        return true;
    }

    public void StopMovement()
    {
        moving = false;
        currentPath = null;
    }

    public void SetFloor(int value)
    {
        floor = value;
    }
}
