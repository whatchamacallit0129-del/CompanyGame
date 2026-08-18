using System;
using UnityEditor;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CompanyGameObjectIdentity : MonoBehaviour
{
    [SerializeField, HideInInspector] private string objectId;

    public string ObjectId => objectId;

    private void Reset()
    {
        EnsureId();
    }

    private void OnValidate()
    {
        EnsureId();
    }

    public void EnsureId()
    {
        if (!string.IsNullOrEmpty(objectId)) return;
        objectId = Guid.NewGuid().ToString("N");
        EditorUtility.SetDirty(this);
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.hierarchyChanged -= EnsureSceneObjectsHaveIds;
        EditorApplication.hierarchyChanged += EnsureSceneObjectsHaveIds;
    }

    private static void EnsureSceneObjectsHaveIds()
    {
        if (Application.isPlaying) return;
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject go in objects)
        {
            if (go == null) continue;
            CompanyGameObjectIdentity identity = go.GetComponent<CompanyGameObjectIdentity>();
            if (identity == null)
            {
                identity = Undo.AddComponent<CompanyGameObjectIdentity>(go);
                identity.EnsureId();
            }
            else
            {
                identity.EnsureId();
            }
        }
    }
#endif
}
