using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Retro full-screen pass (Bayer 4x4 ordered dithering to 16-bit R5G6B5 + optional scanlines).
/// Injected into every game camera at runtime (no renderer-asset edits needed) and runs at the
/// low internal resolution before URP's Point upscale, so dither cells map 1:1 to the big pixels.
/// Also forces AntialiasingMode.None / MSAA off on every camera. F7 toggles scanlines.
/// </summary>
public static class RetroScreenEffect
{
    public static bool Enabled = true;
    public static bool ScanlinesEnabled = true;
    public static float ScanlineIntensity = 0.12f;
    public static float DitherStrength = 1.0f;

    static Material s_Material;
    static RetroPass s_Pass;
    static readonly int ColorLevelsId = Shader.PropertyToID("_ColorLevels");
    static readonly int DitherId = Shader.PropertyToID("_DitherStrength");
    static readonly int ScanlineId = Shader.PropertyToID("_ScanlineIntensity");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;

        var go = new GameObject("RetroScreenEffect_Input") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(go);
        go.AddComponent<RetroToggleInput>();
    }

    public static void ConfigureCamera(Camera cam)
    {
        if (cam == null) return;
        cam.allowMSAA = false;
        var data = cam.GetUniversalAdditionalCameraData();
        if (data != null) data.antialiasing = AntialiasingMode.None;
    }

    static void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam.cameraType != CameraType.Game) return;
        ConfigureCamera(cam);
        if (!Enabled) return;

        if (s_Material == null)
        {
            var shader = Resources.Load<Shader>("LarpSeekRetroScreen");
            if (shader == null || !shader.isSupported) { Enabled = false; return; }
            s_Material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            s_Pass = new RetroPass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
            s_Pass.requiresIntermediateTexture = true;
        }

        s_Material.SetVector(ColorLevelsId, new Vector4(32f, 64f, 32f, 0f));
        s_Material.SetFloat(DitherId, DitherStrength);
        s_Material.SetFloat(ScanlineId, ScanlinesEnabled ? ScanlineIntensity : 0f);
        s_Pass.material = s_Material;

        var data = cam.GetUniversalAdditionalCameraData();
        if (data != null && data.scriptableRenderer != null)
            data.scriptableRenderer.EnqueuePass(s_Pass);
    }

    class RetroPass : ScriptableRenderPass
    {
        public Material material;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;

            TextureHandle source = resources.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(source);
            desc.name = "_LarpSeekRetroColor";
            desc.clearBuffer = false;
            TextureHandle dest = renderGraph.CreateTexture(desc);

            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, dest, material, 0), "LarpSeek Retro Dither");
            resources.cameraColor = dest;
        }
    }

    class RetroToggleInput : MonoBehaviour
    {
        void Update()
        {
            bool pressed;
#if ENABLE_INPUT_SYSTEM
            pressed = Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame;
#else
            pressed = Input.GetKeyDown(KeyCode.F7);
#endif
            if (pressed) ScanlinesEnabled = !ScanlinesEnabled;
        }
    }
}
