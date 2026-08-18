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
    private const string ProcessedFileName = ".command_processed";
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
            { "REMOVE_COMPONENT", RemoveComponent }
        };

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.update -= CheckCommand;
        EditorApplication.update += CheckCommand;
        Application.logMessageReceived -= CaptureLog;
        Application.logMessageReceived += CaptureLog;
        Debug.Log("[Company Game] Command Agent ready.");
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

            string id = GetCommandId(raw, commandPath);
            string processedPath = Path.Combine(projectPath, ProcessedFileName);
            if (File.Exists(processedPath) && File.ReadAllText(processedPath).Trim() == id)
            {
                SafeDelete(commandPath);
                return;
            }

            commandRunning = true;
            commandErrors.Clear();
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

            File.WriteAllText(processedPath, id);
            SafeDelete(commandPath);
            AssetDatabase.Refresh();

            if (result.Success) Debug.Log("[Company Game] SUCCESS: " + result.Message);
            else Debug.LogError("[Company Game] FAILED: " + result.Message);
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
        catch (Exception ex) { Debug.LogWarning("[Company Game] Could not delete command file: " + ex.Message); }
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
        public readonly List<string> DeletedObjects = new List<string>();
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

    private static CommandRequest ParseCommand(string raw)
    {
        string[] parts = raw.Split(new[] { ':' }, 2);
        return new CommandRequest(parts[0].Trim().ToUpperInvariant(), parts.Length > 1 ? parts[1].Trim() : "");
    }

    private static CommandResult Execute(CommandRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return CommandResult.Failure("Command name is empty.");
        Func<CommandRequest, CommandResult> handler;
        if (!Handlers.TryGetValue(request.Name, out handler)) return CommandResult.Failure("Unknown command: " + request.Name);
        return handler(request);
    }

    private static CommandResult CreateInteractableObject(CommandRequest request)
    {
        string[] v = request.Arguments.Split(':');
        string baseName = v.Length > 0 ? v[0].Trim() : "InteractableObject";
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "InteractableObject";
        int count = ParseCount(v, 1);
        if (count < 1 || count > 1000) return CommandResult.Failure("Count must be 1-1000.");
        CommandResult result = CommandResult.SuccessResult("Created " + count + " interactable object(s): " + baseName);
        int next = GetNextNumberedIndex(baseName);
        for (int i = 0; i < count; i++)
        {
            string name = baseName + " (" + (next + i) + ")";
            CreateSingleInteractable(name);
            result.CreatedObjects.Add(name);
        }
        return result;
    }

    private static CommandResult DeleteObject(CommandRequest request)
    {
        string[] v = request.Arguments.Split(':');
        string prefix = v.Length > 0 ? v[0].Trim() : "";
        int count = ParseCount(v, 1);
        if (string.IsNullOrWhiteSpace(prefix)) return CommandResult.Failure("DELETE_OBJECT requires a name.");
        if (count < 1 || count > 1000) return CommandResult.Failure("Count must be 1-1000.");
        List<GameObject> matches = FindByPrefix(prefix);
        if (matches.Count == 0) return CommandResult.Failure("No matching objects found: " + prefix);
        matches.Sort(delegate(GameObject a, GameObject b) { return CompareNames(a.name, b.name, prefix); });
        CommandResult result = CommandResult.SuccessResult("");
        int amount = Math.Min(count, matches.Count);
        for (int i = 0; i < amount; i++)
        {
            if (matches[i] == null) continue;
            string name = matches[i].name;
            Undo.DestroyObjectImmediate(matches[i]);
            result.DeletedObjects.Add(name);
        }
        if (result.DeletedObjects.Count == 0) return CommandResult.Failure("No objects could be deleted: " + prefix);
        result.Message = "Deleted " + result.DeletedObjects.Count + " object(s) matching prefix: " + prefix;
        return result;
    }

    private static List<GameObject> FindByPrefix(string prefix)
    {
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        List<GameObject> matches = new List<GameObject>();
        foreach (GameObject go in all)
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid()) continue;
            if (go.name.Equals(prefix, StringComparison.Ordinal) || go.name.StartsWith(prefix + " (", StringComparison.Ordinal)) matches.Add(go);
        }
        return matches;
    }

    // Delete numbered objects from the highest number downward.
    // Example: Employee (1) ~ Employee (15), count 7 => 15,14,13,12,11,10,9.
    private static int CompareNames(string a, string b, string prefix)
    {
        if (a.Equals(prefix, StringComparison.Ordinal)) return 1;
        if (b.Equals(prefix, StringComparison.Ordinal)) return -1;
        int ai = ExtractNumber(a, prefix), bi = ExtractNumber(b, prefix);
        if (ai >= 0 && bi >= 0) return bi.CompareTo(ai);
        if (ai >= 0) return -1;
        if (bi >= 0) return 1;
        return string.CompareOrdinal(b, a);
    }

    private static int ExtractNumber(string name, string prefix)
    {
        string start = prefix + " (";
        if (!name.StartsWith(start, StringComparison.Ordinal) || !name.EndsWith(")", StringComparison.Ordinal)) return -1;
        int value;
        return int.TryParse(name.Substring(start.Length, name.Length - start.Length - 1), out value) ? value : -1;
    }

    private static int ParseCount(string[] values, int fallback)
    {
        if (values.Length < 2 || string.IsNullOrWhiteSpace(values[1])) return fallback;
        int count;
        return int.TryParse(values[1].Trim(), out count) ? count : -1;
    }

    private static GameObject CreateSingleInteractable(string name)
    {
        GameObject go = new GameObject(name);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("Sprites/Default.sprite");
        go.AddComponent<BoxCollider2D>();
        TryAddComponentByName(go, "DraggableObject2D");
        TryAddComponentByName(go, "InteractableObject2D");
        Undo.RegisterCreatedObjectUndo(go, "Create Interactable Object");
        Selection.activeGameObject = go;
        EditorUtility.SetDirty(go);
        return go;
    }

    private static int GetNextNumberedIndex(string baseName)
    {
        int index = 1;
        while (GameObject.Find(baseName + " (" + index + ")") != null) index++;
        return index;
    }

    private static CommandResult CreateEmptyObject(CommandRequest r)
    {
        string name = string.IsNullOrWhiteSpace(r.Arguments) ? "CompanyObject" : r.Arguments.Trim();
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Company Object");
        Selection.activeGameObject = go;
        return CommandResult.SuccessResult("Created object: " + name);
    }

    private static CommandResult RenameObject(CommandRequest r)
    {
        string[] v; if (!Args(r.Arguments, 2, out v)) return CommandResult.Failure("RENAME_OBJECT requires object:newName.");
        GameObject go = FindObject(v[0]); if (go == null) return CommandResult.Failure("Object not found: " + v[0]);
        Undo.RecordObject(go, "Rename Object"); go.name = v[1]; EditorUtility.SetDirty(go);
        return CommandResult.SuccessResult("Renamed object to: " + v[1]);
    }

    private static CommandResult SetActive(CommandRequest r)
    {
        string[] v; bool active; if (!Args(r.Arguments, 2, out v) || !bool.TryParse(v[1], out active)) return CommandResult.Failure("SET_ACTIVE requires object:true|false.");
        GameObject go = FindObject(v[0]); if (go == null) return CommandResult.Failure("Object not found: " + v[0]);
        Undo.RecordObject(go, "Set Active"); go.SetActive(active); return CommandResult.SuccessResult("Set active: " + v[0]);
    }

    private static CommandResult SetPosition(CommandRequest r) { return SetVector(r, "position", delegate(Transform t, Vector3 v) { t.position = v; }); }
    private static CommandResult SetScale(CommandRequest r) { return SetVector(r, "scale", delegate(Transform t, Vector3 v) { t.localScale = v; }); }
    private static CommandResult SetRotation(CommandRequest r) { return SetVector(r, "rotation", delegate(Transform t, Vector3 v) { t.eulerAngles = v; }); }

    private static CommandResult SetVector(CommandRequest r, string label, Action<Transform, Vector3> apply)
    {
        string[] v; if (!Args(r.Arguments, 4, out v)) return CommandResult.Failure(label + " requires object:x:y:z.");
        float x, y, z; if (!TryFloat(v[1], out x) || !TryFloat(v[2], out y) || !TryFloat(v[3], out z)) return CommandResult.Failure("Invalid vector values.");
        GameObject go = FindObject(v[0]); if (go == null) return CommandResult.Failure("Object not found: " + v[0]);
        Undo.RecordObject(go.transform, "Set " + label); apply(go.transform, new Vector3(x, y, z)); EditorUtility.SetDirty(go);
        return CommandResult.SuccessResult("Set " + label + ": " + go.name);
    }

    private static CommandResult SetParent(CommandRequest r)
    {
        string[] v; if (!Args(r.Arguments, 2, out v)) return CommandResult.Failure("SET_PARENT requires child:parent or child:NONE.");
        GameObject child = FindObject(v[0]); if (child == null) return CommandResult.Failure("Object not found: " + v[0]);
        Transform parent = null;
        if (!v[1].Equals("NONE", StringComparison.OrdinalIgnoreCase)) { GameObject p = FindObject(v[1]); if (p == null) return CommandResult.Failure("Parent not found: " + v[1]); parent = p.transform; }
        Undo.SetTransformParent(child.transform, parent, "Set Parent"); return CommandResult.SuccessResult("Set parent: " + child.name);
    }

    private static CommandResult AddComponent(CommandRequest r)
    {
        string[] v; if (!Args(r.Arguments, 2, out v)) return CommandResult.Failure("ADD_COMPONENT requires object:component.");
        GameObject go = FindObject(v[0]); if (go == null) return CommandResult.Failure("Object not found: " + v[0]);
        Type type = FindComponentType(v[1]); if (!ValidComponent(type)) return CommandResult.Failure("Component type not found: " + v[1]);
        if (go.GetComponent(type) != null) return CommandResult.SuccessResult("Component already exists: " + v[1]);
        Undo.AddComponent(go, type); return CommandResult.SuccessResult("Added component: " + v[1]);
    }

    private static CommandResult RemoveComponent(CommandRequest r)
    {
        string[] v; if (!Args(r.Arguments, 2, out v)) return CommandResult.Failure("REMOVE_COMPONENT requires object:component.");
        GameObject go = FindObject(v[0]); if (go == null) return CommandResult.Failure("Object not found: " + v[0]);
        Type type = FindComponentType(v[1]); if (!ValidComponent(type)) return CommandResult.Failure("Component type not found: " + v[1]);
        Component component = go.GetComponent(type); if (component == null) return CommandResult.Failure("Component not found: " + v[1]);
        Undo.DestroyObjectImmediate(component); return CommandResult.SuccessResult("Removed component: " + v[1]);
    }

    private static GameObject FindObject(string name) { return string.IsNullOrWhiteSpace(name) ? null : GameObject.Find(name); }

    private static Type FindComponentType(string name)
    {
        string requested = name.Trim();
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type exact = assembly.GetType(requested); if (exact != null) return exact;
                foreach (Type candidate in assembly.GetTypes()) if (candidate.Name.Equals(requested, StringComparison.OrdinalIgnoreCase)) return candidate;
            }
            catch (ReflectionTypeLoadException) { }
        }
        return null;
    }

    private static bool TryAddComponentByName(GameObject go, string typeName)
    {
        Type type = FindComponentType(typeName); if (!ValidComponent(type)) return false;
        if (go.GetComponent(type) != null) return true;
        Undo.AddComponent(go, type); return true;
    }

    private static bool ValidComponent(Type type) { return type != null && typeof(Component).IsAssignableFrom(type) && !type.IsAbstract; }

    private static bool Args(string text, int count, out string[] values)
    {
        values = text.Split(':');
        if (values.Length != count) { values = null; return false; }
        for (int i = 0; i < values.Length; i++) values[i] = values[i].Trim();
        return true;
    }

    private static bool TryFloat(string value, out float result) { return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result); }

    private static void WriteResult(string projectPath, string id, string command, CommandResult result)
    {
        string json = "{\n" +
            "  \"id\": \"" + Escape(id) + "\",\n" +
            "  \"command\": \"" + Escape(command) + "\",\n" +
            "  \"success\": " + result.Success.ToString().ToLowerInvariant() + ",\n" +
            "  \"message\": \"" + Escape(result.Message) + "\",\n" +
            "  \"exception\": \"" + Escape(result.Exception) + "\",\n" +
            "  \"createdObjects\": [" + QuoteList(result.CreatedObjects) + "],\n" +
            "  \"deletedObjects\": [" + QuoteList(result.DeletedObjects) + "],\n" +
            "  \"errors\": [" + QuoteErrors(result.Errors) + "]\n" +
            "}";
        File.WriteAllText(Path.Combine(projectPath, ResultFileName), json);
    }

    private static string QuoteList(List<string> values)
    {
        List<string> result = new List<string>();
        foreach (string value in values) result.Add("\"" + Escape(value) + "\"");
        return string.Join(",", result.ToArray());
    }

    private static string QuoteErrors(List<LogRecord> values)
    {
        List<string> result = new List<string>();
        foreach (LogRecord value in values) result.Add("{\"type\":\"" + Escape(value.Type) + "\",\"message\":\"" + Escape(value.Message) + "\",\"stackTrace\":\"" + Escape(value.StackTrace) + "\"}");
        return string.Join(",", result.ToArray());
    }

    private static string Escape(string value)
    {
        return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
