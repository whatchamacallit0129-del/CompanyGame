using System;
using UnityEngine;

/// <summary>
/// Category helper for Command Agent object creation.
/// Automatic hierarchy scanning is intentionally disabled.
/// Objects created by the Command Agent receive their identity explicitly during creation,
/// preventing hierarchyChanged callbacks from interrupting batch creation.
/// </summary>
public static class CompanyGameIdentityClassifier
{
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
