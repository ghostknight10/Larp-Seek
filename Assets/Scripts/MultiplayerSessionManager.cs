using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using Debug = UnityEngine.Debug;

public enum SessionConnectionMode
{
    None,
    SoloOffline,
    OnlineRelayCode,
    LocalLan
}

public struct LobbyPlayerSummary
{
    public string Name;
    public bool IsHost;
    public bool IsLocal;
}

/// <summary>
/// Coordinates Unity Services authentication, Relay/Lobby session hosting and joining by code,
/// LAN local direct play, offline solo play, and returning cleanly to the Main Menu.
/// </summary>
public class MultiplayerSessionManager : MonoBehaviour
{
    public static MultiplayerSessionManager Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] Camera menuCamera;
    [SerializeField] AudioListener menuAudioListener;
    [SerializeField] GameObject networkPlayerPrefab;

    [Header("Session Defaults")]
    [SerializeField] int maxPlayers = 8;
    [SerializeField] ushort defaultLanPort = 7777;

    const string PrefPlayerNameKey = "LarpSeek_PlayerName";

    public event Action OnSessionUpdated;
    public event Action<string> OnStatusMessage;
    public event Action<string> OnErrorMessage;

    public SessionConnectionMode ConnectionMode { get; private set; } = SessionConnectionMode.None;
    public ISession ActiveSession { get; private set; }
    public string LocalPlayerName { get; private set; } = "Player";
    public bool IsInMatch => _inMatch;
    public bool IsHostPlayer =>
        ConnectionMode == SessionConnectionMode.SoloOffline ||
        (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost);

    public string CurrentJoinCode
    {
        get
        {
            if (ConnectionMode == SessionConnectionMode.OnlineRelayCode && ActiveSession != null)
                return ActiveSession.Code ?? string.Empty;
            if (ConnectionMode == SessionConnectionMode.LocalLan)
                return $"LAN:{_lanPort}";
            if (ConnectionMode == SessionConnectionMode.SoloOffline)
                return "SOLO";
            return string.Empty;
        }
    }

    bool _servicesInitialized;
    bool _isBusy;
    bool _inMatch;
    ushort _lanPort = 7777;
    NetworkPlayerController _offlineSoloPlayer;

    public bool IsBusy => _isBusy;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LocalPlayerName = PlayerPrefs.GetString(PrefPlayerNameKey, $"Seek_{UnityEngine.Random.Range(100, 999)}");
        SetMenuCameraActive(true);

        NetworkPlayerController.OnActivePlayersChanged += HandleNetworkPlayersChanged;
        NetworkMatchState.MatchStartedChanged += HandleMatchStartedChanged;
    }

    void Start()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    void OnDestroy()
    {
        NetworkPlayerController.OnActivePlayersChanged -= HandleNetworkPlayersChanged;
        NetworkMatchState.MatchStartedChanged -= HandleMatchStartedChanged;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;

        if (Instance == this)
            Instance = null;
    }

    public void SetPlayerName(string playerName)
    {
        string clean = string.IsNullOrWhiteSpace(playerName) ? $"Seek_{UnityEngine.Random.Range(100, 999)}" : playerName.Trim();
        if (clean.Length > 24) clean = clean.Substring(0, 24);
        LocalPlayerName = clean;
        PlayerPrefs.SetString(PrefPlayerNameKey, clean);
        PlayerPrefs.Save();
    }

    public async Task<bool> HostOnlineLobbyAsync(string playerName)
    {
        if (_isBusy) return false;
        _isBusy = true;
        SetPlayerName(playerName);
        OnStatusMessage?.Invoke("Connecting to Unity Cloud & allocating Relay...");

        try
        {
            await EnsureServicesSignedInAsync();

            var options = new SessionOptions
            {
                Name = $"{LocalPlayerName}'s Room",
                MaxPlayers = Mathf.Clamp(maxPlayers, 2, 16),
                IsPrivate = false,
                PlayerProperties = new Dictionary<string, PlayerProperty>
                {
                    { "name", new PlayerProperty(LocalPlayerName, VisibilityPropertyOptions.Member) }
                }
            }.WithRelayNetwork();

            ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);
            SubscribeSessionEvents(ActiveSession);
            ConnectionMode = SessionConnectionMode.OnlineRelayCode;
            _inMatch = false;

            OnStatusMessage?.Invoke($"Lobby created! Share code: {ActiveSession.Code}");
            OnSessionUpdated?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Multiplayer] Host failed: {ex}");
            await CleanupFailedAttemptAsync();
            OnErrorMessage?.Invoke(FormatServicesError(ex, "Could not host online lobby."));
            return false;
        }
        finally
        {
            _isBusy = false;
        }
    }

    public async Task<bool> JoinOnlineLobbyByCodeAsync(string joinCode, string playerName)
    {
        if (_isBusy) return false;
        string normalizedCode = string.IsNullOrWhiteSpace(joinCode) ? string.Empty : joinCode.Trim().ToUpperInvariant();
        if (normalizedCode.Length < 4)
        {
            OnErrorMessage?.Invoke("Enter a valid Lobby Code (e.g. ABC123).");
            return false;
        }

        _isBusy = true;
        SetPlayerName(playerName);
        OnStatusMessage?.Invoke($"Joining lobby {normalizedCode}...");

        try
        {
            await EnsureServicesSignedInAsync();

            var joinOptions = new JoinSessionOptions
            {
                PlayerProperties = new Dictionary<string, PlayerProperty>
                {
                    { "name", new PlayerProperty(LocalPlayerName, VisibilityPropertyOptions.Member) }
                }
            };

            ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(normalizedCode, joinOptions);
            SubscribeSessionEvents(ActiveSession);
            ConnectionMode = SessionConnectionMode.OnlineRelayCode;
            _inMatch = NetworkMatchState.Instance != null && NetworkMatchState.Instance.MatchStarted.Value;

            OnStatusMessage?.Invoke($"Joined lobby {ActiveSession.Code}!");
            OnSessionUpdated?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Multiplayer] Join failed: {ex}");
            await CleanupFailedAttemptAsync();
            OnErrorMessage?.Invoke(FormatServicesError(ex, $"Could not join lobby '{normalizedCode}'."));
            return false;
        }
        finally
        {
            _isBusy = false;
        }
    }

    public bool StartLanHost(string playerName, ushort port = 0)
    {
        if (_isBusy || NetworkManager.Singleton == null) return false;
        SetPlayerName(playerName);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            OnErrorMessage?.Invoke("UnityTransport missing on NetworkManager.");
            return false;
        }

        _lanPort = port > 0 ? port : defaultLanPort;
        transport.SetConnectionData("0.0.0.0", _lanPort);

        if (!NetworkManager.Singleton.StartHost())
        {
            OnErrorMessage?.Invoke($"Failed to bind LAN Host on port {_lanPort}.");
            return false;
        }

        ConnectionMode = SessionConnectionMode.LocalLan;
        _inMatch = false;
        OnStatusMessage?.Invoke($"LAN Lobby hosted on port {_lanPort}.");
        OnSessionUpdated?.Invoke();
        return true;
    }

    public bool JoinLanClient(string ipAddress, ushort port, string playerName)
    {
        if (_isBusy || NetworkManager.Singleton == null) return false;
        SetPlayerName(playerName);

        string targetIp = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim();
        ushort targetPort = port > 0 ? port : defaultLanPort;

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            OnErrorMessage?.Invoke("UnityTransport missing on NetworkManager.");
            return false;
        }

        _lanPort = targetPort;
        transport.SetConnectionData(targetIp, targetPort);

        if (!NetworkManager.Singleton.StartClient())
        {
            OnErrorMessage?.Invoke($"Failed to connect to {targetIp}:{targetPort}.");
            return false;
        }

        ConnectionMode = SessionConnectionMode.LocalLan;
        _inMatch = false;
        OnStatusMessage?.Invoke($"Connecting to {targetIp}:{targetPort}...");
        OnSessionUpdated?.Invoke();
        return true;
    }

    public void StartSoloOffline(string playerName)
    {
        if (_isBusy) return;
        SetPlayerName(playerName);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        if (networkPlayerPrefab == null)
        {
            OnErrorMessage?.Invoke("Player prefab is not assigned on MultiplayerSessionManager.");
            return;
        }

        CleanupOfflineSoloPlayer();
        GameObject playerObj = Instantiate(networkPlayerPrefab);
        playerObj.name = "Player_Solo";
        var netObj = playerObj.GetComponent<NetworkObject>();
        if (netObj != null) Destroy(netObj);
        var netTrans = playerObj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTrans != null) Destroy(netTrans);

        var controller = playerObj.GetComponent<NetworkPlayerController>();
        _offlineSoloPlayer = controller;

        ConnectionMode = SessionConnectionMode.SoloOffline;
        _inMatch = true;
        SetMenuCameraActive(false);

        if (controller != null)
            controller.ConfigureOfflineSolo(LocalPlayerName);

        OnSessionUpdated?.Invoke();
    }

    public void StartMatchFromLobby()
    {
        if (!IsHostPlayer) return;
        if (NetworkMatchState.Instance != null && NetworkMatchState.Instance.IsSpawned)
        {
            NetworkMatchState.Instance.StartMatchOnHost();
        }
        else
        {
            _inMatch = true;
            SetMenuCameraActive(false);
            OnSessionUpdated?.Invoke();
        }
    }

    public async Task LeaveSessionToMenuAsync()
    {
        if (_isBusy) return;
        _isBusy = true;

        try
        {
            await CleanupFailedAttemptAsync();
            CleanupOfflineSoloPlayer();

            ConnectionMode = SessionConnectionMode.None;
            _inMatch = false;
            SetMenuCameraActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            OnStatusMessage?.Invoke("Returned to Main Menu.");
            OnSessionUpdated?.Invoke();
        }
        finally
        {
            _isBusy = false;
        }
    }

    public List<LobbyPlayerSummary> GetLobbyPlayers()
    {
        var list = new List<LobbyPlayerSummary>();

        if (NetworkPlayerController.ActivePlayers.Count > 0)
        {
            for (int i = 0; i < NetworkPlayerController.ActivePlayers.Count; i++)
            {
                var p = NetworkPlayerController.ActivePlayers[i];
                if (p == null) continue;
                list.Add(new LobbyPlayerSummary
                {
                    Name = p.DisplayName,
                    IsHost = p.IsSpawned ? p.OwnerClientId == NetworkManager.ServerClientId : true,
                    IsLocal = p.IsLocalControlled
                });
            }
            return list;
        }

        if (ActiveSession != null && ActiveSession.Players != null)
        {
            foreach (var sp in ActiveSession.Players)
            {
                string pName = sp.Properties != null && sp.Properties.TryGetValue("name", out var prop)
                    ? prop.Value
                    : sp.Id.Substring(0, Math.Min(6, sp.Id.Length));
                list.Add(new LobbyPlayerSummary
                {
                    Name = pName,
                    IsHost = sp.Id == ActiveSession.Host,
                    IsLocal = ActiveSession.CurrentPlayer != null && sp.Id == ActiveSession.CurrentPlayer.Id
                });
            }
        }

        return list;
    }

    async Task EnsureServicesSignedInAsync()
    {
        if (!_servicesInitialized)
        {
            var initOptions = new InitializationOptions();
            string profile = ResolveAuthProfile();
            if (!string.IsNullOrEmpty(profile))
                initOptions.SetProfile(profile);

            await UnityServices.InitializeAsync(initOptions);
            _servicesInitialized = true;
        }

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    static string ResolveAuthProfile()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-mpProfile" && !string.IsNullOrWhiteSpace(args[i + 1]))
                return SanitizeProfile(args[i + 1]);
        }

        // MPPM Virtual Players execute from Library/VP/<vpId>; give each clone a distinct profile.
        string dataPath = Application.dataPath.Replace('\\', '/');
        int vpIdx = dataPath.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
        if (vpIdx >= 0)
        {
            string rest = dataPath.Substring(vpIdx + "/Library/VP/".Length);
            int slash = rest.IndexOf('/');
            string vpId = slash > 0 ? rest.Substring(0, slash) : rest;
            return SanitizeProfile("vp_" + vpId);
        }

