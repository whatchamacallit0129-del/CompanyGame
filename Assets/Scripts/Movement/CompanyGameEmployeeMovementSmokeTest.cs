using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class CompanyGameEmployeeMovementSmokeTest : MonoBehaviour
{
    [SerializeField] private float timeoutSeconds = 12f;
    [SerializeField] private float destinationTolerance = 0.1f;

    private CompanyGameEmployeeMovement employee;
    private Vector3 destination;
    private float elapsed;
    private int startNodeCount;
    private int pathNodeCount;
    private bool started;

    public void Configure(CompanyGameEmployeeMovement targetEmployee, Vector3 targetDestination, int expectedPathNodeCount)
    {
        employee = targetEmployee;
        destination = targetDestination;
        pathNodeCount = expectedPathNodeCount;
    }

    private void Start()
    {
        startNodeCount = CompanyGameNavigationGraph.Instance.Nodes.Count;
        if (employee == null)
        {
            Fail("Employee movement component was not created.");
            return;
        }

        started = employee.MoveTo(destination);
        if (!started)
        {
            Fail("Employee could not build a reachable path to the destination.");
            return;
        }

        pathNodeCount = employee.CurrentPath != null ? employee.CurrentPath.Nodes.Count : 0;
        Debug.Log("[Company Game] Movement smoke test path accepted. Nodes=" + pathNodeCount);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (!started || employee == null) return;

        if (employee.CurrentPath != null && employee.CurrentPath.IsValid)
            pathNodeCount = employee.CurrentPath.Nodes.Count;

        if (!employee.IsMoving && Vector3.Distance(employee.transform.position, destination) <= destinationTolerance)
        {
            WriteResult(true, "Employee followed the navigation path and reached the destination.");
            Destroy(gameObject);
            return;
        }

        if (elapsed >= timeoutSeconds)
            Fail("Employee did not reach the destination before the smoke-test timeout.");
    }

    private void Fail(string message)
    {
        WriteResult(false, message);
        Destroy(gameObject);
    }

    private void WriteResult(bool success, string message)
    {
        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;
            string resultsDirectory = Path.Combine(projectRoot, "results");
            Directory.CreateDirectory(resultsDirectory);
            string path = Path.Combine(resultsDirectory, "result.json");
            string json = "{\n" +
                          "  \"success\": " + (success ? "true" : "false") + ",\n" +
                          "  \"test\": \"employee-node-movement\",\n" +
                          "  \"timestamp\": \"" + Escape(DateTime.Now.ToString("o")) + "\",\n" +
                          "  \"message\": \"" + Escape(message) + "\",\n" +
                          "  \"nodeCount\": " + startNodeCount.ToString(CultureInfo.InvariantCulture) + ",\n" +
                          "  \"pathNodeCount\": " + pathNodeCount.ToString(CultureInfo.InvariantCulture) + ",\n" +
                          "  \"destination\": { \"x\": " + destination.x.ToString("F4", CultureInfo.InvariantCulture) + ", \"y\": " + destination.y.ToString("F4", CultureInfo.InvariantCulture) + ", \"z\": " + destination.z.ToString("F4", CultureInfo.InvariantCulture) + " }\n" +
                          "}\n";
            string temp = path + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
            Debug.Log("[Company Game] Movement smoke test result written: " + path);
        }
        catch (Exception ex)
        {
            Debug.LogError("[Company Game] Failed to write movement result.json: " + ex);
        }
    }

    private static string Escape(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
}
