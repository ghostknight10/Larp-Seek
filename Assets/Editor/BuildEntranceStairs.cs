using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Builds the lower Bhaskara Block entrance road, brick landing, stone stairs with
/// vertical-bar railings on both sides, terrace retaining skirt walls & railings,
/// warm step lights, realistic multi-leaf potted tree in a wide bowl planter,
/// and blue BHASKARA BLOCK directory signboard on the -X world (+X local) side.
/// All positions are authored in IITB_Cafeteria local space before 180-deg root rotation.
/// </summary>
public static class BuildEntranceStairs
{
    const float LOWER_LANDING_Y = -3.20f;
    const float LOWER_ROAD_Y    = -3.26f;
    const float UPPER_Y         =  0.00f;

    // +X local = -X world after 180-deg Y rotation of IITB_Cafeteria
    const float STAIR_TOP_X     =  42.0f; // flush with Floor_EastGapApron outer X edge (world -42)
    const float STAIR_RUN       =   6.40f; // 16 steps * 0.40m
    const float STAIR_BOT_X     = STAIR_TOP_X + STAIR_RUN; // +48.4f (world -48.4)
    const float STAIR_MIN_Z     =  37.5f; // world -37.5 (Building_EastBlock / courtyard side)
    const float STAIR_MAX_Z     =  45.5f; // world -45.5 (South_Hall outer corner side)
    const float STAIR_CZ        = (STAIR_MIN_Z + STAIR_MAX_Z) * 0.5f; // 41.5f
    const float STAIR_W         = STAIR_MAX_Z - STAIR_MIN_Z;          //  8.0f

    /// <summary>
    /// World-space spawn point for Player in front of the stairs at the bottom,
    /// facing +X (toward the stairs, signboard, potted tree, and upper mess).
    /// </summary>
    public static Vector3 GetWorldSpawnPosition() => new Vector3(-56.5f, LOWER_ROAD_Y + 0.02f, -40.5f);
    public static Quaternion GetWorldSpawnRotation() => Quaternion.Euler(0f, 90f, 0f);

