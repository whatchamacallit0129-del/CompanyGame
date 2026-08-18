using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class CompanyGameObjectIdentity : MonoBehaviour
{
    [SerializeField, HideInInspector] private string objectId;
    [SerializeField, HideInInspector] private string objectType = "Object";

    public string ObjectId => objectId;
    public string ObjectType => objectType;

    public void EnsureId(string type) => EnsureIdentity(type);

    public void EnsureIdentity(string type)
    {
        string normalized = NormalizeType(type);

        if (!string.IsNullOrEmpty(objectId))
        {
            if (string.IsNullOrEmpty(objectType) || objectType.Equals("Object", StringComparison.OrdinalIgnoreCase))
                objectType = normalized;
            EnsureSpecializedComponent(normalized);
            return;
        }

        objectType = normalized;
        objectId = GenerateId(normalized);
        EnsureSpecializedComponent(normalized);
    }

    private void EnsureSpecializedComponent(string normalized)
    {
        if (!normalized.Equals("Employee", StringComparison.OrdinalIgnoreCase)) return;

        EmployeeId employeeId = GetComponent<EmployeeId>();
        if (employeeId == null)
            employeeId = gameObject.AddComponent<EmployeeId>();

        employeeId.SetId(objectId);
    }

    private static string NormalizeType(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) return "Object";
        string value = type.Trim();
        if (value.Equals("직원", StringComparison.OrdinalIgnoreCase) || value.Equals("Employee", StringComparison.OrdinalIgnoreCase)) return "Employee";
        if (value.Equals("방", StringComparison.OrdinalIgnoreCase) || value.Equals("Room", StringComparison.OrdinalIgnoreCase)) return "Room";
        if (value.Equals("부서", StringComparison.OrdinalIgnoreCase) || value.Equals("Department", StringComparison.OrdinalIgnoreCase)) return "Department";
        if (value.Equals("기계", StringComparison.OrdinalIgnoreCase) || value.Equals("Machine", StringComparison.OrdinalIgnoreCase)) return "Machine";
        return value;
    }

    private static string GenerateId(string type)
    {
        string prefix = GetPrefix(type);
        string key = "CompanyGame.Identity.Next." + type.ToUpperInvariant();
#if UNITY_EDITOR
        int next = Math.Max(1, EditorPrefs.GetInt(key, 1));
        string id = prefix + "-" + next.ToString("D6");
        EditorPrefs.SetInt(key, next + 1);
        return id;
#else
        return prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
#endif
    }

    private static string GetPrefix(string type)
    {
        if (type.Equals("Employee", StringComparison.OrdinalIgnoreCase) || type.Equals("직원", StringComparison.OrdinalIgnoreCase)) return "EMP";
        if (type.Equals("Room", StringComparison.OrdinalIgnoreCase) || type.Equals("방", StringComparison.OrdinalIgnoreCase)) return "ROOM";
        if (type.Equals("Department", StringComparison.OrdinalIgnoreCase) || type.Equals("부서", StringComparison.OrdinalIgnoreCase)) return "DEPT";
        if (type.Equals("Machine", StringComparison.OrdinalIgnoreCase) || type.Equals("기계", StringComparison.OrdinalIgnoreCase)) return "MACH";
        return "OBJ";
    }
}
