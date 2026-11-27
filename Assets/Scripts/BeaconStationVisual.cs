using UnityEngine;

/// <summary>
/// Visual and spatial representation of the capture beacon stations.
/// Emits a towering vertical laser into the sky, dynamic Point + Spot lights,
/// and provides capture radius detection for Hiders.
/// </summary>
[ExecuteAlways]
public class BeaconStationVisual : MonoBehaviour
{
    public float captureRadius = 1.6f;
    public int stationIndex = 1;
    public string displayName = "";

    public string DisplayName => string.IsNullOrEmpty(displayName) ? NetworkMatchState.StationLabel(stationIndex) : displayName;
    public static Vector3 DefaultPositionFor(int index) =>
        index == 1 ? NetworkMatchState.LoungeStationPos : NetworkMatchState.CafeteriaStationPos;

    GameObject _beamObj;
    GameObject _outerBeamObj;
    GameObject _baseDisc;
    Material _beamMat;
    Material _outerBeamMat;
    Material _discMat;

    Light _pointLight;
    Light _spotLight;

    Color _idleColor = new Color(0.15f, 0.75f, 1.0f, 0.85f);  // Vibrant Cyan
    Color _activeColor = new Color(1.0f, 0.82f, 0.15f, 0.95f); // Golden Amber

    bool _hasHiderInside;

    void Awake()
    {
        EnsureVisuals();
    }

    void OnEnable()
    {
        // Keep edit-mode scene copies anchored to the Lounge / Cafeteria so beams, discs and lights line up.
        if (!Application.isPlaying)
            transform.position = DefaultPositionFor(stationIndex);
        EnsureVisuals();
    }

