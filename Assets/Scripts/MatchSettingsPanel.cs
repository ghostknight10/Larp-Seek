using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lobby card listing the host-configurable match settings (hide time, seeker vision, speeds,
/// match length, seeker reload). The host gets  &lt; / &gt;  buttons; other players see the values read-only.
/// Built at runtime with the pause-menu pixel sprites.
/// </summary>
public class MatchSettingsPanel : MonoBehaviour
{
    TMP_Text[] _values;
    GameObject[] _buttons;
    TMP_Text _footer;

    public static MatchSettingsPanel Create(Transform parent, Sprite card, Sprite button, TMP_FontAsset font, Color headerColor)
    {
        int rows = NetworkMatchState.SettingSpecs.Length;
        const float rowH = 52f, width = 470f;
        float height = 96f + rows * rowH + 46f;

        var go = new GameObject("MatchSettingsPanel", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        var bg = go.GetComponent<Image>();
        if (card != null) { bg.sprite = card; bg.type = Image.Type.Sliced; bg.color = Color.white; }
        else bg.color = new Color(0.06f, 0.09f, 0.15f, 0.95f);

        var panel = go.AddComponent<MatchSettingsPanel>();
        panel._values = new TMP_Text[rows];
        panel._buttons = new GameObject[rows * 2];

        // Header pill
        var header = NewImage(rt, "Header", button, new Vector2(0f, -22f), new Vector2(width - 48f, 50f), new Vector2(0.5f, 1f));
        var ht = NewText((RectTransform)header.transform, "Title", font, "MATCH SETTINGS", 20f, TextAlignmentOptions.Center, headerColor);
        Stretch((RectTransform)ht.transform);

        for (int i = 0; i < rows; i++)
        {
            var spec = NetworkMatchState.SettingSpecs[i];
            float y = -96f - i * rowH;

            var label = NewText(rt, "Label" + i, font, spec.Label, 15f, TextAlignmentOptions.MidlineLeft, new Color(0.8f, 0.9f, 1f));
            Place((RectTransform)label.transform, new Vector2(28f, y), new Vector2(200f, 40f));

            int idx = i;
            panel._buttons[i * 2] = NewButton(rt, "Minus" + i, button, font, "<", new Vector2(236f, y), () => panel.Step(idx, -1));
            var val = NewText(rt, "Value" + i, font, "-", 16f, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
            Place((RectTransform)val.transform, new Vector2(288f, y), new Vector2(112f, 40f));
            panel._values[i] = val;
            panel._buttons[i * 2 + 1] = NewButton(rt, "Plus" + i, button, font, ">", new Vector2(404f, y), () => panel.Step(idx, +1));
        }

        panel._footer = NewText(rt, "Footer", font, "", 12f, TextAlignmentOptions.Center, new Color(0.7f, 0.75f, 0.85f));
        Place((RectTransform)panel._footer.transform, new Vector2(24f, -height + 44f), new Vector2(width - 48f, 32f));
        return panel;
    }

    void Step(int i, int dir)
    {
        var ms = NetworkMatchState.Instance;
        if (ms != null) ms.StepSetting((NetworkMatchState.Setting)i, dir);
    }

    void Update()
    {
        var ms = NetworkMatchState.Instance;
        bool host = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        for (int i = 0; i < _values.Length; i++)
        {
            var s = (NetworkMatchState.Setting)i;
            _values[i].text = ms != null ? NetworkMatchState.FormatSetting(s, ms.GetSetting(s)) : "-";
        }
        for (int i = 0; i < _buttons.Length; i++)
            if (_buttons[i].activeSelf != host) _buttons[i].SetActive(host);
        _footer.text = host ? "CLICK < > TO CHANGE" : "ONLY THE HOST CAN CHANGE THESE";
    }

    // ------------------------------------------------------------------ tiny UI helpers

    static Image NewImage(RectTransform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, Vector2 anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
        else img.color = new Color(0.85f, 0.35f, 0.3f);
        img.raycastTarget = false;
        return img;
    }

    static TMP_Text NewText(RectTransform parent, string name, TMP_FontAsset font, string text, float size, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.enableAutoSizing = true;
        t.fontSizeMin = size * 0.6f;
        t.fontSizeMax = size;
        t.alignment = align;
        t.color = color;
        t.overflowMode = TextOverflowModes.Truncate;
        t.raycastTarget = false;
        return t;
    }

    static GameObject NewButton(RectTransform parent, string name, Sprite sprite, TMP_FontAsset font, string glyph, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var img = NewImage(parent, name, sprite, pos, new Vector2(44f, 40f), new Vector2(0f, 1f));
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var t = NewText((RectTransform)img.transform, "Glyph", font, glyph, 18f, TextAlignmentOptions.Center, Color.white);
        Stretch((RectTransform)t.transform);
        var hl = img.gameObject.AddComponent<MenuHoverHighlight>();
        hl.backgroundColor = new Color(1f, 1f, 1f, 0.18f);
        hl.labelHighlightColor = new Color(1f, 0.9f, 0.45f);
        return img.gameObject;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
