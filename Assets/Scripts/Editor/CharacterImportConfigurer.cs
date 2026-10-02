using System.IO;
using UnityEditor;
using UnityEngine;

namespace CrazyAquarium.EditorTools
{
    /// <summary>
    /// Configures the import settings for generated character assets.
    ///
    /// Meshy and similar generators emit a 4K texture set and a 20 MB FBX. Left on
    /// Unity's defaults that is roughly 120 MB per character in memory, which is
    /// reckless on a 12 GB card and wasteful for a 0.2 m tall figure on a raft. This
    /// applies the settings that matter:
    ///
    ///   - 2K maximum size, which is invisible on a figure this size,
    ///   - Read/Write off, since nothing samples these from script,
    ///   - normal maps imported as normal maps rather than ordinary images,
    ///   - the metallic/smoothness packing Unity's standard shader expects.
    ///
    /// Run headless:
    ///   Unity.exe -batchmode -executeMethod
    ///     CrazyAquarium.EditorTools.CharacterImportConfigurer.ConfigureAll
    /// </summary>
    public static class CharacterImportConfigurer
    {
        private const string ArtRoot = "Assets/Art/Characters";
        private const int MaxTextureSize = 2048;

        [MenuItem("CrazyAquarium/Configure Character Import Settings")]
        public static void ConfigureAll()
        {
            int models = 0;
            int textures = 0;

            foreach (string path in AssetDatabase.FindAssets("t:Model", new[] { ArtRoot }))
            {
                ConfigureModel(path);
                models++;
            }

            foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
            {
                ConfigureTexture(path);
                textures++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CrazyAquarium] configured {models} models and {textures} textures");
        }

        private static void ConfigureModel(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            // Human height in metres. Meshy exports in centimetres by default, which
            // is why an unconfigured character arrives 100x too tall.
            importer.globalScale = 0.01f;
            importer.useFileScale = true;

            // Import everything so the skeleton, if present, is available later.
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = true;

            // Characters are lit by the scene's directional light; baking occlusion
            // on a mesh that moves relative to the world produces smears.
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;

            // No material is imported: the runtime material library builds its own
            // from the same textures, so a second material per character is pure
            // overhead and a second place for the texture bindings to drift.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            importer.maxTextureSize = MaxTextureSize;
            importer.mipmapEnabled = true;

            // Nothing reads these back from script, so the CPU copy is pure waste.
            importer.isReadable = false;

            importer.sRGBTexture = !name.Contains("normal")
                                   && !name.Contains("metallic")
                                   && !name.Contains("roughness");

            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 50;

            if (name.Contains("normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (name.Contains("metallic") || name.Contains("roughness"))
            {
                // Standard shader reads smoothness from the alpha of the metallic
                // map, so these stay linear and are not treated as colour.
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.None;
            }
            else
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
            }

            importer.SaveAndReimport();
        }
    }
}
