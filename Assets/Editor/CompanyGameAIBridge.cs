using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CompanyGame.AI
{
    /// <summary>
    /// Local GitHub-queue bridge for AI-driven Unity Editor work.
    ///
    /// It intentionally uses a separate ai_command.json queue so the existing
    /// CompanyGameCommandAgent remains untouched. A local git pull brings the
    /// command into the project; this bridge executes it on Unity's main thread
    /// and writes results/ai_result.json.
    /// </summary>
    [InitializeOnLoad]
    public static class CompanyGameAIBridge
    {
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly string CommandPath = Path.Combine(ProjectRoot, "ai_command.json");
        private static readonly string ProcessingPath = Path.Combine(ProjectRoot, "ai_command.processing.json");
        private static readonly string ResultPath = Path.Combine(ProjectRoot, "results", "ai_result.json");
        private static bool processing;
        private static readonly List<string> ConsoleBuffer = new List<string>();
        private const int MaxConsoleEntries = 100;

        static CompanyGameAIBridge()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            string line = "[" + type + "] " + condition;
            lock (ConsoleBuffer)
            {
                ConsoleBuffer.Add(line);
                if (ConsoleBuffer.Count > MaxConsoleEntries)
                    ConsoleBuffer.RemoveAt(0);
            }
        }

        private static void Tick()
        {
            if (processing || !File.Exists(CommandPath)) return;
            processing = true;

            try
            {
                if (!TryMove(CommandPath, ProcessingPath))
                {
                    processing = false;
                    return;
                }

                string raw = File.ReadAllText(ProcessingPath, Encoding.UTF8).Trim().TrimStart('\uFEFF');
                if (string.IsNullOrWhiteSpace(raw))
                {
                    SafeDelete(ProcessingPath);
                    processing = false;
                    return;
                }

                ExecuteAndWrite(raw);
            }
            catch (Exception ex)
            {
                WriteResult(rawCommand: "", success: false, message: "Bridge exception: " + ex.Message, exception: ex.ToString(), data: null);
                SafeDelete(ProcessingPath);
                processing = false;
            }
        }

        private static bool TryMove(string source, string destination)
        {
            if (File.Exists(destination)) SafeDelete(destination);
            try
            {
                File.Move(source, destination);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static void ExecuteAndWrite(string raw)
        {
            bool success = false;
            string message = "";
            string exception = "";
            string data = null;

            try
            {
                Command command = Parse(raw);
                switch (command.Name)
                {
                    case "PING":
                        success = true; message = "CompanyGame AI Bridge is running."; break;
                    case "CREATE_GAMEOBJECT":
                        data = CreateGameObject(command.Arguments); success = true; message = "GameObject created."; break;
                    case "DELETE_GAMEOBJECT":
                        DeleteGameObject(command.Arguments); success = true; message = "GameObject deleted."; break;
                    case "RENAME_GAMEOBJECT":
                        RenameGameObject(command.Arguments); success = true; message = "GameObject renamed."; break;
                    case "SET_ACTIVE":
                        SetActive(command.Arguments); success = true; message = "GameObject active state changed."; break;
                    case "SET_POSITION":
                        SetPosition(command.Arguments); success = true; message = "Position changed."; break;
                    case "SET_ROTATION":
                        SetRotation(command.Arguments); success = true; message = "Rotation changed."; break;
                    case "SET_SCALE":
                        SetScale(command.Arguments); success = true; message = "Scale changed."; break;
                    case "ADD_COMPONENT":
                        AddComponent(command.Arguments); success = true; message = "Component added."; break;
                    case "REMOVE_COMPONENT":
                        RemoveComponent(command.Arguments); success = true; message = "Component removed."; break;
                    case "GET_HIERARCHY":
                        data = GetHierarchy(); success = true; message = "Hierarchy returned."; break;
                    case "GET_OBJECT_INFO":
                        data = GetObjectInfo(command.Arguments); success = true; message = "Object information returned."; break;
                    case "GET_CONSOLE":
                        data = GetConsole(); success = true; message = "Console buffer returned."; break;
                    case "PLAY":
                        EditorApplication.isPlaying = true; success = true; message = "Play Mode requested."; break;
                    case "STOP":
                        EditorApplication.isPlaying = false; success = true; message = "Play Mode stop requested."; break;
                    case "SAVE_SCENE":
                        AssetDatabase.SaveAssets(); EditorSceneManagerProxy.SaveOpenScenes(); success = true; message = "Open scenes saved."; break;
                    default:
                        throw new InvalidOperationException("Unknown AI bridge command: " + command.Name);
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                exception = ex.ToString();
            }

            WriteResult(raw, success, message, exception, data);
            SafeDelete(ProcessingPath);
            processing = false;
            AssetDatabase.Refresh();
        }

        private sealed class Command
        {
            public string Name;
            public string Arguments;
        }

        [Serializable]
        private sealed class JsonCommand
        {
            public string command;
            public string args;
        }

        private static Command Parse(string raw)
        {
            if (raw.StartsWith("{"))
            {
                JsonCommand json = JsonUtility.FromJson<JsonCommand>(raw);
                if (json == null || string.IsNullOrWhiteSpace(json.command))
                    throw new InvalidOperationException("JSON command requires 'command'.");
                return new Command { Name = json.command.Trim().ToUpperInvariant(), Arguments = json.args ?? "" };
            }

            string[] parts = raw.Split(new[] { ':' }, 2);
            return new Command
            {
                Name = parts[0].Trim().ToUpperInvariant(),
                Arguments = parts.Length > 1 ? parts[1].Trim() : ""
            };
        }

        private static GameObject FindObject(string selector)
        {
            GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (GameObject go in objects)
                if (go != null && string.Equals(go.name, selector, StringComparison.Ordinal)) return go;
            return null;
        }

        private static GameObject RequireObject(string selector)
        {
            GameObject go = FindObject(selector.Trim());
            if (go == null) throw new InvalidOperationException("GameObject not found: " + selector);
            return go;
        }

        private static string CreateGameObject(string args)
        {
            string name = string.IsNullOrWhiteSpace(args) ? "AIObject" : args.Trim();
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "AI Create GameObject");
            Selection.activeGameObject = go;
            EditorUtility.SetDirty(go);
            return "{\"name\":\"" + Escape(name) + "\"}";
        }

        private static void DeleteGameObject(string args)
        {
            GameObject go = RequireObject(args);
            Undo.DestroyObjectImmediate(go);
        }

        private static void RenameGameObject(string args)
        {
            string[] p = args.Split(new[] { ':' }, 2);
            if (p.Length != 2) throw new InvalidOperationException("RENAME_GAMEOBJECT requires oldName:newName.");
            GameObject go = RequireObject(p[0]);
            string newName = p[1].Trim();
            if (string.IsNullOrWhiteSpace(newName)) throw new InvalidOperationException("New name cannot be empty.");
            Undo.RecordObject(go, "AI Rename GameObject");
            go.name = newName;
            EditorUtility.SetDirty(go);
        }

        private static void SetActive(string args)
        {
            string[] p = args.Split(new[] { ':' }, 2);
            if (p.Length != 2) throw new InvalidOperationException("SET_ACTIVE requires name:true/false.");
            GameObject go = RequireObject(p[0]);
            bool value;
            if (!bool.TryParse(p[1], out value)) throw new InvalidOperationException("Active value must be true or false.");
            Undo.RecordObject(go, "AI Set Active");
            go.SetActive(value);
        }

        private static void SetPosition(string args) { SetTransform(args, 0); }
        private static void SetRotation(string args) { SetTransform(args, 1); }
        private static void SetScale(string args) { SetTransform(args, 2); }

        private static void SetTransform(string args, int mode)
        {
            string[] p = args.Split(':');
            if (p.Length != 4) throw new InvalidOperationException("Transform command requires name:x:y:z.");
            GameObject go = RequireObject(p[0]);
            float x = ParseFloat(p[1]), y = ParseFloat(p[2]), z = ParseFloat(p[3]);
            Undo.RecordObject(go.transform, "AI Transform");
            Vector3 v = new Vector3(x, y, z);
            if (mode == 0) go.transform.position = v;
            else if (mode == 1) go.transform.eulerAngles = v;
            else go.transform.localScale = v;
            EditorUtility.SetDirty(go);
        }

        private static float ParseFloat(string value)
        {
            float result;
            if (!float.TryParse(value.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result))
                throw new InvalidOperationException("Invalid number: " + value);
            return result;
        }

        private static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName, false);
                if (type != null) return type;
                Type[] types;
                try { types = assembly.GetTypes(); } catch { continue; }
                foreach (Type candidate in types)
                    if (string.Equals(candidate.Name, typeName, StringComparison.Ordinal)) return candidate;
            }
            return null;
        }

        private static void AddComponent(string args)
        {
            string[] p = args.Split(new[] { ':' }, 2);
            if (p.Length != 2) throw new InvalidOperationException("ADD_COMPONENT requires name:ComponentType.");
            GameObject go = RequireObject(p[0]);
            Type type = FindType(p[1].Trim());
            if (type == null || !typeof(Component).IsAssignableFrom(type)) throw new InvalidOperationException("Component type not found: " + p[1]);
            Undo.AddComponent(go, type);
            EditorUtility.SetDirty(go);
        }

        private static void RemoveComponent(string args)
        {
            string[] p = args.Split(new[] { ':' }, 2);
            if (p.Length != 2) throw new InvalidOperationException("REMOVE_COMPONENT requires name:ComponentType.");
            GameObject go = RequireObject(p[0]);
            Type type = FindType(p[1].Trim());
            if (type == null || !typeof(Component).IsAssignableFrom(type)) throw new InvalidOperationException("Component type not found: " + p[1]);
            Component component = go.GetComponent(type);
            if (component == null) throw new InvalidOperationException("Component not found on object: " + p[1]);
            if (component is Transform) throw new InvalidOperationException("Transform cannot be removed.");
            Undo.DestroyObjectImmediate(component);
        }

        private static string GetHierarchy()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            bool first = true;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (!first) sb.Append(",");
                    AppendHierarchyNode(sb, root);
                    first = false;
                }
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static void AppendHierarchyNode(StringBuilder sb, GameObject go)
        {
            sb.Append("{\"name\":\"").Append(Escape(go.name)).Append("\",\"active\":").Append(go.activeSelf ? "true" : "false").Append(",\"children\":[");
            for (int i = 0; i < go.transform.childCount; i++)
            {
                if (i > 0) sb.Append(",");
                AppendHierarchyNode(sb, go.transform.GetChild(i).gameObject);
            }
            sb.Append("]}");
        }

        private static string GetObjectInfo(string args)
        {
            GameObject go = RequireObject(args);
            Component[] components = go.GetComponents<Component>();
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"name\":\"").Append(Escape(go.name)).Append("\",\"active\":").Append(go.activeSelf ? "true" : "false");
            sb.Append(",\"position\":[").Append(go.transform.position.x.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",").Append(go.transform.position.y.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",").Append(go.transform.position.z.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("]");
            sb.Append(",\"components\":[");
            for (int i = 0; i < components.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("\"").Append(Escape(components[i] == null ? "MissingComponent" : components[i].GetType().Name)).Append("\"");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string GetConsole()
        {
            lock (ConsoleBuffer)
            {
                StringBuilder sb = new StringBuilder("[");
                for (int i = 0; i < ConsoleBuffer.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append("\"").Append(Escape(ConsoleBuffer[i])).Append("\"");
                }
                sb.Append("]");
                return sb.ToString();
            }
        }

        private static void WriteResult(string rawCommand, bool success, string message, string exception, string data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"success\": ").Append(success ? "true" : "false").Append(",\n");
            sb.Append("  \"command\": \"").Append(Escape(rawCommand)).Append("\",\n");
            sb.Append("  \"message\": \"").Append(Escape(message ?? "")).Append("\",\n");
            sb.Append("  \"exception\": \"").Append(Escape(exception ?? "")).Append("\",");
            if (data != null) sb.Append("\n  \"data\": ").Append(data).Append("\n");
            else sb.Append("\n  \"data\": null\n");
            sb.Append("}\n");
            File.WriteAllText(ResultPath, sb.ToString(), new UTF8Encoding(false));
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static class EditorSceneManagerProxy
        {
            public static void SaveOpenScenes()
            {
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            }
        }
    }
}
