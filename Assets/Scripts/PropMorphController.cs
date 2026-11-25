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

    FirstPersonRoam _roam;
    CharacterController _cc;
    Transform _visualRoot;
    PropTarget _hoveredProp;
    Texture2D _circleTex;
    bool _isMorphed;

    void Awake()
    {
        _roam = GetComponent<FirstPersonRoam>();
        _cc = GetComponent<CharacterController>();

        var vr = new GameObject("PlayerVisualRoot");
        vr.transform.SetParent(transform, false);
        _visualRoot = vr.transform;

        _circleTex = MakeCircleTexture(32);
    }

    void Update()
    {
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
            MorphInto(_hoveredProp);
        }
        else if (revertKey && _isMorphed)
        {
            RevertToFirstPerson();
        }
    }

    void UpdateHoverRaycast()
    {
        _hoveredProp = null;
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange + (_isMorphed ? _roam.thirdPersonDistance : 0f), propLayerMask, QueryTriggerInteraction.Ignore))
        {
            // Ignore self
            if (hit.transform.IsChildOf(transform)) return;
            _hoveredProp = hit.collider.GetComponentInParent<PropTarget>();
        }
    }

    void MorphInto(PropTarget target)
    {
        ClearVisual();

        // Clone visual hierarchy of the target prop
        GameObject clone = Instantiate(target.gameObject, _visualRoot);
        clone.name = "MorphedProp_" + target.DisplayName;

        // Strip colliders, rigidbodies, and PropTarget from clone so player doesn't self-collide
        foreach (var col in clone.GetComponentsInChildren<Collider>(true))
            Destroy(col);
        foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);
        foreach (var pt in clone.GetComponentsInChildren<PropTarget>(true))
            Destroy(pt);

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
            float h = Mathf.Clamp(b.size.y, 0.45f, 2.2f);
            float r = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.42f, 0.22f, 0.55f);
            _cc.height = Mathf.Max(h, r * 2f);
            _cc.radius = r;
            _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);

            // Scale third-person camera distance slightly for larger props
            _roam.thirdPersonDistance = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 1.4f + 2.2f, 2.8f, 5.5f);
            _roam.thirdPersonHeight = Mathf.Clamp(b.size.y * 0.65f + 0.6f, 0.9f, 2.0f);
        }

        _isMorphed = true;
        _roam.isThirdPerson = true;
    }

    public void RevertToFirstPerson()
    {
        ClearVisual();
        _cc.height = 1.8f;
        _cc.radius = 0.35f;
        _cc.center = new Vector3(0f, 0.9f, 0f);
        _isMorphed = false;
        _roam.isThirdPerson = false;
    }

    void ClearVisual()
    {
        for (int i = _visualRoot.childCount - 1; i >= 0; i--)
            Destroy(_visualRoot.GetChild(i).gameObject);
    }

    void OnGUI()
    {
        if (Cursor.lockState != CursorLockMode.Locked || _circleTex == null) return;

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
