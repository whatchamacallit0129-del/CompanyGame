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
            Dictionary<int, List<Transform>> groups = new Dictionary<int, List<Transform>>();

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

                // Transform.parent can legitimately be null for a root object.
                // Use the instance ID as a stable, non-null grouping key.
                int parentId = current.parent != null ? current.parent.GetInstanceID() : 0;

                List<Transform> list;
                if (!groups.TryGetValue(parentId, out list))
                {
                    list = new List<Transform>();
                    groups.Add(parentId, list);
                }

                list.Add(current);
            }

            foreach (KeyValuePair<int, List<Transform>> pair in groups)
            {
                List<Transform> list = pair.Value;
                if (list == null || list.Count < 2)
                    continue;

                list.Sort(CompareTransforms);

                // All objects in a group share the same parent. Rebuild their
                // sibling order directly from the sorted list.
                int firstIndex = int.MaxValue;
                foreach (Transform item in list)
                {
                    if (item == null)
                        continue;

                    firstIndex = Math.Min(firstIndex, item.GetSiblingIndex());
                }

                if (firstIndex == int.MaxValue)
                    continue;

                for (int i = 0; i < list.Count; i++)
                {
                    Transform item = list[i];
                    if (item == null)
                        continue;

                    int targetIndex = Math.Min(firstIndex + i, item.parent != null ? item.parent.childCount - 1 : int.MaxValue);
                    item.SetSiblingIndex(targetIndex);
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
