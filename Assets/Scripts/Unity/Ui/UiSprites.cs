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
        public static Sprite Panel(int radius = UiTheme.CornerRadius)
        {
            int size = Mathf.Max(8, radius * 2 + 6);
            return Get($"panel{radius}", size, size, s => s.PanelField(radius, 0f));
        }

        /// <summary>A rounded outline, for borders and focus rings.</summary>
        public static Sprite Outline(int radius = UiTheme.CornerRadius, int thickness = 2)
        {
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

        // --- shapes ---

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