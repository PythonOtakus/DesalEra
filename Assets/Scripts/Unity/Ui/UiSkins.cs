using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Authored HUD plate PNGs under StreamingAssets/ui. Sizes and content pads come from
    /// <see cref="UiLayoutSettings"/> so they can be tuned in the Inspector.
    /// </summary>
    public static class UiSkins
    {
        public enum Kind
        {
            Minimap,
            Detail,
            ProductList,
            Status,
            MaterialList
        }

        public static float MinimapSide => UiLayout.Active.minimapSide;
        public static float BuildBarWidth => UiLayout.Active.buildBarWidth;
        public static float StatusWidth => UiLayout.Active.statusWidth;
        public static float ResourceWidth => UiLayout.Active.resourceWidth;
        public static float DetailWidth => UiLayout.Active.detailWidth;
        public static float BottomY => UiLayout.Active.bottomY;
        public static float ReferenceWidth => UiLayout.Active.referenceWidth;

        public static float VitalsCenterOffsetX()
        {
            UiLayoutSettings L = UiLayout.Active;
            float buildEnd = L.screenMargin + L.buildBarWidth;
            float resStart = L.referenceWidth - L.screenMargin - L.resourceWidth;
            float mid = (buildEnd + resStart) * 0.5f;
            return mid - L.referenceWidth * 0.5f;
        }

        private static readonly Dictionary<Kind, Sprite> Cache = new Dictionary<Kind, Sprite>();
        private static readonly List<Object> Owned = new List<Object>();

        public static Sprite Get(Kind kind)
        {
            if (Cache.TryGetValue(kind, out Sprite hit) && hit != null) return hit;

            string file = FileName(kind);
            Sprite sprite = Load(file);
            if (sprite == null) return null;
            Cache[kind] = sprite;
            return sprite;
        }

        public static Image Panel(Transform parent, Kind kind, out bool usedSkin)
        {
            Sprite skin = Get(kind);
            if (skin == null)
            {
                usedSkin = false;
                return UiFactory.ThemedPanel(parent, out _);
            }

            usedSkin = true;
            Image image = UiFactory.Sprite(parent, skin, Color.white, Image.Type.Simple);
            image.preserveAspect = false;
            return image;
        }

        public static Vector2 Size(Kind kind, float primary)
        {
            Sprite skin = Get(kind);
            if (skin == null)
            {
                switch (kind)
                {
                    case Kind.Minimap: return new Vector2(primary, primary);
                    case Kind.Status: return UiTheme.VitalsSize;
                    case Kind.MaterialList: return new Vector2(primary, primary * 0.42f);
                    case Kind.ProductList: return new Vector2(primary, UiTheme.BuildDockHeight);
                    default: return new Vector2(primary, primary * 1.35f);
                }
            }

            float aspect = skin.rect.width / Mathf.Max(1f, skin.rect.height);
            if (kind == Kind.Minimap) return new Vector2(primary, primary);
            return new Vector2(primary, primary / aspect);
        }

        public static Vector4 ContentPad(Kind kind)
        {
            return UiLayout.Active.PadFor(kind).ToVector4();
        }

        public static void ApplyContentPad(RectTransform content, Kind kind, Vector2 panelSize)
        {
            Vector4 p = ContentPad(kind);
            content.offsetMin = new Vector2(panelSize.x * p.x, panelSize.y * p.y);
            content.offsetMax = new Vector2(-panelSize.x * p.z, -panelSize.y * p.w);
        }

        private static string FileName(Kind kind)
        {
            switch (kind)
            {
                case Kind.Minimap: return "Minimap.png";
                case Kind.Detail: return "Detail.png";
                case Kind.ProductList: return "ProductList.png";
                case Kind.Status: return "Status.png";
                case Kind.MaterialList: return "MaterialList.png";
                default: return null;
            }
        }

        private static Sprite Load(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string full = Path.Combine(Application.streamingAssetsPath, "ui", fileName);
            if (!File.Exists(full)) return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(full);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "UiSkin_" + Path.GetFileNameWithoutExtension(fileName),
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!tex.LoadImage(bytes, markNonReadable: false)) return null;

                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                           new Vector2(0.5f, 0.5f), 100f, 0,
                                           SpriteMeshType.FullRect);
                sprite.name = tex.name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                Owned.Add(tex);
                Owned.Add(sprite);
                return sprite;
            }
            catch
            {
                return null;
            }
        }

        public static void Release()
        {
            foreach (Object o in Owned)
            {
                if (o != null) Object.DestroyImmediate(o);
            }
            Owned.Clear();
            Cache.Clear();
        }
    }
}
