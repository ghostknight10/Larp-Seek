using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Populates South_Hall (local x = -16..+24, z = +36..+48, y = 0..4.8):
///  - West/Back Zone (x = -15.6..-0.8, z = 42.2..47.78): Extended, sealed SHAPE UP Gym with black-tinted glass + Water Station
///  - West/Front Zone (x = -10.8..-2.7, z = 38.2): 4 Carrom Tables arranged in a single straight line along the glass facade
///  - Center Study Zone (x = +1.8..+10.2): White study tables with sky-blue molded chairs
///  - East Zone (x = +13.2..+23.78): Blue-floor Lounge with mural wall, sofas, bean bags, ottomans, and foosball table
///  - Ceiling: Structural beam, red sprinkler pipes, and ceiling fans (no TV/speaker/projector)
/// </summary>
public static class BuildSouthHallInterior
{
    public static void Build(
        GameObject southHallParent,
        float xWestInner, float xEastEnd,
        float zN, float zS, float groundH,
        string concTexPx, Material matAlum, Material matGlass, Material matWhiteInt)
    {
        var grp = new GameObject("SouthHall_Interior");
        grp.transform.SetParent(southHallParent.transform, false);

        string texMarble  = MakeBlackMarbleTex("Assets/Textures/marble_black_px.png");
        string texMural   = MakeLoungeMuralTex("Assets/Textures/mural_lounge_px.png");
        string texShapeUp = MakeShapeUpBannerTex("Assets/Textures/sign_shapeup_px.png");
        string texPoster  = MakeGymPosterTex("Assets/Textures/poster_gym_px.png");
        string texCarrom  = MakeCarromBoardTex("Assets/Textures/carrom_board_px.png");

        var matBlueFloor  = SolidMat("M_LoungeBlueFloor",   new Color(0.11f, 0.25f, 0.58f), 0.22f);
        var matYellowLine = SolidMat("M_LoungeYellowLine",  new Color(0.88f, 0.68f, 0.12f), 0.12f);
        var matMarble     = TexMat("M_BlackMarbleWall",     texMarble, 0.28f);
        var matMural      = TexMat("M_LoungeMural",         texMural, 0.08f);
        var matShapeUp    = TexMat("M_ShapeUpSign",         texShapeUp, 0.10f);
        var matGymPoster  = TexMat("M_GymPoster",           texPoster, 0.12f);
        var matCarromTop  = TexMat("M_CarromBoard",         texCarrom, 0.18f);

        var matSofa       = SolidMat("M_LoungeSofa",        new Color(0.24f, 0.36f, 0.44f), 0.08f);
        var matBeanBrown  = SolidMat("M_BeanBagBrown",      new Color(0.42f, 0.20f, 0.10f), 0.22f);
        var matBeanTan    = SolidMat("M_BeanBagTan",        new Color(0.54f, 0.28f, 0.13f), 0.22f);
        var matWhiteTable = SolidMat("M_StudyTableTop",     new Color(0.90f, 0.90f, 0.88f), 0.25f);
        var matSteel      = SolidMat("M_HallSteel",         new Color(0.65f, 0.67f, 0.70f), 0.45f);
        var matDarkFrame  = SolidMat("M_HallDarkFrame",     new Color(0.12f, 0.12f, 0.14f), 0.25f);
        var matRedAccent  = SolidMat("M_HallRedAccent",     new Color(0.76f, 0.12f, 0.12f), 0.20f);
        var matYellow     = SolidMat("M_HallYellow",        new Color(0.92f, 0.76f, 0.12f), 0.18f);
        var matGreenPitch = SolidMat("M_FoosballPitch",     new Color(0.14f, 0.52f, 0.20f), 0.15f);
        var matWaterBlue  = TransparentMat("M_WaterCanBlue", new Color(0.28f, 0.65f, 0.95f, 0.72f), 0.45f);
        var matBlackGlass = TransparentMat("M_GymBlackGlass", new Color(0.03f, 0.04f, 0.05f, 0.84f), 0.65f);
        var matCreamChair = SolidMat("M_Chair_Cream",       new Color(0.84f, 0.78f, 0.68f), 0.28f);
        var matConcrete   = SolidMat("M_HallBeamConc",      new Color(0.60f, 0.59f, 0.56f), 0.08f);
        var matFanWhite   = SolidMat("M_CeilingFanWhite",   new Color(0.92f, 0.92f, 0.92f), 0.22f);
        var matPipeRed    = SolidMat("M_SprinklerPipeRed",  new Color(0.62f, 0.12f, 0.12f), 0.25f);
        var matWood       = SolidMat("M_HallWood",          new Color(0.38f, 0.22f, 0.12f), 0.18f);

        // ════════════════════════════════════════════════════════════════════
        // 1. EXTENDED SEALED GYM (West/Back: x = -15.6 .. -0.8, z = 42.2 .. 47.78)
        //    Inaccessible, enclosed in dark black-tinted glass partitions & closed door
        // ════════════════════════════════════════════════════════════════════
        var grpGymAndStudy = Child(grp, "ZoneB_GymAndStudy");

        BuildShapeUpGym(grpGymAndStudy, -15.6f, -0.8f, 42.2f, 47.78f, groundH,
            matAlum, matBlackGlass, matDarkFrame, matShapeUp, matGymPoster,
            matWhiteTable, matRedAccent, matSteel, matYellow, matBlueFloor);

        // Water dispenser station right outside the East glass wall of the gym
        BuildWaterStation(grpGymAndStudy, new Vector3(-0.05f, 0f, 44.2f), matWhiteTable, matSteel, matWaterBlue, matGreenPitch);

        // Back wall door beside the gym (no TV/screen)
        Box(grpGymAndStudy, "BackWall_Door", new Vector3(1.6f, 1.25f, 47.76f), new Vector3(1.15f, 2.50f, 0.08f), matDarkFrame);

        // ════════════════════════════════════════════════════════════════════
        // 2. CARROM TABLES IN A SINGLE STRAIGHT LINE (along West glass facade)
        //    z = 38.4, x = -10.8, -8.1, -5.4, -2.7 (clears Door 0 & Door 1)
        // ════════════════════════════════════════════════════════════════════
        var grpCarrom = Child(grp, "ZoneC_CarromLine");
        float[] carromXs = { -10.8f, -8.1f, -5.4f, -2.7f };
        for (int i = 0; i < carromXs.Length; i++)
        {
            BuildCarromTableSet(grpCarrom, $"CarromTable_{i}", new Vector3(carromXs[i], 0f, 38.4f),
                matDarkFrame, matCarromTop, matRedAccent, (i == 1) ? matCreamChair : null);
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. WHITE STUDY TABLES + SKY-BLUE CHAIRS (Center: x = +1.8 .. +10.2)
        // ════════════════════════════════════════════════════════════════════
        float[] frontRowZs   = { 38.5f, 40.5f };
        float[] frontTableXs = { 2.5f, 4.7f, 6.9f, 9.1f };
        for (int r = 0; r < frontRowZs.Length; r++)
        {
            for (int c = 0; c < frontTableXs.Length; c++)
            {
                BuildStudyTableSet(grpGymAndStudy, $"StudyTable_Front_{r}_{c}",
                    new Vector3(frontTableXs[c], 0f, frontRowZs[r]),
                    1.85f, 0.85f, matWhiteTable, matSteel, includeEndChairs: (c == 0 || c == frontTableXs.Length - 1));
            }
        }
        float[] backRowZs   = { 43.4f, 45.4f };
        float[] backTableXs = { 4.2f, 6.5f, 8.8f };
        for (int r = 0; r < backRowZs.Length; r++)
        {
            for (int c = 0; c < backTableXs.Length; c++)
            {
                BuildStudyTableSet(grpGymAndStudy, $"StudyTable_Back_{r}_{c}",
                    new Vector3(backTableXs[c], 0f, backRowZs[r]),
                    1.85f, 0.85f, matWhiteTable, matSteel, includeEndChairs: false);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. BLUE LOUNGE ON THE OTHER END (East End: x = +13.2 .. +23.78, z = 36.3 .. 47.8)
        //    No TV, no speakers, no projector — mural, sofas, bean bags, ottomans, foosball
        // ════════════════════════════════════════════════════════════════════
        var grpLounge = Child(grp, "ZoneA_BlueLounge");

        // Blue painted floor with yellow border line on its West divider edge (x = +13.2)
        Box(grpLounge, "Lounge_BlueFloor",    new Vector3(18.50f, 0.006f, 42.10f), new Vector3(10.55f, 0.012f, 11.40f), matBlueFloor);
        Box(grpLounge, "Lounge_YellowBorder", new Vector3(13.22f, 0.008f, 42.10f), new Vector3(0.10f, 0.014f, 11.40f), matYellowLine);

        // Black/dark-green veined marble end wall along Wall_SouthHall_East (x = +23.77)
        Box(grpLounge, "Lounge_MarbleWall",   new Vector3(23.77f, 2.40f, 42.0f), new Vector3(0.05f, groundH, 11.6f), matMarble);
        Box(grpLounge, "Lounge_FireCabinet",  new Vector3(23.70f, 1.85f, 45.2f), new Vector3(0.10f, 1.35f, 0.95f), matRedAccent);
        Box(grpLounge, "Lounge_RecessAlcove", new Vector3(23.72f, 1.05f, 42.6f), new Vector3(0.08f, 2.10f, 2.40f), matDarkFrame);
        Box(grpLounge, "Lounge_RedDoorTrim",  new Vector3(23.73f, 1.25f, 38.4f), new Vector3(0.07f, 2.50f, 0.14f), matRedAccent);

        // Attached 8Bit campus mural along the back wall (z = 47.76)
        Quad(grpLounge, "Lounge_MuralWall", new Vector3(18.55f, 2.25f, 47.76f), Quaternion.identity, new Vector3(10.1f, 4.4f, 1f), matMural);

        // 3 Slate-blue sofas lined up against the mural wall
        for (int s = 0; s < 3; s++)
        {
            BuildSofa(grpLounge, $"Lounge_Sofa_{s}", new Vector3(15.9f + s * 2.45f, 0f, 46.85f), 180f, matSofa, matWood);
        }

        // 4 Realistic Brown Leather Bean Bags + 2 small round Ottomans
        Mesh beanMesh = BuildBeanBagMesh();
        Vector3[] beanPositions = {
            new Vector3(16.6f, 0f, 43.9f),
            new Vector3(18.6f, 0f, 42.5f),
            new Vector3(20.6f, 0f, 43.7f),
            new Vector3(21.4f, 0f, 41.7f)
        };
        float[] beanYaws = { 165f, 210f, 140f, 195f };
        for (int b = 0; b < beanPositions.Length; b++)
        {
            CreateBeanBag(grpLounge, $"BeanBag_{b}", beanPositions[b], beanYaws[b], beanMesh, (b % 2 == 0) ? matBeanBrown : matBeanTan);
        }
        CreateOttoman(grpLounge, "Ottoman_0", new Vector3(17.6f, 0f, 42.3f), matBeanBrown);
        CreateOttoman(grpLounge, "Ottoman_1", new Vector3(19.9f, 0f, 41.9f), matBeanTan);

        // Two square concrete columns separating Center Study Area from East Blue Lounge
        Box(grpLounge, "Lounge_Pillar_N", new Vector3(13.55f, 2.40f, 39.3f), new Vector3(0.75f, groundH, 0.75f), matConcrete);
        Box(grpLounge, "Lounge_Pillar_S", new Vector3(13.55f, 2.40f, 44.7f), new Vector3(0.75f, groundH, 0.75f), matConcrete);

        // Foosball table on the West side of the Blue Lounge
        BuildFoosballTable(grpLounge, "FoosballTable_0", new Vector3(15.6f, 0f, 39.2f), 90f, matGreenPitch, matYellow, matDarkFrame, matSteel, matRedAccent);

        // ════════════════════════════════════════════════════════════════════
        // CEILING STRUCTURAL BEAM, RED SPRINKLER PIPES & FANS
        // ════════════════════════════════════════════════════════════════════
        var grpCeil = Child(grp, "SouthHall_CeilingDetails");
        float hallW = xEastEnd - xWestInner;
        float hallCX = (xWestInner + xEastEnd) * 0.5f;

        Box(grpCeil, "Ceiling_MainBeam", new Vector3(hallCX, 4.48f, 42.15f), new Vector3(hallW, 0.65f, 0.55f), matConcrete);
        Box(grpCeil, "Ceiling_SprinklerPipe_1", new Vector3(hallCX, 4.58f, 39.0f), new Vector3(hallW - 1f, 0.06f, 0.06f), matPipeRed);
        Box(grpCeil, "Ceiling_SprinklerPipe_2", new Vector3(hallCX, 4.58f, 45.0f), new Vector3(hallW - 1f, 0.06f, 0.06f), matPipeRed);

        float[] fanXs = { -11.5f, -1.0f, 7.5f, 18.5f };
        foreach (float fx in fanXs)
        {
            BuildCeilingFan(grpCeil, $"CeilingFan_{fx:F0}", new Vector3(fx, 4.80f, 39.2f), matFanWhite);
            BuildCeilingFan(grpCeil, $"CeilingFanS_{fx:F0}", new Vector3(fx, 4.80f, 45.0f), matFanWhite);
        }
    }

    static void BuildSofa(GameObject parent, string name, Vector3 pos, float rotY, Material matFabric, Material matLeg)
    {
        var sofa = Child(parent, name);
        sofa.transform.localPosition = pos;
        sofa.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);

        Box(sofa, "Base",     new Vector3(0f, 0.24f,  0.00f), new Vector3(2.15f, 0.28f, 0.86f), matFabric);
        Box(sofa, "Back",     new Vector3(0f, 0.58f, -0.35f), new Vector3(2.15f, 0.56f, 0.20f), matFabric);
        Box(sofa, "ArmL",     new Vector3(-0.98f, 0.44f, 0f), new Vector3(0.20f, 0.44f, 0.86f), matFabric);
        Box(sofa, "ArmR",     new Vector3( 0.98f, 0.44f, 0f), new Vector3(0.20f, 0.44f, 0.86f), matFabric);
        Box(sofa, "CushionL", new Vector3(-0.44f, 0.42f, 0.05f), new Vector3(0.84f, 0.14f, 0.70f), matFabric);
        Box(sofa, "CushionR", new Vector3( 0.44f, 0.42f, 0.05f), new Vector3(0.84f, 0.14f, 0.70f), matFabric);

        Box(sofa, "LegFL", new Vector3(-0.96f, 0.05f,  0.35f), new Vector3(0.08f, 0.10f, 0.08f), matLeg);
        Box(sofa, "LegFR", new Vector3( 0.96f, 0.05f,  0.35f), new Vector3(0.08f, 0.10f, 0.08f), matLeg);
        Box(sofa, "LegBL", new Vector3(-0.96f, 0.05f, -0.35f), new Vector3(0.08f, 0.10f, 0.08f), matLeg);
        Box(sofa, "LegBR", new Vector3( 0.96f, 0.05f, -0.35f), new Vector3(0.08f, 0.10f, 0.08f), matLeg);
    }

    static void CreateBeanBag(GameObject parent, string name, Vector3 pos, float rotY, Mesh mesh, Material mat)
    {
        var go = Child(parent, name);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    static void CreateOttoman(GameObject parent, string name, Vector3 pos, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos + new Vector3(0f, 0.16f, 0f);
        go.transform.localScale = new Vector3(0.46f, 0.16f, 0.46f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Mesh BuildBeanBagMesh()
    {
        if (!Directory.Exists("Assets/Meshes")) Directory.CreateDirectory("Assets/Meshes");
        string path = "Assets/Meshes/BeanBagMesh.asset";

        int rings = 10;
        int segs = 16;
        var verts = new List<Vector3>();
        var tris  = new List<int>();

        for (int r = 0; r <= rings; r++)
        {
            float v = r / (float)rings;
            float y = v * 0.78f;
            float baseRadius = Mathf.Sin(Mathf.Pow(v, 0.65f) * Mathf.PI) * 0.54f;
            if (v < 0.12f) baseRadius = Mathf.Lerp(0.32f, baseRadius, v / 0.12f);
            float zLean = -Mathf.Pow(v, 1.5f) * 0.22f;

            for (int s = 0; s <= segs; s++)
            {
                float a = (s / (float)segs) * Mathf.PI * 2f;
                float ripple = 1f + 0.06f * Mathf.Sin(a * 5f + v * 3f);
                float x = Mathf.Cos(a) * baseRadius * ripple;
                float z = Mathf.Sin(a) * baseRadius * 1.08f * ripple + zLean;
                verts.Add(new Vector3(x, y, z));
            }
        }

        int stride = segs + 1;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segs; s++)
            {
                int a = r * stride + s;
                int b = a + stride;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        }

        var m = new Mesh { name = "BeanBagMesh" };
        m.vertices = verts.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();

        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(m, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(m);
            return existing;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void BuildFoosballTable(
        GameObject parent, string name, Vector3 pos, float rotY,
        Material matPitch, Material matCabinet, Material matDark, Material matSteel, Material matRed)
    {
        var tbl = Child(parent, name);
        tbl.transform.localPosition = pos;
        tbl.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);

        Box(tbl, "Cabinet", new Vector3(0f, 0.78f, 0f), new Vector3(1.42f, 0.28f, 0.76f), matCabinet);
        Box(tbl, "Pitch",   new Vector3(0f, 0.93f, 0f), new Vector3(1.26f, 0.02f, 0.64f), matPitch);
        for (int l = 0; l < 4; l++)
        {
            float lx = (l < 2) ? -0.58f : 0.58f;
            float lz = (l % 2 == 0) ? -0.30f : 0.30f;
            Box(tbl, $"Leg_{l}", new Vector3(lx, 0.34f, lz), new Vector3(0.08f, 0.68f, 0.08f), matDark);
        }
        for (int r = 0; r < 6; r++)
        {
            float rx = -0.45f + r * 0.18f;
            Box(tbl, $"Rod_{r}", new Vector3(rx, 0.94f, 0f), new Vector3(0.025f, 0.025f, 1.05f), matSteel);
            Box(tbl, $"Handle_{r}", new Vector3(rx, 0.94f, (r % 2 == 0) ? -0.54f : 0.54f), new Vector3(0.045f, 0.045f, 0.12f), matDark);
            Box(tbl, $"Player_{r}", new Vector3(rx, 0.96f, 0f), new Vector3(0.04f, 0.09f, 0.04f), (r % 2 == 0) ? matRed : matCabinet);
        }
    }

    static void BuildShapeUpGym(
        GameObject parent,
        float minX, float maxX, float minZ, float maxZ, float groundH,
        Material matAlum, Material matBlackGlass, Material matDark,
        Material matShapeUp, Material matPoster, Material matWhite, Material matRed, Material matSteel, Material matYellow, Material matBlue)
    {
        var gym = Child(parent, "ShapeUp_Gym");
        float w = maxX - minX;
        float d = maxZ - minZ;
        float cx = (minX + maxX) * 0.5f;
        float cz = (minZ + maxZ) * 0.5f;

        // Fully sealed black-tinted glass partitions along West, East, and full Front (no open entry!)
        BuildGlassPartitionRun(gym, "GymGlass_West",  new Vector3(minX, 0f, minZ), new Vector3(minX, 0f, maxZ), groundH, 4, matAlum, matBlackGlass);
        BuildGlassPartitionRun(gym, "GymGlass_East",  new Vector3(maxX, 0f, minZ), new Vector3(maxX, 0f, maxZ), groundH, 4, matAlum, matBlackGlass);
        BuildGlassPartitionRun(gym, "GymGlass_Front", new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, minZ), groundH, 10, matAlum, matBlackGlass);

        // Closed SHAPE UP double doors + header sign (x = -4.2 .. -2.2, sealed with black-tinted glass)
        float doorCX = -3.2f;
        Box(gym, "ShapeUp_SignBacking", new Vector3(doorCX, 3.65f, minZ), new Vector3(2.0f, 1.55f, 0.08f), matWhite);
        Quad(gym, "ShapeUp_SignFace", new Vector3(doorCX, 3.65f, minZ - 0.05f), Quaternion.identity, new Vector3(1.92f, 1.48f, 1f), matShapeUp);
        Box(gym, "GymDoor_ClosedL", new Vector3(doorCX - 0.50f, 1.40f, minZ), new Vector3(0.98f, 2.80f, 0.06f), matBlackGlass);
        Box(gym, "GymDoor_ClosedR", new Vector3(doorCX + 0.50f, 1.40f, minZ), new Vector3(0.98f, 2.80f, 0.06f), matBlackGlass);

        // Gym poster, white shoe cubby rack & red dustbin outside front wall
        Quad(gym, "Gym_Poster", new Vector3(doorCX - 1.95f, 1.95f, minZ - 0.05f), Quaternion.identity, new Vector3(1.30f, 2.15f, 1f), matPoster);
        Box(gym, "Gym_ShoeRack", new Vector3(doorCX - 4.30f, 0.28f, minZ - 0.25f), new Vector3(1.65f, 0.56f, 0.36f), matWhite);
        Box(gym, "Gym_ShoeSlot", new Vector3(doorCX - 4.30f, 0.28f, minZ - 0.42f), new Vector3(1.52f, 0.42f, 0.04f), matDark);

        var bin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bin.name = "RedBin_Gym";
        bin.transform.SetParent(gym.transform, false);
        bin.transform.localPosition = new Vector3(doorCX + 1.55f, 0.42f, minZ - 0.38f);
        bin.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
        bin.GetComponent<Renderer>().sharedMaterial = matRed;

        // Interior gym equipment behind the black-tinted glass
        Box(gym, "Gym_RubberFloor", new Vector3(cx, 0.008f, cz), new Vector3(w - 0.2f, 0.016f, d - 0.2f), matDark);

        // Two squat racks & barbells across the extended 14.8m gym
        for (int r = 0; r < 2; r++)
        {
            float rx = -13.0f + r * 6.0f;
            Box(gym, $"Gym_RackPostL_{r}", new Vector3(rx - 0.7f, 1.15f, 46.2f), new Vector3(0.08f, 2.30f, 0.08f), matYellow);
            Box(gym, $"Gym_RackPostR_{r}", new Vector3(rx + 0.7f, 1.15f, 46.2f), new Vector3(0.08f, 2.30f, 0.08f), matYellow);
            Box(gym, $"Gym_Barbell_{r}",   new Vector3(rx,        1.45f, 46.2f), new Vector3(2.10f, 0.04f, 0.04f), matSteel);
        }

        // Weight benches
        Box(gym, "Gym_BenchBase_0", new Vector3(-9.8f, 0.22f, 45.2f), new Vector3(0.50f, 0.44f, 1.35f), matDark);
        Box(gym, "Gym_BenchPad_0",  new Vector3(-9.8f, 0.48f, 45.2f), new Vector3(0.36f, 0.08f, 1.25f), matRed);
        Box(gym, "Gym_BenchBase_1", new Vector3(-4.0f, 0.22f, 45.2f), new Vector3(0.50f, 0.44f, 1.35f), matDark);
        Box(gym, "Gym_BenchPad_1",  new Vector3(-4.0f, 0.48f, 45.2f), new Vector3(0.36f, 0.08f, 1.25f), matRed);

        // Dumbbell rack & stacked mats
        Box(gym, "Gym_DumbbellRack", new Vector3(-14.6f, 0.42f, 44.2f), new Vector3(0.55f, 0.84f, 2.60f), matDark);
        Box(gym, "Gym_MatStack_B",   new Vector3(-2.2f,  0.14f, 43.4f), new Vector3(0.95f, 0.16f, 0.65f), matBlue);
        Box(gym, "Gym_MatStack_R",   new Vector3(-2.2f,  0.30f, 43.4f), new Vector3(0.95f, 0.16f, 0.65f), matRed);
        Box(gym, "Gym_MatStack_Y",   new Vector3(-2.2f,  0.46f, 43.4f), new Vector3(0.95f, 0.16f, 0.65f), matYellow);
    }

    static void BuildGlassPartitionRun(
        GameObject parent, string name, Vector3 p0, Vector3 p1, float height, int bays,
        Material matAlum, Material matGlass)
    {
        Vector3 delta = p1 - p0;
        float len = delta.magnitude;
        if (len < 0.1f) return;
        bool alongZ = Mathf.Abs(delta.z) > Mathf.Abs(delta.x);
        Vector3 mid = (p0 + p1) * 0.5f;

        Vector3 railSize = alongZ ? new Vector3(0.07f, 0.07f, len) : new Vector3(len, 0.07f, 0.07f);
        Box(parent, $"{name}_Bot", new Vector3(mid.x, 0.035f, mid.z), railSize, matAlum);
        Box(parent, $"{name}_Mid", new Vector3(mid.x, 2.85f,  mid.z), railSize, matAlum);
        Box(parent, $"{name}_Top", new Vector3(mid.x, height - 0.04f, mid.z), railSize, matAlum);

        Vector3 glassSize = alongZ ? new Vector3(0.04f, height - 0.10f, len - 0.06f) : new Vector3(len - 0.06f, height - 0.10f, 0.04f);
        Box(parent, $"{name}_Glass", new Vector3(mid.x, height * 0.5f, mid.z), glassSize, matGlass);

        for (int i = 0; i <= bays; i++)
        {
            Vector3 mp = Vector3.Lerp(p0, p1, i / (float)bays);
            Box(parent, $"{name}_Mul{i}", new Vector3(mp.x, height * 0.5f, mp.z), new Vector3(0.07f, height, 0.07f), matAlum);
        }
    }

    static void BuildWaterStation(
        GameObject parent, Vector3 pos,
        Material matTop, Material matSteel, Material matWater, Material matBucket)
    {
        var grp = Child(parent, "Water_Station");
        grp.transform.localPosition = pos;

        Box(grp, "WaterTable_Top", new Vector3(0f, 0.76f, 0f), new Vector3(0.65f, 0.05f, 1.45f), matTop);
        for (int i = 0; i < 4; i++)
        {
            float lx = (i < 2) ? -0.28f : 0.28f;
            float lz = (i % 2 == 0) ? -0.66f : 0.66f;
            Box(grp, $"Leg_{i}", new Vector3(lx, 0.37f, lz), new Vector3(0.04f, 0.74f, 0.04f), matSteel);
        }
        for (int c = 0; c < 3; c++)
        {
            var can = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            can.name = $"WaterCan_{c}";
            can.transform.SetParent(grp.transform, false);
            can.transform.localPosition = new Vector3(0f, 1.03f, -0.42f + c * 0.42f);
            can.transform.localScale = new Vector3(0.28f, 0.25f, 0.28f);
            can.GetComponent<Renderer>().sharedMaterial = matWater;
        }
        var bucket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bucket.name = "WaterBucket";
        bucket.transform.SetParent(grp.transform, false);
        bucket.transform.localPosition = new Vector3(0f, 0.16f, 0.25f);
        bucket.transform.localScale = new Vector3(0.26f, 0.16f, 0.26f);
        bucket.GetComponent<Renderer>().sharedMaterial = matBucket;
    }

    static void BuildStudyTableSet(
        GameObject parent, string name, Vector3 pos, float w, float d,
        Material matTop, Material matSteel, bool includeEndChairs)
    {
        var grp = Child(parent, name);
        grp.transform.localPosition = pos;

        Box(grp, "StudyTable_Top", new Vector3(0f, 0.74f, 0f), new Vector3(w, 0.045f, d), matTop);
        float lx = w * 0.5f - 0.06f;
        float lz = d * 0.5f - 0.06f;
        Box(grp, "Leg_FL", new Vector3(-lx, 0.36f,  lz), new Vector3(0.04f, 0.72f, 0.04f), matSteel);
        Box(grp, "Leg_FR", new Vector3( lx, 0.36f,  lz), new Vector3(0.04f, 0.72f, 0.04f), matSteel);
        Box(grp, "Leg_BL", new Vector3(-lx, 0.36f, -lz), new Vector3(0.04f, 0.72f, 0.04f), matSteel);
        Box(grp, "Leg_BR", new Vector3( lx, 0.36f, -lz), new Vector3(0.04f, 0.72f, 0.04f), matSteel);

        // Place chairs as siblings under parent so Study Table and Chair morph independently
        float[] cx = { -0.44f, 0.44f };
        for (int i = 0; i < cx.Length; i++)
        {
            BuildProps.CreateDiningChair(parent, $"Chair_{name}_N_{i}", pos + new Vector3(cx[i], 0f, -d * 0.5f - 0.22f),   0f, isLeftWing: true);
            BuildProps.CreateDiningChair(parent, $"Chair_{name}_S_{i}", pos + new Vector3(cx[i], 0f,  d * 0.5f + 0.22f), 180f, isLeftWing: true);
        }
        if (includeEndChairs)
        {
            BuildProps.CreateDiningChair(parent, $"Chair_{name}_End", pos + new Vector3((pos.x < 5.5f) ? (-w * 0.5f - 0.22f) : (w * 0.5f + 0.22f), 0f, 0f), (pos.x < 5.5f) ? 90f : -90f, isLeftWing: true);
        }
    }

    static void BuildCarromTableSet(
        GameObject parent, string name, Vector3 pos,
        Material matFrame, Material matBoard, Material matCoin, Material matVariantChair)
    {
        var grp = Child(parent, name);
        grp.transform.localPosition = pos;

        Box(grp, "Carrom_Frame", new Vector3(0f, 0.73f,  0f), new Vector3(1.06f, 0.07f, 1.06f), matFrame);
        Box(grp, "Carrom_Board", new Vector3(0f, 0.755f, 0f), new Vector3(0.84f, 0.03f, 0.84f), matBoard);
        Box(grp, "Carrom_Coins", new Vector3(0f, 0.775f, 0f), new Vector3(0.12f, 0.015f, 0.12f), matCoin);

        float leg = 0.44f;
        Box(grp, "Leg_1", new Vector3(-leg, 0.35f, -leg), new Vector3(0.04f, 0.70f, 0.04f), matFrame);
        Box(grp, "Leg_2", new Vector3( leg, 0.35f, -leg), new Vector3(0.04f, 0.70f, 0.04f), matFrame);
        Box(grp, "Leg_3", new Vector3(-leg, 0.35f,  leg), new Vector3(0.04f, 0.70f, 0.04f), matFrame);
        Box(grp, "Leg_4", new Vector3( leg, 0.35f,  leg), new Vector3(0.04f, 0.70f, 0.04f), matFrame);

        // Place chairs as siblings under parent so Carrom Table morphs without its chairs
        BuildProps.CreateDiningChair(parent, $"Chair_{name}_N", pos + new Vector3(0f, 0f, -0.74f),   0f, isLeftWing: true);
        BuildProps.CreateDiningChair(parent, $"Chair_{name}_S", pos + new Vector3(0f, 0f,  0.74f), 180f, isLeftWing: true);
        BuildProps.CreateDiningChair(parent, $"Chair_{name}_W", pos + new Vector3(-0.74f, 0f, 0f),  90f, isLeftWing: true);
        BuildProps.CreateDiningChair(parent, $"Chair_{name}_E", pos + new Vector3( 0.74f, 0f, 0f), -90f, isLeftWing: true);

        if (matVariantChair != null)
        {
            var creamCh = parent.transform.Find($"Chair_{name}_N/Chair_Shell");
            if (creamCh != null) creamCh.GetComponent<Renderer>().sharedMaterial = matVariantChair;
        }
    }

    static void BuildCeilingFan(GameObject parent, string name, Vector3 ceilingPos, Material matWhite)
    {
        var fan = Child(parent, name);
        fan.transform.localPosition = ceilingPos;
        Box(fan, "Rod", new Vector3(0f, -0.25f, 0f), new Vector3(0.04f, 0.50f, 0.04f), matWhite);
        Box(fan, "Hub", new Vector3(0f, -0.50f, 0f), new Vector3(0.22f, 0.08f, 0.22f), matWhite);
        for (int i = 0; i < 3; i++)
        {
            var blade = Box(fan, $"Blade_{i}", Vector3.zero, new Vector3(0.14f, 0.02f, 0.62f), matWhite);
            blade.transform.localRotation = Quaternion.Euler(0f, i * 120f, 0f);
            blade.transform.localPosition = blade.transform.localRotation * new Vector3(0f, -0.50f, 0.36f);
        }
    }

    static string MakeBlackMarbleTex(string path)
    {
        int s = 48;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                bool vein = ((x + y * 2) % 23 == 0) || ((x * 2 - y + 48) % 29 == 0);
                Color c = vein ? new Color(0.32f, 0.38f, 0.36f) : new Color(0.07f, 0.11f, 0.10f);
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return SaveTex(tex, path, 64, TextureWrapMode.Repeat);
    }

    static string MakeLoungeMuralTex(string path)
    {
        string srcPath = "Assets/Textures/mural_lounge_src.jpg";
        if (File.Exists(srcPath))
        {
            AssetDatabase.ImportAsset(srcPath, ImportAssetOptions.ForceUpdate);
            var srcTi = AssetImporter.GetAtPath(srcPath) as TextureImporter;
            bool prevReadable = srcTi != null && srcTi.isReadable;
            if (srcTi != null && !prevReadable)
            {
                srcTi.isReadable = true;
                srcTi.SaveAndReimport();
            }

            var srcTex = AssetDatabase.LoadAssetAtPath<Texture2D>(srcPath);
            if (srcTex != null)
            {
                int dstW = 240;
                int dstH = 128;
                var dstTex = new Texture2D(dstW, dstH, TextureFormat.RGBA32, false);
                for (int y = 0; y < dstH; y++)
                {
                    int sy = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / dstH * srcTex.height), 0, srcTex.height - 1);
                    for (int x = 0; x < dstW; x++)
                    {
                        int sx = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / dstW * srcTex.width), 0, srcTex.width - 1);
                        Color c = srcTex.GetPixel(sx, sy);
                        c.a = 1f;
                        dstTex.SetPixel(x, y, c);
                    }
                }
                if (srcTi != null && !prevReadable)
                {
                    srcTi.isReadable = false;
                    srcTi.SaveAndReimport();
                }
                return SaveTex(dstTex, path, 256, TextureWrapMode.Clamp);
            }
        }

        int w = 96, h = 48;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color bg = new Color(0.42f, 0.72f, 0.70f, 1f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, bg);
        return SaveTex(tex, path, 128, TextureWrapMode.Clamp);
    }

