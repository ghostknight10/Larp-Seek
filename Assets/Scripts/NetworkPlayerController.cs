using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Synchronizes player identity, owner-gated camera/input, and replicated prop morph state.
/// Supports both NGO networked sessions and offline solo instantiation.
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

    public NetworkVariable<FixedString64Bytes> PlayerName = new(
        new FixedString64Bytes("Player"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> MorphedPropIndex = new(
        -1,
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

    public bool IsLocalControlled => _isOfflineSolo || IsOwner;
    public string DisplayName => PlayerName.Value.Length > 0 ? PlayerName.Value.ToString() : $"Player {OwnerClientId}";
    public string CurrentMorphName => _morph != null ? _morph.CurrentMorphName : "Human";
    public bool IsMorphed => _morph != null && _morph.IsMorphed;

    void Awake()
    {
        _roam = GetComponent<FirstPersonRoam>();
        _morph = GetComponent<PropMorphController>();
        _cc = GetComponent<CharacterController>();
        _playerCamera = GetComponentInChildren<Camera>(true);
        _audioListener = GetComponentInChildren<AudioListener>(true);
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
        OnActivePlayersChanged?.Invoke();
    }

    public override void OnNetworkSpawn()
    {
        if (!ActivePlayers.Contains(this))
            ActivePlayers.Add(this);

        MorphedPropIndex.OnValueChanged += HandleMorphedPropChanged;
        PlayerName.OnValueChanged += HandlePlayerNameChanged;
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
        OnActivePlayersChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        MorphedPropIndex.OnValueChanged -= HandleMorphedPropChanged;
        PlayerName.OnValueChanged -= HandlePlayerNameChanged;
        NetworkMatchState.MatchStartedChanged -= HandleMatchStartedChanged;

        if (_morph != null)
        {
            _morph.OnLocalMorphRequested = null;
            _morph.OnLocalRevertRequested = null;
        }

        if (LocalInstance == this)
            LocalInstance = null;

        ActivePlayers.Remove(this);
        OnActivePlayersChanged?.Invoke();
    }

    public override void OnDestroy()
    {
        if (LocalInstance == this)
            LocalInstance = null;
        if (ActivePlayers.Remove(this))
            OnActivePlayersChanged?.Invoke();
        base.OnDestroy();
    }

    public void SetPaused(bool paused)
    {
        if (!IsLocalControlled) return;
        _isPaused = paused;
        ApplyInputGate();
    }

    public void RequestMorph(PropTarget target)
    {
        HandleLocalMorphRequested(target);
    }

    public void RequestRevert()
    {
        HandleLocalRevertRequested();
    }

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
        bool activePlay = _gameplayActive && !_isPaused;
        if (_roam != null)
        {
            _roam.inputEnabled = activePlay;
            _roam.SetCursorLock(activePlay);
        }
        if (_morph != null)
            _morph.inputEnabled = activePlay;
    }

    void HandleLocalMorphRequested(PropTarget target)
    {
        if (!IsLocalControlled || target == null) return;
        if (_isOfflineSolo)
        {
            _morph.MorphInto(target);
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

    void HandlePlayerNameChanged(FixedString64Bytes prev, FixedString64Bytes current)
    {
        OnActivePlayersChanged?.Invoke();
    }

    void HandleMorphedPropChanged(int prev, int current)
    {
        ApplyMorphIndex(current);
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
