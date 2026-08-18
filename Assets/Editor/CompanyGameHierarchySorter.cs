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

        // Sort existing objects after Unity finishes loading the editor/project.
        EditorApplication.delayCall += SortHierarchy;
    }

    /// <summary>
    /// Manually sort all currently loaded scene objects.
    /// </summary>
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
                if (current == null || EditorUtility.IsPersistent(current) || !current.gameObject.scene.IsValid())
                    continue;

                if (!NumberedName.IsMatch(current.name))
                    continue;

                Transform parent = current.parent;
                if (!groups.TryGetValue(parent, out List<Transform> list))
                {
                    list = new List<Transform>();
                    groups[parent] = list;
                }

                list.Add(current);
            }

            foreach (KeyValuePair<Transform, List<Transform>> pair in groups)
            {
                List<Transform> list = pair.Value;
                if (list.Count < 2)
                    continue;

                list.Sort(CompareTransforms);

                // Move from the end toward the beginning so changing sibling indices
                // cannot disturb the position we are about to assign.
                int firstIndex = int.MaxValue;
                foreach (Transform item in list)
                    firstIndex = Math.Min(firstIndex, item.GetSiblingIndex());

                if (firstIndex == int.MaxValue)
                    firstIndex = 0;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Transform item = list[i];
                    int targetIndex = firstIndex + i;

                    if (item.GetSiblingIndex() != targetIndex)
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
        Match ma = NumberedName.Match(a.name);
        Match mb = NumberedName.Match(b.name);

        string baseA = ma.Groups[1].Value;
        string baseB = mb.Groups[1].Value;

        int baseCompare = string.CompareOrdinal(baseA, baseB);
        if (baseCompare != 0)
            return baseCompare;

        long numberA = long.Parse(ma.Groups[2].Value);
        long numberB = long.Parse(mb.Groups[2].Value);

        int numberCompare = numberA.CompareTo(numberB);
        if (numberCompare != 0)
            return numberCompare;

        return string.CompareOrdinal(a.name, b.name);
    }
}
