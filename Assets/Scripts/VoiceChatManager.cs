using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Zero-setup voice chat over Netcode for GameObjects (no Vivox / cloud dashboard needed).
/// Mic audio is captured with <see cref="Microphone"/>, cut into 20 ms frames, mu-law encoded (8 bit),
/// sent to the server as a named message, and the server routes each frame by channel rules:
///  - Lobby (no match running): everyone hears everyone (2D).
///  - Living Hider: Hider team radio (2D) to all Hiders + 3D proximity (~15 m) to a nearby Hunter.
///  - Hunter: 3D proximity (~15 m) only.
///  - Downed Hider: ghost channel - only other downed Hiders hear them, so they can't call out the Hunter.
/// Controls: hold V = push-to-talk, B = toggle open-mic (voice activation).
/// </summary>
public class VoiceChatManager : MonoBehaviour
{
    public enum MicStatus { Unavailable, Muted, TransmittingPtt, OpenMicListening, TransmittingOpenMic }

    public const float ProximityRadius = 15f;
    const string MsgUp = "LarpSeek_VoiceUp";
    const string MsgDown = "LarpSeek_VoiceDown";
    const int TargetSampleRate = 16000;
    const float FrameSeconds = 0.02f;
    const float VadThreshold = 0.02f;
    const float VadHangSeconds = 0.35f;
    const byte ModeNone = 0, ModeRadio2D = 1, ModeProximity3D = 2;

    public static VoiceChatManager Instance { get; private set; }

    public MicStatus Status { get; private set; } = MicStatus.Unavailable;
    public bool OpenMicEnabled { get; private set; }
    public bool HasMicrophone => _micDevice != null;

    // Capture
    string _micDevice;
    AudioClip _micClip;
    int _micReadPos;
    int _micRate;
    float[] _frame;
    byte[] _encoded;
    float _vadHangTimer;
    bool _transmitting;

    // Networking
    CustomMessagingManager _registeredOn;
    readonly Dictionary<ulong, RemoteVoice> _remotes = new();
    byte[] _rxBuffer = new byte[2048];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("VoiceChatManager");
        DontDestroyOnLoad(go);
        go.AddComponent<VoiceChatManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        UnregisterHandlers();
        StopMic();
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ Update loop

    void Update()
    {
        var nm = NetworkManager.Singleton;
        bool online = nm != null && nm.IsListening && (nm.IsServer || nm.IsConnectedClient) &&
                      !(MultiplayerSessionManager.Instance != null &&
                        MultiplayerSessionManager.Instance.ConnectionMode == SessionConnectionMode.SoloOffline);

        EnsureHandlers(online ? nm.CustomMessagingManager : null);

        if (!online)
        {
            _transmitting = false;
            Status = MicStatus.Unavailable;
            if (_remotes.Count > 0) ClearRemotes();
            StopMic();
            return;
        }

        if (_micClip == null) StartMic();
        HandleInput(out bool pttHeld);
        PumpMicrophone(nm, pttHeld);
        UpdateRemotes();
    }

    void HandleInput(out bool pttHeld)
    {
        bool togglePressed, speakerTogglePressed;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        pttHeld = kb != null && kb.vKey.isPressed;
        togglePressed = kb != null && kb.bKey.wasPressedThisFrame;
        speakerTogglePressed = kb != null && kb.mKey.wasPressedThisFrame;
#else
        pttHeld = Input.GetKey(KeyCode.V);
        togglePressed = Input.GetKeyDown(KeyCode.B);
        speakerTogglePressed = Input.GetKeyDown(KeyCode.M);
#endif
        // Don't hijack keys while typing in the menu.
        bool inGameplay = MultiplayerSessionManager.Instance != null && MultiplayerSessionManager.Instance.IsInMatch
                          ? Cursor.lockState == CursorLockMode.Locked
                          : true;
        if (!inGameplay) { pttHeld = false; togglePressed = false; speakerTogglePressed = false; }
        if (togglePressed) OpenMicEnabled = !OpenMicEnabled;
        if (speakerTogglePressed) SpeakerMuted = !SpeakerMuted;
    }

    /// <summary>When true, other players' voices are silenced locally (toggle with M).</summary>
    public bool SpeakerMuted { get; private set; }
    public void ToggleMic() => OpenMicEnabled = !OpenMicEnabled;
    public void ToggleSpeaker() => SpeakerMuted = !SpeakerMuted;

    // ------------------------------------------------------------------ Capture

