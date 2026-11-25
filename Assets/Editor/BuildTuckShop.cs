using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Builds the North-West Tuck Shop (opposite side: shelves with Kurkure & Lays packets,
/// ice cream chest refrigerator with ice cream inside, glass-door cold drink fridge)
/// and the mess-facing giant purple Ideal Ice Cream poster + pillar banner.
/// </summary>
public static class BuildTuckShop
{
    public static void Build(
        GameObject root,
        float xWestOuter, float xWestInner,
        float zNorth, float zBarS, float H)
    {
        var grp = new GameObject("TuckShop_And_Poster");
        grp.transform.SetParent(root.transform, false);

        // 1. Generate crisp retro pixel-art textures
        string texKurkureRed   = MakeKurkureTex("Assets/Textures/kurkure_red_px.png",
            new Color(0.92f, 0.28f, 0.06f), new Color(0.98f, 0.80f, 0.12f));
        string texKurkureGreen = MakeKurkureTex("Assets/Textures/kurkure_green_px.png",
            new Color(0.12f, 0.62f, 0.22f), new Color(0.95f, 0.86f, 0.18f));
        string texLaysBlue     = MakeLaysTex("Assets/Textures/lays_blue_px.png",
            new Color(0.08f, 0.36f, 0.82f));
        string texLaysYellow   = MakeLaysTex("Assets/Textures/lays_yellow_px.png",
            new Color(0.95f, 0.78f, 0.10f));
        string texLaysGreen    = MakeLaysTex("Assets/Textures/lays_green_px.png",
            new Color(0.15f, 0.68f, 0.28f));
        string texPosterMain   = MakeIdealPosterTex("Assets/Textures/poster_ideal_px.png", 160, 80);
        string texPosterPillar = MakeIdealPillarTex("Assets/Textures/poster_ideal_pillar_px.png", 48, 96);

        // 2. Materials
        var mWood       = SolidMat("M_TuckWood",    new Color(0.42f, 0.26f, 0.13f));
        var mSteel      = SolidMat("M_TuckSteel",   new Color(0.72f, 0.74f, 0.76f));
        var mWhiteAppr  = SolidMat("M_FridgeWhite", new Color(0.90f, 0.92f, 0.94f));
        var mFridgeDark = SolidMat("M_FridgeDark",  new Color(0.16f, 0.18f, 0.20f));
        var mFridgeRed  = SolidMat("M_FridgeRed",   new Color(0.78f, 0.10f, 0.12f));
        var mIceBlue    = SolidMat("M_IceInterior", new Color(0.76f, 0.90f, 0.96f));
        var mGlass      = TransparentMat("M_FridgeGlass", new Color(0.75f, 0.90f, 0.98f, 0.28f));

        var mKurkureRed   = TexMat("M_KurkureRed",   texKurkureRed);
        var mKurkureGreen = TexMat("M_KurkureGreen", texKurkureGreen);
        var mLaysBlue     = TexMat("M_LaysBlue",     texLaysBlue);
        var mLaysYellow   = TexMat("M_LaysYellow",   texLaysYellow);
        var mLaysGreen    = TexMat("M_LaysGreen",    texLaysGreen);

        var mPosterMain   = TexMat("M_IdealPoster",       texPosterMain);
        var mPosterPillar = TexMat("M_IdealPillarBanner", texPosterPillar);

        Material[] chipMats = { mKurkureRed, mLaysBlue, mLaysYellow, mKurkureGreen, mLaysGreen };

        // 3. Tuck Shop Service Area (North-West corner: x in [-32..-16], z in [-34..-22])
        // Walkway from Counter Bar & Courtyard stays open on the East side (x = -21.5..-16)!

        // ── A. Back Snack Shelves along North Wall (z = -33.35) ──
        BuildSnackShelfUnit(grp, "SnackShelf_North_1", new Vector3(-23.5f, 0f, zNorth + 0.65f), 0f,
            3.2f, 2.3f, 0.55f, mSteel, mWood, chipMats);
        BuildSnackShelfUnit(grp, "SnackShelf_North_2", new Vector3(-27.2f, 0f, zNorth + 0.65f), 0f,
            3.2f, 2.3f, 0.55f, mSteel, mWood, chipMats);

        // ── B. West Wall Cold Drink Fridges + Side Snack Shelf + Ice Cream Freezer (facing +X East) ──
        BuildColdDrinkFridge(grp, "ColdDrinkFridge_1",
            new Vector3(xWestOuter + 0.70f, 0f, zNorth + 2.2f), 90f,
            mWhiteAppr, mFridgeRed, mFridgeDark, mGlass);

        BuildColdDrinkFridge(grp, "ColdDrinkFridge_2",
            new Vector3(xWestOuter + 0.70f, 0f, zNorth + 3.7f), 90f,
            mWhiteAppr, mFridgeDark, mFridgeDark, mGlass);

        BuildSnackShelfUnit(grp, "SnackShelf_West",
            new Vector3(xWestOuter + 0.65f, 0f, zNorth + 6.4f), 90f,
            3.0f, 2.3f, 0.55f, mSteel, mWood, chipMats);

        // ── C. Ice Cream Chest Refrigerators (horizontal glass-top freezer with ice cream inside) ──
        BuildIceCreamFreezer(grp, "IceCream_Freezer_1",
            new Vector3(xWestOuter + 1.1f, 0f, zNorth + 9.4f), 90f,
            mWhiteAppr, mFridgeRed, mIceBlue, mGlass);

        BuildIceCreamFreezer(grp, "IceCream_Freezer_2",
            new Vector3(-24.0f, 0f, zBarS - 1.3f), 0f,
            mWhiteAppr, mFridgeRed, mIceBlue, mGlass);

        // ── D. Tuck Shop Front Service Counter (facing East open walkway) ──
        BuildTuckCounter(grp, new Vector3(-23.5f, 0f, zNorth + 5.2f),
            mWood, mSteel, mGlass, chipMats);

        // Warm interior ceiling light for the tuck shop
        var lightGo = new GameObject("TuckShop_Light");
        lightGo.transform.SetParent(grp.transform, false);
        lightGo.transform.position = new Vector3(-26.5f, H - 0.5f, (zNorth + zBarS) * 0.5f);
        var pl = lightGo.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.range = 14f;
        pl.intensity = 1.6f;
        pl.color = new Color(1f, 0.96f, 0.88f);

        // ── E. BIG IDEAL ICE CREAM POSTER (Facing West Wing dining tables, leaving open archway x=-21.6..-16!) ──
        float posterW = 10.2f;
        float posterH = 3.25f;
        float posterX = xWestOuter + posterW * 0.5f + 0.2f; // -26.7 (leaves x=-21.6..-16 completely open!)
        float posterZ = zBarS - 0.25f;                       // -22.25

        // Partition wall backing
        Box(grp, "TuckPoster_PartitionWall",
            new Vector3(posterX, posterH * 0.5f, posterZ),
            new Vector3(posterW, posterH, 0.16f), mSteel);

        // Big purple Ideal poster mounted on the South face (+Z) facing the West Wing mess seating
        var posterQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        posterQuad.name = "Poster_Ideal_MessSide";
        posterQuad.transform.SetParent(grp.transform, false);
        posterQuad.transform.position = new Vector3(posterX, posterH * 0.5f + 0.05f, posterZ + 0.09f);
        posterQuad.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Front normal faces +Z (South into West Wing)
        posterQuad.transform.localScale = new Vector3(posterW - 0.1f, posterH - 0.1f, 1f);
        posterQuad.GetComponent<Renderer>().sharedMaterial = mPosterMain;

        // Vertical Ideal Ice Cream banner wrapped on the West corner pillar (xWestInner, zBarS)
        var pilBanner = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pilBanner.name = "Poster_Ideal_PillarWrap";
        pilBanner.transform.SetParent(grp.transform, false);
        pilBanner.transform.position = new Vector3(xWestInner, H - 1.05f, zBarS + 0.46f);
        pilBanner.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Faces +Z into West Wing
        pilBanner.transform.localScale = new Vector3(0.92f, 2.0f, 1f);
        pilBanner.GetComponent<Renderer>().sharedMaterial = mPosterPillar;

        // Side wrap on West face (-X) of that same pillar facing open archway
        var pilBannerSide = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pilBannerSide.name = "Poster_Ideal_PillarWrap_Side";
        pilBannerSide.transform.SetParent(grp.transform, false);
        pilBannerSide.transform.position = new Vector3(xWestInner - 0.46f, H - 1.05f, zBarS);
        pilBannerSide.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // Faces -X
        pilBannerSide.transform.localScale = new Vector3(0.92f, 2.0f, 1f);
        pilBannerSide.GetComponent<Renderer>().sharedMaterial = mPosterPillar;
    }

    // ════════════════════════════════════════════════════════════════════
    // 3D PROP BUILDERS
    // ════════════════════════════════════════════════════════════════════

    static void BuildSnackShelfUnit(
        GameObject parent, string name, Vector3 pos, float rotY,
        float width, float height, float depth,
        Material mFrame, Material mBoard, Material[] packetMats)
    {
        var rack = new GameObject(name);
        rack.transform.SetParent(parent.transform, false);
        rack.transform.position = pos;
        rack.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

        // Frame posts
        Box(rack, "Post_L", new Vector3(-width * 0.5f + 0.04f, height * 0.5f, 0f), new Vector3(0.06f, height, depth), mFrame);
        Box(rack, "Post_R", new Vector3( width * 0.5f - 0.04f, height * 0.5f, 0f), new Vector3(0.06f, height, depth), mFrame);
        // Backboard
        Box(rack, "Backboard", new Vector3(0f, height * 0.5f, -depth * 0.5f + 0.02f), new Vector3(width, height, 0.04f), mFrame);

        // 4 shelves stocked with Kurkure & Lays packets
        float[] shelfHeights = { 0.35f, 0.90f, 1.45f, 2.00f };
        for (int s = 0; s < shelfHeights.Length; s++)
        {
            float sy = shelfHeights[s];
            Box(rack, $"Shelf_{s}", new Vector3(0f, sy, 0f), new Vector3(width - 0.04f, 0.05f, depth), mBoard);

            // Pack each shelf densely with snack packets (2 rows deep)
            Material pktMat = packetMats[s % packetMats.Length];
            int cols = Mathf.FloorToInt((width - 0.25f) / 0.24f);
            for (int c = 0; c < cols; c++)
            {
                float px = -width * 0.5f + 0.22f + c * 0.24f;
                // Alternate packet brand every few items for authentic tuck shop look
                Material m = (c % 4 < 2) ? pktMat : packetMats[(s + 1) % packetMats.Length];

                // Front packet (slightly tilted back)
                var pktF = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pktF.name = $"Packet_F_{s}_{c}";
                pktF.transform.SetParent(rack.transform, false);
                pktF.transform.localPosition = new Vector3(px, sy + 0.17f, depth * 0.20f);
                pktF.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
                pktF.transform.localScale = new Vector3(0.19f, 0.28f, 0.07f);
                pktF.GetComponent<Renderer>().sharedMaterial = m;

                // Back packet
                var pktB = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pktB.name = $"Packet_B_{s}_{c}";
                pktB.transform.SetParent(rack.transform, false);
                pktB.transform.localPosition = new Vector3(px, sy + 0.17f, -depth * 0.05f);
                pktB.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
                pktB.transform.localScale = new Vector3(0.19f, 0.28f, 0.07f);
                pktB.GetComponent<Renderer>().sharedMaterial = m;
            }
        }
    }

    static void BuildColdDrinkFridge(
        GameObject parent, string name, Vector3 pos, float rotY,
        Material mBody, Material mHeader, Material mDark, Material mGlass)
    {
        var fridge = new GameObject(name);
        fridge.transform.SetParent(parent.transform, false);
        fridge.transform.position = pos;
        fridge.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

        float w = 1.05f, h = 2.15f, d = 0.78f;

        // Outer shell (sides sit cleanly between Base and TopBanner to avoid Z-fighting)
        float midH = h - 0.64f;
        float midY = 0.32f + midH * 0.5f;
        Box(fridge, "Back",      new Vector3(0f, midY, -d * 0.5f + 0.04f), new Vector3(w, midH, 0.08f), mBody);
        Box(fridge, "SideL",     new Vector3(-w * 0.5f + 0.04f, midY, 0f), new Vector3(0.08f, midH, d), mBody);
        Box(fridge, "SideR",     new Vector3( w * 0.5f - 0.04f, midY, 0f), new Vector3(0.08f, midH, d), mBody);
        Box(fridge, "Base",      new Vector3(0f, 0.16f, 0f),               new Vector3(w, 0.32f, d), mDark);
        Box(fridge, "TopBanner", new Vector3(0f, h - 0.16f, 0f),           new Vector3(w, 0.32f, d), mHeader);

        // Glowing cold interior light
        var lGo = new GameObject("Fridge_Glow");
        lGo.transform.SetParent(fridge.transform, false);
        lGo.transform.localPosition = new Vector3(0f, h - 0.45f, 0.15f);
        var l = lGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 3.2f;
        l.intensity = 1.4f;
        l.color = new Color(0.80f, 0.94f, 1.0f);

        // Drink colors (Cola dark/red, Sprite green, Fanta orange, Pepsi blue, Mango yellow)
        Material[] drinkMats = {
            SolidMat("M_Drink_Red",    new Color(0.82f, 0.10f, 0.10f)),
            SolidMat("M_Drink_Green",  new Color(0.10f, 0.72f, 0.22f)),
            SolidMat("M_Drink_Orange", new Color(0.95f, 0.45f, 0.06f)),
            SolidMat("M_Drink_Blue",   new Color(0.10f, 0.38f, 0.88f)),
            SolidMat("M_Drink_Yellow", new Color(0.95f, 0.78f, 0.10f))
        };

        // 4 interior shelves with bottles & cans
        float[] shelfY = { 0.40f, 0.80f, 1.20f, 1.56f };
        for (int s = 0; s < shelfY.Length; s++)
        {
            float y = shelfY[s];
            Box(fridge, $"FridgeShelf_{s}", new Vector3(0f, y, -0.02f), new Vector3(w - 0.16f, 0.03f, d - 0.16f), mDark);

            Material dm = drinkMats[s % drinkMats.Length];
            for (int c = 0; c < 6; c++)
            {
                float bx = -0.34f + c * 0.135f;
                for (int r = 0; r < 2; r++)
                {
                    float bz = 0.16f - r * 0.22f;
                    var bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    bottle.name = $"Drink_{s}_{c}_{r}";
                    bottle.transform.SetParent(fridge.transform, false);
                    bottle.transform.localPosition = new Vector3(bx, y + 0.14f, bz);
                    bottle.transform.localScale = new Vector3(0.08f, 0.12f, 0.08f);
                    bottle.GetComponent<Renderer>().sharedMaterial = (c % 2 == 0) ? dm : drinkMats[(s + 2) % drinkMats.Length];
                }
            }
        }

        // Front Glass Door Frame & Handle
        Box(fridge, "GlassDoor", new Vector3(0f, 1.08f, d * 0.5f - 0.02f), new Vector3(w - 0.12f, h - 0.68f, 0.02f), mGlass);
        Box(fridge, "DoorHandle", new Vector3(-w * 0.5f + 0.12f, 1.1f, d * 0.5f + 0.02f), new Vector3(0.04f, 0.45f, 0.04f), mDark);
    }

    static void BuildIceCreamFreezer(
        GameObject parent, string name, Vector3 pos, float rotY,
        Material mBody, Material mAccent, Material mIce, Material mGlass)
    {
        var fz = new GameObject(name);
        fz.transform.SetParent(parent.transform, false);
        fz.transform.position = pos;
        fz.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

        float w = 1.85f, h = 0.92f, d = 0.85f;

        // Insulated chest walls surrounding hollow frosty tub pit (no corner overlap)
        Box(fz, "Bottom",  new Vector3(0f, 0.24f, 0f),                     new Vector3(w - 0.02f, 0.48f, d - 0.02f), mBody);
        Box(fz, "Wall_N",  new Vector3(0f, h * 0.5f, -d * 0.5f + 0.05f),   new Vector3(w, h, 0.10f), mBody);
        Box(fz, "Wall_S",  new Vector3(0f, h * 0.5f,  d * 0.5f - 0.05f),   new Vector3(w, h, 0.10f), mBody);
        Box(fz, "Wall_W",  new Vector3(-w * 0.5f + 0.05f, h * 0.5f, 0f),   new Vector3(0.10f, h, d - 0.20f), mBody);
        Box(fz, "Wall_E",  new Vector3( w * 0.5f - 0.05f, h * 0.5f, 0f),   new Vector3(0.10f, h, d - 0.20f), mBody);
        // Red/Brand Rim Strip
        Box(fz, "FrontStripe", new Vector3(0f, 0.72f, d * 0.5f + 0.005f),  new Vector3(w - 0.02f, 0.14f, 0.01f), mAccent);
        // Frosty interior base
        Box(fz, "IceFloor", new Vector3(0f, 0.50f, 0f),                    new Vector3(w - 0.22f, 0.04f, d - 0.22f), mIce);

        // Ice cream tubs & bars inside the freezer
        Material[] icMats = {
            SolidMat("M_IC_Vanilla",    new Color(0.98f, 0.94f, 0.80f)),
            SolidMat("M_IC_Choco",      new Color(0.36f, 0.20f, 0.10f)),
            SolidMat("M_IC_Strawberry", new Color(0.95f, 0.48f, 0.62f)),
            SolidMat("M_IC_Mango",      new Color(0.98f, 0.76f, 0.12f)),
            SolidMat("M_IC_Pista",      new Color(0.55f, 0.85f, 0.45f))
        };

        // Row 1: Ice cream family-pack tubs
        for (int i = 0; i < 5; i++)
        {
            float tx = -0.64f + i * 0.32f;
            Box(fz, $"IceCreamTub_{i}",
                new Vector3(tx, 0.65f, -0.14f),
                new Vector3(0.26f, 0.24f, 0.24f), icMats[i % icMats.Length]);
        }
        // Row 2: Ice cream bar boxes & cones
        for (int i = 0; i < 6; i++)
        {
            float tx = -0.68f + i * 0.27f;
            Box(fz, $"IceCreamBox_{i}",
                new Vector3(tx, 0.63f, 0.16f),
                new Vector3(0.20f, 0.20f, 0.22f), icMats[(i + 2) % icMats.Length]);
        }

        // Top sliding glass cover (slightly angled/recessed inside rim)
        Box(fz, "SlidingGlassLid", new Vector3(0f, h - 0.03f, 0f), new Vector3(w - 0.18f, 0.02f, d - 0.18f), mGlass);
        Box(fz, "LidDivider",      new Vector3(0f, h - 0.01f, 0f), new Vector3(0.05f, 0.03f, d - 0.14f), mBody);
    }

    static void BuildTuckCounter(
        GameObject parent, Vector3 pos,
        Material mWood, Material mSteel, Material mGlass, Material[] chipMats)
    {
        var cnt = new GameObject("TuckShop_ServiceCounter");
        cnt.transform.SetParent(parent.transform, false);
        cnt.transform.position = pos;

        // Desk along Z (facing West walkway)
        Box(cnt, "Desk_Body", new Vector3(0f, 0.50f, 0f), new Vector3(0.75f, 1.00f, 3.8f), mWood);
        Box(cnt, "Desk_Top",  new Vector3(0f, 1.03f, 0f), new Vector3(0.90f, 0.06f, 3.9f), mSteel);

        // Countertop snack display jars & hanging chip bags
        for (int i = 0; i < 4; i++)
        {
            float pz = -1.3f + i * 0.85f;
            // Clear jar with snacks inside
            var jar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            jar.name = $"SnackJar_{i}";
            jar.transform.SetParent(cnt.transform, false);
            jar.transform.localPosition = new Vector3(-0.12f, 1.24f, pz);
            jar.transform.localScale = new Vector3(0.24f, 0.18f, 0.24f);
            jar.GetComponent<Renderer>().sharedMaterial = mGlass;

            // Kurkure/Lays packets stacked on counter beside jars
            Box(cnt, $"CounterPacket_{i}",
                new Vector3(0.15f, 1.18f, pz + 0.25f),
                new Vector3(0.08f, 0.24f, 0.18f), chipMats[i % chipMats.Length]);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // PIXEL-ART TEXTURE GENERATORS
    // ════════════════════════════════════════════════════════════════════

    static string MakeKurkureTex(string path, Color bg, Color accent)
    {
        int w = 32, h = 48;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = bg;
                // Silver crimped packet top/bottom edges
                if (y < 3 || y >= h - 3)
                    c = new Color(0.82f, 0.82f, 0.84f);
                // Side shading
                else if (x < 2 || x >= w - 2)
                    c = bg * 0.72f;
                // Diagonal yellow brand ribbon
                else if (y >= 24 && y <= 34 && ((x + y) % 5 != 0))
                    c = accent;
                // Dark bold text blocks inside ribbon
                if (y >= 27 && y <= 31 && x >= 5 && x <= 26 && (x % 3 != 0))
                    c = new Color(0.45f, 0.05f, 0.02f);
                // Crunchy masala puff illustrations at bottom
                if (y >= 7 && y <= 18 && x >= 7 && x <= 24)
                {
                    if ((x + y) % 4 == 0) c = new Color(0.95f, 0.55f, 0.12f);
                    if ((x * 2 + y) % 7 == 0) c = new Color(0.70f, 0.12f, 0.05f);
                }
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return SavePixelTex(tex, path, 64);
    }

    static string MakeLaysTex(string path, Color bg)
    {
        int w = 32, h = 48;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Vector2 logoCenter = new Vector2(16f, 29f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = bg;
                if (y < 3 || y >= h - 3)
                    c = new Color(0.84f, 0.84f, 0.86f);
                else if (x < 2 || x >= w - 2)
                    c = bg * 0.75f;

                float dist = Vector2.Distance(new Vector2(x, y), logoCenter);
                if (dist < 7.5f)
                {
                    c = new Color(0.98f, 0.82f, 0.10f); // Yellow circle
                    if (y >= 27 && y <= 31) c = new Color(0.85f, 0.10f, 0.10f); // Red ribbon
                    if (y >= 28 && y <= 30 && x >= 11 && x <= 21 && x % 2 == 0) c = Color.white; // Lays text pixels
                }
                // Golden potato chip pixels at lower half
                if (y >= 7 && y <= 18 && x >= 6 && x <= 25 && ((x + y * 2) % 3 == 0))
                    c = new Color(0.96f, 0.82f, 0.25f);

                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return SavePixelTex(tex, path, 64);
    }

    static string MakeIdealPosterTex(string path, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color purple  = new Color(0.46f, 0.22f, 0.56f);
        Color purpleD = new Color(0.38f, 0.16f, 0.48f);
        Color cream   = new Color(0.96f, 0.94f, 0.90f);
        Color waffle  = new Color(0.76f, 0.54f, 0.30f);
        Color choco   = new Color(0.32f, 0.18f, 0.10f);
        Color mango   = new Color(0.96f, 0.78f, 0.10f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = ((x + y) % 16 == 0) ? purpleD : purple;

                // Bottom white strip (seen in photo)
                if (y < 9)
                {
                    c = cream;
                    if (y >= 2 && y <= 6 && x >= 65 && x <= 115 && (x % 4 != 0))
                        c = (x > 92) ? new Color(0.75f, 0.15f, 0.15f) : new Color(0.15f, 0.15f, 0.2f);
                }

                // White border trim
                if (x <= 1 || x >= w - 2 || y >= h - 2)
                    c = cream;

                // Top-left green veg icon
                if (x >= 5 && x <= 8 && y >= h - 8 && y <= h - 5)
                    c = (x >= 6 && x <= 7 && y >= h - 7 && y <= h - 6) ? new Color(0.1f, 0.65f, 0.2f) : Color.white;

                // Top-right small red "ideal" oval
                if (Vector2.Distance(new Vector2(x, y), new Vector2(w - 14, h - 8)) < 4.5f)
                    c = new Color(0.85f, 0.14f, 0.16f);

                // Bottom-left iconic white teardrop/loop "d" logo
                float dOuter = Vector2.Distance(new Vector2(x, y), new Vector2(20, 22));
                float dInner = Vector2.Distance(new Vector2(x, y), new Vector2(20, 22));
                if ((dOuter <= 10f && dInner >= 5.5f) || (x >= 25 && x <= 29 && y >= 22 && y <= 38))
                    c = Color.white;

                // Bold Kannada script block row ("ಐಡಿಯಲ್") in upper-mid
                if (y >= 46 && y <= 57 && x >= 44 && x <= 94)
                {
                    int gx = (x - 44) % 9;
                    int gy = y - 46;
                    if (gx >= 1 && gx <= 7 && (gy == 0 || gy == 11 || gx == 1 || gx == 7 || (gy == 5 && gx > 3)))
                        c = Color.white;
                }

                // "ideal" crisp pixel word in middle
                DrawPixelWordIdeal(x, y, 52, 28, ref c, Color.white);

                // "SINCE 1975" small white pixel dashes below
                if (y >= 20 && y <= 22 && x >= 58 && x <= 84 && x % 2 == 0)
                    c = Color.white;

                // Right-side 3 ice creams:
                // 1) Waffle cone (leftmost of trio)
                if (y >= 10 && y <= 32)
                {
                    float halfW = (y - 9) * 0.36f;
                    if (Mathf.Abs(x - 108) <= halfW)
                        c = ((x + y) % 4 == 0) ? choco : waffle;
                }
                if (Vector2.Distance(new Vector2(x, y), new Vector2(108, 36)) <= 9f)
                    c = ((x * 2 + y) % 5 == 0) ? choco : cream;

                // 2) Yellow Mango Popsicle (center)
                if (x >= 117 && x <= 128 && y >= 14 && y <= 46)
                {
                    if (y < 41 || Vector2.Distance(new Vector2(x, y), new Vector2(122.5f, 41f)) <= 5.5f)
                        c = (x < 120) ? new Color(1f, 0.90f, 0.35f) : mango;
                }

                // 3) Chocolate Almond Bar (right)
                if (x >= 129 && x <= 141 && y >= 12 && y <= 47)
                {
                    if (y < 42 || Vector2.Distance(new Vector2(x, y), new Vector2(135f, 42f)) <= 6f)
                    {
                        c = choco;
                        if (Vector2.Distance(new Vector2(x, y), new Vector2(137f, 41f)) <= 3.5f)
                            c = cream; // bitten corner showing vanilla ice cream inside!
                        else if ((x + y * 3) % 11 == 0)
                            c = waffle; // almond bits
                    }
                }

                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return SavePixelTex(tex, path, 256);
    }

    static void DrawPixelWordIdeal(int x, int y, int ox, int oy, ref Color c, Color fg)
    {
        // 5-letter crisp 5x9 blocks for "ideal" (scaled 2x -> 10px height)
        int lx = x - ox;
        int ly = y - oy;
        if (ly < 0 || ly > 12 || lx < 0 || lx > 42) return;

        // i
        if (lx >= 0 && lx <= 2 && (ly <= 8 || ly >= 10)) c = fg;
        // d
        if ((lx >= 6 && lx <= 12 && (ly == 0 || ly == 1 || ly == 6 || ly == 7)) ||
            (lx >= 11 && lx <= 13 && ly <= 12) || (lx >= 6 && lx <= 8 && ly <= 7)) c = fg;
        // e
        if ((lx >= 16 && lx <= 22 && (ly == 0 || ly == 1 || ly == 4 || ly == 7 || ly == 8)) ||
            (lx >= 16 && lx <= 18 && ly <= 8) || (lx >= 20 && lx <= 22 && ly >= 4 && ly <= 8)) c = fg;
        // a
        if ((lx >= 25 && lx <= 31 && (ly == 0 || ly == 1 || ly == 4 || ly == 7 || ly == 8)) ||
            (lx >= 29 && lx <= 31 && ly <= 8) || (lx >= 25 && lx <= 27 && ly <= 4)) c = fg;
        // l
        if (lx >= 34 && lx <= 36 && ly <= 12) c = fg;
    }

    static string MakeIdealPillarTex(string path, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color purple = new Color(0.46f, 0.22f, 0.56f);
        Color cream  = new Color(0.96f, 0.94f, 0.90f);
        Color waffle = new Color(0.76f, 0.54f, 0.30f);
        Color choco  = new Color(0.32f, 0.18f, 0.10f);
        Color mango  = new Color(0.96f, 0.78f, 0.10f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = (y < 26) ? cream : purple;

                // Top small white text band
                if (y >= 72 && y <= 84 && x >= 10 && x <= 38 && (x % 3 != 0) && (y % 4 != 0))
                    c = Color.white;

                // Middle white Ideal loop "d" logo
                float dOuter = Vector2.Distance(new Vector2(x, y), new Vector2(24, 44));
                if ((dOuter <= 9f && dOuter >= 5f) || (x >= 29 && x <= 32 && y >= 44 && y <= 58))
                    c = Color.white;

                // Bottom ice cream cone & mango bar on white base
                if (y >= 4 && y <= 20)
                {
                    if (x >= 12 && x <= 22 && y <= 14) c = waffle;
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(17, 17)) <= 5f) c = choco;
                    if (x >= 24 && x <= 32 && y >= 5 && y <= 21) c = mango;
                }

                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return SavePixelTex(tex, path, 128);
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
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = maxSize;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        return assetPath;
    }

    // ════════════════════════════════════════════════════════════════════
    // MATERIAL / PRIMITIVE HELPERS
    // ════════════════════════════════════════════════════════════════════

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

    static Material SolidMat(string name, Color col)
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
        m.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material TexMat(string name, string texPath)
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
        m.color = Color.white;
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.08f);
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (t != null)
        {
            m.mainTexture = t;
            m.SetTexture("_BaseMap", t);
            m.mainTextureScale = Vector2.one;
            m.SetTextureScale("_BaseMap", Vector2.one);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material TransparentMat(string name, Color col)
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