    public static void Build(
        GameObject root,
        float xWestOuter, float xEastInner, float xEastOuter, float xSouthExtE,
        float zNorth, float zEastEnd, float zSouthHallS,
        string concTexPx)
    {
        var grp = new GameObject("Entrance_Stairs");
        grp.transform.SetParent(root.transform, false);

        string brickTexPx = EnsureBrickPaverTexture("Assets/Textures/paver_brick_px.png", 32);
        string roadTexPx  = EnsureAsphaltTexture("Assets/Textures/road_asphalt_px.png", 32);
        string signTexPx  = EnsureBhaskaraSignTexture("Assets/Textures/sign_bhaskara_px.png");

        var matStep      = MakeMat("M_StairStep",    concTexPx,  new Vector2(8f, 1f),   new Color(0.64f, 0.62f, 0.59f), 0.04f);
        var matCheek     = MakeMat("M_StairCheek",   concTexPx,  new Vector2(4f, 2f),   new Color(0.54f, 0.53f, 0.51f), 0.04f);
        var matSkirt     = MakeMat("M_TerraceSkirt", concTexPx,  new Vector2(12f, 2f),  new Color(0.50f, 0.49f, 0.47f), 0.04f);
        var matParapet   = MakeMat("M_ParapetWhite", null,       Vector2.one,           new Color(0.84f, 0.84f, 0.82f), 0.06f);
        var matRail      = MakeMat("M_RailingMetal", null,       Vector2.one,           new Color(0.24f, 0.26f, 0.29f), 0.35f);
        var matBrick     = MakeMat("M_BrickPaver",   brickTexPx, new Vector2(4f, 6f),   new Color(0.80f, 0.77f, 0.75f), 0.04f);
        var matRoad      = MakeMat("M_LowerRoad",    roadTexPx,  new Vector2(12f, 14f), new Color(0.70f, 0.70f, 0.72f), 0.03f);
        var matCurb      = MakeMat("M_StoneCurb",    concTexPx,  new Vector2(8f, 1f),   new Color(0.64f, 0.61f, 0.57f), 0.10f);
        var matMat       = MakeMat("M_DoorMat",      null,       Vector2.one,           new Color(0.16f, 0.17f, 0.18f), 0.05f);
        var matSign      = MakeMat("M_BhaskaraSign", signTexPx,  Vector2.one,           Color.white, 0.15f);
        var matPost      = MakeMat("M_SignPost",     null,       Vector2.one,           new Color(0.14f, 0.15f, 0.17f), 0.30f);
        var matStepLight = MakeEmissiveMat("M_StairLightGlow", new Color(1.0f, 0.88f, 0.62f));

        // 1. Stone Steps (16 steps rising in -X local: x = +48.4 -> +42.0, y = -3.2 -> 0.0)
        int steps = 16;
        float stepRise = (UPPER_Y - LOWER_LANDING_Y) / steps; // 0.20m
        float stepRun  = STAIR_RUN / steps;                   // 0.40m
        for (int i = 0; i < steps; i++)
        {
            float sx = STAIR_BOT_X - (i + 0.5f) * stepRun;
            float topY = LOWER_LANDING_Y + (i + 1) * stepRise;
            float h = topY - LOWER_LANDING_Y;
            var step = Box(grp, $"Stair_Step_{i}",
                new Vector3(sx, LOWER_LANDING_Y + h * 0.5f, STAIR_CZ),
                new Vector3(stepRun + 0.02f, h, STAIR_W), matStep);
            Object.DestroyImmediate(step.GetComponent<Collider>());
        }

        // Smooth invisible ramp collider matching exact nosing plane for butter-smooth CharacterController climb
        float len = Mathf.Sqrt(STAIR_RUN * STAIR_RUN + (UPPER_Y - LOWER_LANDING_Y) * (UPPER_Y - LOWER_LANDING_Y));
        float theta = Mathf.Atan2(UPPER_Y - LOWER_LANDING_Y, STAIR_RUN) * Mathf.Rad2Deg;
        float rampT = 0.24f;
        Quaternion rampRot = Quaternion.Euler(0f, 0f, -theta);
        Vector3 rampMid = new Vector3((STAIR_BOT_X + STAIR_TOP_X) * 0.5f, (LOWER_LANDING_Y + UPPER_Y) * 0.5f, STAIR_CZ);
        Vector3 rampCenter = rampMid - (rampRot * Vector3.up) * (rampT * 0.5f);

        var rampGo = new GameObject("Stairs_RampCollider");
        rampGo.transform.SetParent(grp.transform, false);
        rampGo.transform.localPosition = rampCenter;
        rampGo.transform.localRotation = rampRot;
        var rampBox = rampGo.AddComponent<BoxCollider>();
        rampBox.size = new Vector3(len, rampT, STAIR_W);

        // 2. Stair Side Cheek Walls + Dense Vertical-Bar Railings + Warm Step Lights
        BuildStairCheekAndRailing(grp, "StairSide_Left",  STAIR_MIN_Z - 0.22f, matCheek, matRail, matStepLight);
        BuildStairCheekAndRailing(grp, "StairSide_Right", STAIR_MAX_Z + 0.22f, matCheek, matRail, matStepLight);

        // 3. Terrace Retaining Skirt Walls under elevated upper platform (y = -3.45 .. 0.0)
        float skirtH = 3.45f;
        float skirtCY = -skirtH * 0.5f;
        float apronMaxX = STAIR_TOP_X; // +42

        // Outer +X local edge (x = +42) flanking the stairs
        Box(grp, "Skirt_Apron_N", new Vector3(apronMaxX - 0.2f, skirtCY, (zEastEnd + STAIR_MIN_Z) * 0.5f),
            new Vector3(0.4f, skirtH, STAIR_MIN_Z - zEastEnd), matSkirt);
        Box(grp, "Skirt_Apron_S", new Vector3(apronMaxX - 0.2f, skirtCY, (STAIR_MAX_Z + zSouthHallS) * 0.5f),
            new Vector3(0.4f, skirtH, zSouthHallS - STAIR_MAX_Z), matSkirt);

        // Back edge of Floor_EastGapApron (z = zEastEnd = +22, x = +32..+42)
        Box(grp, "Skirt_Apron_Back", new Vector3((xEastOuter + apronMaxX) * 0.5f, skirtCY, zEastEnd + 0.2f),
            new Vector3(apronMaxX - xEastOuter, skirtH, 0.4f), matSkirt);

        // Under Wall_EastOuter (x = +32, z = -34..+22) so tall hostel wing sits solidly on lower plaza
        Box(grp, "Skirt_EastWall", new Vector3(xEastOuter - 0.2f, skirtCY, (zNorth + zEastEnd) * 0.5f),
            new Vector3(0.4f, skirtH, zEastEnd - zNorth), matSkirt);

        // South edge (z = +48, x = -32..+42) under South Hall & Floor_EastSouthCornerStrip
        Box(grp, "Skirt_SouthEdge", new Vector3((xWestOuter + apronMaxX) * 0.5f, skirtCY, zSouthHallS - 0.2f),
            new Vector3(apronMaxX - xWestOuter, skirtH, 0.4f), matSkirt);

        // West outer wall skirt (x = -32, z = -34..+48)
        Box(grp, "Skirt_WestWall", new Vector3(xWestOuter + 0.2f, skirtCY, (zNorth + zSouthHallS) * 0.5f),
            new Vector3(0.4f, skirtH, zSouthHallS - zNorth), matSkirt);

        // 4. Upper Terrace Parapet + Dense Vertical-Bar Metal Railings
        BuildParapetWithRailing(grp, "Railing_Apron_N",
            new Vector3(apronMaxX - 0.18f, 0f, zEastEnd + 0.18f),
            new Vector3(apronMaxX - 0.18f, 0f, STAIR_MIN_Z - 0.22f),
            matParapet, matRail);
        BuildParapetWithRailing(grp, "Railing_Apron_S",
            new Vector3(apronMaxX - 0.18f, 0f, STAIR_MAX_Z + 0.22f),
            new Vector3(apronMaxX - 0.18f, 0f, zSouthHallS - 0.18f),
            matParapet, matRail);
        BuildParapetWithRailing(grp, "Railing_Apron_Back",
            new Vector3(xEastOuter + 0.20f, 0f, zEastEnd + 0.18f),
            new Vector3(apronMaxX - 0.18f, 0f, zEastEnd + 0.18f),
            matParapet, matRail);
        BuildParapetWithRailing(grp, "Railing_SouthStrip",
            new Vector3(xSouthExtE, 0f, zSouthHallS - 0.18f),
            new Vector3(apronMaxX - 0.18f, 0f, zSouthHallS - 0.18f),
            matParapet, matRail);

        // 5. Lower Road, Raised Brick Landing, Stone Curb & Entrance Mat
        Box(grp, "Lower_Road",
            new Vector3(58f, LOWER_ROAD_Y - 0.15f, 38f),
            new Vector3(44f, 0.30f, 52f), matRoad);

        // Brick-paved sidewalk landing at foot of stairs (x = +42.0 .. +53.8, z = 31.5 .. 47.0)
        float landMinX = STAIR_TOP_X;
        float landMaxX = 53.8f;
        float landMinZ = 31.5f;
        float landMaxZ = 47.0f;
        Box(grp, "Lower_BrickLanding",
            new Vector3((landMinX + landMaxX) * 0.5f, LOWER_LANDING_Y - 0.12f, (landMinZ + landMaxZ) * 0.5f),
            new Vector3(landMaxX - landMinX, 0.24f, landMaxZ - landMinZ), matBrick);

        // Stone curb edging along front & side of the brick landing
        Box(grp, "Lower_Curb_Front",
            new Vector3(landMaxX - 0.15f, LOWER_LANDING_Y - 0.08f, (landMinZ + landMaxZ) * 0.5f),
            new Vector3(0.30f, 0.20f, landMaxZ - landMinZ + 0.2f), matCurb);
        Box(grp, "Lower_Curb_SideN",
            new Vector3((landMinX + landMaxX) * 0.5f, LOWER_LANDING_Y - 0.08f, landMinZ + 0.15f),
            new Vector3(landMaxX - landMinX, 0.20f, 0.30f), matCurb);
        Box(grp, "Lower_Curb_SideS",
            new Vector3((landMinX + landMaxX) * 0.5f, LOWER_LANDING_Y - 0.08f, landMaxZ - 0.15f),
            new Vector3(landMaxX - landMinX, 0.20f, 0.30f), matCurb);

        // Dark entrance mat at the foot of the stairs (matching photo)
        Box(grp, "Lower_EntranceMat",
            new Vector3(STAIR_BOT_X + 0.75f, LOWER_LANDING_Y + 0.01f, STAIR_CZ),
            new Vector3(1.30f, 0.02f, 5.4f), matMat);

        // 6. Blue BHASKARA BLOCK Directory Signboard (on the brick landing to the left of the stairs)
        BuildDirectorySign(grp, new Vector3(STAIR_BOT_X + 1.4f, LOWER_LANDING_Y, STAIR_MIN_Z - 1.8f), matPost, matSign);

        // 7. Realistic Leafy Potted Bush/Tree in a wide shallow bowl pot beside the stairs & signboard
        BuildRealisticPottedTree(grp, new Vector3(STAIR_BOT_X - 1.8f, LOWER_LANDING_Y, STAIR_MIN_Z - 2.6f));
    }

