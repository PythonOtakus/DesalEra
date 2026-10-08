using System.Collections.Generic;
using UnityEngine;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Every sprite the UI draws, generated at runtime.
    ///
    /// The project builds its whole world in code and keeps no imported art for it, so
    /// pulling in a PNG atlas for panels would break that rule for no gain: a rounded
    /// rectangle is a distance function, not an asset. It also means the theme can change
    /// border thickness or corner radius without a reimport.
    ///
    /// Everything is cached and generated once. Nothing here runs per frame.
    /// </summary>
    public static class UiSprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly List<Object> Owned = new List<Object>();

        /// <summary>A flat white rectangle. The workhorse: bars, dividers, fills.</summary>
        public static Sprite Solid()
        {
            return Get("solid", 8, 8, s => s.Fill(1f));
        }

        /// <summary>A rounded panel that stretches to any size without distorting.</summary>
        public static Sprite Panel(int radius = -1)
        {
            if (radius < 0) radius = UiTheme.CornerRadius;
            int size = Mathf.Max(8, radius * 2 + 6);
            return Get($"panel{radius}", size, size, s => s.PanelField(radius, 0f));
        }

        /// <summary>A rounded outline, for borders and focus rings.</summary>
        public static Sprite Outline(int radius = -1, int thickness = 2)
        {
            if (radius < 0) radius = UiTheme.CornerRadius;
            int size = Mathf.Max(8, radius * 2 + 6);
            return Get($"outline{radius}_{thickness}", size, size, s => s.PanelField(radius, thickness));
        }

        public static Sprite Circle(int diameter = 24)
        {
            int size = Mathf.Max(4, diameter);
            return Get($"circle{size}", size, size, s =>
            {
                float r = size * 0.5f - 0.5f;
                Vector2 c = new Vector2(r, r);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        s.Set(x, y, 1f - (Vector2.Distance(new Vector2(x, y), c) - r));
            });
        }

        /// <summary>
        /// A readable stand-in for each resource.
        ///
        /// Deliberately a silhouette and a tint rather than real art. An icon that is
        /// subtly the wrong shape costs more to fix later than one that is obviously a
        /// placeholder, and these only have to answer "which of these five is it".
        /// </summary>
        public static Sprite ResourceIcon(DesalEra.Game.ResourceKind kind, int size = 48)
        {
            Color tint = UiTheme.ResourceColor(kind);
            return Get($"res_{kind}_{size}", size, size, s =>
            {
                switch (kind)
                {
                    case DesalEra.Game.ResourceKind.Scrap: ThreeChunks(s, size, tint); break;
                    case DesalEra.Game.ResourceKind.Plank: Plank(s, size, tint); break;
                    case DesalEra.Game.ResourceKind.Metal: Ingot(s, size, tint); break;
                    case DesalEra.Game.ResourceKind.Water: Drum(s, size, tint); break;
                    default: Crate(s, size, tint); break;
                }
            });
        }

        /// <summary>
        /// Dark weathered plank face with grain. Nine-sliced; tint with <see cref="UiTheme.Panel"/>.
        /// </summary>
        public static Sprite WoodPanel(int radius = -1)
        {
            if (radius < 0) radius = UiTheme.CornerRadius;
            int size = 48;
            return Get($"wood{radius}", size, size, s =>
            {
                Color light = new Color(0.42f, 0.30f, 0.18f, 1f);
                Color dark = new Color(0.18f, 0.12f, 0.07f, 1f);
                for (int y = 0; y < size; y++)
                {
                    float fade = Mathf.Lerp(0.95f, 0.70f, y / (float)(size - 1)); // top denser
                    bool seam = (y % 9) == 0;
                    for (int x = 0; x < size; x++)
                    {
                        float wave = 0.5f + 0.5f * Mathf.Sin(y * 0.7f + x * 0.05f)
                                   + 0.15f * Mathf.Sin(x * 1.4f);
                        Color wood = Color.Lerp(dark, light, Mathf.Clamp01(wave));
                        if (seam) wood *= 0.55f;
                        float a = SoftRoundAlpha(x, y, size, radius) * fade;
                        s.Set(x, y, a, wood);
                    }
                }
            });
        }

        /// <summary>Weathered metal rim for themed panels.</summary>
        public static Sprite MetalRim(int radius = -1, int thickness = 3)
        {
            if (radius < 0) radius = UiTheme.CornerRadius;
            int size = 48;
            return Get($"metalrim{radius}_{thickness}", size, size, s =>
            {
                Color metal = new Color(0.85f, 0.82f, 0.74f, 1f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float outer = SoftRoundAlpha(x, y, size, radius);
                        float inner = SoftRoundAlpha(x, y, size, radius, thickness);
                        float a = Mathf.Clamp01(outer - inner);
                        // Scuff highlights along the rim.
                        float scuff = 0.75f + 0.25f * Mathf.Sin(x * 0.7f + y * 0.3f);
                        s.Set(x, y, a * scuff, metal);
                    }
                }
            });
        }

        /// <summary>Rounded well for vital tracks.</summary>
        public static Sprite BarWell(int height = 12)
        {
            int w = 32, h = Mathf.Max(8, height);
            return Get($"barwell{h}", w, h, s =>
            {
                float r = h * 0.5f - 0.5f;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = x < r ? r - x : (x > w - 1 - r ? x - (w - 1 - r) : 0f);
                    float dy = y - (h * 0.5f - 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                    s.Set(x, y, 1f - d);
                }
            });
        }

        public static Sprite VitalIcon(DesalEra.Game.Vital vital, int size = 24)
        {
            return Get($"vital_{vital}_{size}", size, size, s =>
            {
                Color c = Color.white;
                switch (vital)
                {
                    case DesalEra.Game.Vital.Food: Bread(s, size, c); break;
                    case DesalEra.Game.Vital.Water: Droplet(s, size, c); break;
                    case DesalEra.Game.Vital.Health: Heart(s, size, c); break;
                    default: Bolt(s, size, c); break;
                }
            });
        }

        /// <summary>Distinct silhouette per build piece — shape first, tint second.</summary>
        public static Sprite PieceIcon(string pieceName, int size = 48)
        {
            string key = pieceName ?? "Deck";
            return Get($"piece_{key}_{size}", size, size, s =>
            {
                Color wood = new Color(0.82f, 0.66f, 0.40f);
                Color steel = new Color(0.62f, 0.68f, 0.74f);
                Color plastic = new Color(0.45f, 0.55f, 0.58f);
                switch (key)
                {
                    case "Column":
                        s.Box(size * 0.38f, size * 0.12f, size * 0.24f, size * 0.76f, wood);
                        s.Box(size * 0.30f, size * 0.12f, size * 0.40f, size * 0.08f, wood * 0.8f);
                        break;
                    case "Pontoon":
                        s.Box(size * 0.12f, size * 0.32f, size * 0.76f, size * 0.36f, plastic);
                        s.Disc(size * 0.18f, size * 0.50f, size * 0.14f, plastic);
                        s.Disc(size * 0.82f, size * 0.50f, size * 0.14f, plastic);
                        break;
                    case "Brace":
                        for (int i = 0; i < size; i++)
                        {
                            s.Set(i, i, 1f, wood);
                            s.Set(i, size - 1 - i, 1f, wood * 0.85f);
                            if (i + 1 < size) { s.Set(i + 1, i, 1f, wood); s.Set(i, size - 2 - i, 1f, wood * 0.85f); }
                        }
                        break;
                    case "Still":
                        s.Box(size * 0.22f, size * 0.28f, size * 0.56f, size * 0.48f, steel);
                        s.Box(size * 0.40f, size * 0.12f, size * 0.20f, size * 0.18f, steel * 0.8f);
                        s.Box(size * 0.62f, size * 0.40f, size * 0.22f, size * 0.08f, steel * 0.7f);
                        break;
                    case "Roof":
                        for (int y = 0; y < size / 2; y++)
                        {
                            float half = (y / (float)(size / 2)) * size * 0.42f;
                            s.Box(size * 0.5f - half, size * 0.18f + y, half * 2f, 1f, wood);
                        }
                        s.Box(size * 0.18f, size * 0.50f, size * 0.64f, size * 0.28f, wood * 0.75f);
                        break;
                    case "Wall":
                        s.Box(size * 0.18f, size * 0.14f, size * 0.64f, size * 0.72f, wood);
                        s.Box(size * 0.18f, size * 0.32f, size * 0.64f, size * 0.04f, wood * 0.5f);
                        s.Box(size * 0.18f, size * 0.52f, size * 0.64f, size * 0.04f, wood * 0.5f);
                        s.Box(size * 0.46f, size * 0.14f, size * 0.06f, size * 0.72f, wood * 0.55f);
                        break;
                    case "Stairs":
                        s.Box(size * 0.16f, size * 0.66f, size * 0.28f, size * 0.16f, wood);
                        s.Box(size * 0.30f, size * 0.48f, size * 0.28f, size * 0.16f, wood * 0.92f);
                        s.Box(size * 0.44f, size * 0.30f, size * 0.28f, size * 0.16f, wood * 0.84f);
                        s.Box(size * 0.58f, size * 0.12f, size * 0.28f, size * 0.16f, wood * 0.76f);
                        break;
                    default: // Deck
                        s.Box(size * 0.10f, size * 0.28f, size * 0.80f, size * 0.44f, wood);
                        s.Box(size * 0.10f, size * 0.38f, size * 0.80f, size * 0.03f, wood * 0.5f);
                        s.Box(size * 0.10f, size * 0.50f, size * 0.80f, size * 0.03f, wood * 0.5f);
                        s.Box(size * 0.10f, size * 0.62f, size * 0.80f, size * 0.03f, wood * 0.5f);
                        break;
                }
            });
        }

        public static Sprite WarnMark(int size = 20)
        {
            return Get($"warn{size}", size, size, s =>
            {
                Color c = new Color(1f, 0.85f, 0.2f);
                // Upward triangle + bang.
                for (int y = 2; y < size - 2; y++)
                {
                    float t = (y - 2) / (float)(size - 4);
                    float half = t * (size * 0.42f);
                    s.Box(size * 0.5f - half, y, half * 2f, 1f, c);
                }
                s.Box(size * 0.46f, size * 0.28f, size * 0.08f, size * 0.32f, Color.black);
                s.Disc(size * 0.5f, size * 0.72f, size * 0.06f, Color.black);
            });
        }

        public static Sprite Crosshair(int size = 28)
        {
            return Get($"cross{size}", size, size, s =>
            {
                int m = size / 2;
                int gap = 3, arm = 5, thick = 2;
                for (int i = gap; i < gap + arm; i++)
                {
                    for (int t = 0; t < thick; t++)
                    {
                        s.Set(m - i, m + t - thick / 2, 1f);
                        s.Set(m + i, m + t - thick / 2, 1f);
                        s.Set(m + t - thick / 2, m - i, 1f);
                        s.Set(m + t - thick / 2, m + i, 1f);
                    }
                }
                s.Disc(m, m, 1.2f, Color.white);
            });
        }

        // --- shapes ---

        private static float SoftRoundAlpha(int x, int y, int size, int radius, float inset = 0f)
        {
            var half = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
            float r = Mathf.Min(radius, Mathf.Min(half.x, half.y)) - inset;
            if (r < 1f) r = 1f;
            var pad = new Vector2(half.x - r, half.y - r);
            var p = new Vector2(x, y) - half;
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - pad;
            float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
                    + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
            return Mathf.Clamp01(1f - d);
        }

        private static void Bread(Surface s, int n, Color tint)
        {
            s.Disc(n * 0.5f, n * 0.52f, n * 0.32f, tint);
            s.Box(n * 0.22f, n * 0.48f, n * 0.56f, n * 0.28f, tint);
            s.Box(n * 0.30f, n * 0.38f, n * 0.08f, n * 0.04f, tint * 0.5f);
            s.Box(n * 0.46f, n * 0.34f, n * 0.08f, n * 0.04f, tint * 0.5f);
            s.Box(n * 0.62f, n * 0.38f, n * 0.08f, n * 0.04f, tint * 0.5f);
        }

        private static void Droplet(Surface s, int n, Color tint)
        {
            s.Disc(n * 0.5f, n * 0.58f, n * 0.26f, tint);
            for (int y = 0; y < n / 2; y++)
            {
                float half = (y / (float)(n / 2)) * n * 0.22f;
                s.Box(n * 0.5f - half, n * 0.12f + y, half * 2f, 1f, tint);
            }
        }

        private static void Heart(Surface s, int n, Color tint)
        {
            s.Disc(n * 0.35f, n * 0.38f, n * 0.18f, tint);
            s.Disc(n * 0.65f, n * 0.38f, n * 0.18f, tint);
            for (int y = 0; y < n / 2; y++)
            {
                float t = y / (float)(n / 2);
                float half = Mathf.Lerp(n * 0.38f, 0f, t);
                s.Box(n * 0.5f - half, n * 0.42f + y, half * 2f, 1f, tint);
            }
        }

        private static void Bolt(Surface s, int n, Color tint)
        {
            // Zigzag lightning.
            s.Box(n * 0.48f, n * 0.10f, n * 0.18f, n * 0.28f, tint);
            s.Box(n * 0.28f, n * 0.34f, n * 0.38f, n * 0.12f, tint);
            s.Box(n * 0.38f, n * 0.42f, n * 0.18f, n * 0.38f, tint);
            s.Box(n * 0.28f, n * 0.72f, n * 0.28f, n * 0.12f, tint);
        }

        private static void ThreeChunks(Surface s, int n, Color tint)
        {
            s.Disc(n * 0.28f, n * 0.34f, n * 0.16f, tint);
            s.Disc(n * 0.70f, n * 0.40f, n * 0.13f, tint * 0.86f);
            s.Disc(n * 0.44f, n * 0.74f, n * 0.11f, tint * 0.72f);
        }

        private static void Plank(Surface s, int n, Color tint)
        {
            s.Box(n * 0.10f, n * 0.30f, n * 0.80f, n * 0.18f, tint);
            s.Box(n * 0.10f, n * 0.56f, n * 0.80f, n * 0.18f, tint * 0.88f);
            s.Box(n * 0.16f, n * 0.39f, n * 0.34f, n * 0.03f, tint * 0.55f);
        }

        private static void Ingot(Surface s, int n, Color tint)
        {
            // Trapezoid: each row widens toward the base, which reads as a bevelled bar.
            int rows = Mathf.Max(2, (int)(n * 0.42f));
            for (int y = 0; y < rows; y++)
            {
                float halfWidth = Mathf.Lerp(n * 0.14f, n * 0.36f, y / (float)rows);
                s.Box(n * 0.5f - halfWidth, n * 0.26f + y, halfWidth * 2f, 1f, tint);
            }

            s.Box(n * 0.30f, n * 0.70f, n * 0.40f, n * 0.07f, tint * 0.6f);
        }

        private static void Drum(Surface s, int n, Color tint)
        {
            s.Box(n * 0.26f, n * 0.22f, n * 0.48f, n * 0.58f, tint);
            s.Box(n * 0.26f, n * 0.31f, n * 0.48f, n * 0.05f, tint * 0.55f);
            s.Box(n * 0.26f, n * 0.66f, n * 0.48f, n * 0.05f, tint * 0.55f);
            s.Box(n * 0.40f, n * 0.08f, n * 0.20f, n * 0.15f, tint * 0.8f);
        }

        private static void Crate(Surface s, int n, Color tint)
        {
            s.Box(n * 0.16f, n * 0.22f, n * 0.68f, n * 0.58f, tint);
            s.Box(n * 0.16f, n * 0.47f, n * 0.68f, n * 0.06f, tint * 0.55f);
            s.Box(n * 0.46f, n * 0.22f, n * 0.06f, n * 0.58f, tint * 0.55f);
        }

        /// <summary>
        /// A texel buffer with its own bounds, so the shape helpers cannot index past the
        /// end. A rounded rect is a signed distance field: negative inside, zero on the
        /// edge, and a band of that many texels either side of zero is the outline.
        /// </summary>
        private struct Surface
        {
            public Color32[] Pixels;
            public int Width;
            public int Height;

            public void Set(int x, int y, float alpha)
            {
                Set(x, y, alpha, Color.white);
            }

            public void Set(int x, int y, float alpha, Color tint)
            {
                if (alpha <= 0f) return;
                if (x < 0 || y < 0 || x >= Width || y >= Height) return;

                byte a = (byte)(Mathf.Clamp01(alpha) * 255f);
                if (a == 0) return;

                Pixels[y * Width + x] = new Color32((byte)(tint.r * 255f), (byte)(tint.g * 255f),
                                                    (byte)(tint.b * 255f), a);
            }

            public void Fill(float alpha)
            {
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                        Set(x, y, alpha);
            }

            public void PanelField(int radius, float thickness)
            {
                var half = new Vector2(Width * 0.5f - 0.5f, Height * 0.5f - 0.5f);
                float r = Mathf.Min(radius, Mathf.Min(half.x, half.y));
                var inset = new Vector2(half.x - r, half.y - r);

                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        var p = new Vector2(x, y) - half;
                        var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - inset;
                        float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
                                + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;

                        float a = thickness > 0f ? 1f - Mathf.Abs(d + thickness * 0.5f) : 1f - d;
                        Set(x, y, a);
                    }
                }
            }

            public void Box(float x0, float y0, float w, float h, Color tint)
            {
                int ix0 = Mathf.Max(0, Mathf.RoundToInt(x0));
                int ix1 = Mathf.Min(Width, Mathf.RoundToInt(x0 + w));
                int iy0 = Mathf.Max(0, Mathf.RoundToInt(y0));
                int iy1 = Mathf.Min(Height, Mathf.RoundToInt(y0 + h));

                for (int y = iy0; y < iy1; y++)
                    for (int x = ix0; x < ix1; x++)
                        Set(x, y, 1f, tint);
            }

            public void Disc(float cx, float cy, float radius, Color tint)
            {
                int ix0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
                int ix1 = Mathf.Min(Width, Mathf.CeilToInt(cx + radius));
                int iy0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
                int iy1 = Mathf.Min(Height, Mathf.CeilToInt(cy + radius));

                for (int y = iy0; y < iy1; y++)
                {
                    for (int x = ix0; x < ix1; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - radius;
                        Set(x, y, 1f - d, tint);
                    }
                }
            }
        }

        // --- plumbing ---

        private delegate void Painter(Surface surface);

        private static Sprite Get(string key, int width, int height, Painter paint)
        {
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var surface = new Surface
            {
                Pixels = new Color32[width * height],
                Width = width,
                Height = height
            };

            paint(surface);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, linear: false)
            {
                name = "Ui_" + key,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave
            };

            texture.SetPixels32(surface.Pixels);
            texture.Apply(false, false);

            // Nine-sliced so one small panel stretches to any size without smearing its
            // corners. The border is half the texture, which is the corner radius plus a
            // texel of antialiasing.
            float inset = Mathf.Min(width, height) * 0.5f - 1f;
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                                       new Vector2(0.5f, 0.5f), 100f, 0,
                                       SpriteMeshType.FullRect,
                                       new Vector4(inset, inset, inset, inset));
            sprite.name = "Ui_" + key;
            sprite.hideFlags = HideFlags.HideAndDontSave;

            Owned.Add(texture);
            Owned.Add(sprite);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Releases every generated texture and sprite. These belong to the library, unlike
        /// the ones loaded from Resources, so they do need tearing down or they survive a
        /// play-mode exit and leak.
        /// </summary>
        public static void Release()
        {
            foreach (Object o in Owned)
            {
                if (o == null) continue;
                Object.DestroyImmediate(o);
            }

            Owned.Clear();
            Cache.Clear();
        }
    }
}