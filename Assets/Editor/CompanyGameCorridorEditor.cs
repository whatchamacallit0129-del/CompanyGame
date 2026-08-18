using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene-based editor for building a 2D corridor and its path network.
/// The editor owns authoring behavior; runtime corridor data stays in
/// CompanyGameCorridor and path connectivity stays in CompanyGamePathNode.
/// </summary>
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

        bool newEditMode = GUILayout.Toggle(
            editMode,
            editMode ? "Exit Corridor Draw Mode" : "Enter Corridor Draw Mode",
            "Button");

        if (newEditMode != editMode)
        {
            editMode = newEditMode;
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox(
            "Draw Mode: left-click empty space to add a Node. Shift-click an existing Node to connect it. " +
            "Move Nodes normally with the Unity transform tools. Use Undo to safely revert changes.",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Connect Nodes In List Order"))
            ConnectNodesInListOrder();

        if (GUILayout.Button("Disconnect All Corridor Nodes"))
            DisconnectAll();

        if (GUILayout.Button("Remove Null Node References"))
        {
            Undo.RecordObject(corridor, "Clean Corridor Nodes");
            corridor.SortNodesByDistance();
            EditorUtility.SetDirty(corridor);
        }
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (corridor == null)
            return;

        DrawCorridorNetwork();

        if (!editMode || Selection.activeGameObject != corridor.gameObject)
            return;

        Event e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0 || e.alt)
            return;

        GameObject picked = HandleUtility.PickGameObject(e.mousePosition, false);
        CompanyGamePathNode pickedNode = picked != null
            ? picked.GetComponent<CompanyGamePathNode>()
            : null;

        if (e.shift && pickedNode != null)
        {
            CreateNodeAtExistingNode(pickedNode);
            e.Use();
            return;
        }

        if (picked != null)
            return;

        if (!TryGetScenePoint(sceneView, e.mousePosition, out Vector3 point))
            return;

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

        CompanyGamePathNode previous = GetNearestNode(position, node);
        if (previous != null)
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

    private void CreateNodeAtExistingNode(CompanyGamePathNode source)
    {
        if (source == null)
            return;

        // Shift-clicking an existing node connects it to the currently selected
        // corridor's nearest node instead of creating duplicate geometry.
        CompanyGamePathNode nearest = GetNearestNode(source.transform.position, source);
        if (nearest == null)
            return;

        Undo.RecordObject(source, "Connect Corridor Path Nodes");
        Undo.RecordObject(nearest, "Connect Corridor Path Nodes");
        source.ConnectTo(nearest);
        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(nearest);
        SceneView.RepaintAll();
    }

    private CompanyGamePathNode GetNearestNode(Vector3 position, CompanyGamePathNode exclude)
    {
        CompanyGamePathNode nearest = null;
        float best = float.MaxValue;

        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null || node == exclude)
                continue;

            float distance = (node.transform.position - position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = node;
            }
        }

        return nearest;
    }

    private void ConnectNodesInListOrder()
    {
        Undo.RecordObject(corridor, "Connect Corridor Nodes");
        CompanyGamePathNode previous = null;

        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null)
                continue;

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
            if (node == null)
                continue;

            CompanyGamePathNode[] connections = new CompanyGamePathNode[node.Connections.Count];
            for (int i = 0; i < node.Connections.Count; i++)
                connections[i] = node.Connections[i];

            Undo.RecordObject(node, "Disconnect Corridor Nodes");
            foreach (CompanyGamePathNode other in connections)
            {
                if (other == null)
                    continue;

                Undo.RecordObject(other, "Disconnect Corridor Nodes");
                node.DisconnectFrom(other);
                EditorUtility.SetDirty(other);
            }

            EditorUtility.SetDirty(node);
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
            if (node == null)
                continue;

            Handles.SphereHandleCap(
                0,
                node.transform.position,
                Quaternion.identity,
                HandleUtility.GetHandleSize(node.transform.position) * 0.12f,
                EventType.Repaint);

            if (previous != null)
            {
                Handles.DrawAAPolyLine(5f, previous.transform.position, node.transform.position);
                DrawDirectionArrow(previous.transform.position, node.transform.position);
            }

            previous = node;
        }

        // Also display non-sequential graph connections, such as junctions.
        Handles.color = new Color(0.9f, 0.65f, 0.15f, 0.75f);
        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null)
                continue;

            foreach (CompanyGamePathNode connection in node.Connections)
            {
                if (connection == null)
                    continue;

                Handles.DrawAAPolyLine(2f, node.transform.position, connection.transform.position);
            }
        }
    }

    private static void DrawDirectionArrow(Vector3 from, Vector3 to)
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

    private static bool TryGetScenePoint(SceneView sceneView, Vector2 mousePosition, out Vector3 point)
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
