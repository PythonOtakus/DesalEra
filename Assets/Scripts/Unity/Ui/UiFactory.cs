using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Builders for the primitives every screen is made of.
    ///
    /// All of it is created in code, on purpose. The project's rule is that no part of the
    /// running game lives in a serialised asset, and a Canvas hierarchy saved into a scene
    /// or a prefab would break that rule in the one place where it would hurt most to fix
    /// later: a UI that cannot be reviewed in a diff.
    ///
    /// Every helper anchors its children so the parent lays out first. Nothing here reads
    /// a screen's size, which is what keeps a rebuild of the world from also rebuilding the
    /// interface.
    /// </summary>
    public static class UiFactory
    {
        /// <summary>An empty RectTransform parented and zeroed, for layout groups to fill.</summary>
        public static RectTransform Container(Transform parent, string name,
                                              LayoutGroup group = null, int index = -1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var rect = (RectTransform)go.transform;
            rect.localScale = Vector3.one;

            if (group != null && index >= 0)
            {
                var element = go.AddComponent<LayoutElement>();
                element.ignoreLayout = false;
            }

            return rect;
        }

        /// <summary>
        /// A background image. Sliced by default so a generated rounded-rect sprite can
        /// stretch to any size without smearing its corners.
        /// </summary>
        public static Image Sprite(Transform parent, Sprite sprite, Color color,
                                   Image.Type type = Image.Type.Sliced)
        {
            var go = new GameObject("Image", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = type;
            return image;
        }

        /// <summary>
        /// A background panel with a hairline border. Returns the content rect to fill,
        /// because callers should never have to know how much padding a panel ate.
        /// </summary>
        public static RectTransform Panel(Transform parent, string name, Color fill, out Image background)
        {
            background = Sprite(parent, UiSprites.Panel(), fill);
            Stretch(background.rectTransform);

            var border = Sprite(background.transform, UiSprites.Outline(), UiTheme.Hairline);
            Stretch(border.rectTransform);
            border.raycastTarget = false;

            var content = Container(background.transform, name);
            var inset = UiTheme.PanelPadding;
            content.offsetMin = new Vector2(inset, inset);
            content.offsetMax = new Vector2(-inset, -inset);
            return content;
        }

        /// <summary>
        /// A text label. When <paramref name="lowerHalf"/> is set the label takes the
        /// bottom half of its parent, which is how a button stacks a title over a cost
        /// line without either of them knowing the other's size.
        /// </summary>
        public static Text Text(Transform parent, string text, int size, Color color,
                                TextAnchor anchor = TextAnchor.UpperLeft,
                                float leftInset = 0f, bool lowerHalf = false)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var label = go.AddComponent<Text>();
            label.font = UiRoot.Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            label.supportRichText = true;

            var rect = label.rectTransform;
            if (lowerHalf)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(1f, 0.5f);
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = Vector2.one;
            }

            rect.offsetMin = new Vector2(leftInset, 0f);
            rect.offsetMax = new Vector2(-UiTheme.PanelPadding, 0f);
            return label;
        }

        /// <summary>A vertical stack with consistent spacing and child alignment.</summary>
        public static VerticalLayoutGroup Vertical(Transform parent, float spacing = UiTheme.RowGap,
                                                   RectOffset padding = null, bool grow = true)
        {
            var layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup Horizontal(Transform parent, float spacing = UiTheme.RowGap,
                                                       RectOffset padding = null, bool grow = false)
        {
            var layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = grow;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static GridLayoutGroup Grid(Transform parent, Vector2 cell, Vector2 spacing)
        {
            var layout = parent.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = cell;
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            return layout;
        }

        /// <summary>Pins a rect to fill its parent with no inset.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Every always-on HUD view must fill the canvas first. Anchoring a panel against a
        /// zero-size root leaves it floating at the screen centre — which is how the first
        /// build bar ended up covering the player.
        /// </summary>
        public static void FillParent(Transform root)
        {
            Stretch((RectTransform)root);
        }

        /// <summary>
        /// Anchors a rect to one corner with an explicit size. Used for the HUD layers,
        /// which are positioned rather than laid out.
        /// </summary>
        public static RectTransform Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot,
                                           Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        /// <summary>
        /// A solid bar, used for the vitals. Returns the fill image so the caller can drive
        /// its width, and the layout element is told to ignore layout because the bar is
        /// positioned inside a fixed-height row.
        /// </summary>
        public static Image Bar(Transform parent, Vector2 size, Color fill, out Image background)
        {
            background = Sprite(parent, UiSprites.Solid(), UiTheme.Well, Image.Type.Simple);
            var rect = background.rectTransform;
            rect.sizeDelta = size;

            var fillImage = Sprite(background.transform, UiSprites.Solid(), fill, Image.Type.Simple);
            Stretch(fillImage.rectTransform);
            // The fill is a plain rect: it is clipped by the caller's own mask or simply
            // left to sit inside the well, which at HUD sizes reads the same and costs
            // nothing to maintain.
            fillImage.rectTransform.offsetMin = Vector2.zero;
            fillImage.rectTransform.offsetMax = Vector2.zero;
            return fillImage;
        }

        /// <summary>Sets an Image's width as a fraction of its parent, from the left.</summary>
        public static void SetFill(Image fill, float fraction01)
        {
            if (fill == null) return;
            RectTransform rect = fill.rectTransform;
            float t = Mathf.Clamp01(fraction01);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(t, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}