    static void BuildStairCheekAndRailing(
        GameObject parent, string name, float zPos,
        Material matCheek, Material matRail, Material matGlow)
    {
        // Solid concrete cheek block stepped cleanly along the stairs
        int segs = 8;
        float dx = STAIR_RUN / segs;
        float dy = (UPPER_Y - LOWER_LANDING_Y) / segs;
        for (int s = 0; s < segs; s++)
        {
            float sx = STAIR_BOT_X - (s + 0.5f) * dx;
            float topY = LOWER_LANDING_Y + (s + 1) * dy + 0.36f;
            float h = topY - LOWER_LANDING_Y;
            Box(parent, $"{name}_Seg{s}",
                new Vector3(sx, LOWER_LANDING_Y + h * 0.5f, zPos),
                new Vector3(dx + 0.02f, h, 0.44f), matCheek);
        }

        // Dense vertical-bar metal railing sloped along the cheek wall (matching campus railing style)
        float len = Mathf.Sqrt(STAIR_RUN * STAIR_RUN + (UPPER_Y - LOWER_LANDING_Y) * (UPPER_Y - LOWER_LANDING_Y));
        float theta = Mathf.Atan2(UPPER_Y - LOWER_LANDING_Y, STAIR_RUN) * Mathf.Rad2Deg;
        Quaternion slopeRot = Quaternion.Euler(0f, 0f, -theta);

        float midX = (STAIR_BOT_X + STAIR_TOP_X) * 0.5f;
        float midY = (LOWER_LANDING_Y + UPPER_Y) * 0.5f;

        var topBar = Box(parent, $"{name}_TopBar",
            new Vector3(midX, midY + 1.12f, zPos),
            new Vector3(len + 0.20f, 0.055f, 0.065f), matRail);
        topBar.transform.localRotation = slopeRot;

        var botBar = Box(parent, $"{name}_BotBar",
            new Vector3(midX, midY + 0.42f, zPos),
            new Vector3(len + 0.16f, 0.045f, 0.055f), matRail);
        botBar.transform.localRotation = slopeRot;

        int barCount = 29; // ~0.22m spacing along the 6.4m stair run
        for (int i = 0; i < barCount; i++)
        {
            float t = i / (float)(barCount - 1);
            float bx = Mathf.Lerp(STAIR_BOT_X - 0.06f, STAIR_TOP_X + 0.06f, t);
            float by = Mathf.Lerp(LOWER_LANDING_Y, UPPER_Y, t);
            bool isMainPost = (i % 7 == 0) || (i == barCount - 1);
            float thick = isMainPost ? 0.05f : 0.024f;
            float baseH = isMainPost ? 0.34f : 0.42f;
            float topH = 1.12f;
            Box(parent, $"{name}_Baluster_{i}",
                new Vector3(bx, by + (baseH + topH) * 0.5f, zPos),
                new Vector3(thick, topH - baseH, thick), matRail);
        }

        // Warm recessed step lights facing inward onto the steps
        for (int l = 0; l < 3; l++)
        {
            float t = (l + 0.5f) / 3f;
            float lx = Mathf.Lerp(STAIR_BOT_X, STAIR_TOP_X, t);
            float ly = Mathf.Lerp(LOWER_LANDING_Y, UPPER_Y, t) + 0.24f;
            float inwardOffset = zPos < STAIR_CZ ? 0.23f : -0.23f;
            Box(parent, $"{name}_Glow_{l}",
                new Vector3(lx, ly, zPos + inwardOffset),
                new Vector3(0.22f, 0.09f, 0.04f), matGlow);

            if (l == 1)
            {
                var lGo = new GameObject($"{name}_Light");
                lGo.transform.SetParent(parent.transform, false);
                lGo.transform.localPosition = new Vector3(lx, ly + 0.45f, zPos + inwardOffset * 3.0f);
                var pt = lGo.AddComponent<Light>();
                pt.type = LightType.Point;
                pt.range = 4.5f;
                pt.intensity = 0.22f;
                pt.color = new Color(1.0f, 0.88f, 0.65f);
            }
        }
    }

