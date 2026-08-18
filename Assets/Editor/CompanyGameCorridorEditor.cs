using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CompanyGameCorridor))]
public sealed class CompanyGameCorridorEditor : Editor
{
    private CompanyGameCorridor corridor;
    private bool editMode;
    private CompanyGamePathNode selectedNode;

    private void OnEnable()
    {
        corridor = (CompanyGameCorridor)target;
        SceneView.duringSceneGui += DuringSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
        selectedNode = null;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Corridor Editor", EditorStyles.boldLabel);

        bool newEditMode = GUILayout.Toggle(editMode, editMode ? "Exit Corridor Edit Mode" : "Enter Corridor Edit Mode", "Button");
        if (newEditMode != editMode)
        {
            editMode = newEditMode;
            selectedNode = null;
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox(
            "Edit Mode: click a Path Node, then click another Path Node to connect them. " +
            "This also works between different Corridors. Click the same selected Node again to cancel.",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Network", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Nodes", corridor.Nodes.Count.ToString());
        EditorGUILayout.LabelField("Connected Corridors", corridor.ConnectedCorridors.Count.ToString());

        if (selectedNode != null)
        {
            EditorGUILayout.LabelField("Selected Node", selectedNode.name);
            if (GUILayout.Button("Cancel Node Selection"))
            {
                selectedNode = null;
                SceneView.RepaintAll();
            }
        }

        if (GUILayout.Button("Connect Nodes In List Order")) ConnectNodesInListOrder();
        if (GUILayout.Button("Disconnect All Corridor Nodes")) DisconnectAll();
        if (GUILayout.Button("Disconnect All Corridors")) DisconnectAllCorridors();

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
                    Undo.RecordObject(corridor, "Disconnect Corridor");
                    Undo.RecordObject(other, "Disconnect Corridor");
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

        if (!editMode || Selection.activeGameObject != corridor.gameObject) return;

        Event e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0 || e.alt) return;

        GameObject picked = HandleUtility.PickGameObject(e.mousePosition, false);
        CompanyGamePathNode pickedNode = picked != null ? picked.GetComponent<CompanyGamePathNode>() : null;

        if (pickedNode != null)
        {
            HandleNodeClick(pickedNode);
            e.Use();
            return;
        }

        if (picked != null) return;

        if (!TryGetScenePoint(e.mousePosition, out Vector3 point)) return;
        CreateNode(point);
        e.Use();
    }

    private void HandleNodeClick(CompanyGamePathNode node)
    {
        if (selectedNode == node)
        {
            selectedNode = null;
            SceneView.RepaintAll();
            return;
        }

        if (selectedNode == null)
        {
            selectedNode = node;
            Selection.activeGameObject = corridor.gameObject;
            SceneView.RepaintAll();
            return;
        }

        ConnectSelectedNodes(selectedNode, node);
        selectedNode = null;
        Selection.activeGameObject = corridor.gameObject;
        SceneView.RepaintAll();
    }

    private void ConnectSelectedNodes(CompanyGamePathNode first, CompanyGamePathNode second)
    {
        if (first == null || second == null || first == second) return;

        CompanyGameCorridor firstOwner = first.GetComponentInParent<CompanyGameCorridor>();
        CompanyGameCorridor secondOwner = second.GetComponentInParent<CompanyGameCorridor>();

        Undo.RecordObject(first, "Connect Path Nodes");
        Undo.RecordObject(second, "Connect Path Nodes");
        first.ConnectTo(second);

        if (firstOwner != null && secondOwner != null && firstOwner != secondOwner)
        {
            Undo.RecordObject(firstOwner, "Connect Corridors");
            Undo.RecordObject(secondOwner, "Connect Corridors");
            firstOwner.ConnectCorridor(secondOwner);
            EditorUtility.SetDirty(firstOwner);
            EditorUtility.SetDirty(secondOwner);
        }

        EditorUtility.SetDirty(first);
        EditorUtility.SetDirty(second);
    }

    private void CreateNode(Vector3 position)
    {
        CompanyGamePathNode previous = corridor.GetNearestNode(position);

        GameObject nodeObject = new GameObject("Path Node");
        Undo.RegisterCreatedObjectUndo(nodeObject, "Create Corridor Path Node");
        nodeObject.transform.position = position;
        nodeObject.transform.SetParent(corridor.transform, true);

        CompanyGamePathNode node = nodeObject.AddComponent<CompanyGamePathNode>();
        Undo.RecordObject(corridor, "Add Corridor Path Node");
        corridor.AddNode(node);

        if (previous != null && previous != node)
        {
            Undo.RecordObject(previous, "Connect Corridor Path Nodes");
            previous.ConnectTo(node);
            EditorUtility.SetDirty(previous);
        }

        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(corridor);
        Selection.activeGameObject = corridor.gameObject;
        SceneView.RepaintAll();
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

    private void DisconnectAll()
    {
        Undo.RecordObject(corridor, "Disconnect Corridor Nodes");
        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null) continue;
            CompanyGamePathNode[] connections = new CompanyGamePathNode[node.Connections.Count];
            for (int i = 0; i < node.Connections.Count; i++) connections[i] = node.Connections[i];
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
        EditorUtility.SetDirty(corridor);
        SceneView.RepaintAll();
    }

    private void DisconnectAllCorridors()
    {
        CompanyGameCorridor[] connected = new CompanyGameCorridor[corridor.ConnectedCorridors.Count];
        for (int i = 0; i < corridor.ConnectedCorridors.Count; i++) connected[i] = corridor.ConnectedCorridors[i];

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
        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null) continue;

            bool isSelected = node == selectedNode;
            Handles.color = isSelected ? Color.yellow : Color.cyan;
            Handles.SphereHandleCap(
                0,
                node.transform.position,
                Quaternion.identity,
                HandleUtility.GetHandleSize(node.transform.position) * (isSelected ? 0.2f : 0.12f),
                EventType.Repaint);
        }

        Handles.color = Color.cyan;
        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null) continue;
            foreach (CompanyGamePathNode connection in node.Connections)
            {
                if (connection == null) continue;
                Handles.DrawAAPolyLine(2f, node.transform.position, connection.transform.position);
            }
        }

        Handles.color = Color.green;
        foreach (CompanyGameCorridor other in corridor.ConnectedCorridors)
        {
            if (other == null) continue;

            CompanyGamePathNode thisNode = corridor.GetNearestNode(other.transform.position);
            CompanyGamePathNode otherNode = other.GetNearestNode(corridor.transform.position);
            if (thisNode == null || otherNode == null) continue;

            Handles.DrawAAPolyLine(7f, thisNode.transform.position, otherNode.transform.position);
        }
    }

    private static bool TryGetScenePoint(Vector2 mousePosition, out Vector3 point)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        if (plane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            point.z = 0f;
            return true;
        }
        point = Vector3.zero;
        return false;
    }
}
