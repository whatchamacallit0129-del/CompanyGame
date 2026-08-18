using UnityEngine;

/// <summary>
/// Employee-specific identity component.
/// Kept separate from CompanyGameObjectIdentity so employee data can grow independently
/// without coupling all object types to employee-only fields.
/// </summary>
[DisallowMultipleComponent]
public sealed class EmployeeId : MonoBehaviour
{
    [SerializeField] private string employeeId;

    public string Id => employeeId;
    public string EmployeeID => employeeId;

    public void SetId(string id)
    {
        if (!string.IsNullOrWhiteSpace(employeeId)) return;
        employeeId = id;
    }
}
