using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum PlayerRole
{
    Hider = 0,
    Hunter = 1
}

/// <summary>
/// Synchronizes player identity, role (Hunter vs Hider), speed scaling (1.25x),
/// shooting mechanics, whistle cooldowns/mandatory timers, decoy clones, and revival state.
/// </summary>
[RequireComponent(typeof(FirstPersonRoam))]
[RequireComponent(typeof(PropMorphController))]
[RequireComponent(typeof(CharacterController))]
public class NetworkPlayerController : NetworkBehaviour
{
    public static readonly List<NetworkPlayerController> ActivePlayers = new();
    public static event Action OnActivePlayersChanged;
    public static NetworkPlayerController LocalInstance { get; private set; }

    public static readonly Vector3 BaseSpawnPosition = new Vector3(-56.50f, -3.24f, -40.50f);
    public static readonly Vector3 BaseSpawnEuler = new Vector3(0f, 90f, 0f);

    public const float HunterWalkSpeed = 5.0f;
    public const float HunterSprintSpeed = 8.0f;
    public const float HiderWalkSpeed = 6.25f;    // 1.25x Hunter
    public const float HiderSprintSpeed = 10.0f;  // 1.25x Hunter

    public NetworkVariable<FixedString64Bytes> PlayerName = new(
        new FixedString64Bytes("Player"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> MorphedPropIndex = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Role = new(
        (int)PlayerRole.Hider,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> IsEliminated = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Score = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> IsBlinded = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<float> BlindTimeRemaining = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    FirstPersonRoam _roam;
    PropMorphController _morph;
    CharacterController _cc;
    Camera _playerCamera;
    AudioListener _audioListener;

    bool _isOfflineSolo;
    bool _isPaused;
    bool _gameplayActive;

    // Timers
    float _mandatoryWhistleTimer = 30f;
    float _whistleCooldownTimer = 0f;
    float _survivalPointsTimer = 3f;
    float _shootCooldownTimer = 0f;
    float _stompCooldownTimer = 0f;

    // Decoy clones (max 2)
    readonly List<GameObject> _decoyClones = new();

    // Weapon tracer visual for Hunter
    LineRenderer _tracerLine;

    public bool IsLocalControlled => _isOfflineSolo || IsOwner;
    public string DisplayName => PlayerName.Value.Length > 0 ? PlayerName.Value.ToString() : $"Player {OwnerClientId}";
    public string CurrentMorphName => _morph != null ? _morph.CurrentMorphName : "Human";
    public bool IsMorphed => _morph != null && _morph.IsMorphed;
    public PlayerRole CurrentRole => (PlayerRole)Role.Value;
    public float WhistleCooldownRemaining => _whistleCooldownTimer;
    /// <summary>Seconds until the Hunter can fire again (reload cooldown).</summary>
    public float ShootCooldownRemaining => Mathf.Max(0f, _shootCooldownTimer);
    public float MandatoryWhistleRemaining => _mandatoryWhistleTimer;
    public int DecoyCount => _decoyClones.Count;

    void Awake()
    {
        _roam = GetComponent<FirstPersonRoam>();
        _morph = GetComponent<PropMorphController>();
        _cc = GetComponent<CharacterController>();
        _playerCamera = GetComponentInChildren<Camera>(true);
        _audioListener = GetComponentInChildren<AudioListener>(true);

        SetupWeaponTracer();
        _weapon = GetComponent<HunterWeaponVisual>() ?? gameObject.AddComponent<HunterWeaponVisual>();
    }

    HunterWeaponVisual _weapon;

    void SetupWeaponTracer()
    {
        var lineGo = new GameObject("WeaponTracer");
        lineGo.transform.SetParent(transform, false);
        _tracerLine = lineGo.AddComponent<LineRenderer>();
        _tracerLine.startWidth = 0.05f;
        _tracerLine.endWidth = 0.01f;
        _tracerLine.positionCount = 2;
        _tracerLine.enabled = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1.0f, 0.4f, 0.1f, 0.9f));
        _tracerLine.sharedMaterial = mat;
    }

    public void ConfigureOfflineSolo(string playerName)
    {
        _isOfflineSolo = true;
        LocalInstance = this;
        if (!ActivePlayers.Contains(this))
            ActivePlayers.Add(this);

        if (_cc != null) _cc.enabled = false;
        transform.SetPositionAndRotation(BaseSpawnPosition, Quaternion.Euler(BaseSpawnEuler));
        if (_cc != null) _cc.enabled = true;

        SetLocalCameraActive(true);
        if (_roam != null)
        {
            _roam.enabled = true;
            _roam.autoManageCursor = false;
        }
        if (_morph != null)
        {
            _morph.SetHumanAvatarVisible(false);
            _morph.OnLocalMorphRequested = target => _morph.MorphInto(target);
            _morph.OnLocalRevertRequested = () => _morph.RevertToFirstPerson();
        }

        _gameplayActive = true;
        _isPaused = false;
        ApplyInputGate();
        ApplyRoleSpeeds();
        OnActivePlayersChanged?.Invoke();

        if (NetworkMatchState.Instance != null)
            NetworkMatchState.Instance.StartMatchSoloOffline();
    }

    public override void OnNetworkSpawn()
    {
        if (!ActivePlayers.Contains(this))
            ActivePlayers.Add(this);

        MorphedPropIndex.OnValueChanged += HandleMorphedPropChanged;
        PlayerName.OnValueChanged += HandlePlayerNameChanged;
        Role.OnValueChanged += HandleRoleChanged;
        IsEliminated.OnValueChanged += HandleEliminatedChanged;
        NetworkMatchState.MatchStartedChanged += HandleMatchStartedChanged;

        if (IsOwner)
        {
            LocalInstance = this;
            PositionAtSpawn(OwnerClientId);

            string desiredName = MultiplayerSessionManager.Instance != null
                ? MultiplayerSessionManager.Instance.LocalPlayerName
                : "Player";
            SetPlayerNameRpc(desiredName);

            if (_roam != null)
            {
                _roam.enabled = true;
                _roam.autoManageCursor = false;
            }
            if (_morph != null)
            {
                _morph.SetHumanAvatarVisible(false);
                _morph.OnLocalMorphRequested = HandleLocalMorphRequested;
                _morph.OnLocalRevertRequested = HandleLocalRevertRequested;
            }

            _gameplayActive = NetworkMatchState.Instance != null && NetworkMatchState.Instance.MatchStarted.Value;
            SetLocalCameraActive(_gameplayActive);
            ApplyInputGate();
            ApplyRoleSpeeds();
        }
        else
        {
            if (IsServer)
                PositionAtSpawn(OwnerClientId);

            SetLocalCameraActive(false);
            if (_roam != null)
            {
                _roam.inputEnabled = false;
                _roam.autoManageCursor = false;
                _roam.enabled = false;
            }
            if (_morph != null)
            {
                _morph.inputEnabled = false;
                _morph.SetHumanAvatarVisible(true);
            }
        }

        ApplyMorphIndex(MorphedPropIndex.Value);
        ApplyRoleSpeeds();
        OnActivePlayersChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        MorphedPropIndex.OnValueChanged -= HandleMorphedPropChanged;
        PlayerName.OnValueChanged -= HandlePlayerNameChanged;
        Role.OnValueChanged -= HandleRoleChanged;
        IsEliminated.OnValueChanged -= HandleEliminatedChanged;
        NetworkMatchState.MatchStartedChanged -= HandleMatchStartedChanged;

        if (_morph != null)
        {
            _morph.OnLocalMorphRequested = null;
            _morph.OnLocalRevertRequested = null;
        }

        ClearDecoys();

        if (LocalInstance == this)
            LocalInstance = null;

        ActivePlayers.Remove(this);
        OnActivePlayersChanged?.Invoke();
    }

    public override void OnDestroy()
    {
        ClearDecoys();
        if (LocalInstance == this || _isOfflineSolo) SeekerVision.Apply(null, false, 0f);
        if (LocalInstance == this)
            LocalInstance = null;
        if (ActivePlayers.Remove(this))
            OnActivePlayersChanged?.Invoke();
        base.OnDestroy();
    }

    void Update()
    {
        if (IsServer && BlindTimeRemaining.Value > 0f)
        {
            BlindTimeRemaining.Value = Mathf.Max(0f, BlindTimeRemaining.Value - Time.deltaTime);
            if (BlindTimeRemaining.Value <= 0f)
                IsBlinded.Value = false;
        }

        if (!IsLocalControlled) return;

        // Solo mode role toggle key (H) - unnetworked solo or the loopback solo host.
        bool soloSession = _isOfflineSolo ||
            (IsServer && MultiplayerSessionManager.Instance != null &&
             MultiplayerSessionManager.Instance.ConnectionMode == SessionConnectionMode.SoloOffline);
        if (soloSession)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                ToggleRoleSolo();
#else
            if (Input.GetKeyDown(KeyCode.H))
                ToggleRoleSolo();
#endif
        }

        // Keep gameplay/camera in step with the replicated match state (covers a missed change event).
        if (!_isOfflineSolo && IsSpawned)
        {
            var ms = NetworkMatchState.Instance;
            if (ms != null && ms.IsSpawned && ms.MatchStarted.Value != _gameplayActive)
                HandleMatchStartedChanged(ms.MatchStarted.Value);
        }

        ApplyRoleSpeeds();
        SeekerVision.Apply(_playerCamera, CurrentRole == PlayerRole.Hunter && _gameplayActive && !IsEliminated.Value,
            NetworkMatchState.Instance != null ? NetworkMatchState.Instance.SetSeekerVision.Value : 10f);

        // Re-apply the input gate when the opening hide phase starts/ends for the Hunter.
        if (IsHunterFrozen != _hunterFrozenApplied)
            ApplyInputGate();

        if (!_gameplayActive || _isPaused || IsEliminated.Value)
            return;

        // Hider mechanics
        if (CurrentRole == PlayerRole.Hider)
        {
            HandleSpectateToggle();
            HandleHiderTick();
            CheckHeadStomp();
        }
        // Hunter mechanics
        else if (CurrentRole == PlayerRole.Hunter)
        {
            HandleHunterTick();
        }
    }

