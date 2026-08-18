using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
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

                Debug.Log("[Company Game] Command executed: " + command);
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

            case "SAVE_SCENE":
                SaveScene();
                return true;

            default:

                if (command.StartsWith("DELETE_OBJECT:"))
                    return DeleteObjectByName(
                        command.Substring("DELETE_OBJECT:".Length).Trim()
                    );

                if (command.StartsWith("CREATE_OBJECT:"))
                    return CreateObject(
                        command.Substring("CREATE_OBJECT:".Length).Trim()
                    );

                if (command.StartsWith("RENAME_OBJECT:"))
                {
                    string[] parts = command
                        .Substring("RENAME_OBJECT:".Length)
                        .Split(':');

                    return parts.Length >= 2 &&
                           RenameObject(
                               parts[0].Trim(),
                               parts[1].Trim()
                           );
                }

                if (command.StartsWith("SET_POSITION:"))
                    return SetTransform(
                        command.Substring("SET_POSITION:".Length),
                        "position"
                    );

                if (command.StartsWith("SET_ROTATION:"))
                    return SetTransform(
                        command.Substring("SET_ROTATION:".Length),
                        "rotation"
                    );

                if (command.StartsWith("SET_SCALE:"))
                    return SetTransform(
                        command.Substring("SET_SCALE:".Length),
                        "scale"
                    );

                if (command.StartsWith("DUPLICATE_OBJECT:"))
                    return DuplicateObject(
                        command.Substring("DUPLICATE_OBJECT:".Length).Trim()
                    );

                if (command.StartsWith("SET_PARENT:"))
                {
                    string[] parts = command
                        .Substring("SET_PARENT:".Length)
                        .Split(':');

                    return parts.Length >= 2 &&
                           SetParent(
                               parts[0].Trim(),
                               parts[1].Trim()
                           );
                }

                if (command.StartsWith("ADD_COMPONENT:"))
                {
                    string[] parts = command
                        .Substring("ADD_COMPONENT:".Length)
                        .Split(':');

                    return parts.Length >= 2 &&
                           AddComponent(
                               parts[0].Trim(),
                               parts[1].Trim()
                           );
                }

                if (command.StartsWith("REMOVE_COMPONENT:"))
                {
                    string[] parts = command
                        .Substring("REMOVE_COMPONENT:".Length)
                        .Split(':');

                    return parts.Length >= 2 &&
                           RemoveComponent(
                               parts[0].Trim(),
                               parts[1].Trim()
                           );
                }

                Debug.LogWarning(
                    "[Company Game] Unknown command: " + command
                );

                return false;
        }
    }

    private static bool CreateObject(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        if (GameObject.Find(objectName) != null)
            return false;

        GameObject newObject = new GameObject(objectName);

        Undo.RegisterCreatedObjectUndo(
            newObject,
            "Create GameObject"
        );

        Selection.activeGameObject = newObject;

        EditorUtility.SetDirty(newObject);

        return true;
    }

    private static void CreateInteractableObject()
    {
        GameObject interactable =
            new GameObject("InteractableObject");

        SpriteRenderer spriteRenderer =
            interactable.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite =
            AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "Sprites/Default.sprite"
            );

        BoxCollider2D collider =
            interactable.AddComponent<BoxCollider2D>();

        collider.size = Vector2.one;

        interactable.AddComponent<DraggableObject2D>();
        interactable.AddComponent<InteractableObject2D>();

        Undo.RegisterCreatedObjectUndo(
            interactable,
            "Create Interactable Object"
        );

        Selection.activeGameObject = interactable;

        EditorUtility.SetDirty(interactable);
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
    }

    private static bool DeleteObjectByName(string objectName)
    {
        GameObject target =
            GameObject.Find(objectName);

        if (target == null)
            return false;

        Undo.DestroyObjectImmediate(target);

        return true;
    }

    private static bool RenameObject(
        string objectName,
        string newName)
    {
        GameObject target =
            GameObject.Find(objectName);

        if (target == null || string.IsNullOrEmpty(newName))
            return false;

        Undo.RecordObject(target, "Rename GameObject");

        target.name = newName;

        EditorUtility.SetDirty(target);

        Selection.activeGameObject = target;

        return true;
    }

    private static bool SetTransform(
        string data,
        string type)
    {
        string[] parts = data.Split(':');

        if (parts.Length < 4)
            return false;

        GameObject target =
            GameObject.Find(parts[0].Trim());

        if (target == null)
            return false;

        if (!float.TryParse(parts[1], out float x) ||
            !float.TryParse(parts[2], out float y) ||
            !float.TryParse(parts[3], out float z))
            return false;

        Undo.RecordObject(
            target.transform,
            "Set Transform"
        );

        Vector3 value = new Vector3(x, y, z);

        if (type == "position")
            target.transform.position = value;

        else if (type == "rotation")
            target.transform.eulerAngles = value;

        else if (type == "scale")
            target.transform.localScale = value;

        EditorUtility.SetDirty(target);

        return true;
    }

    private static bool DuplicateObject(string objectName)
    {
        GameObject target =
            GameObject.Find(objectName);

        if (target == null)
            return false;

        GameObject duplicate =
            UnityEngine.Object.Instantiate(target);

        duplicate.name =
            target.name + "_Copy";

        Undo.RegisterCreatedObjectUndo(
            duplicate,
            "Duplicate GameObject"
        );

        Selection.activeGameObject = duplicate;

        return true;
    }

    private static bool SetParent(
        string childName,
        string parentName)
    {
        GameObject child =
            GameObject.Find(childName);

        GameObject parent =
            GameObject.Find(parentName);

        if (child == null || parent == null)
            return false;

        Undo.SetTransformParent(
            child.transform,
            parent.transform,
            "Set Parent"
        );

        EditorUtility.SetDirty(child);

        return true;
    }

    private static bool AddComponent(
        string objectName,
        string componentName)
    {
        GameObject target =
            GameObject.Find(objectName);

        if (target == null)
            return false;

        Type componentType =
            GetComponentType(componentName);

        if (componentType == null)
            return false;

        if (target.GetComponent(componentType) != null)
            return false;

        Component component =
            Undo.AddComponent(target, componentType);

        return component != null;
    }

    private static bool RemoveComponent(
        string objectName,
        string componentName)
    {
        GameObject target =
            GameObject.Find(objectName);

        if (target == null)
            return false;

        Type componentType =
            GetComponentType(componentName);

        if (componentType == null)
            return false;

        Component component =
            target.GetComponent(componentType);

        if (component == null)
            return false;

        Undo.DestroyObjectImmediate(component);

        return true;
    }

    private static Type GetComponentType(string componentName)
    {
        switch (componentName.ToUpperInvariant())
        {
            case "SPRITERENDERER":
                return typeof(SpriteRenderer);

            case "BOXCOLLIDER2D":
                return typeof(BoxCollider2D);

            case "CIRCLECOLLIDER2D":
                return typeof(CircleCollider2D);

            case "RIGIDBODY2D":
                return typeof(Rigidbody2D);

            case "CAMERA":
                return typeof(Camera);

            case "AUDIOSOURCE":
                return typeof(AudioSource);

            case "CANVAS":
                return typeof(Canvas);

            default:
                return null;
        }
    }

    private static void SaveScene()
    {
        EditorSceneManager.SaveOpenScenes();

        Debug.Log(
            "[Company Game] Current scene saved."
        );
    }
}