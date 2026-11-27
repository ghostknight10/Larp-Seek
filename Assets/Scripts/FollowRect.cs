using UnityEngine;

/// <summary>
/// Keeps this RectTransform exactly over another sibling RectTransform (used for background "pills"
/// placed behind a text that is positioned by a layout group).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class FollowRect : MonoBehaviour
{
    public RectTransform target;
    RectTransform _rt;

    void Awake() => _rt = (RectTransform)transform;

    void LateUpdate()
    {
        if (target == null) return;
        bool show = target.gameObject.activeInHierarchy;
        if (!show) return;
        _rt.anchorMin = target.anchorMin;
        _rt.anchorMax = target.anchorMax;
        _rt.pivot = target.pivot;
        _rt.anchoredPosition = target.anchoredPosition;
        _rt.sizeDelta = target.sizeDelta;
    }
}
