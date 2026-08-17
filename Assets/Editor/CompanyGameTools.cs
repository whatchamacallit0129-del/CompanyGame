using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tools for quickly creating common CompanyGame prototype objects.
/// </summary>
public static class CompanyGameTools
{
    private const string MenuPath = "Company Game/Create Interactable Object";

    [MenuItem(MenuPath)]
    private static void CreateInteractableObject()
    {
        GameObject interactable = new GameObject("InteractableObject");

        SpriteRenderer spriteRenderer = interactable.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("Sprites/Default.sprite");
        spriteRenderer.color = Color.white;

        BoxCollider2D collider = interactable.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        // Reuse the project's existing selection component.
        DraggableObject2D draggable = interactable.AddComponent<DraggableObject2D>();
        interactable.AddComponent<InteractableObject2D>();

        Undo.RegisterCreatedObjectUndo(interactable, "Create Interactable Object");
        Selection.activeGameObject = interactable;
        EditorGUIUtility.PingObject(interactable);

        Debug.Log("[Company Game] InteractableObject created with SpriteRenderer, BoxCollider2D, DraggableObject2D and InteractableObject2D.");
    }
}

