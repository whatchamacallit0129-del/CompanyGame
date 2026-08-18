using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lobotomy-Corp-style command input: left click/drag selects employees;
/// right click issues a move command to all selected employees.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class CompanyGameEmployeeSelectionController : MonoBehaviour
{
    [SerializeField] private LayerMask employeeLayer = ~0;
    [SerializeField] private float dragThreshold = 8f;
    [SerializeField] private float groupSpacing = 0.45f;

    private readonly List<CompanyGameEmployeeMovement> selected = new List<CompanyGameEmployeeMovement>();
    private Camera mainCamera;
    private Vector2 pointerDown;
    private bool dragging;

    public IReadOnlyList<CompanyGameEmployeeMovement> Selected => selected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<CompanyGameEmployeeSelectionController>() != null) return;
        new GameObject("Company Game Employee Selection").AddComponent<CompanyGameEmployeeSelectionController>();
    }

    private void Awake() => mainCamera = Camera.main;

    private void Update()
    {
        if (Mouse.current == null) return;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            pointerDown = Mouse.current.position.ReadValue();
            dragging = false;
        }

        if (Mouse.current.leftButton.isPressed &&
            Vector2.Distance(pointerDown, Mouse.current.position.ReadValue()) >= dragThreshold)
            dragging = true;

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (dragging) SelectBox(pointerDown, Mouse.current.position.ReadValue());
            else SelectSingle(Mouse.current.position.ReadValue());
            dragging = false;
        }

        if (Mouse.current.rightButton.wasPressedThisFrame && selected.Count > 0)
            MoveSelected(Mouse.current.position.ReadValue());
    }

    private void SelectSingle(Vector2 screen)
    {
        Vector3 world = ScreenToWorld(screen);
        Collider2D[] hits = Physics2D.OverlapPointAll(world, employeeLayer);
        CompanyGameEmployeeMovement employee = null;

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            employee = hit.GetComponentInParent<CompanyGameEmployeeMovement>();
            if (employee != null) break;
        }

        ClearSelection();
        if (employee != null) AddSelection(employee);
    }

    private void SelectBox(Vector2 start, Vector2 end)
    {
        Vector3 a = ScreenToWorld(start);
        Vector3 b = ScreenToWorld(end);
        Bounds bounds = new Bounds((a + b) * 0.5f, new Vector3(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y), 10f));

        ClearSelection();
        Collider2D[] hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f, employeeLayer);
        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            CompanyGameEmployeeMovement employee = hit.GetComponentInParent<CompanyGameEmployeeMovement>();
            if (employee != null) AddSelection(employee);
        }
    }

    private void MoveSelected(Vector2 screen)
    {
        Vector3 destination = ScreenToWorld(screen);
        int count = selected.Count;

        for (int i = 0; i < count; i++)
        {
            CompanyGameEmployeeMovement employee = selected[i];
            if (employee == null) continue;

            Vector3 offset = count <= 1 ? Vector3.zero : FormationOffset(i, count);
            employee.MoveTo(destination + offset);
        }
    }

    private static Vector3 FormationOffset(int index, int count)
    {
        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        int row = index / columns;
        int column = index % columns;
        float center = (Mathf.Min(columns, count - row * columns) - 1) * 0.5f;
        return new Vector3((column - center) * 0.45f, -row * 0.45f, 0f);
    }

    private void AddSelection(CompanyGameEmployeeMovement employee)
    {
        if (employee == null || selected.Contains(employee)) return;
        selected.Add(employee);
        DraggableObject2D visual = employee.GetComponent<DraggableObject2D>();
        if (visual != null) visual.SetSelected(true);
    }

    public void ClearSelection()
    {
        foreach (CompanyGameEmployeeMovement employee in selected)
        {
            if (employee == null) continue;
            DraggableObject2D visual = employee.GetComponent<DraggableObject2D>();
            if (visual != null) visual.SetSelected(false);
        }
        selected.Clear();
    }

    private Vector3 ScreenToWorld(Vector2 screen)
    {
        Ray ray = mainCamera.ScreenPointToRay(screen);
        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.zero;
    }

    private void OnGUI()
    {
        if (!dragging) return;
        Vector2 current = Mouse.current.position.ReadValue();
        current.y = Screen.height - current.y;
        Vector2 start = pointerDown;
        start.y = Screen.height - start.y;
        Rect rect = Rect.MinMaxRect(
            Mathf.Min(start.x, current.x), Mathf.Min(start.y, current.y),
            Mathf.Max(start.x, current.x), Mathf.Max(start.y, current.y));
        GUI.Box(rect, GUIContent.none);
    }
}
