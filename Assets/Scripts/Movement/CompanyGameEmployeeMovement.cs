using UnityEngine;

/// <summary>
/// Movement agent for employees and future controllable actors.
/// It knows how to move along a supplied navigation path, but knows nothing
/// about corridors, doors or how the graph was authored.
/// </summary>
public sealed class CompanyGameEmployeeMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private int floor;
    [SerializeField] private bool useUnscaledTime;

    private CompanyGameNavigationService navigation;
    private CompanyGamePath currentPath;
    private Vector3 destination;
    private int pathIndex;
    private bool moving;

    public bool IsMoving => moving;
    public Vector3 Destination => destination;
    public float MoveSpeed => Mathf.Max(0.01f, moveSpeed);
    public int Floor => floor;

    private void Awake()
    {
        navigation = new CompanyGameNavigationService(CompanyGameNavigationGraph.Instance);
    }

    private void Update()
    {
        if (!moving || currentPath == null || !currentPath.IsValid) return;

        float remaining = (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * MoveSpeed;

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

            if (Vector3.Distance(transform.position, target) <= 0.01f)
                pathIndex++;
            else
                break;
        }

        if (pathIndex >= currentPath.Nodes.Count)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, remaining);
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
        CompanyGamePath path = navigation.FindPath(transform.position, worldPosition, floor);
        if (!path.IsValid)
        {
            StopMovement();
            Debug.LogWarning($"[Company Game] No navigation route for {name}. Make sure the destination is on a reachable node network.", this);
            return false;
        }

        destination = worldPosition;
        currentPath = path;
        pathIndex = path.Nodes.Count > 0 &&
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
        pathIndex = 0;
    }

    public void SetFloor(int value) => floor = value;
}
