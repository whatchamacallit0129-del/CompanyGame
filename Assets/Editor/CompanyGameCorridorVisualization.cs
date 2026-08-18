using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CompanyGameCorridorVisualization
{
    private const string ShowAllKey = "CompanyGame.Corridor.ShowAll";
    private static bool showAll;

    static CompanyGameCorridorVisualization()
    {
        showAll = EditorPrefs.GetBool(ShowAllKey, true);
        SceneView.duringSceneGui += DrawAllCorridors;
    }

    public static bool ShowAll
    {
        get => showAll;
        set
        {
            showAll = value;
            EditorPrefs.SetBool(ShowAllKey, value);
            SceneView.RepaintAll();
        }
    }

    [MenuItem("Tools/Company Game/Corridors/Show All Corridors")]
    private static void ShowAllCorridors() => ShowAll = true;

    [MenuItem("Tools/Company Game/Corridors/Hide All Corridors")]
    private static void HideAllCorridors() => ShowAll = false;

    [MenuItem("Tools/Company Game/Corridors/Toggle All Corridor Visibility")]
    private static void ToggleAllCorridors() => ShowAll = !ShowAll;

    private static void DrawAllCorridors(SceneView sceneView)
    {
        if (!ShowAll) return;

        CompanyGameCorridor[] corridors = Object.FindObjectsByType<CompanyGameCorridor>();
        foreach (CompanyGameCorridor corridor in corridors)
        {
            if (corridor == null) continue;
            DrawCorridor(corridor);
        }
    }

    private static void DrawCorridor(CompanyGameCorridor corridor)
    {
        bool corridorConnected = corridor.ConnectedCorridors.Count > 0;

        Handles.color = corridorConnected ? Color.green : Color.blue;

        foreach (CompanyGamePathNode node in corridor.Nodes)
        {
            if (node == null) continue;

            bool nodeConnected = node.Connections.Count > 0;
            bool selected = Selection.activeGameObject == node.gameObject;

            Handles.color = selected
                ? Color.yellow
                : nodeConnected ? Color.green : Color.blue;

            float size = HandleUtility.GetHandleSize(node.transform.position) * 0.14f;
            Handles.SphereHandleCap(
                0,
                node.transform.position,
                Quaternion.identity,
                size,
                EventType.Repaint);

            foreach (CompanyGamePathNode connection in node.Connections)
            {
                if (connection == null) continue;

                Handles.color = Color.green;
                Handles.DrawAAPolyLine(2.5f, node.transform.position, connection.transform.position);
            }
        }

        if (!corridorConnected) return;

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
