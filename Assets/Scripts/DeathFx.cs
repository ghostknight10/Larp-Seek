using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Hider death effect, played on every peer when a Hider goes down:
///  1. Scream (3D, audible to nearby players).
///  2. The body flashes white / red for 3 seconds.
///  3. The body "pixel-explodes": it is replaced by a burst of small coloured voxels that fly
///     outward, bounce on the floor and shrink away, and the avatar stays hidden while downed.
/// </summary>
public static class DeathFx
{
    public const float FlashSeconds = 3f;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static Material s_VoxelMat;

    public static void Play(NetworkPlayerController player, System.Action onExploded)
    {
        if (player == null) return;
        ProceduralAudio.PlayScream(player.transform.position + Vector3.up * 1.5f);
        player.StartCoroutine(Run(player, onExploded));
    }

    static IEnumerator Run(NetworkPlayerController player, System.Action onExploded)
    {
        // Wait a frame so the morph -> human revert (replicated in the same tick) has been applied.
        yield return null;
        if (player == null) yield break;

        List<Renderer> body = CollectBodyRenderers(player);
        var block = new MaterialPropertyBlock();
        float t = 0f;
        while (t < FlashSeconds && player != null)
        {
            // Strobe speeds up as the explosion approaches.
            float rate = Mathf.Lerp(6f, 22f, t / FlashSeconds);
            bool white = Mathf.Repeat(t * rate, 1f) < 0.5f;
            Color c = white ? new Color(1f, 1f, 1f) : new Color(1f, 0.12f, 0.08f);
            block.SetColor(BaseColorId, c);
            block.SetColor(EmissionId, c * 2.5f);
            for (int i = 0; i < body.Count; i++)
                if (body[i] != null) body[i].SetPropertyBlock(block);
            t += Time.deltaTime;
            yield return null;
        }
        if (player == null) yield break;

        // Collect colours + bounds before hiding the body.
        var colors = new List<Color>();
        Bounds bounds = new Bounds(player.transform.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.4f));
        bool any = false;
        for (int i = 0; i < body.Count; i++)
        {
            var r = body[i];
            if (r == null) continue;
            r.SetPropertyBlock(null);
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            var m = r.sharedMaterial;
            if (m != null) colors.Add(m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : m.color);
        }
        if (colors.Count == 0) colors.Add(new Color(0.14f, 0.34f, 0.62f));

        onExploded?.Invoke();
        SpawnVoxelBurst(bounds, colors);
        ProceduralAudio.PlayDecoyPuff(bounds.center, 1f);
    }

    static List<Renderer> CollectBodyRenderers(NetworkPlayerController player)
    {
        var list = new List<Renderer>();
        var cam = player.GetComponentInChildren<Camera>(true);
        foreach (var r in player.GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer || r.GetComponent<TextMesh>() != null) continue;
            if (cam != null && r.transform.IsChildOf(cam.transform)) continue; // first-person viewmodel
            list.Add(r);
        }
        return list;
    }

    static void SpawnVoxelBurst(Bounds b, List<Color> palette)
    {
        if (s_VoxelMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            s_VoxelMat = new Material(sh) { enableInstancing = true };
        }

        var root = new GameObject("DeathVoxelBurst");
        root.transform.position = b.center;
        root.AddComponent<VoxelBurst>().Init(b, palette, s_VoxelMat);
    }

    /// <summary>Simple self-simulating voxel debris (no physics engine cost, no colliders).</summary>
    class VoxelBurst : MonoBehaviour
    {
        struct Voxel { public Transform t; public Vector3 v; public float size; public float spin; }
        readonly List<Voxel> _voxels = new();
        float _age;
        float _floorY;
        const float Life = 1.8f;

        public void Init(Bounds b, List<Color> palette, Material mat)
        {
            _floorY = b.min.y;
            var block = new MaterialPropertyBlock();
            int count = 70;
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                Vector3 local = new Vector3(Random.Range(-b.extents.x, b.extents.x), Random.Range(-b.extents.y, b.extents.y), Random.Range(-b.extents.z, b.extents.z));
                go.transform.position = b.center + local;
                float size = Random.Range(0.08f, 0.17f);
                go.transform.localScale = Vector3.one * size;

                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                Color c = palette[Random.Range(0, palette.Count)];
                if (Random.value < 0.25f) c = Color.Lerp(c, Color.white, 0.6f);
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block);

                Vector3 outward = (local.sqrMagnitude > 0.0001f ? local.normalized : Random.onUnitSphere);
                Vector3 vel = outward * Random.Range(2.5f, 6.5f) + Vector3.up * Random.Range(2f, 5f);
                _voxels.Add(new Voxel { t = go.transform, v = vel, size = size, spin = Random.Range(-720f, 720f) });
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            float shrink = Mathf.Clamp01(1f - Mathf.Max(0f, _age - Life * 0.45f) / (Life * 0.55f));
            for (int i = 0; i < _voxels.Count; i++)
            {
                var v = _voxels[i];
                if (v.t == null) continue;
                v.v += Vector3.down * 18f * dt;
                Vector3 p = v.t.position + v.v * dt;
                if (p.y < _floorY + v.size * 0.5f)
                {
                    p.y = _floorY + v.size * 0.5f;
                    v.v = new Vector3(v.v.x * 0.55f, -v.v.y * 0.35f, v.v.z * 0.55f);
                }
                v.t.position = p;
                // Snap rotation to 45 degree steps so the debris reads as chunky pixels, not smooth cubes.
                float ang = Mathf.Round(_age * v.spin / 45f) * 45f;
                v.t.rotation = Quaternion.Euler(ang, ang * 0.5f, 0f);
                v.t.localScale = Vector3.one * v.size * shrink;
                _voxels[i] = v;
            }
            if (_age >= Life) Destroy(gameObject);
        }
    }
}
