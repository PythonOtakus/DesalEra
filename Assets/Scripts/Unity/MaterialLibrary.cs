using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CrazyAquarium.Unity
{
    /// <summary>
    /// Loads textures from StreamingAssets and builds materials from them.
    ///
    /// Runtime loading rather than imported assets, because the whole scene is
    /// assembled in code. Imported textures would need a scene or prefab to live in,
    /// and this project deliberately avoids those: a .unity file is thousands of lines
    /// of GUID-linked YAML that neither a person nor an agent can edit reliably.
    ///
    /// Everything degrades to a flat colour if a texture is missing, so a failed
    /// download produces a greybox rather than a broken scene.
    /// </summary>
    public sealed class MaterialLibrary : MonoBehaviour
    {
        private const string TextureFolder = "textures";

        [Serializable]
        public struct Palette
        {
            public string Key;
            public string FileName;
            public Color Fallback;
            public float TilingMetres;
            public float Smoothness;
        }

        /// <summary>
        /// The five surfaces the greybox needs. Each is CC0 from ambientCG, chosen for
        /// the flooded-industrial read the design calls for: rusted steel, weathered
        /// decking, old timber, poured concrete, and peeling plaster.
        /// </summary>
        private static readonly Palette[] Defaults =
        {
            new Palette { Key = "rust",    FileName = "rust_Metal063.jpg",      Fallback = new Color(0.42f, 0.26f, 0.18f), TilingMetres = 2f, Smoothness = 0.35f },
            new Palette { Key = "deck",    FileName = "woodfloor_WoodFloor064.jpg", Fallback = new Color(0.45f, 0.36f, 0.24f), TilingMetres = 2f, Smoothness = 0.18f },
            new Palette { Key = "timber",  FileName = "wood_Wood035.jpg",        Fallback = new Color(0.38f, 0.30f, 0.21f), TilingMetres = 2f, Smoothness = 0.16f },
            new Palette { Key = "concrete",FileName = "concrete_Concrete034.jpg", Fallback = new Color(0.48f, 0.47f, 0.45f), TilingMetres = 3f, Smoothness = 0.12f },
            new Palette { Key = "plaster", FileName = "plaster_Plaster001.jpg",  Fallback = new Color(0.55f, 0.53f, 0.48f), TilingMetres = 3f, Smoothness = 0.14f }
        };

        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();

        /// <summary>Files that were expected but could not be loaded, for the HUD.</summary>
        public readonly List<string> MissingTextures = new List<string>();

        private void Awake()
        {
            BuildAll();
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<string, Material> entry in _materials)
            {
                if (entry.Value != null) SafeDestroy(entry.Value);
            }
            foreach (KeyValuePair<string, Texture2D> entry in _textures)
            {
                if (entry.Value != null) SafeDestroy(entry.Value);
            }
        }

        private void BuildAll()
        {
            foreach (Palette palette in Defaults) _materials[palette.Key] = Build(palette);
        }

        private Material Build(Palette palette)
        {
            Texture2D texture = LoadTexture(palette.FileName);
            if (texture == null) MissingTextures.Add(palette.FileName);
            else _textures[palette.FileName] = texture;

            var material = new Material(FindShader()) { name = $"Mat_{palette.Key}" };

            if (texture != null)
            {
                material.mainTexture = texture;
                // Texture tiling is per-material, but the scale is shared, so the
                // texture is left at its own tiling and the mesh UVs carry the scale.
                // That keeps one texture usable at several physical scales.
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", palette.Fallback);
            if (material.HasProperty("_Color")) material.color = palette.Fallback;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", palette.Smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", palette.Smoothness);

            return material;
        }

        private static Shader FindShader()
        {
            // URP first, then the built-in pipeline, then the most basic shader that
            // will still draw something. A missing shader must not black out the scene.
            return Shader.Find("Universal Render Pipeline/Lit")
                   ?? Shader.Find("Standard")
                   ?? Shader.Find("Legacy Shaders/Diffuse")
                   ?? Shader.Find("Unlit/Texture");
        }

        private Texture2D LoadTexture(string fileName)
        {
            string root = Path.Combine(Application.streamingAssetsPath, TextureFolder);
            string full = Path.Combine(root, fileName);
            if (!File.Exists(full)) return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(full);
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, true);
                texture.name = fileName;

                // LoadImage is the one path that works without an image library and
                // keeps the import off the main thread's critical section.
                if (!texture.LoadImage(bytes, markNonReadable: false)) return null;

                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Bilinear;
                texture.anisoLevel = 4;
                return texture;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Returns a material by key, or a neutral grey if the key is unknown.</summary>
        public Material Get(string key)
        {
            if (_materials.TryGetValue(key, out Material material) && material != null) return material;
            return FallbackMaterial();
        }

        private Material _fallback;

        private Material FallbackMaterial()
        {
            if (_fallback == null)
            {
                _fallback = new Material(FindShader()) { name = "Mat_Fallback" };
                if (_fallback.HasProperty("_BaseColor")) _fallback.SetColor("_BaseColor", new Color(0.5f, 0.5f, 0.5f));
                if (_fallback.HasProperty("_Color")) _fallback.color = new Color(0.5f, 0.5f, 0.5f);
            }
            return _fallback;
        }

        private static void SafeDestroy(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
