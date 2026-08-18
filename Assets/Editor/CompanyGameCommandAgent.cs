using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CompanyGameCommandAgent
{
    private const string NumberRegistryKeyPrefix = "CompanyGame.NextNumber.";
    private static readonly Dictionary<string, Func<CommandRequest, CommandResult>> Handlers = new Dictionary<string, Func<CommandRequest, CommandResult>>(StringComparer.OrdinalIgnoreCase)
    {
        { "CREATE_INTERACTABLE_OBJECT", CreateInteractableObject },
        { "CREATE_EMPTY_OBJECT", CreateEmptyObject },
        { "DELETE_OBJECT", DeleteObject },
        { "RENAME_OBJECT", RenameObject }
    };
    private static string CommandFilePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "command.json"));

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.update -= CheckCommand;
        EditorApplication.update += CheckCommand;
    }

    private static void CheckCommand()
    {
        string path = CommandFilePath;
        if (!File.Exists(path)) return;
        try
        {
            string raw = File.ReadAllText(path, Encoding.UTF8).Trim();
            if (string.IsNullOrWhiteSpace(raw)) return;
            ExecuteAndReport(raw, path);
        }
        catch (Exception ex) { Debug.LogError("[Company Game] Agent error: " + ex); }
    }

    private static void ExecuteAndReport(string raw, string path)
    {
        string id = GetCommandId(raw, path);
        CommandResult result;
        try { result = Execute(ParseCommand(raw)); }
        catch (Exception ex)
        {
            result = CommandResult.Failure("Command execution exception: " + ex.Message);
            result.Exception = ex.ToString();
        }
        WriteResult(path, id, raw, result);
        SafeDelete(path);
        AssetDatabase.Refresh();
    }

    private static string GetCommandId(string raw, string path)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(raw + "|" + File.GetLastWriteTimeUtc(path).Ticks.ToString(CultureInfo.InvariantCulture));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in sha.ComputeHash(bytes)) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    private static void SafeDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

    private sealed class CommandRequest
    {
        public string Name; public string Arguments;
        public CommandRequest(string name, string arguments) { Name = name; Arguments = arguments; }
    }

    private sealed class CommandResult
    {
        public bool Success; public string Message; public string Exception;
        public readonly List<string> CreatedObjects = new List<string>();
        public readonly List<string> CreatedObjectIds = new List<string>();
        public readonly List<string> DeletedObjects = new List<string>();
        public readonly List<string> DeletedObjectIds = new List<string>();
        public readonly List<string> RenamedObjects = new List<string>();
        public readonly List<LogRecord> Errors = new List<LogRecord>();
        private CommandResult(bool success, string message) { Success = success; Message = message; }
        public static CommandResult SuccessResult(string message) => new CommandResult(true, message);
        public static CommandResult Failure(string message) => new CommandResult(false, message);
    }

    private sealed class LogRecord
    {
        public string Type; public string Message; public string StackTrace;
        public LogRecord(string type, string message, string stackTrace) { Type = type; Message = message; StackTrace = stackTrace; }
    }

    private static CommandRequest ParseCommand(string raw)
    {
        string[] parts = raw.Split(new[] { ':' }, 2);
        return new CommandRequest(parts[0].Trim().ToUpperInvariant(), parts.Length > 1 ? parts[1].Trim() : "");
    }

    private static CommandResult Execute(CommandRequest request)
    {
        Func<CommandRequest, CommandResult> handler;
        return Handlers.TryGetValue(request.Name, out handler) ? handler(request) : CommandResult.Failure("Unknown command: " + request.Name);
    }

    private static CommandResult CreateInteractableObject(CommandRequest request)
    {
        string[] parts = request.Arguments.Split(':');
        string baseName = parts.Length > 0 ? parts[0].Trim() : "InteractableObject";
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "InteractableObject";
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1])) return CommandResult.Failure("CREATE_INTERACTABLE_OBJECT requires a count or explicit numbers.");
        string category = ResolveCreationCategory(baseName);
        string spec = parts[1].Trim();
        List<int> explicitNumbers;
        if (TryParseExplicitNumbers(spec, out explicitNumbers))
        {
            HashSet<int> reserved = new HashSet<int>();
            foreach (int n in explicitNumbers)
            {
                if (n < 1 || !reserved.Add(n)) return CommandResult.Failure("Invalid or duplicate object number: " + n);
                if (FindSceneObjectByExactName(baseName + " (" + n + ")") != null) return CommandResult.Failure("Object already exists: " + baseName + " (" + n + ")");
            }
            CommandResult result = CommandResult.SuccessResult("Created " + explicitNumbers.Count + " interactable object(s): " + baseName);
            foreach (int n in explicitNumbers)
            {
                string name = baseName + " (" + n + ")";
                GameObject go = CreateSingleInteractable(name, category);
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
        if (!int.TryParse(spec, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1 || count > 1000) return CommandResult.Failure("Count must be 1-1000, or explicit numbers such as 7,9.");
        int nextNumber = GetNextNumberedIndex(baseName);
        CommandResult created = CommandResult.SuccessResult("Created " + count + " interactable object(s): " + baseName);
        for (int i = 0; i < count; i++)
        {
            string name = baseName + " (" + (nextNumber + i) + ")";
            GameObject go = CreateSingleInteractable(name, category);
            created.CreatedObjects.Add(name);
            AddIdentityResult(created, go);
        }
        SetNextNumber(baseName, nextNumber + count);
        return created;
    }

    private static string ResolveCreationCategory(string baseName)
    {
        if (baseName.StartsWith("직원", StringComparison.OrdinalIgnoreCase) || baseName.StartsWith("Employee", StringComparison.OrdinalIgnoreCase)) return "Employee";
        if (baseName.StartsWith("방", StringComparison.OrdinalIgnoreCase) || baseName.StartsWith("Room", StringComparison.OrdinalIgnoreCase)) return "Room";
        if (baseName.StartsWith("부서", StringComparison.OrdinalIgnoreCase) || baseName.StartsWith("Department", StringComparison.OrdinalIgnoreCase)) return "Department";
        if (baseName.StartsWith("기계", StringComparison.OrdinalIgnoreCase) || baseName.StartsWith("Machine", StringComparison.OrdinalIgnoreCase)) return "Machine";
        return "Object";
    }

    private static void AddIdentityResult(CommandResult result, GameObject go)
    {
        if (go == null) return;
        CompanyGameObjectIdentity identity = go.GetComponent<CompanyGameObjectIdentity>();
        if (identity != null && !string.IsNullOrEmpty(identity.ObjectId)) result.CreatedObjectIds.Add(identity.ObjectId);
    }

    private static bool TryParseExplicitNumbers(string text, out List<int> numbers)
    {
        numbers = new List<int>();
        if (text.IndexOf(',') < 0) return false;
        foreach (string part in text.Split(','))
        {
            int n;
            if (!int.TryParse(part.Trim(), out n)) return false;
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

    private static void SetNextNumber(string baseName, int next) { EditorPrefs.SetInt(NumberRegistryKeyPrefix + baseName, Math.Max(1, next)); }

    private static GameObject CreateSingleInteractable(string name, string category)
    {
        GameObject go = new GameObject(name);
        Undo.AddComponent<BoxCollider2D>(go);
        TryAddComponentByName(go, "DraggableObject2D");
        TryAddComponentByName(go, "InteractableObject2D");
        CompanyGameObjectIdentity identity = Undo.AddComponent<CompanyGameObjectIdentity>(go);
        identity.EnsureIdentity(category);
        Undo.RegisterCreatedObjectUndo(go, "Create Interactable Object");
        EditorUtility.SetDirty(go);
        return go;
    }

    private static CommandResult CreateEmptyObject(CommandRequest request)
    {
        string name = string.IsNullOrWhiteSpace(request.Arguments) ? "CompanyObject" : request.Arguments.Trim();
        GameObject go = new GameObject(name);
        CompanyGameObjectIdentity identity = Undo.AddComponent<CompanyGameObjectIdentity>(go);
        identity.EnsureIdentity(ResolveCreationCategory(name));
        Undo.RegisterCreatedObjectUndo(go, "Create Company Object");
        CommandResult result = CommandResult.SuccessResult("Created object: " + name);
        result.CreatedObjects.Add(name); AddIdentityResult(result, go); return result;
    }

    private static CommandResult DeleteObject(CommandRequest request)
    {
        string[] values = request.Arguments.Split(':'); string selector = values[0].Trim(); int count = values.Length > 1 ? ParseCount(values[1]) : 1;
        if (count < 1) return CommandResult.Failure("Count must be positive.");
        if (IsIdSelector(selector))
        {
            GameObject byId = FindObject(selector); if (byId == null) return CommandResult.Failure("Object ID not found: " + selector);
            CommandResult one = CommandResult.SuccessResult("Deleted object: " + byId.name); AddDeletedResult(one, byId); Undo.DestroyObjectImmediate(byId); return one;
        }
        List<GameObject> matches = FindByPrefix(selector); if (matches.Count == 0) return CommandResult.Failure("No objects matched: " + selector);
        if (count > matches.Count) count = matches.Count; matches.Sort((a, b) => b.name.CompareTo(a.name));
        CommandResult result = CommandResult.SuccessResult("Deleted " + count + " object(s): " + selector);
        for (int i = 0; i < count; i++) { GameObject go = matches[i]; AddDeletedResult(result, go); Undo.DestroyObjectImmediate(go); }
        return result;
    }

    private static void AddDeletedResult(CommandResult result, GameObject go)
    {
        if (go == null) return; result.DeletedObjects.Add(go.name); CompanyGameObjectIdentity identity = go.GetComponent<CompanyGameObjectIdentity>();
        if (identity != null && !string.IsNullOrEmpty(identity.ObjectId)) result.DeletedObjectIds.Add(identity.ObjectId);
    }

    private static CommandResult RenameObject(CommandRequest request)
    {
        string[] parts = request.Arguments.Split(':'); if (parts.Length < 2) return CommandResult.Failure("RENAME_OBJECT requires old name and new name.");
        string selector = parts[0].Trim(), newName = parts[1].Trim(); if (string.IsNullOrWhiteSpace(newName)) return CommandResult.Failure("New name cannot be empty.");
        GameObject go = FindObject(selector); if (go == null) return CommandResult.Failure("Object not found: " + selector);
        string oldName = go.name; Undo.RecordObject(go, "Rename Object"); go.name = newName; EditorUtility.SetDirty(go);
        CommandResult result = CommandResult.SuccessResult("Renamed object: " + oldName + " -> " + newName); result.RenamedObjects.Add(oldName + " -> " + newName); return result;
    }

    private static int ParseCount(string value) { int count; return int.TryParse(value.Trim(), out count) ? count : 0; }

    private static bool IsIdSelector(string selector)
    {
        return selector.StartsWith("EMP-", StringComparison.OrdinalIgnoreCase) || selector.StartsWith("ROOM-", StringComparison.OrdinalIgnoreCase) || selector.StartsWith("DEPT-", StringComparison.OrdinalIgnoreCase) || selector.StartsWith("MACH-", StringComparison.OrdinalIgnoreCase) || selector.StartsWith("OBJ-", StringComparison.OrdinalIgnoreCase);
    }

    private static GameObject FindObject(string selector)
    {
        if (IsIdSelector(selector))
        {
            CompanyGameObjectIdentity[] identities = UnityEngine.Object.FindObjectsByType<CompanyGameObjectIdentity>(FindObjectsInactive.Include);
            foreach (CompanyGameObjectIdentity identity in identities) if (identity != null && string.Equals(identity.ObjectId, selector, StringComparison.OrdinalIgnoreCase)) return identity.gameObject;
            return null;
        }
        return FindSceneObjectByExactName(selector);
    }

    private static List<GameObject> FindByPrefix(string prefix)
    {
        List<GameObject> result = new List<GameObject>();
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (GameObject go in objects) if (go != null && go.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) result.Add(go);
        return result;
    }

    private static GameObject FindSceneObjectByExactName(string name)
    {
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (GameObject go in objects) if (go != null && string.Equals(go.name, name, StringComparison.Ordinal)) return go;
        return null;
    }

    private static void TryAddComponentByName(GameObject go, string typeName)
    {
        Type type = FindType(typeName); if (type != null && typeof(Component).IsAssignableFrom(type))
        {
            try { Undo.AddComponent(go, type); } catch (Exception ex) { Debug.LogWarning("[Company Game] Optional component " + typeName + " was not added: " + ex.Message); }
        }
    }

    private static Type FindType(string typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) { Type type = assembly.GetType(typeName); if (type != null) return type; }
        return null;
    }

    private static void WriteResult(string projectPath, string id, string raw, CommandResult result)
    {
        string resultsDir = Path.Combine(projectPath, "results"); Directory.CreateDirectory(resultsDir); string path = Path.Combine(resultsDir, "result.json");
        StringBuilder sb = new StringBuilder(); sb.AppendLine("{");
        sb.AppendLine("  \"id\": \"" + Escape(id) + "\","); sb.AppendLine("  \"command\": \"" + Escape(raw) + "\","); sb.AppendLine("  \"success\": " + (result.Success ? "true" : "false") + ","); sb.AppendLine("  \"message\": \"" + Escape(result.Message) + "\","); sb.AppendLine("  \"exception\": \"" + Escape(result.Exception ?? "") + "\",");
        WriteStringArray(sb, "createdObjects", result.CreatedObjects, true); WriteStringArray(sb, "createdObjectIds", result.CreatedObjectIds, true); WriteStringArray(sb, "deletedObjects", result.DeletedObjects, true); WriteStringArray(sb, "deletedObjectIds", result.DeletedObjectIds, true); WriteStringArray(sb, "renamedObjects", result.RenamedObjects, false);
        sb.AppendLine("}"); File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteStringArray(StringBuilder sb, string name, List<string> values, bool commaAfter)
    {
        sb.Append("  \"").Append(name).Append("\": ["); for (int i = 0; i < values.Count; i++) { if (i > 0) sb.Append(", "); sb.Append("\"").Append(Escape(values[i])).Append("\""); } sb.Append("]"); if (commaAfter) sb.Append(","); sb.AppendLine();
    }

    private static string Escape(string value) { return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n"); }
}
