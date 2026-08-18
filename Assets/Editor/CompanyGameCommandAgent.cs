using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CompanyGameCommandAgent
{
    private const string CommandFileName = "command.json";
    private const string ResultFileName = "result.json";
    private const string ResultsDirectoryName = "results";
    private const string ErrorFileName = "error.json";
    private const string NumberRegistryKeyPrefix = "CompanyGame.CommandAgent.NextNumber.";
    private static bool commandRunning;
    private static readonly List<LogRecord> commandErrors = new List<LogRecord>();

    private static readonly Dictionary<string, Func<CommandRequest, CommandResult>> Handlers =
        new Dictionary<string, Func<CommandRequest, CommandResult>>(StringComparer.OrdinalIgnoreCase)
    {
        { "CREATE_INTERACTABLE_OBJECT", CreateInteractableObject },
        { "CREATE_EMPTY_OBJECT", CreateEmptyObject },
        { "CREATE_OBJECT", CreateEmptyObject },
        { "DELETE_OBJECT", DeleteObject },
        { "RENAME_OBJECT", RenameObject },
        { "SET_ACTIVE", SetActive },
        { "SET_POSITION", SetPosition },
        { "SET_SCALE", SetScale },
        { "SET_ROTATION", SetRotation },
        { "SET_PARENT", SetParent },
        { "ADD_COMPONENT", AddComponent },
        { "REMOVE_COMPONENT", RemoveComponent },
        { "SET_COMPONENT_ACTIVE", SetComponentActive },
        { "SET_COMPONENT_PROPERTY", SetComponentProperty }
    };

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.update -= CheckCommand;
        EditorApplication.update += CheckCommand;
        Application.logMessageReceived -= CaptureLog;
        Application.logMessageReceived += CaptureLog;
    }

    private static void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (!commandRunning) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            commandErrors.Add(new LogRecord(type.ToString(), condition, stackTrace));
    }

    private static void CheckCommand()
    {
        if (commandRunning) return;
        string projectPath = Directory.GetParent(Application.dataPath).FullName;
        string commandPath = Path.Combine(projectPath, CommandFileName);
        if (!File.Exists(commandPath)) return;

        try
        {
            string raw = File.ReadAllText(commandPath).Trim();
            if (string.IsNullOrWhiteSpace(raw)) return;

            commandRunning = true;
            commandErrors.Clear();
            string id = GetCommandId(raw, commandPath);
            CommandResult result;

            try { result = Execute(ParseCommand(raw)); }
            catch (Exception ex)
            {
                result = CommandResult.Failure("Command execution exception: " + ex.Message);
                result.Exception = ex.ToString();
            }

            result.Errors.AddRange(commandErrors);
            if (result.Errors.Count > 0)
            {
                result.Success = false;
                result.Message = "Unity reported errors while executing the command.";
            }

            WriteResult(projectPath, id, raw, result);
            SafeDelete(commandPath);
            AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            Debug.LogError("[Company Game] Agent error: " + ex);
        }
        finally
        {
            commandRunning = false;
            commandErrors.Clear();
        }
    }

    private static string GetCommandId(string raw, string path)
    {
        long ticks = File.GetLastWriteTimeUtc(path).Ticks;
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(raw + "|" + ticks.ToString(CultureInfo.InvariantCulture));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in sha.ComputeHash(bytes)) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    private static void SafeDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private sealed class CommandRequest
    {
        public string Name;
        public string Arguments;
        public CommandRequest(string name, string arguments) { Name = name; Arguments = arguments; }
    }

    private sealed class CommandResult
    {
        public bool Success;
        public string Message;
        public string Exception;
        public readonly List<string> CreatedObjects = new List<string>();
        public readonly List<string> CreatedObjectIds = new List<string>();
        public readonly List<string> DeletedObjects = new List<string>();
        public readonly List<string> DeletedObjectIds = new List<string>();
        public readonly List<string> RenamedObjects = new List<string>();
        public readonly List<LogRecord> Errors = new List<LogRecord>();
        private CommandResult(bool success, string message) { Success = success; Message = message; }
        public static CommandResult SuccessResult(string message) { return new CommandResult(true, message); }
        public static CommandResult Failure(string message) { return new CommandResult(false, message); }
    }

    private sealed class LogRecord
    {
        public string Type;
        public string Message;
        public string StackTrace;
        public LogRecord(string type, string message, string stackTrace) { Type = type; Message = message; StackTrace = stackTrace; }
    }

    private sealed class RenamePair
    {
        public string OldName;
        public string NewName;
        public RenamePair(string oldName, string newName) { OldName = oldName; NewName = newName; }
    }

    private static CommandRequest ParseCommand(string raw)
    {
        string[] parts = raw.Split(new[] { ':' }, 2);
        return new CommandRequest(parts[0].Trim().ToUpperInvariant(), parts.Length > 1 ? parts[1].Trim() : "");
    }

    private static CommandResult Execute(CommandRequest request)
    {
        Func<CommandRequest, CommandResult> handler;
        return Handlers.TryGetValue(request.Name, out handler)
            ? handler(request)
            : CommandResult.Failure("Unknown command: " + request.Name);
    }

    private static CommandResult CreateInteractableObject(CommandRequest request)
    {
        string[] parts = request.Arguments.Split(':');
        string baseName = parts.Length > 0 ? parts[0].Trim() : "InteractableObject";
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "InteractableObject";
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
            return CommandResult.Failure("CREATE_INTERACTABLE_OBJECT requires a count or explicit numbers.");

        string spec = parts[1].Trim();
        List<int> explicitNumbers;
        if (TryParseExplicitNumbers(spec, out explicitNumbers))
        {
            HashSet<int> reserved = new HashSet<int>();
            foreach (int n in explicitNumbers)
            {
                if (n < 1 || !reserved.Add(n)) return CommandResult.Failure("Invalid or duplicate object number: " + n);
                if (FindSceneObjectByExactName(baseName + " (" + n + ")") != null)
                    return CommandResult.Failure("Object already exists: " + baseName + " (" + n + ")");
            }

            CommandResult result = CommandResult.SuccessResult("Created " + explicitNumbers.Count + " interactable object(s): " + baseName);
            foreach (int n in explicitNumbers)
            {
                string name = baseName + " (" + n + ")";
                GameObject go = CreateSingleInteractable(name, CompanyGameIdentityClassifier.GetCategory(baseName));
                result.CreatedObjects.Add(name);
                AddIdentityResult(result, go);
            }

            int highest = 0;
            foreach (int n in explicitNumbers) if (n > highest) highest = n;
            int next = GetNextNumberedIndex(baseName);
            if (next <= highest) SetNextNumber(baseName, highest + 1);
            return result;
        }

        int count;
        if (!int.TryParse(spec, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1 || count > 1000)
            return CommandResult.Failure("Count must be 1-1000, or explicit numbers such as 7,9.");

        int nextNumber = GetNextNumberedIndex(baseName);
        CommandResult created = CommandResult.SuccessResult("Created " + count + " interactable object(s): " + baseName);
        for (int i = 0; i < count; i++)
        {
            string name = baseName + " (" + (nextNumber + i) + ")";
            GameObject go = CreateSingleInteractable(name, CompanyGameIdentityClassifier.GetCategory(baseName));
            created.CreatedObjects.Add(name);
            AddIdentityResult(created, go);
        }
        SetNextNumber(baseName, nextNumber + count);
        return created;
    }

    private static void AddIdentityResult(CommandResult result, GameObject go)
    {
        if (go == null) return;
        CompanyGameObjectIdentity identity = go.GetComponent<CompanyGameObjectIdentity>();
        if (identity != null) result.CreatedObjectIds.Add(identity.ObjectId);
    }

    private static bool TryParseExplicitNumbers(string text, out List<int> numbers)
    {
        numbers = new List<int>();
        if (text.IndexOf(',') < 0) return false;
        foreach (string part in text.Split(','))
        {
            int n;
            if (!int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return false;
            numbers.Add(n);
        }
        return numbers.Count > 0;
    }

    private static int GetNextNumberedIndex(string baseName)
    {
        string key = NumberRegistryKeyPrefix + baseName;
        int stored = Math.Max(1, EditorPrefs.GetInt(key, 1));
        int next = stored;
        while (FindSceneObjectByExactName(baseName + " (" + next + ")") != null) next++;
        if (next != stored) EditorPrefs.SetInt(key, next);
        return next;
    }

    private static void SetNextNumber(string baseName, int next)
    {
        EditorPrefs.SetInt(NumberRegistryKeyPrefix + baseName, Math.Max(1, next));
    }

    private static GameObject CreateSingleInteractable(string name, string category)
    {
        GameObject go = new GameObject(name);

        // Creation is deliberately independent of graphics assets.
        // Sprites are assigned separately, so missing Unity built-in sprites can never fail creation.
        Undo.AddComponent<BoxCollider2D>(go);
        TryAddComponentByName(go, "DraggableObject2D");
        TryAddComponentByName(go, "InteractableObject2D");

        CompanyGameObjectIdentity identity = Undo.AddComponent<CompanyGameObjectIdentity>(go);
        identity.EnsureIdentity(category);

        Undo.RegisterCreatedObjectUndo(go, "Create Interactable Object");
        Selection.activeGameObject = go;
        EditorUtility.SetDirty(go);
        return go;
    }

    private static CommandResult CreateEmptyObject(CommandRequest request)
    {
        string name = string.IsNullOrWhiteSpace(request.Arguments) ? "CompanyObject" : request.Arguments.Trim();
        GameObject go = new GameObject(name);
        CompanyGameObjectIdentity identity = Undo.AddComponent<CompanyGameObjectIdentity>(go);
        identity.EnsureIdentity(CompanyGameIdentityClassifier.GetCategory(name));
        Undo.RegisterCreatedObjectUndo(go, "Create Company Object");

        CommandResult result = CommandResult.SuccessResult("Created object: " + name);
        result.CreatedObjects.Add(name);
        AddIdentityResult(result, go);
        return result;
    }

    private static CommandResult DeleteObject(CommandRequest request)
    {
        string[] values = request.Arguments.Split(':');
        string selector = values[0].Trim();
        int count = values.Length > 1 ? ParseCount(values[1]) : 1;
        if (count < 1) return CommandResult.Failure("Count must be positive.");

        if (IsIdSelector(selector))
        {
            GameObject byId = FindObject(selector);
            if (byId == null) return CommandResult.Failure("Object ID not found: " + selector);
            CommandResult one = CommandResult.SuccessResult("Deleted object: " + byId.name);
            AddDeletedResult(one, byId);
            Undo.DestroyObjectImmediate(byId);
            return one;
        }

        List<GameObject> matches = FindByPrefix(selector);
        if (matches.Count == 0) return CommandResult.Failure("No matching objects found: " + selector);
        matches.Sort((a, b) => CompareNamesDescending(a.name, b.name, selector));

        int amount = Math.Min(count, matches.Count);
        CommandResult result = CommandResult.SuccessResult("Deleted " + amount + " object(s) matching: " + selector);
        for (int i = 0; i < amount; i++)
        {
            GameObject go = matches[i];
            AddDeletedResult(result, go);
            Undo.DestroyObjectImmediate(go);
        }
        return result;
    }

    private static void AddDeletedResult(CommandResult result, GameObject go)
    {
        if (go == null) return;
        result.DeletedObjects.Add(go.name);
        CompanyGameObjectIdentity identity = go.GetComponent<CompanyGameObjectIdentity>();
        if (identity != null) result.DeletedObjectIds.Add(identity.ObjectId);
    }

    private static int ParseCount(string value)
    {
        int n;
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : -1;
    }

    private static List<GameObject> FindByPrefix(string prefix)
    {
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        List<GameObject> result = new List<GameObject>();
        foreach (GameObject go in all)
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid()) continue;
            if (go.name.Equals(prefix, StringComparison.Ordinal) || go.name.StartsWith(prefix + " (", StringComparison.Ordinal))
                result.Add(go);
        }
        return result;
    }

    private static int CompareNamesDescending(string a, string b, string prefix)
    {
        int ai = ExtractNumber(a, prefix);
        int bi = ExtractNumber(b, prefix);
        if (ai >= 0 && bi >= 0) return bi.CompareTo(ai);
        return string.CompareOrdinal(b, a);
    }

    private static int ExtractNumber(string name, string prefix)
    {
        string start = prefix + " (";
        if (!name.StartsWith(start, StringComparison.Ordinal) || !name.EndsWith(")", StringComparison.Ordinal)) return -1;
        int number;
        return int.TryParse(name.Substring(start.Length, name.Length - start.Length - 1), out number) ? number : -1;
    }

    private static CommandResult RenameObject(CommandRequest request)
    {
        List<RenamePair> pairs;
        string error;
        if (!TryParseRenamePairs(request.Arguments, out pairs, out error)) return CommandResult.Failure(error);

        Dictionary<GameObject, string> plan = new Dictionary<GameObject, string>();
        Dictionary<GameObject, string> oldNames = new Dictionary<GameObject, string>();
        foreach (RenamePair pair in pairs)
        {
            GameObject go = FindObject(pair.OldName);
            if (go == null) return CommandResult.Failure("Rename validation failed. Object not found or ambiguous: " + pair.OldName);
            if (plan.ContainsKey(go)) return CommandResult.Failure("Duplicate rename target: " + pair.OldName);
            plan.Add(go, pair.NewName);
            oldNames.Add(go, go.name);
        }

        // Duplicate final display names are allowed. Identity IDs remain unique.
        CommandResult result = CommandResult.SuccessResult("Renamed " + plan.Count + " object(s).");
        try
        {
            foreach (KeyValuePair<GameObject, string> item in plan)
            {
                Undo.RecordObject(item.Key, "Batch Rename Objects");
                item.Key.name = item.Value;
                EditorUtility.SetDirty(item.Key);
                result.RenamedObjects.Add(oldNames[item.Key] + " -> " + item.Value);
            }
        }
        catch (Exception ex)
        {
            foreach (KeyValuePair<GameObject, string> item in oldNames)
                if (item.Key != null) item.Key.name = item.Value;
            return CommandResult.Failure("Batch rename rolled back: " + ex.Message);
        }
        return result;
    }

    private static bool TryParseRenamePairs(string text, out List<RenamePair> pairs, out string error)
    {
        pairs = new List<RenamePair>();
        error = null;
        foreach (string entry in text.Split(','))
        {
            string[] pair = entry.Split(new[] { '=' }, 2);
            if (pair.Length != 2 || string.IsNullOrWhiteSpace(pair[0]) || string.IsNullOrWhiteSpace(pair[1]))
            {
                error = "RENAME_OBJECT format: oldName=newName[,oldName=newName...].";
                return false;
            }
            pairs.Add(new RenamePair(pair[0].Trim(), pair[1].Trim()));
        }
        return pairs.Count > 0;
    }

    private static GameObject FindObject(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;
        string value = selector.Trim();
        string id = value.StartsWith("ID:", StringComparison.OrdinalIgnoreCase) ? value.Substring(3).Trim() : value;

        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        List<GameObject> idMatches = new List<GameObject>();
        foreach (GameObject go in all)
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid()) continue;
            CompanyGameObjectIdentity identity = go.GetComponent<CompanyGameObjectIdentity>();
            if (identity != null && string.Equals(identity.ObjectId, id, StringComparison.OrdinalIgnoreCase)) idMatches.Add(go);
        }
        if (idMatches.Count == 1) return idMatches[0];
        if (IsIdSelector(value)) return null;

        List<GameObject> nameMatches = new List<GameObject>();
        foreach (GameObject go in all)
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid()) continue;
            if (go.name.Equals(value, StringComparison.Ordinal)) nameMatches.Add(go);
        }
        if (nameMatches.Count == 1) return nameMatches[0];
        if (nameMatches.Count > 1)
        {
            Debug.LogError("[Company Game] Ambiguous object name. Use its ID: " + value);
            return null;
        }
        return null;
    }

    private static GameObject FindSceneObjectByExactName(string name)
    {
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in all)
            if (go != null && !EditorUtility.IsPersistent(go) && go.scene.IsValid() && go.name.Equals(name, StringComparison.Ordinal)) return go;
        return null;
    }

    private static bool IsIdSelector(string selector)
    {
        if (selector.StartsWith("ID:", StringComparison.OrdinalIgnoreCase)) return true;
        string upper = selector.ToUpperInvariant();
        return upper.StartsWith("EMP-") || upper.StartsWith("ROOM-") || upper.StartsWith("DEPT-") || upper.StartsWith("MACH-") || upper.StartsWith("OBJ-");
    }

    private static CommandResult SetActive(CommandRequest request)
    {
        string[] values;
        bool active;
        if (!Args(request.Arguments, 2, out values) || !bool.TryParse(values[1], out active))
            return CommandResult.Failure("SET_ACTIVE requires object:true|false.");
        GameObject go = FindObject(values[0]);
        if (go == null) return CommandResult.Failure("Object not found or ambiguous: " + values[0]);
        Undo.RecordObject(go, "Set Active");
        go.SetActive(active);
        return CommandResult.SuccessResult("Set active: " + go.name);
    }

    private static CommandResult SetPosition(CommandRequest request) { return SetVector(request, "position", (t, v) => t.position = v); }
    private static CommandResult SetScale(CommandRequest request) { return SetVector(request, "scale", (t, v) => t.localScale = v); }
    private static CommandResult SetRotation(CommandRequest request) { return SetVector(request, "rotation", (t, v) => t.eulerAngles = v); }

    private static CommandResult SetVector(CommandRequest request, string label, Action<Transform, Vector3> apply)
    {
        string[] values;
        if (!Args(request.Arguments, 4, out values)) return CommandResult.Failure(label + " requires object:x:y:z.");
        float x, y, z;
        if (!TryFloat(values[1], out x) || !TryFloat(values[2], out y) || !TryFloat(values[3], out z)) return CommandResult.Failure("Invalid vector values.");
        GameObject go = FindObject(values[0]);
        if (go == null) return CommandResult.Failure("Object not found or ambiguous: " + values[0]);
        Undo.RecordObject(go.transform, "Set " + label);
        apply(go.transform, new Vector3(x, y, z));
        EditorUtility.SetDirty(go);
        return CommandResult.SuccessResult("Set " + label + ": " + go.name);
    }

    private static CommandResult SetParent(CommandRequest request)
    {
        string[] values;
        if (!Args(request.Arguments, 2, out values)) return CommandResult.Failure("SET_PARENT requires child:parent.");
        GameObject child = FindObject(values[0]);
        if (child == null) return CommandResult.Failure("Child not found or ambiguous: " + values[0]);
        Transform parent = null;
        if (!values[1].Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            GameObject parentObject = FindObject(values[1]);
            if (parentObject == null) return CommandResult.Failure("Parent not found or ambiguous: " + values[1]);
            parent = parentObject.transform;
        }
        Undo.SetTransformParent(child.transform, parent, "Set Parent");
        return CommandResult.SuccessResult("Set parent: " + child.name);
    }

    private static Type FindComponentType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type exact = assembly.GetType(name);
                if (exact != null) return exact;
                foreach (Type type in assembly.GetTypes())
                    if (type.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return type;
            }
            catch (ReflectionTypeLoadException) { }
        }
        return null;
    }

    private static bool ValidComponent(Type type)
    {
        return type != null && typeof(Component).IsAssignableFrom(type) && !type.IsAbstract;
    }

    private static CommandResult AddComponent(CommandRequest request)
    {
        string[] values;
        if (!Args(request.Arguments, 2, out values)) return CommandResult.Failure("ADD_COMPONENT requires object:component.");
        GameObject go = FindObject(values[0]);
        Type type = FindComponentType(values[1]);
        if (go == null) return CommandResult.Failure("Object not found or ambiguous: " + values[0]);
        if (!ValidComponent(type)) return CommandResult.Failure("Component type not found: " + values[1]);
        if (go.GetComponent(type) != null) return CommandResult.SuccessResult("Component already exists: " + values[1]);
        Undo.AddComponent(go, type);
        return CommandResult.SuccessResult("Added component: " + values[1]);
    }

    private static CommandResult RemoveComponent(CommandRequest request)
    {
        string[] values;
        if (!Args(request.Arguments, 2, out values)) return CommandResult.Failure("REMOVE_COMPONENT requires object:component.");
        GameObject go = FindObject(values[0]);
        Type type = FindComponentType(values[1]);
        if (go == null) return CommandResult.Failure("Object not found or ambiguous: " + values[0]);
        if (!ValidComponent(type)) return CommandResult.Failure("Component type not found: " + values[1]);
        Component component = go.GetComponent(type);
        if (component == null) return CommandResult.Failure("Component not found: " + values[1]);
        Undo.DestroyObjectImmediate(component);
        return CommandResult.SuccessResult("Removed component: " + values[1]);
    }

    private static CommandResult SetComponentActive(CommandRequest request)
    {
        string[] values;
        if (!Args(request.Arguments, 3, out values)) return CommandResult.Failure("SET_COMPONENT_ACTIVE requires object:component:true|false.");
        bool active;
        if (!bool.TryParse(values[2], out active)) return CommandResult.Failure("Invalid active value.");
        GameObject go = FindObject(values[0]);
        Type type = FindComponentType(values[1]);
        if (go == null) return CommandResult.Failure("Object not found or ambiguous: " + values[0]);
        if (!ValidComponent(type)) return CommandResult.Failure("Component type not found: " + values[1]);
        Component component = go.GetComponent(type);
        if (component == null) return CommandResult.Failure("Component not found: " + values[1]);

        Behaviour behaviour = component as Behaviour;
        if (behaviour != null)
        {
            Undo.RecordObject(behaviour, "Set Component Active");
            behaviour.enabled = active;
            EditorUtility.SetDirty(behaviour);
            return CommandResult.SuccessResult("Set component active: " + type.Name);
        }
        Collider2D collider2D = component as Collider2D;
        if (collider2D != null)
        {
            Undo.RecordObject(collider2D, "Set Component Active");
            collider2D.enabled = active;
            EditorUtility.SetDirty(collider2D);
            return CommandResult.SuccessResult("Set component active: " + type.Name);
        }
        return CommandResult.Failure("Component has no enabled state: " + type.Name);
    }

    private static CommandResult SetComponentProperty(CommandRequest request)
    {
        string[] values;
        if (!Args(request.Arguments, 4, out values))
            return CommandResult.Failure("SET_COMPONENT_PROPERTY requires object:component:property:value.");

        GameObject go = FindObject(values[0]);
        Type type = FindComponentType(values[1]);
        if (go == null) return CommandResult.Failure("Object not found or ambiguous: " + values[0]);
        if (!ValidComponent(type)) return CommandResult.Failure("Component type not found: " + values[1]);
        Component component = go.GetComponent(type);
        if (component == null) return CommandResult.Failure("Component not found: " + values[1]);

        SerializedObject serialized = new SerializedObject(component);
        SerializedProperty property = serialized.FindProperty(values[2]);
        if (property == null) return CommandResult.Failure("Component property not found: " + values[2]);

        string error;
        if (!TrySetSerializedProperty(property, values[3], out error)) return CommandResult.Failure(error);
        Undo.RecordObject(component, "Set Component Property");
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(component);
        return CommandResult.SuccessResult("Set " + values[1] + "." + values[2] + " on " + go.name);
    }

    private static bool TrySetSerializedProperty(SerializedProperty property, string value, out string error)
    {
        error = null;
        try
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    bool boolValue;
                    if (!bool.TryParse(value, out boolValue)) { error = "Invalid boolean value: " + value; return false; }
                    property.boolValue = boolValue;
                    return true;
                case SerializedPropertyType.Integer:
                    long longValue;
                    if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out longValue)) { error = "Invalid integer value: " + value; return false; }
                    property.longValue = longValue;
                    return true;
                case SerializedPropertyType.Float:
                    float floatValue;
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue)) { error = "Invalid float value: " + value; return false; }
                    property.floatValue = floatValue;
                    return true;
                case SerializedPropertyType.String:
                    property.stringValue = value;
                    return true;
                case SerializedPropertyType.Enum:
                    int enumIndex;
                    if (int.TryParse(value, out enumIndex)) property.enumValueIndex = enumIndex;
                    else
                    {
                        string[] names = property.enumDisplayNames;
                        enumIndex = Array.FindIndex(names, n => n.Equals(value, StringComparison.OrdinalIgnoreCase));
                        if (enumIndex < 0) { error = "Enum value not found: " + value; return false; }
                        property.enumValueIndex = enumIndex;
                    }
                    return true;
                case SerializedPropertyType.Vector2:
                    Vector2 vector2;
                    if (!TryParseVector2(value, out vector2)) { error = "Vector2 requires x,y."; return false; }
                    property.vector2Value = vector2;
                    return true;
                case SerializedPropertyType.Vector3:
                    Vector3 vector3;
                    if (!TryParseVector3(value, out vector3)) { error = "Vector3 requires x,y,z."; return false; }
                    property.vector3Value = vector3;
                    return true;
                case SerializedPropertyType.Color:
                    Color color;
                    if (!ColorUtility.TryParseHtmlString(value, out color)) { error = "Color requires HTML color such as #FF0000."; return false; }
                    property.colorValue = color;
                    return true;
                case SerializedPropertyType.ObjectReference:
                    error = "ObjectReference properties require an asset/object-specific command.";
                    return false;
                default:
                    error = "Unsupported property type: " + property.propertyType;
                    return false;
            }
        }
        catch (Exception ex)
        {
            error = "Failed to set property: " + ex.Message;
            return false;
        }
    }

    private static bool TryParseVector2(string text, out Vector2 result)
    {
        result = Vector2.zero;
        string[] p = text.Split(',');
        float x, y;
        if (p.Length != 2 || !TryFloat(p[0], out x) || !TryFloat(p[1], out y)) return false;
        result = new Vector2(x, y);
        return true;
    }

    private static bool TryParseVector3(string text, out Vector3 result)
    {
        result = Vector3.zero;
        string[] p = text.Split(',');
        float x, y, z;
        if (p.Length != 3 || !TryFloat(p[0], out x) || !TryFloat(p[1], out y) || !TryFloat(p[2], out z)) return false;
        result = new Vector3(x, y, z);
        return true;
    }

    private static bool Args(string text, int count, out string[] values)
    {
        values = text.Split(new[] { ':' }, count);
        if (values.Length != count) { values = null; return false; }
        for (int i = 0; i < values.Length; i++) values[i] = values[i].Trim();
        return true;
    }

    private static bool TryFloat(string value, out float result)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static void TryAddComponentByName(GameObject go, string name)
    {
        Type type = FindComponentType(name);
        if (ValidComponent(type) && go.GetComponent(type) == null) Undo.AddComponent(go, type);
    }

    private static void WriteResult(string projectPath, string id, string command, CommandResult result)
    {
        string json = "{\n" +
            "  \"id\":\"" + Escape(id) + "\",\n" +
            "  \"command\":\"" + Escape(command) + "\",\n" +
            "  \"success\":" + result.Success.ToString().ToLowerInvariant() + ",\n" +
            "  \"message\":\"" + Escape(result.Message) + "\",\n" +
            "  \"exception\":\"" + Escape(result.Exception) + "\",\n" +
            "  \"createdObjects\":[" + Quote(result.CreatedObjects) + "],\n" +
            "  \"createdObjectIds\":[" + Quote(result.CreatedObjectIds) + "],\n" +
            "  \"deletedObjects\":[" + Quote(result.DeletedObjects) + "],\n" +
            "  \"deletedObjectIds\":[" + Quote(result.DeletedObjectIds) + "],\n" +
            "  \"renamedObjects\":[" + Quote(result.RenamedObjects) + "],\n" +
            "  \"errors\":[" + QuoteErrors(result.Errors) + "]\n" +
            "}";

        File.WriteAllText(Path.Combine(projectPath, ResultFileName), json);
        string directory = Path.Combine(projectPath, ResultsDirectoryName);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".json"), json);
        File.WriteAllText(Path.Combine(directory, ErrorFileName), BuildErrorJson(id, command, result));
    }

    private static string BuildErrorJson(string id, string command, CommandResult result)
    {
        return "{\n" +
            "  \"id\":\"" + Escape(id) + "\",\n" +
            "  \"command\":\"" + Escape(command) + "\",\n" +
            "  \"success\":" + result.Success.ToString().ToLowerInvariant() + ",\n" +
            "  \"message\":\"" + Escape(result.Message) + "\",\n" +
            "  \"exception\":\"" + Escape(result.Exception) + "\",\n" +
            "  \"errors\":[" + QuoteErrors(result.Errors) + "]\n" +
            "}";
    }

    private static string Quote(List<string> values)
    {
        List<string> quoted = new List<string>();
        foreach (string value in values) quoted.Add("\"" + Escape(value) + "\"");
        return string.Join(",", quoted.ToArray());
    }

    private static string QuoteErrors(List<LogRecord> values)
    {
        List<string> quoted = new List<string>();
        foreach (LogRecord error in values)
            quoted.Add("{\"type\":\"" + Escape(error.Type) + "\",\"message\":\"" + Escape(error.Message) + "\",\"stackTrace\":\"" + Escape(error.StackTrace) + "\"}");
        return string.Join(",", quoted.ToArray());
    }

    private static string Escape(string value)
    {
        return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
