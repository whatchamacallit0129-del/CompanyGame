using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A movement node used by the facility's hallway network.
/// Nodes represent points employees can travel through; rooms, doors,
/// elevators and hallway junctions can later connect to this network.
/// </summary>
public class CompanyGamePathNode : MonoBehaviour
{
    [SerializeField] private List<CompanyGamePathNode> connections = new List<CompanyGamePathNode>();
    [SerializeField] private bool bidirectional = true;
    [SerializeField] private float movementCost = 1f;
    [SerializeField] private int floor;

    public IReadOnlyList<CompanyGamePathNode> Connections => connections;
    public bool Bidirectional => bidirectional;
    public float MovementCost => Mathf.Max(0.01f, movementCost);
    public int Floor => floor;

    public void ConnectTo(CompanyGamePathNode other)
    {
        if (other == null || other == this)
            return;

        if (!connections.Contains(other))
            connections.Add(other);

        if (bidirectional && !other.connections.Contains(this))
            other.connections.Add(this);
    }

    public void DisconnectFrom(CompanyGamePathNode other)
    {
        if (other == null)
            return;

        connections.Remove(other);

        if (bidirectional)
            other.connections.Remove(this);
    }

    private void OnValidate()
    {
        movementCost = Mathf.Max(0.01f, movementCost);
        connections.RemoveAll(node => node == null || node == this);
    }
}
