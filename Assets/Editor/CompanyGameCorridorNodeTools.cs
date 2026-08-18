using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CompanyGameCorridor))]
public sealed class CompanyGameCorridorNodeTools : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CompanyGameCorridor corridor = (CompanyGameCorridor)target;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Path Node Tools", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Current Nodes", corridor.Nodes.Count.ToString());

        EditorGUILayout.HelpBox(
            "Add nodes directly to this Corridor. Nodes are runtime movement data; this tool only creates/organizes them.",
            MessageType.Info);

        if (GUILayout.Button("Add Node At Corridor Position"))
            AddNode(corridor, corridor.transform.position);

        if (GUILayout.Button("Add Node At Scene Mouse Position"))
        {
            SceneView.lastActiveSceneView?.Focus();
            SceneView.duringSceneGui -= HandleSceneNodePlacement;
            SceneView.duringSceneGui += HandleSceneNodePlacement;
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Create Start / End Nodes"))
            CreateStartEndNodes(corridor);

        if (GUILayout.Button("Remove Null Nodes"))
        {
            Undo.RecordObject(corridor, "Clean Corridor Nodes");
            corridor.SortNodesByDistance();
            EditorUtility.SetDirty(corridor);
            SceneView.RepaintAll();
        }
    }

    private static void HandleSceneNodePlacement(SceneView sceneView)
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            SceneView.duringSceneGui -= HandleSceneNodePlacement;
            e.Use();
            return;
        }

        if (e.type != EventType.MouseDown || e.button != 0 || e.alt)
            return;

        GameObject selected = Selection.activeGameObject;
        if (selected == null) return;

        CompanyGameCorridor corridor = selected.GetComponent<CompanyGameCorridor>();
        if (corridor == null) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane plane = new Plane(Vector3.forward, corridor.transform.position);
        if (!plane.Raycast(ray, out float distance)) return;

        Vector3 position = ray.GetPoint(distance);
        AddNode(corridor, position);

        SceneView.duringSceneGui -= HandleSceneNodePlacement;
        e.Use();
    }

    private static void AddNode(CompanyGameCorridor corridor, Vector3 position)
    {
        if (corridor == null) return;

        Undo.RecordObject(corridor, "Add Corridor Node");

        GameObject nodeObject = new GameObject("Path Node");
        Undo.RegisterCreatedObjectUndo(nodeObject, "Create Corridor Node");
        nodeObject.transform.SetParent(corridor.transform, true);
        nodeObject.transform.position = position;

        CompanyGamePathNode node = nodeObject.AddComponent<CompanyGamePathNode>();
        corridor.AddNode(node);

        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(corridor);
        Selection.activeGameObject = nodeObject;
        SceneView.RepaintAll();
    }

    private static void CreateStartEndNodes(CompanyGameCorridor corridor)
    {
        if (corridor == null) return;

        Vector3 center = corridor.transform.position;
        float halfLength = Mathf.Max(1f, corridor.Width);

        AddNode(corridor, center + Vector3.left * halfLength);
        AddNode(corridor, center + Vector3.right * halfLength);

        corridor.SortNodesByDistance();
        EditorUtility.SetDirty(corridor);
        SceneView.RepaintAll();
    }
}
