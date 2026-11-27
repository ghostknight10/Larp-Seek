using UnityEngine;

/// <summary>
/// Built players always launch in borderless fullscreen at the monitor's native resolution,
/// even if an earlier run saved a windowed mode in the player prefs.
/// </summary>
public static class FullscreenEnforcer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        if (Application.isEditor) return;
        var res = Screen.currentResolution;
        Screen.SetResolution(res.width, res.height, FullScreenMode.FullScreenWindow);
    }
}