#if UNITY_EDITOR
        return "editor_main";
#else
        return $"player_{Process.GetCurrentProcess().Id % 10000}";
#endif
    }

    static string SanitizeProfile(string raw)
    {
        var chars = new char[Math.Min(30, raw.Length)];
        int len = 0;
        for (int i = 0; i < raw.Length && len < chars.Length; i++)
        {
            char c = raw[i];
            chars[len++] = char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_';
        }
        return new string(chars, 0, len);
    }

    void SubscribeSessionEvents(ISession session)
    {
        if (session == null) return;
        session.Changed += HandleSessionChanged;
        session.PlayerJoined += HandleSessionPlayerEvent;
        session.PlayerHasLeft += HandleSessionPlayerEvent;
        session.Deleted += HandleSessionEndedRemotely;
        session.RemovedFromSession += HandleSessionEndedRemotely;
    }

    void UnsubscribeSessionEvents(ISession session)
    {
        if (session == null) return;
        session.Changed -= HandleSessionChanged;
        session.PlayerJoined -= HandleSessionPlayerEvent;
        session.PlayerHasLeft -= HandleSessionPlayerEvent;
        session.Deleted -= HandleSessionEndedRemotely;
        session.RemovedFromSession -= HandleSessionEndedRemotely;
    }

    void HandleSessionChanged() => OnSessionUpdated?.Invoke();
    void HandleSessionPlayerEvent(string _) => OnSessionUpdated?.Invoke();

    async void HandleSessionEndedRemotely()
    {
        if (_isBusy) return;
        await LeaveSessionToMenuAsync();
        OnErrorMessage?.Invoke("Lobby closed by the host.");
    }

    void HandleNetworkPlayersChanged()
    {
        OnSessionUpdated?.Invoke();
    }

    void HandleMatchStartedChanged(bool started)
    {
        if (ConnectionMode == SessionConnectionMode.None || ConnectionMode == SessionConnectionMode.SoloOffline)
            return;

        _inMatch = started;
        SetMenuCameraActive(!started);
        if (!started)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        OnSessionUpdated?.Invoke();
    }

    void HandleClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;
        if (clientId == NetworkManager.Singleton.LocalClientId && !NetworkManager.Singleton.IsServer && ConnectionMode != SessionConnectionMode.None)
        {
            string reason = NetworkManager.Singleton.DisconnectReason;
            _ = HandleUnexpectedDisconnectAsync(string.IsNullOrEmpty(reason) ? "Disconnected from host." : reason);
        }
    }

    async Task HandleUnexpectedDisconnectAsync(string message)
    {
        if (_isBusy) return;
        await LeaveSessionToMenuAsync();
        OnErrorMessage?.Invoke(message);
    }

    async Task CleanupFailedAttemptAsync()
    {
        if (ActiveSession != null)
        {
            var session = ActiveSession;
            ActiveSession = null;
            UnsubscribeSessionEvents(session);
            try
            {
                if (session.IsHost)
                    await session.AsHost().DeleteAsync();
                else
                    await session.LeaveAsync();
            }
            catch (Exception)
            {
                // Best effort cleanup
            }
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
    }

    void CleanupOfflineSoloPlayer()
    {
        if (_offlineSoloPlayer != null)
        {
            Destroy(_offlineSoloPlayer.gameObject);
            _offlineSoloPlayer = null;
        }
    }

    void SetMenuCameraActive(bool active)
    {
        if (menuCamera != null)
        {
            menuCamera.enabled = active;
            menuCamera.gameObject.tag = active ? "MainCamera" : "Untagged";
        }
        if (menuAudioListener != null)
            menuAudioListener.enabled = active;
    }

    static string FormatServicesError(Exception ex, string fallback)
    {
        string msg = ex.Message ?? string.Empty;
        if (msg.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("not enabled", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return $"{fallback} Enable Lobby & Relay on Unity Cloud Dashboard first (or use LAN/Solo below).";
        }
        return $"{fallback} {msg}";
    }
}
