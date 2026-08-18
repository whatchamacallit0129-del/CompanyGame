using System.Text;
using UnityEditor;
using UnityEngine;

public static class CompanyGamePathfindingTestTools
{
    [MenuItem("Tools/Company Game/Pathfinding/Create Pathfinding Manager")]
    private static void CreateManager()
    {
        CompanyGamePathfindingManager existing = Object.FindFirstObjectByType<CompanyGamePathfindingManager>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        GameObject managerObject = new GameObject("Company Game Pathfinding Manager");
        Undo.RegisterCreatedObjectUndo(managerObject, "Create Pathfinding Manager");
        CompanyGamePathfindingManager manager = managerObject.AddComponent<CompanyGamePathfindingManager>();
        Selection.activeGameObject = managerObject;
        EditorUtility.SetDirty(manager);
    }

    [MenuItem("Tools/Company Game/Pathfinding/Test Selected Node Route")]
    private static void TestSelectedNodeRoute()
    {
        CompanyGamePathNode[] selected = Selection.GetFiltered<CompanyGamePathNode>(SelectionMode.Editable | SelectionMode.ExcludePrefab);
        if (selected.Length != 2)
        {
            Debug.LogWarning("[Company Game] Select exactly two Path Nodes: start first, goal second.");
            return;
        }

        CompanyGamePathfindingManager manager = Object.FindFirstObjectByType<CompanyGamePathfindingManager>();
        if (manager == null)
        {
            Debug.LogWarning("[Company Game] Pathfinding Manager not found. Use Tools > Company Game > Pathfinding > Create Pathfinding Manager first.");
            return;
        }

        CompanyGamePath path = manager.FindPath(selected[0], selected[1]);
        if (!path.IsValid)
        {
            Debug.LogWarning($"[Company Game] No route found: {selected[0].name} -> {selected[1].name}");
            return;
        }

        var builder = new StringBuilder("[Company Game] Route: ");
        for (int i = 0; i < path.Nodes.Count; i++)
        {
            if (i > 0) builder.Append(" -> ");
            builder.Append(path.Nodes[i].name);
        }
        Debug.Log(builder.ToString());
    }
}
