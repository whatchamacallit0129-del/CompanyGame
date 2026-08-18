using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CompanyGameCorridor))]
public sealed class CompanyGameCorridorEditor : Editor
{
    private CompanyGameCorridor corridor;
    private bool editMode;
    private static CompanyGameCorridor activeSource;

    private void OnEnable()
    {
        corridor = (CompanyGameCorridor)target;
        SceneView.duringSceneGui += DuringSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
        if (activeSource == corridor) activeSource = null;
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
            activeSource = editMode ? corridor : null;
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox(
            editMode
                ? "CONNECT MODE: click another Corridor in the Scene."
                : "Select a Corridor and enter Edit Mode to connect it to another Corridor.",
            MessageType.Info);

        EditorGUILayout.LabelField("Nodes", corridor.Nodes.Count.ToString());
        EditorGUILayout.LabelField("Connected Corridors", corridor.ConnectedCorridors.Count.ToString());

        if (GUILayout.Button("Connect Nodes In List Order")) ConnectNodesInListOrder();
        if (GUILayout.Button("Disconnect All Corridor Nodes")) DisconnectAllNodes();
        if (GUILayout.Button("Disconnect All Corridors")) DisconnectAllCorridors();
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (corridor == null) return;

        // Every Corridor draws its own nodes. The global visualization tool draws all corridors too.
        DrawCorridorNetwork(corridor);

        if (editMode && activeSource == corridor)
        {
            DrawConnectOverlay();
            HandleConnectionClick();
        }
    }

    private static void DrawConnectOverlay()
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(12f, 12f, 340f, 48f), EditorStyles.helpBox);
        GUILayout.Label("CONNECT MODE", EditorStyles.boldLabel);
        GUILayout.Label("Click another Corridor in the Scene.");
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private void HandleConnectionClick()
    {
        Event e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0 || e.alt) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        if (!plane.Raycast(ray, out float distance)) return;

        Vector3 clickPoint = ray.GetPoint(distance);
        CompanyGameCorridor other = FindCorridorAtPoint(clickPoint, corridor);
        if (other == null) return;

        ConnectCorridors(corridor, other, clickPoint);
        e.Use();
    }

    private static CompanyGameCorridor FindCorridorAtPoint(Vector3 point, CompanyGameCorridor source)
    {
        CompanyGameCorridor[] all = Object.FindObjectsByType<CompanyGameCorridor>();
        CompanyGameCorridor best = null;
        float bestDistance = 0.75f;

        foreach (CompanyGameCorridor candidate in all)
        {
            if (candidate == null || candidate == source) continue;

            float distance = DistanceToCorridor(candidate, point);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private static float DistanceToCorridor(CompanyGameCorridor candidate, Vector3 point)
    {
        if (candidate.Nodes.Count == 0)
            return Vector3.Distance(candidate.transform.position, point);

        float best = float.MaxValue;
        CompanyGamePathNode previous = null;

        foreach (CompanyGamePathNode node in candidate.Nodes)
        {
            if (node == null) continue;

            best = Mathf.Min(best, Vector3.Distance(point, node.transform.position));
            if (previous != null)
                best = Mathf.Min(best, DistancePointToSegment(point, previous.transform.position, node.transform.position));
            previous = node;
        }

        return best;
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        if (ab.sqrMagnitude < 0.000001f) return Vector3.Distance(point, a);
        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
        return Vector3.Distance(point, a + ab * t);
    }

    private static void ConnectCorridors(CompanyGameCorridor first, CompanyGameCorridor second, Vector3 position)
    {
        if (first == null || second == null || first == second) return;

        Undo.RecordObject(first, "Connect Corridors");
        Undo.RecordObject(second, "Connect Corridors");

        CompanyGamePathNode firstNode = EnsureConnectionNode(first, position);
        CompanyGamePathNode secondNode = EnsureConnectionNode(second, position);

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
        Debug.Log($"[Company Game] CONNECTED: {first.name} <-> {second.name}");
        SceneView.RepaintAll();
    }

    private static CompanyGamePathNode EnsureConnectionNode(CompanyGameCorridor owner, Vector3 position)
    {
        CompanyGamePathNode nearest = owner.GetNearestNode(position);
        if (nearest != null && Vector3.Distance(nearest.transform.position, position) <= 0.75f)
            return nearest;

        GameObject nodeObject = new GameObject("Path Node");
        Undo.RegisterCreatedObjectUndo(nodeObject, "Create Connection Node");
        nodeObject.transform.SetParent(owner.transform, true);
        nodeObject.transform.position = position;

        CompanyGamePathNode node = nodeObject.AddComponent<CompanyGamePathNode>();
        Undo.RecordObject(owner, "Add Connection Node");
        owner.AddNode(node);
        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(owner);
        return node;
    }

    private static void DrawCorridorNetwork(CompanyGameCorridor target)
    {
        bool connected = target.ConnectedCorridors.Count > 0;
        Handles.color = connected ? Color.green : Color.blue;

        CompanyGamePathNode previous = null;
        foreach (CompanyGamePathNode node in target.Nodes)
        {
            if (node == null) continue;
            float size = HandleUtility.GetHandleSize(node.transform.position) * 0.13f;
            Handles.SphereHandleCap(0, node.transform.position, Quaternion.identity, size, EventType.Repaint);

            if (previous != null)
                Handles.DrawAAPolyLine(2f, previous.transform.position, node.transform.position);
            previous = node;
        }

        foreach (CompanyGamePathNode node in target.Nodes)
        {
            if (node == null) continue;
            foreach (CompanyGamePathNode connection in node.Connections)
            {
                if (connection == null) continue;
                Handles.DrawAAPolyLine(2f, node.transform.position, connection.transform.position);
            }
        }
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
}
