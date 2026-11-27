using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shows a highlight background + accent bar behind a menu option while it is hovered by the mouse
/// or selected with keyboard/gamepad navigation. Added at runtime to the main menu buttons.
/// </summary>
public class MenuHoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public Color backgroundColor = new Color(0.95f, 0.70f, 0.12f, 0.22f);
    public Color accentColor = new Color(0.95f, 0.70f, 0.12f, 1f);
    public Color labelHighlightColor = new Color(1f, 0.85f, 0.35f, 1f);

    GameObject _bg;
    TMP_Text _label;
    Color _labelBaseColor;
    bool _hovered, _selected;

    void Awake()
    {
        _label = GetComponentInChildren<TMP_Text>(true);
        if (_label != null) _labelBaseColor = _label.color;

        _bg = new GameObject("HoverHighlight", typeof(RectTransform));
        var rt = (RectTransform)_bg.transform;
        rt.SetParent(transform, false);
        rt.SetAsFirstSibling();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var fill = _bg.AddComponent<Image>();
        fill.color = backgroundColor;
        fill.raycastTarget = false;

        var bar = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
        var brt = (RectTransform)bar.transform;
        brt.SetParent(rt, false);
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(0f, 1f);
        brt.pivot = new Vector2(0f, 0.5f);
        brt.sizeDelta = new Vector2(8f, 0f);
        brt.anchoredPosition = Vector2.zero;
        var barImg = bar.GetComponent<Image>();
        barImg.color = accentColor;
        barImg.raycastTarget = false;

        _bg.SetActive(false);
    }

    void OnDisable()
    {
        _hovered = _selected = false;
        Apply();
    }

    public void OnPointerEnter(PointerEventData e) { _hovered = true; Apply(); }
    public void OnPointerExit(PointerEventData e) { _hovered = false; Apply(); }
    public void OnSelect(BaseEventData e) { _selected = true; Apply(); }
    public void OnDeselect(BaseEventData e) { _selected = false; Apply(); }

    void Apply()
    {
        var sel = GetComponent<Selectable>();
        bool on = (_hovered || _selected) && (sel == null || sel.interactable);
        if (_bg != null && _bg.activeSelf != on) _bg.SetActive(on);
        if (_label != null) _label.color = on ? labelHighlightColor : _labelBaseColor;
    }
}
