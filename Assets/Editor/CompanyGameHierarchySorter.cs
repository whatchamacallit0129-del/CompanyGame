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
            List<HierarchyGroup> groups = new List<HierarchyGroup>();

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
                HierarchyGroup group = null;

                foreach (HierarchyGroup candidate in groups)
                {
                    if (candidate != null && candidate.Parent == parent)
                    {
                        group = candidate;
                        break;
                    }
                }

                if (group == null)
                {
                    group = new HierarchyGroup(parent);
                    groups.Add(group);
                }

                group.Items.Add(current);
            }

            foreach (HierarchyGroup group in groups)
            {
                if (group == null || group.Items == null || group.Items.Count < 2)
                    continue;

                group.Items.Sort(CompareTransforms);

                // Save the first sibling position before changing any indices.
                int firstIndex = int.MaxValue;
                foreach (Transform item in group.Items)
                {
                    if (item != null)
                        firstIndex = Math.Min(firstIndex, item.GetSiblingIndex());
                }

                if (firstIndex == int.MaxValue)
                    continue;

                // For root objects, Unity's sibling indices use the scene's root
                // object list. Avoid any Transform.rootCount/root API entirely.
                for (int i = 0; i < group.Items.Count; i++)
                {
                    Transform item = group.Items[i];
                    if (item == null)
                        continue;

                    item.SetSiblingIndex(firstIndex + i);
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

    private sealed class HierarchyGroup
    {
        public readonly Transform Parent;
        public readonly List<Transform> Items = new List<Transform>();

        public HierarchyGroup(Transform parent)
        {
            Parent = parent;
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
