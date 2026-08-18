using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CompanyGameCorridor))]
public sealed class CompanyGameCorridorEditor : Editor
{
    private CompanyGameCorridor corridor;
    private bool editMode;

    private void OnEnable()
    {
        corridor = (CompanyGameCorridor)target;
        SceneView.duringSceneGui += DuringSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Corridor Editor", EditorStyles.boldLabel);

        bool newEditMode = GUILayout.Toggle(editMode,
            editMode ? "Exit Corridor Edit Mode" : "Enter Corridor Edit Mode", "Button");

        if (newEditMode != editMode)
        {
            editMode = newEditMode;
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox(
            "Edit Mode: this Corridor is the source. Click another Corridor to connect them. " +
            "The selected Corridor becomes green when connected.", MessageType.Info);

        EditorGUILayout.LabelField("Nodes", corridor.Nodes.Count.ToString());
        EditorGUILayout.LabelField("Connected Corridors", corridor.ConnectedCorridors.Count.ToString());

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Connect Nodes In List Order"))
            ConnectNodesInListOrder();

        if (GUILayout.Button("Disconnect All Corridor Nodes"))
            DisconnectAllNodes();

        if (GUILayout.Button("Disconnect All Corridors"))
            DisconnectAllCorridors();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Connected Corridors", EditorStyles.boldLabel);
        if (corridor.ConnectedCorridors.Count == 0)
        {
            EditorGUILayout.LabelField("None");
        }
        else
        {
            foreach (CompanyGameCorridor other in corridor.ConnectedCorridors)
            {
                if (other == null) continue;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(other, typeof(CompanyGameCorridor), true);
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    Undo.RecordObject(corridor, "Disconnect Corridors");
                    Undo.RecordObject(other, "Disconnect Corridors");
                    corridor.DisconnectCorridor(other);
                    EditorUtility.SetDirty(corridor);
                    EditorUtility.SetDirty(other);
                    SceneView.RepaintAll();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (corridor == null) return;

        DrawCorridorNetwork();

        if (!editMode || Selection.activeGameObject != corridor.gameObject)
            return;

        Event e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0 || e.alt)
            return;

        GameObject picked = HandleUtility.PickGameObject(e.mousePosition, false);
        if (picked == null || picked == corridor.gameObject)
            return;

        CompanyGameCorridor other = picked.GetComponent<CompanyGameCorridor>();
        if (other == null)
            other = picked.GetComponentInParent<CompanyGameCorridor>();

        if (other != null && other != corridor)
        {
            ConnectCorridors(corridor, other);
            e.Use();
        }
    }

    private static void ConnectCorridors(CompanyGameCorridor first, CompanyGameCorridor second)
    {
        if (first == null || second == null || first == second)
            return;

        Undo.RecordObject(first, "Connect Corridors");
        Undo.RecordObject(second, "Connect Corridors");

        // If either corridor has no node yet, create a connection node at its corridor position.
        CompanyGamePathNode firstNode = EnsureConnectionNode(first, second.transform.position);
        CompanyGamePathNode secondNode = EnsureConnectionNode(second, first.transform.position);

        if (firstNode != null && secondNode != null)
        {
            Undo.RecordObject(firstNode, "Connect Corridor Nodes");
            Undo.RecordObject(secondNode, "Connect Corridor Nodes");
            firstNode.ConnectTo(secondNode);
            EditorUtility.SetDirty(firstNode);
            EditorUtility.SetDirty(secondNode);
        }

        first.ConnectCorridor(second);
        EditorUtility.SetDirty(first);
        EditorUtility.SetDirty(second);
        SceneView.RepaintAll();
    }

    private static CompanyGamePathNode EnsureConnectionNode(CompanyGameCorridor owner, Vector3 targetPosition)
    {
        CompanyGamePathNode existing = owner.GetNearestNode(targetPosition);
        if (existing != null)
            return existing;

        GameObject nodeObject = new GameObject("Path Node");
        Undo.RegisterCreatedObjectUndo(nodeObject, "Create Connection Node");
        nodeObject.transform.SetParent(owner.transform, true);
        nodeObject.transform.position = owner.transform.position;

        CompanyGamePathNode node = nodeObject.AddComponent<CompanyGamePathNode>();
        Undo.RecordObject(owner, "Add Connection Node");
        owner.AddNode(node);
        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(owner);
        return node;
    }

    private void ConnectNodesInListOrder()
    {
        Undo.RecordObject(corridor, "Connect Corridor Nodes");
        CompanyGamePathNode previous = null;
        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null) continue;
            if (previous != null)
            {
                Undo.RecordObject(previous, "Connect Corridor Nodes");
                Undo.RecordObject(node, "Connect Corridor Nodes");
                previous.ConnectTo(node);
                EditorUtility.SetDirty(previous);
                EditorUtility.SetDirty(node);
            }
            previous = node;
        }
        EditorUtility.SetDirty(corridor);
        SceneView.RepaintAll();
    }

    private void DisconnectAllNodes()
    {
        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null) continue;

            CompanyGamePathNode[] connections = new CompanyGamePathNode[node.Connections.Count];
            for (int i = 0; i < node.Connections.Count; i++)
                connections[i] = node.Connections[i];

            Undo.RecordObject(node, "Disconnect Corridor Nodes");
            foreach (CompanyGamePathNode other in connections)
            {
                if (other == null) continue;
                Undo.RecordObject(other, "Disconnect Corridor Nodes");
                node.DisconnectFrom(other);
                EditorUtility.SetDirty(other);
            }
            EditorUtility.SetDirty(node);
        }
        SceneView.RepaintAll();
    }

    private void DisconnectAllCorridors()
    {
        CompanyGameCorridor[] connected = new CompanyGameCorridor[corridor.ConnectedCorridors.Count];
        for (int i = 0; i < corridor.ConnectedCorridors.Count; i++)
            connected[i] = corridor.ConnectedCorridors[i];

        Undo.RecordObject(corridor, "Disconnect Corridors");
        foreach (CompanyGameCorridor other in connected)
        {
            if (other == null) continue;
            Undo.RecordObject(other, "Disconnect Corridors");
            corridor.DisconnectCorridor(other);
            EditorUtility.SetDirty(other);
        }
        EditorUtility.SetDirty(corridor);
        SceneView.RepaintAll();
    }

    private void DrawCorridorNetwork()
    {
        bool connected = corridor.ConnectedCorridors.Count > 0;
        Handles.color = connected ? Color.green : Color.blue;

        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null) continue;
            float size = HandleUtility.GetHandleSize(node.transform.position) * 0.13f;
            Handles.SphereHandleCap(0, node.transform.position, Quaternion.identity, size, EventType.Repaint);
        }

        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null) continue;
            foreach (CompanyGamePathNode connection in node.Connections)
            {
                if (connection == null) continue;
                Handles.DrawAAPolyLine(2f, node.transform.position, connection.transform.position);
            }
        }

        if (connected)
        {
            Handles.color = Color.green;
            foreach (CompanyGameCorridor other in corridor.ConnectedCorridors)
            {
                if (other == null) continue;
                CompanyGamePathNode a = corridor.GetNearestNode(other.transform.position);
                CompanyGamePathNode b = other.GetNearestNode(corridor.transform.position);
                if (a != null && b != null)
                    Handles.DrawAAPolyLine(6f, a.transform.position, b.transform.position);
            }
        }
    }
}
