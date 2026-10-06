using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DesalEra.EditorTools
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
    ///     DesalEra.EditorTools.CharacterImportConfigurer.ConfigureAll
    /// </summary>
    public static class CharacterImportConfigurer
    {
        private const string ArtRoot = "Assets/Art/Characters";

        /// <summary>
        /// The survivor also lives under Resources so it can be loaded at runtime,
        /// which keeps the character replaceable without touching a scene file.
        /// </summary>
        private static readonly string[] ImportRoots = { ArtRoot, "Assets/Resources" };

        private const int MaxTextureSize = 2048;

        [MenuItem("DesalEra/Configure Character Import Settings")]
        public static void ConfigureAll()
        {
            int models = 0;
            int textures = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", ImportRoots))
            {
                ConfigureModel(guid);
                models++;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", ImportRoots))
            {
                ConfigureTexture(guid);
                textures++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DesalEra] configured {models} models and {textures} textures");
        }

        private static void ConfigureModel(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            bool isAnimationClip = path.IndexOf("/Animations/", StringComparison.OrdinalIgnoreCase) >= 0;

            // Human height in metres. Blender measures this figure at 1.70 m, so it is
            // already metric and must not be rescaled.
            //
            // The first pass set 0.01 on the assumption that Meshy exports in
            // centimetres. That collapsed every vertex to zero, and Unity reported an
            // 11123-vertex mesh whose bounds were all zero and which rendered
            // nothing. Measuring beat assuming.
            importer.globalScale = 1f;
            importer.useFileScale = true;

            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = true;

            // Characters are lit by the scene's directional light; baking occlusion
            // on a mesh that moves relative to the world produces smears.
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;

            // Animation FBXs ship their own copy of the mesh and skeleton. Importing
            // those as meshes would leave a duplicate, invisible body sitting in the
            // scene next to the real one, so only the clips are taken from them.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            if (isAnimationClip)
            {
                importer.importVisibility = false;
                importer.importCameras = false;
            }

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
