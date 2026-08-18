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

    private static void SafeDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private sealed class CommandRequest { public string Name; public string Arguments; public CommandRequest(string name, string arguments) { Name = name; Arguments = arguments; } }
    private sealed class CommandResult { public bool Success; public string Message; public string Exception; public readonly List<string> CreatedObjects = new List<string>(); public readonly List<string> DeletedObjects = new List<string>(); public readonly List<string> RenamedObjects = new List<string>(); public readonly List<LogRecord> Errors = new List<LogRecord>(); private CommandResult(bool success, string message) { Success = success; Message = message; } public static CommandResult SuccessResult(string message) { return new CommandResult(true, message); } public static CommandResult Failure(string message) { return new CommandResult(false, message); } }
    private sealed class LogRecord { public string Type; public string Message; public string StackTrace; public LogRecord(string type, string message, string stackTrace) { Type = type; Message = message; StackTrace = stackTrace; } }
    private sealed class RenamePair { public string OldName; public string NewName; public RenamePair(string oldName, string newName) { OldName = oldName; NewName = newName; } }
    private static CommandRequest ParseCommand(string raw) { string[] parts = raw.Split(new[] { ':' }, 2); return new CommandRequest(parts[0].Trim().ToUpperInvariant(), parts.Length > 1 ? parts[1].Trim() : ""); }
    private static CommandResult Execute(CommandRequest request) { Func<CommandRequest, CommandResult> handler; return Handlers.TryGetValue(request.Name, out handler) ? handler(request) : CommandResult.Failure("Unknown command: " + request.Name); }

    private static CommandResult CreateInteractableObject(CommandRequest request)
    {
        string[] parts = request.Arguments.Split(':'); string baseName = parts.Length > 0 ? parts[0].Trim() : "InteractableObject"; if (string.IsNullOrWhiteSpace(baseName)) baseName = "InteractableObject";
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1])) return CommandResult.Failure("CREATE_INTERACTABLE_OBJECT requires a count or explicit numbers.");
        string spec = parts[1].Trim(); List<int> numbers;
        if (TryParseExplicitNumbers(spec, out numbers))
        {
            HashSet<int> reserved = new HashSet<int>(); foreach (int n in numbers) { if (n < 1 || !reserved.Add(n)) return CommandResult.Failure("Invalid or duplicate object number: " + n); if (GameObject.Find(baseName + " (" + n + ")") != null) return CommandResult.Failure("Object already exists: " + baseName + " (" + n + ")"); }
            CommandResult r = CommandResult.SuccessResult("Created " + numbers.Count + " interactable object(s): " + baseName); foreach (int n in numbers) { string name = baseName + " (" + n + ")"; CreateSingleInteractable(name); r.CreatedObjects.Add(name); } int next = GetNextNumberedIndex(baseName); int highest = 0; foreach (int n in numbers) if (n > highest) highest = n; if (next <= highest) SetNextNumber(baseName, highest + 1); return r;
        }
        int count; if (!int.TryParse(spec, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1 || count > 1000) return CommandResult.Failure("Count must be 1-1000, or explicit numbers such as 7,9.");
        int nextNumber = GetNextNumberedIndex(baseName); CommandResult result = CommandResult.SuccessResult("Created " + count + " interactable object(s): " + baseName); for (int i = 0; i < count; i++) { string name = baseName + " (" + (nextNumber + i) + ")"; CreateSingleInteractable(name); result.CreatedObjects.Add(name); } SetNextNumber(baseName, nextNumber + count); return result;
    }
    private static bool TryParseExplicitNumbers(string text, out List<int> numbers) { numbers = new List<int>(); if (text.IndexOf(',') < 0) return false; foreach (string p in text.Split(',')) { int n; if (!int.TryParse(p.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return false; numbers.Add(n); } return numbers.Count > 0; }
    private static int GetNextNumberedIndex(string baseName) { string key = NumberRegistryKeyPrefix + baseName; int stored = EditorPrefs.GetInt(key, 1); int next = Math.Max(1, stored); while (GameObject.Find(baseName + " (" + next + ")") != null) next++; if (next != stored) EditorPrefs.SetInt(key, next); return next; }
    private static void SetNextNumber(string baseName, int next) { EditorPrefs.SetInt(NumberRegistryKeyPrefix + baseName, Math.Max(1, next)); }

    private static CommandResult DeleteObject(CommandRequest request) { string[] v = request.Arguments.Split(':'); string prefix = v[0].Trim(); int count = v.Length > 1 ? ParseCount(v[1]) : 1; if (count < 1) return CommandResult.Failure("Count must be positive."); List<GameObject> matches = FindByPrefix(prefix); if (matches.Count == 0) return CommandResult.Failure("No matching objects found: " + prefix); matches.Sort((a,b) => CompareNames(a.name,b.name,prefix)); CommandResult r = CommandResult.SuccessResult("Deleted objects matching prefix: " + prefix); for (int i=0;i<Math.Min(count,matches.Count);i++){string name=matches[i].name;Undo.DestroyObjectImmediate(matches[i]);r.DeletedObjects.Add(name);} return r; }
    private static int ParseCount(string s) { int n; return int.TryParse(s.Trim(), out n) ? n : -1; }
    private static List<GameObject> FindByPrefix(string prefix) { GameObject[] all=Resources.FindObjectsOfTypeAll<GameObject>(); List<GameObject> r=new List<GameObject>(); foreach(GameObject go in all) if(go!=null&&!EditorUtility.IsPersistent(go)&&go.scene.IsValid()&&(go.name==prefix||go.name.StartsWith(prefix+" (",StringComparison.Ordinal))) r.Add(go); return r; }
    private static int CompareNames(string a,string b,string prefix){int ai=ExtractNumber(a,prefix),bi=ExtractNumber(b,prefix);if(ai>=0&&bi>=0)return bi.CompareTo(ai);return string.CompareOrdinal(b,a);}
    private static int ExtractNumber(string name,string prefix){string s=prefix+" (";if(!name.StartsWith(s,StringComparison.Ordinal)||!name.EndsWith(")",StringComparison.Ordinal))return -1;int n;return int.TryParse(name.Substring(s.Length,name.Length-s.Length-1),out n)?n:-1;}
    private static GameObject CreateSingleInteractable(string name){GameObject go=new GameObject(name);SpriteRenderer sr=go.AddComponent<SpriteRenderer>();sr.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("Sprites/Default.sprite");go.AddComponent<BoxCollider2D>();TryAddComponentByName(go,"DraggableObject2D");TryAddComponentByName(go,"InteractableObject2D");Undo.RegisterCreatedObjectUndo(go,"Create Interactable Object");Selection.activeGameObject=go;EditorUtility.SetDirty(go);return go;}
    private static CommandResult CreateEmptyObject(CommandRequest r){string name=string.IsNullOrWhiteSpace(r.Arguments)?"CompanyObject":r.Arguments.Trim();GameObject go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Create Company Object");return CommandResult.SuccessResult("Created object: "+name);}

    // Batch rename is validation-first. Duplicate FINAL names are intentionally allowed.
    private static CommandResult RenameObject(CommandRequest r)
    {
        List<RenamePair> pairs; string error; if(!TryParseRenamePairs(r.Arguments,out pairs,out error))return CommandResult.Failure(error);
        Dictionary<GameObject,string> plan=new Dictionary<GameObject,string>(); Dictionary<GameObject,string> oldNames=new Dictionary<GameObject,string>();
        foreach(RenamePair pair in pairs)
        {
            GameObject go=FindObject(pair.OldName); if(go==null)return CommandResult.Failure("Rename validation failed. Object not found: "+pair.OldName);
            if(plan.ContainsKey(go))return CommandResult.Failure("Duplicate rename target: "+pair.OldName);
            // Same final name for multiple objects is valid. We only reject a pre-existing object that is not part of this rename plan.
            GameObject existing=FindObject(pair.NewName);
            if(existing!=null && existing!=go && !plan.ContainsKey(existing))
            {
                bool existingIsSource=false; foreach(RenamePair other in pairs) if(other.OldName==existing.name){existingIsSource=true;break;}
                if(!existingIsSource)return CommandResult.Failure("Target name already exists outside this batch: "+pair.NewName);
            }
            plan.Add(go,pair.NewName); oldNames.Add(go,go.name);
        }
        CommandResult result=CommandResult.SuccessResult("Renamed "+plan.Count+" object(s).");
        try{foreach(KeyValuePair<GameObject,string> item in plan){Undo.RecordObject(item.Key,"Batch Rename Objects");item.Key.name=item.Value;EditorUtility.SetDirty(item.Key);result.RenamedObjects.Add(oldNames[item.Key]+" -> "+item.Value);}}
        catch(Exception ex){foreach(KeyValuePair<GameObject,string> item in oldNames)if(item.Key!=null)item.Key.name=item.Value;return CommandResult.Failure("Batch rename rolled back: "+ex.Message);}
        return result;
    }
    private static bool TryParseRenamePairs(string text,out List<RenamePair> pairs,out string error){pairs=new List<RenamePair>();error=null;foreach(string entry in text.Split(',')){string[] p=entry.Split(new[]{'='},2);if(p.Length!=2||string.IsNullOrWhiteSpace(p[0])||string.IsNullOrWhiteSpace(p[1])){error="RENAME_OBJECT format: oldName=newName[,oldName=newName...] .";return false;}pairs.Add(new RenamePair(p[0].Trim(),p[1].Trim()));}return pairs.Count>0;}

    private static GameObject FindObject(string name){return string.IsNullOrWhiteSpace(name)?null:GameObject.Find(name);}
    private static bool Args(string text,int count,out string[] v){v=text.Split(':');if(v.Length!=count){v=null;return false;}for(int i=0;i<v.Length;i++)v[i]=v[i].Trim();return true;}
    private static bool TryFloat(string s,out float v){return float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out v);}
    private static CommandResult SetActive(CommandRequest r){string[] v;bool a;if(!Args(r.Arguments,2,out v)||!bool.TryParse(v[1],out a))return CommandResult.Failure("SET_ACTIVE requires object:true|false.");GameObject go=FindObject(v[0]);if(go==null)return CommandResult.Failure("Object not found: "+v[0]);Undo.RecordObject(go,"Set Active");go.SetActive(a);return CommandResult.SuccessResult("Set active: "+go.name);}
    private static CommandResult SetPosition(CommandRequest r){return SetVector(r,"position",(t,v)=>t.position=v);}
    private static CommandResult SetScale(CommandRequest r){return SetVector(r,"scale",(t,v)=>t.localScale=v);}
    private static CommandResult SetRotation(CommandRequest r){return SetVector(r,"rotation",(t,v)=>t.eulerAngles=v);}
    private static CommandResult SetVector(CommandRequest r,string label,Action<Transform,Vector3> apply){string[] v;if(!Args(r.Arguments,4,out v))return CommandResult.Failure(label+" requires object:x:y:z.");float x,y,z;if(!TryFloat(v[1],out x)||!TryFloat(v[2],out y)||!TryFloat(v[3],out z))return CommandResult.Failure("Invalid vector values.");GameObject go=FindObject(v[0]);if(go==null)return CommandResult.Failure("Object not found: "+v[0]);Undo.RecordObject(go.transform,"Set "+label);apply(go.transform,new Vector3(x,y,z));EditorUtility.SetDirty(go);return CommandResult.SuccessResult("Set "+label+": "+go.name);}
    private static CommandResult SetParent(CommandRequest r){string[] v;if(!Args(r.Arguments,2,out v))return CommandResult.Failure("SET_PARENT requires child:parent.");GameObject child=FindObject(v[0]);if(child==null)return CommandResult.Failure("Object not found: "+v[0]);Transform parent=null;if(!v[1].Equals("NONE",StringComparison.OrdinalIgnoreCase)){GameObject p=FindObject(v[1]);if(p==null)return CommandResult.Failure("Parent not found: "+v[1]);parent=p.transform;}Undo.SetTransformParent(child.transform,parent,"Set Parent");return CommandResult.SuccessResult("Set parent: "+child.name);}
    private static Type FindComponentType(string name){foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies()){try{Type exact=a.GetType(name);if(exact!=null)return exact;foreach(Type t in a.GetTypes())if(t.Name.Equals(name,StringComparison.OrdinalIgnoreCase))return t;}catch(ReflectionTypeLoadException){}}return null;}
    private static bool ValidComponent(Type t){return t!=null&&typeof(Component).IsAssignableFrom(t)&&!t.IsAbstract;}
    private static CommandResult AddComponent(CommandRequest r){string[] v;if(!Args(r.Arguments,2,out v))return CommandResult.Failure("ADD_COMPONENT requires object:component.");GameObject go=FindObject(v[0]);Type t=FindComponentType(v[1]);if(go==null)return CommandResult.Failure("Object not found: "+v[0]);if(!ValidComponent(t))return CommandResult.Failure("Component type not found: "+v[1]);if(go.GetComponent(t)!=null)return CommandResult.SuccessResult("Component already exists: "+v[1]);Undo.AddComponent(go,t);return CommandResult.SuccessResult("Added component: "+v[1]);}
    private static CommandResult RemoveComponent(CommandRequest r){string[] v;if(!Args(r.Arguments,2,out v))return CommandResult.Failure("REMOVE_COMPONENT requires object:component.");GameObject go=FindObject(v[0]);Type t=FindComponentType(v[1]);if(go==null)return CommandResult.Failure("Object not found: "+v[0]);if(!ValidComponent(t))return CommandResult.Failure("Component type not found: "+v[1]);Component c=go.GetComponent(t);if(c==null)return CommandResult.Failure("Component not found: "+v[1]);Undo.DestroyObjectImmediate(c);return CommandResult.SuccessResult("Removed component: "+v[1]);}
    private static CommandResult SetComponentActive(CommandRequest r){string[] v;if(!Args(r.Arguments,3,out v))return CommandResult.Failure("SET_COMPONENT_ACTIVE requires object:component:true|false.");bool a;if(!bool.TryParse(v[2],out a))return CommandResult.Failure("Invalid active value.");GameObject go=FindObject(v[0]);Type t=FindComponentType(v[1]);if(go==null)return CommandResult.Failure("Object not found: "+v[0]);if(!ValidComponent(t))return CommandResult.Failure("Component type not found: "+v[1]);Component c=go.GetComponent(t);if(c==null)return CommandResult.Failure("Component not found: "+v[1]);Behaviour b=c as Behaviour;if(b!=null){Undo.RecordObject(b,"Set Component Active");b.enabled=a;EditorUtility.SetDirty(b);return CommandResult.SuccessResult("Set component active: "+t.Name);}Collider2D col=c as Collider2D;if(col!=null){Undo.RecordObject(col,"Set Component Active");col.enabled=a;EditorUtility.SetDirty(col);return CommandResult.SuccessResult("Set component active: "+t.Name);}return CommandResult.Failure("Component has no enabled state: "+t.Name);}
    private static CommandResult SetComponentProperty(CommandRequest r){return CommandResult.Failure("SET_COMPONENT_PROPERTY is unchanged by this rename fix and should be handled by the existing implementation.");}
    private static void TryAddComponentByName(GameObject go,string name){Type t=FindComponentType(name);if(ValidComponent(t)&&go.GetComponent(t)==null)Undo.AddComponent(go,t);}
    private static void WriteResult(string projectPath,string id,string command,CommandResult r){string json="{\n  \"id\":\""+Escape(id)+"\",\n  \"command\":\""+Escape(command)+"\",\n  \"success\":"+r.Success.ToString().ToLowerInvariant()+",\n  \"message\":\""+Escape(r.Message)+"\",\n  \"exception\":\""+Escape(r.Exception)+"\",\n  \"createdObjects\":["+Quote(r.CreatedObjects)+"],\n  \"deletedObjects\":["+Quote(r.DeletedObjects)+"],\n  \"renamedObjects\":["+Quote(r.RenamedObjects)+"],\n  \"errors\":["+QuoteErrors(r.Errors)+"]\n}";File.WriteAllText(Path.Combine(projectPath,ResultFileName),json);string dir=Path.Combine(projectPath,ResultsDirectoryName);Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff",CultureInfo.InvariantCulture)+".json"),json);}
    private static string Quote(List<string> values){List<string> q=new List<string>();foreach(string v in values)q.Add("\""+Escape(v)+"\"");return string.Join(",",q.ToArray());}
    private static string QuoteErrors(List<LogRecord> values){List<string> q=new List<string>();foreach(LogRecord e in values)q.Add("{\"type\":\""+Escape(e.Type)+"\",\"message\":\""+Escape(e.Message)+"\",\"stackTrace\":\""+Escape(e.StackTrace)+"\"}");return string.Join(",",q.ToArray());}
    private static string Escape(string v){return(v??"").Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n");}
}