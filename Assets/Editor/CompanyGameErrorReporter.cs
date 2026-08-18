using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Captures Unity errors/exceptions and writes the latest diagnostic to
/// results/error.json so the automated Git workflow can publish it.
/// </summary>
[InitializeOnLoad]
public static class CompanyGameErrorReporter
{
    private const string ResultsDirectory = "results";
    private const string ErrorFileName = "error.json";
    private static string lastSignature = string.Empty;
    private static double lastWriteTime;
    private static bool writing;

    static CompanyGameErrorReporter()
    {
        Application.logMessageReceivedThreaded -= OnLogMessage;
        Application.logMessageReceivedThreaded += OnLogMessage;
    }

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;

        if (writing)
            return;

        string safeCondition = condition ?? string.Empty;
        string safeStackTrace = stackTrace ?? string.Empty;
        string signature = safeCondition + "\n" + safeStackTrace;
        double now = EditorApplication.timeSinceStartup;

        // Avoid repeatedly writing the exact same error from Unity's repeated log callbacks.
        if (signature == lastSignature && now - lastWriteTime < 2.0)
            return;

        lastSignature = signature;
        lastWriteTime = now;

        // File IO is performed on the main thread because Unity editor callbacks can arrive
        // from a worker thread when using logMessageReceivedThreaded.
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
                return;

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
            Debug.Log("[Company Game] Unity error captured: " + filePath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Company Game] Failed to write error.json: " + ex.Message);
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
