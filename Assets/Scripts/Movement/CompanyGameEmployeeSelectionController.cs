using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lobotomy-Corp-inspired command input: left click selects an employee,
/// left drag selects a group, and right click issues one destination command.
/// Destination clicks on a Node are snapped to that Node. The controller
/// contains no employee-specific rules.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class CompanyGameEmployeeSelectionController : MonoBehaviour
{
    [SerializeField] private LayerMask employeeLayer = ~0;
    [SerializeField] private LayerMask nodeLayer = ~0;
    [Min(1f)] [SerializeField] private float dragThreshold = 8f;
    [Min(1f)] [SerializeField] private float selectionFallbackRadius = 0.75f;
    [SerializeField] private bool allowBoxSelection = true;
    [SerializeField] private bool snapDestinationToNode = true;

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

        if (allowBoxSelection && Mouse.current.leftButton.isPressed &&
            Vector2.Distance(pointerDown, Mouse.current.position.ReadValue()) >= dragThreshold)
            dragging = true;

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (dragging && allowBoxSelection) SelectBox(pointerDown, Mouse.current.position.ReadValue());
            else SelectSingle(Mouse.current.position.ReadValue());
            dragging = false;
        }

        if (Mouse.current.rightButton.wasPressedThisFrame && selected.Count > 0)
            MoveSelected(Mouse.current.position.ReadValue());
    }

    private void SelectSingle(Vector2 screen)
    {
        Vector3 world = ScreenToWorld(screen);
        CompanyGameEmployeeMovement employee = FindEmployeeByCollider(world);

        // Fallback makes selection work even when an employee prefab has no
        // Collider2D yet. It is data-driven and can be tuned without code changes.
        if (employee == null)
            employee = FindNearestEmployee(world, selectionFallbackRadius);

        ClearSelection();
        if (employee != null) AddSelection(employee);
    }

    private CompanyGameEmployeeMovement FindEmployeeByCollider(Vector3 world)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(world, employeeLayer);
        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            CompanyGameEmployeeMovement employee = hit.GetComponentInParent<CompanyGameEmployeeMovement>();
            if (employee != null) return employee;
        }
        return null;
    }

    private static CompanyGameEmployeeMovement FindNearestEmployee(Vector3 world, float radius)
    {
        CompanyGameEmployeeMovement[] employees = FindObjectsByType<CompanyGameEmployeeMovement>(FindObjectsSortMode.None);
        CompanyGameEmployeeMovement nearest = null;
        float best = radius * radius;

        foreach (CompanyGameEmployeeMovement employee in employees)
        {
            if (employee == null) continue;
            float distance = (employee.transform.position - world).sqrMagnitude;
            if (distance <= best)
            {
                best = distance;
                nearest = employee;
            }
        }

        return nearest;
    }

    private void SelectBox(Vector2 start, Vector2 end)
    {
        Vector3 a = ScreenToWorld(start);
        Vector3 b = ScreenToWorld(end);
        Bounds bounds = new Bounds((a + b) * 0.5f,
            new Vector3(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y), 10f));

        ClearSelection();
        Collider2D[] hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f, employeeLayer);
        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            CompanyGameEmployeeMovement employee = hit.GetComponentInParent<CompanyGameEmployeeMovement>();
            if (employee != null) AddSelection(employee);
        }

        // Collider-free fallback for box selection.
        if (selected.Count == 0)
        {
            foreach (CompanyGameEmployeeMovement employee in FindObjectsByType<CompanyGameEmployeeMovement>(FindObjectsSortMode.None))
            {
                if (employee != null && bounds.Contains(employee.transform.position)) AddSelection(employee);
            }
        }
    }

    private void MoveSelected(Vector2 screen)
    {
        Vector3 destination = ScreenToWorld(screen);
        if (snapDestinationToNode)
        {
            CompanyGamePathNode node = FindNearestNode(destination);
            if (node != null) destination = node.transform.position;
        }

        int count = selected.Count;
        float spacing = GetGroupSpacing();
        for (int i = 0; i < count; i++)
        {
            CompanyGameEmployeeMovement employee = selected[i];
            if (employee == null) continue;
            employee.MoveTo(destination + FormationOffset(i, count, spacing));
        }
    }

    private CompanyGamePathNode FindNearestNode(Vector3 world)
    {
        CompanyGameNavigationGraph graph = CompanyGameNavigationGraph.Instance;
        graph.Refresh();
        CompanyGamePathNode nearest = null;
        float best = float.PositiveInfinity;

        foreach (CompanyGamePathNode node in graph.Nodes)
        {
            if (node == null) continue;
            float distance = (node.transform.position - world).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = node;
            }
        }
        return nearest;
    }

    private float GetGroupSpacing()
    {
        foreach (CompanyGameEmployeeMovement employee in selected)
        {
            if (employee == null) continue;
            if (employee.Settings != null) return employee.Settings.GroupSpacing;
        }
        return 0.45f;
    }

    private static Vector3 FormationOffset(int index, int count, float spacing)
    {
        if (count <= 1 || spacing <= 0f) return Vector3.zero;
        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        int row = index / columns;
        int column = index % columns;
        int rowCount = Mathf.Min(columns, count - row * columns);
        float center = (rowCount - 1) * 0.5f;
        return new Vector3((column - center) * spacing, -row * spacing, 0f);
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