    void ToggleRoleSolo()
    {
        var next = CurrentRole == PlayerRole.Hider ? PlayerRole.Hunter : PlayerRole.Hider;
        SetRole(next);
        Debug.Log($"[Solo] Toggled role to: {next}");
    }

    public void SetRole(PlayerRole role)
    {
        Role.Value = (int)role;
        ApplyRoleSpeeds();

        if (role == PlayerRole.Hunter && _morph != null)
        {
            _morph.RevertToFirstPerson();
            _morph.inputEnabled = false; // Hunters don't morph
        }
        else if (role == PlayerRole.Hider && _morph != null)
        {
            // Only the owning player's morph controller may take input (and draw its aim hint);
            // the host used to switch it on for every remote Hider, so a host Hunter saw "Click to morph".
            _morph.inputEnabled = false;
            ApplyInputGate();
        }
    }

    void ApplyRoleSpeeds()
    {
        if (_roam == null) return;
        var ms = NetworkMatchState.Instance;
        if (CurrentRole == PlayerRole.Hider)
        {
            float m = ms != null ? ms.SetHiderSpeed.Value : 1f;
            _roam.SetSpeed(HiderWalkSpeed * m, HiderSprintSpeed * m);
        }
        else
        {
            float m = ms != null ? ms.SetSeekerSpeed.Value : 1f;
            _roam.SetSpeed(HunterWalkSpeed * m, HunterSprintSpeed * m);
        }
    }

