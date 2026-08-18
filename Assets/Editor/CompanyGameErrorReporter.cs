using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Captures Unity Editor log errors/exceptions/asserts and writes an AI-readable
/// report to results/error.json. Also captures unhandled log messages emitted from
/// worker threads. Compilation errors are additionally collected through the
/// Unity Console log pipeline when Unity emits them as log messages.
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
    private static int queuedWrites;

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
        Application.logMessageReceived -= OnLogMessage;
        Application.logMessageReceivedThreaded -= OnLogMessageThreaded;
        Application.logMessageReceived += OnLogMessage;
        Application.logMessageReceivedThreaded += OnLogMessageThreaded;
        Debug.Log("[Company Game] Error Reporter initialized.");
    }

    [MenuItem("Tools/Company Game/Test Error Reporter")]
    private static void TestErrorReporter()
    {
        Capture("CompanyGameErrorReporter test error.",
            "Test Error Reporter menu command -> CompanyGameErrorReporter.TestErrorReporter()",
            LogType.Error);
    }

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        HandleLog(condition, stackTrace, type);
    }

    private static void OnLogMessageThreaded(string condition, string stackTrace, LogType type)
    {
        HandleLog(condition, stackTrace, type);
    }

    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;

        string safeCondition = condition ?? string.Empty;
        string safeStackTrace = stackTrace ?? string.Empty;
        string signature = type + "\n" + safeCondition + "\n" + safeStackTrace;
        double now = EditorApplication.timeSinceStartup;

        // Prevent the same Unity error from flooding error.json repeatedly.
        if (signature == lastSignature && now - lastWriteTime < 2.0)
            return;

        lastSignature = signature;
        lastWriteTime = now;

        // Never touch UnityEditor APIs or files directly from a worker thread.
        // Queue the actual write onto Unity's main editor thread.
        queuedWrites++;
        EditorApplication.delayCall += () =>
        {
            queuedWrites = Math.Max(0, queuedWrites - 1);
            Capture(safeCondition, safeStackTrace, type);
        };
    }

    private static void Capture(string condition, string stackTrace, LogType type)
    {
        WriteError(condition, stackTrace, type);
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

            // Atomic replacement reduces the chance of auto-push reading a partially
            // written JSON file.
            string tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));

            if (File.Exists(filePath))
                File.Delete(filePath);

            File.Move(tempPath, filePath);

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
