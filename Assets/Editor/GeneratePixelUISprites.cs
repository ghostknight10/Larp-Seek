using UnityEngine;
using UnityEditor;
using System.IO;

public static class GeneratePixelUISprites
{
    const string OutDir = "Assets/Textures/UI";

    [MenuItem("LarpSeek/Generate Pixel UI Sprites")]
    public static void GenerateAll()
    {
        if (!Directory.Exists(OutDir))
            Directory.CreateDirectory(OutDir);

        CreateButtonSprite("PixelBtn_Coral_Normal.png",
            outline: new Color32(40, 12, 22, 255),
            highlight: new Color32(255, 174, 193, 255),
            face: new Color32(235, 82, 110, 255),
            shelf: new Color32(163, 31, 58, 255),
            shelfTop: new Color32(195, 45, 75, 255),
            shelfHeight: 5, pressedOffset: 0);

        CreateButtonSprite("PixelBtn_Coral_Hover.png",
            outline: new Color32(40, 12, 22, 255),
            highlight: new Color32(255, 200, 212, 255),
            face: new Color32(248, 107, 133, 255),
            shelf: new Color32(182, 42, 70, 255),
            shelfTop: new Color32(210, 60, 90, 255),
            shelfHeight: 5, pressedOffset: 0);

        CreateButtonSprite("PixelBtn_Coral_Pressed.png",
            outline: new Color32(40, 12, 22, 255),
            highlight: new Color32(235, 82, 110, 255),
            face: new Color32(210, 60, 88, 255),
            shelf: new Color32(140, 24, 48, 255),
            shelfTop: new Color32(163, 31, 58, 255),
            shelfHeight: 2, pressedOffset: 3);

        // Dark Crimson / Quit Button
        CreateButtonSprite("PixelBtn_Quit_Normal.png",
            outline: new Color32(32, 8, 16, 255),
            highlight: new Color32(225, 95, 110, 255),
            face: new Color32(185, 40, 60, 255),
            shelf: new Color32(115, 18, 35, 255),
            shelfTop: new Color32(145, 28, 48, 255),
            shelfHeight: 5, pressedOffset: 0);

        CreateButtonSprite("PixelBtn_Quit_Hover.png",
            outline: new Color32(32, 8, 16, 255),
            highlight: new Color32(245, 120, 135, 255),
            face: new Color32(205, 55, 75, 255),
            shelf: new Color32(135, 25, 45, 255),
            shelfTop: new Color32(165, 38, 58, 255),
            shelfHeight: 5, pressedOffset: 0);

        CreateButtonSprite("PixelBtn_Quit_Pressed.png",
            outline: new Color32(32, 8, 16, 255),
            highlight: new Color32(185, 40, 60, 255),
            face: new Color32(155, 30, 48, 255),
            shelf: new Color32(95, 12, 26, 255),
            shelfTop: new Color32(115, 18, 35, 255),
            shelfHeight: 2, pressedOffset: 3);

        // Panel Card Frame
        CreatePanelSprite("PixelPanel_Card.png",
            outline: new Color32(28, 8, 16, 255),
            innerBorder: new Color32(65, 20, 36, 255),
            topAccent: new Color32(235, 82, 110, 255),
            fill: new Color32(24, 10, 18, 246));

        // Pill Header / Badge Frame
        CreatePanelSprite("PixelBadge_Frame.png",
            outline: new Color32(35, 10, 20, 255),
            innerBorder: new Color32(235, 82, 110, 255),
            topAccent: new Color32(255, 174, 193, 255),
            fill: new Color32(40, 12, 24, 250));

        // Progress Bar Frame & Fill
        CreateProgressBarSprites();

        AssetDatabase.Refresh();

        // Configure Sprite Importers with Point Filtering and 9-Slice Borders
        ConfigureImporter(OutDir + "/PixelBtn_Coral_Normal.png", new Vector4(7, 9, 7, 7));
        ConfigureImporter(OutDir + "/PixelBtn_Coral_Hover.png", new Vector4(7, 9, 7, 7));
        ConfigureImporter(OutDir + "/PixelBtn_Coral_Pressed.png", new Vector4(7, 6, 7, 7));
        ConfigureImporter(OutDir + "/PixelBtn_Quit_Normal.png", new Vector4(7, 9, 7, 7));
        ConfigureImporter(OutDir + "/PixelBtn_Quit_Hover.png", new Vector4(7, 9, 7, 7));
        ConfigureImporter(OutDir + "/PixelBtn_Quit_Pressed.png", new Vector4(7, 6, 7, 7));
        ConfigureImporter(OutDir + "/PixelPanel_Card.png", new Vector4(8, 8, 8, 8));
        ConfigureImporter(OutDir + "/PixelBadge_Frame.png", new Vector4(8, 8, 8, 8));
        ConfigureImporter(OutDir + "/PixelBar_Bg.png", new Vector4(5, 5, 5, 5));
        ConfigureImporter(OutDir + "/PixelBar_Fill.png", new Vector4(3, 3, 3, 3));

        AssetDatabase.Refresh();
        Debug.Log("[GeneratePixelUISprites] Generated all 9-slice pixel UI sprites!");
    }

