using System;
using UnityEditor;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CompanyGameObjectIdentity : MonoBehaviour
{
    [SerializeField, HideInInspector] private string objectId;
    [SerializeField, HideInInspector] private string objectType = "Object";

    public string ObjectId => objectId;
    public string ObjectType => objectType;

    private void Reset()
    {
        if (string.IsNullOrEmpty(objectId)) EnsureIdentity(objectType);
    }

    public void EnsureId(string type)
    {
        EnsureIdentity(type);
    }

    public void EnsureIdentity(string type)
    {
        string normalized = NormalizeType(type);
        if (!string.IsNullOrEmpty(objectId))
        {
            if (string.IsNullOrEmpty(objectType)) objectType = normalized;
            EditorUtility.SetDirty(this);
            return;
        }

        objectType = normalized;
        objectId = GenerateId(normalized);
        EditorUtility.SetDirty(this);
    }

    private static string NormalizeType(string type)
    {
        return string.IsNullOrWhiteSpace(type) ? "Object" : type.Trim();
    }

    private static string GenerateId(string type)
    {
        string prefix = GetPrefix(type);
        string key = "CompanyGame.Identity.Next." + type.ToUpperInvariant();
        int next = Math.Max(1, EditorPrefs.GetInt(key, 1));
        string id;
        do
        {
            id = prefix + "-" + next.ToString("D6");
            next++;
        }
        while (IdExists(id));
        EditorPrefs.SetInt(key, next);
        return id;
    }

    private static string GetPrefix(string type)
    {
        if (type.Equals("Employee", StringComparison.OrdinalIgnoreCase)) return "EMP";
        if (type.Equals("Room", StringComparison.OrdinalIgnoreCase)) return "ROOM";
        if (type.Equals("Department", StringComparison.OrdinalIgnoreCase)) return "DEPT";
        if (type.Equals("Machine", StringComparison.OrdinalIgnoreCase)) return "MACH";
        return "OBJ";
    }

#if UNITY_EDITOR
    private static bool IdExists(string id)
    {
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject go in objects)
        {
            if (go == null) continue;
            CompanyGameObjectIdentity identity = go.GetComponent<CompanyGameObjectIdentity>();
            if (identity != null && identity.objectId == id) return true;
        }
        return false;
    }
#endif
}