    static void BuildParapetWithRailing(
        GameObject parent, string name, Vector3 p0, Vector3 p1,
        Material matParapet, Material matRail)
    {
        Vector3 mid = (p0 + p1) * 0.5f;
        Vector3 delta = p1 - p0;
        float len = delta.magnitude;
        if (len < 0.2f) return;

        bool alongZ = Mathf.Abs(delta.z) > Mathf.Abs(delta.x);
        float wallH = 0.38f;
        Vector3 wallSize = alongZ ? new Vector3(0.26f, wallH, len) : new Vector3(len, wallH, 0.26f);
        Box(parent, $"{name}_Wall", new Vector3(mid.x, wallH * 0.5f, mid.z), wallSize, matParapet);

        float botY = 0.44f;
        float railY = 1.08f;
        Vector3 topBarSize = alongZ ? new Vector3(0.06f, 0.05f, len) : new Vector3(len, 0.05f, 0.06f);
        Vector3 botBarSize = alongZ ? new Vector3(0.045f, 0.04f, len) : new Vector3(len, 0.04f, 0.045f);
        Box(parent, $"{name}_TopBar", new Vector3(mid.x, railY, mid.z), topBarSize, matRail);
        Box(parent, $"{name}_BotBar", new Vector3(mid.x, botY, mid.z),  botBarSize, matRail);

        // Dense vertical railing bars (~0.22m spacing, matching photo background)
        int bars = Mathf.Max(3, Mathf.RoundToInt(len / 0.22f) + 1);
        for (int i = 0; i < bars; i++)
        {
            Vector3 pos = Vector3.Lerp(p0, p1, i / (float)(bars - 1));
            bool isMainPost = (i % 8 == 0) || (i == bars - 1);
            float thick = isMainPost ? 0.05f : 0.024f;
            float baseH = isMainPost ? wallH : botY;
            Box(parent, $"{name}_Bar{i}",
                new Vector3(pos.x, (baseH + railY) * 0.5f, pos.z),
                new Vector3(thick, railY - baseH, thick), matRail);
        }
    }

