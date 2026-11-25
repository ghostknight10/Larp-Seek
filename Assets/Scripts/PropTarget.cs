using UnityEngine;

/// <summary>
/// Marks a GameObject as a morphable Prop for Prop Hunt.
/// </summary>
public class PropTarget : MonoBehaviour
{
    public string propDisplayName;

    public string DisplayName => string.IsNullOrEmpty(propDisplayName) ? gameObject.name : propDisplayName;
}
