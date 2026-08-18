using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Soft-coded numbering layer for CompanyGameCommandAgent.
// This does not hard-code "직원" or any other object type.
// It replaces only the CREATE_INTERACTABLE_OBJECT handler at runtime.
[InitializeOnLoad]
public static class CompanyGameCommandAgentNumberingOverride
{
    private const string AgentTypeName = "CompanyGameCommandAgent";
    private const string HandlerName = "CREATE_INTERACTABLE_OBJECT";
    private const string RegistryFileName = "CompanyGameObjectNumberRegistry";
    private static readonly Dictionary<string, int> highWaterMarks = new Dictionary<string, int>(StringComparer.Ordinal);

    static CompanyGameCommandAgentNumberingOverride()
    {
        EditorApplication.delayCall += Install;
    }

    private static void Install()
    {
        EditorApplication.delayCall -= Install;

        Type agentType = FindType(AgentTypeName);
        if (agentType == null) return;

        FieldInfo handlersField = agentType.GetField("Handlers", BindingFlags.Static | BindingFlags.NonPublic);
        if (handlersField == null) return;

        IDictionary handlers = handlersField.GetValue(null) as IDictionary;
        if (handlers == null || !handlers.Contains(HandlerName)) return;

        object originalDelegate = handlers[HandlerName];
        Type delegateType = originalDelegate.GetType();
        MethodInfo invoke = delegateType.GetMethod("Invoke");
        if (invoke == null) return;

        ParameterExpression parameter = Expression.Parameter(invoke.GetParameters()[0].ParameterType, "request");
        MethodInfo bridge = typeof(CompanyGameCommandAgentNumberingOverride).GetMethod("HandleBridge", BindingFlags.Static | BindingFlags.NonPublic);
        Expression body = Expression.Convert(
            Expression.Call(bridge, Expression.Convert(parameter, typeof(object))),
            invoke.ReturnType);

        Delegate replacement = Expression.Lambda(delegateType, body, parameter).Compile();
        handlers[HandlerName] = replacement;
    }

    private static object HandleBridge(object request)
    {
        Type requestType = request.GetType();
        FieldInfo argumentsField = requestType.GetField("Arguments", BindingFlags.Instance | BindingFlags.Public);
        string arguments = argumentsField == null ? "" : (string)argumentsField.GetValue(request);

        string[] values = arguments.Split(':');
        string baseName = values.Length > 0 ? values[0].Trim() : "InteractableObject";
        string numberSpec = values.Length > 1 ? values[1].Trim() : "";
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "InteractableObject";

        List<int> requestedNumbers;
        if (TryParseExplicitNumbers(numberSpec, out requestedNumbers))
        {
            return CreateWithExplicitNumbers(requestType, baseName, requestedNumbers);
        }

        int count;
        if (!int.TryParse(numberSpec, out count) || count < 1 || count > 1000)
            return CreateFailure(requestType, "Count must be 1-1000, or an explicit number list such as 7,9.");

        int next = GetNextNumber(baseName);
        List<int> numbers = new List<int>();
        for (int i = 0; i < count; i++) numbers.Add(next + i);
        return CreateWithNumbers(requestType, baseName, numbers, true);
    }

    private static object CreateWithExplicitNumbers(Type requestType, string baseName, List<int> numbers)
    {
        HashSet<int> unique = new HashSet<int>();
        List<int> normalized = new List<int>();
        foreach (int number in numbers)
        {
            if (number < 1 || number > 1000000) return CreateFailure(requestType, "Object number must be between 1 and 1000000.");
            if (unique.Add(number)) normalized.Add(number);
        }
        normalized.Sort();

        // Explicit numbering is strict: do not silently create a different number.
        foreach (int number in normalized)
        {
            if (FindSceneObject(baseName + " (" + number + ")") != null)
                return CreateFailure(requestType, "Object number already exists: " + baseName + " (" + number + ")");
        }

        return CreateWithNumbers(requestType, baseName, normalized, false);
    }

