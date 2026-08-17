using UnityEngine;

/// <summary>
/// Selectable 2D object for the first prototype.
/// SelectionManager2D handles click/drag selection.
/// </summary>
public class DraggableObject2D : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isSelected;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = selected ? Color.yellow : originalColor;
        }
    }

    public bool IsSelected => isSelected;
}