    static void BuildDirectorySign(GameObject parent, Vector3 basePos, Material matPost, Material matSign)
    {
        var sign = new GameObject("Signboard_BhaskaraBlock");
        sign.transform.SetParent(parent.transform, false);
        sign.transform.localPosition = basePos;

        float w = 1.85f;
        float boardH = 2.45f;
        float legH = 0.45f;

        Box(sign, "Post_L", new Vector3(0f, (legH + boardH) * 0.5f, -w * 0.5f + 0.06f), new Vector3(0.08f, legH + boardH, 0.08f), matPost);
        Box(sign, "Post_R", new Vector3(0f, (legH + boardH) * 0.5f,  w * 0.5f - 0.06f), new Vector3(0.08f, legH + boardH, 0.08f), matPost);
        Box(sign, "Board",  new Vector3(0f, legH + boardH * 0.5f, 0f), new Vector3(0.06f, boardH, w), matPost);

        // Front-facing textured face (facing +X local, toward the spawning player at x = +56.5)
        var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
        face.name = "SignFace_Front";
        face.transform.SetParent(sign.transform, false);
        face.transform.localPosition = new Vector3(0.035f, legH + boardH * 0.5f, 0f);
        face.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        face.transform.localScale = new Vector3(w - 0.04f, boardH - 0.04f, 1f);
        face.GetComponent<Renderer>().sharedMaterial = matSign;
        Object.DestroyImmediate(face.GetComponent<Collider>());
    }

    // ════════════════════════════════════════════════════════════════════
    // REALISTIC POTTED BUSHY TREE IN WIDE SHALLOW BOWL PLANTER (photo match)
    // ════════════════════════════════════════════════════════════════════

