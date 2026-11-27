using UnityEngine;

/// <summary>
/// Tuck Shop refrigerator revival zone (beside ColdDrinkFridge_1/2). While a living Hider stands inside
/// <see cref="zoneRadius"/> and teammates are downed, a 20 s channel runs (tracked in NetworkMatchState).
/// Visuals: the floor ring is ice blue when idle and pulses emerald green with a looping hum while channelling.
/// Driven on every peer from replicated state via <see cref="SetRevivingActive"/>.
/// </summary>
public class RefrigeratorRevivalStation : MonoBehaviour
{
    public static RefrigeratorRevivalStation Instance { get; private set; }

    public float zoneRadius = 3.6f;

    // Not serialized: a scene copy of this component used to carry stale coordinates (the ring kept
    // appearing on the East Gap walkway). Positions now come from NetworkMatchState and are ground-snapped.
    public Vector3 DefaultPosition => NetworkMatchState.FridgeZonePos;
    public Vector3 ReviveSpawnPosition => NetworkMatchState.SnapToGround(NetworkMatchState.FridgeReviveSpawn);

    GameObject _floorRing;
    Material _ringMat;
    AudioSource _hum;
    readonly Color _frostColor = new Color(0.2f, 0.7f, 1.0f, 0.7f);
    readonly Color _activeReviveColor = new Color(0.1f, 1.0f, 0.5f, 0.9f);

    bool _isRevivingActive;
    float _blend; // 0 = frost, 1 = emerald

    public bool IsRevivingActive => _isRevivingActive;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        transform.SetParent(null, true);
        transform.position = NetworkMatchState.SnapToGround(DefaultPosition);
        BuildVisuals();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BuildVisuals()
    {
        _floorRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _floorRing.name = "ReviveRing";
        _floorRing.transform.SetParent(transform, false);
        _floorRing.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        _floorRing.transform.localScale = new Vector3(zoneRadius * 2f, 0.06f, zoneRadius * 2f);

        var col = _floorRing.GetComponent<Collider>();
        if (col != null) Destroy(col);

        _ringMat = CreateGlowMaterial(_frostColor);
        var rend = _floorRing.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = _ringMat;

        _hum = gameObject.AddComponent<AudioSource>();
        _hum.clip = ProceduralAudio.BeaconHumClip;
        _hum.loop = true;
        _hum.playOnAwake = false;
        _hum.spatialBlend = 1f;
        _hum.rolloffMode = AudioRolloffMode.Linear;
        _hum.minDistance = 2f;
        _hum.maxDistance = 22f;
        _hum.volume = 0.55f;
    }

    public void SetRevivingActive(bool active)
    {
        if (_isRevivingActive == active) return;
        _isRevivingActive = active;

        if (_hum != null)
        {
            if (active && !_hum.isPlaying) _hum.Play();
            else if (!active && _hum.isPlaying) _hum.Stop();
        }
    }

    public bool IsPlayerInside(Vector3 playerPos)
    {
        Vector2 station2D = new Vector2(transform.position.x, transform.position.z);
        Vector2 player2D = new Vector2(playerPos.x, playerPos.z);
        return Vector2.Distance(station2D, player2D) <= zoneRadius && Mathf.Abs(playerPos.y - transform.position.y) < 3.0f;
    }

    void Update()
    {
        _blend = Mathf.MoveTowards(_blend, _isRevivingActive ? 1f : 0f, Time.deltaTime * 2.5f);

        if (_floorRing != null)
            _floorRing.transform.Rotate(Vector3.up, Mathf.Lerp(15f, 60f, _blend) * Time.deltaTime);

        if (_ringMat != null)
        {
            float pulse = _isRevivingActive ? 0.75f + 0.25f * Mathf.Sin(Time.time * 6f) : 1f;
            Color c = Color.Lerp(_frostColor, _activeReviveColor, _blend);
            c = new Color(c.r * pulse, c.g * pulse, c.b * pulse, c.a);
            if (_ringMat.HasProperty("_BaseColor")) _ringMat.SetColor("_BaseColor", c);
            else _ringMat.color = c;
        }

        if (_hum != null && _hum.isPlaying)
            _hum.pitch = 0.9f + 0.1f * Mathf.Sin(Time.time * 3f);
    }

    static Material CreateGlowMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
        return mat;
    }
}
