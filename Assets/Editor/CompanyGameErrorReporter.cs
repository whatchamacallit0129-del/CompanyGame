using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Captures Unity Editor errors and exceptions and writes them to results/error.json.
/// Includes a menu command that verifies the reporter itself is alive.
/// </summary>
[InitializeOnLoad]
public static class CompanyGameErrorReporter
{
    private const string ResultsDirectory = "results";
    private const string ErrorFileName = "error.json";
    private static bool initialized;
    private static bool writing;
    private static string lastSignature = string.Empty;
    private static double lastWriteTime;

    static CompanyGameErrorReporter()
    {
        Initialize();
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        Initialize();
    }

    private static void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        Application.logMessageReceivedThreaded -= OnLogMessage;
        Application.logMessageReceivedThreaded += OnLogMessage;
        Debug.Log("[Company Game] Error Reporter initialized.");
    }

    [MenuItem("Tools/Company Game/Test Error Reporter")]
    private static void TestErrorReporter()
    {
        // Write a deterministic diagnostic before emitting the intentional error.
        WriteError(
            "CompanyGameErrorReporter test error. This error was intentionally generated to verify error capture.",
            "Test Error Reporter menu command -> CompanyGameErrorReporter.TestErrorReporter()",
            LogType.Error);

        Debug.LogError("[Company Game] TEST ERROR: Error Reporter capture test.");
    }

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;

        string safeCondition = condition ?? string.Empty;
        string safeStackTrace = stackTrace ?? string.Empty;
        string signature = type + "\n" + safeCondition + "\n" + safeStackTrace;
        double now = EditorApplication.timeSinceStartup;

        if (signature == lastSignature && now - lastWriteTime < 2.0)
            return;

        lastSignature = signature;
        lastWriteTime = now;

        EditorApplication.delayCall += () => WriteError(safeCondition, safeStackTrace, type);
    }

    private static void WriteError(string condition, string stackTrace, LogType type)
    {
        if (writing)
            return;

        try
        {
            writing = true;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Could not determine Unity project root.");

            string resultsPath = Path.Combine(projectRoot, ResultsDirectory);
            Directory.CreateDirectory(resultsPath);

            string filePath = Path.Combine(resultsPath, ErrorFileName);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            string json = "{\n" +
                          "  \"success\": false,\n" +
                          "  \"type\": \"" + Escape(type.ToString()) + "\",\n" +
                          "  \"timestamp\": \"" + Escape(timestamp) + "\",\n" +
                          "  \"message\": \"" + Escape(condition) + "\",\n" +
                          "  \"stackTrace\": \"" + Escape(stackTrace) + "\"\n" +
                          "}\n";

            File.WriteAllText(filePath, json, new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("[Company Game] Error captured: " + filePath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Company Game] Failed to write error.json: " + ex);
        }
        finally
        {
            writing = false;
        }
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}
