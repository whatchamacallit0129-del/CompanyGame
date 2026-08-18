using UnityEngine;

/// <summary>
/// Shared, editable movement policy for employees.
/// Keeps tuning out of movement code so different employee types can reuse
/// the same movement system with different assets later.
/// </summary>
[CreateAssetMenu(fileName = "EmployeeMovementSettings", menuName = "Company Game/Movement/Employee Movement Settings")]
public sealed class CompanyGameEmployeeMovementSettings : ScriptableObject
{
    [Header("Speed")]
    [Min(0.01f)] [SerializeField] private float moveSpeed = 2f;
    [Min(0.01f)] [SerializeField] private float acceleration = 8f;
    [Min(0.01f)] [SerializeField] private float deceleration = 12f;

    [Header("Path Following")]
    [Min(0.001f)] [SerializeField] private float nodeArrivalDistance = 0.06f;
    [Min(0.001f)] [SerializeField] private float destinationArrivalDistance = 0.06f;
    [SerializeField] private bool useUnscaledTime;

    [Header("Formation")]
    [Min(0f)] [SerializeField] private float groupSpacing = 0.45f;

    [Header("Navigation")]
    [Min(0.01f)] [SerializeField] private float nodeSnapDistance = 2.5f;

    public float MoveSpeed => Mathf.Max(0.01f, moveSpeed);
    public float Acceleration => Mathf.Max(0.01f, acceleration);
    public float Deceleration => Mathf.Max(0.01f, deceleration);
    public float NodeArrivalDistance => Mathf.Max(0.001f, nodeArrivalDistance);
    public float DestinationArrivalDistance => Mathf.Max(0.001f, destinationArrivalDistance);
    public bool UseUnscaledTime => useUnscaledTime;
    public float GroupSpacing => Mathf.Max(0f, groupSpacing);
    public float NodeSnapDistance => Mathf.Max(0.01f, nodeSnapDistance);
}
