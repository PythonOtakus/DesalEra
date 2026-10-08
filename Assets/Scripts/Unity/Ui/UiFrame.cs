using System.IO;
using UnityEngine;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// HUD plates from StreamingAssets timber + rust maps.
    /// Wood is built as wide horizontal boards with photographic grain inside each
    /// board (tiled fill). The riveted metal rim is a separate nine-sliced overlay.
    /// </summary>
    public static class UiFrame
    {
        // CanvasScaler reference PPU is 100 → 1 texel ≈ 1 canvas unit.
        private const float Ppu = 100f;
        private const int PanelSize = 176;
        private const int PanelRim = 16;
        private const int Radius = 8;

        private static Sprite _rimOnly;
        private static Sprite _woodTile;
        private static Texture2D _woodSrc;
        private static Texture2D _metalSrc;
        private static readonly System.Collections.Generic.List<Object> Owned =
            new System.Collections.Generic.List<Object>();

        /// <summary>
        /// Outer-shell riveted metal only. Inner cards/slots stay timber + gold outline.
        /// </summary>
        public static Sprite Framed()
        {
            if (_rimOnly != null) return _rimOnly;
            _rimOnly = BuildRim(PanelSize, PanelRim, rivetStep: 28f);
            return _rimOnly;
        }

        /// <summary>
        /// Wide stained boards with photo grain. Image.Type.Tiled — never nine-slice the fill.
        /// </summary>
        public static Sprite WoodTile()
        {
            if (_woodTile != null) return _woodTile;
            EnsureSources();
            if (_woodSrc == null)
            {
                _woodTile = UiSprites.WoodPanel();
                return _woodTile;
            }

            // ~160 canvas units / tile → slots (~90 px) show 2–3 boards; larger panels tile.
            const int crop = 160;
            const int plankH = 36;
            var tex = new Texture2D(crop, crop, TextureFormat.RGBA32, false)
            {
                name = "UiWoodTile",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[crop * crop];
            int planks = (crop + plankH - 1) / plankH;
            for (int y = 0; y < crop; y++)
            {
                int pi = y / plankH;
                int py = y % plankH;
                bool seam = py <= 1 || py >= plankH - 2;
                // Staggered end-joints like decking.
                int jointA = ((pi * 97) % 64) + 40;
                int jointB = (jointA + 128) % crop;

                // Per-board sample strip from the photo map.
                float vBase = 0.12f + (pi % planks) * (0.70f / planks);
                float plankShade = 0.78f + ((pi * 37) % 5) * 0.045f;

                for (int x = 0; x < crop; x++)
                {
                    float u = (x / (float)crop) * 1.35f + pi * 0.19f;
                    float v = vBase + (py / (float)plankH) * 0.07f;
                    Color c = _woodSrc.GetPixelBilinear(Frac(u), Frac(v));
                    c = BoardStain(c, plankShade);

                    bool joint = py > 3 && py < plankH - 3
                                 && (Mathf.Abs(x - jointA) <= 1 || Mathf.Abs(x - jointB) <= 1);

                    if (seam)
                    {
                        float seamT = py <= 1 ? (2 - py) / 2f : (py - (plankH - 3)) / 2f;
                        seamT = Mathf.Clamp01(seamT);
                        c *= Mathf.Lerp(1f, 0.22f, seamT);
                    }
                    else if (joint)
                    {
                        c *= 0.40f;
                    }
                    else
                    {
                        float bevel = Mathf.Clamp01(py / 6f);
                        c = Color.Lerp(c * 0.72f, c, bevel);
                    }

                    pixels[y * crop + x] = To32(c, 0.96f);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            _woodTile = Sprite.Create(tex, new Rect(0, 0, crop, crop), new Vector2(0.5f, 0.5f),
                                      Ppu, 0, SpriteMeshType.FullRect);
            _woodTile.name = "UiWoodTile";
            _woodTile.hideFlags = HideFlags.HideAndDontSave;
            Owned.Add(tex);
            Owned.Add(_woodTile);
            return _woodTile;
        }

        private static Sprite BuildRim(int size, int rimWidth, float rivetStep)
        {
            EnsureSources();
            var pixels = new Color32[size * size];
            float inset = rimWidth;
            float r = Radius;

            for (int y = 0; y < size; y++)
            {
                // Top-lit channel: upper lip reads brighter (machined steel).
                float topLit = Mathf.Lerp(0.72f, 1.18f, y / (float)(size - 1));

                for (int x = 0; x < size; x++)
                {
                    float outer = RoundDist(x, y, size, r);
                    float inner = RoundDist(x, y, size, r, inset);
                    float aOuter = Mathf.Clamp01(1f - outer);
                    float aInner = Mathf.Clamp01(1f - inner);

                    if (aOuter <= 0f || aInner >= 0.99f)
                    {
                        if (aOuter <= 0f) pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    Color metal = SampleMetal(x, y, size);

                    // Across-channel: dark body, thin bright outer lip, darker inner step.
                    float across = Mathf.Clamp01((aOuter - aInner) / Mathf.Max(0.001f, aOuter));
                    float profile = 1f;
                    if (across > 0.82f)
                        profile = Mathf.Lerp(1.0f, 1.85f, (across - 0.82f) / 0.18f);
                    else if (across < 0.32f)
                        profile = Mathf.Lerp(0.55f, 0.95f, across / 0.32f);

                    float fillet = Mathf.Clamp01(1.15f - Mathf.Abs(outer)) * 0.55f;
                    metal = metal * (profile * topLit)
                          + new Color(0.62f, 0.64f, 0.68f) * fillet;

                    pixels[y * size + x] = To32(metal, Mathf.Clamp01(aOuter) * 0.99f);
                }
            }

            StampEdgeRivets(pixels, size, rimWidth, rivetStep);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "UiRim",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            float border = rimWidth + 1f;
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                       Ppu, 0, SpriteMeshType.FullRect,
                                       new Vector4(border, border, border, border));
            sprite.name = "UiRim";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Owned.Add(tex);
            Owned.Add(sprite);
            return sprite;
        }

        private static void StampEdgeRivets(Color32[] pixels, int size, int rimWidth, float step)
        {
            float inset = rimWidth * 0.52f;
            float radius = rimWidth * 0.22f; // compact heads on dark channel
            float lo = inset;
            float hi = size - inset;

            float cornerR = radius * 1.15f;
            StampRivet(pixels, size, lo, lo, cornerR);
            StampRivet(pixels, size, hi, lo, cornerR);
            StampRivet(pixels, size, lo, hi, cornerR);
            StampRivet(pixels, size, hi, hi, cornerR);

            for (float t = lo + step; t < hi - step * 0.35f; t += step)
            {
                StampRivet(pixels, size, t, lo, radius);
                StampRivet(pixels, size, t, hi, radius);
                StampRivet(pixels, size, lo, t, radius);
                StampRivet(pixels, size, hi, t, radius);
            }
        }

        private static void StampRivet(Color32[] pixels, int size, float cx, float cy, float radius)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 2));
            int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(cx + radius + 2));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 2));
            int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(cy + radius + 2));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                if (d > radius + 1.2f) continue;

                Color32 prev = pixels[y * size + x];
                if (prev.a < 8) continue;

                // Tight dome: silver head, thin dark ring — no large black halo.
                float ring = Mathf.Clamp01((d - radius * 0.55f) / (radius * 0.55f));
                float dome = Mathf.Clamp01(1f - d / (radius * 0.85f));
                Color head = new Color(0.70f, 0.72f, 0.76f);
                Color tip = new Color(0.88f, 0.90f, 0.93f);
                Color collar = new Color(0.12f, 0.12f, 0.13f);
                Color rivet = Color.Lerp(head, tip, dome * dome);
                rivet = Color.Lerp(rivet, collar, ring * 0.85f);
                float a = Mathf.Clamp01(1.1f - d / (radius + 0.6f));

                Color blended = Color.Lerp(From32(prev), rivet, a * 0.95f);
                pixels[y * size + x] = To32(blended, prev.a / 255f);
            }
        }

        private static Color BoardStain(Color c, float shade)
        {
            float lum = c.r * 0.28f + c.g * 0.55f + c.b * 0.17f;
            float contrasted = Mathf.Clamp01((lum - 0.40f) * 1.65f + 0.40f);
            Color wood = Color.Lerp(new Color(contrasted, contrasted, contrasted), c, 0.50f);
            // Dark stained deck boards from the mock — warm, not charcoal mush.
            wood = new Color(wood.r * 0.98f, wood.g * 0.72f, wood.b * 0.42f, 1f);
            wood *= shade;
            return wood;
        }

        private static Color SampleMetal(int x, int y, int size)
        {
            // Mock: dark weathered steel channel, not bright chrome ribbon.
            if (_metalSrc == null)
                return new Color(0.22f, 0.23f, 0.25f, 1f);

            float u = (x / (float)size) * 2.8f + 0.05f;
            float v = (y / (float)size) * 2.8f + 0.09f;
            Color c = _metalSrc.GetPixelBilinear(Frac(u), Frac(v));

            float grey = c.r * 0.28f + c.g * 0.40f + c.b * 0.32f;
            Color steel = new Color(
                Mathf.Clamp01(grey * 0.38f + 0.10f),
                Mathf.Clamp01(grey * 0.40f + 0.11f),
                Mathf.Clamp01(grey * 0.44f + 0.12f), 1f);
            float rust = Mathf.Clamp01((c.r - c.b) * 1.6f);
            Color rustTint = new Color(0.42f, 0.24f, 0.12f, 1f);
            return Color.Lerp(steel, Color.Lerp(steel, rustTint, 0.5f), rust * 0.40f);
        }

        private static float RoundDist(int x, int y, int size, float radius, float inset = 0f)
        {
            var half = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
            float r = Mathf.Max(1f, radius - inset * 0.12f);
            var pad = new Vector2(half.x - r - inset, half.y - r - inset);
            if (pad.x < 0f) pad.x = 0f;
            if (pad.y < 0f) pad.y = 0f;
            var p = new Vector2(x, y) - half;
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - pad;
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
                 + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        private static void EnsureSources()
        {
            // Board-scale grain (Wood035) reads better as wide UI planks than fine parquet.
            if (_woodSrc == null) _woodSrc = Load("wood_Wood035.jpg") ?? Load("woodfloor_WoodFloor064.jpg");
            if (_metalSrc == null) _metalSrc = Load("rust_Metal063.jpg");
        }

        private static Texture2D Load(string fileName)
        {
            string full = Path.Combine(Application.streamingAssetsPath, "textures", fileName);
            if (!File.Exists(full)) return null;
            try
            {
                byte[] bytes = File.ReadAllBytes(full);
                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!tex.LoadImage(bytes, markNonReadable: false)) return null;
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.filterMode = FilterMode.Bilinear;
                tex.hideFlags = HideFlags.HideAndDontSave;
                Owned.Add(tex);
                return tex;
            }
            catch
            {
                return null;
            }
        }

        private static float Frac(float t) => t - Mathf.Floor(t);

        private static Color32 To32(Color c, float a)
        {
            return new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f),
                               (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(a) * 255f));
        }

        private static Color From32(Color32 c)
        {
            return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
        }

        public static void Release()
        {
            foreach (Object o in Owned)
            {
                if (o != null) Object.DestroyImmediate(o);
            }
            Owned.Clear();
            _rimOnly = null;
            _woodTile = null;
            _woodSrc = null;
            _metalSrc = null;
        }
    }
}
