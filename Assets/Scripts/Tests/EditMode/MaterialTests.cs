using System.Collections.Generic;
using System.IO;
using DesalEra.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace DesalEra.Tests
{
    /// <summary>
    /// Tests for the material and mesh plumbing.
    ///
    /// These exist because of a specific failure that shipped: the files named as the
    /// five surface textures were the ambientCG *preview sphere renders* that sit in the
    /// same zip as the real maps. Everything loaded, nothing errored, and the raft was
    /// textured with photographs of a ball. A test that only checks "the file is there"
    /// passes on that, so these check what the pixels actually are.
    /// </summary>
    public sealed class MaterialTests
    {
        private const string TextureFolder = "textures";

        private static string TexturePath(string fileName)
        {
            return Path.Combine(Application.streamingAssetsPath, TextureFolder, fileName);
        }

        private static MaterialLibrary BuildLibrary()
        {
            var host = new GameObject("materialHost");
            MaterialLibrary library = host.AddComponent<MaterialLibrary>();
            library.Rebuild();
            return library;
        }

        // --- file naming ---

        [Test]
        public void Channel_InsertsBeforeTheExtension()
        {
            Assert.AreEqual("rust_Metal063_NormalGL.jpg",
                            MaterialLibrary.Channel("rust_Metal063.jpg", "NormalGL"),
                            "a channel map is a sibling of the albedo, named by channel");
        }

        [Test]
        public void Channel_KeepsTheExtensionItWasGiven()
        {
            // The lookup is by name, not by convention, so an asset kept as png must not
            // be silently searched for as a jpg.
            Assert.AreEqual("deck_WoodFloor064_Roughness.png",
                            MaterialLibrary.Channel("deck_WoodFloor064.png", "Roughness"));
        }

        [Test]
        public void Channel_HandlesAMissingExtension()
        {
            Assert.AreEqual("rust_Metal063_Roughness.jpg",
                            MaterialLibrary.Channel("rust_Metal063", "Roughness"));
        }

        [Test]
        public void Channel_RejectsNullInput()
        {
            Assert.IsNull(MaterialLibrary.Channel(null, "Roughness"),
                "an unknown palette row must not become a file named \"_Roughness.jpg\"");
        }

        // --- the shipped texture set ---

        [Test]
        public void EveryPaletteSurfaceHasItsFullChannelSet()
        {
            var host = new GameObject("materialHost");
            try
            {
                MaterialLibrary library = host.AddComponent<MaterialLibrary>();
                library.Rebuild();

                Assert.IsEmpty(library.MissingTextures,
                    "surfaces shipped without a channel: " + string.Join(", ", library.MissingTextures));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [TestCase("rust_Metal063")]
        [TestCase("wood_Wood035")]
        [TestCase("woodfloor_WoodFloor064")]
        [TestCase("concrete_Concrete034")]
        [TestCase("plaster_Plaster001")]
        public void NormalMapsLookLikeNormalMaps(string baseName)
        {
            string path = TexturePath(MaterialLibrary.Channel(baseName + ".jpg", "NormalGL"));
            Assert.IsTrue(File.Exists(path), path + " is missing");

            // A tangent-space normal map is overwhelmingly blue: flat areas sit near
            // (128,128,255). An albedo or a lit preview render has no such bias, which is
            // what makes this able to tell a real normal map from a wrong file.
            Color32 mean = MeanChannel(path);
            Assert.Greater(mean.b, mean.r + 24,
                $"{baseName} normal map is not normal-map shaped (r={mean.r} b={mean.b}); " +
                "it is probably an albedo or a preview render saved under the wrong name");
        }

        [TestCase("rust_Metal063")]
        [TestCase("wood_Wood035")]
        [TestCase("woodfloor_WoodFloor064")]
        [TestCase("concrete_Concrete034")]
        [TestCase("plaster_Plaster001")]
        public void AlbedosAreNotPreviewRenders(string baseName)
        {
            string path = TexturePath(baseName + ".jpg");
            Assert.IsTrue(File.Exists(path), path + " is missing");

            // The ambientCG preview renders are photographs of a sphere on a white
            // backdrop: a large fraction of near-pure-white pixels. A tileable surface
            // has no such region.
            float whiteFraction = WhiteFraction(path, 244);
            Assert.Less(whiteFraction, 0.06f,
                $"{baseName}.jpg looks like a preview render: {whiteFraction:P0} of its pixels are near-white, " +
                "so it is being tiled as if it were a surface");
        }

        [Test]
        public void EveryShippedTextureIsBigEnoughToReadAtGameDistance()
        {
            foreach (string fileName in ShippedTextures())
            {
                Texture2D texture = Load(fileName);
                try
                {
                    // Not required to be square: ambientCG ships plenty of non-square
                    // assets, and the beams carry metric UVs, so a rectangular map wraps
                    // without a seam.
                    Assert.GreaterOrEqual(Mathf.Min(texture.width, texture.height), 512, fileName,
                        "below 512 the grain disappears at the distances this camera actually sits at");
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        [TestCase("rust_Metal063")]
        [TestCase("wood_Wood035")]
        [TestCase("woodfloor_WoodFloor064")]
        [TestCase("concrete_Concrete034")]
        [TestCase("plaster_Plaster001")]
        public void ChannelMapsMatchTheirAlbedoExactly(string baseName)
        {
            // A normal map at a different resolution from its albedo lands on a different
            // texel grid, so the lighting detail slides against the colour underneath it.
            Texture2D albedo = Load(baseName + ".jpg");
            try
            {
                foreach (string channel in new[] { "NormalGL", "Roughness" })
                {
                    string fileName = MaterialLibrary.Channel(baseName + ".jpg", channel);
                    Texture2D map = Load(fileName);
                    try
                    {
                        Assert.AreEqual(albedo.width, map.width, fileName);
                        Assert.AreEqual(albedo.height, map.height, fileName);
                    }
                    finally
                    {
                        Object.DestroyImmediate(map);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(albedo);
            }
        }

        // --- colour space ---

        [Test]
        public void NormalAndRoughnessLoadAsLinearData()
        {
            Texture2D texture = Load("rust_Metal063_NormalGL.jpg", linear: true);
            try
            {
                // sRGB-decoding a normal map tilts every vector off vertical, which reads
                // as dents turned into bumps. LoadImage is free to drop the constructor's
                // linear flag, so this is checked rather than assumed.
                Assert.IsFalse(GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat),
                    "a normal map must not be sampled through an sRGB decode");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void AlbedoLoadsAsColour()
        {
            Texture2D texture = Load("rust_Metal063.jpg", linear: false);
            try
            {
                Assert.IsTrue(GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat),
                    "albedo is the one channel that must stay sRGB, or the whole palette shifts");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void GlossMapPutsInvertedRoughnessInAlpha()
        {
            // The built-in Standard shader and URP/Lit both read smoothness from the alpha
            // of the gloss map, inverted. A grayscale roughness jpg has alpha 1 everywhere,
            // so passing it through unchanged renders the whole raft as polished chrome.
            var host = new GameObject("materialHost");
            try
            {
                MaterialLibrary library = host.AddComponent<MaterialLibrary>();
                library.Rebuild();

                Material material = library.Get("rust");
                var gloss = (Texture2D)(material.HasProperty("_GlossinessMap")
                    ? material.GetTexture("_GlossinessMap")
                    : material.GetTexture("_MetallicGlossMap"));
                Assert.IsNotNull(gloss, "no gloss map was bound at all");

                Texture2D source = Load("rust_Metal063_Roughness.jpg", linear: true);
                try
                {
                    Assert.AreEqual(source.width, gloss.width, "gloss map and roughness map disagree on width");
                    Assert.AreEqual(source.height, gloss.height, "gloss map and roughness map disagree on height");

                    Color32[] sourcePixels = source.GetPixels32();
                    Color32[] glossPixels = gloss.GetPixels32();

                    int mismatches = 0;
                    int samples = 0;
                    int lowest = 255;
                    int highest = 0;

                    // A fixed stride rather than random texels, so a failure is
                    // reproducible instead of depending on the random seed.
                    for (int y = 0; y < source.height; y += 37)
                    {
                        for (int x = 0; x < source.width; x += 37)
                        {
                            int i = y * source.width + x;
                            Color32 from = sourcePixels[i];
                            Color32 to = glossPixels[i];
                            samples++;

                            if (to.a != (byte)(255 - from.r)) mismatches++;
                            if (to.r != 255 || to.g != 255 || to.b != 255) mismatches++;
                            if (to.a < lowest) lowest = to.a;
                            if (to.a > highest) highest = to.a;
                        }
                    }

                    Assert.AreEqual(0, mismatches,
                        $"{mismatches} of {samples} texels are not white RGB with 1 - roughness in alpha");
                    Assert.Greater(highest - lowest, 30,
                        "the gloss map is flat, so the surface cannot vary between matte and polished");
                }
                finally
                {
                    Object.DestroyImmediate(source);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void StressTintStaysOffTheSharedMaterial()
        {
            // BuildMember used to write stress colour straight into the material the
            // library hands out, so every member of a kind inherited the last one's
            // stress and the structural readout said nothing about any individual beam.
            var host = new GameObject("world");
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.Initialise();
                bootstrap.Materials.Rebuild();
                bootstrap.Reanalyse();

                Material steel = bootstrap.Materials.Get("rust");
                // Same slot BuildMember writes; checking _Color on a _BaseColor shader reads
                // a default and reports no tint.
                string slot = steel.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                Color untouched = steel.GetColor(slot);

                var blocks = new List<MaterialPropertyBlock>();
                var renderers = new List<Renderer>();
                foreach (Renderer renderer in host.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.GetComponent<MeshFilter>() == null) continue;
                    if (renderer.sharedMaterial != steel) continue;

                    renderers.Add(renderer);
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    blocks.Add(block);
                }

                Assert.Greater(renderers.Count, 1,
                    "the opening raft needs several members for this to mean anything");

                Color after = steel.GetColor(slot);
                Assert.AreEqual(untouched.r, after.r, 0.0001f,
                    "a member's stress tint was written into the shared material");
                Assert.AreEqual(untouched.b, after.b, 0.0001f,
                    "a member's stress tint was written into the shared material");

                // The tint is deliberately soft (near white at low stress, so the albedo
                // still reads), so the red channel alone can stay at 1; any channel counts.
                bool anyTinted = false;
                foreach (MaterialPropertyBlock block in blocks)
                {
                    if (block.isEmpty) continue;
                    Color tint = block.GetColor(slot);
                    if (Mathf.Abs(tint.r - untouched.r) > 0.002f ||
                        Mathf.Abs(tint.g - untouched.g) > 0.002f ||
                        Mathf.Abs(tint.b - untouched.b) > 0.002f) anyTinted = true;
                }

                Assert.IsTrue(anyTinted,
                    "no member carries its own tint, so the per-member readout is dead");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // --- beam geometry ---

        [Test]
        public void Beam_GivesEveryFaceItsOwnVertices()
        {
            // The beam is a chamfered extrusion, not a box, so the vertex count is not
            // fixed. What matters is that no vertex is shared between faces that point
            // different ways: a shared corner gets an averaged normal and one UV, which
            // smears both the lighting and the grain across the edge.
            Mesh mesh = MeshFactory.Beam(new Vector3(0.2f, 3f, 0.2f), 2f);
            try
            {
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;
                var owner = new Dictionary<int, Vector3>();
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).normalized;
                    for (int k = 0; k < 3; k++)
                    {
                        int index = t[i + k];
                        if (!owner.TryGetValue(index, out Vector3 first)) owner[index] = face;
                        else Assert.Greater(Vector3.Dot(first, face), 0.999f,
                            $"vertex {index} is shared by faces facing {first} and {face}");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Beam_UVsAreMeasuredInMetresPerFace()
        {
            // Four side faces of 0.4 by 3 and two 0.4 square caps. Each has to be measured
            // against its own two dimensions: projecting every face from the same pair of
            // axes is what makes a beam look smeared along its length.
            const float tile = 2f;
            const float thickness = 0.4f;
            const float length = 3f;

            // Sides are unrolled: V runs along the length, U along the perimeter, both in
            // metres / tile. Caps are projected straight down onto their own plane.
            Mesh mesh = MeshFactory.Beam(new Vector3(thickness, length, thickness), tile);
            try
            {
                Vector3[] v = mesh.vertices;
                Vector3[] n = mesh.normals;
                Vector2[] uv = mesh.uv;
                int sides = 0, caps = 0;

                for (int i = 0; i < v.Length; i++)
                {
                    if (Mathf.Abs(n[i].y) < 0.01f)
                    {
                        sides++;
                        float expectedV = v[i].y > 0f ? length / tile : 0f;
                        Assert.AreEqual(expectedV, uv[i].y, 1e-4f, $"side vertex {i} V is not measured along the length");
                    }
                    else if (Mathf.Abs(n[i].y) > 0.99f)
                    {
                        caps++;
                        Assert.AreEqual(v[i].x / tile, uv[i].x, 1e-4f, $"cap vertex {i} U");
                        Assert.AreEqual(v[i].z / tile, uv[i].y, 1e-4f, $"cap vertex {i} V");
                    }
                }

                // Every side quad's U span must equal its edge length in tiles.
                for (int q = 0; q + 3 < sides; q += 4)
                {
                    float edge = Vector3.Distance(v[q], v[q + 1]) / tile;
                    Assert.AreEqual(edge, Mathf.Abs(uv[q + 1].x - uv[q].x), 1e-4f, $"side quad {q / 4} U span");
                }

                Assert.Greater(sides, 0, "no side faces found");
                Assert.Greater(caps, 0, "no cap faces found");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Beam_KeepsGrainTheSameSizeOnEveryMember()
        {
            // Two beams four times apart in length must show the same number of tiles per
            // metre. This is the property the earlier 0-to-1 UVs destroyed.
            const float tile = 2f;
            Mesh shortBeam = MeshFactory.Beam(new Vector3(0.2f, 1f, 0.2f), tile);
            Mesh longBeam = MeshFactory.Beam(new Vector3(0.2f, 4f, 0.2f), tile);
            try
            {
                Assert.AreEqual(4f, MaxUvExtent(longBeam) / MaxUvExtent(shortBeam), 0.001f,
                    "texture density must not depend on how long the beam is");
            }
            finally
            {
                Object.DestroyImmediate(shortBeam);
                Object.DestroyImmediate(longBeam);
            }
        }

        [Test]
        public void Beam_NormalsPointOutwards()
        {
            Mesh mesh = MeshFactory.Beam(new Vector3(0.4f, 2.5f, 0.6f), 2f);
            try
            {
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Assert.AreEqual(vertices.Length, normals.Length,
                    "a vertex without a normal renders unlit, which reads as a hole in the beam");

                for (int i = 0; i < vertices.Length; i++)
                {
                    Assert.Greater(Vector3.Dot(normals[i], vertices[i]), 0f,
                        $"vertex {i} at {vertices[i]} has normal {normals[i]}; the beam would render inside out");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Beam_HasTangentsForTheNormalMap()
        {
            Mesh mesh = MeshFactory.Beam(new Vector3(0.3f, 2f, 0.3f), 2f);
            try
            {
                Assert.AreEqual(mesh.vertexCount, mesh.tangents.Length,
                    "a normal map with no tangent buffer falls back to screen-space derivatives, " +
                    "which seams wherever the UVs are discontinuous");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Beam_UsesTheRequestedTileSize()
        {
            Mesh fine = MeshFactory.Beam(new Vector3(0.3f, 2f, 0.3f), 1f);
            Mesh coarse = MeshFactory.Beam(new Vector3(0.3f, 2f, 0.3f), 4f);
            try
            {
                Assert.AreEqual(4f, MaxUvExtent(fine) / MaxUvExtent(coarse), 0.001f,
                    "the palette's tiling metres must reach the mesh, or it is dead config");
            }
            finally
            {
                Object.DestroyImmediate(fine);
                Object.DestroyImmediate(coarse);
            }
        }

        // --- helpers ---

        private static float MaxUvExtent(Mesh mesh)
        {
            float max = 0f;
            foreach (Vector2 uv in mesh.uv)
            {
                max = Mathf.Max(max, Mathf.Abs(uv.x));
                max = Mathf.Max(max, Mathf.Abs(uv.y));
            }

            return max;
        }

        private static IEnumerable<string> ShippedTextures()
        {
            string root = Path.Combine(Application.streamingAssetsPath, TextureFolder);
            if (!Directory.Exists(root)) yield break;

            foreach (string path in Directory.GetFiles(root, "*.jpg"))
            {
                yield return Path.GetFileName(path);
            }
        }

        /// <summary>Loads a shipped jpg with the same path the library uses.</summary>
        private static Texture2D Load(string fileName, bool linear = false)
        {
            byte[] bytes = File.ReadAllBytes(TexturePath(fileName));
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, true, linear);
            Assert.IsTrue(texture.LoadImage(bytes, markNonReadable: false), fileName + " failed to decode");
            return texture;
        }

        /// <summary>Averages a whole image, sampling it down so this stays quick.</summary>
        private static Color32 MeanChannel(string path)
        {
            var source = new Texture2D(2, 2);
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                source.LoadImage(bytes, markNonReadable: false);

                Color32[] pixels = source.GetPixels32();
                long r = 0;
                long g = 0;
                long b = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    r += pixels[i].r;
                    g += pixels[i].g;
                    b += pixels[i].b;
                }

                int n = Mathf.Max(pixels.Length, 1);
                return new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        private static float WhiteFraction(string path, byte threshold)
        {
            var source = new Texture2D(2, 2);
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                source.LoadImage(bytes, markNonReadable: false);

                Color32[] pixels = source.GetPixels32();
                int white = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].r >= threshold && pixels[i].g >= threshold && pixels[i].b >= threshold) white++;
                }

                return (float)white / Mathf.Max(pixels.Length, 1);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }
    }
}