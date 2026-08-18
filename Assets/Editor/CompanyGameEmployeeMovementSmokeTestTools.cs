using UnityEditor;
using UnityEngine;

public static class CompanyGameEmployeeMovementSmokeTestTools
{
    [MenuItem("Tools/Company Game/Run Employee Movement Smoke Test")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Company Game] Stop Play Mode before starting the movement smoke test.");
            return;
        }

        CleanupPreviousTest();

        GameObject root = new GameObject("Employee Movement Smoke Test");
        Undo.RegisterCreatedObjectUndo(root, "Create Employee Movement Smoke Test");

        CompanyGameNavigationGraph graph = CompanyGameNavigationGraph.Instance;
        graph.Refresh();

        CompanyGamePathNode[] nodes = new CompanyGamePathNode[4];
        Vector3[] positions =
        {
            new Vector3(-3f, 0f, 0f),
            new Vector3(-1f, 1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(3f, 0f, 0f)
        };

        for (int i = 0; i < nodes.Length; i++)
        {
            GameObject nodeObject = new GameObject("SmokeTest Node " + i);
            Undo.RegisterCreatedObjectUndo(nodeObject, "Create SmokeTest Node");
            nodeObject.transform.SetParent(root.transform, true);
            nodeObject.transform.position = positions[i];
            nodes[i] = nodeObject.AddComponent<CompanyGamePathNode>();
        }

        for (int i = 0; i < nodes.Length - 1; i++)
            nodes[i].ConnectTo(nodes[i + 1]);

        GameObject employeeObject = new GameObject("SmokeTest Employee");
        Undo.RegisterCreatedObjectUndo(employeeObject, "Create SmokeTest Employee");
        employeeObject.transform.SetParent(root.transform, true);
        employeeObject.transform.position = positions[0];
        CompanyGameEmployeeMovement movement = employeeObject.AddComponent<CompanyGameEmployeeMovement>();
        movement.SetFloor(0);

        CompanyGameEmployeeMovementSmokeTest test = root.AddComponent<CompanyGameEmployeeMovementSmokeTest>();
        test.Configure(movement, positions[3], nodes.Length);

        Selection.activeGameObject = employeeObject;
        EditorUtility.SetDirty(root);
        EditorApplication.isPlaying = true;

        Debug.Log("[Company Game] Employee movement smoke test started. Expected route: Node 0 -> Node 1 -> Node 2 -> Node 3.");
    }

    private static void CleanupPreviousTest()
    {
        GameObject existing = GameObject.Find("Employee Movement Smoke Test");
        if (existing != null) Undo.DestroyObjectImmediate(existing);
    }
}
