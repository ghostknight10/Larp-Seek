using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Raycasts from the camera center, draws a small white circle reticle,
/// and morphs the player into the targeted PropTarget on Left Click (switching to Third-Person).
/// </summary>
[RequireComponent(typeof(FirstPersonRoam))]
[RequireComponent(typeof(CharacterController))]
public class PropMorphController : MonoBehaviour
{
    [Header("Targeting")]
    public float interactRange = 7.0f;
    public LayerMask propLayerMask = ~0;

    [Header("Reticle")]
    public float normalCircleSize = 8f;
    public float hoverCircleSize = 14f;

    [Header("Control")]
    public bool inputEnabled = true;

    public System.Action<PropTarget> OnLocalMorphRequested;
    public System.Action OnLocalRevertRequested;
    public System.Action<bool, string> OnMorphStateUpdated;

    public bool IsMorphed => _isMorphed;
    public string CurrentMorphName => _currentMorphName;

    FirstPersonRoam _roam;
    CharacterController _cc;
    Transform _visualRoot;
    GameObject _humanAvatar;
    PropTarget _hoveredProp;
    Texture2D _circleTex;
    bool _isMorphed;
    string _currentMorphName = "Human";
    bool _showHumanWhenUnmorphed = false;

    void Awake()
    {
        EnsureVisualRoot();
        _circleTex = MakeCircleTexture(32);
    }

    public void EnsureVisualRoot()
    {
        if (_roam == null) _roam = GetComponent<FirstPersonRoam>();
        if (_cc == null) _cc = GetComponent<CharacterController>();

        if (_visualRoot == null)
        {
            var existing = transform.Find("PlayerVisualRoot");
            if (existing != null)
            {
                _visualRoot = existing;
            }
            else
            {
                var vr = new GameObject("PlayerVisualRoot");
                vr.transform.SetParent(transform, false);
                _visualRoot = vr.transform;
            }
        }

        if (_humanAvatar == null && _visualRoot != null)
        {
            var av = _visualRoot.Find("HumanAvatar");
            if (av != null) _humanAvatar = av.gameObject;
            else BuildHumanAvatar();
        }
    }

