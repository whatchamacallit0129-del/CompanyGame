using System;
using System.Collections.Generic;
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

            if (string.IsNullOrWhiteSpace(command))
            {
                Debug.LogWarning("[Company Game] Empty command ignored.");
                return;
            }

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
        string[] parts = command.Split(new[] { ':' }, 2);
        string commandName = parts[0].Trim().ToUpperInvariant();
        string argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        switch (commandName)
        {
            case "CREATE_INTERACTABLE_OBJECT":
                CreateInteractableObject(argument);
                return true;

            case "CREATE_EMPTY_OBJECT":
                CreateEmptyObject(argument);
                return true;

            case "DELETE_OBJECT":
                return DeleteObjectByName(argument);

            case "RENAME_OBJECT":
                return RenameObject(argument);

            case "SET_ACTIVE":
                return SetActive(argument);

            case "SET_POSITION":
                return SetPosition(argument);

            case "SET_SCALE":
                return SetScale(argument);

            case "SET_ROTATION":
                return SetRotation(argument);

            case "SET_PARENT":
                return SetParent(argument);

            case "ADD_COMPONENT":
                return AddComponent(argument);

            case "REMOVE_COMPONENT":
                return RemoveComponent(argument);

            default:
                Debug.LogWarning("[Company Game] Unknown command: " + command);
                return false;
        }
    }

    private static GameObject FindObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            Debug.LogWarning("[Company Game] Object name is required.");
            return null;
        }

        GameObject target = GameObject.Find(objectName);

        if (target == null)
            Debug.LogWarning("[Company Game] Object not found: " + objectName);

        return target;
    }

    private static void CreateInteractableObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            objectName = "InteractableObject";

        GameObject interactable = new GameObject(objectName);

        SpriteRenderer spriteRenderer = interactable.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("Sprites/Default.sprite");
        spriteRenderer.color = Color.white;

        BoxCollider2D collider = interactable.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        interactable.AddComponent<DraggableObject2D>();
        interactable.AddComponent<InteractableObject2D>();

        Undo.RegisterCreatedObjectUndo(interactable, "Create Interactable Object");
        Selection.activeGameObject = interactable;
        EditorUtility.SetDirty(interactable);

        Debug.Log("[Company Game] InteractableObject created: " + objectName);
    }

    private static void CreateEmptyObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            objectName = "CompanyObject";

        GameObject emptyObject = new GameObject(objectName);

        Undo.RegisterCreatedObjectUndo(emptyObject, "Create Company Object");
        Selection.activeGameObject = emptyObject;
        EditorUtility.SetDirty(emptyObject);

        Debug.Log("[Company Game] Empty object created: " + objectName);
    }

    private static bool DeleteObjectByName(string objectName)
    {
        GameObject target = FindObject(objectName);
        if (target == null)
            return false;

        Undo.DestroyObjectImmediate(target);
        Debug.Log("[Company Game] Object deleted: " + objectName);
        return true;
    }

    private static bool RenameObject(string argument)
    {
        string[] values = SplitArguments(argument, 2);
        if (values.Count != 2)
        {
            Debug.LogWarning("[Company Game] RENAME_OBJECT requires: oldName:newName");
            return false;
        }

        GameObject target = FindObject(values[0]);
        if (target == null || string.IsNullOrWhiteSpace(values[1]))
            return false;

        Undo.RecordObject(target, "Rename Object");
        target.name = values[1];
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool SetActive(string argument)
    {
        string[] values = SplitArguments(argument, 2);
        if (values.Count != 2 || !bool.TryParse(values[1], out bool active))
        {
            Debug.LogWarning("[Company Game] SET_ACTIVE requires: objectName:true/false");
            return false;
        }

        GameObject target = FindObject(values[0]);
        if (target == null)
            return false;

        Undo.RecordObject(target, "Set Active State");
        target.SetActive(active);
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool SetPosition(string argument)
    {
        if (!TryGetTransformVector(argument, out GameObject target, out Vector3 value))
            return false;

        Undo.RecordObject(target.transform, "Set Position");
        target.transform.position = value;
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool SetScale(string argument)
    {
        if (!TryGetTransformVector(argument, out GameObject target, out Vector3 value))
            return false;

        Undo.RecordObject(target.transform, "Set Scale");
        target.transform.localScale = value;
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool SetRotation(string argument)
    {
        if (!TryGetTransformVector(argument, out GameObject target, out Vector3 value))
            return false;

        Undo.RecordObject(target.transform, "Set Rotation");
        target.transform.eulerAngles = value;
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool TryGetTransformVector(
        string argument,
        out GameObject target,
        out Vector3 value)
    {
        target = null;
        value = Vector3.zero;

        string[] values = SplitArguments(argument, 4);
        if (values.Count != 4 ||
            !float.TryParse(values[1], out float x) ||
            !float.TryParse(values[2], out float y) ||
            !float.TryParse(values[3], out float z))
        {
            Debug.LogWarning(
                "[Company Game] Transform command requires: objectName:x:y:z"
            );
            return false;
        }

        target = FindObject(values[0]);
        if (target == null)
            return false;

        value = new Vector3(x, y, z);
        return true;
    }

    private static bool SetParent(string argument)
    {
        string[] values = SplitArguments(argument, 2);
        if (values.Count != 2)
        {
            Debug.LogWarning("[Company Game] SET_PARENT requires: childName:parentName");
            return false;
        }

        GameObject child = FindObject(values[0]);
        if (child == null)
            return false;

        GameObject parent = string.IsNullOrWhiteSpace(values[1])
            ? null
            : FindObject(values[1]);

        if (!string.IsNullOrWhiteSpace(values[1]) && parent == null)
            return false;

        Undo.SetTransformParent(child.transform, parent?.transform, "Set Parent");
        return true;
    }

    private static bool AddComponent(string argument)
    {
        string[] values = SplitArguments(argument, 2);
        if (values.Count != 2)
        {
            Debug.LogWarning("[Company Game] ADD_COMPONENT requires: objectName:ComponentType");
            return false;
        }

        GameObject target = FindObject(values[0]);
        if (target == null)
            return false;

        Type componentType = FindComponentType(values[1]);
        if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
        {
            Debug.LogWarning("[Company Game] Component type not found: " + values[1]);
            return false;
        }

        if (target.GetComponent(componentType) != null)
        {
            Debug.Log("[Company Game] Component already exists: " + values[1]);
            return true;
        }

        Undo.AddComponent(target, componentType);
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool RemoveComponent(string argument)
    {
        string[] values = SplitArguments(argument, 2);
        if (values.Count != 2)
        {
            Debug.LogWarning("[Company Game] REMOVE_COMPONENT requires: objectName:ComponentType");
            return false;
        }

        GameObject target = FindObject(values[0]);
        if (target == null)
            return false;

        Type componentType = FindComponentType(values[1]);
        if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
        {
            Debug.LogWarning("[Company Game] Component type not found: " + values[1]);
            return false;
        }

        Component component = target.GetComponent(componentType);
        if (component == null)
        {
            Debug.LogWarning("[Company Game] Component not found on object: " + values[1]);
            return false;
        }

        Undo.DestroyObjectImmediate(component);
        return true;
    }

    private static Type FindComponentType(string typeName)
    {
        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type type = assembly.GetType(typeName);
                if (type != null)
                    return type;

                foreach (Type candidate in assembly.GetTypes())
                {
                    if (candidate.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Ignore assemblies Unity cannot fully reflect during compilation.
            }
        }

        return null;
    }

    private static List<string> SplitArguments(string argument, int expectedCount)
    {
        string[] raw = argument.Split(':');
        var result = new List<string>(raw.Length);

        foreach (string item in raw)
            result.Add(item.Trim());

        return result.Count == expectedCount ? result : new List<string>();
    }
}
