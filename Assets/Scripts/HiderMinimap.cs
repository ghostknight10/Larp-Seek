using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Pixelated top-down minimap for Hiders (bottom-left of the HUD).
/// A player-centred orthographic camera renders into a tiny Point-filtered RenderTexture (96x96), so the map
/// reads as chunky pixels. The camera sits just above head height with a short far plane, so ceilings are culled
/// and the map works on every floor. Beacon stations are drawn as markers (clamped to the edge when off-map),
/// plus the local player's arrow and the fridge revive point.
/// </summary>
public class HiderMinimap : MonoBehaviour
{
    const int TexSize = 96;
    const float OrthoSize = 24f;      // metres from centre to edge
    const float MapPixels = 200f;     // on-screen size (canvas units)

    RawImage _mapImage;
    RenderTexture _rt;
    Camera _cam;
    RectTransform _playerArrow;
    readonly List<(RectTransform rt, Image img, int index)> _beacons = new();
    RectTransform _fridgeMarker;
    static Sprite s_Square, s_Arrow;

    public static HiderMinimap Create(Transform hudParent, Sprite frameSprite)
    {
        var root = new GameObject("HiderMinimap", typeof(RectTransform));
        var rt = (RectTransform)root.transform;
        rt.SetParent(hudParent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(20f, 20f);
        rt.sizeDelta = new Vector2(MapPixels + 16f, MapPixels + 16f);

        var frame = root.AddComponent<Image>();
        frame.sprite = frameSprite;
        frame.type = frameSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        frame.color = frameSprite != null ? Color.white : new Color(0.06f, 0.09f, 0.15f, 0.95f);
        frame.raycastTarget = false;

        var mm = root.AddComponent<HiderMinimap>();
        mm.Build(rt);
        return mm;
    }

    void Build(RectTransform root)
    {
        _rt = new RenderTexture(TexSize, TexSize, 16, RenderTextureFormat.ARGB32)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "MinimapRT"
        };
        _rt.Create();

        var mapGo = new GameObject("Map", typeof(RectTransform), typeof(RawImage), typeof(RectMask2D));
        var mapRt = (RectTransform)mapGo.transform;
        mapRt.SetParent(root, false);
        mapRt.anchorMin = mapRt.anchorMax = mapRt.pivot = new Vector2(0.5f, 0.5f);
        mapRt.sizeDelta = new Vector2(MapPixels, MapPixels);
        _mapImage = mapGo.GetComponent<RawImage>();
        _mapImage.texture = _rt;
        _mapImage.raycastTarget = false;

        EnsureSprites();
        for (int i = 1; i <= 2; i++)
        {
            var m = Marker(mapRt, $"Beacon_{i}", s_Square, 12f, new Color(0.15f, 0.85f, 1f));
            _beacons.Add((m, m.GetComponent<Image>(), i));
        }
        _fridgeMarker = Marker(mapRt, "Fridge", s_Square, 8f, new Color(0.3f, 1f, 0.55f));
        _playerArrow = Marker(mapRt, "Player", s_Arrow, 14f, Color.white);

        var camGo = new GameObject("MinimapCamera");
        DontDestroyOnLoad(camGo);
        _cam = camGo.AddComponent<Camera>();
        _cam.orthographic = true;
        _cam.orthographicSize = OrthoSize;
        _cam.nearClipPlane = 0.05f;
        _cam.farClipPlane = 14f;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.03f, 0.04f, 0.08f, 1f);
        _cam.targetTexture = _rt;
        _cam.allowMSAA = false;
        _cam.allowHDR = false;
        _cam.depth = -50f;
        camGo.tag = "Untagged";
        var data = _cam.GetUniversalAdditionalCameraData();
        if (data != null)
        {
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
        }
        _cam.enabled = false;
    }

    static RectTransform Marker(RectTransform parent, string name, Sprite sprite, float size, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = c;
        img.raycastTarget = false;
        return rt;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        if (_cam != null) _cam.enabled = visible;
    }

    void OnDisable()
    {
        if (_cam != null) _cam.enabled = false;
    }

    void OnDestroy()
    {
        if (_cam != null) Destroy(_cam.gameObject);
        if (_rt != null) { _rt.Release(); Destroy(_rt); }
    }

    void LateUpdate()
    {
        var local = NetworkPlayerController.LocalInstance;
        if (local == null || _cam == null) return;

        Vector3 p = local.transform.position;
        // Just above head height, looking straight down; far plane reaches ~10 m below so floors render.
        _cam.transform.SetPositionAndRotation(new Vector3(p.x, p.y + 3.0f, p.z), Quaternion.Euler(90f, 0f, 0f));

        // Player arrow: north-up map, arrow rotates with view yaw.
        float yaw = local.transform.eulerAngles.y;
        var cam = Camera.main;
        if (cam != null) yaw = cam.transform.eulerAngles.y;
        _playerArrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);
        _playerArrow.anchoredPosition = Vector2.zero;

        var ms = NetworkMatchState.Instance;
        for (int i = 0; i < _beacons.Count; i++)
        {
            var (rt, img, index) = _beacons[i];
            Vector3 world = NetworkMatchState.StationWorldPos(index);
            rt.anchoredPosition = WorldToMap(world, p, out bool clamped);
            bool capturing = ms != null && (index == 1 ? ms.Station1Capturing.Value : ms.Station2Capturing.Value);
            Color baseCol = capturing ? new Color(1f, 0.82f, 0.15f) : new Color(0.15f, 0.85f, 1f);
            float blink = Mathf.Repeat(Time.unscaledTime * (capturing ? 4f : 1.5f), 1f) < 0.5f ? 1f : 0.55f;
            img.color = new Color(baseCol.r * blink, baseCol.g * blink, baseCol.b * blink, 1f);
            rt.sizeDelta = Vector2.one * (clamped ? 10f : 14f);
        }

        bool showFridge = ms != null && ms.DownedHidersCount.Value > 0;
        _fridgeMarker.gameObject.SetActive(showFridge);
        if (showFridge) _fridgeMarker.anchoredPosition = WorldToMap(NetworkMatchState.FridgeZonePos, p, out _);
    }

    static Vector2 WorldToMap(Vector3 world, Vector3 centre, out bool clamped)
    {
        float scale = MapPixels / (OrthoSize * 2f);
        Vector2 v = new Vector2(world.x - centre.x, world.z - centre.z) * scale;
        float half = MapPixels * 0.5f - 8f;
        clamped = Mathf.Abs(v.x) > half || Mathf.Abs(v.y) > half;
        if (clamped)
        {
            float m = Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y));
            v *= half / m;
        }
        // Snap to the RenderTexture pixel grid so markers move in chunky steps like the map.
        float px = MapPixels / TexSize;
        return new Vector2(Mathf.Round(v.x / px) * px, Mathf.Round(v.y / px) * px);
    }

    static void EnsureSprites()
    {
        if (s_Square != null) return;
        s_Square = MakeSprite(8, (x, y) => (x == 0 || y == 0 || x == 7 || y == 7) ? 0.6f : 1f);
        // 9x9 pixel arrow pointing up
        string[] arrow =
        {
            "....#....",
            "...###...",
            "..#####..",
            ".#######.",
            "#########",
            "...###...",
            "...###...",
            "...###...",
            ".........",
        };
        s_Arrow = MakeSprite(9, (x, y) => arrow[8 - y][x] == '#' ? 1f : -1f);
    }

    static Sprite MakeSprite(int size, System.Func<int, int, float> shade)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float s = shade(x, y);
            px[y * size + x] = s < 0f ? new Color32(0, 0, 0, 0) : new Color(s, s, s, 1f);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
