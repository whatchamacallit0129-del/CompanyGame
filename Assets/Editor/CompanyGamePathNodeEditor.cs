using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CompanyGamePathNode))]
public class CompanyGamePathNodeEditor : Editor
{
    private CompanyGamePathNode node;

    private void OnEnable()
    {
        node = (CompanyGamePathNode)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Node Tools", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Connected Node"))
                CreateConnectedNode();

            if (GUILayout.Button("Disconnect All"))
            {
                Undo.RecordObject(node, "Disconnect All Path Node Connections");
                List<CompanyGamePathNode> copy = new List<CompanyGamePathNode>(node.Connections);
                foreach (CompanyGamePathNode other in copy)
                    node.DisconnectFrom(other);
                EditorUtility.SetDirty(node);
            }
        }

        EditorGUILayout.HelpBox(
            "Use Shift-click in the Scene view to create/connect nodes. " +
            "Connections are stored on the node graph, not inferred from object names.",
            MessageType.Info);
    }

    private void CreateConnectedNode()
    {
        GameObject go = new GameObject("Path Node");
        Undo.RegisterCreatedObjectUndo(go, "Create Path Node");
        go.transform.position = node.transform.position + Vector3.right;
        go.transform.SetParent(node.transform.parent);

        CompanyGamePathNode newNode = go.AddComponent<CompanyGamePathNode>();
        Undo.RecordObject(node, "Connect Path Nodes");
        node.ConnectTo(newNode);
        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(newNode);
        Selection.activeGameObject = go;
    }

    private void OnSceneGUI()
    {
        if (node == null)
            return;

        DrawConnections(node);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && e.shift && !e.alt)
        {
            GameObject picked = HandleUtility.PickGameObject(e.mousePosition, false);
            if (picked != null)
            {
                CompanyGamePathNode other = picked.GetComponent<CompanyGamePathNode>();
                if (other != null && other != node)
                {
                    Undo.RecordObject(node, "Connect Path Nodes");
                    Undo.RecordObject(other, "Connect Path Nodes");
                    node.ConnectTo(other);
                    EditorUtility.SetDirty(node);
                    EditorUtility.SetDirty(other);
                    e.Use();
                    SceneView.RepaintAll();
                }
            }
        }
    }

    private static void DrawConnections(CompanyGamePathNode source)
    {
        Handles.color = new Color(0.2f, 0.85f, 1f, 0.85f);

        foreach (CompanyGamePathNode target in source.Connections)
        {
            if (target == null)
                continue;

            Vector3 a = source.transform.position;
            Vector3 b = target.transform.position;
            Handles.DrawAAPolyLine(3f, a, b);
            DrawArrow(a, b);
        }

        Handles.color = Color.white;
        Handles.SphereHandleCap(
            0,
            source.transform.position,
            Quaternion.identity,
            HandleUtility.GetHandleSize(source.transform.position) * 0.12f,
            EventType.Repaint);
    }

    private static void DrawArrow(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 midpoint = Vector3.Lerp(from, to, 0.5f);
        float size = HandleUtility.GetHandleSize(midpoint) * 0.08f;
        Handles.ConeHandleCap(
            0,
            midpoint,
            Quaternion.LookRotation(direction.normalized),
            size,
            EventType.Repaint);
    }
}
