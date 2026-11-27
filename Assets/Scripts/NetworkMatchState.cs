using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum MatchOutcomeType
{
    InProgress = 0,
    HidersWin = 1,
    HunterWins = 2
}

/// <summary>
/// Master networked match state for Larp-Seek.
/// Synchronizes:
/// - 5-minute match countdown
/// - Random hunter assignment
/// - Dual beacon station positions and 90-second cumulative capture progress
/// - Mess refrigerator 20-second revival tracking
/// - Win / Loss match evaluation
/// </summary>
public class NetworkMatchState : NetworkBehaviour
{
    public static NetworkMatchState Instance { get; private set; }

    public static event Action<bool> MatchStartedChanged;
    public static event Action<MatchOutcomeType> MatchOutcomeChanged;

    public const float MatchDurationSeconds = 300.0f; // 5 minutes
    public const float BeaconTargetSeconds = 90.0f;   // 90 seconds
    public const float ReviveTargetSeconds = 20.0f;   // 20 seconds
    public const int ReviverBonusPoints = 150;

    // Coordinates verified against the built scene (floor raycast + clearance check).
    /// <summary>Station 1: open floor in the Blue Lounge (Lounge_BlueFloor, between bean bags and foosball).</summary>
    public static readonly Vector3 LoungeStationPos = new Vector3(-19.00f, 0.05f, -39.80f);
    /// <summary>Station 2: standing area in front of WaterDispenser_Serving (dispenser at -31.15, 30.9 facing -Z).</summary>
    public static readonly Vector3 CafeteriaStationPos = new Vector3(-30.20f, 0.05f, 29.40f);
    /// <summary>Revival ring centre, in front of ColdDrinkFridge_1/2 (fridges at x=31.3 facing -X).</summary>
    public static readonly Vector3 FridgeZonePos = new Vector3(29.80f, 0.05f, 31.00f);
    /// <summary>Where revived Hiders reappear, beside the Tuck Shop cold-drink fridges.</summary>
    public static readonly Vector3 FridgeReviveSpawn = new Vector3(28.00f, 0.05f, 31.00f);

