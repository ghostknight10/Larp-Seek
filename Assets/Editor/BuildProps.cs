using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Builds realistic campus props in retro pixel-art style:
///  1) Multi-stemmed Lady Palm (Rhapis) in ribbed black pot with cream saucer
///  2) Blue Sintex 60L hooded campus dustbin with arched open hood, Sintex & Recycle decals
/// </summary>
public static class BuildProps
{
    static Mesh s_MeshPot;
    static Mesh s_MeshSaucer;
    static Mesh s_MeshSoil;
    static Mesh s_MeshStems;
    static Mesh s_MeshLeavesDark;
    static Mesh s_MeshLeavesLight;

    static Mesh s_MeshBinBody;
    static Mesh s_MeshBinHood;

    static Mesh s_MeshChairShell;
    static Mesh s_MeshChairLegs;

    static Material s_MatPot;
    static Material s_MatSaucer;
    static Material s_MatSoil;
    static Material s_MatStem;
    static Material s_MatLeafDark;
    static Material s_MatLeafLight;

    static Material s_MatBinBlue;
    static Material s_MatBinDarkInside;
    static Material s_MatBinDecal;

    static Material s_MatChairBlue;
    static Material s_MatChairOrange;
    static Material s_MatChairSteelLegs;
    static Material s_MatTableMarbleSmall;
    static Material s_MatTableMarbleLong;
    static Material s_MatTableLegs;

    public static void ResetCache()
    {
        s_MeshPot = s_MeshSaucer = s_MeshSoil = s_MeshStems = s_MeshLeavesDark = s_MeshLeavesLight = null;
        s_MeshBinBody = s_MeshBinHood = null;
        s_MeshChairShell = s_MeshChairLegs = null;
    }

    public static void CreatePalmPlant(GameObject parent, Vector3 pos, float scale = 1.0f)
    {
        EnsurePlantAssets();

        var root = new GameObject("PalmPlant");
        root.transform.SetParent(parent.transform, false);
        root.transform.position = pos;
        float yaw = Mathf.Abs(pos.x * 47.3f + pos.z * 91.7f) % 360f;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        root.transform.localScale = Vector3.one * scale;

        AddMeshPart(root, "Saucer",      s_MeshSaucer,      s_MatSaucer,    false);
        AddMeshPart(root, "Pot",         s_MeshPot,         s_MatPot,       true);
        AddMeshPart(root, "Soil",        s_MeshSoil,        s_MatSoil,      false);
        AddMeshPart(root, "Stems",       s_MeshStems,       s_MatStem,      false);
        AddMeshPart(root, "Leaves_Dark", s_MeshLeavesDark,  s_MatLeafDark,  false);
        AddMeshPart(root, "Leaves_Lite", s_MeshLeavesLight, s_MatLeafLight, false);
    }

    public static void CreateSintexDustbin(GameObject parent, Vector3 pos, float rotY)
    {
        EnsureDustbinAssets();

        var bin = new GameObject("Dustbin_Sintex");
        bin.transform.SetParent(parent.transform, false);
        bin.transform.position = pos;
        bin.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

        // Tapered blue body (base 0.34 -> top 0.43, height 0.78)
        AddMeshPart(bin, "Bin_Body", s_MeshBinBody, s_MatBinBlue, true);

        // Collar rim at top of body
        var collar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        collar.name = "Bin_Collar";
        collar.transform.SetParent(bin.transform, false);
        collar.transform.localPosition = new Vector3(0f, 0.79f, 0f);
        collar.transform.localScale = new Vector3(0.49f, 0.055f, 0.49f);
        collar.GetComponent<Renderer>().sharedMaterial = s_MatBinBlue;

        // Domed arched hood shell with open throw-in tunnel
        AddMeshPart(bin, "Bin_Hood", s_MeshBinHood, s_MatBinBlue, false);

        // Dark cavity inside opening so bin looks hollow inside
        var cavity = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cavity.name = "Bin_Inside";
        cavity.transform.SetParent(bin.transform, false);
        cavity.transform.localPosition = new Vector3(0f, 0.81f, 0f);
        cavity.transform.localScale = new Vector3(0.37f, 0.04f, 0.37f);
        cavity.GetComponent<Renderer>().sharedMaterial = s_MatBinDarkInside;

        // Front & Back Sintex + Recycle decal panel
        AddDecalQuad(bin, "Decal_Front", new Vector3(0f, 0.43f,  0.205f), Quaternion.Euler(-3.3f, 180f, 0f), new Vector3(0.34f, 0.56f, 1f), s_MatBinDecal);
        AddDecalQuad(bin, "Decal_Back",  new Vector3(0f, 0.43f, -0.205f), Quaternion.Euler(-3.3f,   0f, 0f), new Vector3(0.34f, 0.56f, 1f), s_MatBinDecal);
    }

    // ════════════════════════════════════════════════════════════════════
    // WOVEN-BACK MOLDED CHAIR & BLACK MARBLE MESS TABLES
    // ════════════════════════════════════════════════════════════════════

    public static void CreateDiningChair(GameObject parent, string name, Vector3 localPos, float rotY, bool isLeftWing)
    {
        EnsureFurnitureAssets();
        var ch = new GameObject(name);
        ch.transform.SetParent(parent.transform, false);
        ch.transform.localPosition = localPos;
        ch.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);

        Material mShell = isLeftWing ? s_MatChairBlue : s_MatChairOrange;