    void HandleHiderTick()
    {
        float dt = Time.deltaTime;

        // 1. Mandatory sound every 30 seconds
        _mandatoryWhistleTimer -= dt;
        if (_mandatoryWhistleTimer <= 0f)
        {
            _mandatoryWhistleTimer = 30f;
            TriggerWhistle(isMandatory: true);
        }

        // 2. Manual whistle cooldown
        if (_whistleCooldownTimer > 0f)
            _whistleCooldownTimer -= dt;

        bool whistlePressed = false;
        bool rightClickPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            whistlePressed = true;
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            rightClickPressed = true;
#else
        if (Input.GetKeyDown(KeyCode.F))
            whistlePressed = true;
        if (Input.GetMouseButtonDown(1))
            rightClickPressed = true;
#endif

        if (whistlePressed && _whistleCooldownTimer <= 0f && _roam.IsCursorLocked)
        {
            _whistleCooldownTimer = 5.0f;
            TriggerWhistle(isMandatory: false);
            AddScore(10);
        }

        // 3. Survival points: +1 every 3 seconds
        _survivalPointsTimer -= dt;
        if (_survivalPointsTimer <= 0f)
        {
            _survivalPointsTimer = 3.0f;
            AddScore(1);
        }

        // 4. Decoy Clones (Right Click)
        if (rightClickPressed && _roam.IsCursorLocked)
        {
            SpawnDecoyClone();
        }
    }