    public void RebuildVisuals()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
        BuildVisuals();
    }

    public void EnsureVisuals()
    {
        var bd = transform.Find("BaseDisc");
        if (bd != null) _baseDisc = bd.gameObject;

        var core = transform.Find("LaserBeam_Core");
        if (core != null) _beamObj = core.gameObject;

        var aura = transform.Find("LaserBeam_Aura");
        if (aura != null) _outerBeamObj = aura.gameObject;

        var pl = transform.Find("Station_PointLight");
        if (pl != null) _pointLight = pl.GetComponent<Light>();

        var sl = transform.Find("Station_SpotLight");
        if (sl != null) _spotLight = sl.GetComponent<Light>();

        if (_baseDisc != null && _beamObj != null && _outerBeamObj != null && _pointLight != null && _spotLight != null)
        {
            float beamHeight = 85f;
            _beamObj.transform.localScale = new Vector3(0.18f, beamHeight * 0.5f, 0.18f);
            _outerBeamObj.transform.localScale = new Vector3(0.36f, beamHeight * 0.5f, 0.36f);
            _baseDisc.transform.localScale = new Vector3(captureRadius * 2f, 0.06f, captureRadius * 2f);

            if (_beamMat == null && _beamObj.GetComponent<Renderer>() != null)
                _beamMat = _beamObj.GetComponent<Renderer>().sharedMaterial;
            if (_outerBeamMat == null && _outerBeamObj.GetComponent<Renderer>() != null)
                _outerBeamMat = _outerBeamObj.GetComponent<Renderer>().sharedMaterial;
            if (_discMat == null && _baseDisc.GetComponent<Renderer>() != null)
                _discMat = _baseDisc.GetComponent<Renderer>().sharedMaterial;
            return;
        }

        // Clean broken or partial children if any
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }

        BuildVisuals();
    }

    void BuildVisuals()
    {
        // 1. Base pedestal disc (compact glowing platform)
        _baseDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _baseDisc.name = "BaseDisc";
        _baseDisc.transform.SetParent(transform, false);
        _baseDisc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        _baseDisc.transform.localScale = new Vector3(captureRadius * 2f, 0.06f, captureRadius * 2f);

        var discCol = _baseDisc.GetComponent<Collider>();
        if (discCol != null)
        {
            if (Application.isPlaying) Destroy(discCol);
            else DestroyImmediate(discCol);
        }

        _discMat = CreateGlowMaterial(_idleColor, 2.0f);
        var discRend = _baseDisc.GetComponent<Renderer>();
        if (discRend != null) discRend.sharedMaterial = _discMat;

        // 2. Vertical Core Laser Beam shooting up into the sky (thin pipe laser)
        float beamHeight = 85f;
        _beamObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _beamObj.name = "LaserBeam_Core";
        _beamObj.transform.SetParent(transform, false);
        _beamObj.transform.localPosition = new Vector3(0f, beamHeight * 0.5f, 0f);
        _beamObj.transform.localScale = new Vector3(0.18f, beamHeight * 0.5f, 0.18f);

        var beamCol = _beamObj.GetComponent<Collider>();
        if (beamCol != null)
        {
            if (Application.isPlaying) Destroy(beamCol);
            else DestroyImmediate(beamCol);
        }

        _beamMat = CreateGlowMaterial(_idleColor, 4.0f);
        var beamRend = _beamObj.GetComponent<Renderer>();
        if (beamRend != null) beamRend.sharedMaterial = _beamMat;

        // 3. Outer Aura Beam Cylinder (thin glowing wrap)
        _outerBeamObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _outerBeamObj.name = "LaserBeam_Aura";
        _outerBeamObj.transform.SetParent(transform, false);
        _outerBeamObj.transform.localPosition = new Vector3(0f, beamHeight * 0.5f, 0f);
        _outerBeamObj.transform.localScale = new Vector3(0.36f, beamHeight * 0.5f, 0.36f);

        var auraCol = _outerBeamObj.GetComponent<Collider>();
        if (auraCol != null)
        {
            if (Application.isPlaying) Destroy(auraCol);
            else DestroyImmediate(auraCol);
        }

        _outerBeamMat = CreateGlowMaterial(new Color(_idleColor.r, _idleColor.g, _idleColor.b, 0.35f), 1.5f);
        var auraRend = _outerBeamObj.GetComponent<Renderer>();
        if (auraRend != null) auraRend.sharedMaterial = _outerBeamMat;

        // 4. Real Ground Point Light (casts bright light on surrounding floor and players)
        var plObj = new GameObject("Station_PointLight");
        plObj.transform.SetParent(transform, false);
        plObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        _pointLight = plObj.AddComponent<Light>();
        _pointLight.type = LightType.Point;
        _pointLight.color = _idleColor;
        _pointLight.intensity = 7.5f;
        _pointLight.range = 18f;

        // 5. Real Upward Spot Light (shoots vertical beam of light into the sky)
        var slObj = new GameObject("Station_SpotLight");
        slObj.transform.SetParent(transform, false);
        slObj.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        slObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        _spotLight = slObj.AddComponent<Light>();
        _spotLight.type = LightType.Spot;
        _spotLight.color = _idleColor;
        _spotLight.intensity = 15.0f;
        _spotLight.range = 80f;
        _spotLight.spotAngle = 40f;
        _spotLight.innerSpotAngle = 20f;
    }

    public bool IsCapturing => _hasHiderInside;

    public void SetCapturing(bool isCapturing)
    {
        if (_hasHiderInside == isCapturing && _beamMat != null) return;
        _hasHiderInside = isCapturing;
        Color col = isCapturing ? _activeColor : _idleColor;

        UpdateMaterialColor(_beamMat, col, isCapturing ? 6.0f : 4.0f);
        UpdateMaterialColor(_outerBeamMat, new Color(col.r, col.g, col.b, isCapturing ? 0.5f : 0.35f), isCapturing ? 2.5f : 1.5f);
        UpdateMaterialColor(_discMat, col, isCapturing ? 3.5f : 2.0f);

        if (_pointLight != null)
        {
            _pointLight.color = col;
            _pointLight.intensity = isCapturing ? 12.0f : 7.5f;
        }

        if (_spotLight != null)
        {
            _spotLight.color = col;
            _spotLight.intensity = isCapturing ? 25.0f : 15.0f;
        }

        if (_beamObj != null)
        {
            float width = isCapturing ? 0.28f : 0.18f;
            float beamHeight = 85f;
            _beamObj.transform.localScale = new Vector3(width, beamHeight * 0.5f, width);
        }

        if (_outerBeamObj != null)
        {
            float width = isCapturing ? 0.50f : 0.36f;
            float beamHeight = 85f;
            _outerBeamObj.transform.localScale = new Vector3(width, beamHeight * 0.5f, width);
        }
    }

    public bool IsPlayerInside(Vector3 playerPos)
    {
        Vector2 station2D = new Vector2(transform.position.x, transform.position.z);
        Vector2 player2D = new Vector2(playerPos.x, playerPos.z);
        return Vector2.Distance(station2D, player2D) <= captureRadius && Mathf.Abs(playerPos.y - transform.position.y) < 3.5f;
    }

    void Update()
    {
        if (_baseDisc != null)
        {
            _baseDisc.transform.Rotate(Vector3.up, (_hasHiderInside ? 80f : 25f) * Time.deltaTime);
        }

        float pulse = Mathf.Sin(Time.time * (_hasHiderInside ? 10f : 3.5f)) * 0.2f + 0.8f;
        Color baseCol = _hasHiderInside ? _activeColor : _idleColor;

        if (_beamMat != null)
        {
            UpdateMaterialColor(_beamMat, baseCol * pulse, (_hasHiderInside ? 6.0f : 4.0f) * pulse);
        }

        if (_pointLight != null)
        {
            _pointLight.intensity = (_hasHiderInside ? 12f : 7.5f) * pulse;
        }

        if (_spotLight != null)
        {
            _spotLight.intensity = (_hasHiderInside ? 25f : 15f) * pulse;
        }
    }

    static Material CreateGlowMaterial(Color color, float emissionMultiplier)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;

        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", color * emissionMultiplier);

        return mat;
    }

    static void UpdateMaterialColor(Material mat, Color col, float emissionMultiplier)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
        else mat.color = col;

        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", col * emissionMultiplier);
    }
}