    static void BuildRealisticPottedTree(GameObject parent, Vector3 basePos)
    {
        var tree = new GameObject("Entrance_TreePlanter");
        tree.transform.SetParent(parent.transform, false);
        tree.transform.localPosition = basePos;

        var matBowl     = MakeMat("M_TreeBowlPot",  null, Vector2.one, new Color(0.24f, 0.30f, 0.25f), 0.12f);
        var matSoil     = MakeMat("M_TreeSoil",     null, Vector2.one, new Color(0.16f, 0.12f, 0.09f), 0.02f);
        var matBark     = MakeMat("M_TreeBark",     null, Vector2.one, new Color(0.24f, 0.17f, 0.12f), 0.04f);
        var matLeafDark = MakeMat("M_TreeLeafDark", null, Vector2.one, new Color(0.09f, 0.24f, 0.09f), 0.02f);
        var matLeafMid  = MakeMat("M_TreeLeafMid",  null, Vector2.one, new Color(0.16f, 0.38f, 0.13f), 0.02f);
        var matLeafLite = MakeMat("M_TreeLeafLite", null, Vector2.one, new Color(0.26f, 0.48f, 0.17f), 0.02f);

        // 1. Wide tapered shallow bowl planter pot + rolled rim + dark soil disk
        var potParts = new List<CombineInstance>();
        potParts.Add(new CombineInstance { mesh = BuildFrustumMesh("BowlBody", 0.00f, 0.52f, 0.56f, 0.92f, 18, true), transform = Matrix4x4.identity });
        potParts.Add(new CombineInstance { mesh = BuildFrustumMesh("BowlRim",  0.50f, 0.60f, 0.90f, 0.98f, 18, true), transform = Matrix4x4.identity });
        Mesh potMesh = SaveMeshAsset(CombineMeshes("EntranceBowlPotMesh", potParts));

        var soilMesh = SaveMeshAsset(BuildFrustumMesh("EntranceBowlSoilMesh", 0.48f, 0.55f, 0.84f, 0.86f, 16, true));

        // 2. Multi-stemmed woody branches + dense layered organic leaf sprays
        var branchParts = new List<CombineInstance>();
        var darkLeaves  = new List<CombineInstance>();
        var midLeaves   = new List<CombineInstance>();
        var liteLeaves  = new List<CombineInstance>();

        var rng = new System.Random(7331);
        int mainBranches = 9;
        for (int b = 0; b < mainBranches; b++)
        {
            float yaw = (b / (float)mainBranches) * 360f + (float)(rng.NextDouble() * 24f - 12f);
            float tilt = 16f + (float)rng.NextDouble() * 28f;
            float branchLen = 1.35f + (float)rng.NextDouble() * 1.15f;
            Vector3 start = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.12f, 0.52f, 0f);
            Vector3 dir   = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -tilt) * Vector3.up;
            Vector3 mid   = start + dir * (branchLen * 0.55f);
            Vector3 tip   = mid + (dir + new Vector3((float)(rng.NextDouble() - 0.5) * 0.35f, 0.25f, (float)(rng.NextDouble() - 0.5) * 0.35f)).normalized * (branchLen * 0.48f);

            branchParts.Add(MakeBranchSegment(start, mid, 0.055f, 0.036f));
            branchParts.Add(MakeBranchSegment(mid,   tip, 0.036f, 0.018f));

            // Sub-twigs radiating from mid and tip
            int twigs = 4;
            for (int tw = 0; tw < twigs; tw++)
            {
                Vector3 anchor = Vector3.Lerp(mid, tip, (tw + 0.3f) / twigs);
                float subYaw = yaw + (tw - 1.5f) * 42f + (float)(rng.NextDouble() * 18f - 9f);
                float subPitch = 20f + (float)rng.NextDouble() * 40f;
                Vector3 subDir = Quaternion.Euler(subPitch, subYaw, 0f) * Vector3.forward;
                float subLen = 0.55f + (float)rng.NextDouble() * 0.55f;
                Vector3 subTip = anchor + subDir * subLen;
                branchParts.Add(MakeBranchSegment(anchor, subTip, 0.018f, 0.008f));

                // Leaf sprays along each twig
                AddLeafSpray(darkLeaves, midLeaves, liteLeaves, anchor, subTip, rng);
            }

