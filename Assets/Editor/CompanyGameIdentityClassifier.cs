using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assigns identities to scene objects after Unity finishes processing a hierarchy change.
/// Classification is deliberately deferred so newly-created components are fully initialized
/// before identity code touches them. Existing IDs are never changed.
/// </summary>
[InitializeOnLoad]
public static class CompanyGameIdentityClassifier
{
    private static bool processing;
    private static bool queued;

    static CompanyGameIdentityClassifier()
    {
        EditorApplication.hierarchyChanged -= QueueClassification;
        EditorApplication.hierarchyChanged += QueueClassification;
    }

    private static void QueueClassification()
    {
        if (Application.isPlaying || queued) return;
        queued = true;
        EditorApplication.delayCall -= ClassifySceneObjects;
        EditorApplication.delayCall += ClassifySceneObjects;
    }

    private static void ClassifySceneObjects()
    {
        queued = false;
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
                    if (identity == null) continue;
                }

                if (string.IsNullOrEmpty(identity.ObjectId))
                    identity.EnsureIdentity(GetCategory(go.name));
            }
        }
        finally
        {
            processing = false;
        }
    }

    public static string GetCategory(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return "Object";
        if (StartsWithAny(objectName, "직원", "Employee")) return "Employee";
        if (StartsWithAny(objectName, "방", "Room")) return "Room";
        if (StartsWithAny(objectName, "부서", "Department")) return "Department";
        if (StartsWithAny(objectName, "기계", "Machine")) return "Machine";
        return "Object";
    }

    private static bool StartsWithAny(string value, params string[] prefixes)
    {
        foreach (string prefix in prefixes)
            if (!string.IsNullOrEmpty(prefix) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
