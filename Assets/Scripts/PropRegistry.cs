using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Builds a deterministic index <-> PropTarget mapping shared across Host and Clients
/// based on sorted transform hierarchy paths with sibling indices.
/// </summary>
public class PropRegistry : MonoBehaviour
{
    public static PropRegistry Instance { get; private set; }

    readonly List<PropTarget> _props = new();
    readonly Dictionary<PropTarget, int> _propToIndex = new();

    public int Count => _props.Count;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Refresh();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Refresh()
    {
        _props.Clear();
        _propToIndex.Clear();

        var found = FindObjectsByType<PropTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var entries = new List<(string key, PropTarget target)>(found.Length);
        for (int i = 0; i < found.Length; i++)
        {
            PropTarget pt = found[i];
            if (pt == null) continue;
            entries.Add((GetUniqueHierarchyPath(pt.transform), pt));
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
        for (int i = 0; i < entries.Count; i++)
        {
            _props.Add(entries[i].target);
            _propToIndex[entries[i].target] = i;
        }
    }

    public int IndexOf(PropTarget target)
    {
        if (target == null) return -1;
        if (_props.Count == 0) Refresh();
        return _propToIndex.TryGetValue(target, out int idx) ? idx : -1;
    }

    public PropTarget GetTarget(int index)
    {
        if (index < 0) return null;
        if (_props.Count == 0) Refresh();
        return index < _props.Count ? _props[index] : null;
    }

    static string GetUniqueHierarchyPath(Transform t)
    {
        var sb = new StringBuilder(96);
        AppendPath(t, sb);
        return sb.ToString();
    }

    static void AppendPath(Transform t, StringBuilder sb)
    {
        if (t.parent != null)
        {
            AppendPath(t.parent, sb);
            sb.Append('/');
        }
        sb.Append(t.name);
        sb.Append('[');
        sb.Append(t.GetSiblingIndex());
        sb.Append(']');
    }
}