            AddLeafSpray(darkLeaves, midLeaves, liteLeaves, mid, tip, rng);
        }

        // Low outer overhanging sprays around rim (gives bushy photo silhouette)
        for (int s = 0; s < 14; s++)
        {
            float ang = (s / 14f) * 360f + (float)(rng.NextDouble() * 18f);
            Vector3 p0 = Quaternion.Euler(0f, ang, 0f) * new Vector3(0.45f, 0.65f, 0f);
            Vector3 p1 = Quaternion.Euler(0f, ang, 0f) * new Vector3(1.15f + (float)rng.NextDouble() * 0.45f, 0.95f + (float)rng.NextDouble() * 0.55f, 0f);
            branchParts.Add(MakeBranchSegment(p0, p1, 0.020f, 0.008f));
            AddLeafSpray(darkLeaves, midLeaves, liteLeaves, p0, p1, rng);
        }

        Mesh stemsMesh    = SaveMeshAsset(CombineMeshes("EntranceTreeStemsMesh",     branchParts));
        Mesh leafDarkMesh = SaveMeshAsset(CombineMeshes("EntranceTreeLeafDarkMesh",  darkLeaves));
        Mesh leafMidMesh  = SaveMeshAsset(CombineMeshes("EntranceTreeLeafMidMesh",   midLeaves));
        Mesh leafLiteMesh = SaveMeshAsset(CombineMeshes("EntranceTreeLeafLiteMesh",  liteLeaves));

        AddMeshPart(tree, "BowlPot",     potMesh,      matBowl,     true);
        AddMeshPart(tree, "BowlSoil",    soilMesh,     matSoil,     false);
        AddMeshPart(tree, "Stems",       stemsMesh,    matBark,     false);
        AddMeshPart(tree, "Leaves_Dark", leafDarkMesh, matLeafDark, false);
        AddMeshPart(tree, "Leaves_Mid",  leafMidMesh,  matLeafMid,  false);
        AddMeshPart(tree, "Leaves_Lite", leafLiteMesh, matLeafLite, false);
    }

    static CombineInstance BuildUVBoxInstance(Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh m = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
        Object.DestroyImmediate(go);
        return new CombineInstance { mesh = m, transform = Matrix4x4.TRS(pos, Quaternion.identity, scale) };
    }

    static void AddLeafSpray(
        List<CombineInstance> dark, List<CombineInstance> mid, List<CombineInstance> lite,
        Vector3 p0, Vector3 p1, System.Random rng)
    {
        // Chunky blocky foliage clusters with flat shading (no thin shimmering cards)
        int clusters = 4;
        for (int c = 0; c < clusters; c++)
        {
            float t = (c + 0.35f) / clusters;
            Vector3 center = Vector3.Lerp(p0, p1, t) + new Vector3(
                (float)(rng.NextDouble() * 0.16 - 0.08),
                (float)(rng.NextDouble() * 0.14 - 0.06),
                (float)(rng.NextDouble() * 0.16 - 0.08));

            float boxSizeX = 0.22f + (float)rng.NextDouble() * 0.12f;
            float boxSizeY = 0.14f + (float)rng.NextDouble() * 0.08f;
            float boxSizeZ = 0.22f + (float)rng.NextDouble() * 0.12f;
            var ci = BuildUVBoxInstance(center, new Vector3(boxSizeX, boxSizeY, boxSizeZ));

            bool isHighOuter = center.y > 1.6f || t > 0.65f;
            double pick = rng.NextDouble();
            if (isHighOuter && pick > 0.40)      lite.Add(ci);
            else if (center.y < 1.15f && pick < 0.50) dark.Add(ci);
            else if (pick < 0.30)                dark.Add(ci);
            else                                 mid.Add(ci);
        }
    }

    static Mesh s_CachedLeafCard;
    static Mesh BuildOvateLeafMesh()
    {
        if (s_CachedLeafCard != null) return s_CachedLeafCard;

        // Double-sided slightly cupped ovate leaf blade (~0.34m long, 0.16m wide).
        // Top and bottom sides use separate vertices with slight thickness offset so
        // RecalculateNormals() produces clean opposite normals (never zero-length NaN normals).
        var m = new Mesh { name = "OvateLeafCard" };
        Vector3[] baseVerts = {
            new Vector3( 0.00f,  0.00f, 0.00f), // 0 base
            new Vector3(-0.080f, 0.025f, 0.12f), // 1 mid-left
            new Vector3( 0.080f, 0.025f, 0.12f), // 2 mid-right
            new Vector3(-0.052f, 0.015f, 0.25f), // 3 upper-left
            new Vector3( 0.052f, 0.015f, 0.25f), // 4 upper-right
            new Vector3( 0.00f, -0.025f, 0.34f), // 5 tip (droops slightly)
        };

        var verts = new Vector3[12];
        for (int i = 0; i < 6; i++)
        {
            verts[i]     = baseVerts[i] + new Vector3(0f,  0.002f, 0f);
            verts[i + 6] = baseVerts[i] + new Vector3(0f, -0.002f, 0f);
        }

        int[] tris = {
            // Top face (verts 0..5)
            0, 1, 2,  1, 3, 2,  2, 3, 4,  3, 5, 4,
            // Bottom face (verts 6..11, opposite winding)
            6, 8, 7,  7, 8, 9,  8, 10, 9,  9, 10, 11
        };
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        s_CachedLeafCard = m;
        return m;
    }

    static CombineInstance MakeBranchSegment(Vector3 p0, Vector3 p1, float r0, float r1)
    {
        Vector3 dir = p1 - p0;
        Mesh m = BuildFrustumMesh("Twig", 0f, dir.magnitude, r0, r1, 6, true);
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        return new CombineInstance { mesh = m, transform = Matrix4x4.TRS(p0, rot, Vector3.one) };
    }

    static Mesh BuildFrustumMesh(string name, float y0, float y1, float r0, float r1, int segs, bool capEnds)
    {
        var m = new Mesh { name = name };
        var verts = new List<Vector3>();
        var tris  = new List<int>();
        for (int i = 0; i <= segs; i++)
        {
            float a = (float)i / segs * Mathf.PI * 2f;
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            verts.Add(new Vector3(ca * r0, y0, sa * r0));
            verts.Add(new Vector3(ca * r1, y1, sa * r1));
        }
        for (int i = 0; i < segs; i++)
        {
            int b = i * 2;
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2);
        }
        if (capEnds)
        {
            int cBot = verts.Count; verts.Add(new Vector3(0f, y0, 0f));
            int cTop = verts.Count; verts.Add(new Vector3(0f, y1, 0f));
            for (int i = 0; i < segs; i++)
            {
                int b = i * 2;
                tris.Add(cBot); tris.Add(b); tris.Add(b + 2);
                tris.Add(cTop); tris.Add(b + 3); tris.Add(b + 1);
            }
        }
        m.vertices = verts.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        return m;
    }

    static Mesh CombineMeshes(string name, List<CombineInstance> parts)
    {
        var m = new Mesh { name = name };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.CombineMeshes(parts.ToArray(), true, true);
        m.RecalculateBounds();
        return m;
    }

    static Mesh SaveMeshAsset(Mesh m)
    {
        if (!Directory.Exists("Assets/Meshes"))
            Directory.CreateDirectory("Assets/Meshes");
        string path = $"Assets/Meshes/{m.name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            existing.vertices = m.vertices;
            existing.normals = m.normals;
            existing.uv = m.uv;
            existing.triangles = m.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(m);
            return existing;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void AddMeshPart(GameObject parent, string name, Mesh mesh, Material mat, bool addCollider)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (addCollider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    static string EnsureBhaskaraSignTexture(string dstAssetPath)
    {
        int w = 64, h = 96;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color white = new Color(0.94f, 0.95f, 0.96f);
        Color blueA = new Color(0.11f, 0.22f, 0.52f);
        Color blueB = new Color(0.08f, 0.17f, 0.42f);
        Color dark  = new Color(0.10f, 0.12f, 0.18f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c;
                if (y >= 78)
                {
                    c = white;
                    if (y >= 88 && y <= 92 && x >= 6 && x <= 14) c = blueA;
                    if (y >= 81 && y <= 84 && x >= 6 && x <= 54 && (x % 3 != 0)) c = dark;
                }
                else
                {
                    int slat = y / 7;
                    bool gap = (y % 7 == 0);
                    c = gap ? new Color(0.05f, 0.09f, 0.22f) : ((slat % 2 == 0) ? blueA : blueB);
                    int sy = y % 7;
                    if (!gap && sy >= 2 && sy <= 4 && x >= 5 && x <= 46 - (slat % 3) * 6 && (x % 3 != 0))
                        c = white;
                }
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return SavePixelTex(tex, dstAssetPath, 128);
    }

    static string EnsureBrickPaverTexture(string dstAssetPath, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var rng = new System.Random(101);
        for (int y = 0; y < size; y++)
        {
            int row = y / 4;
            int off = (row % 2 == 0) ? 0 : 4;
            for (int x = 0; x < size; x++)
            {
                bool seam = (y % 4 == 0) || ((x + off) % 8 == 0);
                float n = (float)(rng.NextDouble() * 0.08 - 0.04);
                Color c = seam
                    ? new Color(0.32f, 0.30f, 0.28f)
                    : new Color(0.55f + n, 0.48f + n, 0.44f + n);
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return SavePixelTex(tex, dstAssetPath, size);
    }

    static string EnsureAsphaltTexture(string dstAssetPath, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var rng = new System.Random(202);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = 0.30f + (float)(rng.NextDouble() * 0.08 - 0.04);
                if (x == 0 && y % 2 == 0) v -= 0.03f;
                tex.SetPixel(x, y, new Color(v, v, v + 0.01f, 1f));
            }
        }
        return SavePixelTex(tex, dstAssetPath, size);
    }

    static string SavePixelTex(Texture2D tex, string assetPath, int maxSize)
    {
        tex.Apply();
        File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti != null)
        {
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = Mathf.Max(32, Mathf.NextPowerOfTwo(maxSize));
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        return assetPath;
    }

    static GameObject Box(GameObject parent, string name, Vector3 localPos, Vector3 size, Material mat)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent.transform, false);
        cube.transform.localPosition = localPos;
        cube.transform.localScale = size;
        if (mat != null) cube.GetComponent<Renderer>().sharedMaterial = mat;
        return cube;
    }

    static Material MakeMat(string name, string texPath, Vector2 tiling, Color col, float smoothness)
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
        m.DisableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", Color.black);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
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
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MakeEmissiveMat(string name, Color col)
    {
        var m = MakeMat(name, null, Vector2.one, col, 0.1f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", col * 0.85f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
