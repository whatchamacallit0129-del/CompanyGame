using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps numbered Company Game objects in natural numeric order in the Unity Hierarchy.
/// Example: 직원 (1), 직원 (2), ... 직원 (15).
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

    [MenuItem("Tools/Company Game/Sort Hierarchy Now")]
    public static void SortHierarchyNow()
    {
        SortHierarchy();
    }

    private static void SortHierarchy()
    {
        if (sorting)
            return;

        sorting = true;
        try
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            Dictionary<Transform, List<Transform>> groups = new Dictionary<Transform, List<Transform>>();

            foreach (Transform current in all)
            {
                if (current == null)
                    continue;

                GameObject go = current.gameObject;
                if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid())
                    continue;

                Match match = NumberedName.Match(current.name ?? string.Empty);
                if (!match.Success)
                    continue;

                Transform parent = current.parent;
                List<Transform> list;
                if (!groups.TryGetValue(parent, out list))
                {
                    list = new List<Transform>();
                    groups.Add(parent, list);
                }

                list.Add(current);
            }

            foreach (KeyValuePair<Transform, List<Transform>> pair in groups)
            {
                List<Transform> list = pair.Value;
                if (list == null || list.Count < 2)
                    continue;

                list.Sort(CompareTransforms);

                // Move from the end toward the beginning. This avoids sibling-index
                // shifts changing the destination of objects that are still pending.
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Transform item = list[i];
                    if (item == null)
                        continue;

                    item.SetSiblingIndex(GetTargetSiblingIndex(item, list, i));
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

    private static int GetTargetSiblingIndex(Transform item, List<Transform> sorted, int sortedIndex)
    {
        int firstIndex = int.MaxValue;

        for (int i = 0; i < sorted.Count; i++)
        {
            Transform candidate = sorted[i];
            if (candidate == null)
                continue;

            firstIndex = Math.Min(firstIndex, candidate.GetSiblingIndex());
        }

        if (firstIndex == int.MaxValue)
            return item.GetSiblingIndex();

        return firstIndex + sortedIndex;
    }

    private static int CompareTransforms(Transform a, Transform b)
    {
        if (a == null) return 1;
        if (b == null) return -1;

        Match ma = NumberedName.Match(a.name ?? string.Empty);
        Match mb = NumberedName.Match(b.name ?? string.Empty);

        string baseA = ma.Success ? ma.Groups[1].Value : string.Empty;
        string baseB = mb.Success ? mb.Groups[1].Value : string.Empty;

        int baseCompare = string.CompareOrdinal(baseA, baseB);
        if (baseCompare != 0)
            return baseCompare;

        long numberA;
        long numberB;
        if (!long.TryParse(ma.Groups[2].Value, out numberA))
            numberA = long.MaxValue;
        if (!long.TryParse(mb.Groups[2].Value, out numberB))
            numberB = long.MaxValue;

        int numberCompare = numberA.CompareTo(numberB);
        if (numberCompare != 0)
            return numberCompare;

        return string.CompareOrdinal(a.name ?? string.Empty, b.name ?? string.Empty);
    }
}
