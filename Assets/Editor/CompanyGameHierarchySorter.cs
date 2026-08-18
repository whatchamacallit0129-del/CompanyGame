using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Automatically keeps numbered Company Game objects in natural numeric order
/// inside the Unity Hierarchy. For example: 직원 (1), 직원 (2), ... 직원 (15).
/// Only objects with the pattern "Name (number)" are reordered, so unrelated
/// objects and intentional hierarchy groups are left alone.
/// </summary>
[InitializeOnLoad]
public static class CompanyGameHierarchySorter
{
    private static bool sorting;
    private static readonly Regex NumberedName = new Regex(
        @"^(.*) \((\d+)\)$",
        RegexOptions.Compiled);

    static CompanyGameHierarchySorter()
    {
        EditorApplication.hierarchyChanged -= SortHierarchy;
        EditorApplication.hierarchyChanged += SortHierarchy;
        EditorApplication.delayCall += SortHierarchy;
    }

    private static void SortHierarchy()
    {
        if (sorting) return;

        sorting = true;
        try
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            Dictionary<Transform, List<Transform>> groups = new Dictionary<Transform, List<Transform>>();

            foreach (Transform current in all)
            {
                if (current == null || EditorUtility.IsPersistent(current) || !current.gameObject.scene.IsValid())
                    continue;

                Match match = NumberedName.Match(current.name);
                if (!match.Success) continue;

                Transform parent = current.parent;
                List<Transform> list;
                if (!groups.TryGetValue(parent, out list))
                {
                    list = new List<Transform>();
                    groups[parent] = list;
                }
                list.Add(current);
            }

            foreach (KeyValuePair<Transform, List<Transform>> pair in groups)
            {
                List<Transform> list = pair.Value;
                list.Sort(CompareTransforms);

                int targetIndex = FindFirstRelevantIndex(pair.Key, list);
                for (int i = 0; i < list.Count; i++)
                {
                    Transform item = list[i];
                    if (item.GetSiblingIndex() != targetIndex + i)
                        item.SetSiblingIndex(targetIndex + i);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[Company Game] Hierarchy sort failed: " + ex);
        }
        finally
        {
            sorting = false;
        }
    }

    private static int FindFirstRelevantIndex(Transform parent, List<Transform> sorted)
    {
        if (sorted.Count == 0) return 0;

        int first = int.MaxValue;
        foreach (Transform item in sorted)
            first = Math.Min(first, item.GetSiblingIndex());

        return first == int.MaxValue ? 0 : first;
    }

    private static int CompareTransforms(Transform a, Transform b)
    {
        Match ma = NumberedName.Match(a.name);
        Match mb = NumberedName.Match(b.name);

        string baseA = ma.Groups[1].Value;
        string baseB = mb.Groups[1].Value;

        int baseCompare = string.CompareOrdinal(baseA, baseB);
        if (baseCompare != 0) return baseCompare;

        long numberA = long.Parse(ma.Groups[2].Value);
        long numberB = long.Parse(mb.Groups[2].Value);
        int numberCompare = numberA.CompareTo(numberB);
        if (numberCompare != 0) return numberCompare;

        return string.CompareOrdinal(a.name, b.name);
    }
}
