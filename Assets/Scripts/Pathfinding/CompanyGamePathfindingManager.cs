using UnityEngine;

/// <summary>
/// Scene-level access point for pathfinding. Keeps consumers decoupled from the
/// concrete search implementation so the algorithm can be replaced later.
/// </summary>
public sealed class CompanyGamePathfindingManager : MonoBehaviour
{
    public static CompanyGamePathfindingManager Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad;

    private ICompanyGamePathfindingService service;

    public ICompanyGamePathfindingService Service => service;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        service = new CompanyGamePathfindingService();

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    public CompanyGamePath FindPath(CompanyGamePathNode start, CompanyGamePathNode goal)
    {
        EnsureService();
        return service.FindPath(start, goal);
    }

    public CompanyGamePath FindPath(Vector3 startPosition, Vector3 goalPosition, int floor = 0)
    {
        EnsureService();
        return service.FindPath(startPosition, goalPosition, floor);
    }

    public void RefreshGraph()
    {
        EnsureService();
        if (service is CompanyGamePathfindingService concrete)
            concrete.RefreshGraph();
    }

    private void EnsureService()
    {
        if (service == null)
            service = new CompanyGamePathfindingService();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
