```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CompanyGameCommandAgent
{
    private const string CommandFileName = "command.json";

    [InitializeOnLoadMethod]
    private static void StartWatching()
    {
        EditorApplication.update -= CheckCommand;
        EditorApplication.update += CheckCommand;

        Debug.Log("[Company Game] Command Agent started.");
    }

    private static void CheckCommand()
    {
        string projectPath = Directory.GetParent(Application.dataPath).FullName;
        string commandPath = Path.Combine(projectPath, CommandFileName);

        if (!File.Exists(commandPath))
            return;

        try
        {
            string command = File.ReadAllText(commandPath).Trim();

            if (command == "CREATE_INTERACTABLE_OBJECT")
            {
                CreateInteractableObject();

                File.Delete(commandPath);

                AssetDatabase.Refresh();

                Debug.Log(
                    "[Company Game] Command executed: CREATE_INTERACTABLE_OBJECT"
                );
            }
            else
            {
                Debug.LogWarning(
                    "[Company Game] Unknown command: " + command
                );
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "[Company Game] Command execution failed:\n" +
                exception
            );
        }
    }

    private static void CreateInteractableObject()
    {
        GameObject interactable =
            new GameObject("InteractableObject");

        SpriteRenderer spriteRenderer =
            interactable.AddComponent<SpriteRenderer>();

        BoxCollider2D collider =
            interactable.AddComponent<BoxCollider2D>();

        collider.size = Vector2.one;

        // DraggableObject2D가 프로젝트에 존재할 경우 추가
        if (System.Type.GetType("DraggableObject2D") != null)
        {
            interactable.AddComponent<DraggableObject2D>();
        }

        Undo.RegisterCreatedObjectUndo(
            interactable,
            "Create Interactable Object"
        );

        Selection.activeGameObject = interactable;

        EditorUtility.SetDirty(interactable);

        Debug.Log(
            "[Company Game] InteractableObject created automatically."
        );
    }
}
```
