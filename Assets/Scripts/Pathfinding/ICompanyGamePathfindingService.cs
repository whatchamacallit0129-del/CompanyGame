using UnityEngine;

/// <summary>
/// Abstraction for movement route calculation.
/// Consumers do not need to know how the graph is searched.
/// </summary>
public interface ICompanyGamePathfindingService
{
    CompanyGamePath FindPath(CompanyGamePathNode start, CompanyGamePathNode goal);
    CompanyGamePath FindPath(Vector3 startWorldPosition, Vector3 goalWorldPosition, int floor = 0);
}
