using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Default graph pathfinder. Uses Dijkstra so per-node movement costs can be
/// introduced now and richer traversal rules can be added later without changing callers.
/// </summary>
public sealed class CompanyGamePathfindingService : ICompanyGamePathfindingService
{
    private readonly List<CompanyGamePathNode> graph = new List<CompanyGamePathNode>();

    public CompanyGamePathfindingService(IEnumerable<CompanyGamePathNode> nodes = null)
    {
        if (nodes != null) graph.AddRange(nodes);
    }

    public CompanyGamePath FindPath(CompanyGamePathNode start, CompanyGamePathNode goal)
    {
        if (start == null || goal == null) return new CompanyGamePath(null);
        if (start == goal) return new CompanyGamePath(new[] { start });

        var distances = new Dictionary<CompanyGamePathNode, float>();
        var previous = new Dictionary<CompanyGamePathNode, CompanyGamePathNode>();
        var unvisited = new List<CompanyGamePathNode>();

        AddNode(start, distances, unvisited);
        AddNode(goal, distances, unvisited);

        while (unvisited.Count > 0)
        {
            CompanyGamePathNode current = GetLowestCostNode(unvisited, distances);
            if (current == null) break;
            unvisited.Remove(current);

            if (current == goal) break;

            foreach (CompanyGamePathNode neighbour in current.Connections)
            {
                if (neighbour == null) continue;
                AddNode(neighbour, distances, unvisited);

                float candidate = distances[current] + neighbour.MovementCost;
                if (candidate >= distances[neighbour]) continue;

                distances[neighbour] = candidate;
                previous[neighbour] = current;
            }
        }

        if (!previous.ContainsKey(goal)) return new CompanyGamePath(null);

        var result = new List<CompanyGamePathNode>();
        CompanyGamePathNode cursor = goal;
        result.Add(cursor);

        while (previous.TryGetValue(cursor, out CompanyGamePathNode parent))
        {
            cursor = parent;
            result.Add(cursor);
            if (cursor == start) break;
        }

        if (result[result.Count - 1] != start) return new CompanyGamePath(null);
        result.Reverse();
        return new CompanyGamePath(result);
    }

    public CompanyGamePath FindPath(Vector3 startWorldPosition, Vector3 goalWorldPosition, int floor = 0)
    {
        RefreshGraph();
        CompanyGamePathNode start = FindNearestNode(startWorldPosition, floor);
        CompanyGamePathNode goal = FindNearestNode(goalWorldPosition, floor);
        return FindPath(start, goal);
    }

    public void RefreshGraph()
    {
        graph.Clear();
        graph.AddRange(Object.FindObjectsByType<CompanyGamePathNode>());
    }

    private CompanyGamePathNode FindNearestNode(Vector3 position, int floor)
    {
        CompanyGamePathNode nearest = null;
        float best = float.MaxValue;

        foreach (CompanyGamePathNode node in graph)
        {
            if (node == null || node.Floor != floor) continue;
            float distance = (node.transform.position - position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = node;
            }
        }
        return nearest;
    }

    private static void AddNode(
        CompanyGamePathNode node,
        Dictionary<CompanyGamePathNode, float> distances,
        List<CompanyGamePathNode> unvisited)
    {
        if (distances.ContainsKey(node)) return;
        distances[node] = float.MaxValue;
        unvisited.Add(node);
    }

    private static CompanyGamePathNode GetLowestCostNode(
        List<CompanyGamePathNode> nodes,
        Dictionary<CompanyGamePathNode, float> distances)
    {
        CompanyGamePathNode result = null;
        float best = float.MaxValue;
        foreach (CompanyGamePathNode node in nodes)
        {
            if (!distances.TryGetValue(node, out float cost) || cost >= best) continue;
            best = cost;
            result = node;
        }
        return result;
    }
}