    void StartMic()
    {
        if (Microphone.devices == null || Microphone.devices.Length == 0) { _micDevice = null; return; }
        _micDevice = Microphone.devices[0];

        Microphone.GetDeviceCaps(_micDevice, out int minFreq, out int maxFreq);
        int rate = TargetSampleRate;
        if (minFreq != 0 || maxFreq != 0) rate = Mathf.Clamp(rate, minFreq, maxFreq == 0 ? rate : maxFreq);

        _micClip = Microphone.Start(_micDevice, true, 1, rate);
        if (_micClip == null) { _micDevice = null; return; }
        _micRate = _micClip.frequency;
        int frameSamples = Mathf.Max(64, Mathf.RoundToInt(_micRate * FrameSeconds));
        _frame = new float[frameSamples];
        _encoded = new byte[frameSamples];
        _micReadPos = 0;
    }

    void StopMic()
    {
        if (_micClip != null && _micDevice != null && Microphone.IsRecording(_micDevice))
            Microphone.End(_micDevice);
        _micClip = null;
    }

    void PumpMicrophone(NetworkManager nm, bool pttHeld)
    {
        if (_micClip == null || _micDevice == null)
        {
            Status = MicStatus.Unavailable;
            _transmitting = false;
            return;
        }

        int writePos = Microphone.GetPosition(_micDevice);
        int len = _micClip.samples;
        int available = (writePos - _micReadPos + len) % len;
        bool anyTx = false;

        while (available >= _frame.Length)
        {
            _micClip.GetData(_frame, _micReadPos);
            _micReadPos = (_micReadPos + _frame.Length) % len;
            available -= _frame.Length;

            bool send = pttHeld;
            if (!send && OpenMicEnabled)
            {
                float rms = 0f;
                for (int i = 0; i < _frame.Length; i++) rms += _frame[i] * _frame[i];
                rms = Mathf.Sqrt(rms / _frame.Length);
                if (rms > VadThreshold) _vadHangTimer = VadHangSeconds;
                send = _vadHangTimer > 0f;
            }

            if (send)
            {
                anyTx = true;
                for (int i = 0; i < _frame.Length; i++) _encoded[i] = MuLawEncode(_frame[i]);
                SendFrame(nm, _encoded, _frame.Length, (ushort)_micRate);
            }
        }

        _vadHangTimer = Mathf.Max(0f, _vadHangTimer - Time.unscaledDeltaTime);
        _transmitting = anyTx || (_transmitting && (pttHeld || _vadHangTimer > 0f));

        if (pttHeld) Status = MicStatus.TransmittingPtt;
        else if (OpenMicEnabled) Status = _transmitting ? MicStatus.TransmittingOpenMic : MicStatus.OpenMicListening;
        else Status = MicStatus.Muted;
    }

    void SendFrame(NetworkManager nm, byte[] data, int count, ushort rate)
    {
        if (nm.IsServer)
        {
            RouteFrame(nm, nm.LocalClientId, rate, data, count);
            return;
        }

        using var writer = new FastBufferWriter(count + 8, Allocator.Temp);
        writer.WriteValueSafe(rate);
        writer.WriteValueSafe((ushort)count);
        writer.WriteBytesSafe(data, count);
        nm.CustomMessagingManager.SendNamedMessage(MsgUp, NetworkManager.ServerClientId, writer, NetworkDelivery.UnreliableSequenced);
    }

    // ------------------------------------------------------------------ Server routing

    void EnsureHandlers(CustomMessagingManager cmm)
    {
        if (cmm == _registeredOn) return;
        UnregisterHandlers();
        if (cmm == null) return;
        cmm.RegisterNamedMessageHandler(MsgUp, OnVoiceUp);
        cmm.RegisterNamedMessageHandler(MsgDown, OnVoiceDown);
        _registeredOn = cmm;
    }

    void UnregisterHandlers()
    {
        if (_registeredOn == null) return;
        try
        {
            _registeredOn.UnregisterNamedMessageHandler(MsgUp);
            _registeredOn.UnregisterNamedMessageHandler(MsgDown);
        }
        catch (Exception) { /* manager already torn down */ }
        _registeredOn = null;
    }

