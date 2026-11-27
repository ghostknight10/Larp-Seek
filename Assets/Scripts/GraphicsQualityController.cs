using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Fixed retro look: the game always renders at 240 vertical lines, whatever the window size, then
/// upscales with nearest-neighbour (Point) filtering. Anti-aliasing is off. Players can't change it.
/// The render scale is recomputed whenever the window height changes so it always stays 240p.
/// </summary>
public static class GraphicsQualityController
{
    public const int TargetLines = 240;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyOnStartup()
    {
        Apply();
        var go = new GameObject("GraphicsQuality240p");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<Watcher>();
    }

    public static float RenderScaleForScreen(int screenHeight) =>
        Mathf.Clamp(TargetLines / (float)Mathf.Max(1, screenHeight), 0.1f, 1f);

    public static void Apply()
    {
        QualitySettings.antiAliasing = 0;
        QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Low;

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            urp.renderScale = RenderScaleForScreen(Screen.height);
            urp.upscalingFilter = UpscalingFilterSelection.Point;
            urp.msaaSampleCount = (int)MsaaQuality.Disabled;
            urp.shadowDistance = 35f;
        }
    }

    public const string Label = "GRAPHICS: RETRO 240p";

    /// <summary>Re-applies the 240p scale when the window is resized or goes fullscreen.</summary>
    class Watcher : MonoBehaviour
    {
        int _lastHeight;
        void Update()
        {
            if (Screen.height == _lastHeight) return;
            _lastHeight = Screen.height;
            Apply();
        }
    }
}
