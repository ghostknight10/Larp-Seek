using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls all uGUI screens for Larp-Seek: Main Menu, Join Code modal, LAN modal,
/// Controls modal, Lobby Room, in-match HUD, and Pause Menu.
/// </summary>
public class MultiplayerMenuUI : MonoBehaviour
{
    public static MultiplayerMenuUI Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] GameObject joinModalPanel;
    [SerializeField] GameObject lanModalPanel;
    [SerializeField] GameObject controlsModalPanel;
    [SerializeField] GameObject lobbyPanel;
    [SerializeField] GameObject hudPanel;
    [SerializeField] GameObject pausePanel;

    [Header("Main Menu Widgets")]
    [SerializeField] TMP_InputField playerNameInput;
    [SerializeField] UnityEngine.UI.Button hostOnlineButton;
    [SerializeField] UnityEngine.UI.Button openJoinModalButton;
    [SerializeField] UnityEngine.UI.Button playSoloButton;
    [SerializeField] UnityEngine.UI.Button openLanModalButton;
    [SerializeField] UnityEngine.UI.Button openControlsModalButton;
    [SerializeField] UnityEngine.UI.Button quitAppButton;
    [SerializeField] TMP_Text mainStatusText;

    [Header("Join Modal Widgets")]
    [SerializeField] TMP_InputField joinCodeInput;
    [SerializeField] UnityEngine.UI.Button confirmJoinButton;
    [SerializeField] UnityEngine.UI.Button cancelJoinButton;
    [SerializeField] TMP_Text joinStatusText;

    [Header("LAN Modal Widgets")]
    [SerializeField] TMP_InputField lanIpInput;
    [SerializeField] TMP_InputField lanPortInput;
    [SerializeField] UnityEngine.UI.Button hostLanButton;
    [SerializeField] UnityEngine.UI.Button joinLanButton;
    [SerializeField] UnityEngine.UI.Button closeLanButton;
    [SerializeField] TMP_Text lanStatusText;

    [Header("Controls Modal Widgets")]
    [SerializeField] UnityEngine.UI.Button closeControlsButton;

    [Header("Lobby Widgets")]
    [SerializeField] TMP_Text lobbyModeText;
    [SerializeField] TMP_Text lobbyCodeText;
    [SerializeField] UnityEngine.UI.Button copyCodeButton;
    [SerializeField] TMP_Text copyCodeButtonText;
    [SerializeField] TMP_Text lobbyCountText;
    [SerializeField] TMP_Text lobbyPlayerListText;
    [SerializeField] UnityEngine.UI.Button startMatchButton;
    [SerializeField] TMP_Text startMatchButtonText;
    [SerializeField] UnityEngine.UI.Button leaveLobbyButton;

    [Header("HUD Widgets")]
    [SerializeField] TMP_Text hudCodeText;
    [SerializeField] TMP_Text hudMorphText;
    [SerializeField] TMP_Text hudPlayersText;

    [Header("Pause Widgets")]
    [SerializeField] UnityEngine.UI.Button resumeButton;
    [SerializeField] UnityEngine.UI.Button pauseCopyCodeButton;
    [SerializeField] TMP_Text pauseCopyCodeText;
    [SerializeField] UnityEngine.UI.Button disconnectButton;
    [SerializeField] UnityEngine.UI.Button pauseQuitButton;

    [Header("Audio Hook")]
    [SerializeField] UnityEvent onButtonClickSfx;

    bool _isPaused;
    float _copyFeedbackTimer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        WireButtons();
    }

    void Start()
    {
        var sessionMgr = MultiplayerSessionManager.Instance;
        if (sessionMgr != null)
        {
            sessionMgr.OnSessionUpdated += RefreshView;
            sessionMgr.OnStatusMessage += HandleStatusMessage;
            sessionMgr.OnErrorMessage += HandleErrorMessage;

            if (playerNameInput != null)
                playerNameInput.text = sessionMgr.LocalPlayerName;
        }

        if (lanIpInput != null && string.IsNullOrEmpty(lanIpInput.text))
            lanIpInput.text = "127.0.0.1";
        if (lanPortInput != null && string.IsNullOrEmpty(lanPortInput.text))
            lanPortInput.text = "7777";

        RefreshView();
    }

    void OnDestroy()
    {
        var sessionMgr = MultiplayerSessionManager.Instance;
        if (sessionMgr != null)
        {
            sessionMgr.OnSessionUpdated -= RefreshView;
            sessionMgr.OnStatusMessage -= HandleStatusMessage;
            sessionMgr.OnErrorMessage -= HandleErrorMessage;
        }
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (_copyFeedbackTimer > 0f)
        {
            _copyFeedbackTimer -= Time.unscaledDeltaTime;
            if (_copyFeedbackTimer <= 0f)
                ResetCopyLabels();
        }

        var sessionMgr = MultiplayerSessionManager.Instance;
        if (sessionMgr != null && sessionMgr.IsInMatch)
        {
            UpdateHudValues();

            bool escPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                escPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (escPressed)
                SetPaused(!_isPaused);
        }
    }

    void WireButtons()
    {
        AddClick(hostOnlineButton, OnHostOnlineClicked);
        AddClick(openJoinModalButton, () => OpenSubModal(joinModalPanel));
        AddClick(playSoloButton, OnPlaySoloClicked);
        AddClick(openLanModalButton, () => OpenSubModal(lanModalPanel));
        AddClick(openControlsModalButton, () => OpenSubModal(controlsModalPanel));
        AddClick(quitAppButton, OnQuitClicked);

        AddClick(confirmJoinButton, OnConfirmJoinClicked);
        AddClick(cancelJoinButton, () => OpenSubModal(null));

        AddClick(hostLanButton, OnHostLanClicked);
        AddClick(joinLanButton, OnJoinLanClicked);
        AddClick(closeLanButton, () => OpenSubModal(null));

        AddClick(closeControlsButton, () => OpenSubModal(null));

        AddClick(copyCodeButton, OnCopyCodeClicked);
        AddClick(startMatchButton, OnStartMatchClicked);
        AddClick(leaveLobbyButton, OnLeaveLobbyClicked);

        AddClick(resumeButton, () => SetPaused(false));
        AddClick(pauseCopyCodeButton, OnCopyCodeClicked);
        AddClick(disconnectButton, OnLeaveLobbyClicked);
        AddClick(pauseQuitButton, OnQuitClicked);
    }

    void AddClick(UnityEngine.UI.Button btn, UnityAction action)
    {
        if (btn == null) return;
        btn.onClick.AddListener(() =>
        {
            onButtonClickSfx?.Invoke();
            action?.Invoke();
        });
    }

    public void RefreshView()
    {
        var sessionMgr = MultiplayerSessionManager.Instance;
        if (sessionMgr == null || sessionMgr.ConnectionMode == SessionConnectionMode.None)
        {
            _isPaused = false;
            SetActivePanel(mainMenuPanel, true);
            SetActivePanel(lobbyPanel, false);
            SetActivePanel(hudPanel, false);
            SetActivePanel(pausePanel, false);
            SetActivePanel(joinModalPanel, false);
            SetActivePanel(lanModalPanel, false);
            SetActivePanel(controlsModalPanel, false);
            RefreshInteractables(false);
            return;
        }

        if (!sessionMgr.IsInMatch)
        {
            _isPaused = false;
            SetActivePanel(mainMenuPanel, false);
            SetActivePanel(joinModalPanel, false);
            SetActivePanel(lanModalPanel, false);
            SetActivePanel(controlsModalPanel, false);
            SetActivePanel(lobbyPanel, true);
            SetActivePanel(hudPanel, false);
            SetActivePanel(pausePanel, false);
            PopulateLobby(sessionMgr);
            return;
        }

        SetActivePanel(mainMenuPanel, false);
        SetActivePanel(joinModalPanel, false);
        SetActivePanel(lanModalPanel, false);
        SetActivePanel(controlsModalPanel, false);
        SetActivePanel(lobbyPanel, false);
        SetActivePanel(hudPanel, true);
        SetActivePanel(pausePanel, _isPaused);

        if (pauseCopyCodeButton != null)
            pauseCopyCodeButton.gameObject.SetActive(sessionMgr.ConnectionMode != SessionConnectionMode.SoloOffline);

        UpdateHudValues();
    }

    public void SetPaused(bool paused)
    {
        var sessionMgr = MultiplayerSessionManager.Instance;
        if (sessionMgr == null || !sessionMgr.IsInMatch) return;

        _isPaused = paused;
        SetActivePanel(pausePanel, _isPaused);
        if (NetworkPlayerController.LocalInstance != null)
            NetworkPlayerController.LocalInstance.SetPaused(_isPaused);
    }

    void OpenSubModal(GameObject targetModal)
    {
        SetActivePanel(joinModalPanel, targetModal == joinModalPanel);
        SetActivePanel(lanModalPanel, targetModal == lanModalPanel);
        SetActivePanel(controlsModalPanel, targetModal == controlsModalPanel);
        if (targetModal == joinModalPanel && joinStatusText != null)
            joinStatusText.text = string.Empty;
        if (targetModal == lanModalPanel && lanStatusText != null)
            lanStatusText.text = string.Empty;
    }

    async void OnHostOnlineClicked()
    {
        var sessionMgr = MultiplayerSessionManager.Instance;
        if (sessionMgr == null) return;
        RefreshInteractables(true);
        await sessionMgr.HostOnlineLobbyAsync(GetPlayerNameInput());
        RefreshInteractables(false);
        RefreshView();
    }

    async void OnConfirmJoinClicked()
    {
        var sessionMgr = MultiplayerSessionManager.Instance;
        if (sessionMgr == null) return;
        RefreshInteractables(true);
        string code = joinCodeInput != null ? joinCodeInput.text : string.Empty;
        bool ok = await sessionMgr.JoinOnlineLobbyByCodeAsync(code, GetPlayerNameInput());
        RefreshInteractables(false);
        if (ok) RefreshView();
    }

    void OnPlaySoloClicked()
    {
        var sessionMgr = MultiplayerSessionManager.Instance;
        if (sessionMgr == null) return;
        sessionMgr.StartSoloOffline(GetPlayerNameInput());
        RefreshView();
    }

    void OnHostLanClicked()
    {
        var sessionMgr = MultiplayerSessionManager.Instance;
        if (sessionMgr == null) return;
        ushort port = ParsePort();
        if (sessionMgr.StartLanHost(GetPlayerNameInput(), port))
            RefreshView();
    }

    void OnJoinLanClicked()
    {
        var sessionMgr = MultiplayerSessionManager.Instance;
        if (sessionMgr == null) return;
        string ip = lanIpInput != null ? lanIpInput.text : "127.0.0.1";
        ushort port = ParsePort();
        if (sessionMgr.JoinLanClient(ip, port, GetPlayerNameInput()))
            RefreshView();
    }

    void OnStartMatchClicked()
    {
        MultiplayerSessionManager.Instance?.StartMatchFromLobby();
    }

    async void OnLeaveLobbyClicked()
    {
        var sessionMgr = MultiplayerSessionManager.Instance;
        if (sessionMgr == null) return;
        RefreshInteractables(true);
        await sessionMgr.LeaveSessionToMenuAsync();
        RefreshInteractables(false);
        RefreshView();
    }

    void OnCopyCodeClicked()
    {
        var sessionMgr = MultiplayerSessionManager.Instance;
        if (sessionMgr == null) return;
        string code = sessionMgr.CurrentJoinCode;
        if (string.IsNullOrEmpty(code)) return;

        GUIUtility.systemCopyBuffer = code;
        _copyFeedbackTimer = 1.6f;
        if (copyCodeButtonText != null) copyCodeButtonText.text = "COPIED!";
        if (pauseCopyCodeText != null) pauseCopyCodeText.text = "COPIED CODE!";
    }

    void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void PopulateLobby(MultiplayerSessionManager sessionMgr)
    {
        if (lobbyModeText != null)
        {
            lobbyModeText.text = sessionMgr.ConnectionMode == SessionConnectionMode.OnlineRelayCode
                ? "ONLINE LOBBY // RELAY"
                : "LOCAL LAN SESSION";
        }

        if (lobbyCodeText != null)
            lobbyCodeText.text = string.IsNullOrEmpty(sessionMgr.CurrentJoinCode) ? "------" : sessionMgr.CurrentJoinCode;

        List<LobbyPlayerSummary> players = sessionMgr.GetLobbyPlayers();
        if (lobbyCountText != null)
            lobbyCountText.text = $"{players.Count} PLAYER{(players.Count == 1 ? "" : "S")} CONNECTED";

        if (lobbyPlayerListText != null)
        {
            var lines = new List<string>(players.Count);
            for (int i = 0; i < players.Count; i++)
            {
                LobbyPlayerSummary p = players[i];
                string tags = p.IsHost ? " [HOST]" : string.Empty;
                if (p.IsLocal) tags += " (YOU)";
                lines.Add($">  {p.Name}{tags}");
            }
            lobbyPlayerListText.text = lines.Count > 0 ? string.Join("\n", lines) : ">  Connecting...";
        }

        bool isHost = sessionMgr.IsHostPlayer;
        if (startMatchButton != null)
            startMatchButton.interactable = isHost;
        if (startMatchButtonText != null)
            startMatchButtonText.text = isHost ? "START MATCH" : "WAITING FOR HOST...";
    }

    void UpdateHudValues()
    {
        var sessionMgr = MultiplayerSessionManager.Instance;
        if (sessionMgr == null) return;

        if (hudCodeText != null)
        {
            string code = sessionMgr.CurrentJoinCode;
            hudCodeText.text = sessionMgr.ConnectionMode == SessionConnectionMode.SoloOffline
                ? "MODE: SOLO OFFLINE"
                : $"LOBBY CODE: {code}";
        }

        if (hudMorphText != null)
        {
            string form = NetworkPlayerController.LocalInstance != null
                ? NetworkPlayerController.LocalInstance.CurrentMorphName
                : "Human";
            hudMorphText.text = $"FORM: {form.ToUpperInvariant()}";
        }

        if (hudPlayersText != null)
        {
            int count = Mathf.Max(1, NetworkPlayerController.ActivePlayers.Count);
            hudPlayersText.text = $"PLAYERS: {count}";
        }
    }

    void HandleStatusMessage(string msg)
    {
        SetMessageText(mainStatusText, msg, new Color(0.82f, 0.90f, 1.0f));
        SetMessageText(joinStatusText, msg, new Color(0.82f, 0.90f, 1.0f));
        SetMessageText(lanStatusText, msg, new Color(0.82f, 0.90f, 1.0f));
    }

    void HandleErrorMessage(string msg)
    {
        Color errCol = new Color(1.0f, 0.45f, 0.40f);
        SetMessageText(mainStatusText, msg, errCol);
        SetMessageText(joinStatusText, msg, errCol);
        SetMessageText(lanStatusText, msg, errCol);
    }

    static void SetMessageText(TMP_Text label, string text, Color color)
    {
        if (label == null) return;
        label.color = color;
        label.text = text;
    }

    void RefreshInteractables(bool busy)
    {
        bool interactable = !busy;
        if (hostOnlineButton != null) hostOnlineButton.interactable = interactable;
        if (openJoinModalButton != null) openJoinModalButton.interactable = interactable;
        if (playSoloButton != null) playSoloButton.interactable = interactable;
        if (openLanModalButton != null) openLanModalButton.interactable = interactable;
        if (confirmJoinButton != null) confirmJoinButton.interactable = interactable;
        if (hostLanButton != null) hostLanButton.interactable = interactable;
        if (joinLanButton != null) joinLanButton.interactable = interactable;
    }

    void ResetCopyLabels()
    {
        if (copyCodeButtonText != null) copyCodeButtonText.text = "COPY CODE";
        if (pauseCopyCodeText != null) pauseCopyCodeText.text = "COPY LOBBY CODE";
    }

    string GetPlayerNameInput()
    {
        return playerNameInput != null ? playerNameInput.text : "Player";
    }

    ushort ParsePort()
    {
        if (lanPortInput != null && ushort.TryParse(lanPortInput.text, out ushort p) && p > 0)
            return p;
        return 7777;
    }

    static void SetActivePanel(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }
}
