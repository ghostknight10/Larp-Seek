using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Enforces crisp pixel-art import settings across all world and prop textures:
/// - FilterMode.Point (no bilinear blur)
/// - mipmapEnabled = false (stays sharp up close without blurring at angles)
/// - TextureImporterCompression.Uncompressed (no DXT/BC compression artifacts)
/// - Low/consistent target resolutions
/// </summary>
public class PixelArtTexturePostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string path = assetPath.Replace("\\", "/");
        var importer = (TextureImporter)assetImporter;
        if (importer == null) return;

        // Every project texture (world, props, UI sprites) samples with Point filtering for crisp pixels.
        // Font atlases (TextMesh Pro SDF) are excluded since SDF text needs bilinear sampling to stay legible.
        if (path.StartsWith("Assets/") && !path.Contains("TextMesh Pro") && !path.Contains("/Fonts/"))
        {
            importer.filterMode = FilterMode.Point;
            importer.anisoLevel = 0;
        }

        if (!path.StartsWith("Assets/Textures/") || path.Contains("/UI/"))
            return;

        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.anisoLevel = 0;

        // Set appropriate max resolution:
        // Murals & big posters: 256 or 512
        // World tiles & props: 32 or 64 or 128
        string lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        if (lower.Contains("mural") || lower.Contains("poster") || lower.Contains("menu"))
        {
            importer.maxTextureSize = 256;
        }
        else if (lower.Contains("floor") || lower.Contains("wall") || lower.Contains("brick") || lower.Contains("carrom"))
        {
            importer.maxTextureSize = 64;
        }
        else
        {
            importer.maxTextureSize = 64;
        }
    }

    [MenuItem("LarpSeek/Reimport All Textures (Crisp Pixel Art)")]
    public static void ReimportAllTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Textures" });
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.anisoLevel = 0;
                
                string lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (lower.Contains("mural") || lower.Contains("poster") || lower.Contains("menu"))
                {
                    importer.maxTextureSize = 256;
                }
                else
                {
                    importer.maxTextureSize = 64;
                }
                importer.SaveAndReimport();
                count++;
            }
        }
        AssetDatabase.Refresh();
        Debug.Log($"[PixelArtTexturePostprocessor] Reimported {count} textures with crisp Point filtering, Uncompressed, no Mipmaps.");
    }
}
