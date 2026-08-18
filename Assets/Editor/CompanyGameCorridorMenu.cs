using UnityEditor;
using UnityEngine;

public static class CompanyGameCorridorMenu
{
    [MenuItem("Tools/Company Game/Create Corridor")]
    private static void CreateCorridor()
    {
        GameObject corridorObject = new GameObject("Corridor");
        Undo.RegisterCreatedObjectUndo(corridorObject, "Create Corridor");

        CompanyGameCorridor corridor = corridorObject.AddComponent<CompanyGameCorridor>();
        Selection.activeGameObject = corridorObject;
        EditorUtility.SetDirty(corridor);
    }
}
