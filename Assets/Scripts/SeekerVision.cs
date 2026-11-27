using UnityEngine;

/// <summary>
/// Limits how far the local Seeker can see: thick dark fog that is fully opaque at the configured
/// distance, plus a matching camera far-clip so nothing past it is drawn at all. Hiders (and the
/// Seeker once downed / in the menu) get the scene's normal fog and view distance back.
/// Only ever touches the local player's camera and the local RenderSettings.
/// </summary>
public static class SeekerVision
{
    static readonly Color FogColor = new Color(0.035f, 0.035f, 0.045f);

    static bool s_Active;
    static bool s_SavedFog;
    static FogMode s_SavedMode;
    static float s_SavedStart, s_SavedEnd, s_SavedDensity;
    static Color s_SavedColor;
    static float s_SavedFar = -1f;
    static CameraClearFlags s_SavedClear;
    static Color s_SavedBg;
    static Camera s_Cam;
    static float s_Distance = -1f;

    public static void Apply(Camera cam, bool limited, float distance)
    {
        if (limited && cam != null)
        {
            if (!s_Active || s_Cam != cam)
            {
                Restore();
                s_SavedFog = RenderSettings.fog;
                s_SavedMode = RenderSettings.fogMode;
                s_SavedStart = RenderSettings.fogStartDistance;
                s_SavedEnd = RenderSettings.fogEndDistance;
                s_SavedDensity = RenderSettings.fogDensity;
                s_SavedColor = RenderSettings.fogColor;
                s_SavedFar = cam.farClipPlane;
                s_SavedClear = cam.clearFlags;
                s_SavedBg = cam.backgroundColor;
                s_Cam = cam;
                s_Active = true;
                s_Distance = -1f;
            }
            if (!Mathf.Approximately(s_Distance, distance))
            {
                s_Distance = distance;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = FogColor;
                RenderSettings.fogStartDistance = distance * 0.35f;
                RenderSettings.fogEndDistance = distance;
                cam.farClipPlane = distance + 1.5f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = FogColor;
            }
        }
        else if (s_Active)
        {
            Restore();
        }
    }

    static void Restore()
    {
        if (!s_Active) return;
        RenderSettings.fog = s_SavedFog;
        RenderSettings.fogMode = s_SavedMode;
        RenderSettings.fogStartDistance = s_SavedStart;
        RenderSettings.fogEndDistance = s_SavedEnd;
        RenderSettings.fogDensity = s_SavedDensity;
        RenderSettings.fogColor = s_SavedColor;
        if (s_Cam != null)
        {
            s_Cam.farClipPlane = s_SavedFar;
            s_Cam.clearFlags = s_SavedClear;
            s_Cam.backgroundColor = s_SavedBg;
        }
        s_Active = false;
        s_Cam = null;
        s_Distance = -1f;
    }
}
