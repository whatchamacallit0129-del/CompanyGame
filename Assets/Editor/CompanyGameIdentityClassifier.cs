using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assigns category-specific persistent IDs without coupling the Command Agent to a fixed list of object types.
/// Add new rules here later (Room, Department, Machine, etc.) without changing the identity system.
/// </summary>
[InitializeOnLoad]
public static class CompanyGameIdentityClassifier
{
    static CompanyGameIdentityClassifier()
    {
        EditorApplication.hierarchyChanged -= ClassifySceneObjects;
        EditorApplication.hierarchyChanged += ClassifySceneObjects;
    }

    private static void ClassifySceneObjects()
    {
        if (Application.isPlaying) return;

        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject go in objects)
        {
            if (go == null) continue;

            CompanyGameObjectIdentity identity = go.GetComponent<CompanyGameObjectIdentity>();
            if (identity == null)
                identity = Undo.AddComponent<CompanyGameObjectIdentity>(go);

            string category = GetCategory(go.name);
            identity.EnsureIdentity(category);
        }
    }

    private static string GetCategory(string objectName)
    {
        if (StartsWithAny(objectName, "직원", "Employee")) return "Employee";
        if (StartsWithAny(objectName, "방", "Room")) return "Room";
        if (StartsWithAny(objectName, "부서", "Department")) return "Department";
        if (StartsWithAny(objectName, "기계", "Machine")) return "Machine";
        return "Object";
    }

    private static bool StartsWithAny(string value, params string[] prefixes)
    {
        foreach (string prefix in prefixes)
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
