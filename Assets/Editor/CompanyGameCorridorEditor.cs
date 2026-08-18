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

        bool newEditMode = GUILayout.Toggle(editMode, editMode ? "Exit Corridor Draw Mode" : "Enter Corridor Draw Mode", "Button");
        if (newEditMode != editMode)
        {
            editMode = newEditMode;
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox(
            "Draw Mode: left-click empty space to add a Node. Shift-click a Node to connect it. " +
            "Shift-click another Corridor to connect the two nearest nodes. Connections are stored as graph data.",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Connect Nodes In List Order")) ConnectNodesInListOrder();
        if (GUILayout.Button("Disconnect All Corridor Nodes")) DisconnectAll();
        if (GUILayout.Button("Disconnect All Corridors")) DisconnectAllCorridors();
        if (GUILayout.Button("Remove Null References"))
        {
            Undo.RecordObject(corridor, "Clean Corridor References");
            corridor.SortNodesByDistance();
            EditorUtility.SetDirty(corridor);
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
        if (picked == corridor.gameObject) return;

        CompanyGamePathNode pickedNode = picked != null ? picked.GetComponent<CompanyGamePathNode>() : null;
        CompanyGameCorridor pickedCorridor = picked != null ? picked.GetComponent<CompanyGameCorridor>() : null;

        if (e.shift && pickedCorridor != null && pickedCorridor != corridor)
        {
            ConnectCorridors(pickedCorridor);
            e.Use();
            return;
        }

        if (e.shift && pickedNode != null)
        {
            ConnectNearestNodeTo(pickedNode);
            e.Use();
            return;
        }

        if (picked != null) return;
        if (!TryGetScenePoint(e.mousePosition, out Vector3 point)) return;

        CreateNode(point);
        e.Use();
    }

    private void CreateNode(Vector3 position)
    {
        GameObject nodeObject = new GameObject("Path Node");
        Undo.RegisterCreatedObjectUndo(nodeObject, "Create Corridor Path Node");
        nodeObject.transform.position = position;
        nodeObject.transform.SetParent(corridor.transform, true);

        CompanyGamePathNode node = nodeObject.AddComponent<CompanyGamePathNode>();
        Undo.RecordObject(corridor, "Add Corridor Path Node");
        corridor.AddNode(node);

        CompanyGamePathNode previous = corridor.GetNearestNode(position);
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

    private void ConnectNearestNodeTo(CompanyGamePathNode pickedNode)
    {
        CompanyGamePathNode nearest = corridor.GetNearestNode(pickedNode.transform.position);
        if (nearest == null || nearest == pickedNode) return;

        Undo.RecordObject(pickedNode, "Connect Corridor Nodes");
        Undo.RecordObject(nearest, "Connect Corridor Nodes");
        pickedNode.ConnectTo(nearest);
        EditorUtility.SetDirty(pickedNode);
        EditorUtility.SetDirty(nearest);
        SceneView.RepaintAll();
    }

    private void ConnectCorridors(CompanyGameCorridor other)
    {
        if (other == null || other == corridor) return;

        CompanyGamePathNode thisNode = corridor.GetNearestNode(other.transform.position);
        CompanyGamePathNode otherNode = other.GetNearestNode(corridor.transform.position);
        if (thisNode == null || otherNode == null)
        {
            Debug.LogWarning("[Company Game] Both corridors need at least one Path Node before they can be connected.");
            return;
        }

        Undo.RecordObject(corridor, "Connect Corridors");
        Undo.RecordObject(other, "Connect Corridors");
        Undo.RecordObject(thisNode, "Connect Corridor Nodes");
        Undo.RecordObject(otherNode, "Connect Corridor Nodes");

        corridor.ConnectCorridor(other);
        thisNode.ConnectTo(otherNode);

        EditorUtility.SetDirty(corridor);
        EditorUtility.SetDirty(other);
        EditorUtility.SetDirty(thisNode);
        EditorUtility.SetDirty(otherNode);
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
        Handles.color = new Color(0.15f, 0.65f, 1f, 0.9f);
        CompanyGamePathNode previous = null;
        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null) continue;
            Handles.SphereHandleCap(0, node.transform.position, Quaternion.identity,
                HandleUtility.GetHandleSize(node.transform.position) * 0.12f, EventType.Repaint);

            if (previous != null)
            {
                Handles.DrawAAPolyLine(5f, previous.transform.position, node.transform.position);
                DrawDirectionArrow(previous.transform.position, node.transform.position);
            }
            previous = node;
        }

        Handles.color = new Color(0.9f, 0.65f, 0.15f, 0.8f);
        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null) continue;
            foreach (CompanyGamePathNode connection in node.Connections)
            {
                if (connection == null) continue;
                Handles.DrawAAPolyLine(2f, node.transform.position, connection.transform.position);
            }
        }

        Handles.color = new Color(0.2f, 1f, 0.35f, 0.85f);
        foreach (CompanyGameCorridor other in corridor.ConnectedCorridors)
        {
            if (other == null) continue;
            Handles.DrawAAPolyLine(6f, corridor.transform.position, other.transform.position);
        }
    }

    private static void DrawDirectionArrow(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        if (direction.sqrMagnitude < 0.0001f) return;
        Vector3 midpoint = Vector3.Lerp(from, to, 0.5f);
        float size = HandleUtility.GetHandleSize(midpoint) * 0.08f;
        Handles.ConeHandleCap(0, midpoint, Quaternion.LookRotation(direction.normalized), size, EventType.Repaint);
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