    public static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Object.Destroy(obj);
        else Object.DestroyImmediate(obj);
    }

    public void SetHumanAvatarVisible(bool visible)
    {
        _showHumanWhenUnmorphed = visible;
        if (_humanAvatar != null)
            _humanAvatar.SetActive(visible && !_isMorphed);
    }

    void Update()
    {
        if (!inputEnabled)
        {
            _hoveredProp = null;
            return;
        }

        UpdateHoverRaycast();

        bool leftClick = false;
        bool revertKey = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            leftClick = Mouse.current.leftButton.wasPressedThisFrame;
        if (Keyboard.current != null)
            revertKey = Keyboard.current.rKey.wasPressedThisFrame;
#else
        leftClick = Input.GetMouseButtonDown(0);
        revertKey = Input.GetKeyDown(KeyCode.R);
#endif

        if (leftClick && _hoveredProp != null && Cursor.lockState == CursorLockMode.Locked)
        {
            if (OnLocalMorphRequested != null)
                OnLocalMorphRequested.Invoke(_hoveredProp);
            else
                MorphInto(_hoveredProp);
        }
        else if (revertKey && _isMorphed && Cursor.lockState == CursorLockMode.Locked)
        {
            if (OnLocalRevertRequested != null)
                OnLocalRevertRequested.Invoke();
            else
                RevertToFirstPerson();
        }
    }

    Camera GetPlayerCamera()
    {
        if (_roam != null && _roam.cameraTransform != null)
        {
            var cam = _roam.cameraTransform.GetComponent<Camera>();
            if (cam != null && cam.enabled) return cam;
        }
        return Camera.main;
    }

    void UpdateHoverRaycast()
    {
        _hoveredProp = null;
        Camera cam = GetPlayerCamera();
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange + (_isMorphed ? _roam.thirdPersonDistance : 0f), propLayerMask, QueryTriggerInteraction.Ignore))
        {
            // Ignore self
            if (hit.transform.IsChildOf(transform)) return;
            _hoveredProp = hit.collider.GetComponentInParent<PropTarget>();
        }
    }

    public void MorphInto(PropTarget target)
    {
        if (target == null) return;
        EnsureVisualRoot();
        ClearVisual();
        if (_humanAvatar != null) _humanAvatar.SetActive(false);

        // Clone visual hierarchy of the target prop
        GameObject clone = Instantiate(target.gameObject, _visualRoot);
        clone.name = "MorphedProp_" + target.DisplayName;

        // Strip colliders, rigidbodies, and PropTarget from clone so player doesn't self-collide
        foreach (var col in clone.GetComponentsInChildren<Collider>(true))
            SafeDestroy(col);
        foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true))
            SafeDestroy(rb);
        foreach (var pt in clone.GetComponentsInChildren<PropTarget>(true))
            SafeDestroy(pt);

        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;

        // Measure combined world bounds of renderers to align bottom to y = 0 and size CharacterController
        var renderers = clone.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            // Shift clone so its bottom rests cleanly on y = 0 (player's feet) and centered on X/Z
            Vector3 shift = transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            clone.transform.position += shift;

            // Adjust CharacterController smoothly
            if (_cc != null)
            {
                float h = Mathf.Clamp(b.size.y, 0.45f, 2.2f);
                float r = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.42f, 0.22f, 0.55f);
                _cc.height = Mathf.Max(h, r * 2f);
                _cc.radius = r;
                _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
            }

            // Scale third-person camera distance slightly for larger props
            if (_roam != null)
            {
                _roam.thirdPersonDistance = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 1.4f + 2.2f, 2.8f, 5.5f);
                _roam.thirdPersonHeight = Mathf.Clamp(b.size.y * 0.65f + 0.6f, 0.9f, 2.0f);
            }
        }

        _isMorphed = true;
        _currentMorphName = target.DisplayName;
        if (_roam != null) _roam.isThirdPerson = true;
        OnMorphStateUpdated?.Invoke(_isMorphed, _currentMorphName);
    }

    public void RevertToFirstPerson()
    {
        EnsureVisualRoot();
        ClearVisual();
        if (_humanAvatar != null)
            _humanAvatar.SetActive(_showHumanWhenUnmorphed);

        if (_cc != null)
        {
            _cc.height = 1.8f;
            _cc.radius = 0.35f;
            _cc.center = new Vector3(0f, 0.9f, 0f);
        }
        _isMorphed = false;
        _currentMorphName = "Human";
        if (_roam != null) _roam.isThirdPerson = false;
        OnMorphStateUpdated?.Invoke(_isMorphed, _currentMorphName);
    }

    void ClearVisual()
    {
        EnsureVisualRoot();
        for (int i = _visualRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = _visualRoot.GetChild(i).gameObject;
            if (child == _humanAvatar) continue;
            SafeDestroy(child);
        }
    }

    /// <summary>
    /// Creates an exact standalone visual duplicate of what the player is currently morphed into
    /// (e.g. blue chair, table, sofa, bean bag, or human avatar), complete with collider for Hunter targeting.
    /// </summary>
    public GameObject CreateCurrentVisualCopy()
    {
        EnsureVisualRoot();
        if (_isMorphed && _visualRoot != null)
        {
            for (int i = 0; i < _visualRoot.childCount; i++)
            {
                Transform child = _visualRoot.GetChild(i);
                if (child.gameObject == _humanAvatar) continue;

                GameObject copy = Instantiate(child.gameObject);
                copy.name = "Decoy_" + _currentMorphName;
                copy.SetActive(true);

                // Position copy to match player's current pose
                copy.transform.position = transform.position + transform.TransformVector(child.localPosition);
                copy.transform.rotation = transform.rotation * child.localRotation;
                copy.transform.localScale = Vector3.Scale(transform.lossyScale, child.localScale);

                foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
                    r.enabled = true;
                foreach (var pt in copy.GetComponentsInChildren<PropTarget>(true))
                    SafeDestroy(pt);
                foreach (var c in copy.GetComponentsInChildren<Collider>(true))
                    SafeDestroy(c);

                EnsureDecoyCollider(copy);
                return copy;
            }
        }

        if (_humanAvatar != null)
        {
            GameObject copy = Instantiate(_humanAvatar);
            copy.name = "Decoy_HumanAvatar";
            copy.SetActive(true);
            copy.transform.position = transform.position;
            copy.transform.rotation = transform.rotation;
            copy.transform.localScale = transform.lossyScale;

            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
                r.enabled = true;
            foreach (var c in copy.GetComponentsInChildren<Collider>(true))
                SafeDestroy(c);

            EnsureDecoyCollider(copy);
            return copy;
        }

        return null;
    }

    static void EnsureDecoyCollider(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            var bc = go.AddComponent<BoxCollider>();
            bc.center = go.transform.InverseTransformPoint(b.center);
            Vector3 lossy = go.transform.lossyScale;
            bc.size = new Vector3(
                lossy.x != 0f ? Mathf.Abs(b.size.x / lossy.x) : b.size.x,
                lossy.y != 0f ? Mathf.Abs(b.size.y / lossy.y) : b.size.y,
                lossy.z != 0f ? Mathf.Abs(b.size.z / lossy.z) : b.size.z
            );
        }
        else
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.7f, 1.4f, 0.7f);
            bc.center = new Vector3(0f, 0.7f, 0f);
        }
    }

    void BuildHumanAvatar()
    {
        _humanAvatar = new GameObject("HumanAvatar");
        _humanAvatar.transform.SetParent(_visualRoot, false);

        var shirtMat = CreateMatteMaterial(new Color(0.14f, 0.34f, 0.62f));
        var pantsMat = CreateMatteMaterial(new Color(0.13f, 0.16f, 0.22f));
        var skinMat = CreateMatteMaterial(new Color(0.84f, 0.66f, 0.52f));

        CreateAvatarPart("Torso", PrimitiveType.Cube, new Vector3(0f, 1.05f, 0f), new Vector3(0.46f, 0.62f, 0.26f), shirtMat);
        CreateAvatarPart("Head", PrimitiveType.Cube, new Vector3(0f, 1.56f, 0f), new Vector3(0.32f, 0.32f, 0.32f), skinMat);
        CreateAvatarPart("LegL", PrimitiveType.Cube, new Vector3(-0.12f, 0.38f, 0f), new Vector3(0.18f, 0.72f, 0.20f), pantsMat);
        CreateAvatarPart("LegR", PrimitiveType.Cube, new Vector3(0.12f, 0.38f, 0f), new Vector3(0.18f, 0.72f, 0.20f), pantsMat);
        CreateAvatarPart("ArmL", PrimitiveType.Cube, new Vector3(-0.33f, 1.05f, 0f), new Vector3(0.16f, 0.56f, 0.18f), shirtMat);
        CreateAvatarPart("ArmR", PrimitiveType.Cube, new Vector3(0.33f, 1.05f, 0f), new Vector3(0.16f, 0.56f, 0.18f), shirtMat);

        _humanAvatar.SetActive(false);
    }

    void CreateAvatarPart(string partName, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = partName;
        go.transform.SetParent(_humanAvatar.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var rend = go.GetComponent<Renderer>();
        if (rend != null && mat != null) rend.sharedMaterial = mat;
    }

    static Material CreateMatteMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
        return mat;
    }

    void OnGUI()
    {
        if (!inputEnabled || Cursor.lockState != CursorLockMode.Locked || _circleTex == null) return;

        bool hasTarget = _hoveredProp != null;
        float size = hasTarget ? hoverCircleSize : normalCircleSize;
        Rect rect = new Rect(
            (Screen.width - size) * 0.5f,
            (Screen.height - size) * 0.5f,
            size,
            size
        );

        Color prev = GUI.color;
        GUI.color = hasTarget ? Color.white : new Color(1f, 1f, 1f, 0.85f);
        GUI.DrawTexture(rect, _circleTex);

        if (hasTarget)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width * 0.5f - 140f, Screen.height * 0.5f + 16f, 280f, 26f),
                $"Click to morph: {_hoveredProp.DisplayName}", style);
        }
        GUI.color = prev;
    }

    static Texture2D MakeCircleTexture(int res)
    {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float c = (res - 1) * 0.5f;
        float rOuter = res * 0.45f;
        float rInner = res * 0.25f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float a = (d <= rOuter && d >= rInner) ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }
}
