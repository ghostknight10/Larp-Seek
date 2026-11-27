using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Blocky low-poly pistol for the Hunter (seeker).
///  - Local Hunter: first-person viewmodel parented to the player camera (bob, sway, recoil kick).
///  - Remote Hunter: third-person copy held at hand height so other players can see who is armed.
/// Built procedurally from cubes, so no asset import is required. Added at runtime by NetworkPlayerController.
/// </summary>
public class HunterWeaponVisual : MonoBehaviour
{
    static readonly Vector3 FpRestPos = new Vector3(0.24f, -0.22f, 0.55f);
    static readonly Vector3 TpPos = new Vector3(0.32f, 1.15f, 0.42f);

    NetworkPlayerController _owner;
    Camera _camera;
    Transform _fpRoot;
    Transform _tpRoot;
    Transform _fpMuzzle;
    float _kick;
    float _bobTime;
    Vector3 _lastPos;

    static Material s_Body, s_Metal, s_Accent;

    public Vector3 MuzzlePosition => _fpMuzzle != null && _fpRoot.gameObject.activeInHierarchy
        ? _fpMuzzle.position
        : transform.position + Vector3.up * 1.5f;

    void Awake()
    {
        _owner = GetComponent<NetworkPlayerController>();
        _camera = GetComponentInChildren<Camera>(true);

        if (_camera != null)
        {
            _fpRoot = BuildGun("Hunter_FP_Gun", _camera.transform, out _fpMuzzle, firstPerson: true);
            _fpRoot.localPosition = FpRestPos;
            _fpRoot.localRotation = Quaternion.Euler(0f, -2f, 0f);
        }

        _tpRoot = BuildGun("Hunter_TP_Gun", transform, out _, firstPerson: false);
        _tpRoot.localPosition = TpPos;
        _tpRoot.localRotation = Quaternion.identity;
        _tpRoot.localScale = Vector3.one * 1.15f;

        SetVisible(_fpRoot, false);
        SetVisible(_tpRoot, false);
        _lastPos = transform.position;
    }

    public void Kick() => _kick = 1f;

    void LateUpdate()
    {
        if (_owner == null) return;

        bool armed = _owner.CurrentRole == PlayerRole.Hunter && !_owner.IsEliminated.Value;
        bool local = _owner.IsLocalControlled;
        bool cameraLive = _camera != null && _camera.enabled;

        SetVisible(_fpRoot, armed && local && cameraLive);
        SetVisible(_tpRoot, armed && !local);

        if (_fpRoot == null || !_fpRoot.gameObject.activeSelf) { _lastPos = transform.position; return; }

        // Keep the viewmodel from being clipped by the near plane.
        if (_camera.nearClipPlane > 0.05f) _camera.nearClipPlane = 0.05f;

        float dt = Time.deltaTime;
        Vector3 vel = (transform.position - _lastPos) / Mathf.Max(dt, 0.0001f);
        _lastPos = transform.position;
        float speed = new Vector2(vel.x, vel.z).magnitude;
        _bobTime += dt * Mathf.Lerp(0f, 9f, Mathf.Clamp01(speed / 6f));

        Vector3 bob = new Vector3(Mathf.Sin(_bobTime) * 0.012f, Mathf.Abs(Mathf.Cos(_bobTime)) * 0.014f, 0f) * Mathf.Clamp01(speed / 3f);

        _kick = Mathf.MoveTowards(_kick, 0f, dt * 7f);
        float k = _kick * _kick;
        _fpRoot.localPosition = FpRestPos + bob + new Vector3(0f, 0.015f * k, -0.09f * k);
        _fpRoot.localRotation = Quaternion.Euler(-14f * k, -2f, 0f);
    }

    static void SetVisible(Transform t, bool visible)
    {
        if (t != null && t.gameObject.activeSelf != visible) t.gameObject.SetActive(visible);
    }

    static Transform BuildGun(string name, Transform parent, out Transform muzzle, bool firstPerson)
    {
        EnsureMaterials();
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);

        // Slide / body
        Part(root, "Slide", new Vector3(0f, 0.02f, 0.02f), new Vector3(0.075f, 0.085f, 0.34f), s_Body, firstPerson);
        // Frame under the slide
        Part(root, "Frame", new Vector3(0f, -0.035f, 0.0f), new Vector3(0.07f, 0.04f, 0.26f), s_Metal, firstPerson);
        // Barrel poking out of the front
        Part(root, "Barrel", new Vector3(0f, 0.025f, 0.215f), new Vector3(0.035f, 0.035f, 0.08f), s_Metal, firstPerson);
        // Grip (angled back)
        var grip = Part(root, "Grip", new Vector3(0f, -0.115f, -0.085f), new Vector3(0.065f, 0.16f, 0.075f), s_Body, firstPerson);
        grip.localRotation = Quaternion.Euler(-14f, 0f, 0f);
        // Trigger guard
        Part(root, "TriggerGuard", new Vector3(0f, -0.075f, 0.015f), new Vector3(0.02f, 0.02f, 0.07f), s_Metal, firstPerson);
        // Sights + retro orange accent stripe
        Part(root, "RearSight", new Vector3(0f, 0.072f, -0.13f), new Vector3(0.05f, 0.02f, 0.02f), s_Metal, firstPerson);
        Part(root, "FrontSight", new Vector3(0f, 0.072f, 0.17f), new Vector3(0.015f, 0.02f, 0.015f), s_Accent, firstPerson);
        Part(root, "Stripe", new Vector3(0f, 0.02f, 0.02f), new Vector3(0.078f, 0.018f, 0.3f), s_Accent, firstPerson);

        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(root, false);
        muzzle.localPosition = new Vector3(0f, 0.025f, 0.27f);
        return root;
    }

    static Transform Part(Transform root, string name, Vector3 pos, Vector3 size, Material mat, bool firstPerson)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = firstPerson ? ShadowCastingMode.Off : ShadowCastingMode.On;
        r.receiveShadows = !firstPerson;
        return go.transform;
    }

    static void EnsureMaterials()
    {
        if (s_Body != null) return;
        s_Body = MakeMat(new Color(0.16f, 0.17f, 0.19f));
        s_Metal = MakeMat(new Color(0.42f, 0.44f, 0.47f));
        s_Accent = MakeMat(new Color(1.0f, 0.45f, 0.12f));
    }

    static Material MakeMat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
        return m;
    }
}