    bool _hunterFrozenApplied;
    bool _forcedFreeLook;

    /// <summary>True for the Hunter during the opening 20 s hide phase (frozen + blind, can't shoot).</summary>
    public bool IsHunterFrozen =>
        CurrentRole == PlayerRole.Hunter && NetworkMatchState.Instance != null && NetworkMatchState.Instance.IsHidePhase;

    public bool IsSpectating => _roam != null && _roam.IsFreeLook;
    /// <summary>Set when the player pressed E while moving (HUD shows a short hint).</summary>
    public float SpectateDeniedTimer { get; private set; }

    /// <summary>E toggles spectate (free-look) mode for a stationary Hider: the camera looks around
    /// on its own while the body/prop keeps its rotation, so nothing gives the Hider away.</summary>
    void HandleSpectateToggle()
    {
        if (SpectateDeniedTimer > 0f) SpectateDeniedTimer -= Time.deltaTime;
        if (_roam == null || IsEliminated.Value || _roam.IsSpectatorFlying) return;

        bool pressed;
#if ENABLE_INPUT_SYSTEM
        pressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        pressed = Input.GetKeyDown(KeyCode.E);
#endif
        if (!pressed || !_roam.IsCursorLocked) return;

        if (_roam.IsFreeLook)
            _roam.SetFreeLook(false);
        else if (_roam.IsStationary)
            _roam.SetFreeLook(true);
        else
            SpectateDeniedTimer = 1.5f;
    }

    void HandleHunterTick()
    {
        if (IsHunterFrozen) return; // hiders are still hiding
        if (_shootCooldownTimer > 0f)
            _shootCooldownTimer -= Time.deltaTime;

        if (IsBlinded.Value) return; // Cannot shoot while blinded

        bool shootPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            shootPressed = true;
#else
        if (Input.GetMouseButtonDown(0))
            shootPressed = true;
#endif

        if (shootPressed && _shootCooldownTimer <= 0f && _roam.IsCursorLocked)
        {
            _shootCooldownTimer = NetworkMatchState.Instance != null ? NetworkMatchState.Instance.SetSeekerReload.Value : 3f;
            FireHunterShot();
        }
    }

