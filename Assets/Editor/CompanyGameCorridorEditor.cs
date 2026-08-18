using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CompanyGameCorridor))]
public sealed class CompanyGameCorridorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CompanyGameCorridor corridor = (CompanyGameCorridor)target;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Corridor Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Create Node At Corridor Position"))
        {
            CreateNode(corridor, corridor.transform.position);
        }

        if (GUILayout.Button("Create Node At Scene View Center"))
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            Vector3 position = sceneView != null ? sceneView.pivot : corridor.transform.position;
            CreateNode(corridor, position);
        }

        if (GUILayout.Button("Connect Nodes In List Order"))
        {
            ConnectNodes(corridor);
        }
    }

    private static void CreateNode(CompanyGameCorridor corridor, Vector3 position)
    {
        GameObject nodeObject = new GameObject("Path Node");
        Undo.RegisterCreatedObjectUndo(nodeObject, "Create Path Node");
        nodeObject.transform.position = position;

        CompanyGamePathNode node = nodeObject.AddComponent<CompanyGamePathNode>();
        corridor.AddNode(node);

        Selection.activeGameObject = nodeObject;
        EditorUtility.SetDirty(corridor);
    }

    private static void ConnectNodes(CompanyGameCorridor corridor)
    {
        CompanyGamePathNode previous = null;

        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null)
                continue;

            if (previous != null)
            {
                Undo.RecordObject(previous, "Connect Corridor Nodes");
                previous.ConnectTo(node);
                EditorUtility.SetDirty(previous);
                EditorUtility.SetDirty(node);
            }

            previous = node;
        }

        EditorUtility.SetDirty(corridor);
    }
}
