using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// IITB Mess – π (pi) / U-shaped layout (top-down view, -Z = North):
///
///  +----+----------------------------+----+
///  |    |     COUNTER BAR (North)    |    |  <- open east & west short ends
///  |    +----------------------------+    |
///  |                                      |
///  | W  |   OPEN MIDDLE COURTYARD    | E  |
///  | E  |   (benches & potted plants)| A  |
///  | S  |                            | S  |
///  | T  |                            | T  |
///  |    |                            |    |
///  +----+                            +----+
///       v SOUTH (open front with thick pillars -> Outer Courtyard)
/// </summary>
public static class BuildCafeteria
{
    // ── Layout dimensions ────────────────────────────────────────────────
    const float WING_W = 16f;   // east-west width of each wing
    const float WING_D = 56f;   // extended north-south depth of each wing
    const float BAR_W  = 32f;   // gap between wings (middle courtyard width)
    const float BAR_D  = 12f;   // north-south depth of counter bar
    const float H      =  4.8f; // ground floor ceiling height
    const float BLDG_H = 22f;   // white Bhaskara building height above (no windows)

    const float TOTAL_W = WING_W + BAR_W + WING_W; // 64m
    const float TOTAL_D = BAR_D + WING_D;          // 68m

    const float FLOOR_TEX_UNIT = 4.0f; // 1 texture repeat (4x4 tiles) = 4m x 4m (1m per tile)
    const float WALL_TEX_UNIT  = 8.0f; // 1 texture repeat (2x2 panels) = 8m x 8m

    static readonly Dictionary<string, Material> s_MatCache = new Dictionary<string, Material>();