    void FireHunterShot()
    {
        ProceduralAudio.PlayGunshot(transform.position);

        Camera cam = _playerCamera != null ? _playerCamera : Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint = ray.origin + ray.direction * 75f;

        if (Physics.Raycast(ray, out RaycastHit hit, 75f, ~0, QueryTriggerInteraction.Ignore))
        {
            // Ignore self
            if (!hit.transform.IsChildOf(transform))
            {
                targetPoint = hit.point;

                // Check hit on Decoy
                var decoy = hit.collider.GetComponentInParent<DecoyTarget>();
                if (decoy != null)
                {
                    decoy.OnShotByHunter();
                    if (!_isOfflineSolo && IsSpawned)
                        DestroyDecoyRpc(decoy.OwnerClientId, decoy.transform.position);
                }
                else
                {
                    // Check hit on Hider
                    var hider = hit.collider.GetComponentInParent<NetworkPlayerController>();
                    if (hider != null && hider.CurrentRole == PlayerRole.Hider && !hider.IsEliminated.Value)
                    {
                        ProceduralAudio.PlayHit(hit.point);
                        AddScore(50);

                        if (_isOfflineSolo)
                        {
                            hider.DownHider();
                        }
                        else
                        {
                            ShootHiderRpc(hider.OwnerClientId);
                        }
                    }
                }
            }
        }

        Vector3 tracerStart = cam.transform.position + cam.transform.forward * 0.4f + cam.transform.right * 0.2f - cam.transform.up * 0.15f;
        if (_weapon != null)
        {
            _weapon.Kick();
            tracerStart = _weapon.MuzzlePosition;
        }
        StartCoroutine(ShowTracerRoutine(tracerStart, targetPoint));
    }

    System.Collections.IEnumerator ShowTracerRoutine(Vector3 start, Vector3 end)
    {
        if (_tracerLine == null) yield break;
        _tracerLine.SetPosition(0, start);
        _tracerLine.SetPosition(1, end);
        _tracerLine.enabled = true;
        yield return new WaitForSeconds(0.06f);
        _tracerLine.enabled = false;
    }

    [Rpc(SendTo.Server)]
    void ShootHiderRpc(ulong targetClientId)
    {
        for (int i = 0; i < ActivePlayers.Count; i++)
        {
            var p = ActivePlayers[i];
            if (p != null && p.OwnerClientId == targetClientId && p.CurrentRole == PlayerRole.Hider)
            {
                p.DownHider();
                break;
            }
        }
    }

    /// <summary>Server/solo: put this Hider into the downed (spectator) state and morph them back to human.</summary>
    public void DownHider()
    {
        IsEliminated.Value = true;
        MorphedPropIndex.Value = -1; // replicated: everyone sees the human silhouette again

        if (_morph != null)
            _morph.RevertToFirstPerson();

        if (!_isOfflineSolo && IsSpawned)
            PlayDownedRpc(transform.position);
        else
            ProceduralAudio.PlayDowned(transform.position);

        ApplyInputGate();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlayDownedRpc(Vector3 pos)
    {
        ProceduralAudio.PlayDowned(pos);
    }

    /// <summary>
    /// Server/solo: revive this Hider at the fridge spawn. The transform is owner-authoritative,
    /// so the actual teleport + CharacterController/camera/speed restore runs on the owning client.
    /// </summary>
    public void ReviveAt(Vector3 spawnPos)
    {
        IsEliminated.Value = false;

        if (_isOfflineSolo || !IsSpawned)
            ApplyReviveLocally(spawnPos);
        else
            ReviveTeleportRpc(spawnPos);
    }

    [Rpc(SendTo.Owner)]
    void ReviveTeleportRpc(Vector3 spawnPos)
    {
        ApplyReviveLocally(spawnPos);
    }

    void ApplyReviveLocally(Vector3 spawnPos)
    {
        Quaternion rot = transform.rotation;
        if (_cc != null) _cc.enabled = false;

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null && IsSpawned && netTransform.CanCommitToTransform)
            netTransform.Teleport(spawnPos, rot, transform.localScale);
        else
            transform.position = spawnPos;

        if (_cc != null) _cc.enabled = true;

        SetLocalCameraActive(_gameplayActive);
        ApplyRoleSpeeds();
        ApplyInputGate();
    }

    /// <summary>Server/solo-only score award (e.g. revival bonus for the reviver).</summary>
    public void AwardScoreServer(int delta)
    {
        if (IsServer || _isOfflineSolo || !IsSpawned)
            Score.Value += delta;
    }

