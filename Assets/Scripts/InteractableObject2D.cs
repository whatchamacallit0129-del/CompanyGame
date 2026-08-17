using UnityEngine;

/// <summary>
/// Handles a direct player click on a 2D interactable object.
/// A Collider2D is required so Unity can receive the mouse click.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class InteractableObject2D : MonoBehaviour
{
    private void OnMouseUpAsButton()
    {
        Debug.Log($"[Company Game] Interactable clicked: {gameObject.name}", this);
    }
}
