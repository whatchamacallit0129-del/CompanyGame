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
    private const string NumberRegistryKeyPrefix = "CompanyGame.CommandAgent.NextNumber.";
    private static bool commandRunning;
    private static readonly List<LogRecord> commandErrors = new List<LogRecord>();
    private static readonly Dictionary<string, Func<CommandRequest, CommandResult>> Handlers = new Dictionary<string, Func<CommandRequest, CommandResult>>(StringComparer.OrdinalIgnoreCase)
    {
        { "CREATE_INTERACTABLE_OBJECT", CreateInteractableObject }, { "CREATE_EMPTY_OBJECT", CreateEmptyObject }, { "CREATE_OBJECT", CreateEmptyObject },
        { "DELETE_OBJECT", DeleteObject }, { "RENAME_OBJECT", RenameObject }, { "SET_ACTIVE", SetActive }, { "SET_POSITION", SetPosition },
        { "SET_SCALE", SetScale }, { "SET_ROTATION", SetRotation }, { "SET_PARENT", SetParent }, { "ADD_COMPONENT", AddComponent },
        { "REMOVE_COMPONENT", RemoveComponent }, { "SET_COMPONENT_ACTIVE", SetComponentActive }, { "SET_COMPONENT_PROPERTY", SetComponentProperty }
    };

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.update -= CheckCommand; EditorApplication.update += CheckCommand;
        Application.logMessageReceived -= CaptureLog; Application.logMessageReceived += CaptureLog;
        Debug.Log("[Company Game] Command Agent ready.");
    }

    private static void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (commandRunning && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) commandErrors.Add(new LogRecord(type.ToString(), condition, stackTrace));
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
            commandRunning = true; commandErrors.Clear();
            CommandResult result;
            string id = GetCommandId(raw, commandPath);
            try { result = Execute(ParseCommand(raw)); }
            catch (Exception ex) { result = CommandResult.Failure("Command execution exception: " + ex.Message); result.Exception = ex.ToString(); }
            result.Errors.AddRange(commandErrors);
            if (result.Errors.Count > 0) { result.Success = false; result.Message = "Unity reported errors while executing the command."; }
            WriteResult(projectPath, id, raw, result);
            SafeDelete(commandPath); AssetDatabase.Refresh();
            if (result.Success) Debug.Log("[Company Game] SUCCESS: " + result.Message); else Debug.LogError("[Company Game] FAILED: " + result.Message);
        }
        catch (Exception ex) { Debug.LogError("[Company Game] Agent error: " + ex); }
        finally { commandRunning = false; commandErrors.Clear(); }
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

    private static void SafeDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (Exception ex) { Debug.LogWarning("[Company Game] Could not delete command file: " + ex.Message); } }
    private sealed class CommandRequest { public string Name; public string Arguments; public CommandRequest(string name, string arguments) { Name = name; Arguments = arguments; } }
    private sealed class CommandResult { public bool Success; public string Message; public string Exception; public readonly List<string> CreatedObjects = new List<string>(); public readonly List<string> DeletedObjects = new List<string>(); public readonly List<LogRecord> Errors = new List<LogRecord>(); private CommandResult(bool success, string message) { Success = success; Message = message; } public static CommandResult SuccessResult(string message) { return new CommandResult(true, message); } public static CommandResult Failure(string message) { return new CommandResult(false, message); } }
    private sealed class LogRecord { public string Type; public string Message; public string StackTrace; public LogRecord(string type, string message, string stackTrace) { Type = type; Message = message; StackTrace = stackTrace; } }
    private static CommandRequest ParseCommand(string raw) { string[] parts = raw.Split(new[] { ':' }, 2); return new CommandRequest(parts[0].Trim().ToUpperInvariant(), parts.Length > 1 ? parts[1].Trim() : ""); }
    private static CommandResult Execute(CommandRequest request) { if (string.IsNullOrWhiteSpace(request.Name)) return CommandResult.Failure("Command name is empty."); Func<CommandRequest, CommandResult> handler; if (!Handlers.TryGetValue(request.Name, out handler)) return CommandResult.Failure("Unknown command: " + request.Name); return handler(request); }

    private static CommandResult CreateInteractableObject(CommandRequest request)
    {
        string[] parts = request.Arguments.Split(':');
        string baseName = parts.Length > 0 ? parts[0].Trim() : "InteractableObject";
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "InteractableObject";
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1])) return CommandResult.Failure("CREATE_INTERACTABLE_OBJECT requires a count or explicit numbers.");
        string numberSpec = parts[1].Trim();
        List<int> numbers;
        if (TryParseExplicitNumbers(numberSpec, out numbers))
        {
            CommandResult explicitResult = CommandResult.SuccessResult("Created " + numbers.Count + " interactable object(s): " + baseName);
            HashSet<int> reserved = new HashSet<int>();
            foreach (int number in numbers)
            {
                if (number < 1) return CommandResult.Failure("Object numbers must be positive.");
                if (!reserved.Add(number)) return CommandResult.Failure("Duplicate object number: " + number);
                string objectName = baseName + " (" + number + ")";
                if (GameObject.Find(objectName) != null) return CommandResult.Failure("Object already exists: " + objectName);
            }
            foreach (int number in numbers)
            {
                string objectName = baseName + " (" + number + ")";
                CreateSingleInteractable(objectName); explicitResult.CreatedObjects.Add(objectName);
            }
            int next = GetNextNumberedIndex(baseName);
            if (numbers.Count > 0) { int highest = 0; foreach (int n in numbers) if (n > highest) highest = n; if (next <= highest) SetNextNumber(baseName, highest + 1); }
            return explicitResult;
        }
        int count;
        if (!int.TryParse(numberSpec, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1 || count > 1000) return CommandResult.Failure("Count must be 1-1000, or provide explicit numbers such as 7,9.");
        int nextNumber = GetNextNumberedIndex(baseName);
        CommandResult result = CommandResult.SuccessResult("Created " + count + " interactable object(s): " + baseName);
        for (int i = 0; i < count; i++)
        {
            int number = nextNumber + i;
            string objectName = baseName + " (" + number + ")";
            CreateSingleInteractable(objectName); result.CreatedObjects.Add(objectName);
        }
        SetNextNumber(baseName, nextNumber + count);
        return result;
    }

    private static bool TryParseExplicitNumbers(string text, out List<int> numbers)
    {
        numbers = new List<int>();
        if (text.IndexOf(',') < 0) return false;
        string[] pieces = text.Split(',');
        if (pieces.Length == 0) return false;
        foreach (string piece in pieces)
        {
            int value; if (!int.TryParse(piece.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return false;
            numbers.Add(value);
        }
        return numbers.Count > 0;
    }

    private static int GetNextNumberedIndex(string baseName)
    {
        string key = NumberRegistryKeyPrefix + baseName;
        int stored = EditorPrefs.GetInt(key, 1);
        int next = Math.Max(1, stored);
        while (GameObject.Find(baseName + " (" + next + ")") != null) next++;
        if (next != stored) EditorPrefs.SetInt(key, next);
        return next;
    }

    private static void SetNextNumber(string baseName, int next) { EditorPrefs.SetInt(NumberRegistryKeyPrefix + baseName, Math.Max(1, next)); }

    private static CommandResult DeleteObject(CommandRequest request) { string[] v = request.Arguments.Split(':'); string prefix = v.Length > 0 ? v[0].Trim() : ""; int count = ParseCount(v, 1); if (string.IsNullOrWhiteSpace(prefix)) return CommandResult.Failure("DELETE_OBJECT requires a name."); if (count < 1 || count > 1000) return CommandResult.Failure("Count must be 1-1000."); List<GameObject> matches = FindByPrefix(prefix); if (matches.Count == 0) return CommandResult.Failure("No matching objects found: " + prefix); matches.Sort(delegate(GameObject a, GameObject b) { return CompareNames(a.name, b.name, prefix); }); CommandResult result = CommandResult.SuccessResult(""); int amount = Math.Min(count, matches.Count); for (int i = 0; i < amount; i++) { if (matches[i] == null) continue; string name = matches[i].name; Undo.DestroyObjectImmediate(matches[i]); result.DeletedObjects.Add(name); } if (result.DeletedObjects.Count == 0) return CommandResult.Failure("No objects could be deleted: " + prefix); result.Message = "Deleted " + result.DeletedObjects.Count + " object(s) matching prefix: " + prefix; return result; }
    private static List<GameObject> FindByPrefix(string prefix) { GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>(); List<GameObject> matches = new List<GameObject>(); foreach (GameObject go in all) { if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid()) continue; if (go.name.Equals(prefix, StringComparison.Ordinal) || go.name.StartsWith(prefix + " (", StringComparison.Ordinal)) matches.Add(go); } return matches; }
    private static int CompareNames(string a, string b, string prefix) { if (a.Equals(prefix, StringComparison.Ordinal)) return 1; if (b.Equals(prefix, StringComparison.Ordinal)) return -1; int ai = ExtractNumber(a, prefix), bi = ExtractNumber(b, prefix); if (ai >= 0 && bi >= 0) return bi.CompareTo(ai); if (ai >= 0) return -1; if (bi >= 0) return 1; return string.CompareOrdinal(b, a); }
    private static int ExtractNumber(string name, string prefix) { string start = prefix + " ("; if (!name.StartsWith(start, StringComparison.Ordinal) || !name.EndsWith(")", StringComparison.Ordinal)) return -1; int value; return int.TryParse(name.Substring(start.Length, name.Length - start.Length - 1), out value) ? value : -1; }
    private static int ParseCount(string[] values, int fallback) { if (values.Length < 2 || string.IsNullOrWhiteSpace(values[1])) return fallback; int count; return int.TryParse(values[1].Trim(), out count) ? count : -1; }
    private static GameObject CreateSingleInteractable(string name) { GameObject go = new GameObject(name); SpriteRenderer sr = go.AddComponent<SpriteRenderer>(); sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("Sprites/Default.sprite"); go.AddComponent<BoxCollider2D>(); TryAddComponentByName(go, "DraggableObject2D"); TryAddComponentByName(go, "InteractableObject2D"); Undo.RegisterCreatedObjectUndo(go, "Create Interactable Object"); Selection.activeGameObject = go; EditorUtility.SetDirty(go); return go; }

    private static CommandResult CreateEmptyObject(CommandRequest r) { string name = string.IsNullOrWhiteSpace(r.Arguments) ? "CompanyObject" : r.Arguments.Trim(); GameObject go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Create Company Object"); Selection.activeGameObject = go; return CommandResult.SuccessResult("Created object: " + name); }
    private static CommandResult RenameObject(CommandRequest r) { string[] v; if (!Args(r.Arguments, 2, out v)) return CommandResult.Failure("RENAME_OBJECT requires object:newName."); GameObject go = FindObject(v[0]); if (go == null) return CommandResult.Failure("Object not found: " + v[0]); Undo.RecordObject(go, "Rename Object"); go.name = v[1]; EditorUtility.SetDirty(go); return CommandResult.SuccessResult("Renamed object to: " + v[1]); }
    private static CommandResult SetActive(CommandRequest r) { string[] v; bool active; if (!Args(r.Arguments, 2, out v) || !bool.TryParse(v[1], out active)) return CommandResult.Failure("SET_ACTIVE requires object:true|false."); GameObject go = FindObject(v[0]); if (go == null) return CommandResult.Failure("Object not found: " + v[0]); Undo.RecordObject(go, "Set Active"); go.SetActive(active); return CommandResult.SuccessResult("Set active: " + v[0]); }
    private static CommandResult SetPosition(CommandRequest r) { return SetVector(r, "position", delegate(Transform t, Vector3 v) { t.position = v; }); }
    private static CommandResult SetScale(CommandRequest r) { return SetVector(r, "scale", delegate(Transform t, Vector3 v) { t.localScale = v; }); }
    private static CommandResult SetRotation(CommandRequest r) { return SetVector(r, "rotation", delegate(Transform t, Vector3 v) { t.eulerAngles = v; }); }
    private static CommandResult SetVector(CommandRequest r, string label, Action<Transform, Vector3> apply) { string[] v; if (!Args(r.Arguments, 4, out v)) return CommandResult.Failure(label + " requires object:x:y:z."); float x, y, z; if (!TryFloat(v[1], out x) || !TryFloat(v[2], out y) || !TryFloat(v[3], out z)) return CommandResult.Failure("Invalid vector values."); GameObject go = FindObject(v[0]); if (go == null) return CommandResult.Failure("Object not found: " + v[0]); Undo.RecordObject(go.transform, "Set " + label); apply(go.transform, new Vector3(x, y, z)); EditorUtility.SetDirty(go); return CommandResult.SuccessResult("Set " + label + ": " + go.name); }
    private static CommandResult SetParent(CommandRequest r) { string[] v; if (!Args(r.Arguments, 2, out v)) return CommandResult.Failure("SET_PARENT requires child:parent or child:NONE."); GameObject child = FindObject(v[0]); if (child == null) return CommandResult.Failure("Object not found: " + v[0]); Transform parent = null; if (!v[1].Equals("NONE", StringComparison.OrdinalIgnoreCase)) { GameObject p = FindObject(v[1]); if (p == null) return CommandResult.Failure("Parent not found: " + v[1]); parent = p.transform; } Undo.SetTransformParent(child.transform, parent, "Set Parent"); return CommandResult.SuccessResult("Set parent: " + child.name); }
    private static CommandResult AddComponent(CommandRequest r) { string[] v; if (!Args(r.Arguments, 2, out v)) return CommandResult.Failure("ADD_COMPONENT requires object:component."); GameObject go = FindObject(v[0]); if (go == null) return CommandResult.Failure("Object not found: " + v[0]); Type type = FindComponentType(v[1]); if (!ValidComponent(type)) return CommandResult.Failure("Component type not found: " + v[1]); if (go.GetComponent(type) != null) return CommandResult.SuccessResult("Component already exists: " + v[1]); Undo.AddComponent(go, type); return CommandResult.SuccessResult("Added component: " + v[1]); }
    private static CommandResult RemoveComponent(CommandRequest r) { string[] v; if (!Args(r.Arguments, 2, out v)) return CommandResult.Failure("REMOVE_COMPONENT requires object:component."); GameObject go = FindObject(v[0]); if (go == null) return CommandResult.Failure("Object not found: " + v[0]); Type type = FindComponentType(v[1]); if (!ValidComponent(type)) return CommandResult.Failure("Component type not found: " + v[1]); Component component = go.GetComponent(type); if (component == null) return CommandResult.Failure("Component not found: " + v[1]); Undo.DestroyObjectImmediate(component); return CommandResult.SuccessResult("Removed component: " + v[1]); }
    private static CommandResult SetComponentActive(CommandRequest r) { string[] v; if (!Args(r.Arguments, 3, out v)) return CommandResult.Failure("SET_COMPONENT_ACTIVE requires object:component:true|false."); bool active; if (!bool.TryParse(v[2], out active)) return CommandResult.Failure("Component active value must be true or false."); GameObject go = FindObject(v[0]); if (go == null) return CommandResult.Failure("Object not found: " + v[0]); Type type = FindComponentType(v[1]); if (!ValidComponent(type)) return CommandResult.Failure("Component type not found: " + v[1]); Component component = go.GetComponent(type); if (component == null) return CommandResult.Failure("Component not found on object: " + v[1]); Behaviour behaviour = component as Behaviour; if (behaviour != null) { Undo.RecordObject(behaviour, "Set Component Active"); behaviour.enabled = active; EditorUtility.SetDirty(behaviour); return CommandResult.SuccessResult("Set component active: " + go.name + " / " + type.Name + " = " + active); } Renderer renderer = component as Renderer; if (renderer != null) { Undo.RecordObject(renderer, "Set Component Active"); renderer.enabled = active; EditorUtility.SetDirty(renderer); return CommandResult.SuccessResult("Set component active: " + go.name + " / " + type.Name + " = " + active); } Collider collider = component as Collider; if (collider != null) { Undo.RecordObject(collider, "Set Component Active"); collider.enabled = active; EditorUtility.SetDirty(collider); return CommandResult.SuccessResult("Set component active: " + go.name + " / " + type.Name + " = " + active); } Collider2D collider2D = component as Collider2D; if (collider2D != null) { Undo.RecordObject(collider2D, "Set Component Active"); collider2D.enabled = active; EditorUtility.SetDirty(collider2D); return CommandResult.SuccessResult("Set component active: " + go.name + " / " + type.Name + " = " + active); } return CommandResult.Failure("Component does not expose an enabled state: " + type.Name); }
    private static CommandResult SetComponentProperty(CommandRequest r) { string[] v = r.Arguments.Split(new[] { ':' }, 4); if (v.Length != 4) return CommandResult.Failure("SET_COMPONENT_PROPERTY requires object:component:property:value."); GameObject go = FindObject(v[0].Trim()); if (go == null) return CommandResult.Failure("Object not found: " + v[0]); Type type = FindComponentType(v[1].Trim()); if (!ValidComponent(type)) return CommandResult.Failure("Component type not found: " + v[1]); Component component = go.GetComponent(type); if (component == null) return CommandResult.Failure("Component not found on object: " + v[1]); SerializedObject serializedObject = new SerializedObject(component); SerializedProperty property = serializedObject.FindProperty(v[2].Trim()); if (property == null) return CommandResult.Failure("Serialized property not found: " + v[1] + "." + v[2]); Undo.RecordObject(component, "Set Component Property"); string error; if (!TrySetSerializedProperty(property, v[3].Trim(), out error)) return CommandResult.Failure(error); serializedObject.ApplyModifiedProperties(); EditorUtility.SetDirty(component); return CommandResult.SuccessResult("Set component property: " + go.name + " / " + type.Name + "." + v[2].Trim()); }
    private static bool TrySetSerializedProperty(SerializedProperty property, string valueText, out string error) { error = null; switch (property.propertyType) { case SerializedPropertyType.Integer: int i; if (!int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) { error = "Invalid integer: " + valueText; return false; } property.intValue = i; return true; case SerializedPropertyType.Boolean: bool b; if (!bool.TryParse(valueText, out b)) { error = "Invalid boolean: " + valueText; return false; } property.boolValue = b; return true; case SerializedPropertyType.Float: float f; if (!TryFloat(valueText, out f)) { error = "Invalid float: " + valueText; return false; } property.floatValue = f; return true; case SerializedPropertyType.String: property.stringValue = valueText; return true; case SerializedPropertyType.Enum: for (int n = 0; n < property.enumNames.Length; n++) if (property.enumNames[n].Equals(valueText, StringComparison.OrdinalIgnoreCase) || property.enumDisplayNames[n].Equals(valueText, StringComparison.OrdinalIgnoreCase)) { property.enumValueIndex = n; return true; } int enumIndex; if (int.TryParse(valueText, out enumIndex) && enumIndex >= 0 && enumIndex < property.enumNames.Length) { property.enumValueIndex = enumIndex; return true; } error = "Enum value not found: " + valueText; return false; case SerializedPropertyType.Vector2: Vector2 v2; if (!TryParseVector2(valueText, out v2)) { error = "Vector2 requires x,y: " + valueText; return false; } property.vector2Value = v2; return true; case SerializedPropertyType.Vector3: Vector3 v3; if (!TryParseVector3(valueText, out v3)) { error = "Vector3 requires x,y,z: " + valueText; return false; } property.vector3Value = v3; return true; case SerializedPropertyType.Vector4: Vector4 v4; if (!TryParseVector4(valueText, out v4)) { error = "Vector4 requires x,y,z,w: " + valueText; return false; } property.vector4Value = v4; return true; case SerializedPropertyType.Color: Color color; if (!TryParseColor(valueText, out color)) { error = "Color requires r,g,b or r,g,b,a: " + valueText; return false; } property.colorValue = color; return true; default: error = "Unsupported serialized property type: " + property.propertyType; return false; } }
    private static bool TryParseVector2(string text, out Vector2 value) { float[] n; if (!TryParseNumbers(text, 2, out n)) { value = Vector2.zero; return false; } value = new Vector2(n[0], n[1]); return true; }
    private static bool TryParseVector3(string text, out Vector3 value) { float[] n; if (!TryParseNumbers(text, 3, out n)) { value = Vector3.zero; return false; } value = new Vector3(n[0], n[1], n[2]); return true; }
    private static bool TryParseVector4(string text, out Vector4 value) { float[] n; if (!TryParseNumbers(text, 4, out n)) { value = Vector4.zero; return false; } value = new Vector4(n[0], n[1], n[2], n[3]); return true; }
    private static bool TryParseColor(string text, out Color value) { float[] n; if (!TryParseNumbers(text, 3, out n) && !TryParseNumbers(text, 4, out n)) { value = Color.white; return false; } value = n.Length == 3 ? new Color(n[0], n[1], n[2], 1f) : new Color(n[0], n[1], n[2], n[3]); return true; }
    private static bool TryParseNumbers(string text, int count, out float[] values) { string[] parts = text.Split(','); values = null; if (parts.Length != count) return false; float[] result = new float[count]; for (int i = 0; i < count; i++) if (!TryFloat(parts[i].Trim(), out result[i])) return false; values = result; return true; }
    private static GameObject FindObject(string name) { return string.IsNullOrWhiteSpace(name) ? null : GameObject.Find(name); }
    private static Type FindComponentType(string name) { string requested = name.Trim(); foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) { try { Type exact = assembly.GetType(requested); if (exact != null) return exact; foreach (Type candidate in assembly.GetTypes()) if (candidate.Name.Equals(requested, StringComparison.OrdinalIgnoreCase)) return candidate; } catch (ReflectionTypeLoadException) { } } return null; }
    private static bool TryAddComponentByName(GameObject go, string typeName) { Type type = FindComponentType(typeName); if (!ValidComponent(type)) return false; if (go.GetComponent(type) != null) return true; Undo.AddComponent(go, type); return true; }
    private static bool ValidComponent(Type type) { return type != null && typeof(Component).IsAssignableFrom(type) && !type.IsAbstract; }
    private static bool Args(string text, int count, out string[] values) { values = text.Split(':'); if (values.Length != count) { values = null; return false; } for (int i = 0; i < values.Length; i++) values[i] = values[i].Trim(); return true; }
    private static bool TryFloat(string value, out float result) { return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result); }
    private static void WriteResult(string projectPath, string id, string command, CommandResult result) { string json = "{\n  \"id\": \"" + Escape(id) + "\",\n  \"command\": \"" + Escape(command) + "\",\n  \"success\": " + result.Success.ToString().ToLowerInvariant() + ",\n  \"message\": \"" + Escape(result.Message) + "\",\n  \"exception\": \"" + Escape(result.Exception) + "\",\n  \"createdObjects\": [" + QuoteList(result.CreatedObjects) + "],\n  \"deletedObjects\": [" + QuoteList(result.DeletedObjects) + "],\n  \"errors\": [" + QuoteErrors(result.Errors) + "]\n}"; File.WriteAllText(Path.Combine(projectPath, ResultFileName), json); string resultsPath = Path.Combine(projectPath, ResultsDirectoryName); Directory.CreateDirectory(resultsPath); File.WriteAllText(Path.Combine(resultsPath, id + ".json"), json); }
    private static string QuoteList(List<string> values) { List<string> result = new List<string>(); foreach (string value in values) result.Add("\"" + Escape(value) + "\""); return string.Join(",", result.ToArray()); }
    private static string QuoteErrors(List<LogRecord> values) { List<string> result = new List<string>(); foreach (LogRecord value in values) result.Add("{\"type\":\"" + Escape(value.Type) + "\",\"message\":\"" + Escape(value.Message) + "\",\"stackTrace\":\"" + Escape(value.StackTrace) + "\"}"); return string.Join(",", result.ToArray()); }
    private static string Escape(string value) { return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n"); }
}