    static void CreateButtonSprite(string filename, Color32 outline, Color32 highlight, Color32 face, Color32 shelf, Color32 shelfTop, int shelfHeight, int pressedOffset)
    {
        int w = 32;
        int h = 28;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color32 clear = new Color32(0, 0, 0, 0);

        Color32[] pixels = new Color32[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Rounded corner cutouts (1-2 pixels)
                bool isCorner = (x <= 1 && y <= 1) || (x >= w - 2 && y <= 1) || (x <= 1 && y >= h - 2) || (x >= w - 2 && y >= h - 2);
                bool isCornerOuter = (x == 0 && y == 0) || (x == w - 1 && y == 0) || (x == 0 && y == h - 1) || (x == w - 1 && y == h - 1)
                                  || (x == 1 && y == 0) || (x == w - 2 && y == 0) || (x == 0 && y == 1) || (x == w - 1 && y == 1)
                                  || (x == 1 && y == h - 1) || (x == w - 2 && y == h - 1) || (x == 0 && y == h - 2) || (x == w - 1 && y == h - 2);

                if (isCornerOuter)
                {
                    pixels[y * w + x] = clear;
                    continue;
                }

                // Outer border check
                bool isBorder = (x == 0 || x == w - 1 || y == 0 || y == h - 1)
                             || ((x == 1 || x == w - 2) && (y == 1 || y == h - 2));

                if (isBorder)
                {
                    pixels[y * w + x] = outline;
                    continue;
                }

                // Inner content
                int innerY = y - pressedOffset;
                if (innerY <= shelfHeight + 1)
                {
                    if (innerY == shelfHeight + 1)
                        pixels[y * w + x] = shelfTop;
                    else
                        pixels[y * w + x] = shelf;
                }
                else if (y >= h - 3)
                {
                    pixels[y * w + x] = highlight;
                }
                else
                {
                    pixels[y * w + x] = face;
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, filename), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void CreatePanelSprite(string filename, Color32 outline, Color32 innerBorder, Color32 topAccent, Color32 fill)
    {
        int w = 32;
        int h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color32 clear = new Color32(0, 0, 0, 0);

        Color32[] pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Stepped 2-pixel rounded corner cutout
                bool isCornerCut = (x == 0 && y == 0) || (x == w - 1 && y == 0) || (x == 0 && y == h - 1) || (x == w - 1 && y == h - 1)
                                || (x == 1 && y == 0) || (x == w - 2 && y == 0) || (x == 0 && y == 1) || (x == w - 1 && y == 1)
                                || (x == 1 && y == h - 1) || (x == w - 2 && y == h - 1) || (x == 0 && y == h - 2) || (x == w - 1 && y == h - 2);

                if (isCornerCut)
                {
                    pixels[y * w + x] = clear;
                    continue;
                }

                // Dark outline
                bool isOutline = (x <= 1 || x >= w - 2 || y <= 1 || y >= h - 2);
                if (isOutline)
                {
                    pixels[y * w + x] = outline;
                    continue;
                }

                // Inner 1px border
                bool isInnerBorder = (x == 2 || x == w - 3 || y == 2 || y == h - 3);
                if (isInnerBorder)
                {
                    if (y >= h - 4)
                        pixels[y * w + x] = topAccent;
                    else
                        pixels[y * w + x] = innerBorder;
                    continue;
                }

                // Top inner highlight line
                if (y == h - 4)
                {
                    pixels[y * w + x] = new Color32(topAccent.r, topAccent.g, topAccent.b, 120);
                }
                else
                {
                    pixels[y * w + x] = fill;
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, filename), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void CreateProgressBarSprites()
    {
        // 1. Progress Bar Background (24 x 16)
        int w = 24, h = 16;
        var bg = new Texture2D(w, h, TextureFormat.RGBA32, false);
        bg.filterMode = FilterMode.Point;
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 border = new Color32(35, 10, 20, 255);
        Color32 innerDark = new Color32(18, 5, 12, 255);
        Color32 innerBevel = new Color32(45, 14, 28, 255);

        Color32[] bgPix = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool isCorner = (x == 0 && y == 0) || (x == w - 1 && y == 0) || (x == 0 && y == h - 1) || (x == w - 1 && y == h - 1);
                if (isCorner) { bgPix[y * w + x] = clear; continue; }
                bool isEdge = (x == 0 || x == w - 1 || y == 0 || y == h - 1);
                if (isEdge) { bgPix[y * w + x] = border; continue; }
                if (y == 1) bgPix[y * w + x] = innerBevel;
                else bgPix[y * w + x] = innerDark;
            }
        }
        bg.SetPixels32(bgPix);
        bg.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, "PixelBar_Bg.png"), bg.EncodeToPNG());
        Object.DestroyImmediate(bg);

        // 2. Progress Bar Fill (16 x 16)
        var fill = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        fill.filterMode = FilterMode.Point;
        Color32 fillTop = new Color32(255, 185, 200, 255);
        Color32 fillMid = new Color32(235, 82, 110, 255);
        Color32 fillBot = new Color32(163, 31, 58, 255);

        Color32[] fillPix = new Color32[16 * 16];
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                if (y >= 13) fillPix[y * 16 + x] = fillTop;
                else if (y <= 3) fillPix[y * 16 + x] = fillBot;
                else fillPix[y * 16 + x] = fillMid;
            }
        }
        fill.SetPixels32(fillPix);
        fill.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, "PixelBar_Fill.png"), fill.EncodeToPNG());
        Object.DestroyImmediate(fill);
    }

    static void ConfigureImporter(string assetPath, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
}
