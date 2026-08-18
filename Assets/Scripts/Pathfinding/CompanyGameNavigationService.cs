using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Soft-coded navigation service. It knows only the graph and path policy.
/// No employee, corridor or room-specific assumptions live here.
/// </summary>
public sealed class CompanyGameNavigationService
{
    private readonly CompanyGameNavigationGraph graph;

    public CompanyGameNavigationService(CompanyGameNavigationGraph graph)
    {
        this.graph = graph;
    }

    public CompanyGamePath FindPath(Vector3 startPosition, Vector3 goalPosition, int floor, float nodeSnapDistance)
    {
        if (graph == null) return new CompanyGamePath(null);
        graph.Refresh();
        CompanyGamePathNode start = graph.FindNearest(startPosition, floor, nodeSnapDistance);
        CompanyGamePathNode goal = graph.FindNearest(goalPosition, floor, nodeSnapDistance);
        return FindPath(start, goal);
    }

    public CompanyGamePath FindPath(CompanyGamePathNode start, CompanyGamePathNode goal)
    {
        if (start == null || goal == null) return new CompanyGamePath(null);
        if (start == goal) return new CompanyGamePath(new[] { start });

        var distance = new Dictionary<CompanyGamePathNode, float>();
        var previous = new Dictionary<CompanyGamePathNode, CompanyGamePathNode>();
        var open = new List<CompanyGamePathNode> { start };
        distance[start] = 0f;

        while (open.Count > 0)
        {
            CompanyGamePathNode current = Lowest(open, distance);
            open.Remove(current);
            if (current == goal) break;

            foreach (CompanyGamePathNode next in current.Connections)
            {
                if (next == null || next.Floor != start.Floor) continue;

                float edgeDistance = Vector3.Distance(current.transform.position, next.transform.position);
                float candidate = distance[current] + Mathf.Max(0.01f, edgeDistance) * next.MovementCost;

                if (!distance.TryGetValue(next, out float known) || candidate < known)
                {
                    distance[next] = candidate;
                    previous[next] = current;
                    if (!open.Contains(next)) open.Add(next);
                }
            }
        }

        if (!previous.ContainsKey(goal)) return new CompanyGamePath(null);

        var result = new List<CompanyGamePathNode> { goal };
        CompanyGamePathNode cursor = goal;
        int safety = 0;
        while (previous.TryGetValue(cursor, out CompanyGamePathNode parent))
        {
            cursor = parent;
            result.Add(cursor);
            if (cursor == start) break;
            if (++safety > 10000) return new CompanyGamePath(null);
        }

        if (result[result.Count - 1] != start) return new CompanyGamePath(null);
        result.Reverse();
        return new CompanyGamePath(result);
    }

    private static CompanyGamePathNode Lowest(List<CompanyGamePathNode> open, Dictionary<CompanyGamePathNode, float> distance)
    {
        CompanyGamePathNode result = open[0];
        float best = distance[result];

        for (int i = 1; i < open.Count; i++)
        {
            CompanyGamePathNode candidate = open[i];
            if (distance[candidate] < best)
            {
                best = distance[candidate];
                result = candidate;
            }
        }

        return result;
    }
}
