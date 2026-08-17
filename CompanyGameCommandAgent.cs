using System.IO;
using UnityEditor;
using UnityEngine;

public static class CompanyGameCommandAgent
{
    private const string CommandPath = "command.json";

    [InitializeOnLoadMethod]
    private static void StartWatching()
    {
        EditorApplication.update -= CheckCommand;
        EditorApplication.update += CheckCommand;
    }

    private static void CheckCommand()
    {
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, CommandPath);

        if (!File.Exists(path))
            return;

        string command = File.ReadAllText(path).Trim();

        if (command == "CREATE_INTERACTABLE_OBJECT")
        {
            CreateInteractableObject();
            File.Delete(path);
            AssetDatabase.Refresh();

            Debug.Log("[Company Game] Command executed: CREATE_INTERACTABLE_OBJECT");
        }
    }

    private static void CreateInteractableObject()
    {
        GameObject interactable = new GameObject("InteractableObject");

        SpriteRenderer spriteRenderer =
            interactable.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite =
            AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "Sprites/Default.sprite");

        BoxCollider2D collider =
            interactable.AddComponent<BoxCollider2D>();

        collider.size = Vector2.one;

        interactable.AddComponent<DraggableObject2D>();

        Undo.RegisterCreatedObjectUndo(
            interactable,
            "Create Interactable Object");

        Selection.activeGameObject = interactable;

        EditorUtility.SetDirty(interactable);
        Debug.Log("[Company Game] InteractableObject created automatically.");
    }
}