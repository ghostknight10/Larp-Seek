using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Randomly removes (deactivates) a percentage of each morphable prop type at round start
/// so Prop players can morph into that prop type and take the empty spot to hide.
/// Always leaves at least 1 active instance of each prop type so every type stays morphable.
/// </summary>
public class PropRandomizer : MonoBehaviour
{
    [Header("Seed (sync this across network in multiplayer)")]
    public bool useFixedSeed = false;
    public int seed = 42;

    [Header("Missing Chance per Prop Type (0 = keep all, 1 = remove all but 1)")]
    [Range(0f, 0.9f)] public float chairMissingChance       = 0.20f;
    [Range(0f, 0.9f)] public float tableMissingChance       = 0.12f;
    [Range(0f, 0.9f)] public float plantMissingChance       = 0.25f;
    [Range(0f, 0.9f)] public float dustbinMissingChance     = 0.30f;
    [Range(0f, 0.9f)] public float chipsPacketMissingChance = 0.25f;
    [Range(0f, 0.9f)] public float snackJarMissingChance    = 0.25f;
    [Range(0f, 0.9f)] public float freezerMissingChance     = 0.50f;

    [Header("Debug Info")]
    [SerializeField] int _removedCount;
    [SerializeField] int _keptCount;

    void Start()
    {
        int activeSeed = useFixedSeed ? seed : System.Environment.TickCount;
        ApplyRandomMissing(activeSeed);
    }

    public void ApplyRandomMissing(int roundSeed)
    {
        var rng = new System.Random(roundSeed);
        var allProps = GetComponentsInChildren<PropTarget>(true);

        // Reset all props active first (useful when starting a new round)
        for (int i = 0; i < allProps.Length; i++)
            allProps[i].gameObject.SetActive(true);

        // Bucket props by DisplayName; handle parent-level props (Table) before child props (Chair)
        var buckets = new Dictionary<string, List<PropTarget>>();
        foreach (var pt in allProps)
        {
            string key = pt.DisplayName;
            if (!buckets.TryGetValue(key, out var list))
            {
                list = new List<PropTarget>();
                buckets[key] = list;
            }
            list.Add(pt);
        }

        _removedCount = 0;
        _keptCount = 0;

        // 1. Process "Table" first (deactivating a Table also hides its child Chairs)
        if (buckets.TryGetValue("Table", out var tables))
            ProcessBucket(tables, tableMissingChance, rng);

        // 2. Process all other prop categories (skipping any whose parent Table was just hidden)
        foreach (var kvp in buckets)
        {
            if (kvp.Key == "Table") continue;
            float chance = GetMissingChance(kvp.Key);
            ProcessBucket(kvp.Value, chance, rng);
        }

        Debug.Log($"[PropRandomizer] Seed={roundSeed}: Removed {_removedCount} props to create hiding spots ({_keptCount} active).");
    }

    void ProcessBucket(List<PropTarget> items, float missingChance, System.Random rng)
    {
        // Filter to items whose parent hierarchy is currently active
        var activeCandidates = new List<PropTarget>(items.Count);
        foreach (var item in items)
        {
            if (item != null && item.gameObject.activeInHierarchy)
                activeCandidates.Add(item);
        }

        if (activeCandidates.Count <= 1)
        {
            _keptCount += activeCandidates.Count;
            return;
        }

        // Shuffle candidates with Fisher-Yates so every spot has an equal chance
        for (int i = activeCandidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (activeCandidates[i], activeCandidates[j]) = (activeCandidates[j], activeCandidates[i]);
        }

        // Always guarantee at least 1 stays active so the player can morph into it
        int targetRemove = Mathf.RoundToInt(activeCandidates.Count * missingChance);
        targetRemove = Mathf.Clamp(targetRemove, 0, activeCandidates.Count - 1);

        for (int i = 0; i < activeCandidates.Count; i++)
        {
            bool remove = i < targetRemove;
            activeCandidates[i].gameObject.SetActive(!remove);
            if (remove) _removedCount++;
            else _keptCount++;
        }
    }

    float GetMissingChance(string displayName)
    {
        switch (displayName)
        {
            case "Chair":             return chairMissingChance;
            case "Table":             return tableMissingChance;
            case "Potted Plant":      return plantMissingChance;
            case "Dustbin":           return dustbinMissingChance;
            case "Chips Packet":      return chipsPacketMissingChance;
            case "Snack Jar":         return snackJarMissingChance;
            case "Ice Cream Freezer": return freezerMissingChance;
            default:                  return 0.20f;
        }
    }
}
