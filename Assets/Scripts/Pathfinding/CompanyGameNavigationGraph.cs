using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime navigation graph. It only knows about nodes and explicit connections.
/// Corridors, rooms, doors and future transport links can all use the same graph.
/// </summary>
public sealed class CompanyGameNavigationGraph : MonoBehaviour
{
    private static CompanyGameNavigationGraph instance;
    private readonly List<CompanyGamePathNode> nodes = new List<CompanyGamePathNode>();

    public static CompanyGameNavigationGraph Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = FindFirstObjectByType<CompanyGameNavigationGraph>();
            if (instance != null) return instance;

            GameObject go = new GameObject("Company Game Navigation Graph");
            instance = go.AddComponent<CompanyGameNavigationGraph>();
            return instance;
        }
    }

    public IReadOnlyList<CompanyGamePathNode> Nodes => nodes;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => _ = Instance;

    public void Refresh()
    {
        nodes.Clear();
        nodes.AddRange(Object.FindObjectsByType<CompanyGamePathNode>());
    }

    public CompanyGamePathNode FindNearest(Vector3 position, int floor, float maxDistance)
    {
        CompanyGamePathNode nearest = null;
        float best = maxDistance * maxDistance;

        foreach (CompanyGamePathNode node in nodes)
        {
            if (node == null || node.Floor != floor) continue;
            float distance = (node.transform.position - position).sqrMagnitude;
            if (distance <= best)
            {
                best = distance;
                nearest = node;
            }
        }

        return nearest;
    }
}