    void CheckHeadStomp()
    {
        if (_stompCooldownTimer > 0f)
        {
            _stompCooldownTimer -= Time.deltaTime;
            return;
        }

        // Must be in the air (jumping/falling down)
        if (_roam == null || _roam.VerticalVelocity > 2f) return;

        // Check distance to active Hunter
        for (int i = 0; i < ActivePlayers.Count; i++)
        {
            var p = ActivePlayers[i];
            if (p == null || p == this || p.CurrentRole != PlayerRole.Hunter || p.IsEliminated.Value)
                continue;

            Vector3 hiderPos = transform.position;
            Vector3 hunterPos = p.transform.position;

            float horizontalDist = Vector2.Distance(new Vector2(hiderPos.x, hiderPos.z), new Vector2(hunterPos.x, hunterPos.z));
            float heightDiff = hiderPos.y - hunterPos.y; // Hider feet vs Hunter feet

            // Hunter head height ~1.5m to 2.2m
            if (horizontalDist < 0.85f && heightDiff >= 1.25f && heightDiff <= 2.35f)
            {
                // STOMP TRIGGERED!
                _stompCooldownTimer = 2.0f;
                _roam.Bounce(8.5f); // Mario spring bounce!
                ProceduralAudio.PlayStomp(hiderPos);
                AddScore(25);

                if (_isOfflineSolo)
                {
                    p.ApplyBlind(5.0f);
                }
                else
                {
                    BlindPlayerRpc(p.OwnerClientId, 5.0f);
                }
                break;
            }
        }
    }

    [Rpc(SendTo.Server)]
    void BlindPlayerRpc(ulong targetClientId, float duration)
    {
        for (int i = 0; i < ActivePlayers.Count; i++)
        {
            var p = ActivePlayers[i];
            if (p != null && p.OwnerClientId == targetClientId)
            {
                p.ApplyBlind(duration);
                break;
            }
        }
    }

    public void ApplyBlind(float duration)
    {
        IsBlinded.Value = true;
        BlindTimeRemaining.Value = duration;
        ProceduralAudio.PlayHit(transform.position);
    }

    void TriggerWhistle(bool isMandatory)
    {
        ProceduralAudio.PlayWhistle(transform.position, isMandatory ? 1.0f : 0.85f);

        if (!_isOfflineSolo && IsSpawned)
            BroadcastWhistleRpc(transform.position);
    }

    // NotMe: the whistler already played it locally (avoids a doubled, louder whistle).
    [Rpc(SendTo.NotMe)]
    void BroadcastWhistleRpc(Vector3 pos)
    {
        ProceduralAudio.PlayWhistle(pos, 0.9f);
    }

