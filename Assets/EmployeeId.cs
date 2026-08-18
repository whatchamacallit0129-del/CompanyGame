using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Employee-specific identity. This is the authoritative identity for employees.
/// It is intentionally separate from the generic CompanyGameObjectIdentity.
/// </summary>
[DisallowMultipleComponent]
public sealed class EmployeeId : MonoBehaviour
{
    [SerializeField] private string employeeId;

    public string Id => employeeId;
    public string EmployeeID => employeeId;

    public void EnsureId()
    {
        if (!string.IsNullOrEmpty(employeeId)) return;
#if UNITY_EDITOR
        const string key = "CompanyGame.EmployeeId.Next";
        int next = Math.Max(1, EditorPrefs.GetInt(key, 1));
        employeeId = "EMP-" + next.ToString("D6");
        EditorPrefs.SetInt(key, next + 1);
        EditorUtility.SetDirty(this);
#else
        employeeId = "EMP-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
#endif
    }

    public void SetId(string id)
    {
        if (!string.IsNullOrWhiteSpace(employeeId)) return;
        employeeId = id;
    }
}
