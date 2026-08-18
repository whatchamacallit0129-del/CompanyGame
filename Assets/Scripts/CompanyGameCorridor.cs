using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A logical hallway segment that owns movement nodes.
/// Geometry/visuals are intentionally separated from the movement network so
/// the same network can later support rooms, doors, elevators and restrictions.
/// </summary>
public sealed class CompanyGameCorridor : MonoBehaviour
{
    [SerializeField] private string corridorId;
    [SerializeField] private float width = 2f;
    [SerializeField] private int floor;
    [SerializeField] private bool walkable = true;
    [SerializeField] private List<CompanyGamePathNode> nodes = new List<CompanyGamePathNode>();

    public string CorridorId => corridorId;
    public float Width => Mathf.Max(0.1f, width);
    public int Floor => floor;
    public bool Walkable => walkable;
    public IReadOnlyList<CompanyGamePathNode> Nodes => nodes;

    public void AddNode(CompanyGamePathNode node)
    {
        if (node == null || nodes.Contains(node))
            return;

        nodes.Add(node);
    }

    public void RemoveNode(CompanyGamePathNode node)
    {
        if (node == null)
            return;

        nodes.Remove(node);
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
        if (string.IsNullOrEmpty(corridorId))
            corridorId = gameObject.name;
    }

    private void OnValidate()
    {
        width = Mathf.Max(0.1f, width);
        nodes.RemoveAll(node => node == null);

        if (string.IsNullOrEmpty(corridorId))
            corridorId = gameObject.name;
    }
}