    private static object CreateWithNumbers(Type requestType, string baseName, List<int> numbers, bool updateWaterMark)
    {
        Type resultType = requestType.DeclaringType.GetNestedType("CommandResult", BindingFlags.NonPublic);
        MethodInfo successMethod = resultType.GetMethod("SuccessResult", BindingFlags.Static | BindingFlags.Public);
        object result = successMethod.Invoke(null, new object[] { "Created " + numbers.Count + " interactable object(s): " + baseName });
        FieldInfo createdField = resultType.GetField("CreatedObjects", BindingFlags.Instance | BindingFlags.Public);
        IList createdObjects = createdField.GetValue(result) as IList;

        int highest = GetStoredHighWaterMark(baseName);
        foreach (int number in numbers)
        {
            string name = baseName + " (" + number + ")";
            CreateSingleInteractable(requestType.DeclaringType, name);
            if (createdObjects != null) createdObjects.Add(name);
            if (number > highest) highest = number;
        }

        if (updateWaterMark || numbers.Count > 0)
            SetStoredHighWaterMark(baseName, highest);

        return result;
    }

    private static object CreateFailure(Type requestType, string message)
    {
        Type resultType = requestType.DeclaringType.GetNestedType("CommandResult", BindingFlags.NonPublic);
        MethodInfo failureMethod = resultType.GetMethod("Failure", BindingFlags.Static | BindingFlags.Public);
        return failureMethod.Invoke(null, new object[] { message });
    }

    private static void CreateSingleInteractable(Type agentType, string name)
    {
        MethodInfo method = agentType.GetMethod("CreateSingleInteractable", BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new MissingMethodException(agentType.FullName, "CreateSingleInteractable");
        method.Invoke(null, new object[] { name });
    }

    private static bool TryParseExplicitNumbers(string text, out List<int> numbers)
    {
        numbers = new List<int>();
        if (string.IsNullOrWhiteSpace(text) || text.IndexOf(',') < 0) return false;
        string[] parts = text.Split(',');
        if (parts.Length == 0) return false;
        foreach (string part in parts)
        {
            int number;
            if (!int.TryParse(part.Trim(), out number)) return false;
            numbers.Add(number);
        }
        return numbers.Count > 0;
    }

    private static int GetNextNumber(string baseName)
    {
        int high = GetStoredHighWaterMark(baseName);
        int next = high + 1;
        while (FindSceneObject(baseName + " (" + next + ")") != null) next++;
        return next;
    }

    private static int GetStoredHighWaterMark(string baseName)
    {
        int value;
        if (highWaterMarks.TryGetValue(baseName, out value)) return value;
        value = EditorPrefs.GetInt(PrefKey(baseName), FindHighestSceneNumber(baseName));
        highWaterMarks[baseName] = value;
        return value;
    }

    private static void SetStoredHighWaterMark(string baseName, int value)
    {
        highWaterMarks[baseName] = value;
        EditorPrefs.SetInt(PrefKey(baseName), value);
    }

    private static string PrefKey(string baseName)
    {
        return "CompanyGame.CommandAgent.Numbering." + baseName;
    }

    private static int FindHighestSceneNumber(string baseName)
    {
        int highest = 0;
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        string prefix = baseName + " (";
        foreach (GameObject go in all)
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid()) continue;
            if (!go.name.StartsWith(prefix, StringComparison.Ordinal) || !go.name.EndsWith(")", StringComparison.Ordinal)) continue;
            string numberText = go.name.Substring(prefix.Length, go.name.Length - prefix.Length - 1);
            int number;
            if (int.TryParse(numberText, out number) && number > highest) highest = number;
        }
        return highest;
    }

    private static GameObject FindSceneObject(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) return go;

        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject candidate in all)
        {
            if (candidate != null && !EditorUtility.IsPersistent(candidate) && candidate.scene.IsValid() && candidate.name.Equals(name, StringComparison.Ordinal))
                return candidate;
        }
        return null;
    }

    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type exact = assembly.GetType(name);
                if (exact != null) return exact;
                foreach (Type type in assembly.GetTypes())
                    if (type.Name.Equals(name, StringComparison.Ordinal)) return type;
            }
            catch (ReflectionTypeLoadException) { }
        }
        return null;
    }
}
