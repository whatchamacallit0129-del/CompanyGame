using UnityEngine;

/// <summary>
/// Ensures every employee receives the reusable movement agent at runtime.
/// Employee identity remains the source of truth; movement is an attached capability.
/// </summary>
public static class CompanyGameEmployeeMovementBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureEmployeeMovementAgents()
    {
        EmployeeId[] employees = Object.FindObjectsByType<EmployeeId>(FindObjectsInactive.Include);
        foreach (EmployeeId employee in employees)
        {
            if (employee == null) continue;
            if (employee.GetComponent<CompanyGameEmployeeMovement>() == null)
                employee.gameObject.AddComponent<CompanyGameEmployeeMovement>();
        }
    }
}
