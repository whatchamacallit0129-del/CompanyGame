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

    // IDs are assigned explicitly by Command Agent after creation.
    // No Unity lifecycle callback generates IDs during object construction.
    public void EnsureId(string type) => EnsureIdentity(type);

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
        // Do not scan the scene while an object is being constructed.
        // A persistent EditorPrefs counter is sufficient because IDs are never reused.
        string prefix = GetPrefix(type);
        string key = "CompanyGame.Identity.Next." + type.ToUpperInvariant();
        int next = Math.Max(1, EditorPrefs.GetInt(key, 1));
        string id = prefix + "-" + next.ToString("D6");
        EditorPrefs.SetInt(key, next + 1);
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
}
