using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Logical hallway segment that owns movement nodes and explicit corridor links.
/// Runtime movement data is kept separate from editor authoring behavior so
/// rooms, doors, elevators and restrictions can be added without replacing the network.
/// </summary>
public sealed class CompanyGameCorridor : MonoBehaviour
{
    [SerializeField] private string corridorId;
    [SerializeField] private float width = 2f;
    [SerializeField] private int floor;
    [SerializeField] private bool walkable = true;
    [SerializeField] private List<CompanyGamePathNode> nodes = new List<CompanyGamePathNode>();
    [SerializeField] private List<CompanyGameCorridor> connectedCorridors = new List<CompanyGameCorridor>();

    public string CorridorId => corridorId;
    public float Width => Mathf.Max(0.1f, width);
    public int Floor => floor;
    public bool Walkable => walkable;
    public IReadOnlyList<CompanyGamePathNode> Nodes => nodes;
    public IReadOnlyList<CompanyGameCorridor> ConnectedCorridors => connectedCorridors;

    public void AddNode(CompanyGamePathNode node)
    {
        if (node == null || nodes.Contains(node)) return;
        nodes.Add(node);
    }

    public void RemoveNode(CompanyGamePathNode node)
    {
        if (node == null) return;
        nodes.Remove(node);
    }

    public void ConnectCorridor(CompanyGameCorridor other)
    {
        if (other == null || other == this) return;
        if (!connectedCorridors.Contains(other)) connectedCorridors.Add(other);
        if (!other.connectedCorridors.Contains(this)) other.connectedCorridors.Add(this);
    }

    public void DisconnectCorridor(CompanyGameCorridor other)
    {
        if (other == null) return;
        connectedCorridors.Remove(other);
        other.connectedCorridors.Remove(this);
    }

    public CompanyGamePathNode GetNearestNode(Vector3 worldPosition)
    {
        CompanyGamePathNode nearest = null;
        float bestDistance = float.MaxValue;

        foreach (CompanyGamePathNode node in nodes)
        {
            if (node == null) continue;
            float distance = (node.transform.position - worldPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = node;
            }
        }
        return nearest;
    }

    public void SortNodesByDistance()
    {
        nodes.RemoveAll(node => node == null);
        nodes.Sort((a, b) =>
        {
            if (a == null) return 1;
            if (b == null) return -1;
            return a.transform.position.sqrMagnitude.CompareTo(b.transform.position.sqrMagnitude);
        });
    }

    private void Reset()
    {
        if (string.IsNullOrEmpty(corridorId)) corridorId = gameObject.name;
    }

    private void OnValidate()
    {
        width = Mathf.Max(0.1f, width);
        nodes.RemoveAll(node => node == null);
        connectedCorridors.RemoveAll(c => c == null || c == this);
        if (string.IsNullOrEmpty(corridorId)) corridorId = gameObject.name;
    }
}
