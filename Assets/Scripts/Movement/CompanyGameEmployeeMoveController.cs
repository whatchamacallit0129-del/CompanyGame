using UnityEngine;

/// <summary>
/// Runtime command layer for the prototype employee movement system.
/// First click an employee, then click a destination. The controller delegates
/// route calculation and movement to the employee movement component.
/// </summary>
public sealed class CompanyGameEmployeeMoveController : MonoBehaviour
{
    [SerializeField] private LayerMask selectableLayer = ~0;
    [SerializeField] private bool autoAttachMovementToDraggable = true;
    [SerializeField] private int defaultFloor;

    private Camera mainCamera;
    private CompanyGameEmployeeMovement selectedEmployee;
    private DraggableObject2D selectedVisual;

    public CompanyGameEmployeeMovement SelectedEmployee => selectedEmployee;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<CompanyGameEmployeeMoveController>() != null) return;
        GameObject controllerObject = new GameObject("Company Game Employee Move Controller");
        controllerObject.AddComponent<CompanyGameEmployeeMoveController>();
    }

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector3 world = GetMouseWorldPosition();
        CompanyGameEmployeeMovement clickedEmployee = FindEmployeeAt(world);

        if (clickedEmployee != null)
        {
            SelectEmployee(clickedEmployee);
            return;
        }

        if (selectedEmployee == null) return;

        if (selectedEmployee.MoveTo(world))
        {
            Debug.Log($"[Company Game] {selectedEmployee.name} moving to {world}.");
        }
    }

    private CompanyGameEmployeeMovement FindEmployeeAt(Vector3 worldPosition)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition, selectableLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;

            CompanyGameEmployeeMovement movement = hit.GetComponentInParent<CompanyGameEmployeeMovement>();
            if (movement != null) return movement;

            if (!autoAttachMovementToDraggable) continue;

            DraggableObject2D draggable = hit.GetComponentInParent<DraggableObject2D>();
            if (draggable == null) continue;

            movement = draggable.GetComponent<CompanyGameEmployeeMovement>();
            if (movement == null)
                movement = draggable.gameObject.AddComponent<CompanyGameEmployeeMovement>();

            movement.SetFloor(defaultFloor);
            return movement;
        }

        return null;
    }

    private void SelectEmployee(CompanyGameEmployeeMovement employee)
    {
        if (selectedVisual != null)
            selectedVisual.SetSelected(false);

        selectedEmployee = employee;
        selectedVisual = employee.GetComponent<DraggableObject2D>();

        if (selectedVisual != null)
            selectedVisual.SetSelected(true);

        Debug.Log($"[Company Game] Employee selected: {employee.name}");
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 screen = Input.mousePosition;
        Plane movementPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, selectedEmployee != null ? selectedEmployee.transform.position.z : 0f));

        Ray ray = mainCamera.ScreenPointToRay(screen);
        if (movementPlane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);

        screen.z = Mathf.Abs(mainCamera.transform.position.z);
        return mainCamera.ScreenToWorldPoint(screen);
    }
}
