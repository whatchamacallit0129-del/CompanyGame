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
    private static bool commandRunning;
    private static string activeCommand;
    private static readonly List<LogRecord> commandErrors = new List<LogRecord>();

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
        Application.logMessageReceived -= CaptureLog;
        Application.logMessageReceived += CaptureLog;
        Debug.Log("[Company Game] Command Agent started.");
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
            string rawCommand = File.ReadAllText(commandPath).Trim();
            if (string.IsNullOrWhiteSpace(rawCommand)) return;
            commandRunning = true;
            activeCommand = rawCommand;
            commandErrors.Clear();
            CommandResult result = ExecuteCommand(ParseCommand(rawCommand));
            EditorApplication.delayCall += () =>
            {
                try { FinalizeCommand(projectPath, commandPath, result); }
                finally { commandRunning = false; activeCommand = null; commandErrors.Clear(); }
            };
        }
        catch (Exception exception)
        {
            commandRunning = false;
            CommandResult result = CommandResult.Failure("Command execution failed: " + exception.Message);
            result.Exception = exception.ToString();
            WriteResult(projectPath, activeCommand ?? string.Empty, result);
            Debug.LogError("[Company Game] " + exception);
        }
    }

    private static void FinalizeCommand(string projectPath, string commandPath, CommandResult result)
    {
        result.Errors.AddRange(commandErrors);
        if (result.Errors.Count > 0)
        {
            result.Success = false;
            result.Message = "Unity reported errors while executing the command.";
        }
        WriteResult(projectPath, activeCommand ?? string.Empty, result);
        if (result.Success)
        {
            File.Delete(commandPath);
            AssetDatabase.Refresh();
            Debug.Log("[Company Game] SUCCESS: " + result.Message);
        }
        else Debug.LogError("[Company Game] FAILED: " + result.Message);
    }

    private static CommandRequest ParseCommand(string rawCommand)
    {
        string[] parts = rawCommand.Split(new[] { ':' }, 2);
        return new CommandRequest(parts[0].Trim().ToUpperInvariant(), parts.Length > 1 ? parts[1].Trim() : string.Empty);
    }

    private static CommandResult ExecuteCommand(CommandRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name)) return CommandResult.Failure("Command name is empty.");
        if (!CommandHandlers.TryGetValue(request.Name, out Func<CommandRequest, CommandResult> handler)) return CommandResult.Failure("Unknown command: " + request.Name);
        return handler(request);
    }

    private sealed class CommandRequest
    {
        public string Name { get; }
        public string Arguments { get; }
        public CommandRequest(string name, string arguments) { Name = name; Arguments = arguments; }
    }

    private sealed class CommandResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Exception { get; set; }
        public List<string> CreatedObjects { get; } = new List<string>();
        public List<string> DeletedObjects { get; } = new List<string>();
        public List<LogRecord> Errors { get; } = new List<LogRecord>();
        private CommandResult(bool success, string message) { Success = success; Message = message; }
        public static CommandResult SuccessResult(string message) => new CommandResult(true, message);
        public static CommandResult Failure(string message) => new CommandResult(false, message);
    }

    private sealed class LogRecord
    {
        public string Type { get; }
        public string Message { get; }
        public string StackTrace { get; }
        public LogRecord(string type, string message, string stackTrace) { Type = type; Message = message; StackTrace = stackTrace; }
    }

    private static CommandResult CreateInteractableObject(CommandRequest request)
    {
        string[] values = request.Arguments.Split(':');
        string baseName = values.Length > 0 ? values[0].Trim() : "InteractableObject";
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "InteractableObject";
        int count = ParseCount(values, 1);
        if (count < 1 || count > 1000) return CommandResult.Failure("CREATE_INTERACTABLE_OBJECT count must be between 1 and 1000.");

        CommandResult result = CommandResult.SuccessResult("Added " + count + " interactable object(s): " + baseName);
        if (count == 1)
        {
            string name = GameObject.Find(baseName) == null ? baseName : GetNextNumberedName(baseName);
            CreateSingleInteractable(name);
            result.CreatedObjects.Add(name);
            Selection.activeGameObject = GameObject.Find(name);
            return result;
        }

        int next = GetNextNumberedIndex(baseName);
        GameObject first = null;
        for (int i = 0; i < count; i++)
        {
            string name = baseName + " (" + (next + i) + ")";
            GameObject created = CreateSingleInteractable(name);
            if (first == null) first = created;
            result.CreatedObjects.Add(name);
        }
        Selection.activeGameObject = first;
        return result;
    }

    // DELETE_OBJECT:name[:count]
    // With count, find the exact base name and numbered variants using the name prefix.
    private static CommandResult DeleteObjectByName(CommandRequest request)
    {
        string[] values = request.Arguments.Split(':');
        string baseName = values.Length > 0 ? values[0].Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(baseName)) return CommandResult.Failure("DELETE_OBJECT requires an object name.");
        int count = ParseCount(values, 1);
        if (count < 1 || count > 1000) return CommandResult.Failure("DELETE_OBJECT count must be between 1 and 1000.");

        List<GameObject> matches = FindObjectsByPrefix(baseName);
        if (matches.Count == 0) return CommandResult.Failure("No matching objects found: " + baseName);

        matches.Sort((a, b) => CompareObjectNames(a.name, b.name, baseName));
        int deleteCount = Math.Min(count, matches.Count);
        CommandResult result = CommandResult.SuccessResult(string.Empty);

        for (int i = 0; i < deleteCount; i++)
        {
            GameObject target = matches[i];
            if (target == null) continue;
            string name = target.name;
            Undo.DestroyObjectImmediate(target);
            result.DeletedObjects.Add(name);
        }

        if (result.DeletedObjects.Count == 0)
            return CommandResult.Failure("No matching objects could be deleted: " + baseName);

        result.Message = "Deleted " + result.DeletedObjects.Count + " object(s) matching prefix: " + baseName;
        if (result.DeletedObjects.Count < count) result.Message += " (requested " + count + ")";
        return result;
    }

    private static List<GameObject> FindObjectsByPrefix(string prefix)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        List<GameObject> matches = new List<GameObject>();
        foreach (GameObject go in allObjects)
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid()) continue;
            if (go.name.Equals(prefix, StringComparison.Ordinal) || go.name.StartsWith(prefix + " (", StringComparison.Ordinal))
                matches.Add(go);
        }
        return matches;
    }

    private static int CompareObjectNames(string a, string b, string baseName)
    {
        if (a.Equals(baseName, StringComparison.Ordinal)) return -1;
        if (b.Equals(baseName, StringComparison.Ordinal)) return 1;
        int ai = ExtractNumber(a, baseName);
        int bi = ExtractNumber(b, baseName);
        if (ai >= 0 && bi >= 0) return ai.CompareTo(bi);
        if (ai >= 0) return -1;
        if (bi >= 0) return 1;
        return string.CompareOrdinal(a, b);
    }

    private static int ExtractNumber(string name, string baseName)
    {
        string prefix = baseName + " (";
        const string suffix = ")";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal)) return -1;
        string number = name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length);
        return int.TryParse(number, out int value) ? value : -1;
    }

    private static int ParseCount(string[] values, int fallback)
    {
        if (values.Length < 2 || string.IsNullOrWhiteSpace(values[1])) return fallback;
        return int.TryParse(values[1].Trim(), out int count) ? count : -1;
    }

    private static GameObject CreateSingleInteractable(string objectName)
    {
        GameObject go = new GameObject(objectName);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("Sprites/Default.sprite");
        sr.color = Color.white;
        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        TryAddComponentByName(go, "DraggableObject2D");
        TryAddComponentByName(go, "InteractableObject2D");
        Undo.RegisterCreatedObjectUndo(go, "Create Interactable Object");
        EditorUtility.SetDirty(go);
        return go;
    }

    private static int GetNextNumberedIndex(string baseName)
    {
        int index = 1;
        while (GameObject.Find(baseName + " (" + index + ")") != null) index++;
        return index;
    }

    private static string GetNextNumberedName(string baseName) => baseName + " (" + GetNextNumberedIndex(baseName) + ")";

    private static CommandResult CreateEmptyObject(CommandRequest request)
    {
        string name = string.IsNullOrWhiteSpace(request.Arguments) ? "CompanyObject" : request.Arguments.Trim();
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Company Object");
        Selection.activeGameObject = go;
        EditorUtility.SetDirty(go);
        return CommandResult.SuccessResult("Created object: " + name);
    }

    private static CommandResult RenameObject(CommandRequest request)
    {
        if (!TryGetArguments(request.Arguments, 2, out string[] v)) return CommandResult.Failure("RENAME_OBJECT requires objectName:newName.");
        GameObject go = FindObject(v[0]);
        if (go == null) return CommandResult.Failure("Object not found: " + v[0]);
        Undo.RecordObject(go, "Rename Object"); go.name = v[1]; EditorUtility.SetDirty(go);
        return CommandResult.SuccessResult("Renamed object to: " + v[1]);
    }

    private static CommandResult SetActive(CommandRequest request)
    {
        if (!TryGetArguments(request.Arguments, 2, out string[] v) || !bool.TryParse(v[1], out bool active)) return CommandResult.Failure("SET_ACTIVE requires objectName:true|false.");
        GameObject go = FindObject(v[0]); if (go == null) return CommandResult.Failure("Object not found: " + v[0]);
        Undo.RecordObject(go, "Set Active State"); go.SetActive(active); EditorUtility.SetDirty(go);
        return CommandResult.SuccessResult("Set active: " + v[0] + " = " + active);
    }

    private static CommandResult SetPosition(CommandRequest r) => SetTransformVector(r, "Set Position", (t,v)=>t.position=v);
    private static CommandResult SetScale(CommandRequest r) => SetTransformVector(r, "Set Scale", (t,v)=>t.localScale=v);
    private static CommandResult SetRotation(CommandRequest r) => SetTransformVector(r, "Set Rotation", (t,v)=>t.eulerAngles=v);

    private static CommandResult SetTransformVector(CommandRequest r, string undoName, Action<Transform,Vector3> apply)
    {
        if (!TryGetVectorArguments(r.Arguments, out GameObject go, out Vector3 value)) return CommandResult.Failure("Transform command requires objectName:x:y:z.");
        Undo.RecordObject(go.transform, undoName); apply(go.transform,value); EditorUtility.SetDirty(go);
        return CommandResult.SuccessResult(undoName + ": " + go.name);
    }

    private static CommandResult SetParent(CommandRequest r)
    {
        if (!TryGetArguments(r.Arguments,2,out string[] v)) return CommandResult.Failure("SET_PARENT requires child:parent or child:NONE.");
        GameObject child=FindObject(v[0]); if(child==null) return CommandResult.Failure("Object not found: "+v[0]);
        GameObject parent=null; if(!v[1].Equals("NONE",StringComparison.OrdinalIgnoreCase)){parent=FindObject(v[1]);if(parent==null)return CommandResult.Failure("Parent not found: "+v[1]);}
        Undo.SetTransformParent(child.transform,parent?.transform,"Set Parent"); return CommandResult.SuccessResult("Set parent: "+child.name);
    }

    private static CommandResult AddComponent(CommandRequest r)
    {
        if(!TryGetArguments(r.Arguments,2,out string[] v)) return CommandResult.Failure("ADD_COMPONENT requires objectName:componentType.");
        GameObject go=FindObject(v[0]); if(go==null)return CommandResult.Failure("Object not found: "+v[0]);
        Type type=FindComponentType(v[1]); if(!IsValidComponentType(type))return CommandResult.Failure("Component type not found: "+v[1]);
        if(go.GetComponent(type)!=null)return CommandResult.SuccessResult("Component already exists: "+v[1]);
        Undo.AddComponent(go,type); EditorUtility.SetDirty(go); return CommandResult.SuccessResult("Added component: "+v[1]);
    }

    private static CommandResult RemoveComponent(CommandRequest r)
    {
        if(!TryGetArguments(r.Arguments,2,out string[] v))return CommandResult.Failure("REMOVE_COMPONENT requires objectName:componentType.");
        GameObject go=FindObject(v[0]);if(go==null)return CommandResult.Failure("Object not found: "+v[0]);
        Type type=FindComponentType(v[1]);if(!IsValidComponentType(type))return CommandResult.Failure("Component type not found: "+v[1]);
        Component component=go.GetComponent(type);if(component==null)return CommandResult.Failure("Component not found on object: "+v[1]);
        Undo.DestroyObjectImmediate(component);return CommandResult.SuccessResult("Removed component: "+v[1]);
    }

    private static GameObject FindObject(string name)=>string.IsNullOrWhiteSpace(name)?null:GameObject.Find(name);

    private static Type FindComponentType(string name)
    {
        string requested=name.Trim();
        foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type exact=assembly.GetType(requested);if(exact!=null)return exact;
                foreach(Type candidate in assembly.GetTypes())if(candidate.Name.Equals(requested,StringComparison.OrdinalIgnoreCase))return candidate;
            }
            catch(ReflectionTypeLoadException){}
        }
        return null;
    }

    private static bool TryAddComponentByName(GameObject go,string typeName)
    {
        Type type=FindComponentType(typeName);if(!IsValidComponentType(type))return false;if(go.GetComponent(type)!=null)return true;Undo.AddComponent(go,type);return true;
    }

    private static bool IsValidComponentType(Type type)=>type!=null&&typeof(Component).IsAssignableFrom(type)&&!type.IsAbstract;

    private static bool TryGetArguments(string args,int expected,out string[] values)
    {
        values=args.Split(':');if(values.Length!=expected){values=null;return false;}for(int i=0;i<values.Length;i++)values[i]=values[i].Trim();return true;
    }

    private static bool TryGetVectorArguments(string args,out GameObject target,out Vector3 value)
    {
        target=null;value=Vector3.zero;if(!TryGetArguments(args,4,out string[] v)||!TryParseFloat(v[1],out float x)||!TryParseFloat(v[2],out float y)||!TryParseFloat(v[3],out float z))return false;target=FindObject(v[0]);if(target==null)return false;value=new Vector3(x,y,z);return true;
    }

    private static bool TryParseFloat(string value,out float result)=>float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out result);

    private static void WriteResult(string projectPath,string command,CommandResult result)
    {
        string path=Path.Combine(projectPath,ResultFileName);
        string json="{\n"+
            "  \"command\": \""+EscapeJson(command)+"\",\n"+
            "  \"success\": "+result.Success.ToString().ToLowerInvariant()+",\n"+
            "  \"message\": \""+EscapeJson(result.Message)+"\",\n"+
            "  \"exception\": \""+EscapeJson(result.Exception)+"\",\n"+
            "  \"createdObjects\": ["+QuoteList(result.CreatedObjects)+"],\n"+
            "  \"deletedObjects\": ["+QuoteList(result.DeletedObjects)+"],\n"+
            "  \"errors\": ["+QuoteErrors(result.Errors)+"]\n"+
            "}";
        File.WriteAllText(path,json);
    }

    private static string QuoteList(List<string> values)
    {
        List<string> quoted=new List<string>();foreach(string value in values)quoted.Add("\""+EscapeJson(value)+"\"");return string.Join(",",quoted.ToArray());
    }

    private static string QuoteErrors(List<LogRecord> values)
    {
        List<string> quoted=new List<string>();
        foreach(LogRecord value in values)quoted.Add("{\"type\":\""+EscapeJson(value.Type)+"\",\"message\":\""+EscapeJson(value.Message)+"\",\"stackTrace\":\""+EscapeJson(value.StackTrace)+"\"}");
        return string.Join(",",quoted.ToArray());
    }

    private static string EscapeJson(string value)
    {
        return (value??string.Empty).Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n");
    }
}
