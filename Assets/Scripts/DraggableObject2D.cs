using UnityEngine;

/// <summary>
/// Simple 2D object interaction for the first prototype.
/// Attach this component to a GameObject that has a Collider2D.
/// Left-click selects the object, and dragging moves it with the mouse.
/// </summary>
public class DraggableObject2D : MonoBehaviour
{
    private Camera mainCamera;
    private Vector3 dragOffset;
    private float objectDepth;
    private bool isDragging;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void OnMouseDown()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        Vector3 mouseWorldPosition = GetMouseWorldPosition();
        objectDepth = mainCamera.WorldToScreenPoint(transform.position).z;
        dragOffset = transform.position - mouseWorldPosition;
        isDragging = true;

        Debug.Log($"Selected: {gameObject.name}");
    }

    private void OnMouseDrag()
    {
        if (!isDragging)
        {
            return;
        }

        transform.position = GetMouseWorldPosition() + dragOffset;
    }

    private void OnMouseUp()
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        Debug.Log($"Released: {gameObject.name}");
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPosition = Input.mousePosition;
        mouseScreenPosition.z = objectDepth;

        return mainCamera.ScreenToWorldPoint(mouseScreenPosition);
    }
}