        AddMeshPart(ch, "Chair_Shell", s_MeshChairShell, mShell,               true);
        AddMeshPart(ch, "Chair_Legs",  s_MeshChairLegs,  s_MatChairSteelLegs,  false);
    }

    public static void CreateMessTable(GameObject parent, string name, Vector3 worldPos, float width, float depth, bool isLeftWing, System.Random rng = null)
    {
        EnsureFurnitureAssets();

        var tblGroup = new GameObject(name);
        tblGroup.transform.SetParent(parent.transform, false);
        float tblYaw = rng != null ? (float)(rng.NextDouble() * 6.0 - 3.0) : 0f;
        tblGroup.transform.position = worldPos;
        tblGroup.transform.rotation = Quaternion.Euler(0f, tblYaw, 0f);

        bool isLong = width > 2.0f;
        Material mTop = isLong ? s_MatTableMarbleLong : s_MatTableMarbleSmall;

        // Black marble tabletop
        var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.name = "TableTop_Marble";
        top.transform.SetParent(tblGroup.transform, false);
        top.transform.localPosition = new Vector3(0f, 0.745f, 0f);
        top.transform.localScale = new Vector3(width, 0.055f, depth);
        top.GetComponent<Renderer>().sharedMaterial = mTop;

        // Under-table metal frame apron
        var apron = GameObject.CreatePrimitive(PrimitiveType.Cube);
        apron.name = "Frame_Apron";
        apron.transform.SetParent(tblGroup.transform, false);
        apron.transform.localPosition = new Vector3(0f, 0.69f, 0f);
        apron.transform.localScale = new Vector3(width - 0.14f, 0.055f, depth - 0.14f);
        apron.GetComponent<Renderer>().sharedMaterial = s_MatTableLegs;

        // Slender steel legs (4 corners + middle legs if long table)
        float lx = width * 0.5f - 0.10f;
        float lz = depth * 0.5f - 0.10f;
        AddLeg(tblGroup, "Leg_FL", new Vector3(-lx, 0.36f,  lz));
        AddLeg(tblGroup, "Leg_FR", new Vector3( lx, 0.36f,  lz));
        AddLeg(tblGroup, "Leg_BL", new Vector3(-lx, 0.36f, -lz));
        AddLeg(tblGroup, "Leg_BR", new Vector3( lx, 0.36f, -lz));
        if (isLong)
        {
            AddLeg(tblGroup, "Leg_MF", new Vector3(0f, 0.36f,  lz));
            AddLeg(tblGroup, "Leg_MB", new Vector3(0f, 0.36f, -lz));
        }

        // Spawn dining chairs as SIBLINGS under parent (never children of tblGroup)
        // so Prop Hunt morphs table and chair independently, with messy natural placement.
        float zOff = depth * 0.5f + 0.25f;
        float xOff = width * 0.5f + 0.25f;

        if (isLong)
        {
            float[] xs = { -width * 0.32f, 0f, width * 0.32f };
            for (int i = 0; i < xs.Length; i++)
            {
                TryPlaceMessyChair(parent, $"Chair_{name}_N_{i}", worldPos, tblYaw, new Vector3(xs[i], 0f,  zOff), 180f, isLeftWing, rng);
                TryPlaceMessyChair(parent, $"Chair_{name}_S_{i}", worldPos, tblYaw, new Vector3(xs[i], 0f, -zOff),   0f, isLeftWing, rng);
            }
            TryPlaceMessyChair(parent, $"Chair_{name}_E", worldPos, tblYaw, new Vector3( xOff, 0f, 0f), -90f, isLeftWing, rng);
            TryPlaceMessyChair(parent, $"Chair_{name}_W", worldPos, tblYaw, new Vector3(-xOff, 0f, 0f),  90f, isLeftWing, rng);
        }
        else
        {
            TryPlaceMessyChair(parent, $"Chair_{name}_N", worldPos, tblYaw, new Vector3(0f, 0f,  zOff), 180f, isLeftWing, rng);
            TryPlaceMessyChair(parent, $"Chair_{name}_S", worldPos, tblYaw, new Vector3(0f, 0f, -zOff),   0f, isLeftWing, rng);
            TryPlaceMessyChair(parent, $"Chair_{name}_E", worldPos, tblYaw, new Vector3( xOff, 0f, 0f), -90f, isLeftWing, rng);
            TryPlaceMessyChair(parent, $"Chair_{name}_W", worldPos, tblYaw, new Vector3(-xOff, 0f, 0f),  90f, isLeftWing, rng);
        }
    }

    static void TryPlaceMessyChair(
        GameObject parent, string chairName,
        Vector3 tableWorldPos, float tableYaw,
        Vector3 baseOffset, float baseFacingYaw,
        bool isLeftWing, System.Random rng)
    {
        if (rng != null)
        {
            // ~26% chance this chair is missing (moved elsewhere / fewer chairs at table)
            if (rng.NextDouble() < 0.26) return;

            // Push out/in along facing axis and slide slightly sideways so chairs aren't neatly aligned
            Vector3 outward = baseOffset.normalized;
            Vector3 tangent = new Vector3(-outward.z, 0f, outward.x);
            float pullOut = (float)(rng.NextDouble() * 0.30 - 0.04); // -0.04m tucked .. +0.26m pulled out
            float slide   = (float)(rng.NextDouble() * 0.28 - 0.14);
            baseOffset += outward * pullOut + tangent * slide;

            // Occasional chair turned sharply as if someone just stood up
            float yawJitter = (rng.NextDouble() < 0.20)
                ? (float)(rng.NextDouble() * 90.0 - 45.0)
                : (float)(rng.NextDouble() * 34.0 - 17.0);
            baseFacingYaw += yawJitter;
        }

        Vector3 chairPos = tableWorldPos + Quaternion.Euler(0f, tableYaw, 0f) * baseOffset;
        CreateDiningChair(parent, chairName, chairPos, tableYaw + baseFacingYaw, isLeftWing);
    }

    static void AddLeg(GameObject parent, string name, Vector3 localPos)
    {
        var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leg.name = name;
        leg.transform.SetParent(parent.transform, false);
        leg.transform.localPosition = localPos;
        leg.transform.localScale = new Vector3(0.055f, 0.72f, 0.055f);
        leg.GetComponent<Renderer>().sharedMaterial = s_MatTableLegs;
    }

    static void EnsureFurnitureAssets()
    {
        if (s_MeshChairShell != null && s_MatChairBlue != null) return;

        string marbleTex = EnsureTableMarbleTexture("Assets/Textures/table_marble_src.png", "Assets/Textures/table_marble_px.png");

        // Exact sky-blue plastic color sampled from uploaded chair photo (#6FA7D8 -> mid #5282A6..#9ACAF8)
        Color skyBlueCol = new Color(0.435f, 0.655f, 0.855f);
        Color orangeCol  = new Color(0.92f,  0.45f,  0.08f);

        s_MatChairBlue      = MakeMat("M_Chair_SkyBlue",   null, skyBlueCol, 0.36f);
        s_MatChairOrange    = MakeMat("M_Chair_Orange",    null, orangeCol,  0.36f);
        s_MatChairSteelLegs = MakeMat("M_Chair_SteelLegs", null, new Color(0.68f, 0.70f, 0.73f), 0.58f);
        s_MatChairSteelLegs.SetFloat("_Metallic", 0.75f);

        s_MatTableLegs        = MakeMat("M_Table_Legs",         null,      new Color(0.14f, 0.15f, 0.16f), 0.35f);
        s_MatTableMarbleSmall = MakeMat("M_Table_Marble_Small", marbleTex, new Color(0.95f, 0.95f, 0.95f), 0.35f);
        s_MatTableMarbleLong  = MakeMat("M_Table_Marble_Long",  marbleTex, new Color(0.95f, 0.95f, 0.95f), 0.35f);
        s_MatTableMarbleLong.mainTextureScale = new Vector2(2.2f, 1f);
        s_MatTableMarbleLong.SetTextureScale("_BaseMap", new Vector2(2.2f, 1f));

        BuildChairMeshes();
    }

    static void BuildChairMeshes()
    {
        // Flat, simple, blocky retro chair just like Minecraft
        var shellParts = new List<CombineInstance>();
        var legParts   = new List<CombineInstance>();

        float legH = 0.42f;
        float legSize = 0.045f;
        // 4 chunky square legs
        legParts.Add(BuildUVBoxInstance(new Vector3(-0.16f, legH * 0.5f,  0.16f), new Vector3(legSize, legH, legSize)));
        legParts.Add(BuildUVBoxInstance(new Vector3( 0.16f, legH * 0.5f,  0.16f), new Vector3(legSize, legH, legSize)));
        legParts.Add(BuildUVBoxInstance(new Vector3(-0.16f, legH * 0.5f, -0.16f), new Vector3(legSize, legH, legSize)));
        legParts.Add(BuildUVBoxInstance(new Vector3( 0.16f, legH * 0.5f, -0.16f), new Vector3(legSize, legH, legSize)));

        // Lower brace rails
        legParts.Add(BuildUVBoxInstance(new Vector3(0f, 0.14f,  0.16f), new Vector3(0.32f, 0.025f, 0.025f)));
        legParts.Add(BuildUVBoxInstance(new Vector3(0f, 0.14f, -0.16f), new Vector3(0.32f, 0.025f, 0.025f)));
        legParts.Add(BuildUVBoxInstance(new Vector3(-0.16f, 0.14f, 0f), new Vector3(0.025f, 0.025f, 0.32f)));
        legParts.Add(BuildUVBoxInstance(new Vector3( 0.16f, 0.14f, 0f), new Vector3(0.025f, 0.025f, 0.32f)));

        // Flat square seat slab (Minecraft style)
        shellParts.Add(BuildUVBoxInstance(new Vector3(0f, 0.45f, 0f), new Vector3(0.44f, 0.06f, 0.44f)));

        // Solid, vertical, flat backrest standing straight UP at back of seat (Minecraft style)
        // Upright support posts
        shellParts.Add(BuildUVBoxInstance(new Vector3(-0.17f, 0.72f, -0.19f), new Vector3(0.045f, 0.50f, 0.045f)));
        shellParts.Add(BuildUVBoxInstance(new Vector3( 0.17f, 0.72f, -0.19f), new Vector3(0.045f, 0.50f, 0.045f)));
        // Top cross rail
        shellParts.Add(BuildUVBoxInstance(new Vector3(0f, 0.94f, -0.19f), new Vector3(0.42f, 0.06f, 0.045f)));
        // Flat solid backrest panel
        shellParts.Add(BuildUVBoxInstance(new Vector3(0f, 0.73f, -0.19f), new Vector3(0.34f, 0.38f, 0.035f)));

        s_MeshChairShell = SaveMeshAsset(CombineMeshes("ChairShellMesh", shellParts));
        s_MeshChairLegs  = SaveMeshAsset(CombineMeshes("ChairLegsMesh",  legParts));
    }

    static CombineInstance BuildCylinderLegInstance(Vector3 pBottom, Vector3 pTop, float rBot, float rTop)
    {
        Vector3 dir = pTop - pBottom;
        Mesh m = BuildFrustumMesh("SteelLeg", 0f, dir.magnitude, rBot, rTop, 8, true);
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        return new CombineInstance { mesh = m, transform = Matrix4x4.TRS(pBottom, rot, Vector3.one) };
    }

    static Mesh BuildCurvedBackrestMesh()
    {
        // Curved plastic backrest: 8 columns across X, 7 rows up Y, reclined back ~11 deg,
        // horizontally concave (edges curve forward +Z), with rounded/arched top silhouette.
        var m = new Mesh { name = "CurvedBackrest" };
        int cols = 8;
        int rows = 7;
        float thick = 0.028f;
        float height = 0.46f;
        Vector3 pivot = new Vector3(0f, 0.43f, -0.178f);
        Quaternion tilt = Quaternion.Euler(-11f, 0f, 0f);

        var verts = new List<Vector3>();
        var tris  = new List<int>();

        for (int r = 0; r <= rows; r++)
        {
            float v = (float)r / rows; // 0 = waist, 1 = top edge
            // Width narrows gently near the rounded top dome
            float halfW = (v < 0.65f)
                ? Mathf.Lerp(0.205f, 0.198f, v / 0.65f)
                : Mathf.Lerp(0.198f, 0.135f, Mathf.Pow((v - 0.65f) / 0.35f, 1.8f));

            for (int c = 0; c <= cols; c++)
            {
                float u = (float)c / cols * 2f - 1f; // -1..+1
                float x = u * halfW;

                // Smooth rounded top arch drop near outer top corners
                float topArch = (v > 0.72f) ? (u * u) * 0.045f * ((v - 0.72f) / 0.28f) : 0f;
                float y = v * height - topArch;

                // Concave cradle curve (sides come forward +Z by 0.032m)
                float zCurve = (u * u) * 0.032f;

                Vector3 pFront = pivot + tilt * new Vector3(x, y, zCurve + thick * 0.5f);
                Vector3 pBack  = pivot + tilt * new Vector3(x, y, zCurve - thick * 0.5f);

                verts.Add(pFront); // even index
                verts.Add(pBack);  // odd index
            }
        }

        int stride = (cols + 1) * 2;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int bl = r * stride + c * 2;
                int br = bl + 2;
                int tl = (r + 1) * stride + c * 2;
                int tr = tl + 2;

                // Front (+Z)
                tris.Add(bl); tris.Add(tl); tris.Add(br);
                tris.Add(br); tris.Add(tl); tris.Add(tr);

                // Back (-Z)
                tris.Add(bl + 1); tris.Add(br + 1); tris.Add(tl + 1);
                tris.Add(br + 1); tris.Add(tr + 1); tris.Add(tl + 1);

                // Top cap
                if (r == rows - 1)
                {
                    tris.Add(tl); tris.Add(tl + 1); tris.Add(tr);
                    tris.Add(tr); tris.Add(tl + 1); tris.Add(tr + 1);
                }
                // Left edge cap
                if (c == 0)
                {
                    tris.Add(bl); tris.Add(bl + 1); tris.Add(tl);
                    tris.Add(tl); tris.Add(bl + 1); tris.Add(tl + 1);
                }
                // Right edge cap
                if (c == cols - 1)
                {
                    tris.Add(br); tris.Add(tr); tris.Add(br + 1);
                    tris.Add(tr); tris.Add(tr + 1); tris.Add(br + 1);
                }
            }
        }

        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        return m;
    }

    static CombineInstance BuildUVBoxInstance(Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh m = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
        Object.DestroyImmediate(go);
        return new CombineInstance { mesh = m, transform = Matrix4x4.TRS(pos, Quaternion.identity, scale) };
    }

    static CombineInstance BuildRotatedBox(Vector3 pivot, Quaternion tilt, Vector3 localOffset, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh m = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
        Object.DestroyImmediate(go);
        Vector3 worldPos = pivot + tilt * localOffset;
        return new CombineInstance { mesh = m, transform = Matrix4x4.TRS(worldPos, tilt, scale) };
    }

    /// <summary>
    /// Crops out the top/bottom white bars from floor_slate_src.png and generates a crisp pixelated 2x2 seamless stone tile PNG.
    /// </summary>
    public static string EnsureSlateFloorTexture(string srcPath, string dstPath)
    {
        var srcTi = AssetImporter.GetAtPath(srcPath) as TextureImporter;
        bool prevReadable = srcTi != null && srcTi.isReadable;
        if (srcTi != null && !prevReadable)
        {
            srcTi.isReadable = true;
            srcTi.SaveAndReimport();
        }

        var src = AssetDatabase.LoadAssetAtPath<Texture2D>(srcPath);
        if (src != null)
        {
            // Non-white slate band is Y=34..216 out of 250; mirror-tile into a crisp 64x64 (2x2 stone slabs) PNG
            int target = 64;
            int half = target / 2;
            var dst = new Texture2D(target, target, TextureFormat.RGBA32, false);
            int yMin = Mathf.RoundToInt(src.height * 0.145f);
            int yMax = Mathf.RoundToInt(src.height * 0.855f);

            for (int y = 0; y < target; y++)
            {
                for (int x = 0; x < target; x++)
                {
                    int tileX = x / half;
                    int tileY = y / half;
                    int lx = x % half;
                    int ly = y % half;

                    // Mirror alternating tiles horizontally/vertically for seamless tiling
                    float u = (tileX % 2 == 0) ? (lx + 0.5f) / half : 1f - (lx + 0.5f) / half;
                    float v = (tileY % 2 == 0) ? (ly + 0.5f) / half : 1f - (ly + 0.5f) / half;

                    int sx = Mathf.Clamp(Mathf.FloorToInt(u * src.width), 0, src.width - 1);
                    int sy = Mathf.Clamp(yMin + Mathf.FloorToInt(v * (yMax - yMin)), yMin, yMax - 1);

                    Color c = src.GetPixel(sx, sy);
                    // Boost cleft texture contrast slightly so pixels pop cleanly
                    float lum = c.grayscale;
                    float boosted = Mathf.Clamp01((lum - 0.50f) * 1.45f + 0.49f);
                    c = new Color(boosted * 0.96f, boosted * 0.98f, boosted, 1f);

                    // Subtle slate tile variation + crisp 1px stone slab joint
                    if ((tileX + tileY) % 2 == 1) c *= 0.93f;
                    if (lx == 0 || ly == 0) c *= 0.72f;

                    c.a = 1f;
                    dst.SetPixel(x, y, c);
                }
            }
            SavePixelTex(dst, dstPath, 64, FilterMode.Point, TextureWrapMode.Repeat);
        }

        if (srcTi != null && !prevReadable)
        {
            srcTi.isReadable = false;
            srcTi.SaveAndReimport();
        }
        return dstPath;
    }

    static string EnsureTableMarbleTexture(string srcPath, string dstPath)
    {
        var srcTi = AssetImporter.GetAtPath(srcPath) as TextureImporter;
        bool prevReadable = srcTi != null && srcTi.isReadable;
        if (srcTi != null && !prevReadable)
        {
            srcTi.isReadable = true;
            srcTi.SaveAndReimport();
        }

        var src = AssetDatabase.LoadAssetAtPath<Texture2D>(srcPath);
        if (src != null)
        {
            int w = 96, h = 64;
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int sx = Mathf.FloorToInt(((float)x + 0.5f) / w * src.width);
                    int sy = Mathf.FloorToInt(((float)y + 0.5f) / h * src.height);
                    Color c = src.GetPixel(sx, sy);
                    // Keep black marble dark with crisp white veins
                    float v = Mathf.Clamp01((c.grayscale - 0.25f) * 1.35f + 0.16f);
                    dst.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            }
            SavePixelTex(dst, dstPath, 128, FilterMode.Point, TextureWrapMode.Repeat);
        }

        if (srcTi != null && !prevReadable)
        {
            srcTi.isReadable = false;
            srcTi.SaveAndReimport();
        }
        return dstPath;
    }

    static string MakeChairWeaveTex(string path)
    {
        int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                int bx = x % 4;
                int by = y % 4;
                bool horiz = ((x / 4 + y / 4) % 2) == 0;
                float shade;
                if (horiz)
                    shade = (by == 0 || by == 3) ? 0.38f : ((bx == 0) ? 0.65f : 0.92f);
                else
                    shade = (bx == 0 || bx == 3) ? 0.38f : ((by == 0) ? 0.65f : 0.92f);
                tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
            }
        }
        return SavePixelTex(tex, path, 32, FilterMode.Point, TextureWrapMode.Repeat);
    }

    static string MakeChairSeatRibTex(string path)
    {
        int s = 16;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float shade = (x % 4 == 0) ? 0.84f : 1.0f;
                tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
            }
        }
        return SavePixelTex(tex, path, 16, FilterMode.Point, TextureWrapMode.Repeat);
    }

    static void AddDecalQuad(GameObject parent, string name, Vector3 localPos, Quaternion localRot, Vector3 scale, Material mat)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        q.transform.SetParent(parent.transform, false);
        q.transform.localPosition = localPos;
        q.transform.localRotation = localRot;
        q.transform.localScale = scale;
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void AddMeshPart(GameObject parent, string name, Mesh mesh, Material mat, bool addCollider)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (addCollider)
        {
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // REALISTIC LADY PALM MESH GENERATION
    // ════════════════════════════════════════════════════════════════════

    static void EnsurePlantAssets()
    {
        if (s_MeshPot != null && s_MatLeafDark != null) return;

        string leafTexPath = MakePalmLeafTex("Assets/Textures/palm_leaf_px.png");

        s_MatPot       = MakeMat("M_PalmPot",    null, new Color(0.20f, 0.16f, 0.12f), 0.15f); // terracotta/dark planter
        s_MatSaucer    = MakeMat("M_PalmSaucer", null, new Color(0.76f, 0.68f, 0.55f), 0.15f);
        s_MatSoil      = MakeMat("M_PalmSoil",   null, new Color(0.16f, 0.11f, 0.06f), 0.02f);
        s_MatStem      = MakeMat("M_PalmStem",   null, new Color(0.22f, 0.42f, 0.14f), 0.10f);
        s_MatLeafDark  = MakeMat("M_PalmLeaf_D", leafTexPath, new Color(0.18f, 0.48f, 0.14f), 0.15f);
        s_MatLeafLight = MakeMat("M_PalmLeaf_L", leafTexPath, new Color(0.35f, 0.70f, 0.22f), 0.18f);

        // 1. Saucer tray (Minecraft low poly box)
        var saucerParts = new List<CombineInstance>();
        saucerParts.Add(BuildUVBoxInstance(new Vector3(0f, 0.02f, 0f), new Vector3(0.48f, 0.04f, 0.48f)));
        s_MeshSaucer = SaveMeshAsset(CombineMeshes("PalmSaucerMesh", saucerParts));

        // 2. Square Minecraft Pot
        var potParts = new List<CombineInstance>();
        potParts.Add(BuildUVBoxInstance(new Vector3(0f, 0.20f, 0f), new Vector3(0.40f, 0.36f, 0.40f))); // main pot cube
        potParts.Add(BuildUVBoxInstance(new Vector3(0f, 0.40f, 0f), new Vector3(0.46f, 0.08f, 0.46f))); // top rim
        s_MeshPot = SaveMeshAsset(CombineMeshes("PalmPotMesh", potParts));

        // 3. Soil slab
        s_MeshSoil = SaveMeshAsset(CombineMeshes("PalmSoilMesh", new List<CombineInstance> {
            BuildUVBoxInstance(new Vector3(0f, 0.42f, 0f), new Vector3(0.38f, 0.04f, 0.38f))
        }));

        // 4. Low-poly Minecraft plant stem & blocky leaf volumes
        var stemMeshes     = new List<CombineInstance>();
        var leafDarkMeshes = new List<CombineInstance>();
        var leafLiteMeshes = new List<CombineInstance>();

        // Central vertical bamboo / stem block
        stemMeshes.Add(BuildUVBoxInstance(new Vector3(0f, 0.70f, 0f), new Vector3(0.08f, 0.54f, 0.08f)));

        // Blocky Minecraft-style leaf volumes
        // Tier 1: Lower leaf block (dark green)
        leafDarkMeshes.Add(BuildUVBoxInstance(new Vector3(0f, 0.62f, 0f), new Vector3(0.38f, 0.18f, 0.38f)));
        // 4 side leaf buds
        leafDarkMeshes.Add(BuildUVBoxInstance(new Vector3( 0.22f, 0.60f,  0f), new Vector3(0.12f, 0.12f, 0.16f)));
        leafDarkMeshes.Add(BuildUVBoxInstance(new Vector3(-0.22f, 0.60f,  0f), new Vector3(0.12f, 0.12f, 0.16f)));
        leafDarkMeshes.Add(BuildUVBoxInstance(new Vector3( 0f, 0.60f,  0.22f), new Vector3(0.16f, 0.12f, 0.12f)));
        leafDarkMeshes.Add(BuildUVBoxInstance(new Vector3( 0f, 0.60f, -0.22f), new Vector3(0.16f, 0.12f, 0.12f)));

        // Tier 2: Mid leaf block (light green)
        leafLiteMeshes.Add(BuildUVBoxInstance(new Vector3(0f, 0.78f, 0f), new Vector3(0.30f, 0.18f, 0.30f)));
        leafLiteMeshes.Add(BuildUVBoxInstance(new Vector3( 0.16f, 0.78f,  0f), new Vector3(0.10f, 0.10f, 0.14f)));
        leafLiteMeshes.Add(BuildUVBoxInstance(new Vector3(-0.16f, 0.78f,  0f), new Vector3(0.10f, 0.10f, 0.14f)));

        // Tier 3: Top leaf crown (light green)
        leafLiteMeshes.Add(BuildUVBoxInstance(new Vector3(0f, 0.94f, 0f), new Vector3(0.20f, 0.16f, 0.20f)));

        s_MeshStems       = SaveMeshAsset(CombineMeshes("PalmStemsMesh", stemMeshes));
        s_MeshLeavesDark  = SaveMeshAsset(CombineMeshes("PalmLeavesDarkMesh", leafDarkMeshes));
        s_MeshLeavesLight = SaveMeshAsset(CombineMeshes("PalmLeavesLightMesh", leafLiteMeshes));
    }

    static Mesh BuildArchedLeafMesh(float length, float width)
    {
        // Double-sided 4-segment slender arched lanceolate palm leaflet along +Z
        var m = new Mesh();
        int segs = 4;
        var verts = new List<Vector3>();
        var uvs   = new List<Vector2>();
        var tris  = new List<int>();

        float[] wProfile = { 0.25f, 1.0f, 0.92f, 0.55f, 0.04f };
        float[] yProfile = { 0.00f, 0.045f, 0.055f, 0.020f, -0.055f };

        // Top side & Bottom side vertices so leaves are visible from every angle
        for (int side = 0; side < 2; side++)
        {
            int vBase = verts.Count;
            for (int i = 0; i <= segs; i++)
            {
                float u = (float)i / segs;
                float z = u * length;
                float hw = width * 0.5f * wProfile[i];
                float y  = yProfile[i] * (length / 0.35f);

                verts.Add(new Vector3(-hw, y, z));
                verts.Add(new Vector3( hw, y, z));
                uvs.Add(new Vector2(0f, u));
                uvs.Add(new Vector2(1f, u));
            }
            for (int i = 0; i < segs; i++)
            {
                int i0 = vBase + i * 2;
                int i1 = i0 + 1;
                int i2 = i0 + 2;
                int i3 = i0 + 3;
                if (side == 0)
                {
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }
                else
                {
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i1); tris.Add(i3); tris.Add(i2);
                }
            }
        }

        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        return m;
    }

    static CombineInstance BuildTubeInstance(Vector3 p0, Vector3 p1, float rBottom, float rTop)
    {
        Vector3 dir = p1 - p0;
        float len = dir.magnitude;
        Mesh m = BuildFrustumMesh("StemSeg", 0f, len, rBottom, rTop, 6, false);
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        return new CombineInstance { mesh = m, transform = Matrix4x4.TRS(p0, rot, Vector3.one) };
    }

    static Mesh BuildFrustumMesh(string name, float y0, float y1, float r0, float r1, int sides, bool capTop)
    {
        var m = new Mesh { name = name };
        var verts = new List<Vector3>();
        var tris  = new List<int>();

        for (int i = 0; i <= sides; i++)
        {
            float a = (float)i / sides * Mathf.PI * 2f;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            verts.Add(new Vector3(c * r0, y0, s * r0));
            verts.Add(new Vector3(c * r1, y1, s * r1));
        }

        for (int i = 0; i < sides; i++)
        {
            int b0 = i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
            tris.Add(b0); tris.Add(t0); tris.Add(b1);
            tris.Add(b1); tris.Add(t0); tris.Add(t1);
        }

        if (capTop)
        {
            int centerIdx = verts.Count;
            verts.Add(new Vector3(0f, y1, 0f));
            for (int i = 0; i < sides; i++)
            {
                int t0 = i * 2 + 1;
                int t1 = (i + 1) * 2 + 1;
                tris.Add(centerIdx); tris.Add(t1); tris.Add(t0);
            }
        }

        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        return m;
    }

    // ════════════════════════════════════════════════════════════════════
    // BLUE SINTEX 60L DUSTBIN MESH & TEXTURE
    // ════════════════════════════════════════════════════════════════════

    static void EnsureDustbinAssets()
    {
        if (s_MeshBinBody != null && s_MatBinBlue != null) return;

        string decalTex = MakeSintexDecalTex("Assets/Textures/dustbin_sintex_px.png");
        s_MatBinBlue       = MakeMat("M_SintexBlue",       null, new Color(0.11f, 0.27f, 0.68f), 0.35f);
        s_MatBinDarkInside = MakeMat("M_SintexDarkInside", null, new Color(0.04f, 0.05f, 0.07f), 0.05f);
        s_MatBinDecal      = MakeMat("M_SintexDecal",      decalTex, Color.white, 0.25f);

        // Chunky stepped square body
        var bodyParts = new List<CombineInstance>();
        bodyParts.Add(BuildUVBoxInstance(new Vector3(0f, 0.20f, 0f), new Vector3(0.36f, 0.40f, 0.36f)));
        bodyParts.Add(BuildUVBoxInstance(new Vector3(0f, 0.58f, 0f), new Vector3(0.42f, 0.38f, 0.42f)));
        s_MeshBinBody = SaveMeshAsset(CombineMeshes("SintexBodyMesh", bodyParts));

        // Chunky stepped voxel hood shell with open throw-in tunnel along Z
        var hoodParts = new List<CombineInstance>();
        hoodParts.Add(BuildUVBoxInstance(new Vector3(-0.19f, 0.93f, 0f), new Vector3(0.06f, 0.22f, 0.44f)));
        hoodParts.Add(BuildUVBoxInstance(new Vector3( 0.19f, 0.93f, 0f), new Vector3(0.06f, 0.22f, 0.44f)));
        hoodParts.Add(BuildUVBoxInstance(new Vector3(0f, 1.05f, 0f), new Vector3(0.44f, 0.045f, 0.44f)));
        hoodParts.Add(BuildUVBoxInstance(new Vector3(0f, 1.095f, 0f), new Vector3(0.32f, 0.045f, 0.38f)));
        s_MeshBinHood = SaveMeshAsset(CombineMeshes("SintexHoodMesh", hoodParts));
    }

    static Mesh BuildTaperedBoxMesh(float bHalf, float tHalf, float y0, float y1)
    {
        var m = new Mesh { name = "SintexBodyMesh" };
        Vector3[] v = {
            // Bottom 0..3
            new Vector3(-bHalf, y0, -bHalf), new Vector3( bHalf, y0, -bHalf),
            new Vector3( bHalf, y0,  bHalf), new Vector3(-bHalf, y0,  bHalf),
            // Top 4..7
            new Vector3(-tHalf, y1, -tHalf), new Vector3( tHalf, y1, -tHalf),
            new Vector3( tHalf, y1,  tHalf), new Vector3(-tHalf, y1,  tHalf)
        };
        int[] t = {
            // -Z
            0,4,5, 0,5,1,
            // +X
            1,5,6, 1,6,2,
            // +Z
            2,6,7, 2,7,3,
            // -X
            3,7,4, 3,4,0,
            // Bottom & Top caps
            0,1,2, 0,2,3,
            4,7,6, 4,6,5
        };
        m.vertices = v;
        m.triangles = t;
        m.RecalculateNormals();
        return m;
    }

    static Mesh BuildArchedHoodMesh(float outerR, float thickness, float halfDepthZ, float baseY, float archHeight, int segs)
    {
        var m = new Mesh { name = "SintexHoodMesh" };
        var verts = new List<Vector3>();
        var tris  = new List<int>();
        float innerR = outerR - thickness;

        // Create arch profile along X (cos/sin from 0 to PI), extruded along Z from -halfDepthZ to +halfDepthZ
        for (int i = 0; i <= segs; i++)
        {
            float ang = (float)i / segs * Mathf.PI;
            float cx = Mathf.Cos(ang);
            float sy = Mathf.Sin(ang);

            float ox = cx * outerR;
            float oy = baseY + sy * archHeight;
            float ix = cx * innerR;
            float iy = baseY + sy * (archHeight - thickness);

            // 4 verts per ring step: outer-back, outer-front, inner-back, inner-front
            verts.Add(new Vector3(ox, oy, -halfDepthZ)); // 0
            verts.Add(new Vector3(ox, oy,  halfDepthZ)); // 1
            verts.Add(new Vector3(ix, iy, -halfDepthZ)); // 2
            verts.Add(new Vector3(ix, iy,  halfDepthZ)); // 3
        }

        for (int i = 0; i < segs; i++)
        {
            int a = i * 4;
            int b = (i + 1) * 4;
            // Outer arch roof
            tris.Add(a + 0); tris.Add(b + 0); tris.Add(a + 1);
            tris.Add(a + 1); tris.Add(b + 0); tris.Add(b + 1);
            // Inner arch ceiling
            tris.Add(a + 2); tris.Add(a + 3); tris.Add(b + 2);
            tris.Add(a + 3); tris.Add(b + 3); tris.Add(b + 2);
            // Back rim (-Z)
            tris.Add(a + 0); tris.Add(a + 2); tris.Add(b + 0);
            tris.Add(b + 0); tris.Add(a + 2); tris.Add(b + 2);
            // Front rim (+Z)
            tris.Add(a + 1); tris.Add(b + 1); tris.Add(a + 3);
            tris.Add(a + 3); tris.Add(b + 1); tris.Add(b + 3);
        }

        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        return m;
    }

    static Mesh SaveMeshAsset(Mesh mesh)
    {
        if (!Directory.Exists("Assets/Meshes")) Directory.CreateDirectory("Assets/Meshes");
        string path = $"Assets/Meshes/{mesh.name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.uv = mesh.uv;
            existing.triangles = mesh.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Mesh CombineMeshes(string name, List<CombineInstance> parts)
    {
        var combined = new Mesh { name = name };
        combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combined.CombineMeshes(parts.ToArray(), true, true);
        combined.RecalculateBounds();
        return combined;
    }

    // ════════════════════════════════════════════════════════════════════
    // PIXEL-ART TEXTURE GENERATORS
    // ════════════════════════════════════════════════════════════════════

    static string MakePalmLeafTex(string path)
    {
        int w = 16, h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color cBase  = new Color(0.18f, 0.48f, 0.14f);
        Color cBright= new Color(0.32f, 0.68f, 0.20f);
        Color cVein  = new Color(0.45f, 0.78f, 0.25f);
        Color cDark  = new Color(0.11f, 0.32f, 0.08f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = cBase;
                if (x == 7 || x == 8) c = cVein;
                else if (y > h * 0.65f) c = cBright;
                else if (x <= 2 || x >= w - 3) c = cDark;
                else if ((x + y) % 5 == 0) c = cDark;
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return SavePixelTex(tex, path, 32, FilterMode.Point, TextureWrapMode.Clamp);
    }

    static string MakeSintexDecalTex(string path)
    {
        int w = 48, h = 72;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color blue  = new Color(0.11f, 0.27f, 0.68f);
        Color green = new Color(0.14f, 0.62f, 0.24f);
        Color white = new Color(0.95f, 0.96f, 0.98f);

        Vector2 emblemCenter = new Vector2(24f, 32f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = blue;

                // Top green & white "Sintex" logo band (y = 54..63)
                if (y >= 54 && y <= 63 && x >= 7 && x <= 40)
                {
                    if (y == 54 || y == 63 || x == 7 || x == 40) c = green;
                    else c = (x % 4 == 1 && y >= 56 && y <= 61) ? green : white;
                }

                // Circular white/green RECYCLE emblem in center
                float d = Vector2.Distance(new Vector2(x, y), emblemCenter);
                if (d <= 12.5f)
                {
                    c = white;
                    if (d >= 8.5f && d <= 10.8f && (x + y) % 3 != 0)
                        c = new Color(0.15f, 0.18f, 0.22f); // "RECYCLE" ring pixels
                    else if (d <= 6.5f)
                    {
                        // 3 green recycle arrows triangle motif
                        if ((y > 33 && Mathf.Abs(x - 24) < 5) ||
                            (y <= 32 && y >= 27 && Mathf.Abs(x - 24) >= 2 && Mathf.Abs(x - 24) <= 6))
                            c = green;
                    }
                }

                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return SavePixelTex(tex, path, 64, FilterMode.Point, TextureWrapMode.Clamp);
    }

    static string SavePixelTex(Texture2D tex, string assetPath, int maxSize, FilterMode filter, TextureWrapMode wrap)
    {
        tex.Apply();
        File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti != null)
        {
            ti.filterMode = filter;
            ti.wrapMode = wrap;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = maxSize;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        return assetPath;
    }

    static Material MakeMat(string name, string texPath, Color col, float smoothness)
    {
        string path = $"Assets/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(sh) { name = name };
            if (!Directory.Exists("Assets/Materials")) Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = col;
        m.SetColor("_BaseColor", col);
        m.SetFloat("_Smoothness", smoothness);
        if (!string.IsNullOrEmpty(texPath))
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (t != null)
            {
                m.mainTexture = t;
                m.SetTexture("_BaseMap", t);
                m.mainTextureScale = Vector2.one;
                m.SetTextureScale("_BaseMap", Vector2.one);
            }
        }
        else
        {
            m.mainTexture = null;
            m.SetTexture("_BaseMap", null);
        }
        EditorUtility.SetDirty(m);
        return m;
    }
}
