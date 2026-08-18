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
    private const string ResultFileName = "result.json";

    private static readonly Dictionary<string, Func<CommandRequest, CommandResult>> CommandHandlers =
        new Dictionary<string, Func<CommandRequest, CommandResult>>(StringComparer.OrdinalIgnoreCase)
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
            string rawCommand = File.ReadAllText(commandPath).Trim();
            if (string.IsNullOrWhiteSpace(rawCommand)) return;

            CommandRequest request = ParseCommand(rawCommand);
            CommandResult result = ExecuteCommand(request);
            WriteResult(projectPath, result);

            if (result.Success)
            {
                File.Delete(commandPath);
                AssetDatabase.Refresh();
            }

            Debug.Log("[Company Game] " + result.Message);
        }
        catch (Exception exception)
        {
            CommandResult result = CommandResult.Failure("Command execution failed: " + exception.Message);
            WriteResult(projectPath, result);
            Debug.LogError("[Company Game] " + exception);
        }
    }

    private static CommandRequest ParseCommand(string rawCommand)
    {
        string[] parts = rawCommand.Split(new[] { ':' }, 2);
        string name = parts[0].Trim().ToUpperInvariant();
        string arguments = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        return new CommandRequest(name, arguments);
    }

    private static CommandResult ExecuteCommand(CommandRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
            return CommandResult.Failure("Command name is empty.");

        if (!CommandHandlers.TryGetValue(request.Name, out Func<CommandRequest, CommandResult> handler))
            return CommandResult.Failure("Unknown command: " + request.Name);

        return handler(request);
    }

    private sealed class CommandRequest
    {
        public string Name { get; }
        public string Arguments { get; }

        public CommandRequest(string name, string arguments)
        {
            Name = name;
            Arguments = arguments;
        }
    }

    private sealed class CommandResult
    {
        public bool Success { get; }
        public string Message { get; }
        public List<string> CreatedObjects { get; } = new List<string>();

        private CommandResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static CommandResult SuccessResult(string message)
        {
            return new CommandResult(true, message);
        }

        public static CommandResult Failure(string message)
        {
            return new CommandResult(false, message);
        }
    }

    // CREATE_INTERACTABLE_OBJECT:name[:count]
    // count means "add this many".
    // Example: :3 then :7 => 10 total, assuming the first three still exist.
    private static CommandResult CreateInteractableObject(CommandRequest request)
    {
        string[] values = request.Arguments.Split(':');
        string objectName = values.Length > 0 ? values[0].Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(objectName)) objectName = "InteractableObject";

        int count = 1;
        if (values.Length >= 2 && !int.TryParse(values[1].Trim(), out count))
            return CommandResult.Failure("CREATE_INTERACTABLE_OBJECT count must be an integer.");

        if (count < 1 || count > 1000)
            return CommandResult.Failure("CREATE_INTERACTABLE_OBJECT count must be between 1 and 1000.");

        CommandResult result = CommandResult.SuccessResult("Created " + count + " interactable object(s): " + objectName);

        if (count == 1)
        {
            string finalName = GameObject.Find(objectName) == null ? objectName : GetNextNumberedName(objectName);
            CreateSingleInteractable(finalName);
            result.CreatedObjects.Add(finalName);
            Selection.activeGameObject = GameObject.Find(finalName);
            return result;
        }

        int nextIndex = GetNextNumberedIndex(objectName);
        GameObject firstCreated = null;

        for (int i = 0; i < count; i++)
        {
            string finalName = objectName + " (" + (nextIndex + i) + ")";
            GameObject created = CreateSingleInteractable(finalName);
            if (firstCreated == null) firstCreated = created;
            result.CreatedObjects.Add(finalName);
        }

        Selection.activeGameObject = firstCreated;
        return result;
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

    private static string GetNextNumberedName(string baseName)
    {
        return baseName + " (" + GetNextNumberedIndex(baseName) + ")";
    }

    private static CommandResult CreateEmptyObject(CommandRequest request)
    {
        string objectName = string.IsNullOrWhiteSpace(request.Arguments) ? "CompanyObject" : request.Arguments.Trim();
        GameObject emptyObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(emptyObject, "Create Company Object");
        Selection.activeGameObject = emptyObject;
        EditorUtility.SetDirty(emptyObject);
        return CommandResult.SuccessResult("Created object: " + objectName);
    }

    private static CommandResult DeleteObjectByName(CommandRequest request)
    {
        GameObject target = FindObject(request.Arguments.Trim());
        if (target == null) return CommandResult.Failure("Object not found: " + request.Arguments.Trim());
        string name = target.name;
        Undo.DestroyObjectImmediate(target);
        return CommandResult.SuccessResult("Deleted object: " + name);
    }

    private static CommandResult RenameObject(CommandRequest request)
    {
        if (!TryGetArguments(request.Arguments, 2, out string[] values))
            return CommandResult.Failure("RENAME_OBJECT requires objectName:newName.");
        GameObject target = FindObject(values[0]);
        if (target == null) return CommandResult.Failure("Object not found: " + values[0]);
        Undo.RecordObject(target, "Rename Object");
        target.name = values[1];
        EditorUtility.SetDirty(target);
        return CommandResult.SuccessResult("Renamed object to: " + values[1]);
    }

    private static CommandResult SetActive(CommandRequest request)
    {
        if (!TryGetArguments(request.Arguments, 2, out string[] values) || !bool.TryParse(values[1], out bool active))
            return CommandResult.Failure("SET_ACTIVE requires objectName:true|false.");
        GameObject target = FindObject(values[0]);
        if (target == null) return CommandResult.Failure("Object not found: " + values[0]);
        Undo.RecordObject(target, "Set Active State");
        target.SetActive(active);
        EditorUtility.SetDirty(target);
        return CommandResult.SuccessResult("Set active: " + values[0] + " = " + active);
    }

    private static CommandResult SetPosition(CommandRequest request) => SetTransformVector(request, "Set Position", (t, v) => t.position = v);
    private static CommandResult SetScale(CommandRequest request) => SetTransformVector(request, "Set Scale", (t, v) => t.localScale = v);
    private static CommandResult SetRotation(CommandRequest request) => SetTransformVector(request, "Set Rotation", (t, v) => t.eulerAngles = v);

    private static CommandResult SetTransformVector(CommandRequest request, string undoName, Action<Transform, Vector3> apply)
    {
        if (!TryGetVectorArguments(request.Arguments, out GameObject target, out Vector3 value))
            return CommandResult.Failure("Transform command requires objectName:x:y:z.");
        Undo.RecordObject(target.transform, undoName);
        apply(target.transform, value);
        EditorUtility.SetDirty(target);
        return CommandResult.SuccessResult(undoName + ": " + target.name);
    }

    private static CommandResult SetParent(CommandRequest request)
    {
        if (!TryGetArguments(request.Arguments, 2, out string[] values))
            return CommandResult.Failure("SET_PARENT requires child:parent or child:NONE.");
        GameObject child = FindObject(values[0]);
        if (child == null) return CommandResult.Failure("Object not found: " + values[0]);
        GameObject parent = null;
        if (!string.IsNullOrWhiteSpace(values[1]) && !values[1].Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            parent = FindObject(values[1]);
            if (parent == null) return CommandResult.Failure("Parent not found: " + values[1]);
        }
        Undo.SetTransformParent(child.transform, parent?.transform, "Set Parent");
        return CommandResult.SuccessResult("Set parent: " + child.name);
    }

    private static CommandResult AddComponent(CommandRequest request)
    {
        if (!TryGetArguments(request.Arguments, 2, out string[] values))
            return CommandResult.Failure("ADD_COMPONENT requires objectName:componentType.");
        GameObject target = FindObject(values[0]);
        if (target == null) return CommandResult.Failure("Object not found: " + values[0]);
        Type componentType = FindComponentType(values[1]);
        if (!IsValidComponentType(componentType)) return CommandResult.Failure("Component type not found: " + values[1]);
        if (target.GetComponent(componentType) != null)
            return CommandResult.SuccessResult("Component already exists: " + values[1]);
        Undo.AddComponent(target, componentType);
        EditorUtility.SetDirty(target);
        return CommandResult.SuccessResult("Added component: " + values[1]);
    }

    private static CommandResult RemoveComponent(CommandRequest request)
    {
        if (!TryGetArguments(request.Arguments, 2, out string[] values))
            return CommandResult.Failure("REMOVE_COMPONENT requires objectName:componentType.");
        GameObject target = FindObject(values[0]);
        if (target == null) return CommandResult.Failure("Object not found: " + values[0]);
        Type componentType = FindComponentType(values[1]);
        if (!IsValidComponentType(componentType)) return CommandResult.Failure("Component type not found: " + values[1]);
        Component component = target.GetComponent(componentType);
        if (component == null) return CommandResult.Failure("Component not found on object: " + values[1]);
        Undo.DestroyObjectImmediate(component);
        return CommandResult.SuccessResult("Removed component: " + values[1]);
    }

    private static GameObject FindObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return null;
        return GameObject.Find(objectName);
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

    private static bool TryGetArguments(string arguments, int expectedCount, out string[] values)
    {
        values = arguments.Split(':');
        if (values.Length != expectedCount)
        {
            values = null;
            return false;
        }
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

    private static void WriteResult(string projectPath, CommandResult result)
    {
        string resultPath = Path.Combine(projectPath, ResultFileName);
        string created = string.Join(",", result.CreatedObjects.ToArray());
        string json = "{\n" +
                      "  \"success\": " + result.Success.ToString().ToLowerInvariant() + ",\n" +
                      "  \"message\": \"" + EscapeJson(result.Message) + "\",\n" +
                      "  \"createdObjects\": [" + QuoteList(result.CreatedObjects) + "]\n" +
                      "}";
        File.WriteAllText(resultPath, json);
    }

    private static string QuoteList(List<string> values)
    {
        List<string> quoted = new List<string>();
        foreach (string value in values) quoted.Add("\"" + EscapeJson(value) + "\"");
        return string.Join(",", quoted.ToArray());
    }

    private static string EscapeJson(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