    void SpawnDecoyClone()
    {
        if (_isOfflineSolo || !IsSpawned)
        {
            SpawnDecoyCloneLocal(transform.position, transform.rotation, MorphedPropIndex.Value);
        }
        else
        {
            SpawnDecoyRpc(transform.position, transform.rotation, MorphedPropIndex.Value);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SpawnDecoyRpc(Vector3 pos, Quaternion rot, int propIndex)
    {
        SpawnDecoyCloneLocal(pos, rot, propIndex);
    }

    void SpawnDecoyCloneLocal(Vector3 pos, Quaternion rot, int propIndex)
    {
        GameObject clone = null;

        // 1. If this is local player and we have active morph visual, clone it directly
        if (IsLocalControlled && _morph != null)
        {
            clone = _morph.CreateCurrentVisualCopy();
            if (clone != null)
            {
                clone.transform.position = pos;
                clone.transform.rotation = rot;
            }
        }

        // 2. Otherwise (remote client or fallback), clone from PropRegistry if available
        if (clone == null && propIndex >= 0 && PropRegistry.Instance != null)
        {
            var propTarget = PropRegistry.Instance.GetTarget(propIndex);
            if (propTarget != null)
            {
                clone = Instantiate(propTarget.gameObject, pos, rot);
                foreach (var pt in clone.GetComponentsInChildren<PropTarget>(true))
                    Destroy(pt);
                foreach (var c in clone.GetComponentsInChildren<Collider>(true))
                    Destroy(c);
            }
        }

        // 3. Fallback: if unmorphed on remote client, try local morph copy or human avatar
        if (clone == null)
        {
            if (_morph != null)
                clone = _morph.CreateCurrentVisualCopy();

            if (clone == null)
            {
                clone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                clone.transform.position = pos + new Vector3(0f, 0.9f, 0f);
                clone.transform.rotation = rot;
                clone.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            }
        }

        clone.name = $"DecoyClone_{_decoyClones.Count + 1}";
        var decoyComp = clone.GetComponent<DecoyTarget>() ?? clone.AddComponent<DecoyTarget>();
        decoyComp.OwnerClientId = OwnerClientId;
        decoyComp.OwnerPlayer = this;

        ProceduralAudio.PlayDecoyPuff(pos);

        // Max 2 clones rule: if more than 2, remove oldest
        if (_decoyClones.Count >= 2)
        {
            if (_decoyClones[0] != null)
            {
                ProceduralAudio.PlayDecoyPuff(_decoyClones[0].transform.position);
                Destroy(_decoyClones[0]);
            }
            _decoyClones.RemoveAt(0);
        }

        _decoyClones.Add(clone);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void DestroyDecoyRpc(ulong ownerClientId, Vector3 pos)
    {
        ProceduralAudio.PlayDecoyPuff(pos);
        var decoys = UnityEngine.Object.FindObjectsByType<DecoyTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < decoys.Length; i++)
        {
            if (decoys[i] != null && decoys[i].OwnerClientId == ownerClientId && Vector3.Distance(decoys[i].transform.position, pos) < 1.5f)
            {
                if (decoys[i].OwnerPlayer != null)
                    decoys[i].OwnerPlayer.OnDecoyDestroyed(decoys[i].gameObject);
                Destroy(decoys[i].gameObject);
                break;
            }
        }
    }

    public void OnDecoyDestroyed(GameObject decoy)
    {
        _decoyClones.Remove(decoy);
    }

    void ClearDecoys()
    {
        for (int i = _decoyClones.Count - 1; i >= 0; i--)
        {
            if (_decoyClones[i] != null)
                Destroy(_decoyClones[i]);
        }
        _decoyClones.Clear();
    }

    void AddScore(int delta)
    {
        if (IsServer || _isOfflineSolo)
            Score.Value += delta;
        else if (IsOwner)
            AddScoreRpc(delta);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void AddScoreRpc(int delta)
    {
        Score.Value += delta;
    }

    public void SetPaused(bool paused)
    {
        if (!IsLocalControlled) return;
        _isPaused = paused;
        ApplyInputGate();
    }

    public void RequestMorph(PropTarget target) => HandleLocalMorphRequested(target);
    public void RequestRevert() => HandleLocalRevertRequested();

    void PositionAtSpawn(ulong clientId)
    {
        int slot = (int)(clientId % 8UL);
        int row = slot / 4;
        int col = slot % 4;
        Vector3 offset = new Vector3(-row * 1.4f, 0f, (col - 1.5f) * 1.35f);
        if (_cc != null) _cc.enabled = false;
        transform.SetPositionAndRotation(BaseSpawnPosition + offset, Quaternion.Euler(BaseSpawnEuler));
        if (_cc != null) _cc.enabled = true;
    }

    void HandleMatchStartedChanged(bool started)
    {
        if (!IsOwner) return;
        _gameplayActive = started;
        _isPaused = false;
        SetLocalCameraActive(started);
        ApplyInputGate();
        ApplyRoleSpeeds();
    }

    void SetLocalCameraActive(bool active)
    {
        if (_playerCamera != null)
        {
            _playerCamera.enabled = active;
            _playerCamera.gameObject.tag = active ? "MainCamera" : "Untagged";
        }
        if (_audioListener != null)
            _audioListener.enabled = active;
    }

    void ApplyInputGate()
    {
        if (!IsLocalControlled) return;
        bool live = _gameplayActive && !_isPaused;
        bool downed = IsEliminated.Value;
        bool hunterFrozen = IsHunterFrozen;
        _hunterFrozenApplied = hunterFrozen;

        if (_roam != null)
        {
            // Look stays enabled while downed (spectating) and for the frozen Hunter; only movement is locked.
            _roam.inputEnabled = live;
            _roam.movementLocked = downed || hunterFrozen;
            _roam.SetCursorLock(live);

            // Downed Hiders become free-flying spectators; revived players snap back into their body.
            if (downed && !_roam.IsSpectatorFlying) { _roam.SetFreeLook(false); _roam.SetSpectatorFly(true); _forcedFreeLook = true; }
            else if (!downed && _forcedFreeLook) { _roam.SetSpectatorFly(false); _forcedFreeLook = false; }
            if (CurrentRole != PlayerRole.Hider && !downed && _roam.IsFreeLook) _roam.SetFreeLook(false);
        }
        if (_morph != null)
            _morph.inputEnabled = live && !downed && CurrentRole == PlayerRole.Hider;
    }

    void HandleLocalMorphRequested(PropTarget target)
    {
        if (!IsLocalControlled || target == null || CurrentRole != PlayerRole.Hider) return;
        if (_isOfflineSolo)
        {
            _morph.MorphInto(target);
            MorphedPropIndex.Value = PropRegistry.Instance != null ? PropRegistry.Instance.IndexOf(target) : -1;
            return;
        }
        if (PropRegistry.Instance == null) return;
        int index = PropRegistry.Instance.IndexOf(target);
        if (index >= 0)
            RequestMorphRpc(index);
    }

    void HandleLocalRevertRequested()
    {
        if (!IsLocalControlled) return;
        if (_isOfflineSolo)
        {
            _morph.RevertToFirstPerson();
            MorphedPropIndex.Value = -1;
            return;
        }
        RequestMorphRpc(-1);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void SetPlayerNameRpc(string rawName)
    {
        string clean = string.IsNullOrWhiteSpace(rawName) ? $"Player {OwnerClientId}" : rawName.Trim();
        if (clean.Length > 24) clean = clean.Substring(0, 24);
        PlayerName.Value = new FixedString64Bytes(clean);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void RequestMorphRpc(int propIndex)
    {
        int count = PropRegistry.Instance != null ? PropRegistry.Instance.Count : 0;
        if (propIndex < -1 || propIndex >= count) return;
        MorphedPropIndex.Value = propIndex;
    }

    void HandlePlayerNameChanged(FixedString64Bytes prev, FixedString64Bytes current) => OnActivePlayersChanged?.Invoke();
    void HandleMorphedPropChanged(int prev, int current) => ApplyMorphIndex(current);

    void HandleRoleChanged(int prev, int current)
    {
        ApplyRoleSpeeds();
        ApplyInputGate();
        if ((PlayerRole)current == PlayerRole.Hunter && _morph != null)
        {
            _morph.RevertToFirstPerson();
            _morph.inputEnabled = false;
        }
        OnActivePlayersChanged?.Invoke();
    }

    void HandleEliminatedChanged(bool prev, bool current)
    {
        ApplyInputGate();

        if (current && !prev)
        {
            // Scream, 3 s flash, then pixel explosion; the body stays hidden while downed.
            DeathFx.Play(this, () =>
            {
                if (_morph != null && !IsLocalControlled) _morph.SetHumanAvatarVisible(false);
            });
        }
        else if (!current && prev)
        {
            if (_morph != null && !IsLocalControlled) _morph.SetHumanAvatarVisible(true);
        }

        OnActivePlayersChanged?.Invoke();
    }

    void ApplyMorphIndex(int propIndex)
    {
        if (_morph == null) return;
        if (propIndex < 0)
        {
            _morph.RevertToFirstPerson();
            return;
        }

        PropTarget target = PropRegistry.Instance != null ? PropRegistry.Instance.GetTarget(propIndex) : null;
        if (target != null)
            _morph.MorphInto(target);
    }
}
