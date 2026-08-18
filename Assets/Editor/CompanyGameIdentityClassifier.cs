using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives newly-created scene objects a category when they do not already have one.
/// Existing IDs are never changed. Add category rules here as the game grows.
/// </summary>
[InitializeOnLoad]
public static class CompanyGameIdentityClassifier
{
    private static bool processing;

    static CompanyGameIdentityClassifier()
    {
        EditorApplication.hierarchyChanged -= ClassifySceneObjects;
        EditorApplication.hierarchyChanged += ClassifySceneObjects;
    }

    private static void ClassifySceneObjects()
    {
        if (processing || Application.isPlaying) return;
        processing = true;
        try
        {
            GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject go in objects)
            {
                if (go == null) continue;
                CompanyGameObjectIdentity identity = go.GetComponent<CompanyGameObjectIdentity>();
                if (identity == null)
                {
                    identity = Undo.AddComponent<CompanyGameObjectIdentity>(go);
                    identity.EnsureIdentity(GetCategory(go.name));
                }
            }
        }
        finally
        {
            processing = false;
        }
    }

    public static string GetCategory(string objectName)
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
