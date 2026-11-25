using System;
using Unity.Netcode;

/// <summary>
/// Networked match lifecycle state shared between Host and Clients.
/// Transitions all lobby members into active gameplay simultaneously.
/// </summary>
public class NetworkMatchState : NetworkBehaviour
{
    public static NetworkMatchState Instance { get; private set; }

    public static event Action<bool> MatchStartedChanged;

    public NetworkVariable<bool> MatchStarted = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

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
        MatchStartedChanged?.Invoke(MatchStarted.Value);
    }

    public override void OnNetworkDespawn()
    {
        MatchStarted.OnValueChanged -= HandleMatchStartedChanged;
        MatchStartedChanged?.Invoke(false);
    }

    public void StartMatchOnHost()
    {
        if (!IsServer) return;
        MatchStarted.Value = true;
    }

    public void ResetMatchOnHost()
    {
        if (!IsServer) return;
        MatchStarted.Value = false;
    }

    void HandleMatchStartedChanged(bool prev, bool current)
    {
        MatchStartedChanged?.Invoke(current);
    }
}
