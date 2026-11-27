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
/// Controls modal, Lobby Room, in-match HUD (with 5-minute timer & beacon progress), and Pause Menu.
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

    [Header("HUD Widgets - Top Left Info")]
    [SerializeField] TMP_Text hudCodeText;
    [SerializeField] TMP_Text hudMorphText;
    [SerializeField] TMP_Text hudPlayersText;

    [Header("HUD Widgets - Top Center Timer & Beacons")]
    [SerializeField] TMP_Text hudMatchTimerText;
    [SerializeField] TMP_Text hudBeaconProgressText;
    [SerializeField] UnityEngine.UI.Image hudBeaconProgressBar;

    [Header("HUD Widgets - Role & Status")]
    [SerializeField] TMP_Text hudRoleText;
    [SerializeField] TMP_Text hudScoreText;
    [SerializeField] TMP_Text hudAbilitiesText;
    [SerializeField] TMP_Text hudReviveText;

    [Header("HUD Widgets - Overlays")]
    [SerializeField] GameObject hudBlindOverlay;
    [SerializeField] TMP_Text hudBlindText;
    [SerializeField] GameObject hudMatchOverPanel;
    [SerializeField] TMP_Text hudMatchOverTitleText;
    [SerializeField] TMP_Text hudMatchOverSubtitleText;

    [Header("Pause Widgets")]
    [SerializeField] UnityEngine.UI.Button resumeButton;
    [SerializeField] UnityEngine.UI.Button pauseCopyCodeButton;
    [SerializeField] TMP_Text pauseCopyCodeText;
    [SerializeField] UnityEngine.UI.Button disconnectButton;
    [SerializeField] UnityEngine.UI.Button pauseQuitButton;

    [Header("Audio Hook")]
    [SerializeField] UnityEvent onButtonClickSfx;

    static readonly Color ColAccentGold = new Color(0.95f, 0.70f, 0.12f, 1f);

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

        // HUD stays crisp at native resolution: overlay canvases are drawn after the low-res Point upscale.
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.isRootCanvas) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.pixelPerfect = true; }

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

        RebuildPixelFonts();
        CachePixelSprites();
        ApplyMainMenuStyling();
        ApplyPixelModalStyling();
        RebuildControlsModal();
        FitAllTexts();
        RefreshView();
        SetupGraphicsButtons();
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
        UpdateLoadingOverlay(sessionMgr);
        UpdateVoiceOverlay(sessionMgr);
        UpdatePingAndSettings(sessionMgr);
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
            // No pause menu on top of the match-over screen.
            if (escPressed && !(hudMatchOverPanel != null && hudMatchOverPanel.activeSelf))
                SetPaused(!_isPaused);
        }
    }

    PingDisplay _ping;
    MatchSettingsPanel _settingsPanel;

    void UpdatePingAndSettings(MultiplayerSessionManager mgr)
    {
        bool networked = mgr != null && !mgr.IsConnecting &&
            (mgr.ConnectionMode == SessionConnectionMode.OnlineRelayCode || mgr.ConnectionMode == SessionConnectionMode.LocalLan);
        if (networked && _ping == null) _ping = PingDisplay.Create(transform, PixelFont);
        if (_ping != null) { _ping.SetVisible(networked); _ping.transform.SetAsLastSibling(); }

        bool inLobby = mgr != null && !mgr.IsInMatch && mgr.ConnectionMode != SessionConnectionMode.None &&
                       lobbyPanel != null && lobbyPanel.activeInHierarchy;
        if (inLobby && _settingsPanel == null)
        {
            _settingsPanel = MatchSettingsPanel.Create(lobbyPanel.transform, _sprCard, _sprCoral, PixelFont, _headerTextColor);
            PlaceSettingsPanel();
        }
        if (_settingsPanel != null && _settingsPanel.gameObject.activeSelf != inLobby)
        {
            _settingsPanel.gameObject.SetActive(inLobby);
            if (inLobby) PlaceSettingsPanel();
        }
    }

    /// <summary>Puts the settings card beside the lobby card, shifting the lobby card left so both fit.</summary>
    void PlaceSettingsPanel()
    {
        var card = lobbyPanel.transform.Find("LobbyCard") as RectTransform;
        var srt = (RectTransform)_settingsPanel.transform;
        float gap = 28f;
        if (card == null) { srt.anchoredPosition = new Vector2(-srt.sizeDelta.x * 0.5f, 0f); return; }
        float cardW = card.rect.width, setW = srt.sizeDelta.x;
        float total = cardW + gap + setW;
        // Card is centred on its own pivot; lay the pair out centred as a group.
        float cardCentreX = -total * 0.5f + cardW * 0.5f;
        card.anchoredPosition = new Vector2(cardCentreX, card.anchoredPosition.y);
        srt.anchorMin = srt.anchorMax = card.anchorMin;
        srt.anchoredPosition = new Vector2(cardCentreX + cardW * 0.5f + gap, card.anchoredPosition.y);
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

    /// <summary>
    /// Graphics are locked to retro 240p, so there is no quality switch: remove any graphics button
    /// left in the Controls / Pause cards (scene-baked or cloned by older builds).
    /// </summary>
    void SetupGraphicsButtons()
    {
        HideCardButton(controlsModalPanel, "ControlsModalPanel_Card", "GraphicsQualityButton");
        HideCardButton(pausePanel, "PausePanel_Card", "PauseGraphicsButton");
    }

    static void HideCardButton(GameObject panel, string cardName, string buttonName)
    {
        if (panel == null) return;
        var card = panel.transform.Find(cardName);
        var btn = card != null ? card.Find(buttonName) : null;
        if (btn != null) btn.gameObject.SetActive(false);
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
            SetActivePanel(hudMatchOverPanel, false);
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
            SetActivePanel(hudMatchOverPanel, false);
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
        ApplyHudTopLayout();

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
        {
            var mgr = MultiplayerSessionManager.Instance;
            if (mgr != null) mgr.RefreshLocalLanIPv4();
            lanStatusText.color = new Color(0.82f, 0.90f, 1.0f);
            lanStatusText.text = mgr != null
                ? $"THIS PC'S LAN IP: {mgr.LocalLanIPv4}  (others on your Wi-Fi/Ethernet join with this IP)"
                : string.Empty;
        }
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
        // Hide the in-match overlays immediately (the match-over panel is a sibling of the HUD, not a child).
        _isPaused = false;
        SetActivePanel(pausePanel, false);
        SetActivePanel(hudMatchOverPanel, false);
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
            if (sessionMgr.ConnectionMode == SessionConnectionMode.OnlineRelayCode)
                lobbyModeText.text = sessionMgr.ActiveRelayProtocol == Unity.Services.Multiplayer.RelayProtocol.WSS
                    ? "ONLINE LOBBY // RELAY (WSS:443)"
                    : "ONLINE LOBBY // RELAY (DTLS)";
            else
                lobbyModeText.text = sessionMgr.IsHostPlayer
                    ? $"LOCAL LAN SESSION // HOST IP {sessionMgr.LocalLanIPv4}:{sessionMgr.LanPort}"
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

        // Top Left basic info
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

        // Top Center 5-minute timer & Beacon status
        var matchState = NetworkMatchState.Instance;
        if (matchState != null)
        {
            float t = matchState.MatchTimeRemaining.Value;
            int mins = Mathf.FloorToInt(t / 60f);
            int secs = Mathf.FloorToInt(t % 60f);
            if (hudMatchTimerText != null)
            {
                hudMatchTimerText.text = $"{mins:00}:{secs:00}";
                if (t <= 30f)
                    hudMatchTimerText.color = new Color(1.0f, 0.35f, 0.35f);
                else
                    hudMatchTimerText.color = Color.white;
            }

            float beaconTime = matchState.BeaconTotalStayTime.Value;
            float targetTime = NetworkMatchState.BeaconTargetSeconds;
            if (hudBeaconProgressText != null)
            {
                string s1 = matchState.Station1Capturing.Value ? "<color=#FFD126>CAPTURING</color>" : "<color=#9FB4CC>IDLE</color>";
                string s2 = matchState.Station2Capturing.Value ? "<color=#FFD126>CAPTURING</color>" : "<color=#9FB4CC>IDLE</color>";
                hudBeaconProgressText.text =
                    $"<size=150%><b>BEACON {beaconTime:00.0}s</b></size> <color=#9FB4CC>/ {targetTime:F0}s</color>\n" +
                    $"<size=85%>LOUNGE: {s1}   CAFETERIA: {s2}</size>";
            }
            if (hudBeaconProgressBar != null)
            {
                hudBeaconProgressBar.fillAmount = Mathf.Clamp01(beaconTime / targetTime);
            }

            // Match Outcome
            var outcome = (MatchOutcomeType)matchState.MatchOutcome.Value;
            if (outcome != MatchOutcomeType.InProgress)
            {
                if (hudMatchOverPanel != null && !hudMatchOverPanel.activeSelf)
                {
                    hudMatchOverPanel.SetActive(true);
                    EnsureMatchOverMenuButton();
                    _isPaused = false;
                    SetActivePanel(pausePanel, false);
                    // Free the cursor so the Back To Menu button can be clicked.
                    if (NetworkPlayerController.LocalInstance != null)
                        NetworkPlayerController.LocalInstance.SetPaused(true);
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                if (hudMatchOverTitleText != null)
                {
                    hudMatchOverTitleText.text = outcome == MatchOutcomeType.HidersWin ? "HIDERS WIN!" : "HUNTER WINS!";
                    hudMatchOverTitleText.color = outcome == MatchOutcomeType.HidersWin
                        ? new Color(0.2f, 1.0f, 0.4f)
                        : new Color(1.0f, 0.35f, 0.35f);
                }
                if (hudMatchOverSubtitleText != null)
                {
                    hudMatchOverSubtitleText.text = outcome == MatchOutcomeType.HidersWin
                        ? "Hiders held the beacon stations for 90 seconds!"
                        : (t <= 0f ? $"{matchState.SetMatchMinutes.Value:0} minutes elapsed! Hiders ran out of time." : "All hiders were eliminated!");
                }
            }
            else
            {
                if (hudMatchOverPanel != null) hudMatchOverPanel.SetActive(false);
            }
        }

        // Local Player Role & Abilities
        var localPlayer = NetworkPlayerController.LocalInstance;
        if (localPlayer != null)
        {
            bool isHunter = localPlayer.CurrentRole == PlayerRole.Hunter;
            if (hudRoleText != null)
            {
                hudRoleText.text = isHunter ? "ROLE: HUNTER [SEEKER]" : "ROLE: HIDER";
                hudRoleText.color = isHunter ? new Color(1.0f, 0.45f, 0.35f) : new Color(0.35f, 0.85f, 1.0f);
            }

            if (hudScoreText != null)
            {
                hudScoreText.text = $"SCORE: {localPlayer.Score.Value} PTS";
            }

            if (hudAbilitiesText != null)
            {
                if (isHunter)
                {
                    float cd = localPlayer.ShootCooldownRemaining;
                    string gun = cd > 0f ? $"<color=#FF8A5A>RELOADING {cd:0.0}s</color>" : "<color=#66FF88>READY</color>";
                    hudAbilitiesText.text = $"[LMB] Shoot: {gun}\nFind & eliminate every hider!";
                }
                else
                {
                    string whistleStatus = localPlayer.WhistleCooldownRemaining <= 0f
                        ? "READY (+10pts)"
                        : $"{localPlayer.WhistleCooldownRemaining:F1}s";
                    string mandatoryStatus = $"{localPlayer.MandatoryWhistleRemaining:F0}s";
                    int decoys = localPlayer.DecoyCount;
                    hudAbilitiesText.text = $"[F] Whistle: {whistleStatus} | Mandatory Whistle: {mandatoryStatus}\n[RMB] Decoys: {decoys}/2 | Jump on Hunter head to blind 5s!";
                }
            }

            // Downed / Refrigerator Alert
            bool showRing = false;
            float ringFill = 0f;
            if (matchState != null && !isHunter && !localPlayer.IsEliminated.Value &&
                matchState.DownedHidersCount.Value > 0 && RefrigeratorRevivalStation.Instance != null &&
                RefrigeratorRevivalStation.Instance.IsPlayerInside(localPlayer.transform.position))
            {
                showRing = true;
                ringFill = Mathf.Clamp01(matchState.ReviveProgress.Value / NetworkMatchState.ReviveTargetSeconds);
            }
            UpdateReviveRing(showRing, ringFill);

            bool hidePhase = matchState != null && matchState.IsHidePhase;
            float hideLeft = matchState != null ? matchState.HidePhaseRemaining.Value : 0f;

            if (hidePhase && !isHunter)
                SetBanner($"HIDE NOW!  SEEKER RELEASED IN {Mathf.CeilToInt(hideLeft)}s", new Color(1.0f, 0.85f, 0.2f));
            else if (showRing)
            {
                float remaining = Mathf.Max(0f, NetworkMatchState.ReviveTargetSeconds - matchState.ReviveProgress.Value);
                SetBanner($"REVIVING TEAMMATES... ({Mathf.CeilToInt(remaining):00}s)  STAY IN THE CIRCLE!", new Color(0.2f, 1.0f, 0.5f));
            }
            else if (localPlayer.IsEliminated.Value)
            {
                const string flyHint = "\n<size=75%><color=#9FD8FF>SPECTATING: WASD FLY  SPACE/CTRL UP/DOWN  SHIFT FAST</color></size>";
                SetBanner((matchState != null && matchState.ReviveProgress.Value > 0f
                    ? $"YOU ARE DOWNED - REVIVE IN PROGRESS {matchState.ReviveProgress.Value:F0}s / 20s"
                    : "YOU ARE DOWNED - TEAMMATES CAN REVIVE YOU AT THE TUCK SHOP FRIDGE") + flyHint, new Color(1.0f, 0.35f, 0.35f));
            }
            else if (!isHunter && localPlayer.IsSpectating)
                SetBanner("SPECTATE MODE - LOOK AROUND FREELY   [E] EXIT", new Color(0.6f, 0.85f, 1.0f));
            else if (!isHunter && localPlayer.SpectateDeniedTimer > 0f)
                SetBanner("STAND STILL TO ENTER SPECTATE MODE [E]", new Color(1.0f, 0.7f, 0.4f));
            else if (matchState != null && matchState.DownedHidersCount.Value > 0 && !isHunter)
            {
                if (matchState.ReviveProgress.Value > 0f)
                    SetBanner(matchState.ReviveChanneling.Value
                        ? $"TEAMMATE REVIVING AT FRIDGE: {matchState.ReviveProgress.Value:F0}s / 20s"
                        : $"REVIVE STALLED {matchState.ReviveProgress.Value:F0}s / 20s - RETURN TO THE FRIDGE!", new Color(0.2f, 1.0f, 0.5f));
                else
                    SetBanner($"!! {matchState.DownedHidersCount.Value} TEAMMATE(S) DOWNED - REVIVE AT THE TUCK SHOP FRIDGE", new Color(1.0f, 0.85f, 0.2f));
            }
            else
                SetBanner(null, Color.white);

            UpdateVoiceHud();
            UpdateCrosshairAndMinimap(localPlayer, matchState);

            // Hunter Blind Overlay (stomp blind, or the opening hide phase)
            if (hudBlindOverlay != null)
            {
                bool frozen = isHunter && localPlayer.IsHunterFrozen;
                bool isBlind = isHunter && (localPlayer.IsBlinded.Value || frozen);
                if (hudBlindOverlay.activeSelf != isBlind) hudBlindOverlay.SetActive(isBlind);
                if (isBlind && hudBlindText != null)
                {
                    if (PixelFont != null && hudBlindText.font != PixelFont) hudBlindText.font = PixelFont;
                    hudBlindText.lineSpacing = 30f;
                    hudBlindText.text = frozen
                        ? $"<size=140%>HIDERS ARE HIDING</size>\n\nYou will be released in\n<size=220%><color=#FFD126>{Mathf.CeilToInt(hideLeft)}</color></size>"
                        : $"BLINDED!\nA hider jumped on your head!\n({localPlayer.BlindTimeRemaining.Value:F1}s)";
                }
            }
        }
    }

    // ------------------------------------------------------------------ Runtime-built HUD widgets

    /// <summary>Shows/hides the HUD banner. The banner's parent (ReviveBanner) is disabled in the scene,
    /// so toggling only the text never displayed anything - toggle the whole banner instead.</summary>
    void SetBanner(string text, Color color)
    {
        if (hudReviveText == null) return;
        var banner = hudReviveText.transform.parent != null && hudReviveText.transform.parent.name == "ReviveBanner"
            ? hudReviveText.transform.parent.gameObject
            : hudReviveText.gameObject;
        bool show = !string.IsNullOrEmpty(text);
        if (banner.activeSelf != show) banner.SetActive(show);
        if (!hudReviveText.gameObject.activeSelf) hudReviveText.gameObject.SetActive(true);
        if (!show) return;
        hudReviveText.text = text;
        hudReviveText.color = color;
    }

    bool _hudLayoutApplied;

    /// <summary>
    /// Top-centre widget was too cramped (timer + 2-line beacon text + bar in 140 px). Give it room:
    /// padding, a bigger timer pill, a large bold beacon readout, and push the banner below it.
    /// </summary>
    void ApplyHudTopLayout()
    {
        if (_hudLayoutApplied || hudPanel == null) return;
        _hudLayoutApplied = true;

        var widget = hudPanel.transform.Find("HudTopCenterWidget") as RectTransform;
        if (widget != null) widget.sizeDelta = new Vector2(600f, 222f);

        var pill = widget != null ? widget.Find("TimerPill") as RectTransform : null;
        if (pill != null) PlaceTop(pill, -18f, new Vector2(300f, 70f));
        if (hudMatchTimerText != null)
        {
            hudMatchTimerText.enableAutoSizing = false;
            hudMatchTimerText.fontSize = 40f;
            hudMatchTimerText.fontStyle = FontStyles.Bold;
            hudMatchTimerText.characterSpacing = 4f;
            hudMatchTimerText.alignment = TextAlignmentOptions.Center;
        }

        if (hudBeaconProgressText != null)
        {
            PlaceTop(hudBeaconProgressText.rectTransform, -104f, new Vector2(548f, 72f));
            hudBeaconProgressText.enableAutoSizing = false;
            hudBeaconProgressText.fontSize = 17f;
            hudBeaconProgressText.lineSpacing = 22f;
            hudBeaconProgressText.alignment = TextAlignmentOptions.Center;
            hudBeaconProgressText.textWrappingMode = TextWrappingModes.NoWrap;
            hudBeaconProgressText.overflowMode = TextOverflowModes.Overflow;
            var le = hudBeaconProgressText.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le != null) le.ignoreLayout = true;
        }

        var barBg = widget != null ? widget.Find("BeaconProgressBarBg") as RectTransform : null;
        if (barBg != null)
        {
            PlaceTop(barBg, -184f, new Vector2(540f, 24f));
            var le = barBg.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le != null) le.ignoreLayout = true;
        }

        // Banner sits below the widget with comfortable padding and a readable size.
        var banner = hudReviveText != null ? hudReviveText.transform.parent as RectTransform : null;
        if (banner != null && banner.name == "ReviveBanner")
        {
            PlaceTop(banner, -16f - 222f - 14f, new Vector2(760f, 52f));
            hudReviveText.enableAutoSizing = true;
            hudReviveText.fontSizeMax = 17f;
            hudReviveText.fontSizeMin = 11f;
            hudReviveText.alignment = TextAlignmentOptions.Center;
            hudReviveText.margin = new Vector4(16f, 6f, 16f, 6f);
        }
    }

    static void PlaceTop(RectTransform rt, float y, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = size;
    }

    UnityEngine.UI.Button _matchOverMenuButton;

    void EnsureMatchOverMenuButton()
    {
        if (_matchOverMenuButton != null || hudMatchOverPanel == null) return;
        var card = hudMatchOverPanel.transform.Find("MatchOverPanel_Card") ?? hudMatchOverPanel.transform;
        var template = disconnectButton != null ? disconnectButton : resumeButton;
        if (template == null) return;

        var go = Instantiate(template.gameObject, card, false);
        go.name = "BackToMenuButton";
        go.SetActive(true);
        _matchOverMenuButton = go.GetComponent<UnityEngine.UI.Button>();
        _matchOverMenuButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        AddClick(_matchOverMenuButton, OnLeaveLobbyClicked);
        var label = go.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "BACK TO MENU";
        var le = go.GetComponent<UnityEngine.UI.LayoutElement>() ?? go.AddComponent<UnityEngine.UI.LayoutElement>();
        le.minHeight = le.preferredHeight = 56f;
        if (go.GetComponent<MenuHoverHighlight>() == null)
        {
            var hl = go.AddComponent<MenuHoverHighlight>();
            hl.backgroundColor = new Color(1f, 1f, 1f, 0.16f);
            hl.labelHighlightColor = new Color(1f, 0.9f, 0.45f);
        }
        // Make sure the card has room: put the panel/card on top of everything and give it padding.
        var vlg = card.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (vlg != null) { vlg.padding = new RectOffset(28, 28, 22, 26); vlg.spacing = 16f; }
        hudMatchOverPanel.transform.SetAsLastSibling();
    }

    UnityEngine.UI.Image _crosshair;
    HiderMinimap _minimap;
    TMP_FontAsset _runtimePixelFont;

    // ------------------------------------------------------------------ Fonts

    /// <summary>
    /// The baked PressStart2P dynamic font asset has a stale atlas (glyph table entries pointing at empty atlas
    /// regions), so some digits in the match timer rendered blank. Build a fresh dynamic atlas from the same
    /// source TTF at runtime and point every PressStart2P label at it.
    /// </summary>
    void RebuildPixelFonts()
    {
        var texts = GetComponentsInChildren<TMP_Text>(true);
        Font source = null;
        foreach (var t in texts)
        {
            if (t.font != null && t.font.name.StartsWith("PressStart2P") && t.font.sourceFontFile != null)
            {
                source = t.font.sourceFontFile;
                break;
            }
        }
        if (source == null) return;

        _runtimePixelFont = TMP_FontAsset.CreateFontAsset(source, 32, 4, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
            1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (_runtimePixelFont == null) return;
        _runtimePixelFont.name = "PressStart2P_Runtime";
        _runtimePixelFont.TryAddCharacters(" 0123456789:./|-+()[]!?%'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz", out _);

        foreach (var t in texts)
        {
            if (t.font != null && t.font.name.StartsWith("PressStart2P"))
                t.font = _runtimePixelFont;
        }
    }

    TMP_FontAsset PixelFont => _runtimePixelFont != null ? _runtimePixelFont : (hudScoreText != null ? hudScoreText.font : null);

    // ------------------------------------------------------------------ Main menu styling

    void ApplyMainMenuStyling()
    {
        if (mainMenuPanel == null) return;

        // Hover / selection highlight on every main-menu option.
        var list = mainMenuPanel.transform.Find("RightStrip/MenuList");
        if (list != null)
        {
            foreach (var btn in list.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (btn.GetComponent<MenuHoverHighlight>() == null) btn.gameObject.AddComponent<MenuHoverHighlight>();
        }

        // Bottom-left block: keep only the game title.
        var title = mainMenuPanel.transform.Find("LeftStrip/TitleBlock");
        if (title != null)
        {
            foreach (Transform child in title)
                if (child.name != "TitleText") child.gameObject.SetActive(false);
        }

        // Name prompt styled like the pause menu (pixel card + coral header + pixel font).
        Sprite cardSprite = null, headerSprite = null;
        Color headerTextColor = Color.white;
        if (pausePanel != null)
        {
            var card = pausePanel.transform.Find("PausePanel_Card");
            if (card != null && card.GetComponent<UnityEngine.UI.Image>() != null) cardSprite = card.GetComponent<UnityEngine.UI.Image>().sprite;
            var header = card != null ? card.Find("Header") : null;
            if (header != null)
            {
                headerSprite = header.GetComponent<UnityEngine.UI.Image>()?.sprite;
                var ht = header.GetComponentInChildren<TMP_Text>(true);
                if (ht != null) headerTextColor = ht.color;
            }
        }

        if (playerNameInput != null)
        {
            var img = playerNameInput.GetComponent<UnityEngine.UI.Image>();
            if (img != null && cardSprite != null)
            {
                img.sprite = cardSprite;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.color = Color.white;
            }
            var outline = playerNameInput.GetComponent<UnityEngine.UI.Outline>();
            if (outline != null) outline.enabled = false;

            if (PixelFont != null)
            {
                if (playerNameInput.textComponent != null) playerNameInput.textComponent.font = PixelFont;
                if (playerNameInput.placeholder is TMP_Text ph) ph.font = PixelFont;
                playerNameInput.fontAsset = PixelFont;
            }
            if (playerNameInput.textComponent != null)
            {
                playerNameInput.textComponent.color = Color.white;
                playerNameInput.textComponent.fontSize = 26;
                playerNameInput.textComponent.alignment = TextAlignmentOptions.Center;
            }
            if (playerNameInput.placeholder is TMP_Text placeholder)
            {
                placeholder.fontSize = 22;
                placeholder.alignment = TextAlignmentOptions.Center;
                placeholder.color = new Color(1f, 1f, 1f, 0.45f);
                placeholder.text = "ENTER YOUR NAME...";
            }
            playerNameInput.caretColor = new Color(0.95f, 0.70f, 0.12f);
            playerNameInput.customCaretColor = true;
            playerNameInput.caretWidth = 4;
            playerNameInput.selectionColor = new Color(0.95f, 0.70f, 0.12f, 0.45f);
        }

        // The "PLAYER CALLSIGN" header above the name box is removed (the placeholder already says ENTER YOUR NAME).
        var nameLabel = mainMenuPanel.transform.Find("RightStrip/MenuList/NameLabel");
        if (nameLabel != null) nameLabel.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ Pixel UI kit (sprites borrowed from the pause menu)

    Sprite _sprCard, _sprCoral, _sprQuit, _sprBadge, _sprBar;
    Color _headerTextColor = Color.white;

    void CachePixelSprites()
    {
        if (pausePanel != null)
        {
            var card = pausePanel.transform.Find("PausePanel_Card");
            _sprCard = card != null ? card.GetComponent<UnityEngine.UI.Image>()?.sprite : null;
            var header = card != null ? card.Find("Header") : null;
            if (header != null)
            {
                _sprCoral = header.GetComponent<UnityEngine.UI.Image>()?.sprite;
                var ht = header.GetComponentInChildren<TMP_Text>(true);
                if (ht != null) _headerTextColor = ht.color;
            }
            var quit = card != null ? card.Find("PauseQuitButton") : null;
            _sprQuit = quit != null ? quit.GetComponent<UnityEngine.UI.Image>()?.sprite : null;
        }
        if (hudPanel != null)
        {
            var banner = hudPanel.transform.Find("ReviveBanner");
            _sprBadge = banner != null ? banner.GetComponent<UnityEngine.UI.Image>()?.sprite : null;
            var bar = hudPanel.transform.Find("HudTopCenterWidget/BeaconProgressBarBg");
            _sprBar = bar != null ? bar.GetComponent<UnityEngine.UI.Image>()?.sprite : null;
        }
    }

    static void SetSprite(UnityEngine.UI.Image img, Sprite sprite)
    {
        if (img == null || sprite == null) return;
        img.sprite = sprite;
        img.type = UnityEngine.UI.Image.Type.Sliced;
        img.color = Color.white;
        foreach (var o in img.GetComponents<UnityEngine.UI.Outline>()) o.enabled = false;
    }

    /// <summary>Restyle the Join / LAN / Lobby / Match-over cards to match the pixel pause menu.</summary>
    void ApplyPixelModalStyling()
    {
        StyleCard(joinModalPanel != null ? joinModalPanel.transform.Find("JoinModalPanel_Card") : null, "JOIN BY LOBBY CODE");
        StyleCard(lanModalPanel != null ? lanModalPanel.transform.Find("LanModalPanel_Card") : null, "LAN / DIRECT IP");
        StyleCard(lobbyPanel != null ? lobbyPanel.transform.Find("LobbyCard") : null, null);
        var over = transform.Find("MatchOverPanel/MatchOverPanel_Card");
        StyleCard(over, null);

        // Lobby specifics: header pill on the mode line, framed code + roster boxes, bigger code.
        if (lobbyPanel != null)
        {
            var card = lobbyPanel.transform.Find("LobbyCard");
            if (card != null)
            {
                var vlg = card.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                if (vlg != null) { vlg.padding = new RectOffset(28, 28, 26, 26); vlg.spacing = 16f; }

                if (lobbyModeText != null)
                {
                    AddPill(lobbyModeText.transform, _sprCoral);
                    lobbyModeText.color = _headerTextColor;
                    lobbyModeText.alignment = TextAlignmentOptions.Center;
                    lobbyModeText.margin = new Vector4(12, 10, 12, 6);
                }
                var codeBox = card.Find("LobbyCodeBox");
                if (codeBox != null) SetSprite(codeBox.GetComponent<UnityEngine.UI.Image>(), _sprBadge ?? _sprCard);
                var roster = card.Find("RosterBox");
                if (roster != null) SetSprite(roster.GetComponent<UnityEngine.UI.Image>(), _sprBar ?? _sprCard);
                if (lobbyCodeText != null) { lobbyCodeText.color = new Color(1f, 0.85f, 0.3f); lobbyCodeText.characterSpacing = 6f; }
                if (lobbyPlayerListText != null) { lobbyPlayerListText.lineSpacing = 18f; lobbyPlayerListText.color = Color.white; }
                if (lobbyCountText != null) lobbyCountText.color = new Color(0.75f, 0.9f, 1f);
            }
        }
    }

    void StyleCard(Transform card, string headerTitle)
    {
        if (card == null) return;
        SetSprite(card.GetComponent<UnityEngine.UI.Image>(), _sprCard);

        var header = card.Find("Header");
        if (header != null)
        {
            SetSprite(header.GetComponent<UnityEngine.UI.Image>(), _sprCoral);
            var ht = header.GetComponentInChildren<TMP_Text>(true);
            if (ht != null)
            {
                ht.color = _headerTextColor;
                if (!string.IsNullOrEmpty(headerTitle)) ht.text = headerTitle;
            }
        }

        foreach (var btn in card.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            string n = btn.name;
            bool negative = n.Contains("Leave") || n.Contains("Cancel") || n.Contains("Close") || n.Contains("Quit");
            SetSprite(btn.GetComponent<UnityEngine.UI.Image>(), negative && _sprQuit != null ? _sprQuit : _sprCoral);
            var label = btn.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = Color.white;
            if (btn.GetComponent<MenuHoverHighlight>() == null)
            {
                var hl = btn.gameObject.AddComponent<MenuHoverHighlight>();
                hl.backgroundColor = new Color(1f, 1f, 1f, 0.16f);
                hl.labelHighlightColor = new Color(1f, 0.9f, 0.45f);
            }
        }

        foreach (var input in card.GetComponentsInChildren<TMP_InputField>(true))
        {
            SetSprite(input.GetComponent<UnityEngine.UI.Image>(), _sprBar ?? _sprCard);
            if (input.textComponent != null) { input.textComponent.color = Color.white; input.textComponent.alignment = TextAlignmentOptions.Center; }
            if (input.placeholder is TMP_Text ph) { ph.color = new Color(1f, 1f, 1f, 0.4f); ph.alignment = TextAlignmentOptions.Center; }
            input.customCaretColor = true;
            input.caretColor = new Color(0.95f, 0.70f, 0.12f);
            input.caretWidth = 4;
        }

        if (PixelFont != null)
            foreach (var t in card.GetComponentsInChildren<TMP_Text>(true)) t.font = PixelFont;
    }

    static void AddPill(Transform target, Sprite sprite)
    {
        if (sprite == null || target.Find("HeaderPill") != null) return;
        var pill = new GameObject("HeaderPill", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
        var rt = (RectTransform)pill.transform;
        rt.SetParent(target, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = pill.GetComponent<UnityEngine.UI.Image>();
        img.sprite = sprite; img.type = UnityEngine.UI.Image.Type.Sliced; img.raycastTarget = false;
        pill.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        // A TMP graphic draws before its children, so put the pill on a sibling placed just behind the text.
        rt.SetParent(target.parent, false);
        rt.SetSiblingIndex(target.GetSiblingIndex());
        var follow = pill.AddComponent<FollowRect>();
        follow.target = (RectTransform)target;
    }

    // ------------------------------------------------------------------ Controls modal

    const string ControlsBody =
        "<b>MOVEMENT</b>\n" +
        "WASD / ARROWS<pos=52%>Move\n" +
        "LEFT SHIFT<pos=52%>Sprint\n" +
        "SPACE<pos=52%>Jump\n" +
        "ESC<pos=52%>Pause menu\n" +
        "\n<b>HIDER</b>\n" +
        "LMB on prop<pos=52%>Disguise\n" +
        "F<pos=52%>Whistle (+10 pts)\n" +
        "RMB<pos=52%>Spawn decoy (max 2)\n" +
        "Jump on Hunter<pos=52%>Blind 5s\n" +
        "\n<b>SEEKER</b>\n" +
        "LMB<pos=52%>Shoot\n" +
        "\n<b>VOICE</b>\n" +
        "V (hold)<pos=52%>Push-to-talk\n" +
        "B<pos=52%>Mic on / off\n" +
        "M<pos=52%>Speaker on / off\n" +
        "\n<b>OTHER</b>\n" +
        "H (solo)<pos=52%>Swap role\n" +
        "F7<pos=52%>Scanlines on / off";

    void RebuildControlsModal()
    {
        if (controlsModalPanel == null) return;
        var card = controlsModalPanel.transform.Find("ControlsModalPanel_Card");
        if (card == null) return;

        var cardRt = (RectTransform)card;
        cardRt.sizeDelta = new Vector2(760f, cardRt.sizeDelta.y);
        var fitter = card.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        }
        var vlg = card.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.padding = new RectOffset(32, 32, 24, 28);
            vlg.spacing = 18f;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
        }

        var body = card.Find("ControlsText");
        var text = body != null ? body.GetComponent<TMP_Text>() : null;
        if (text != null)
        {
            text.enableAutoSizing = false;
            text.fontSize = 15f;
            text.lineSpacing = 22f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.richText = true;
            text.text = ControlsBody;
            var le = body.GetComponent<UnityEngine.UI.LayoutElement>() ?? body.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            float width = 760f - 64f;
            float h = text.GetPreferredValues(ControlsBody, width, 0f).y + 8f;
            le.minHeight = h;
            le.preferredHeight = h;
            le.flexibleHeight = 0f;
        }

        foreach (var btn in card.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            var le = btn.GetComponent<UnityEngine.UI.LayoutElement>() ?? btn.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            le.minHeight = 56f;
            le.preferredHeight = 56f;
            le.flexibleHeight = 0f;
            if (btn.GetComponent<MenuHoverHighlight>() == null)
            {
                var hl = btn.gameObject.AddComponent<MenuHoverHighlight>();
                hl.backgroundColor = new Color(1f, 1f, 1f, 0.16f);
                hl.labelHighlightColor = new Color(1f, 0.9f, 0.45f);
            }
        }
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(cardRt);
    }

    /// <summary>
    /// Generic text-fit pass: every label that sits in a fixed box shrinks to fit instead of spilling
    /// over neighbouring widgets (min 55% of its designed size). Free-flowing body text is left alone.
    /// </summary>
    void FitAllTexts()
    {
        foreach (var t in GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.name == "ControlsText" || t.name == "LabelText" || t.GetComponentInParent<TMP_InputField>(true) != null) continue;
            if (t.enableAutoSizing) { t.fontSizeMax = Mathf.Max(t.fontSizeMax, t.fontSize); continue; }
            float size = t.fontSize;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = Mathf.Max(9f, size * 0.55f);
            if (t.overflowMode == TextOverflowModes.Overflow) t.overflowMode = TextOverflowModes.Truncate; // fonts lack the ellipsis glyph
        }
    }

    // ------------------------------------------------------------------ Loading overlay

    GameObject _loadingRoot;
    RectTransform _spinner;
    TMP_Text _loadingText;
    float _spinnerTimer;

    void UpdateLoadingOverlay(MultiplayerSessionManager mgr)
    {
        bool show = mgr != null && mgr.IsConnecting;
        if (_loadingRoot == null)
        {
            if (!show) return;
            BuildLoadingOverlay();
        }
        if (_loadingRoot.activeSelf != show) _loadingRoot.SetActive(show);
        if (!show) return;

        _loadingRoot.transform.SetAsLastSibling();
        // Step the spinner in 45 degree ticks for a chunky retro feel.
        _spinnerTimer += Time.unscaledDeltaTime;
        int step = Mathf.FloorToInt(_spinnerTimer * 10f) % 8;
        _spinner.localRotation = Quaternion.Euler(0f, 0f, -45f * step);

        int dots = Mathf.FloorToInt(_spinnerTimer * 3f) % 4;
        _loadingText.text = mgr.ConnectingLabel + new string('.', dots);
    }

    void BuildLoadingOverlay()
    {
        _loadingRoot = new GameObject("LoadingOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rt = (RectTransform)_loadingRoot.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var dim = _loadingRoot.GetComponent<UnityEngine.UI.Image>();
        dim.color = new Color(0.02f, 0.02f, 0.05f, 0.72f);
        dim.raycastTarget = true; // block clicks while connecting

        var card = new GameObject("Card", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var crt = (RectTransform)card.transform;
        crt.SetParent(rt, false);
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(620f, 240f);
        var cimg = card.GetComponent<UnityEngine.UI.Image>();
        if (_sprCard != null) SetSprite(cimg, _sprCard); else cimg.color = new Color(0.06f, 0.09f, 0.15f, 0.97f);

        var spin = new GameObject("Spinner", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        _spinner = (RectTransform)spin.transform;
        _spinner.SetParent(crt, false);
        _spinner.anchorMin = _spinner.anchorMax = _spinner.pivot = new Vector2(0.5f, 0.5f);
        _spinner.anchoredPosition = new Vector2(0f, 34f);
        _spinner.sizeDelta = new Vector2(96f, 96f);
        var simg = spin.GetComponent<UnityEngine.UI.Image>();
        simg.sprite = CreateSpinnerSprite();
        simg.color = new Color(0.95f, 0.70f, 0.12f);
        simg.raycastTarget = false;

        var txtGo = new GameObject("Status", typeof(RectTransform));
        var trt = (RectTransform)txtGo.transform;
        trt.SetParent(crt, false);
        trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 0f); trt.pivot = new Vector2(0.5f, 0f);
        trt.anchoredPosition = new Vector2(0f, 24f);
        trt.sizeDelta = new Vector2(-40f, 60f);
        _loadingText = txtGo.AddComponent<TextMeshProUGUI>();
        if (PixelFont != null) _loadingText.font = PixelFont;
        _loadingText.fontSize = 24f;
        _loadingText.enableAutoSizing = true;
        _loadingText.fontSizeMin = 10f;
        _loadingText.fontSizeMax = 24f;
        _loadingText.alignment = TextAlignmentOptions.Center;
        _loadingText.color = Color.white;
        _loadingText.raycastTarget = false;
    }

    static Sprite CreateSpinnerSprite()
    {
        // 16x16 pixel ring with 8 blocks of fading brightness (the "comet tail" of a retro spinner).
        const int size = 16;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - c, dy = y - c;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 4.5f || d > 7.6f) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }
            float ang = Mathf.Repeat(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg, 360f); // 0 = up, clockwise
            int seg = Mathf.FloorToInt(ang / 45f);
            float within = Mathf.Repeat(ang, 45f);
            if (within > 36f) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; } // gaps between blocks
            byte a = (byte)Mathf.Lerp(255f, 40f, seg / 7f);
            px[y * size + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // ------------------------------------------------------------------ Voice overlay (mic + speaker)

    GameObject _voiceRoot;
    UnityEngine.UI.Image _micIcon, _spkIcon;
    TMP_Text _micText, _spkText, _talkText;

    void UpdateVoiceOverlay(MultiplayerSessionManager mgr)
    {
        var voice = VoiceChatManager.Instance;
        bool online = mgr != null && voice != null &&
                      (mgr.ConnectionMode == SessionConnectionMode.OnlineRelayCode || mgr.ConnectionMode == SessionConnectionMode.LocalLan) &&
                      !mgr.IsConnecting;
        if (_voiceRoot == null)
        {
            if (!online) return;
            BuildVoiceOverlay();
        }
        if (_voiceRoot.activeSelf != online) _voiceRoot.SetActive(online);
        if (!online) return;

        Color on = new Color(0.3f, 1f, 0.5f), off = new Color(1f, 0.4f, 0.35f), idle = new Color(0.75f, 0.85f, 1f), grey = new Color(0.55f, 0.55f, 0.6f);

        string micState; Color micCol;
        switch (voice.Status)
        {
            case VoiceChatManager.MicStatus.TransmittingPtt: micState = "TALKING (PTT)"; micCol = on; break;
            case VoiceChatManager.MicStatus.TransmittingOpenMic: micState = "MIC ON - TALKING"; micCol = on; break;
            case VoiceChatManager.MicStatus.OpenMicListening: micState = "MIC ON"; micCol = idle; break;
            case VoiceChatManager.MicStatus.Muted: micState = "MIC OFF"; micCol = off; break;
            default: micState = "NO MIC FOUND"; micCol = grey; break;
        }
        bool talking = voice.Status == VoiceChatManager.MicStatus.TransmittingPtt || voice.Status == VoiceChatManager.MicStatus.TransmittingOpenMic;
        float pulse = talking ? (Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.5f ? 1f : 0.6f) : 1f;
        _micIcon.color = micCol * pulse;
        _micText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(micCol)}>{micState}</color>\n<size=75%><color=#BBBBBB>[B] mic on/off  [V] hold to talk</color></size>";

        bool muted = voice.SpeakerMuted;
        _spkIcon.color = muted ? off : on;
        _spkText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(muted ? off : on)}>{(muted ? "SPEAKER OFF" : "SPEAKER ON")}</color>\n<size=75%><color=#BBBBBB>[M] speaker on/off</color></size>";

        voice.GetSpeakingPlayers(_speakingNames);
        string channel = string.Empty;
        var local = NetworkPlayerController.LocalInstance;
        if (local != null && NetworkMatchState.Instance != null && NetworkMatchState.Instance.IsMatchActive)
        {
            if (local.CurrentRole == PlayerRole.Hunter) channel = "CH: PROXIMITY (hiders can't be heard)";
            else if (local.IsEliminated.Value) channel = "CH: GHOST (downed only)";
            else channel = "CH: HIDER RADIO";
        }
        else channel = "CH: LOBBY (everyone)";
        _talkText.text = channel + (_speakingNames.Count > 0
            ? "\n<color=#66FF99><))) </color>" + string.Join(", ", _speakingNames)
            : string.Empty);
    }

    void BuildVoiceOverlay()
    {
        _voiceRoot = new GameObject("VoiceOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rt = (RectTransform)_voiceRoot.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-20f, -16f);
        rt.sizeDelta = new Vector2(380f, 168f);
        var bg = _voiceRoot.GetComponent<UnityEngine.UI.Image>();
        if (_sprCard != null) SetSprite(bg, _sprCard); else bg.color = new Color(0.06f, 0.09f, 0.15f, 0.9f);
        bg.raycastTarget = false;

        _micIcon = MakeIcon(rt, "MicIcon", CreateMicSprite(), new Vector2(18f, -18f));
        _micText = MakeLabel(rt, "MicText", new Vector2(68f, -14f));
        _spkIcon = MakeIcon(rt, "SpeakerIcon", CreateSpeakerSprite(), new Vector2(18f, -70f));
        _spkText = MakeLabel(rt, "SpeakerText", new Vector2(68f, -66f));
        _talkText = MakeLabel(rt, "TalkText", new Vector2(18f, -118f));
        ((RectTransform)_talkText.transform).sizeDelta = new Vector2(344f, 44f);
    }

    UnityEngine.UI.Image MakeIcon(RectTransform parent, string name, Sprite sprite, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(40f, 40f);
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    TMP_Text MakeLabel(RectTransform parent, string name, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(296f, 48f);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (PixelFont != null) t.font = PixelFont;
        t.fontSize = 13f;
        t.enableAutoSizing = true;
        t.fontSizeMin = 8f;
        t.fontSizeMax = 13f;
        t.lineSpacing = 12f;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    static Sprite PixelArt(string[] rows)
    {
        int h = rows.Length, w = rows[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            px[y * w + x] = rows[h - 1 - y][x] == '#' ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite CreateMicSprite() => PixelArt(new[]
    {
        "....####....",
        "...######...",
        "...######...",
        "...######...",
        "...######...",
        ".#.######.#.",
        ".#.######.#.",
        ".#..####..#.",
        "..#......#..",
        "...######...",
        ".....##.....",
        "...######...",
    });

    static Sprite CreateSpeakerSprite() => PixelArt(new[]
    {
        "......#.....",
        ".....##...#.",
        "....###..#..",
        "#######...#.",
        "#######.#..#",
        "#######.#..#",
        "#######.#..#",
        "#######...#.",
        "....###..#..",
        ".....##...#.",
        "......#.....",
        "............",
    });

    // ------------------------------------------------------------------ Crosshair + minimap

    void UpdateCrosshairAndMinimap(NetworkPlayerController localPlayer, NetworkMatchState matchState)
    {
        if (hudPanel == null) return;
        bool isHunter = localPlayer.CurrentRole == PlayerRole.Hunter;
        bool matchLive = matchState == null || (MatchOutcomeType)matchState.MatchOutcome.Value == MatchOutcomeType.InProgress;

        // Seeker crosshair
        bool showCross = isHunter && matchLive && !_isPaused && !localPlayer.IsEliminated.Value && !localPlayer.IsBlinded.Value;
        if (_crosshair == null && showCross)
        {
            var go = new GameObject("HunterCrosshair", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(hudPanel.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(44f, 44f);
            _crosshair = go.GetComponent<UnityEngine.UI.Image>();
            _crosshair.sprite = CreateCrosshairSprite();
            _crosshair.raycastTarget = false;
            _crosshair.color = new Color(1f, 0.35f, 0.25f, 0.95f);
        }
        if (_crosshair != null && _crosshair.gameObject.activeSelf != showCross)
            _crosshair.gameObject.SetActive(showCross);

        // Hider minimap (bottom-left)
        bool showMap = !isHunter && matchLive;
        if (_minimap == null && showMap)
        {
            Sprite frame = null;
            var card = hudPanel.transform.Find("HudTopLeftCard");
            if (card != null) frame = card.GetComponent<UnityEngine.UI.Image>()?.sprite;
            _minimap = HiderMinimap.Create(hudPanel.transform, frame);
        }
        if (_minimap != null) _minimap.SetVisible(showMap);

    }

    static Sprite CreateCrosshairSprite()
    {
        // 11x11 pixel crosshair with a centre gap and a dot.
        const int size = 11;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        const int c = size / 2;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool arm = (x == c && Mathf.Abs(y - c) >= 2) || (y == c && Mathf.Abs(x - c) >= 2);
            bool dot = x == c && y == c;
            px[y * size + x] = arm || dot ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
    UnityEngine.UI.Image _reviveRing;
    UnityEngine.UI.Image _reviveRingBack;
    readonly List<string> _speakingNames = new();

    void UpdateVoiceHud()
    {
        // Voice status now lives in the always-visible top-right overlay (see UpdateVoiceOverlay).
    }

    void UpdateReviveRing(bool show, float fill)
    {
        if (hudPanel == null) return;
        if (_reviveRing == null)
        {
            if (!show) return;
            Sprite ringSprite = CreateRingSprite();
            _reviveRingBack = CreateRingImage("ReviveRingBack", ringSprite, new Color(1f, 1f, 1f, 0.18f));
            _reviveRing = CreateRingImage("ReviveRingFill", ringSprite, new Color(0.15f, 1f, 0.5f, 0.95f));
            _reviveRing.type = UnityEngine.UI.Image.Type.Filled;
            _reviveRing.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            _reviveRing.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            _reviveRing.fillClockwise = true;
        }

        if (_reviveRing.gameObject.activeSelf != show)
        {
            _reviveRing.gameObject.SetActive(show);
            _reviveRingBack.gameObject.SetActive(show);
        }
        if (show) _reviveRing.fillAmount = fill;
    }

    UnityEngine.UI.Image CreateRingImage(string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(hudPanel.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        rt.sizeDelta = new Vector2(128f, 128f);
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Sprite CreateRingSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            px[y * size + x] = d <= c && d >= c - 7f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
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
