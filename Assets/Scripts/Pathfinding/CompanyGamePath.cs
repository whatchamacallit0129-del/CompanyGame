using System.Collections.Generic;

/// <summary>
/// Result of a path calculation. Independent from any employee or movement controller.
/// </summary>
public sealed class CompanyGamePath
{
    private readonly List<CompanyGamePathNode> nodes;

    public IReadOnlyList<CompanyGamePathNode> Nodes => nodes;
    public bool IsValid => nodes.Count > 0;

    public CompanyGamePath(IEnumerable<CompanyGamePathNode> source)
    {
        nodes = source == null
            ? new List<CompanyGamePathNode>()
            : new List<CompanyGamePathNode>(source);
    }
}
