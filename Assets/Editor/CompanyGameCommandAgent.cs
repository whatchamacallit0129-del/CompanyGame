using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CompanyGameCommandAgent
{
    private const string CommandFileName = "command.json";

    private static readonly Dictionary<string, Func<string, bool>> CommandHandlers =
        new Dictionary<string, Func<string, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            { "CREATE_INTERACTABLE_OBJECT", CreateInteractableObject },
            { "CREATE_EMPTY_OBJECT", CreateEmptyObject },
            { "CREATE_OBJECT", CreateEmptyObject },
            { "DELETE_OBJECT", DeleteObjectByName },
            { "RENAME_OBJECT", RenameObject },
            { "SET_ACTIVE", SetActive },
            { "SET_POSITION", SetPosition },
            { "SET_SCALE", SetScale },
            { "SET_ROTATION", SetRotation },
            { "SET_PARENT", SetParent },
            { "ADD_COMPONENT", AddComponent },
            { "REMOVE_COMPONENT", RemoveComponent }
        };

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
        if (!File.Exists(commandPath)) return;

        try
        {
            string command = File.ReadAllText(commandPath).Trim();
            if (string.IsNullOrWhiteSpace(command)) return;

            if (ExecuteCommand(command))
            {
                File.Delete(commandPath);
                AssetDatabase.Refresh();
                Debug.Log("[Company Game] Command executed: " + command);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("[Company Game] Command execution failed:\n" + exception);
        }
    }

    private static bool ExecuteCommand(string rawCommand)
    {
        ParsedCommand parsed = ParseCommand(rawCommand);
        if (parsed == null) return false;
        if (!CommandHandlers.TryGetValue(parsed.Name, out Func<string, bool> handler))
        {
            Debug.LogWarning("[Company Game] Unknown command: " + parsed.Name);
            return false;
        }
        return handler(parsed.Arguments);
    }

    private static ParsedCommand ParseCommand(string rawCommand)
    {
        string[] parts = rawCommand.Split(new[] { ':' }, 2);
        string name = parts[0].Trim().ToUpperInvariant();
        string arguments = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        return string.IsNullOrWhiteSpace(name) ? null : new ParsedCommand(name, arguments);
    }

    private sealed class ParsedCommand
    {
        public string Name { get; }
        public string Arguments { get; }
        public ParsedCommand(string name, string arguments) { Name = name; Arguments = arguments; }
    }

    private static GameObject FindObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return null;
        GameObject target = GameObject.Find(objectName);
        if (target == null) Debug.LogWarning("[Company Game] Object not found: " + objectName);
        return target;
    }

    // CREATE_INTERACTABLE_OBJECT:name[:count]
    // count means "add this many", not "make the total equal to this number".
    // Example: CREATE_INTERACTABLE_OBJECT:권재민:3
    // First call -> 권재민 (1), (2), (3)
    // Next call with :7 -> 권재민 (4) ... (10)
    private static bool CreateInteractableObject(string arguments)
    {
        string[] values = arguments.Split(':');
        string objectName = values.Length > 0 ? values[0].Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(objectName)) objectName = "InteractableObject";

        int count = 1;
        if (values.Length >= 2 && !int.TryParse(values[1].Trim(), out count))
        {
            Debug.LogWarning("[Company Game] CREATE_INTERACTABLE_OBJECT count must be an integer.");
            return false;
        }

        if (count < 1 || count > 1000)
        {
            Debug.LogWarning("[Company Game] CREATE_INTERACTABLE_OBJECT count must be between 1 and 1000.");
            return false;
        }

        if (count == 1 && FindObject(objectName) == null)
        {
            CreateSingleInteractable(objectName);
            Selection.activeGameObject = GameObject.Find(objectName);
            Debug.Log("[Company Game] InteractableObject created: " + objectName);
            return true;
        }

        int nextIndex = GetNextNumberedIndex(objectName);
        GameObject firstCreated = null;

        for (int i = 0; i < count; i++)
        {
            string finalName = objectName + " (" + (nextIndex + i) + ")";
            GameObject interactable = CreateSingleInteractable(finalName);
            if (firstCreated == null) firstCreated = interactable;
        }

        Selection.activeGameObject = firstCreated;
        Debug.Log("[Company Game] InteractableObjects added: " + objectName + " x" + count);
        return true;
    }

    private static GameObject CreateSingleInteractable(string objectName)
    {
        GameObject interactable = new GameObject(objectName);

        SpriteRenderer spriteRenderer = interactable.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("Sprites/Default.sprite");
        spriteRenderer.color = Color.white;

        BoxCollider2D collider = interactable.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        TryAddComponentByName(interactable, "DraggableObject2D");
        TryAddComponentByName(interactable, "InteractableObject2D");

        Undo.RegisterCreatedObjectUndo(interactable, "Create Interactable Object");
        EditorUtility.SetDirty(interactable);
        return interactable;
    }

    private static int GetNextNumberedIndex(string baseName)
    {
        int index = 1;
        while (GameObject.Find(baseName + " (" + index + ")") != null)
            index++;
        return index;
    }

    private static bool CreateEmptyObject(string arguments)
    {
        string objectName = GetOptionalArgument(arguments, "CompanyObject");
        GameObject emptyObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(emptyObject, "Create Company Object");
        Selection.activeGameObject = emptyObject;
        EditorUtility.SetDirty(emptyObject);
        return true;
    }

    private static bool DeleteObjectByName(string arguments)
    {
        GameObject target = FindObject(arguments.Trim());
        if (target == null) return false;
        Undo.DestroyObjectImmediate(target);
        Debug.Log("[Company Game] Object deleted: " + target.name);
        return true;
    }

    private static bool RenameObject(string arguments)
    {
        if (!TryGetArguments(arguments, 2, out string[] values)) return false;
        GameObject target = FindObject(values[0]);
        if (target == null || string.IsNullOrWhiteSpace(values[1])) return false;
        Undo.RecordObject(target, "Rename Object");
        target.name = values[1];
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool SetActive(string arguments)
    {
        if (!TryGetArguments(arguments, 2, out string[] values) || !bool.TryParse(values[1], out bool active)) return false;
        GameObject target = FindObject(values[0]);
        if (target == null) return false;
        Undo.RecordObject(target, "Set Active State");
        target.SetActive(active);
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool SetPosition(string arguments) => SetTransformVector(arguments, "Set Position", (t, v) => t.position = v);
    private static bool SetScale(string arguments) => SetTransformVector(arguments, "Set Scale", (t, v) => t.localScale = v);
    private static bool SetRotation(string arguments) => SetTransformVector(arguments, "Set Rotation", (t, v) => t.eulerAngles = v);

    private static bool SetTransformVector(string arguments, string undoName, Action<Transform, Vector3> apply)
    {
        if (!TryGetVectorArguments(arguments, out GameObject target, out Vector3 value)) return false;
        Undo.RecordObject(target.transform, undoName);
        apply(target.transform, value);
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool SetParent(string arguments)
    {
        if (!TryGetArguments(arguments, 2, out string[] values)) return false;
        GameObject child = FindObject(values[0]);
        if (child == null) return false;
        GameObject parent = null;
        if (!string.IsNullOrWhiteSpace(values[1]) && values[1] != "NONE")
        {
            parent = FindObject(values[1]);
            if (parent == null) return false;
        }
        Undo.SetTransformParent(child.transform, parent?.transform, "Set Parent");
        return true;
    }

    private static bool AddComponent(string arguments)
    {
        if (!TryGetArguments(arguments, 2, out string[] values)) return false;
        GameObject target = FindObject(values[0]);
        if (target == null) return false;
        Type componentType = FindComponentType(values[1]);
        if (!IsValidComponentType(componentType)) return false;
        if (target.GetComponent(componentType) != null) return true;
        Undo.AddComponent(target, componentType);
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool RemoveComponent(string arguments)
    {
        if (!TryGetArguments(arguments, 2, out string[] values)) return false;
        GameObject target = FindObject(values[0]);
        if (target == null) return false;
        Type componentType = FindComponentType(values[1]);
        if (!IsValidComponentType(componentType)) return false;
        Component component = target.GetComponent(componentType);
        if (component == null) return false;
        Undo.DestroyObjectImmediate(component);
        return true;
    }

    private static Type FindComponentType(string typeName)
    {
        string requestedName = typeName.Trim();
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type exactType = assembly.GetType(requestedName);
                if (exactType != null) return exactType;
                foreach (Type candidate in assembly.GetTypes())
                    if (candidate.Name.Equals(requestedName, StringComparison.OrdinalIgnoreCase)) return candidate;
            }
            catch (ReflectionTypeLoadException) { }
        }
        return null;
    }

    private static bool TryAddComponentByName(GameObject target, string typeName)
    {
        Type componentType = FindComponentType(typeName);
        if (!IsValidComponentType(componentType)) return false;
        if (target.GetComponent(componentType) != null) return true;
        Undo.AddComponent(target, componentType);
        return true;
    }

    private static bool IsValidComponentType(Type type) => type != null && typeof(Component).IsAssignableFrom(type) && !type.IsAbstract;

    private static string GetOptionalArgument(string arguments, string fallback)
    {
        string value = arguments.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static bool TryGetArguments(string arguments, int expectedCount, out string[] values)
    {
        values = arguments.Split(':');
        if (values.Length != expectedCount) { values = null; return false; }
        for (int i = 0; i < values.Length; i++) values[i] = values[i].Trim();
        return true;
    }

    private static bool TryGetVectorArguments(string arguments, out GameObject target, out Vector3 value)
    {
        target = null;
        value = Vector3.zero;
        if (!TryGetArguments(arguments, 4, out string[] values) ||
            !TryParseFloat(values[1], out float x) ||
            !TryParseFloat(values[2], out float y) ||
            !TryParseFloat(values[3], out float z)) return false;
        target = FindObject(values[0]);
        if (target == null) return false;
        value = new Vector3(x, y, z);
        return true;
    }

    private static bool TryParseFloat(string value, out float result)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