    /// <summary>
    /// Snaps a point down onto the walkable floor beneath it (ignores triggers, players and steep surfaces)
    /// so stations / rings can never float in the air or sit outside the map.
    /// </summary>
    public static Vector3 SnapToGround(Vector3 p)
    {
        var hits = Physics.RaycastAll(p + Vector3.up * 2.5f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.normal.y < 0.7f) continue;
            if (h.collider.GetComponentInParent<NetworkPlayerController>() != null) continue;
            if (h.collider.GetComponentInParent<BeaconStationVisual>() != null) continue;
            if (h.point.y > p.y + 0.6f) continue; // furniture tops / ceilings above the floor
            if (h.point.y > best) best = h.point.y;
        }
        return float.IsNegativeInfinity(best) ? p : new Vector3(p.x, best + 0.03f, p.z);
    }

    public const string Station1Label = "Lounge Station";
    public const string Station2Label = "Cafeteria Station";

    public NetworkVariable<bool> MatchStarted = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<float> MatchTimeRemaining = new(
        MatchDurationSeconds,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<float> BeaconTotalStayTime = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<Vector3> Station1Pos = new(
        LoungeStationPos,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<Vector3> Station2Pos = new(
        CafeteriaStationPos,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<ulong> HunterClientId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<float> ReviveProgress = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> MatchOutcome = new(
        (int)MatchOutcomeType.InProgress,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>Seconds left in the opening hide phase (Hunter frozen + blind, match clock paused).</summary>
    public NetworkVariable<float> HidePhaseRemaining = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> Station1Capturing = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> Station2Capturing = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>True while at least one living Hider is channelling a revive at the fridge.</summary>
    public NetworkVariable<bool> ReviveChanneling = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> DownedHidersCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ------------------------------------------------------------------ Host-configurable match settings
    // Written by the host in the lobby, replicated to everyone, remembered in PlayerPrefs on the host.

    public enum Setting { HideTime, SeekerVision, SeekerSpeed, HiderSpeed, MatchLength, SeekerReload }

    public struct SettingSpec
    {
        public string Label; public float Min, Max, Step, Default; public string Format;
        public SettingSpec(string label, float min, float max, float step, float def, string format)
        { Label = label; Min = min; Max = max; Step = step; Default = def; Format = format; }
    }

    public static readonly SettingSpec[] SettingSpecs =
    {
        new("HIDE TIME",      5f,  60f, 5f,   20f, "{0:0}s"),
        new("SEEKER VISION",  4f,  40f, 2f,   10f, "{0:0}m"),
        new("SEEKER SPEED",   0.5f, 1.5f, 0.1f, 1f, "{0:0%}"),
        new("HIDER SPEED",    0.5f, 1.5f, 0.1f, 1f, "{0:0%}"),
        new("MATCH LENGTH",   2f,  10f, 1f,    5f, "{0:0} MIN"),
        new("SEEKER RELOAD",  1f,  6f,  0.5f,  3f, "{0:0.0}s"),
    };

    public NetworkVariable<float> SetHideTime = new(20f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> SetSeekerVision = new(10f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> SetSeekerSpeed = new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> SetHiderSpeed = new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> SetMatchMinutes = new(5f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> SetSeekerReload = new(3f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    NetworkVariable<float> SettingVar(Setting s) => s switch
    {
        Setting.HideTime => SetHideTime,
        Setting.SeekerVision => SetSeekerVision,
        Setting.SeekerSpeed => SetSeekerSpeed,
        Setting.HiderSpeed => SetHiderSpeed,
        Setting.MatchLength => SetMatchMinutes,
        _ => SetSeekerReload
    };

    public float GetSetting(Setting s) => SettingVar(s).Value;

    public static string FormatSetting(Setting s, float v) => string.Format(SettingSpecs[(int)s].Format, v);

    /// <summary>Host only: step a setting up/down (clamped) and remember it for next time.</summary>
    public void StepSetting(Setting s, int dir)
    {
        if (!IsServer || MatchStarted.Value) return;
        var spec = SettingSpecs[(int)s];
        float v = Mathf.Clamp(Mathf.Round((GetSetting(s) + dir * spec.Step) / spec.Step) * spec.Step, spec.Min, spec.Max);
        SettingVar(s).Value = v;
        PlayerPrefs.SetFloat("LarpSeek_Setting_" + s, v);
        PlayerPrefs.Save();
    }

    void LoadSettingsOnServer()
    {
        for (int i = 0; i < SettingSpecs.Length; i++)
        {
            var s = (Setting)i;
            var spec = SettingSpecs[i];
            SettingVar(s).Value = Mathf.Clamp(PlayerPrefs.GetFloat("LarpSeek_Setting_" + s, spec.Default), spec.Min, spec.Max);
        }
    }

    public float MatchLengthSeconds => SetMatchMinutes.Value * 60f;

    BeaconStationVisual _stationVisual1;
    BeaconStationVisual _stationVisual2;

    public bool IsMatchActive => MatchStarted.Value && (MatchOutcomeType)MatchOutcome.Value == MatchOutcomeType.InProgress;
    public bool IsHidePhase => IsMatchActive && HidePhaseRemaining.Value > 0f;
    public const float HidePhaseSeconds = 20f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        MatchStarted.OnValueChanged += HandleMatchStartedChanged;
        MatchOutcome.OnValueChanged += HandleOutcomeChanged;
        Station1Pos.OnValueChanged += HandleStationPosChanged;
        Station2Pos.OnValueChanged += HandleStationPosChanged;

        if (IsServer)
        {
            // Every new host session starts in the lobby. The scene object survives NGO shutdown with
            // its last values (e.g. MatchStarted = true after a Solo game), which made the next
            // "Start Match" a no-op and dropped joining clients into a half-started match.
            LoadSettingsOnServer();
            ResetMatchOnHost();

            // Overwrite any stale values serialized into the scene (old outdoor terrace spots).
            SelectDistantStationPositions(out Vector3 p1, out Vector3 p2);
            Station1Pos.Value = p1;
            Station2Pos.Value = p2;
        }

        UpdateStationVisuals();
        MatchStartedChanged?.Invoke(MatchStarted.Value);
    }

    public override void OnNetworkDespawn()
    {
        MatchStarted.OnValueChanged -= HandleMatchStartedChanged;
        MatchOutcome.OnValueChanged -= HandleOutcomeChanged;
        Station1Pos.OnValueChanged -= HandleStationPosChanged;
        Station2Pos.OnValueChanged -= HandleStationPosChanged;
        MatchStartedChanged?.Invoke(false);
    }

    public void StartMatchOnHost()
    {
        if (!IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            return;

        // 1. Anchor the two stations to the Lounge and Cafeteria
        SelectDistantStationPositions(out Vector3 p1, out Vector3 p2);
        Station1Pos.Value = p1;
        Station2Pos.Value = p2;

        // 2. Select initial random hunter
        SelectInitialHunter();

        // 3. Reset timers and state
        MatchTimeRemaining.Value = MatchLengthSeconds;
        HidePhaseRemaining.Value = SetHideTime.Value;
        BeaconTotalStayTime.Value = 0f;
        ReviveProgress.Value = 0f;
        ReviveChanneling.Value = false;
        Station1Capturing.Value = false;
        Station2Capturing.Value = false;
        DownedHidersCount.Value = 0;
        MatchOutcome.Value = (int)MatchOutcomeType.InProgress;
        bool wasStarted = MatchStarted.Value;
        MatchStarted.Value = true;

        UpdateStationVisuals();
        // Value didn't change -> OnValueChanged won't fire; raise the start locally so the host moves on.
        if (wasStarted) MatchStartedChanged?.Invoke(true);
    }

    public void StartMatchSoloOffline()
    {
        SelectDistantStationPositions(out Vector3 p1, out Vector3 p2);
        Station1Pos.Value = p1;
        Station2Pos.Value = p2;

        MatchTimeRemaining.Value = MatchLengthSeconds;
        HidePhaseRemaining.Value = SetHideTime.Value;
        BeaconTotalStayTime.Value = 0f;
        ReviveProgress.Value = 0f;
        ReviveChanneling.Value = false;
        Station1Capturing.Value = false;
        Station2Capturing.Value = false;
        DownedHidersCount.Value = 0;
        MatchOutcome.Value = (int)MatchOutcomeType.InProgress;
        MatchStarted.Value = true;

        UpdateStationVisuals();
        MatchStartedChanged?.Invoke(true);
    }

    public void ResetMatchOnHost()
    {
        if (!IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            return;

        MatchStarted.Value = false;
        MatchOutcome.Value = (int)MatchOutcomeType.InProgress;
        MatchTimeRemaining.Value = MatchLengthSeconds;
        HidePhaseRemaining.Value = SetHideTime.Value;
        BeaconTotalStayTime.Value = 0f;
        ReviveProgress.Value = 0f;
        ReviveChanneling.Value = false;
        Station1Capturing.Value = false;
        Station2Capturing.Value = false;
        DownedHidersCount.Value = 0;
    }

    /// <summary>Stations are fixed tactical points: Station 1 = Blue Lounge, Station 2 = Cafeteria serving side.</summary>
    static void SelectDistantStationPositions(out Vector3 p1, out Vector3 p2)
    {
        p1 = SnapToGround(LoungeStationPos);
        p2 = SnapToGround(CafeteriaStationPos);
    }

    /// <summary>Rejects stale positions (e.g. old terrace values serialized in the scene) - only the two fixed spots are valid.</summary>
    static bool IsValidStationPos(Vector3 p) =>
        (new Vector2(p.x - LoungeStationPos.x, p.z - LoungeStationPos.z)).sqrMagnitude < 1f ||
        (new Vector2(p.x - CafeteriaStationPos.x, p.z - CafeteriaStationPos.z)).sqrMagnitude < 1f;

    /// <summary>Current world position of a station (its placed visual if available, else the fixed spot).</summary>
    public static Vector3 StationWorldPos(int index)
    {
        var inst = Instance;
        var vis = inst != null ? (index == 1 ? inst._stationVisual1 : inst._stationVisual2) : null;
        if (vis != null) return vis.transform.position;
        return index == 1 ? LoungeStationPos : CafeteriaStationPos;
    }

    public static string StationLabel(int index) => index == 1 ? Station1Label : Station2Label;

    void SelectInitialHunter()
    {
        var players = NetworkPlayerController.ActivePlayers;
        if (players.Count == 0) return;

        int hunterIdx = UnityEngine.Random.Range(0, players.Count);
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            if (i == hunterIdx)
            {
                p.SetRole(PlayerRole.Hunter);
                HunterClientId.Value = p.OwnerClientId;
            }
            else
            {
                p.SetRole(PlayerRole.Hider);
            }
        }
    }

    void Update()
    {
        // Only host/server or offline solo ticks match rules
        bool isHostOrSolo = (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ||
                            (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening);

        if (!isHostOrSolo || !IsMatchActive) return;

        float dt = Time.deltaTime;

        // 0. Opening hide phase: Hiders scatter while the Hunter is frozen and blind.
        //    The 5-minute clock, beacons and revives don't run until the Hunter is released.
        if (HidePhaseRemaining.Value > 0f)
        {
            HidePhaseRemaining.Value = Mathf.Max(0f, HidePhaseRemaining.Value - dt);
            return;
        }

        // 1. Tick 5-minute match timer
        MatchTimeRemaining.Value = Mathf.Max(0f, MatchTimeRemaining.Value - dt);
        if (MatchTimeRemaining.Value <= 0f)
        {
            // 5 minutes elapsed and beacon not finished -> Hunter wins
            EndMatch(MatchOutcomeType.HunterWins, "5 minutes elapsed! Hiders failed to activate beacons in time.");
            return;
        }

        // 2. Beacon capture progress
        int hidersInStation1 = 0;
        int hidersInStation2 = 0;
        int totalHiders = 0;
        int downedHiders = 0;

        bool livingHiderInFridge = false;
        _reviversInZone.Clear();

        var players = NetworkPlayerController.ActivePlayers;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            if (p.CurrentRole == PlayerRole.Hider)
            {
                totalHiders++;
                if (p.IsEliminated.Value)
                {
                    downedHiders++;
                    continue;
                }

                // Check Station 1
                if (_stationVisual1 != null && _stationVisual1.IsPlayerInside(p.transform.position))
                    hidersInStation1++;

                // Check Station 2
                if (_stationVisual2 != null && _stationVisual2.IsPlayerInside(p.transform.position))
                    hidersInStation2++;

                // Check Refrigerator Revival Zone
                if (RefrigeratorRevivalStation.Instance != null &&
                    RefrigeratorRevivalStation.Instance.IsPlayerInside(p.transform.position))
                {
                    livingHiderInFridge = true;
                    _reviversInZone.Add(p);
                }
            }
        }

        DownedHidersCount.Value = downedHiders;

        // Check if all hiders are dead
        if (totalHiders > 0 && downedHiders >= totalHiders)
        {
            EndMatch(MatchOutcomeType.HunterWins, "All hiders have been eliminated! Hunter wins.");
            return;
        }

        // Add to 90s beacon stay time
        int totalCapturing = hidersInStation1 + hidersInStation2;
        if (totalCapturing > 0)
        {
            BeaconTotalStayTime.Value = Mathf.Min(BeaconTargetSeconds, BeaconTotalStayTime.Value + dt * totalCapturing);
            if (BeaconTotalStayTime.Value >= BeaconTargetSeconds)
            {
                EndMatch(MatchOutcomeType.HidersWin, "Beacons fully activated! Hiders win!");
                return;
            }
        }

        // Replicated capture state (visuals react on every peer in LateUpdate)
        Station1Capturing.Value = hidersInStation1 > 0;
        Station2Capturing.Value = hidersInStation2 > 0;

        // 3. Refrigerator revival tracking
        //    Living hider(s) in the zone channel; leaving (or being shot) makes progress decay.
        if (downedHiders > 0 && livingHiderInFridge)
        {
            ReviveChanneling.Value = true;
            ReviveProgress.Value = Mathf.Min(ReviveTargetSeconds, ReviveProgress.Value + dt);

            if (ReviveProgress.Value >= ReviveTargetSeconds)
            {
                ReviveAllDownedHiders();
                for (int i = 0; i < _reviversInZone.Count; i++)
                    if (_reviversInZone[i] != null) _reviversInZone[i].AwardScoreServer(ReviverBonusPoints);
                ReviveProgress.Value = 0f;
                ReviveChanneling.Value = false;
            }
        }
        else
        {
            ReviveChanneling.Value = false;
            if (downedHiders == 0)
                ReviveProgress.Value = 0f;
            else if (ReviveProgress.Value > 0f)
                ReviveProgress.Value = Mathf.Max(0f, ReviveProgress.Value - dt * 2f);
        }
    }

    readonly List<NetworkPlayerController> _reviversInZone = new();

    void LateUpdate()
    {
        // Runs on every peer: drive visuals from replicated state.
        if (_stationVisual1 != null) _stationVisual1.SetCapturing(IsMatchActive && Station1Capturing.Value);
        if (_stationVisual2 != null) _stationVisual2.SetCapturing(IsMatchActive && Station2Capturing.Value);
        if (RefrigeratorRevivalStation.Instance != null)
            RefrigeratorRevivalStation.Instance.SetRevivingActive(IsMatchActive && ReviveChanneling.Value);
    }

    void HandleStationPosChanged(Vector3 prev, Vector3 cur) => UpdateStationVisuals();

    void EndMatch(MatchOutcomeType outcome, string reason)
    {
        MatchOutcome.Value = (int)outcome;
        MatchOutcomeChanged?.Invoke(outcome);
        Debug.Log($"[NetworkMatchState] Match Ended: {outcome} ({reason})");
    }

    void ReviveAllDownedHiders()
    {
        Vector3 reviveSpawn = RefrigeratorRevivalStation.Instance != null
            ? RefrigeratorRevivalStation.Instance.ReviveSpawnPosition
            : FridgeReviveSpawn;

        bool networked = IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (!networked)
            ProceduralAudio.PlayRevived(reviveSpawn);

        var players = NetworkPlayerController.ActivePlayers;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p != null && p.CurrentRole == PlayerRole.Hider && p.IsEliminated.Value)
            {
                p.ReviveAt(reviveSpawn);
            }
        }

        if (networked)
            ReviveHidersClientRpc(reviveSpawn); // global revive sound on every peer
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ReviveHidersClientRpc(Vector3 reviveSpawn)
    {
        ProceduralAudio.PlayRevived(reviveSpawn);
    }

    public void UpdateStationVisuals()
    {
        if (_stationVisual1 == null)
        {
            var go1 = GameObject.Find("BeaconStation_1") ?? new GameObject("BeaconStation_1");
            _stationVisual1 = go1.GetComponent<BeaconStationVisual>() ?? go1.AddComponent<BeaconStationVisual>();
            _stationVisual1.stationIndex = 1;
        }
        _stationVisual1.EnsureVisuals();
        _stationVisual1.displayName = Station1Label;
        _stationVisual1.transform.position = SnapToGround(IsValidStationPos(Station1Pos.Value) ? Station1Pos.Value : LoungeStationPos);

        if (_stationVisual2 == null)
        {
            var go2 = GameObject.Find("BeaconStation_2") ?? new GameObject("BeaconStation_2");
            _stationVisual2 = go2.GetComponent<BeaconStationVisual>() ?? go2.AddComponent<BeaconStationVisual>();
            _stationVisual2.stationIndex = 2;
        }
        _stationVisual2.EnsureVisuals();
        _stationVisual2.displayName = Station2Label;
        _stationVisual2.transform.position = SnapToGround(IsValidStationPos(Station2Pos.Value) ? Station2Pos.Value : CafeteriaStationPos);

        // Ensure RefrigeratorRevivalStation exists
        if (RefrigeratorRevivalStation.Instance == null)
        {
            var frGo = GameObject.Find("RefrigeratorRevivalStation") ?? new GameObject("RefrigeratorRevivalStation");
            if (frGo.GetComponent<RefrigeratorRevivalStation>() == null)
                frGo.AddComponent<RefrigeratorRevivalStation>();
        }
    }

    void HandleMatchStartedChanged(bool prev, bool current)
    {
        UpdateStationVisuals();
        MatchStartedChanged?.Invoke(current);
    }

    void HandleOutcomeChanged(int prev, int current)
    {
        MatchOutcomeChanged?.Invoke((MatchOutcomeType)current);
    }
}