    static string MakeShapeUpBannerTex(string path)
    {
        int w = 64, h = 48;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color white = new Color(0.94f, 0.94f, 0.92f);
        Color dark  = new Color(0.10f, 0.10f, 0.12f);
        Color gold  = new Color(0.88f, 0.70f, 0.12f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = white;
                if (y <= 11)
                {
                    c = dark;
                    if (y >= 4 && y <= 7 && x >= 14 && x <= 50 && (x % 3 != 0)) c = gold;
                }
                else
                {
                    if (y >= 26 && y <= 29 && x >= 10 && x <= 54) c = dark;
                    if (y >= 21 && y <= 34 && ((x >= 10 && x <= 14) || (x >= 50 && x <= 54))) c = dark;
                    if (y >= 16 && y <= 38 && x >= 27 && x <= 37) c = dark;
                }
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return SaveTex(tex, path, 64, TextureWrapMode.Clamp);
    }

    static string MakeGymPosterTex(string path)
    {
        int w = 32, h = 48;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool border = (x == 0 || x == w - 1 || y == 0 || y == h - 1);
                Color c = border ? Color.white : new Color(0.12f, 0.11f, 0.13f);
                if (!border && y >= 14 && y <= 38 && x >= 10 && x <= 22) c = new Color(0.55f, 0.42f, 0.34f);
                if (!border && y >= 6 && y <= 9 && x >= 5 && x <= 27 && (x % 2 == 0)) c = Color.white;
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return SaveTex(tex, path, 64, TextureWrapMode.Clamp);
    }

    static string MakeCarromBoardTex(string path)
    {
        int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Color wood = new Color(0.82f, 0.71f, 0.54f);
        Color line = new Color(0.28f, 0.16f, 0.10f);
        Color hole = new Color(0.06f, 0.06f, 0.07f);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                Color c = wood;
                bool pocket = (x <= 2 && y <= 2) || (x >= s - 3 && y <= 2) || (x <= 2 && y >= s - 3) || (x >= s - 3 && y >= s - 3);
                if (pocket) c = hole;
                else if (x == 4 || x == s - 5 || y == 4 || y == s - 5) c = line;
                else if (Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) - 4.5f) < 0.8f) c = line;
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return SaveTex(tex, path, 32, TextureWrapMode.Clamp);
    }

    static string SaveTex(Texture2D tex, string assetPath, int maxSize, TextureWrapMode wrap)
    {
        tex.Apply();
        File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti != null)
        {
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = wrap;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = maxSize;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        return assetPath;
    }

    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
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

    static void Quad(GameObject parent, string name, Vector3 localPos, Quaternion localRot, Vector3 scale, Material mat)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        q.transform.SetParent(parent.transform, false);
        q.transform.localPosition = localPos;
        q.transform.localRotation = localRot;
        q.transform.localScale = scale;
        q.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(q.GetComponent<Collider>());
    }

    static Material SolidMat(string name, Color col, float smooth)
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
        m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material TexMat(string name, string texPath, float smooth)
    {
        var m = SolidMat(name, Color.white, smooth);
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (t != null)
        {
            m.mainTexture = t;
            m.SetTexture("_BaseMap", t);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material TransparentMat(string name, Color col, float smooth)
    {
        var m = SolidMat(name, col, smooth);
        m.SetFloat("_Surface", 1);
        m.SetInt("_SrcBlend", 5);
        m.SetInt("_DstBlend", 10);
        m.SetInt("_ZWrite", 0);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(m);
        return m;
    }
}
