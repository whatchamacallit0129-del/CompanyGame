using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime pathfinding over the corridor/node network.
/// Corridor authoring data is converted into a traversable graph at runtime.
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
        distances[start] = 0f;

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

        if (start == null || goal == null)
        {
            Debug.LogWarning($"[Company Game] Pathfinding could not find nodes. Start={startWorldPosition}, Goal={goalWorldPosition}, Floor={floor}");
            return new CompanyGamePath(null);
        }

        return FindPath(start, goal);
    }

    public void RefreshGraph()
    {
        graph.Clear();
        graph.AddRange(Object.FindObjectsByType<CompanyGamePathNode>());

        // Corridor nodes are a graph automatically: nodes belonging to the same
        // corridor are connected in their authoring order.
        CompanyGameCorridor[] corridors = Object.FindObjectsByType<CompanyGameCorridor>();
        foreach (CompanyGameCorridor corridor in corridors)
        {
            if (corridor == null || !corridor.Walkable) continue;

            CompanyGamePathNode previous = null;
            foreach (CompanyGamePathNode node in corridor.Nodes)
            {
                if (node == null) continue;

                // Keep node floor consistent with the owning corridor at runtime.
                SetNodeFloor(node, corridor.Floor);

                if (previous != null)
                    previous.ConnectTo(node);

                previous = node;
            }
        }

        // Explicit Corridor ↔ Corridor links become graph edges through the
        // nearest node on each side. No manual node-to-node connection is required.
        foreach (CompanyGameCorridor corridor in corridors)
        {
            if (corridor == null || !corridor.Walkable) continue;

            foreach (CompanyGameCorridor other in corridor.ConnectedCorridors)
            {
                if (other == null || !other.Walkable) continue;

                CompanyGamePathNode a = corridor.GetNearestNode(other.transform.position);
                CompanyGamePathNode b = other.GetNearestNode(corridor.transform.position);

                if (a != null && b != null)
                {
                    SetNodeFloor(a, corridor.Floor);
                    SetNodeFloor(b, other.Floor);
                    a.ConnectTo(b);
                }
            }
        }
    }

    private CompanyGamePathNode FindNearestNode(Vector3 position, int floor)
    {
        CompanyGamePathNode nearest = null;
        float best = float.MaxValue;

        foreach (CompanyGamePathNode node in graph)
        {
            if (node == null || GetEffectiveFloor(node) != floor) continue;

            float distance = (node.transform.position - position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = node;
            }
        }

        return nearest;
    }

    private static int GetEffectiveFloor(CompanyGamePathNode node)
    {
        CompanyGameCorridor corridor = node.GetComponentInParent<CompanyGameCorridor>();
        return corridor != null ? corridor.Floor : node.Floor;
    }

    private static void SetNodeFloor(CompanyGamePathNode node, int floor)
    {
        // The node currently exposes Floor as serialized data without a public setter.
        // Corridor ownership is therefore the authoritative floor during pathfinding.
    }

    private static void AddNode(CompanyGamePathNode node, Dictionary<CompanyGamePathNode, float> distances, List<CompanyGamePathNode> unvisited)
    {
        if (distances.ContainsKey(node)) return;
        distances[node] = float.MaxValue;
        unvisited.Add(node);
    }

    private static CompanyGamePathNode GetLowestCostNode(List<CompanyGamePathNode> nodes, Dictionary<CompanyGamePathNode, float> distances)
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
