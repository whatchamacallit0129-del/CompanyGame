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
    private static readonly Dictionary<string, Func<CommandRequest, CommandResult>> Handlers = new Dictionary<string, Func<CommandRequest, CommandResult>>(StringComparer.OrdinalIgnoreCase)
    {
        { "CREATE_INTERACTABLE_OBJECT", CreateInteractableObject },
        { "CREATE_EMPTY_OBJECT", CreateEmptyObject },
        { "DELETE_OBJECT", DeleteObject },
        { "DELETE_OBJECTS", DeleteObjects },
        { "RENAME_OBJECT", RenameObject }
    };

    private static string CommandFilePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "command.json"));
    private static string ProcessingFilePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "command.processing.json"));
    private static bool commandInProgress;
    private const int FileMoveAttempts = 10;
    private const int FileMoveDelayMs = 75;

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.update -= CheckCommand;
        EditorApplication.update += CheckCommand;
    }

    private static void CheckCommand()
    {
        if (commandInProgress || !File.Exists(CommandFilePath)) return;

        try
        {
            commandInProgress = true;

            // A producer may still be closing command.json. Do not treat a transient
            // sharing violation as a command failure, and never delete the producer's file.
            if (!TryClaimCommandFile())
            {
                commandInProgress = false;
                return;
            }

            string raw = File.ReadAllText(ProcessingFilePath, Encoding.UTF8).Trim().TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(raw))
            {
                SafeDelete(ProcessingFilePath);
                commandInProgress = false;
                return;
            }

            ExecuteAndReport(raw, ProcessingFilePath);
        }
        catch (Exception ex)
        {
            Debug.LogError("[Company Game] Agent error: " + ex);
            SafeDelete(ProcessingFilePath);
            // command.json is intentionally NOT deleted here. If claiming failed,
            // the producer still owns it; if processing failed, retaining the source
            // lets the next editor tick retry instead of silently losing work.
            commandInProgress = false;
        }
    }

    private static bool TryClaimCommandFile()
    {
        if (File.Exists(ProcessingFilePath)) SafeDelete(ProcessingFilePath);

        for (int attempt = 0; attempt < FileMoveAttempts; attempt++)
        {
            try
            {
                File.Move(CommandFilePath, ProcessingFilePath);
                return true;
            }
            catch (IOException)
            {
                if (attempt == FileMoveAttempts - 1) return false;
                System.Threading.Thread.Sleep(FileMoveDelayMs);
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt == FileMoveAttempts - 1) return false;
                System.Threading.Thread.Sleep(FileMoveDelayMs);
            }
        }

        return false;
    }

    private static void ExecuteAndReport(string raw, string processingPath)
    {
        string id = GetCommandId(raw, processingPath);
        CommandResult result;
        try { result = Execute(ParseCommand(raw)); }
        catch (Exception ex)
        {
            result = CommandResult.Failure("Command execution exception: " + ex.Message);
            result.Exception = ex.ToString();
        }
        try { WriteResult(id, raw, result); }
        catch (Exception ex) { Debug.LogError("[Company Game] Failed to write result.json: " + ex); }
        SafeDelete(processingPath);
        commandInProgress = false;
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

    [Serializable]
    private sealed class CommandEnvelope { public string content; public string encoding; }

    private sealed class CommandResult
    {
        public bool Success; public string Message; public string Exception;
        public readonly List<string> CreatedObjects = new List<string>();
        public readonly List<string> CreatedObjectIds = new List<string>();
        public readonly List<string> DeletedObjects = new List<string>();
        public readonly List<string> DeletedObjectIds = new List<string>();
        public readonly List<string> RenamedObjects = new List<string>();
        private CommandResult(bool success, string message) { Success = success; Message = message; }
        public static CommandResult SuccessResult(string message) => new CommandResult(true, message);
        public static CommandResult Failure(string message) => new CommandResult(false, message);
    }

    private static CommandRequest ParseCommand(string raw)
    {
        raw = (raw ?? string.Empty).Trim().TrimStart('\uFEFF');
        if (raw.StartsWith("{"))
        {
            CommandEnvelope envelope = JsonUtility.FromJson<CommandEnvelope>(raw);
            if (envelope == null || string.IsNullOrWhiteSpace(envelope.content)) throw new InvalidOperationException("command.json JSON envelope is missing 'content'.");
            raw = envelope.content.Trim().TrimStart('\uFEFF');
        }
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
        if (parts.Length < 2) return CommandResult.Failure("CREATE_INTERACTABLE_OBJECT requires a count.");
        string category = ResolveCreationCategory(baseName);
        int count; if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1 || count > 1000) return CommandResult.Failure("Count must be 1-1000.");
        CommandResult result = CommandResult.SuccessResult("Created " + count + " interactable object(s): " + baseName);
        HashSet<int> used = new HashSet<int>();
        for (int i = 0; i < count; i++)
        {
            int number = GetNextAvailableNumber(baseName, used); used.Add(number);
            string name = baseName + " (" + number + ")";
            GameObject go = CreateSingleInteractable(name, category);
            result.CreatedObjects.Add(name); AddIdentityResult(result, go);
        }
        return result;
    }

    private static int GetNextAvailableNumber(string baseName, HashSet<int> reserved)
    {
        int number = 1;
        while (reserved.Contains(number) || FindSceneObjectByExactName(baseName + " (" + number + ")") != null) number++;
        return number;
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
        EmployeeId employee = go.GetComponent<EmployeeId>();
        if (employee != null && !string.IsNullOrEmpty(employee.EmployeeID)) { result.CreatedObjectIds.Add(employee.EmployeeID); return; }
        CompanyGameObjectIdentity identity = go.GetComponent<CompanyGameObjectIdentity>();
        if (identity != null && !string.IsNullOrEmpty(identity.ObjectId)) result.CreatedObjectIds.Add(identity.ObjectId);
    }

    private static GameObject CreateSingleInteractable(string name, string category)
    {
        GameObject go = new GameObject(name);
        Undo.AddComponent<BoxCollider2D>(go);
        TryAddComponentByName(go, "DraggableObject2D");
        TryAddComponentByName(go, "InteractableObject2D");
        if (string.Equals(category, "Employee", StringComparison.OrdinalIgnoreCase)) { EmployeeId employeeId = Undo.AddComponent<EmployeeId>(go); employeeId.EnsureId(); }
        else { CompanyGameObjectIdentity identity = Undo.AddComponent<CompanyGameObjectIdentity>(go); identity.EnsureIdentity(category); }
        Undo.RegisterCreatedObjectUndo(go, "Create Interactable Object"); EditorUtility.SetDirty(go); return go;
    }

    private static CommandResult CreateEmptyObject(CommandRequest request)
    {
        string name = string.IsNullOrWhiteSpace(request.Arguments) ? "CompanyObject" : request.Arguments.Trim();
        string category = ResolveCreationCategory(name); GameObject go = new GameObject(name);
        if (category == "Employee") { EmployeeId employeeId = Undo.AddComponent<EmployeeId>(go); employeeId.EnsureId(); }
        else { CompanyGameObjectIdentity identity = Undo.AddComponent<CompanyGameObjectIdentity>(go); identity.EnsureIdentity(category); }
        Undo.RegisterCreatedObjectUndo(go, "Create Company Object"); CommandResult result = CommandResult.SuccessResult("Created object: " + name); result.CreatedObjects.Add(name); AddIdentityResult(result, go); return result;
    }

    private static CommandResult DeleteObjects(CommandRequest request)
    {
        string[] parts = request.Arguments.Split(':');
        if (parts.Length < 2) return CommandResult.Failure("DELETE_OBJECTS requires category and count.");
        string selector = parts[0].Trim(); int count;
        if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1 || count > 1000) return CommandResult.Failure("Count must be 1-1000.");
        List<GameObject> matches = FindByCategory(selector);
        if (matches.Count == 0) return CommandResult.Failure("No objects matched category: " + selector);
        matches.Sort(CompareObjectNamesDescending);
        int deleteCount = Math.Min(count, matches.Count);
        CommandResult result = CommandResult.SuccessResult("Deleted " + deleteCount + " object(s): " + selector);
        for (int i = 0; i < deleteCount; i++) { GameObject go = matches[i]; AddDeletedResult(result, go); Undo.DestroyObjectImmediate(go); }
        return result;
    }

    private static int CompareObjectNamesDescending(GameObject a, GameObject b) { return string.Compare(b.name, a.name, StringComparison.OrdinalIgnoreCase); }

    private static List<GameObject> FindByCategory(string selector)
    {
        List<GameObject> result = new List<GameObject>();
        EmployeeId[] employees = UnityEngine.Object.FindObjectsByType<EmployeeId>(FindObjectsInactive.Include);
        foreach (EmployeeId employee in employees) if (employee != null && (selector.Equals("직원", StringComparison.OrdinalIgnoreCase) || selector.Equals("Employee", StringComparison.OrdinalIgnoreCase))) result.Add(employee.gameObject);
        if (result.Count > 0) return result;
        return FindByPrefix(selector);
    }

    private static void AddDeletedResult(CommandResult result, GameObject go)
    {
        if (go == null) return; result.DeletedObjects.Add(go.name);
        EmployeeId employee = go.GetComponent<EmployeeId>();
        if (employee != null && !string.IsNullOrEmpty(employee.EmployeeID)) result.DeletedObjectIds.Add(employee.EmployeeID);
        else { CompanyGameObjectIdentity identity = go.GetComponent<CompanyGameObjectIdentity>(); if (identity != null && !string.IsNullOrEmpty(identity.ObjectId)) result.DeletedObjectIds.Add(identity.ObjectId); }
    }

    private static CommandResult DeleteObject(CommandRequest request)
    {
        string selector = request.Arguments.Trim(); GameObject go = FindObject(selector); if (go == null) return CommandResult.Failure("Object not found: " + selector);
        CommandResult result = CommandResult.SuccessResult("Deleted object: " + go.name); AddDeletedResult(result, go); Undo.DestroyObjectImmediate(go); return result;
    }

    private static CommandResult RenameObject(CommandRequest request)
    {
        string[] parts = request.Arguments.Split(':'); if (parts.Length < 2) return CommandResult.Failure("RENAME_OBJECT requires old name and new name.");
        GameObject go = FindObject(parts[0].Trim()); if (go == null) return CommandResult.Failure("Object not found: " + parts[0]);
        string oldName = go.name, newName = parts[1].Trim(); if (string.IsNullOrWhiteSpace(newName)) return CommandResult.Failure("New name cannot be empty.");
        Undo.RecordObject(go, "Rename Object"); go.name = newName; EditorUtility.SetDirty(go); CommandResult result = CommandResult.SuccessResult("Renamed object: " + oldName + " -> " + newName); result.RenamedObjects.Add(oldName + " -> " + newName); return result;
    }

    private static GameObject FindObject(string selector)
    {
        EmployeeId[] employees = UnityEngine.Object.FindObjectsByType<EmployeeId>(FindObjectsInactive.Include);
        foreach (EmployeeId employee in employees) if (employee != null && string.Equals(employee.EmployeeID, selector, StringComparison.OrdinalIgnoreCase)) return employee.gameObject;
        CompanyGameObjectIdentity[] identities = UnityEngine.Object.FindObjectsByType<CompanyGameObjectIdentity>(FindObjectsInactive.Include);
        foreach (CompanyGameObjectIdentity identity in identities) if (identity != null && string.Equals(identity.ObjectId, selector, StringComparison.OrdinalIgnoreCase)) return identity.gameObject;
        return FindSceneObjectByExactName(selector);
    }

    private static List<GameObject> FindByPrefix(string prefix)
    {
        List<GameObject> result = new List<GameObject>(); GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (GameObject go in objects) if (go != null && go.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) result.Add(go); return result;
    }

    private static GameObject FindSceneObjectByExactName(string name)
    {
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (GameObject go in objects) if (go != null && string.Equals(go.name, name, StringComparison.Ordinal)) return go; return null;
    }

    private static void TryAddComponentByName(GameObject go, string typeName)
    {
        Type type = FindType(typeName); if (type != null && typeof(Component).IsAssignableFrom(type)) try { Undo.AddComponent(go, type); } catch (Exception ex) { Debug.LogWarning("[Company Game] Optional component " + typeName + " was not added: " + ex.Message); }
    }

    private static Type FindType(string typeName) { foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) { Type type = assembly.GetType(typeName); if (type != null) return type; } return null; }

    private static void WriteResult(string id, string raw, CommandResult result)
    {
        string dir = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "results"); Directory.CreateDirectory(dir);
        StringBuilder sb = new StringBuilder(); sb.AppendLine("{");
        sb.AppendLine("  \"id\": \"" + Escape(id) + "\","); sb.AppendLine("  \"command\": \"" + Escape(raw) + "\","); sb.AppendLine("  \"success\": " + (result.Success ? "true" : "false") + ","); sb.AppendLine("  \"message\": \"" + Escape(result.Message) + "\","); sb.AppendLine("  \"exception\": \"" + Escape(result.Exception ?? "") + "\",");
        WriteArray(sb, "createdObjects", result.CreatedObjects); sb.AppendLine(","); WriteArray(sb, "createdObjectIds", result.CreatedObjectIds); sb.AppendLine(","); WriteArray(sb, "deletedObjects", result.DeletedObjects); sb.AppendLine(","); WriteArray(sb, "deletedObjectIds", result.DeletedObjectIds); sb.AppendLine(","); WriteArray(sb, "renamedObjects", result.RenamedObjects); sb.AppendLine(); sb.AppendLine("}");
        File.WriteAllText(Path.Combine(dir, "result.json"), sb.ToString(), new UTF8Encoding(false));
    }

    private static void WriteArray(StringBuilder sb, string name, List<string> values)
    {
        sb.Append("  \"").Append(name).Append("\": [");
        for (int i = 0; i < values.Count; i++) { if (i > 0) sb.Append(", "); sb.Append("\"").Append(Escape(values[i])).Append("\""); }
        sb.Append("]");
    }

    private static string Escape(string value) { return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n"); }
}
