using System;
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

            bool executed = ExecuteCommand(command);

            if (executed)
            {
                File.Delete(commandPath);
                AssetDatabase.Refresh();

                Debug.Log(
                    "[Company Game] Command executed: " + command
                );
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[Company Game] Command execution failed:\n" + exception
            );
        }
    }

    private static bool ExecuteCommand(string command)
    {
        switch (command)
        {
            case "CREATE_INTERACTABLE_OBJECT":
                CreateInteractableObject();
                return true;

            case "CREATE_EMPTY_OBJECT":
                CreateEmptyObject();
                return true;

            default:
                if (command.StartsWith("DELETE_OBJECT:"))
                {
                    string objectName =
                        command.Substring("DELETE_OBJECT:".Length).Trim();

                    return DeleteObjectByName(objectName);
                }

                Debug.LogWarning(
                    "[Company Game] Unknown command: " + command
                );

                return false;
        }
    }

    private static void CreateInteractableObject()
    {
        GameObject interactable =
            new GameObject("InteractableObject");

        interactable.AddComponent<SpriteRenderer>();

        BoxCollider2D collider =
            interactable.AddComponent<BoxCollider2D>();

        collider.size = Vector2.one;

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

    private static void CreateEmptyObject()
    {
        GameObject emptyObject =
            new GameObject("CompanyObject");

        Undo.RegisterCreatedObjectUndo(
            emptyObject,
            "Create Company Object"
        );

        Selection.activeGameObject = emptyObject;

        EditorUtility.SetDirty(emptyObject);

        Debug.Log(
            "[Company Game] CompanyObject created automatically."
        );
    }

    private static bool DeleteObjectByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            Debug.LogWarning(
                "[Company Game] Delete command has no object name."
            );

            return false;
        }

        GameObject target =
            GameObject.Find(objectName);

        if (target == null)
        {
            Debug.LogWarning(
                "[Company Game] Object not found: " + objectName
            );

            return false;
        }

        Undo.DestroyObjectImmediate(target);

        Debug.Log(
            "[Company Game] Object deleted automatically: "
            + objectName
        );

        return true;
    }
}