    [MenuItem("Tools/Build Cafeteria Map")]
    public static void Build()
    {
        s_MatCache.Clear();
        BuildProps.ResetCache();

        // 0. Clear previous cafeteria (keep Main Camera & Directional Light)
        var existing = GameObject.Find("IITB_Cafeteria");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        // 1. Ensure crisp pixelated textures exist
        string floorTexPx = BuildProps.EnsureSlateFloorTexture(
            "Assets/Textures/floor_slate_src.png",
            "Assets/Textures/floor_slate_px.png");

        string wallTexPx = EnsurePixelatedTexture(
            "Assets/Textures/bhaskara_white_wall_v2.jpg",
            "Assets/Textures/bhaskara_white_wall_px.png",
            64, contrastBoost: 1.30f, preserveDarkLines: true);

        string concTexPx = EnsureConcretePixelTexture(
            "Assets/Textures/concrete_px.png", 48);

        // 2. Base colors
        Color colWhite   = new Color(0.95f, 0.95f, 0.93f);
        Color colConc    = new Color(0.62f, 0.60f, 0.57f);
        Color colFloor   = new Color(0.95f, 0.95f, 0.95f);
        Color colCourt   = new Color(0.88f, 0.88f, 0.88f);
        Color colChair   = new Color(0.85f, 0.40f, 0.08f);
        Color colTable   = new Color(0.18f, 0.16f, 0.14f);
        Color colCnt     = new Color(0.40f, 0.38f, 0.36f);
        Color colCntTop  = new Color(0.82f, 0.82f, 0.80f);
        Color colPlant   = new Color(0.15f, 0.42f, 0.10f);
        Color colPot     = new Color(0.10f, 0.10f, 0.10f);
        Color colLight   = new Color(1.00f, 0.98f, 0.85f);

        var matChair  = Mat("M_Chair",   null, Vector2.one, colChair);
        var matTable  = Mat("M_Table",   null, Vector2.one, colTable);
        var matCnt    = Mat("M_Counter", concTexPx, new Vector2(8f, 1f), colCnt);
        var matCntTop = Mat("M_CntTop",  null, Vector2.one, colCntTop);
        var matPlant  = Mat("M_Plant",   null, Vector2.one, colPlant);
        var matPot    = Mat("M_Pot",     null, Vector2.one, colPot);
        var matLight  = Mat("M_Light",   null, Vector2.one, colLight);
        var matConcDefault = Mat("M_Concrete", concTexPx, new Vector2(2f, 2f), colConc);

        // 3. Root & coordinate bounds
        var root = new GameObject("IITB_Cafeteria");
        Undo.RegisterCreatedObjectUndo(root, "Build Cafeteria");

        float zNorth = -TOTAL_D * 0.5f;         // -34
        float zBarS  = zNorth + BAR_D;          // -22
        float zSouth =  TOTAL_D * 0.5f;         // +34

        float xWestOuter = -TOTAL_W * 0.5f;     // -32
        float xWestInner = xWestOuter + WING_W; // -16
        float xEastInner = xWestInner + BAR_W;  // +16
        float xEastOuter =  TOTAL_W * 0.5f;     // +32

        var grpStructure = CreateChild(root, "Structure");
        var grpWings     = CreateChild(root, "Seating_Wings");
        var grpCounter   = CreateChild(root, "Food_Counter");
        var grpProps     = CreateChild(root, "Plants_And_Props");
        var grpLights    = CreateChild(root, "Lights");

        // ── FLOORS (proportionate tiling so tiles are crisp & square) ────
        CreateFloor(grpStructure, "Floor_WestWing",
            (xWestOuter + xWestInner) * 0.5f, (zBarS + zSouth) * 0.5f,
            WING_W, WING_D, floorTexPx, colFloor);

        CreateFloor(grpStructure, "Floor_EastWing",
            (xEastInner + xEastOuter) * 0.5f, (zBarS + zSouth) * 0.5f,
            WING_W, WING_D, floorTexPx, colFloor);

        CreateFloor(grpStructure, "Floor_CounterBar",
            0f, (zNorth + zBarS) * 0.5f,
            TOTAL_W, BAR_D, floorTexPx, colFloor);

        CreateFloor(grpStructure, "Floor_MiddleCourtyard",
            0f, (zBarS + zSouth) * 0.5f,
            BAR_W, WING_D, floorTexPx, colCourt);

        float outerCourtD = 16f;
        CreateFloor(grpStructure, "Floor_OuterCourtyard",
            0f, zSouth + outerCourtD * 0.5f,
            TOTAL_W + 20f, outerCourtD, floorTexPx, colCourt);

        // ── CEILING SLABS ────────────────────────────────────────────────
        CreateBoxWorld(grpStructure, "Ceiling_WestWing",
            new Vector3((xWestOuter + xWestInner) * 0.5f, H + 0.25f, (zBarS + zSouth) * 0.5f),
            new Vector3(WING_W, 0.5f, WING_D),
            Mat("M_Conc_WingCeil", concTexPx, new Vector2(4f, 14f), colConc));

        CreateBoxWorld(grpStructure, "Ceiling_EastWing",
            new Vector3((xEastInner + xEastOuter) * 0.5f, H + 0.25f, (zBarS + zSouth) * 0.5f),
            new Vector3(WING_W, 0.5f, WING_D),
            Mat("M_Conc_WingCeil", concTexPx, new Vector2(4f, 14f), colConc));

        CreateBoxWorld(grpStructure, "Ceiling_CounterBar",
            new Vector3(0f, H + 0.25f, (zNorth + zBarS) * 0.5f),
            new Vector3(TOTAL_W, 0.5f, BAR_D),
            Mat("M_Conc_BarCeil", concTexPx, new Vector2(16f, 3f), colConc));

        // ── OUTER WALLS ──────────────────────────────────────────────────
        CreateBoxWorld(grpStructure, "Wall_North",
            new Vector3(0f, H * 0.5f, zNorth),
            new Vector3(TOTAL_W, H, 0.4f),
            Mat("M_Conc_NorthWall", concTexPx, new Vector2(16f, 1.5f), colConc));

        CreateBoxWorld(grpStructure, "Wall_WestOuter",
            new Vector3(xWestOuter, H * 0.5f, (zNorth + zSouth) * 0.5f),
            new Vector3(0.4f, H, TOTAL_D),
            Mat("M_Conc_SideWall", concTexPx, new Vector2(17f, 1.5f), colConc));

        CreateBoxWorld(grpStructure, "Wall_EastOuter",
            new Vector3(xEastOuter, H * 0.5f, (zNorth + zSouth) * 0.5f),
            new Vector3(0.4f, H, TOTAL_D),
            Mat("M_Conc_SideWall", concTexPx, new Vector2(17f, 1.5f), colConc));

        // ── PILLARS (Thick 0.9m columns, widely spaced) ──────────────────
        float pilSize = 0.9f;
        var matPillar = Mat("M_Conc_Pillar", concTexPx, new Vector2(1f, 2f), colConc);

        float[] barPillarsX = { -12f, -4f, 4f, 12f };
        foreach (float px in barPillarsX)
        {
            CreateBoxWorld(grpStructure, $"Pillar_BarFront_{px}",
                new Vector3(px, H * 0.5f, zBarS),
                new Vector3(pilSize, H, pilSize), matPillar);
        }

        // Corner junction pillars (East corner also hosts the Ideal vertical banner)
        CreateBoxWorld(grpStructure, "Pillar_WestCorner",
            new Vector3(xWestInner, H * 0.5f, zBarS),
            new Vector3(pilSize, H, pilSize), matPillar);
        CreateBoxWorld(grpStructure, "Pillar_EastCorner",
            new Vector3(xEastInner, H * 0.5f, zBarS),
            new Vector3(pilSize, H, pilSize), matPillar);

        // Inner courtyard wing edges (every 8m along 56m wing -> 7 pillars per side)
        for (float pz = zBarS + 4f; pz <= zSouth - 1f; pz += 8f)
        {
            CreateBoxWorld(grpStructure, $"Pillar_WestInner_{pz:F0}",
                new Vector3(xWestInner, H * 0.5f, pz),
                new Vector3(pilSize, H, pilSize), matPillar);

            CreateBoxWorld(grpStructure, $"Pillar_EastInner_{pz:F0}",
                new Vector3(xEastInner, H * 0.5f, pz),
                new Vector3(pilSize, H, pilSize), matPillar);
        }

        // South entrance pillars
        CreateBoxWorld(grpStructure, "Pillar_WestSouth_1", new Vector3(xWestOuter + 5.3f, H * 0.5f, zSouth), new Vector3(pilSize, H, pilSize), matPillar);
        CreateBoxWorld(grpStructure, "Pillar_WestSouth_2", new Vector3(xWestOuter + 10.6f, H * 0.5f, zSouth), new Vector3(pilSize, H, pilSize), matPillar);
        CreateBoxWorld(grpStructure, "Pillar_EastSouth_1", new Vector3(xEastInner + 5.3f, H * 0.5f, zSouth), new Vector3(pilSize, H, pilSize), matPillar);
        CreateBoxWorld(grpStructure, "Pillar_EastSouth_2", new Vector3(xEastInner + 10.6f, H * 0.5f, zSouth), new Vector3(pilSize, H, pilSize), matPillar);

        // ── WHITE BHASKARA BUILDING ABOVE (NO WINDOWS) ───────────────────
        float bldgY = H + 0.5f + BLDG_H * 0.5f;

        CreateBoxWorld(grpStructure, "Building_NorthBlock",
            new Vector3(0f, bldgY, (zNorth + zBarS) * 0.5f),
            new Vector3(TOTAL_W + 0.8f, BLDG_H, BAR_D + 0.8f),
            Mat("M_White_North", wallTexPx, new Vector2(TOTAL_W / WALL_TEX_UNIT, BLDG_H / WALL_TEX_UNIT), colWhite));

        CreateBoxWorld(grpStructure, "Building_WestBlock",
            new Vector3((xWestOuter + xWestInner) * 0.5f, bldgY, (zBarS + zSouth) * 0.5f),
            new Vector3(WING_W + 0.8f, BLDG_H, WING_D + 0.8f),
            Mat("M_White_Wing", wallTexPx, new Vector2(WING_D / WALL_TEX_UNIT, BLDG_H / WALL_TEX_UNIT), colWhite));

        CreateBoxWorld(grpStructure, "Building_EastBlock",
            new Vector3((xEastInner + xEastOuter) * 0.5f, bldgY, (zBarS + zSouth) * 0.5f),
            new Vector3(WING_W + 0.8f, BLDG_H, WING_D + 0.8f),
            Mat("M_White_Wing", wallTexPx, new Vector2(WING_D / WALL_TEX_UNIT, BLDG_H / WALL_TEX_UNIT), colWhite));

        // Roof cornices
        CreateBoxWorld(grpStructure, "Building_Cornice_North",
            new Vector3(0f, H + 0.5f + BLDG_H + 0.4f, (zNorth + zBarS) * 0.5f),
            new Vector3(TOTAL_W + 1.4f, 0.8f, BAR_D + 1.4f), matConcDefault);
        CreateBoxWorld(grpStructure, "Building_Cornice_West",
            new Vector3((xWestOuter + xWestInner) * 0.5f, H + 0.5f + BLDG_H + 0.4f, (zBarS + zSouth) * 0.5f),
            new Vector3(WING_W + 1.4f, 0.8f, WING_D + 1.4f), matConcDefault);
        CreateBoxWorld(grpStructure, "Building_Cornice_East",
            new Vector3((xEastInner + xEastOuter) * 0.5f, H + 0.5f + BLDG_H + 0.4f, (zBarS + zSouth) * 0.5f),
            new Vector3(WING_W + 1.4f, 0.8f, WING_D + 1.4f), matConcDefault);

        // ── FOOD COUNTER (North Bar - starts after West Tuck Shop, NO SEATING) ─
        float counterZ   = zNorth + 2.0f;
        float cntMinX    = xWestInner + 2.0f;  // -14 (leaves NW corner for Tuck Shop)
        float cntMaxX    = xEastOuter - 2.0f;  // +30
        float cntCenterX = (cntMinX + cntMaxX) * 0.5f;
        float counterLen = cntMaxX - cntMinX;

        CreateBoxWorld(grpCounter, "Counter_Base",
            new Vector3(cntCenterX, 0.55f, counterZ), new Vector3(counterLen, 1.1f, 1.0f), matCnt);
        CreateBoxWorld(grpCounter, "Counter_Top",
            new Vector3(cntCenterX, 1.12f, counterZ), new Vector3(counterLen + 0.2f, 0.08f, 1.2f), matCntTop);
        CreateBoxWorld(grpCounter, "Tray_Rail",
            new Vector3(cntCenterX, 0.95f, counterZ + 0.65f), new Vector3(counterLen, 0.05f, 0.25f), matCntTop);
        CreateBoxWorld(grpCounter, "Sneeze_Guard",
            new Vector3(cntCenterX, 1.55f, counterZ + 0.2f), new Vector3(counterLen, 0.7f, 0.04f), matConcDefault);
        CreateBoxWorld(grpCounter, "Overhead_Hood",
            new Vector3(cntCenterX, 2.7f, counterZ), new Vector3(counterLen, 0.2f, 1.1f), matConcDefault);

        for (float fx = cntMinX + 4f; fx <= cntMaxX - 4f; fx += 8f)
        {
            CreateBoxWorld(grpCounter, $"Food_Warmer_{fx:F0}",
                new Vector3(fx, 1.18f, counterZ), new Vector3(2.5f, 0.12f, 0.8f), matTable);
        }

        // ── TUCK SHOP (North-West Corner - opposite side) & IDEAL POSTER ──
        BuildTuckShop.Build(root, xWestOuter, xWestInner, zNorth, zBarS, H);

        // ── DENSE CONGESTED SEATING (Both Extended Wings: Mix of Big Long Tables + Standard Tables) ──
        BuildWingSeating(grpWings, "WestWing", xWestOuter + 0.75f, xWestInner - 0.75f, zBarS + 1.35f, zSouth - 1.1f, isLeftWing: true);
        BuildWingSeating(grpWings, "EastWing", xEastInner + 0.75f, xEastOuter - 0.75f, zBarS + 1.35f, zSouth - 1.1f, isLeftWing: false);

        // ── INTERIOR CEILING LIGHTS ──────────────────────────────────────
        AddInteriorLights(grpLights, xWestOuter + 2f, xWestInner - 2f, zBarS + 4f, zSouth - 4f, H - 0.2f, matLight);
        AddInteriorLights(grpLights, xEastInner + 2f, xEastOuter - 2f, zBarS + 4f, zSouth - 4f, H - 0.2f, matLight);
        AddCounterLights(grpLights, cntMinX + 2f, cntMaxX - 2f, counterZ + 1.5f, H - 0.2f, matLight);

        // ── POTTED PALM PLANTS & BLUE SINTEX DUSTBINS (middle courtyard kept clean!) ──
        for (float pz = zBarS + 4f; pz <= zSouth - 2f; pz += 6f)
        {
            BuildProps.CreatePalmPlant(grpProps, new Vector3(xWestInner + 1.5f, 0f, pz), 0.95f);
            BuildProps.CreatePalmPlant(grpProps, new Vector3(xEastInner - 1.5f, 0f, pz), 0.95f);
        }
        for (float px = xWestOuter + 2f; px <= xEastOuter - 2f; px += 5.5f)
        {
            BuildProps.CreatePalmPlant(grpProps, new Vector3(px, 0f, zSouth + 1.5f), 1.05f);
        }

        // Blue Sintex Dustbins placed in key cafeteria areas
        // 1. Food counter corridor
        BuildProps.CreateSintexDustbin(grpProps, new Vector3(-13.5f, 0f, zBarS - 1.2f),   0f);
        BuildProps.CreateSintexDustbin(grpProps, new Vector3(  8.0f, 0f, zBarS - 1.2f),   0f);
        BuildProps.CreateSintexDustbin(grpProps, new Vector3( 28.5f, 0f, zBarS - 1.2f),   0f);
        // 2. Tuck shop archway entrance (West side)
        BuildProps.CreateSintexDustbin(grpProps, new Vector3(-20.6f, 0f, zBarS - 0.8f),  90f);
        // 3. Inside West & East dining wings near courtyard pillars
        BuildProps.CreateSintexDustbin(grpProps, new Vector3(xWestInner - 1.0f, 0f, -6.0f), 90f);
        BuildProps.CreateSintexDustbin(grpProps, new Vector3(xEastInner + 1.0f, 0f, -6.0f), -90f);
        BuildProps.CreateSintexDustbin(grpProps, new Vector3(xWestInner - 1.0f, 0f, 18.0f), 90f);
        BuildProps.CreateSintexDustbin(grpProps, new Vector3(xEastInner + 1.0f, 0f, 18.0f), -90f);
        // 4. South entrances (facing courtyard)
        BuildProps.CreateSintexDustbin(grpProps, new Vector3(xWestInner - 1.2f, 0f, zSouth - 0.6f), 0f);
        BuildProps.CreateSintexDustbin(grpProps, new Vector3(xEastInner + 1.2f, 0f, zSouth - 0.6f), 0f);

        // ── PROP HUNT TAGS & RANDOMIZER ──────────────────────────────────
        SetupPropHuntTags(root);

        // ── LIGHTING & SAVE ──────────────────────────────────────────────
        var dirLight = Object.FindAnyObjectByType<Light>();
        if (dirLight != null && dirLight.type == LightType.Directional)
        {
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            dirLight.intensity = 1.25f;
            dirLight.color = new Color(1.0f, 0.98f, 0.92f);
        }

        var curScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(curScene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[BuildCafeteria] Built extended Pi-shaped cafeteria ({TOTAL_W}m x {TOTAL_D}m) with crisp pixelated textures.");
    }

    public static void SetupPropHuntTags(GameObject cafeRoot)
    {
        if (cafeRoot.GetComponent<PropRandomizer>() == null)
            cafeRoot.AddComponent<PropRandomizer>();

        foreach (Transform t in cafeRoot.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            string label = null;

            if (n.StartsWith("Chair_") && !n.Contains("Shell") && !n.Contains("Legs"))
                label = "Chair";
            else if (n.StartsWith("StdTable_"))
                label = "Table";
            else if (n == "PalmPlant")
                label = "Potted Plant";
            else if (n == "Dustbin_Sintex")
                label = "Dustbin";
            else if (n.StartsWith("Packet_") || n.StartsWith("CounterPacket_"))
                label = "Chips Packet";
            else if (n.StartsWith("SnackJar_"))
                label = "Snack Jar";
            else if (n.StartsWith("IceCream_Freezer_"))
                label = "Ice Cream Freezer";

            if (label != null)
            {
                var pt = t.GetComponent<PropTarget>();
                if (pt == null) pt = t.gameObject.AddComponent<PropTarget>();
                pt.propDisplayName = label;
            }
        }
    }

    static void CreateFloor(GameObject parent, string name, float cx, float cz, float w, float d, string texPath, Color col)
    {
        var tiling = new Vector2(w / FLOOR_TEX_UNIT, d / FLOOR_TEX_UNIT);
        var mat = Mat($"M_{name}", texPath, tiling, col);
        CreateBoxWorld(parent, name, new Vector3(cx, -0.1f, cz), new Vector3(w, 0.2f, d), mat);
    }

    static void BuildWingSeating(GameObject parent, string wingName, float minX, float maxX, float minZ, float maxZ, bool isLeftWing)
    {
        var wingGroup = CreateChild(parent, wingName);

        float midX = (minX + maxX) * 0.5f;
        float splitZ = Mathf.Lerp(minZ, maxZ, 0.44f); // North 44% = Big Long Rectangular Tables; South 56% = Standard Tables

        // 1. Long Big Rectangular Tables (2.9m x 0.82m, 8 chairs each) — 3 congested columns across the wing
        float longW = 2.9f, longD = 0.82f;
        float colStep = 4.65f;
        float[] longColsX = { midX - colStep, midX, midX + colStep };
        float longDz = 2.15f;
        int longRows = Mathf.Max(1, Mathf.FloorToInt((splitZ - minZ) / longDz));
        for (int r = 0; r < longRows; r++)
        {
            float tz = minZ + 1.0f + r * longDz;
            for (int c = 0; c < longColsX.Length; c++)
            {
                BuildProps.CreateMessTable(wingGroup, $"BigTable_{r}_{c}",
                    new Vector3(longColsX[c], 0f, tz), longW, longD, isLeftWing);
            }
        }

        // 2. Standard 4-Seater Tables (1.25m x 0.78m, 4 chairs each) — 5 congested columns filling to the south edge
        float stdW = 1.25f, stdD = 0.78f;
        float stdStartZ = minZ + 1.0f + longRows * longDz + 0.15f;
        float remainingZ = maxZ - stdStartZ;
        float dz = 2.02f;
        int rows = Mathf.Max(1, Mathf.FloorToInt(remainingZ / dz) + 1);
        if (stdStartZ + (rows - 1) * dz > maxZ - 0.8f) rows--;

        int cols = 5;
        float dx = (maxX - minX - 2.1f) / Mathf.Max(1, cols - 1);
        float startX = minX + 1.05f;

        for (int r = 0; r < rows; r++)
        {
            float tz = stdStartZ + r * dz;
            for (int c = 0; c < cols; c++)
            {
                float tx = startX + c * dx;
                BuildProps.CreateMessTable(wingGroup, $"StdTable_{r}_{c}",
                    new Vector3(tx, 0f, tz), stdW, stdD, isLeftWing);
            }
        }
    }

    static void AddInteriorLights(GameObject parent, float minX, float maxX, float minZ, float maxZ, float y, Material matLight)
    {
        for (float z = minZ; z <= maxZ; z += 7f)
        {
            for (float x = minX + 2f; x <= maxX - 2f; x += 6f)
            {
                var fix = CreateBoxWorld(parent, "LightFixture", new Vector3(x, y, z), new Vector3(0.3f, 0.08f, 1.8f), matLight);
                var lGo = new GameObject("LightSource");
                lGo.transform.SetParent(fix.transform, false);
                lGo.transform.localPosition = new Vector3(0f, -0.2f, 0f);
                var l = lGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 10f;
                l.intensity = 1.0f;
                l.color = new Color(1.0f, 0.96f, 0.88f);
            }
        }
    }

    static void AddCounterLights(GameObject parent, float minX, float maxX, float z, float y, Material matLight)
    {
        for (float x = minX; x <= maxX; x += 6f)
        {
            var fix = CreateBoxWorld(parent, "CounterLightFixture", new Vector3(x, y, z), new Vector3(1.8f, 0.08f, 0.3f), matLight);
            var lGo = new GameObject("LightSource");
            lGo.transform.SetParent(fix.transform, false);
            lGo.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            var l = lGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 9f;
            l.intensity = 1.2f;
            l.color = new Color(1.0f, 0.95f, 0.85f);
        }
    }

    static void CreatePot(GameObject parent, Vector3 pos, Material matPlant, Material matPot)
    {
        var potGo = CreateChild(parent, "PlantPot");
        potGo.transform.position = pos;

        var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.name = "Pot";
        cyl.transform.SetParent(potGo.transform, false);
        cyl.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        cyl.transform.localScale = new Vector3(0.7f, 0.35f, 0.7f);
        cyl.GetComponent<Renderer>().sharedMaterial = matPot;

        var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sph.name = "Bush";
        sph.transform.SetParent(potGo.transform, false);
        sph.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        sph.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
        sph.GetComponent<Renderer>().sharedMaterial = matPlant;
    }

    static GameObject CreateBoxWorld(GameObject parent, string name, Vector3 worldPos, Vector3 size, Material mat)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent.transform, false);
        cube.transform.position = worldPos;
        cube.transform.localScale = size;
        if (mat != null) cube.GetComponent<Renderer>().sharedMaterial = mat;
        return cube;
    }

    static GameObject CreateBoxLocal(GameObject parent, string name, Vector3 localPos, Vector3 size, Material mat)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent.transform, false);
        cube.transform.localPosition = localPos;
        cube.transform.localScale = size;
        if (mat != null) cube.GetComponent<Renderer>().sharedMaterial = mat;
        return cube;
    }

    static GameObject CreateChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    /// <summary>
    /// Downsamples a source texture to a crisp low-res nearest-neighbor PNG so every texel pops.
    /// </summary>
    public static string EnsurePixelatedTexture(string srcAssetPath, string dstAssetPath, int targetSize, float contrastBoost, bool preserveDarkLines = false)
    {
        if (!File.Exists(srcAssetPath)) return srcAssetPath;

        var srcTi = AssetImporter.GetAtPath(srcAssetPath) as TextureImporter;
        bool prevReadable = srcTi != null && srcTi.isReadable;
        if (srcTi != null && !prevReadable)
        {
            srcTi.isReadable = true;
            srcTi.SaveAndReimport();
        }

        var srcTex = AssetDatabase.LoadAssetAtPath<Texture2D>(srcAssetPath);
        if (srcTex != null)
        {
            var dstTex = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, false);
            int srcW = srcTex.width;
            int srcH = srcTex.height;

            for (int y = 0; y < targetSize; y++)
            {
                for (int x = 0; x < targetSize; x++)
                {
                    int sx0 = Mathf.FloorToInt((float)x / targetSize * srcW);
                    int sy0 = Mathf.FloorToInt((float)y / targetSize * srcH);
                    int sx1 = Mathf.Min(srcW - 1, Mathf.FloorToInt((float)(x + 1) / targetSize * srcW));
                    int sy1 = Mathf.Min(srcH - 1, Mathf.FloorToInt((float)(y + 1) / targetSize * srcH));

                    Color chosen = srcTex.GetPixel((sx0 + sx1) / 2, (sy0 + sy1) / 2);

                    if (preserveDarkLines)
                    {
                        Color darkest = chosen;
                        float minLum = chosen.grayscale;
                        for (int sy = sy0; sy <= sy1; sy += 2)
                        {
                            for (int sx = sx0; sx <= sx1; sx += 2)
                            {
                                Color c = srcTex.GetPixel(sx, sy);
                                if (c.grayscale < minLum)
                                {
                                    minLum = c.grayscale;
                                    darkest = c;
                                }
                            }
                        }
                        if (chosen.grayscale - minLum > 0.14f)
                            chosen = Color.Lerp(chosen, darkest, 0.70f);
                    }

                    if (Mathf.Abs(contrastBoost - 1f) > 0.01f)
                    {
                        chosen.r = Mathf.Clamp01((chosen.r - 0.5f) * contrastBoost + 0.5f);
                        chosen.g = Mathf.Clamp01((chosen.g - 0.5f) * contrastBoost + 0.5f);
                        chosen.b = Mathf.Clamp01((chosen.b - 0.5f) * contrastBoost + 0.5f);
                    }
                    chosen.a = 1f;
                    dstTex.SetPixel(x, y, chosen);
                }
            }
            dstTex.Apply();
            File.WriteAllBytes(dstAssetPath, dstTex.EncodeToPNG());
            Object.DestroyImmediate(dstTex);
            AssetDatabase.ImportAsset(dstAssetPath, ImportAssetOptions.ForceUpdate);
        }

        if (srcTi != null && !prevReadable)
        {
            srcTi.isReadable = false;
            srcTi.SaveAndReimport();
        }

        ConfigureRetroTextureImporter(dstAssetPath, targetSize);
        return dstAssetPath;
    }

    public static string EnsureConcretePixelTexture(string dstAssetPath, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var rng = new System.Random(42);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = 0.88f + (float)(rng.NextDouble() * 0.16 - 0.08);
                // Subtle concrete panel seam every 24 pixels
                if (x == 0 || y == 0 || x == size / 2 || y == size / 2)
                    v -= 0.08f;
                v = Mathf.Clamp01(v);
                tex.SetPixel(x, y, new Color(v, v, v, 1f));
            }
        }
        tex.Apply();
        File.WriteAllBytes(dstAssetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(dstAssetPath, ImportAssetOptions.ForceUpdate);
        ConfigureRetroTextureImporter(dstAssetPath, size);
        return dstAssetPath;
    }

    static void ConfigureRetroTextureImporter(string assetPath, int maxSize)
    {
        var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti == null) return;
        ti.filterMode = FilterMode.Point;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = Mathf.Max(32, Mathf.NextPowerOfTwo(maxSize));
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
    }

    static Material Mat(string name, string texPath, Vector2 tiling, Color baseColor)
    {
        if (s_MatCache.TryGetValue(name, out var cached) && cached != null)
            return cached;

        string matPath = $"Assets/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(sh) { name = name };
            if (!Directory.Exists("Assets/Materials"))
                Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(m, matPath);
        }

        m.color = baseColor;
        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Smoothness", 0.08f); // matte retro look

        if (!string.IsNullOrEmpty(texPath))
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (t != null)
            {
                m.mainTexture = t;
                m.SetTexture("_BaseMap", t);
                m.mainTextureScale = tiling;
                m.SetTextureScale("_BaseMap", tiling);
            }
        }
        else
        {
            m.mainTexture = null;
            m.SetTexture("_BaseMap", null);
        }

        EditorUtility.SetDirty(m);
        s_MatCache[name] = m;
        return m;
    }
}
