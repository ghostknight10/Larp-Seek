using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
/// Coordinates Unity Services authentication, Relay/Lobby session hosting and joining by code
/// (DTLS/UDP first, automatic WebSocket Secure wss:443 fallback for strict NATs/firewalls),
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

    [Header("Relay Transport Fallback")]
    [Tooltip("Seconds to wait for a Relay handshake before retrying with the other protocol (DTLS <-> WSS).")]
    [SerializeField] float relayConnectTimeoutSeconds = 8f;

    const string PrefPlayerNameKey = "LarpSeek_PlayerName";
    const string PrefPreferWssKey = "LarpSeek_PreferWss";

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

    /// <summary>Relay protocol used by the current online session (DTLS or WSS).</summary>
    public RelayProtocol ActiveRelayProtocol { get; private set; } = RelayProtocol.DTLS;

    string _cachedLanIp;
    /// <summary>Active local IPv4 of this machine (Wi-Fi / Ethernet) for LAN joining.</summary>
    public string LocalLanIPv4 => string.IsNullOrEmpty(_cachedLanIp) ? (_cachedLanIp = DetectLocalIPv4()) : _cachedLanIp;
    public ushort LanPort => _lanPort;
    public void RefreshLocalLanIPv4() => _cachedLanIp = DetectLocalIPv4();

    public string CurrentJoinCode
    {
        get
        {
            if (ConnectionMode == SessionConnectionMode.OnlineRelayCode && ActiveSession != null)
                return ActiveSession.Code ?? string.Empty;
            if (ConnectionMode == SessionConnectionMode.LocalLan)
                return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
                    ? $"{LocalLanIPv4}:{_lanPort}"
                    : $"{_lanTargetIp}:{_lanPort}";
            if (ConnectionMode == SessionConnectionMode.SoloOffline)
                return "SOLO";
            return string.Empty;
        }
    }

    bool _servicesInitialized;
    bool _isBusy;
    bool _inMatch;
    ushort _lanPort = 7777;
    string _lanTargetIp = "127.0.0.1";
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
        OnStatusMessage += m => LastStatusMessage = m;
        RetroScreenEffect.ConfigureCamera(menuCamera);
        SetMenuCameraActive(true);

        NetworkPlayerController.OnActivePlayersChanged += HandleNetworkPlayersChanged;
        NetworkMatchState.MatchStartedChanged += HandleMatchStartedChanged;
    }

    /// <summary>
    /// Safety net for networked sessions: keep the local in-match flag/menu camera in step with the
    /// replicated MatchStarted value even if the change event was missed (e.g. it replicated while a
    /// client was still finishing its join handshake).
    /// </summary>
    void SyncMatchStarted()
    {
        if (ConnectionMode != SessionConnectionMode.LocalLan && ConnectionMode != SessionConnectionMode.OnlineRelayCode)
            return;
        var ms = NetworkMatchState.Instance;
        if (ms == null || !ms.IsSpawned) return;
        if (ms.MatchStarted.Value != _inMatch)
            HandleMatchStartedChanged(ms.MatchStarted.Value);
    }

    void Update()
    {
        if (!_isBusy) SyncMatchStarted();
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

        if (ActiveSession != null)
            UnsubscribeSessionEvents(ActiveSession);

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

        try
        {
            ConnectingLabel = "HOSTING GAME";
            OnStatusMessage?.Invoke("Hosting game...");
            await EnsureServicesSignedInAsync();

            Exception lastError = null;
            foreach (RelayProtocol protocol in GetProtocolAttemptOrder())
            {
                Debug.Log($"[Multiplayer] Allocating Relay via {ProtocolLabel(protocol)}");
                try
                {
                    ConfigureTransportForProtocol(protocol);
                    var options = new SessionOptions
                    {
                        Name = $"{LocalPlayerName}'s Room",
                        MaxPlayers = Mathf.Clamp(maxPlayers, 2, 16),
                        IsPrivate = false,
                        PlayerProperties = new Dictionary<string, PlayerProperty>
                        {
                            { "name", new PlayerProperty(LocalPlayerName, VisibilityPropertyOptions.Member) }
                        }
                    }.WithRelayNetwork().WithNetworkOptions(new NetworkOptions { RelayProtocol = protocol });

                    ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                    SubscribeSessionEvents(ActiveSession);

                    if (!await WaitForNetworkAsync(host: true))
                        throw new TimeoutException($"Relay host over {protocol} did not start listening in time.");

                    ActiveRelayProtocol = protocol;
                    RememberProtocol(protocol);
                    ConnectionMode = SessionConnectionMode.OnlineRelayCode;
                    _inMatch = false;

                    OnStatusMessage?.Invoke($"Lobby created! Share code: {ActiveSession.Code}");
                    OnSessionUpdated?.Invoke();
                    return true;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Debug.LogWarning($"[Multiplayer] Host over {protocol} failed: {ex.Message}");
                    await CleanupFailedAttemptAsync();
                    if (IsServicesConfigError(ex)) break; // another protocol won't fix a dashboard/auth problem
                    Debug.Log($"[Multiplayer] {ProtocolLabel(protocol)} failed - retrying with fallback protocol");
                }
            }

            OnErrorMessage?.Invoke(FormatServicesError(lastError, "Could not host online lobby (DTLS and WebSocket both failed)."));
            return false;
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
        ConnectingLabel = "JOINING GAME";
        OnStatusMessage?.Invoke($"Joining lobby {normalizedCode}...");

        try
        {
            await EnsureServicesSignedInAsync();

            Exception lastError = null;
            foreach (RelayProtocol protocol in GetProtocolAttemptOrder())
            {
                Debug.Log($"[Multiplayer] Joining {normalizedCode} via {ProtocolLabel(protocol)}");
                try
                {
                    ConfigureTransportForProtocol(protocol);
                    var joinOptions = new JoinSessionOptions
                    {
                        PlayerProperties = new Dictionary<string, PlayerProperty>
                        {
                            { "name", new PlayerProperty(LocalPlayerName, VisibilityPropertyOptions.Member) }
                        }
                    }.WithNetworkOptions(new NetworkOptions { RelayProtocol = protocol });

                    ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(normalizedCode, joinOptions);
                    SubscribeSessionEvents(ActiveSession);

                    if (!await WaitForNetworkAsync(host: false))
                        throw new TimeoutException($"Relay handshake over {protocol} timed out.");

                    ActiveRelayProtocol = protocol;
                    RememberProtocol(protocol);
                    ConnectionMode = SessionConnectionMode.OnlineRelayCode;
                    _inMatch = false;
                    SyncMatchStarted();

                    OnStatusMessage?.Invoke($"Joined lobby {ActiveSession.Code}!");
                    OnSessionUpdated?.Invoke();
                    return true;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Debug.LogWarning($"[Multiplayer] Join over {protocol} failed: {ex.Message}");
                    await CleanupFailedAttemptAsync();
                    if (IsServicesConfigError(ex) || IsSessionNotFound(ex)) break;
                    Debug.Log($"[Multiplayer] {ProtocolLabel(protocol)} failed - retrying with fallback protocol");
                }
            }

            OnErrorMessage?.Invoke(FormatServicesError(lastError, $"Could not join lobby '{normalizedCode}'."));
            return false;
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

    IEnumerable<RelayProtocol> GetProtocolAttemptOrder()
    {
        if (PlayerPrefs.GetInt(PrefPreferWssKey, 0) == 1)
        {
            yield return RelayProtocol.WSS;
            yield return RelayProtocol.DTLS;
        }
        else
        {
            yield return RelayProtocol.DTLS;
            yield return RelayProtocol.WSS;
        }
    }

    static void RememberProtocol(RelayProtocol protocol)
    {
        // If DTLS was blocked on this network, start with WSS next time and skip the timeout.
        PlayerPrefs.SetInt(PrefPreferWssKey, protocol == RelayProtocol.WSS ? 1 : 0);
        PlayerPrefs.Save();
    }

    static string ProtocolLabel(RelayProtocol p) => p == RelayProtocol.WSS ? "WebSocket (wss:443)" : "DTLS/UDP";

    static void ConfigureTransportForProtocol(RelayProtocol protocol)
    {
        var transport = NetworkManager.Singleton != null ? NetworkManager.Singleton.GetComponent<UnityTransport>() : null;
        if (transport != null)
            transport.UseWebSockets = protocol == RelayProtocol.WSS;
    }

    async Task<bool> WaitForNetworkAsync(bool host)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return false;

        bool transportFailed = false;
        Action onTransportFailure = () => transportFailed = true;
        nm.OnTransportFailure += onTransportFailure;
        try
        {
            var sw = Stopwatch.StartNew();
            float timeout = Mathf.Max(relayConnectTimeoutSeconds, 15f);
            while (sw.Elapsed.TotalSeconds < timeout)
            {
                if (host ? (nm.IsListening && nm.IsServer) : nm.IsConnectedClient)
                    return true;
                if (transportFailed)
                    return false;
                // Client transport already gave up (e.g. UDP blocked) -> stop waiting early.
                if (!host && sw.Elapsed.TotalSeconds > 4.0 && !nm.IsListening && !nm.ShutdownInProgress)
                    return false;
                await Task.Delay(100);
                if (this == null) return false;
            }
            Debug.LogWarning($"[Multiplayer] Network {(host ? "host start" : "client connect")} timed out after {timeout:0}s " +
                             $"(listening={nm.IsListening}, client={nm.IsClient}, connected={nm.IsConnectedClient}).");
            return host ? (nm.IsListening && nm.IsServer) : nm.IsConnectedClient;
        }
        finally
        {
            if (nm != null) nm.OnTransportFailure -= onTransportFailure;
        }
    }

    /// <summary>True while a host/join/LAN connection attempt is in flight (drives the loading spinner).</summary>
    public bool IsConnecting
    {
        get
        {
            if (_isBusy) return true;
            var nm = NetworkManager.Singleton;
            return ConnectionMode == SessionConnectionMode.LocalLan && nm != null &&
                   nm.IsClient && !nm.IsServer && !nm.IsConnectedClient;
        }
    }

    public string LastStatusMessage { get; private set; } = string.Empty;

    /// <summary>Short, player-facing label for the loading overlay ("HOSTING GAME" / "JOINING GAME").</summary>
    public string ConnectingLabel { get; private set; } = "CONNECTING";

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
        transport.UseWebSockets = false;
        transport.SetConnectionData("0.0.0.0", _lanPort, "0.0.0.0");
        RefreshLocalLanIPv4();

        if (!NetworkManager.Singleton.StartHost())
        {
            OnErrorMessage?.Invoke($"Failed to bind LAN Host on port {_lanPort}.");
            return false;
        }

        ConnectionMode = SessionConnectionMode.LocalLan;
        _inMatch = false;
        OnStatusMessage?.Invoke($"LAN Lobby hosted at {LocalLanIPv4}:{_lanPort} - share this IP with players on the same Wi-Fi/Ethernet.");
        OnSessionUpdated?.Invoke();
        return true;
    }

    public bool JoinLanClient(string ipAddress, ushort port, string playerName)
    {
        if (_isBusy || NetworkManager.Singleton == null) return false;
        SetPlayerName(playerName);

        string targetIp = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim();
        ushort targetPort = port > 0 ? port : defaultLanPort;

        // Accept "192.168.1.20:7777" pasted from the host's lobby card.
        int colon = targetIp.LastIndexOf(':');
        if (colon > 0 && ushort.TryParse(targetIp.Substring(colon + 1), out ushort pastedPort) && pastedPort > 0)
        {
            targetPort = pastedPort;
            targetIp = targetIp.Substring(0, colon);
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            OnErrorMessage?.Invoke("UnityTransport missing on NetworkManager.");
            return false;
        }

        _lanPort = targetPort;
        _lanTargetIp = targetIp;
        transport.UseWebSockets = false;
        transport.SetConnectionData(targetIp, targetPort);

        if (!NetworkManager.Singleton.StartClient())
        {
            OnErrorMessage?.Invoke($"Failed to connect to {targetIp}:{targetPort}.");
            return false;
        }

        ConnectionMode = SessionConnectionMode.LocalLan;
        _inMatch = false;
        ConnectingLabel = "JOINING GAME";
        OnStatusMessage?.Invoke($"Connecting to {targetIp}:{targetPort}...");
        OnSessionUpdated?.Invoke();
        return true;
    }

    /// <summary>
    /// Solo play runs as a private host bound to loopback only (no firewall prompt, nobody can join).
    /// Every NetworkVariable/RPC path is then a normal, spawned host path - previously solo wrote to
    /// unspawned NetworkVariables every frame, flooding the console with tens of thousands of warnings
    /// and stalling the editor (which also broke MPPM/Relay testing).
    /// </summary>
    public void StartSoloOffline(string playerName)
    {
        if (_isBusy) return;
        SetPlayerName(playerName);

        var nm = NetworkManager.Singleton;
        var transport = nm != null ? nm.GetComponent<UnityTransport>() : null;
        if (nm != null && transport != null && !nm.IsListening)
        {
            CleanupOfflineSoloPlayer();
            transport.UseWebSockets = false;
            ushort port = FindFreeLoopbackPort();
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            if (nm.StartHost())
            {
                ConnectionMode = SessionConnectionMode.SoloOffline;
                _inMatch = true;
                SetMenuCameraActive(false);
                if (NetworkMatchState.Instance != null && NetworkMatchState.Instance.IsSpawned)
                {
                    NetworkMatchState.Instance.StartMatchOnHost();
                    // Solo starts as a Hider (press H to swap roles).
                    if (NetworkPlayerController.LocalInstance != null)
                        NetworkPlayerController.LocalInstance.SetRole(PlayerRole.Hider);
                }
                OnSessionUpdated?.Invoke();
                return;
            }
            Debug.LogWarning("[Multiplayer] Loopback solo host failed to start; falling back to unnetworked solo.");
        }

        StartSoloUnnetworked();
    }

    static ushort FindFreeLoopbackPort()
    {
        try
        {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            return (ushort)((IPEndPoint)udp.Client.LocalEndPoint).Port;
        }
        catch (Exception)
        {
            return (ushort)UnityEngine.Random.Range(40000, 60000);
        }
    }

    void StartSoloUnnetworked()
    {
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
            SeekerVision.Apply(null, false, 0f); // drop the Seeker's fog / short view distance
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

    /// <summary>
    /// Fully tears down a session attempt: leaves/deletes the UGS session, unregisters callbacks,
    /// shuts NGO down and waits for the transport to release its sockets, resets the transport to
    /// defaults, and unfreezes cursor/input so the menu is usable again.
    /// </summary>
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

        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            if (nm.IsListening || nm.IsClient || nm.IsServer)
                nm.Shutdown(true);

            var sw = Stopwatch.StartNew();
            while (nm != null && nm.ShutdownInProgress && sw.Elapsed.TotalSeconds < 5.0)
            {
                await Task.Delay(50);
                if (this == null) return;
            }

            var transport = nm != null ? nm.GetComponent<UnityTransport>() : null;
            if (transport != null)
            {
                transport.UseWebSockets = false;
                transport.SetConnectionData("127.0.0.1", defaultLanPort);
            }
        }

        if (ConnectionMode != SessionConnectionMode.SoloOffline)
        {
            ConnectionMode = SessionConnectionMode.None;
            _inMatch = false;
            SeekerVision.Apply(null, false, 0f); // drop the Seeker's fog / short view distance
            SetMenuCameraActive(true);
        }

        if (NetworkPlayerController.LocalInstance != null)
            NetworkPlayerController.LocalInstance.SetPaused(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        OnSessionUpdated?.Invoke();
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

    static string DetectLocalIPv4()
    {
        try
        {
            // The interface the OS would route outbound traffic through (UDP connect sends no packets).
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint ep && !IPAddress.IsLoopback(ep.Address) && !ep.Address.Equals(IPAddress.Any))
                return ep.Address.ToString();
        }
        catch (Exception) { /* isolated LAN without a default route - fall through */ }

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                var t = ni.NetworkInterfaceType;
                if (t != NetworkInterfaceType.Ethernet && t != NetworkInterfaceType.Wireless80211 && t != NetworkInterfaceType.GigabitEthernet)
                    continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
                        return addr.Address.ToString();
                }
            }
        }
        catch (Exception) { }

        return "127.0.0.1";
    }

    static bool IsServicesConfigError(Exception ex)
    {
        string msg = ex?.Message ?? string.Empty;
        return msg.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 ||
               msg.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0 ||
               msg.IndexOf("not enabled", StringComparison.OrdinalIgnoreCase) >= 0 ||
               msg.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsSessionNotFound(Exception ex)
    {
        string msg = ex?.Message ?? string.Empty;
        return msg.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
               msg.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string FormatServicesError(Exception ex, string fallback)
    {
        if (IsServicesConfigError(ex))
            return $"{fallback} Enable Lobby & Relay on Unity Cloud Dashboard first (or use LAN/Solo below).";
        return $"{fallback} {ex?.Message}";
    }
}
