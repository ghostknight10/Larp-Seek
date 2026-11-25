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
    const float WING_W      = 16f;   // east-west width of each wing
    const float WEST_WING_D = 58f;   // north-south depth of West Wing (connects to South Hall)
    const float EAST_WING_D = 44f;   // shorter East Wing depth (leaves walkway gap before South Hall)
    const float BAR_W       = 32f;   // gap between wings (middle courtyard width)
    const float BAR_D       = 12f;   // north-south depth of counter bar
    const float SOUTH_D     = 12f;   // north-south depth of South Hall
    const float SOUTH_EXT_E = 24f;   // South Hall eastern X bound (stops halfway under East Wing, leaving gap)
    const float H           =  4.8f; // ground floor ceiling height
    const float BLDG_H      = 22f;   // white Bhaskara building height above (no windows)
    const float SOUTH_BLDG_H = 6.7f; // upper smooth concrete wall height above South Hall ground floor

    const float TOTAL_W = WING_W + BAR_W + WING_W;      // 64m
    const float TOTAL_D = BAR_D + WEST_WING_D + SOUTH_D; // 82m (North bar + West wing + South hall corner)

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

        float zNorth      = -34f;
        float zBarS       = zNorth + BAR_D;            // -22
        float zEastEnd    = zBarS + EAST_WING_D;       // +22 (shorter East Wing, leaves walkway gap)
        float zSouthHallN = zBarS + WEST_WING_D;       // +36 (West Wing connects into South Hall)
        float zSouthHallS = zSouthHallN + SOUTH_D;     // +48 (outer South glass-door facade)

        float xWestOuter = -TOTAL_W * 0.5f;            // -32
        float xWestInner = xWestOuter + WING_W;        // -16
        float xEastInner = xWestInner + BAR_W;         // +16
        float xEastOuter =  TOTAL_W * 0.5f;            // +32
        float xSouthExtE = SOUTH_EXT_E;                // +24 (South Hall eastern end)

        var grpStructure = CreateChild(root, "Structure");
        var grpSouthHall = CreateChild(root, "South_Hall");
        var grpWings     = CreateChild(root, "Seating_Wings");
        var grpCounter   = CreateChild(root, "Food_Counter");
        var grpProps     = CreateChild(root, "Plants_And_Props");
        var grpLights    = CreateChild(root, "Lights");

        // ── FLOORS (proportionate tiling so tiles are crisp & square) ────
        CreateFloor(grpStructure, "Floor_WestWing",
            (xWestOuter + xWestInner) * 0.5f, (zBarS + zSouthHallN) * 0.5f,
            WING_W, WEST_WING_D, floorTexPx, colFloor);

        CreateFloor(grpStructure, "Floor_EastWing",
            (xEastInner + xEastOuter) * 0.5f, (zBarS + zEastEnd) * 0.5f,
            WING_W, EAST_WING_D, floorTexPx, colFloor);

        CreateFloor(grpStructure, "Floor_CounterBar",
            0f, (zNorth + zBarS) * 0.5f,
            TOTAL_W, BAR_D, floorTexPx, colFloor);

        CreateFloor(grpStructure, "Floor_MiddleCourtyard",
            0f, (zBarS + zSouthHallN) * 0.5f,
            BAR_W, WEST_WING_D, floorTexPx, colCourt);

        // Walkway tiles in the gap between shorter East Wing and South Hall
        float gapD = zSouthHallN - zEastEnd; // 14m
        CreateFloor(grpStructure, "Floor_EastGapWalkway",
            (xEastInner + xEastOuter) * 0.5f, (zEastEnd + zSouthHallN) * 0.5f,
            WING_W, gapD, floorTexPx, colCourt);

        // East side outer apron & strip around walkway gap and South Hall (prevents void outside East exit)
        CreateFloor(grpStructure, "Floor_EastGapApron",
            (xEastOuter + (xEastOuter + 10f)) * 0.5f, (zEastEnd + zSouthHallS) * 0.5f,
            10f, zSouthHallS - zEastEnd, floorTexPx, colCourt);

        CreateFloor(grpStructure, "Floor_EastSouthCornerStrip",
            (xSouthExtE + xEastOuter) * 0.5f, (zSouthHallN + zSouthHallS) * 0.5f,
            xEastOuter - xSouthExtE, SOUTH_D, floorTexPx, colCourt);

        // ── CEILING SLABS ────────────────────────────────────────────────
        CreateBoxWorld(grpStructure, "Ceiling_WestWing",
            new Vector3((xWestOuter + xWestInner) * 0.5f, H + 0.25f, (zBarS + zSouthHallN) * 0.5f),
            new Vector3(WING_W, 0.5f, WEST_WING_D),
            Mat("M_Conc_WingCeil_W", concTexPx, new Vector2(4f, 14.5f), colConc));

        CreateBoxWorld(grpStructure, "Ceiling_EastWing",
            new Vector3((xEastInner + xEastOuter) * 0.5f, H + 0.25f, (zBarS + zEastEnd) * 0.5f),
            new Vector3(WING_W, 0.5f, EAST_WING_D),
            Mat("M_Conc_WingCeil_E", concTexPx, new Vector2(4f, 11f), colConc));

        CreateBoxWorld(grpStructure, "Ceiling_CounterBar",
            new Vector3(0f, H + 0.25f, (zNorth + zBarS) * 0.5f),
            new Vector3(TOTAL_W, 0.5f, BAR_D),
            Mat("M_Conc_BarCeil", concTexPx, new Vector2(16f, 3f), colConc));

        // ── OUTER WALLS ──────────────────────────────────────────────────
        CreateBoxWorld(grpStructure, "Wall_North",
            new Vector3(0f, H * 0.5f, zNorth),
            new Vector3(TOTAL_W, H, 0.4f),
            Mat("M_Conc_NorthWall", concTexPx, new Vector2(16f, 1.5f), colConc));

        float westWallD = zSouthHallS - zNorth; // 82m
        CreateBoxWorld(grpStructure, "Wall_WestOuter",
            new Vector3(xWestOuter, H * 0.5f, (zNorth + zSouthHallS) * 0.5f),
            new Vector3(0.4f, H, westWallD),
            Mat("M_Conc_SideWall_W", concTexPx, new Vector2(20.5f, 1.5f), colConc));

        float eastWallD = zEastEnd - zNorth; // 56m
        CreateBoxWorld(grpStructure, "Wall_EastOuter",
            new Vector3(xEastOuter, H * 0.5f, (zNorth + zEastEnd) * 0.5f),
            new Vector3(0.4f, H, eastWallD),
            Mat("M_Conc_SideWall_E", concTexPx, new Vector2(14f, 1.5f), colConc));

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

        // Corner junction pillars (West corner also hosts the Ideal vertical banner)
        CreateBoxWorld(grpStructure, "Pillar_WestCorner",
            new Vector3(xWestInner, H * 0.5f, zBarS),
            new Vector3(pilSize, H, pilSize), matPillar);
        CreateBoxWorld(grpStructure, "Pillar_EastCorner",
            new Vector3(xEastInner, H * 0.5f, zBarS),
            new Vector3(pilSize, H, pilSize), matPillar);

        // Inner courtyard wing edges
        for (float pz = zBarS + 4f; pz <= zSouthHallN - 1f; pz += 8f)
        {
            CreateBoxWorld(grpStructure, $"Pillar_WestInner_{pz:F0}",
                new Vector3(xWestInner, H * 0.5f, pz),
                new Vector3(pilSize, H, pilSize), matPillar);
        }
        for (float pz = zBarS + 4f; pz <= zEastEnd - 1f; pz += 8f)
        {
            CreateBoxWorld(grpStructure, $"Pillar_EastInner_{pz:F0}",
                new Vector3(xEastInner, H * 0.5f, pz),
                new Vector3(pilSize, H, pilSize), matPillar);
        }

        // Wing South junction / end pillars
        CreateBoxWorld(grpStructure, "Pillar_WestJunction_1", new Vector3(xWestOuter + 5.3f, H * 0.5f, zSouthHallN), new Vector3(pilSize, H, pilSize), matPillar);
        CreateBoxWorld(grpStructure, "Pillar_WestJunction_2", new Vector3(xWestOuter + 10.6f, H * 0.5f, zSouthHallN), new Vector3(pilSize, H, pilSize), matPillar);
        CreateBoxWorld(grpStructure, "Pillar_EastSouth_1", new Vector3(xEastInner + 5.3f, H * 0.5f, zEastEnd), new Vector3(pilSize, H, pilSize), matPillar);
        CreateBoxWorld(grpStructure, "Pillar_EastSouth_2", new Vector3(xEastInner + 10.6f, H * 0.5f, zEastEnd), new Vector3(pilSize, H, pilSize), matPillar);

        // ── WHITE BHASKARA BUILDING ABOVE (NO WINDOWS) ───────────────────
        float bldgY = H + 0.5f + BLDG_H * 0.5f;
        float westBldgD = WEST_WING_D + SOUTH_D; // 70m (extends to South-West corner)

        CreateBoxWorld(grpStructure, "Building_NorthBlock",
            new Vector3(0f, bldgY, (zNorth + zBarS) * 0.5f),
            new Vector3(TOTAL_W + 0.8f, BLDG_H, BAR_D + 0.8f),
            Mat("M_White_North", wallTexPx, new Vector2(TOTAL_W / WALL_TEX_UNIT, BLDG_H / WALL_TEX_UNIT), colWhite));

        CreateBoxWorld(grpStructure, "Building_WestBlock",
            new Vector3((xWestOuter + xWestInner) * 0.5f, bldgY, (zBarS + zSouthHallS) * 0.5f),
            new Vector3(WING_W + 0.8f, BLDG_H, westBldgD + 0.8f),
            Mat("M_White_Wing_W", wallTexPx, new Vector2(westBldgD / WALL_TEX_UNIT, BLDG_H / WALL_TEX_UNIT), colWhite));

        CreateBoxWorld(grpStructure, "Building_EastBlock",
            new Vector3((xEastInner + xEastOuter) * 0.5f, bldgY, (zBarS + zEastEnd) * 0.5f),
            new Vector3(WING_W + 0.8f, BLDG_H, EAST_WING_D + 0.8f),
            Mat("M_White_Wing_E", wallTexPx, new Vector2(EAST_WING_D / WALL_TEX_UNIT, BLDG_H / WALL_TEX_UNIT), colWhite));

        // Roof cornices
        CreateBoxWorld(grpStructure, "Building_Cornice_North",
            new Vector3(0f, H + 0.5f + BLDG_H + 0.4f, (zNorth + zBarS) * 0.5f),
            new Vector3(TOTAL_W + 1.4f, 0.8f, BAR_D + 1.4f), matConcDefault);
        CreateBoxWorld(grpStructure, "Building_Cornice_West",
            new Vector3((xWestOuter + xWestInner) * 0.5f, H + 0.5f + BLDG_H + 0.4f, (zBarS + zSouthHallS) * 0.5f),
            new Vector3(WING_W + 1.4f, 0.8f, westBldgD + 1.4f), matConcDefault);
        CreateBoxWorld(grpStructure, "Building_Cornice_East",
            new Vector3((xEastInner + xEastOuter) * 0.5f, H + 0.5f + BLDG_H + 0.4f, (zBarS + zEastEnd) * 0.5f),
            new Vector3(WING_W + 1.4f, 0.8f, EAST_WING_D + 1.4f), matConcDefault);

        // ── SOUTH HALL ("This should be here" — empty inside, glass-door facade visible from outside) ─
        BuildSouthHall(grpSouthHall, grpLights, xWestOuter, xWestInner, xSouthExtE, zSouthHallN, zSouthHallS, H, SOUTH_BLDG_H, concTexPx, matLight, matConcDefault);

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

        // White water dispenser standing beside the East end of the serving counter
        BuildServingWaterDispenser(grpCounter, new Vector3(cntMaxX + 1.15f, 0f, counterZ + 1.1f), colWhite);

        // ── TUCK SHOP (North-West Corner - opposite side) & IDEAL POSTER ──
        BuildTuckShop.Build(root, xWestOuter, xWestInner, zNorth, zBarS, H);

        // ── DENSE CONGESTED SEATING (West Wing extends to South Hall; East Wing is shorter) ──
        BuildWingSeating(grpWings, "WestWing", xWestOuter + 0.75f, xWestInner - 0.75f, zBarS + 1.35f, zSouthHallN - 1.5f, isLeftWing: true);
        BuildWingSeating(grpWings, "EastWing", xEastInner + 0.75f, xEastOuter - 0.75f, zBarS + 1.35f, zEastEnd - 1.1f, isLeftWing: false);

        // ── INTERIOR CEILING LIGHTS ──────────────────────────────────────
        AddInteriorLights(grpLights, xWestOuter + 2f, xWestInner - 2f, zBarS + 4f, zSouthHallN - 4f, H - 0.2f, matLight);
        AddInteriorLights(grpLights, xEastInner + 2f, xEastOuter - 2f, zBarS + 4f, zEastEnd - 4f, H - 0.2f, matLight);
        AddCounterLights(grpLights, cntMinX + 2f, cntMaxX - 2f, counterZ + 1.5f, H - 0.2f, matLight);

        // ── POTTED PALM PLANTS & BLUE SINTEX DUSTBINS (middle courtyard kept clean!) ──
        for (float pz = zBarS + 4f; pz <= zSouthHallN - 5f; pz += 6f)
        {
            BuildProps.CreatePalmPlant(grpProps, new Vector3(xWestInner + 1.5f, 0f, pz), 0.95f);
        }
        for (float pz = zBarS + 4f; pz <= zEastEnd - 2f; pz += 6f)
        {
            BuildProps.CreatePalmPlant(grpProps, new Vector3(xEastInner - 1.5f, 0f, pz), 0.95f);
        }
        // Courtyard palms along the front (North / -Z) glass facade of South Hall (centered in front of closed glass bays)
        float[] southFrontPalmXs = { -7.0f, 5.5f, 18.6f };
        foreach (float px in southFrontPalmXs)
        {
            BuildProps.CreatePalmPlant(grpProps, new Vector3(px, 0f, zSouthHallN - 1.5f), 1.05f);
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
        // 4. South wing entrances (facing courtyard / walkway gap)
        BuildProps.CreateSintexDustbin(grpProps, new Vector3(xWestInner - 1.2f, 0f, zSouthHallN - 0.8f), 0f);
        BuildProps.CreateSintexDustbin(grpProps, new Vector3(xEastInner + 1.2f, 0f, zEastEnd - 0.6f), 0f);

        // ── COURTYARD DRAIN LINES (connected into South Hall's drain grate) ──
        BuildCourtyardDrainLines(grpStructure, xWestInner, xEastInner, zBarS, zEastEnd, zSouthHallN);

        // ── ENTRANCE STAIRS & LOWER BHASKARA PLAZA ───────────────────────
        BuildEntranceStairs.Build(root, xWestOuter, xEastInner, xEastOuter, xSouthExtE, zNorth, zEastEnd, zSouthHallS, concTexPx);

        // ── PROP TARGETS (for Prop Hunt morphing) ────────────────────────
        ApplyPropTargets(root);

        // ── ROTATE STRUCTURE (glass-door facade faces -Z / Front) ────────
        root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // ── PLAYER SPAWN (bottom in front of entrance stairs) ────────────
        var player = GameObject.Find("Player");
        if (player != null)
        {
            Undo.RecordObject(player.transform, "Move Player to Stairs Spawn");
            player.transform.position = BuildEntranceStairs.GetWorldSpawnPosition();
            player.transform.rotation = BuildEntranceStairs.GetWorldSpawnRotation();
        }

        // ── LIGHTING & SAVE ──────────────────────────────────────────────
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional)
            {
                l.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                l.intensity = 0.95f;
                l.color = new Color(1.0f, 0.98f, 0.92f);
            }
        }

        var curScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(curScene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[BuildCafeteria] Built asymmetric Pi/G-shaped cafeteria ({TOTAL_W}m x {TOTAL_D}m) with empty South Hall glass facade facing courtyard.");
    }

    static void BuildSouthHall(
        GameObject parent, GameObject lightsParent,
        float xWestOuter, float xWestInner, float xEastEnd,
        float zN, float zS, float groundH, float upperH,
        string concTexPx, Material matLight, Material matConcDefault)
    {
        float hallW   = xEastEnd - xWestOuter;      // 56m (-32..+24)
        float hallD   = zS - zN;                    // 12m (+36..+48)
        float hallCX  = (xWestOuter + xEastEnd) * 0.5f; // -4
        float hallCZ  = (zN + zS) * 0.5f;           // +42

        float spanW   = xEastEnd - xWestInner;      // 40m (-16..+24)
        float spanCX  = (xWestInner + xEastEnd) * 0.5f; // +4

        // Smooth interior concrete floor (clean, empty inside, subtle sheen like photo)
        var matInnerFloor = Mat("M_SouthHall_Floor", concTexPx, new Vector2(14f, 3f), new Color(0.76f, 0.75f, 0.72f));
        matInnerFloor.SetFloat("_Smoothness", 0.28f);
        CreateBoxWorld(parent, "Floor_SouthHall",
            new Vector3(hallCX, -0.1f, hallCZ),
            new Vector3(hallW, 0.2f, hallD), matInnerFloor);

        // Ceiling slab
        CreateBoxWorld(parent, "Ceiling_SouthHall",
            new Vector3(hallCX, groundH + 0.25f, hallCZ),
            new Vector3(hallW, 0.5f, hallD),
            Mat("M_Conc_SouthCeil", concTexPx, new Vector2(14f, 3f), new Color(0.66f, 0.65f, 0.62f)));

        // Interior white back wall at z = zS (solid across full South Hall width)
        var matWhiteInt = Mat("M_SouthHall_WhiteInt", null, Vector2.one, new Color(0.90f, 0.90f, 0.88f));
        matWhiteInt.SetFloat("_Smoothness", 0.12f);

        CreateBoxWorld(parent, "Wall_SouthHall_South",
            new Vector3(hallCX, groundH * 0.5f, zS),
            new Vector3(hallW, groundH, 0.35f), matWhiteInt);

        // East end wall (x = +24)
        CreateBoxWorld(parent, "Wall_SouthHall_East",
            new Vector3(xEastEnd, groundH * 0.5f, hallCZ),
            new Vector3(0.4f, groundH, hallD), matWhiteInt);

        // West end wall (x = -16, closing off the blue-chair West Wing side where the glass facade begins)
        CreateBoxWorld(parent, "Wall_SouthHall_West",
            new Vector3(xWestInner, groundH * 0.5f, hallCZ),
            new Vector3(0.4f, groundH, hallD), matWhiteInt);

        // Smooth light-grey upper concrete facade above the South Hall (y = 4.8..11.5)
        // Leaves West corner (-32..-16) owned by the taller 22m Bhaskara Building_WestBlock
        var matUpperConc = Mat("M_SouthHall_UpperConc", concTexPx, new Vector2(10f, 2f), new Color(0.76f, 0.77f, 0.78f));
        CreateBoxWorld(parent, "Building_SouthHallBlock",
            new Vector3(spanCX, groundH + 0.5f + upperH * 0.5f, hallCZ),
            new Vector3(spanW + 0.4f, upperH, hallD + 0.4f), matUpperConc);

        // Parapet coping rail along top of South Hall roof (South, North, and East exposed edges)
        float roofTopY = groundH + 0.5f + upperH;
        CreateBoxWorld(parent, "Building_SouthHall_Parapet_S",
            new Vector3(spanCX, roofTopY + 0.22f, zS + 0.05f),
            new Vector3(spanW + 0.6f, 0.45f, 0.45f), matConcDefault);
        CreateBoxWorld(parent, "Building_SouthHall_Parapet_N",
            new Vector3(spanCX, roofTopY + 0.22f, zN - 0.05f),
            new Vector3(spanW + 0.6f, 0.45f, 0.45f), matConcDefault);
        CreateBoxWorld(parent, "Building_SouthHall_Parapet_E",
            new Vector3(xEastEnd + 0.05f, roofTopY + 0.22f, hallCZ),
            new Vector3(0.45f, 0.45f, hallD + 0.6f), matConcDefault);

        // Concrete lintel header above the glass doors on the courtyard-facing North side (z = zN, x = -16..+24)
        float doorH = 3.85f;
        float lintelH = groundH - doorH;
        CreateBoxWorld(parent, "SouthFacade_Lintel",
            new Vector3(spanCX, doorH + lintelH * 0.5f, zN),
            new Vector3(spanW, lintelH, 0.42f), matUpperConc);

        // Drainage metal grate strip right outside the glass doors on the Middle Courtyard side (z = zN - 0.34)
        string grateTexPx = EnsureDrainGrateTexture("Assets/Textures/drain_grate_px.png", 32);
        CreateDrainGrateStrip(parent, "SouthFacade_DrainGrate",
            new Vector3(spanCX, 0.01f, zN - 0.34f), spanW, alongZ: false, grateTexPx);

        // Glass Door Facade along z = zN (x = -15.75 to +23.0, facing -Z forward into the Middle Courtyard)
        float glassStartX = xWestInner + 0.25f; // -15.75 (junction pillar sits at x = -16)
        float glassEndX   = xEastEnd - 0.25f;   // +23.75

        // Narrow solid pier closing the right-most meter before Wall_SouthHall_East
        float panelW = 1.25f;
        int totalPanels = Mathf.FloorToInt((glassEndX - glassStartX) / panelW); // 31 panels -> ends at +23.0
        float actualEnd = glassStartX + totalPanels * panelW;
        if (xEastEnd - actualEnd > 0.05f)
        {
            CreateBoxWorld(parent, "SouthFacade_EastCornerPier",
                new Vector3((actualEnd + xEastEnd) * 0.5f, doorH * 0.5f, zN),
                new Vector3(xEastEnd - actualEnd, doorH, 0.35f), matWhiteInt);
        }

        var matAlum   = Mat("M_DoorAlumFrame", null, Vector2.one, new Color(0.62f, 0.64f, 0.67f));
        matAlum.SetFloat("_Smoothness", 0.42f);
        var matHandle = Mat("M_DoorHandle", null, Vector2.one, new Color(0.10f, 0.10f, 0.12f));
        matHandle.SetFloat("_Smoothness", 0.3f);
        var matGlass  = TransparentMat("M_SouthDoorGlass", new Color(0.76f, 0.86f, 0.92f, 0.24f));

        // Every 10-panel bay matches the photo:
        // [0]=hinged open inward (+Z), [1..2]=wide open walkway gap, [3]=hinged open inward (+Z), [4..9]=closed glass panels
        for (int i = 0; i < totalPanels; i++)
        {
            float px = glassStartX + (i + 0.5f) * panelW;
            int mod = i % 10;

            if (mod != 2) // skip middle post inside the 2-panel wide open entrance gap
            {
                CreateBoxWorld(parent, $"Mullion_{i}",
                    new Vector3(px - panelW * 0.5f, doorH * 0.5f, zN),
                    new Vector3(0.08f, doorH, 0.12f), matAlum);
            }

            if (mod == 1 || mod == 2)
            {
                CreateBoxWorld(parent, $"DoorTopTrack_{i}",
                    new Vector3(px, doorH - 0.05f, zN),
                    new Vector3(panelW, 0.10f, 0.12f), matAlum);
                continue;
            }

            if (mod == 0 || mod == 3)
            {
                CreateBoxWorld(parent, $"DoorTopTrack_{i}",
                    new Vector3(px, doorH - 0.05f, zN),
                    new Vector3(panelW, 0.10f, 0.12f), matAlum);

                // Hinged door swung open inward (+Z into South Hall)
                bool hingeLeft = (mod == 0);
                float hingeX = hingeLeft ? (px - panelW * 0.5f + 0.04f) : (px + panelW * 0.5f - 0.04f);
                var pivot = new GameObject($"OpenGlassDoor_{i}");
                pivot.transform.SetParent(parent.transform, false);
                pivot.transform.position = new Vector3(hingeX, 0f, zN + 0.04f);
                pivot.transform.rotation = Quaternion.Euler(0f, hingeLeft ? -82f : 82f, 0f);

                float leafDir = hingeLeft ? 1f : -1f;
                BuildGlassLeaf(pivot, panelW - 0.08f, doorH - 0.10f, leafDir, matAlum, matGlass, matHandle);
            }
            else
            {
                var panelGo = new GameObject($"ClosedGlassDoor_{i}");
                panelGo.transform.SetParent(parent.transform, false);
                panelGo.transform.position = new Vector3(px - (panelW - 0.08f) * 0.5f, 0f, zN);
                BuildGlassLeaf(panelGo, panelW - 0.08f, doorH, 1f, matAlum, matGlass, matHandle, hasHandle: (mod % 2 == 0));
            }
        }
        CreateBoxWorld(parent, "Mullion_End",
            new Vector3(actualEnd, doorH * 0.5f, zN),
            new Vector3(0.08f, doorH, 0.12f), matAlum);

        // Populate interior lounge (bean bags, sofas, mural), Shape Up glass gym, study tables & carrom tables
        BuildSouthHallInterior.Build(parent, xWestInner, xEastEnd, zN, zS, groundH, concTexPx, matAlum, matGlass, matWhiteInt);

        // Interior ceiling lights inside the enclosed South Hall
        AddInteriorLights(lightsParent, xWestInner + 2.5f, xEastEnd - 2.5f, zN + 3.5f, zS - 3.5f, groundH - 0.2f, matLight);
    }

    static void ApplyPropTargets(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            string display = null;
            if (n.StartsWith("Chair") && n != "Chair_Shell" && n != "Chair_Legs") display = "Chair";
            else if (n.StartsWith("StdTable") || n.StartsWith("BigTable")) display = "Table";
            else if (n.StartsWith("StudyTable_")) display = "Study Table";
            else if (n.StartsWith("CarromTable_")) display = "Carrom Table";
            else if (n.StartsWith("BeanBag_")) display = "Bean Bag";
            else if (n.StartsWith("Lounge_Sofa_")) display = "Sofa";
            else if (n.StartsWith("Ottoman_")) display = "Ottoman";
            else if (n.StartsWith("FoosballTable_")) display = "Foosball Table";
            else if (n == "WaterDispenser_Serving") display = "Water Dispenser";
            else if (n.StartsWith("WaterCan_")) display = "Water Can";
            else if (n == "PalmPlant") display = "Potted Plant";
            else if (n.StartsWith("Dustbin") || n.StartsWith("SintexDustbin") || n.StartsWith("RedBin")) display = "Dustbin";
            else if (n.StartsWith("Packet_") || n.StartsWith("CounterPacket")) display = "Chips Packet";
            else if (n.StartsWith("IceCream_Freezer")) display = "Ice Cream Freezer";
            else if (n.StartsWith("SnackJar")) display = "Snack Jar";

            if (display != null)
            {
                var pt = t.GetComponent<PropTarget>() ?? t.gameObject.AddComponent<PropTarget>();
                pt.propDisplayName = display;
            }
        }
    }

    static void BuildGlassLeaf(GameObject pivot, float width, float height, float xDir, Material matFrame, Material matGlass, Material matHandle, bool hasHandle = true)
    {
        float cx = xDir * width * 0.5f;
        float frameT = 0.07f;

        // Top, bottom, and side aluminum rails
        CreateBoxLocal(pivot, "Rail_Top",    new Vector3(cx, height - frameT * 0.5f, 0f), new Vector3(width, frameT, 0.06f), matFrame);
        CreateBoxLocal(pivot, "Rail_Bottom", new Vector3(cx, frameT * 0.5f, 0f),          new Vector3(width, frameT, 0.06f), matFrame);
        CreateBoxLocal(pivot, "Stile_Hinge", new Vector3(xDir * frameT * 0.5f, height * 0.5f, 0f), new Vector3(frameT, height, 0.06f), matFrame);
        CreateBoxLocal(pivot, "Stile_Latch", new Vector3(xDir * (width - frameT * 0.5f), height * 0.5f, 0f), new Vector3(frameT, height, 0.06f), matFrame);

        // Clear glass pane (box collider blocks player cleanly on closed panels)
        CreateBoxLocal(pivot, "GlassPane",
            new Vector3(cx, height * 0.5f, 0f),
            new Vector3(width - frameT * 2f, height - frameT * 2f, 0.02f), matGlass);

        if (hasHandle)
        {
            float hx = xDir * (width - frameT * 1.1f);
            CreateBoxLocal(pivot, "Handle_Front", new Vector3(hx, 1.05f,  0.05f), new Vector3(0.04f, 0.16f, 0.04f), matHandle);
            CreateBoxLocal(pivot, "Handle_Back",  new Vector3(hx, 1.05f, -0.05f), new Vector3(0.04f, 0.16f, 0.04f), matHandle);
        }
    }

    static void BuildCourtyardDrainLines(
        GameObject parent,
        float xWestInner, float xEastInner,
        float zBarS, float zEastEnd, float zSouthHallN)
    {
        string grateTexPx = EnsureDrainGrateTexture("Assets/Textures/drain_grate_px.png", 32);
        const float grateW = 0.52f;
        const float offset = 0.72f; // clears 0.45m inner pillars without clipping

        float westX  = xWestInner + offset; // -15.28
        float eastX  = xEastInner - offset; // +15.28
        float northZ = zBarS + offset;      // -21.28

        // South Hall's drain grate occupies z = [zSouthHallN - 0.60 .. zSouthHallN - 0.08]
        float southDrainMinZ = zSouthHallN - 0.34f - grateW * 0.5f;
        float northDrainMinZ = northZ - grateW * 0.5f;

        // 1. West Wing drain line connecting South Hall's drain grate to North Counter Bar drain line
        float westLen = southDrainMinZ - northDrainMinZ;
        CreateDrainGrateStrip(parent, "DrainGrate_WestWing",
            new Vector3(westX, 0.01f, (northDrainMinZ + southDrainMinZ) * 0.5f),
            westLen, alongZ: true, grateTexPx);

        // 2. North Counter Bar drain line bridging West Wing and East Wing drain lines
        float northMinX = westX + grateW * 0.5f;
        float northMaxX = eastX - grateW * 0.5f;
        CreateDrainGrateStrip(parent, "DrainGrate_NorthBar",
            new Vector3((northMinX + northMaxX) * 0.5f, 0.01f, northZ),
            northMaxX - northMinX, alongZ: false, grateTexPx);

        // 3. East Wing drain line running from North corner along East Wing to its end (z = zEastEnd)
        float eastLen = zEastEnd - northDrainMinZ;
        CreateDrainGrateStrip(parent, "DrainGrate_EastWing",
            new Vector3(eastX, 0.01f, (northDrainMinZ + zEastEnd) * 0.5f),
            eastLen, alongZ: true, grateTexPx);
    }

    static void CreateDrainGrateStrip(
        GameObject parent, string name, Vector3 center, float length, bool alongZ, string grateTexPx)
    {
        var mat = Mat($"M_{name}", grateTexPx, new Vector2(length * 1.5f, 1f), new Color(0.75f, 0.77f, 0.80f));
        mat.SetFloat("_Smoothness", 0.22f);
        var strip = CreateBoxWorld(parent, name, center, new Vector3(length, 0.02f, 0.52f), mat);
        if (alongZ)
            strip.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
    }

    static string EnsureDrainGrateTexture(string dstAssetPath, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool border = (y <= 1 || y >= size - 2);
                bool slot = (x % 4 < 2) && !border;
                Color c = border
                    ? new Color(0.32f, 0.33f, 0.35f, 1f)
                    : (slot ? new Color(0.10f, 0.11f, 0.12f, 1f) : new Color(0.54f, 0.56f, 0.58f, 1f));
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        File.WriteAllBytes(dstAssetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(dstAssetPath, ImportAssetOptions.ForceUpdate);
        ConfigureRetroTextureImporter(dstAssetPath, size);
        return dstAssetPath;
    }

    static Material TransparentMat(string name, Color col)
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
        m.color = col;
        m.SetColor("_BaseColor", col);
        m.SetFloat("_Smoothness", 0.55f);
        m.SetFloat("_Surface", 1);
        m.SetInt("_SrcBlend", 5);
        m.SetInt("_DstBlend", 10);
        m.SetInt("_ZWrite", 0);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(m);
        s_MatCache[name] = m;
        return m;
    }

    static void CreateFloor(GameObject parent, string name, float cx, float cz, float w, float d, string texPath, Color col)
    {
        var tiling = new Vector2(w / FLOOR_TEX_UNIT, d / FLOOR_TEX_UNIT);
        var mat = Mat($"M_{name}", texPath, tiling, col);
        CreateBoxWorld(parent, name, new Vector3(cx, -0.1f, cz), new Vector3(w, 0.2f, d), mat);
    }

    static void BuildServingWaterDispenser(GameObject parent, Vector3 pos, Color colWhite)
    {
        var disp = CreateChild(parent, "WaterDispenser_Serving");
        disp.transform.position = pos;

        var matBody   = Mat("M_WaterDispWhite", null, Vector2.one, colWhite);
        matBody.SetFloat("_Smoothness", 0.28f);
        var matRecess = Mat("M_WaterDispRecess", null, Vector2.one, new Color(0.16f, 0.17f, 0.19f));
        var matCold   = Mat("M_WaterTapCold", null, Vector2.one, new Color(0.12f, 0.48f, 0.90f));
        var matHot    = Mat("M_WaterTapHot",  null, Vector2.one, new Color(0.85f, 0.16f, 0.14f));
        var matJug    = TransparentMat("M_WaterCanBlue", new Color(0.28f, 0.65f, 0.95f, 0.72f));

        // White cooler body (lower cabinet + upper sides + top deck)
        CreateBoxLocal(disp, "Body_Lower",  new Vector3(0f, 0.36f, 0f),     new Vector3(0.42f, 0.72f, 0.42f), matBody);
        CreateBoxLocal(disp, "Body_Top",    new Vector3(0f, 1.01f, 0f),     new Vector3(0.42f, 0.08f, 0.42f), matBody);
        CreateBoxLocal(disp, "Body_Back",   new Vector3(0f, 0.845f, -0.14f),new Vector3(0.42f, 0.25f, 0.14f), matBody);
        CreateBoxLocal(disp, "Body_SideL",  new Vector3(-0.18f, 0.845f, 0.07f), new Vector3(0.06f, 0.25f, 0.28f), matBody);
        CreateBoxLocal(disp, "Body_SideR",  new Vector3( 0.18f, 0.845f, 0.07f), new Vector3(0.06f, 0.25f, 0.28f), matBody);
        CreateBoxLocal(disp, "Drip_Tray",   new Vector3(0f, 0.73f, 0.11f),  new Vector3(0.30f, 0.03f, 0.20f), matRecess);
        CreateBoxLocal(disp, "Tap_Cold",    new Vector3(-0.07f, 0.91f, 0.10f), new Vector3(0.04f, 0.07f, 0.05f), matCold);
        CreateBoxLocal(disp, "Tap_Hot",     new Vector3( 0.07f, 0.91f, 0.10f), new Vector3(0.04f, 0.07f, 0.05f), matHot);

        var jug = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        jug.name = "WaterBottle_Top";
        jug.transform.SetParent(disp.transform, false);
        jug.transform.localPosition = new Vector3(0f, 1.29f, 0f);
        jug.transform.localScale = new Vector3(0.28f, 0.24f, 0.28f);
        jug.GetComponent<Renderer>().sharedMaterial = matJug;
    }

    static void BuildWingSeating(GameObject parent, string wingName, float minX, float maxX, float minZ, float maxZ, bool isLeftWing)
    {
        var wingGroup = CreateChild(parent, wingName);
        var rng = new System.Random(isLeftWing ? 4041 : 8082);

        float midX = (minX + maxX) * 0.5f;
        float splitZ = Mathf.Lerp(minZ, maxZ, 0.44f); // North 44% = Big Long Rectangular Tables; South 56% = Standard Tables

        // 1. Long Big Rectangular Tables (2.9m x 0.82m) — chairs placed with natural randomness
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
                Vector3 jitter = new Vector3((float)(rng.NextDouble() * 0.14 - 0.07), 0f, (float)(rng.NextDouble() * 0.14 - 0.07));
                BuildProps.CreateMessTable(wingGroup, $"BigTable_{r}_{c}",
                    new Vector3(longColsX[c], 0f, tz) + jitter, longW, longD, isLeftWing, rng);
            }
        }

        // 2. Standard 4-Seater Tables (1.25m x 0.78m) — chairs placed with natural randomness
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
                Vector3 jitter = new Vector3((float)(rng.NextDouble() * 0.14 - 0.07), 0f, (float)(rng.NextDouble() * 0.14 - 0.07));
                BuildProps.CreateMessTable(wingGroup, $"StdTable_{r}_{c}",
                    new Vector3(tx, 0f, tz) + jitter, stdW, stdD, isLeftWing, rng);
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
