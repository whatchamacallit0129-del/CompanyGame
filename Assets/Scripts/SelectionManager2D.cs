using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Basic left-click selection system for the first 2D prototype.
/// Click: select one object.
/// Left-drag: draw a selection rectangle and select multiple objects inside it.
/// Objects must have a Collider2D and be on a selectable layer.
/// </summary>
public class SelectionManager2D : MonoBehaviour
{
    [SerializeField] private LayerMask selectableLayer;

    private Camera mainCamera;
    private Vector3 mouseDownWorldPosition;
    private bool isSelecting;
    private readonly List<DraggableObject2D> selectedObjects = new();

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            mouseDownWorldPosition = GetMouseWorldPosition();
            isSelecting = false;
        }

        if (Input.GetMouseButton(0))
        {
            Vector3 currentMousePosition = GetMouseWorldPosition();
            if (Vector2.Distance(mouseDownWorldPosition, currentMousePosition) > 0.1f)
            {
                isSelecting = true;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isSelecting)
            {
                SelectObjectsInRectangle(mouseDownWorldPosition, GetMouseWorldPosition());
            }
            else
            {
                SelectObjectAtPoint(mouseDownWorldPosition);
            }

            isSelecting = false;
        }
    }

    private void SelectObjectAtPoint(Vector3 worldPosition)
    {
        ClearSelection();

        Collider2D hit = Physics2D.OverlapPoint(worldPosition, selectableLayer);
        if (hit == null)
        {
            return;
        }

        DraggableObject2D draggable = hit.GetComponent<DraggableObject2D>();
        if (draggable != null)
        {
            selectedObjects.Add(draggable);
            draggable.SetSelected(true);
            Debug.Log($"Selected: {draggable.gameObject.name}");
        }
    }

    private void SelectObjectsInRectangle(Vector3 start, Vector3 end)
    {
        ClearSelection();

        Vector2 min = Vector2.Min(start, end);
        Vector2 max = Vector2.Max(start, end);
        Bounds selectionBounds = new Bounds((min + max) * 0.5f, max - min);

        Collider2D[] hits = Physics2D.OverlapAreaAll(min, max, selectableLayer);
        foreach (Collider2D hit in hits)
        {
            DraggableObject2D draggable = hit.GetComponent<DraggableObject2D>();
            if (draggable == null || !selectionBounds.Intersects(hit.bounds))
            {
                continue;
            }

            selectedObjects.Add(draggable);
            draggable.SetSelected(true);
        }

        Debug.Log($"Selected objects: {selectedObjects.Count}");
    }

    private void ClearSelection()
    {
        foreach (DraggableObject2D selectedObject in selectedObjects)
        {
            if (selectedObject != null)
            {
                selectedObject.SetSelected(false);
            }
        }

        selectedObjects.Clear();
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        Vector3 screenPosition = Input.mousePosition;
        screenPosition.z = Mathf.Abs(mainCamera.transform.position.z);
        return mainCamera.ScreenToWorldPoint(screenPosition);
    }

    private void OnGUI()
    {
        if (!isSelecting)
        {
            return;
        }

        Vector3 startScreen = mainCamera.WorldToScreenPoint(mouseDownWorldPosition);
        Vector3 currentScreen = Input.mousePosition;

        startScreen.y = Screen.height - startScreen.y;

        Rect rect = new Rect(
            Mathf.Min(startScreen.x, currentScreen.x),
            Mathf.Min(startScreen.y, currentScreen.y),
            Mathf.Abs(startScreen.x - currentScreen.x),
            Mathf.Abs(startScreen.y - currentScreen.y));

        GUI.Box(rect, GUIContent.none);
    }
}
