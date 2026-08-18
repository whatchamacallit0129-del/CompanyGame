using UnityEditor;
using UnityEngine;

public static class CompanyGameCorridorTestTools
{
    [MenuItem("Tools/Company Game/Create Test Corridors")]
    private static void CreateTestCorridors()
    {
        GameObject existingA = GameObject.Find("Test Corridor A");
        GameObject existingB = GameObject.Find("Test Corridor B");

        if (existingA != null) Undo.DestroyObjectImmediate(existingA);
        if (existingB != null) Undo.DestroyObjectImmediate(existingB);

        GameObject corridorAObject = new GameObject("Test Corridor A");
        Undo.RegisterCreatedObjectUndo(corridorAObject, "Create Test Corridor A");
        corridorAObject.transform.position = new Vector3(0f, 0f, 0f);
        CompanyGameCorridor corridorA = corridorAObject.AddComponent<CompanyGameCorridor>();

        GameObject corridorBObject = new GameObject("Test Corridor B");
        Undo.RegisterCreatedObjectUndo(corridorBObject, "Create Test Corridor B");
        corridorBObject.transform.position = new Vector3(3f, 0f, 0f);
        CompanyGameCorridor corridorB = corridorBObject.AddComponent<CompanyGameCorridor>();

        CreateNode(corridorA, new Vector3(-1f, 0f, 0f));
        CreateNode(corridorA, new Vector3(1f, 0f, 0f));
        CreateNode(corridorB, new Vector3(2f, 0f, 0f));
        CreateNode(corridorB, new Vector3(4f, 0f, 0f));

        Selection.activeGameObject = corridorAObject;
        EditorUtility.SetDirty(corridorA);
        EditorUtility.SetDirty(corridorB);
        SceneView.lastActiveSceneView?.FrameSelected();

        Debug.Log("[Company Game] Test corridors created: A=(0,0,0), B=(3,0,0). Select A, enter Corridor Edit Mode, then click B to test connection.");
    }

    private static void CreateNode(CompanyGameCorridor corridor, Vector3 worldPosition)
    {
        GameObject nodeObject = new GameObject("Path Node");
        Undo.RegisterCreatedObjectUndo(nodeObject, "Create Test Path Node");
        nodeObject.transform.SetParent(corridor.transform, true);
        nodeObject.transform.position = worldPosition;

        CompanyGamePathNode node = nodeObject.AddComponent<CompanyGamePathNode>();
        corridor.AddNode(node);
        EditorUtility.SetDirty(node);
    }
}