    void OnVoiceUp(ulong senderClientId, FastBufferReader reader)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        if (!ReadFrame(reader, out ushort rate, out int count)) return;
        RouteFrame(nm, senderClientId, rate, _rxBuffer, count);
    }

    void RouteFrame(NetworkManager nm, ulong senderId, ushort rate, byte[] data, int count)
    {
        NetworkPlayerController sender = FindPlayer(senderId);
        bool matchActive = NetworkMatchState.Instance != null && NetworkMatchState.Instance.IsMatchActive;

        foreach (ulong clientId in nm.ConnectedClientsIds)
        {
            if (clientId == senderId) continue;
            NetworkPlayerController receiver = FindPlayer(clientId);
            byte mode = Classify(sender, receiver, matchActive);
            if (mode == ModeNone) continue;

            if (clientId == nm.LocalClientId)
            {
                // Host listening locally.
                ReceiveFrame(senderId, mode, rate, data, count);
                continue;
            }

            using var writer = new FastBufferWriter(count + 16, Allocator.Temp);
            writer.WriteValueSafe(senderId);
            writer.WriteValueSafe(mode);
            writer.WriteValueSafe(rate);
            writer.WriteValueSafe((ushort)count);
            writer.WriteBytesSafe(data, count);
            nm.CustomMessagingManager.SendNamedMessage(MsgDown, clientId, writer, NetworkDelivery.UnreliableSequenced);
        }
    }

    static byte Classify(NetworkPlayerController s, NetworkPlayerController r, bool matchActive)
    {
        if (!matchActive || s == null || r == null) return ModeRadio2D; // lobby / pre-spawn: open channel

        bool near = Vector3.Distance(s.transform.position, r.transform.position) <= ProximityRadius;
        bool senderHider = s.CurrentRole == PlayerRole.Hider;
        bool receiverHider = r.CurrentRole == PlayerRole.Hider;

        if (senderHider && s.IsEliminated.Value)
            return receiverHider && r.IsEliminated.Value ? ModeRadio2D : ModeNone; // ghost channel

        if (senderHider)
        {
            return receiverHider ? ModeRadio2D : ModeNone;   // team radio; the Hunter never hears Hiders
        }

        return near ? ModeProximity3D : ModeNone;            // Hunter: proximity only
    }

    static NetworkPlayerController FindPlayer(ulong clientId)
    {
        var list = NetworkPlayerController.ActivePlayers;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].IsSpawned && list[i].OwnerClientId == clientId) return list[i];
        return null;
    }

    // ------------------------------------------------------------------ Client receive / playback

    void OnVoiceDown(ulong _, FastBufferReader reader)
    {
        if (!reader.TryBeginRead(FastBufferWriter.GetWriteSize<ulong>() + 1)) return;
        reader.ReadValue(out ulong senderId);
        reader.ReadValue(out byte mode);
        if (!ReadFrame(reader, out ushort rate, out int count)) return;
        ReceiveFrame(senderId, mode, rate, _rxBuffer, count);
    }

    bool ReadFrame(FastBufferReader reader, out ushort rate, out int count)
    {
        rate = 0; count = 0;
        if (!reader.TryBeginRead(4)) return false;
        reader.ReadValue(out rate);
        reader.ReadValue(out ushort len);
        count = len;
        if (count <= 0 || count > 4096 || rate < 4000 || rate > 48000) return false;
        if (_rxBuffer.Length < count) _rxBuffer = new byte[count];
        if (!reader.TryBeginRead(count)) return false;
        reader.ReadBytes(ref _rxBuffer, count);
        return true;
    }

    void ReceiveFrame(ulong senderId, byte mode, ushort rate, byte[] data, int count)
    {
        if (!_remotes.TryGetValue(senderId, out var rv) || rv.SampleRate != rate)
        {
            rv?.Dispose();
            rv = new RemoteVoice(senderId, rate, transform);
            _remotes[senderId] = rv;
        }
        rv.Push(data, count, mode);
    }

    void UpdateRemotes()
    {
        List<ulong> stale = null;
        foreach (var kv in _remotes)
        {
            NetworkPlayerController p = FindPlayer(kv.Key);
            if (p == null && Time.unscaledTime - kv.Value.LastHeard > 5f)
            {
                (stale ??= new List<ulong>()).Add(kv.Key);
                continue;
            }
            kv.Value.Tick(p, SpeakerMuted);
        }
        if (stale != null)
            foreach (var id in stale) { _remotes[id].Dispose(); _remotes.Remove(id); }
    }

    void ClearRemotes()
    {
        foreach (var rv in _remotes.Values) rv.Dispose();
        _remotes.Clear();
    }

    // ------------------------------------------------------------------ HUD queries

    public bool IsLocalTransmitting => _transmitting;

    public bool IsPlayerSpeaking(ulong clientId) =>
        _remotes.TryGetValue(clientId, out var rv) && rv.IsSpeaking;

    public void GetSpeakingPlayers(List<string> names)
    {
        names.Clear();
        foreach (var kv in _remotes)
        {
            if (!kv.Value.IsSpeaking) continue;
            var p = FindPlayer(kv.Key);
            string tag = kv.Value.LastMode == ModeProximity3D ? " (nearby)" : "";
            names.Add((p != null ? p.DisplayName : $"Player {kv.Key}") + tag);
        }
    }

    // ------------------------------------------------------------------ mu-law codec

    static byte MuLawEncode(float sample)
    {
        const int bias = 0x84, clip = 32635;
        int pcm = Mathf.Clamp(Mathf.RoundToInt(sample * 32767f), -32768, 32767);
        int sign = (pcm >> 8) & 0x80;
        if (sign != 0) pcm = -pcm;
        if (pcm > clip) pcm = clip;
        pcm += bias;
        int exponent = 7;
        for (int mask = 0x4000; (pcm & mask) == 0 && exponent > 0; mask >>= 1) exponent--;
        int mantissa = (pcm >> (exponent + 3)) & 0x0F;
        return (byte)~(sign | (exponent << 4) | mantissa);
    }

    static readonly float[] s_MuLawTable = BuildMuLawTable();

    static float[] BuildMuLawTable()
    {
        var t = new float[256];
        for (int i = 0; i < 256; i++)
        {
            int u = ~i & 0xFF;
            int sign = u & 0x80, exponent = (u >> 4) & 0x07, mantissa = u & 0x0F;
            int sample = (((mantissa << 3) + 0x84) << exponent) - 0x84;
            t[i] = (sign != 0 ? -sample : sample) / 32768f;
        }
        return t;
    }

    // ------------------------------------------------------------------ Remote speaker

    sealed class RemoteVoice
    {
        public readonly int SampleRate;
        public float LastHeard = -100f;
        public byte LastMode = ModeRadio2D;
        public bool IsSpeaking => Time.unscaledTime - LastHeard < 0.3f;

        readonly GameObject _go;
        readonly AudioSource _source;
        readonly TextMesh _waves;
        readonly float[] _ring;
        readonly object _lock = new();
        int _read, _write, _count;
        bool _primed;
        readonly int _primeSamples;

        public RemoteVoice(ulong clientId, int sampleRate, Transform parent)
        {
            SampleRate = sampleRate;
            _ring = new float[sampleRate]; // 1 s jitter buffer
            _primeSamples = Mathf.RoundToInt(sampleRate * 0.08f);

            _go = new GameObject($"Voice_{clientId}");
            _go.transform.SetParent(parent, false);
            _source = _go.AddComponent<AudioSource>();
            _source.clip = AudioClip.Create($"VoiceStream_{clientId}", sampleRate, 1, sampleRate, true, OnAudioRead);
            _source.loop = true;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = 1.5f;
            _source.maxDistance = ProximityRadius;
            _source.dopplerLevel = 0f;
            _source.Play();

            var wavesGo = new GameObject("SpeakerWaves");
            wavesGo.transform.SetParent(_go.transform, false);
            wavesGo.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            _waves = wavesGo.AddComponent<TextMesh>();
            _waves.text = "<)))";
            _waves.anchor = TextAnchor.MiddleCenter;
            _waves.characterSize = 0.06f;
            _waves.fontSize = 48;
            _waves.color = new Color(0.4f, 1f, 0.6f);
            _waves.gameObject.SetActive(false);
        }

        public void Push(byte[] data, int count, byte mode)
        {
            LastHeard = Time.unscaledTime;
            LastMode = mode;
            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                {
                    if (_count == _ring.Length) { _read = (_read + 1) % _ring.Length; _count--; } // drop oldest
                    _ring[_write] = s_MuLawTable[data[i]];
                    _write = (_write + 1) % _ring.Length;
                    _count++;
                }
            }
        }

        void OnAudioRead(float[] buffer)
        {
            lock (_lock)
            {
                if (!_primed && _count >= _primeSamples) _primed = true;
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (_primed && _count > 0)
                    {
                        buffer[i] = _ring[_read];
                        _read = (_read + 1) % _ring.Length;
                        _count--;
                    }
                    else
                    {
                        buffer[i] = 0f;
                        _primed = false; // underrun -> re-buffer
                    }
                }
            }
        }

        public void Tick(NetworkPlayerController speaker, bool muted)
        {
            if (_source.mute != muted) _source.mute = muted;
            bool spatial = LastMode == ModeProximity3D && speaker != null;
            _source.spatialBlend = spatial ? 1f : 0f;
            if (speaker != null)
                _go.transform.position = speaker.transform.position;

            bool showWaves = IsSpeaking && speaker != null && !speaker.IsLocalControlled;
            if (_waves.gameObject.activeSelf != showWaves) _waves.gameObject.SetActive(showWaves);
            if (showWaves)
            {
                var cam = Camera.main;
                if (cam != null) _waves.transform.rotation = Quaternion.LookRotation(_waves.transform.position - cam.transform.position);
                float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 12f);
                _waves.transform.localScale = Vector3.one * pulse;
            }
        }

        public void Dispose()
        {
            if (_source != null) _source.Stop();
            if (_go != null) UnityEngine.Object.Destroy(_go);
        }
    }
}
