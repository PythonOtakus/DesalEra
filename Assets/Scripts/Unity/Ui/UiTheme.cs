using UnityEngine;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// The UI's whole visual vocabulary: colours, metrics and type scale, in one place.
    ///
    /// Everything here is data. The reason it exists is that the first version of the HUD
    /// was IMGUI text, and the moment real panels arrived every screen started inventing
    /// its own grey. A theme is cheaper than a design system review, and it is the only
    /// way to guarantee the stress colours in the world and in the legend cannot drift
    /// apart.
    ///
    /// The palette is read off the same surfaces the world is built from: oxidised steel,
    /// weathered decking, wet concrete, and the flat overcast sky the sea sits under.
    /// Nothing is pure white or pure black, because nothing in this world is.
    /// </summary>
    public static class UiTheme
    {
        // --- surfaces ---

        /// <summary>Panel fill. Dark enough that the world stays the brightest thing.</summary>
        public static readonly Color Panel = new Color(0.10f, 0.115f, 0.125f, 0.94f);

        /// <summary>A panel that sits above another panel.</summary>
        public static readonly Color PanelRaised = new Color(0.145f, 0.160f, 0.172f, 0.97f);

        /// <summary>Well or slot interior, e.g. an empty inventory cell.</summary>
        public static readonly Color Well = new Color(0.055f, 0.065f, 0.072f, 0.90f);

        /// <summary>Hairline used for separators and panel borders.</summary>
        public static readonly Color Hairline = new Color(0.32f, 0.34f, 0.33f, 0.75f);

        // --- text ---

        public static readonly Color TextPrimary = new Color(0.88f, 0.89f, 0.87f, 1f);
        public static readonly Color TextMuted = new Color(0.56f, 0.59f, 0.58f, 1f);
        public static readonly Color TextDisabled = new Color(0.38f, 0.40f, 0.40f, 0.85f);

        // --- interaction ---

        /// <summary>Selected or hovered affordance. Cold, so it never reads as a resource.</summary>
        public static readonly Color Accent = new Color(0.36f, 0.62f, 0.56f, 1f);

        public static readonly Color AccentDim = new Color(0.22f, 0.36f, 0.34f, 1f);

        /// <summary>Affordable / valid.</summary>
        public static readonly Color Good = new Color(0.42f, 0.68f, 0.44f, 1f);

        /// <summary>Too expensive / will overload. Distinct from Bad on purpose.</summary>
        public static readonly Color Warn = new Color(0.88f, 0.72f, 0.24f, 1f);

        /// <summary>Illegal / unaffordable / broken.</summary>
        public static readonly Color Bad = new Color(0.86f, 0.30f, 0.26f, 1f);

        // --- structure stress ---
        //
        // These must equal StructureView's serialized palette. The world tints members by
        // utilization; a legend that disagrees with the world is worse than no legend, so
        // both read from here and StructureView is the one that keeps its fields for the
        // inspector.

        public static readonly Color StressIntact = new Color(0.36f, 0.62f, 0.56f);
        public static readonly Color StressStrained = new Color(0.88f, 0.72f, 0.24f);
        public static readonly Color StressCritical = new Color(0.98f, 0.24f, 0.18f);

        // --- resources ---
        //
        // Tints for the procedurally drawn inventory icons. Kept apart from the world
        // materials: a plank in the pack and a timber beam in the raft are the same stuff
        // and should not be the same colour, or the inventory reads as a legend.

        public static Color ResourceColor(DesalEra.Game.ResourceKind kind)
        {
            switch (kind)
            {
                case DesalEra.Game.ResourceKind.Scrap: return new Color(0.62f, 0.48f, 0.32f);
                case DesalEra.Game.ResourceKind.Plank: return new Color(0.78f, 0.62f, 0.36f);
                case DesalEra.Game.ResourceKind.Metal: return new Color(0.60f, 0.66f, 0.72f);
                case DesalEra.Game.ResourceKind.Water: return new Color(0.42f, 0.68f, 0.84f);
                default: return new Color(0.82f, 0.56f, 0.38f);
            }
        }

        public static Color MaterialColor(DesalEra.Structure.MaterialKind kind)
        {
            switch (kind)
            {
                case DesalEra.Structure.MaterialKind.Steel: return new Color(0.58f, 0.62f, 0.66f);
                case DesalEra.Structure.MaterialKind.Concrete: return new Color(0.62f, 0.61f, 0.58f);
                case DesalEra.Structure.MaterialKind.Plastic: return new Color(0.52f, 0.58f, 0.54f);
                default: return new Color(0.60f, 0.46f, 0.28f);
            }
        }

        // --- metrics, in reference-resolution units ---

        public const float PanelPadding = 14f;
        public const float RowGap = 8f;
        public const int CornerRadius = 6;

        public const int FontSmall = 18;
        public const int FontBody = 22;
        public const int FontHeading = 28;
        public const int FontTitle = 34;

        /// <summary>Line height used by every vertical layout, so rows never disagree.</summary>
        public const float RowHeight = 30f;
        public const float ButtonHeight = 64f;
        public const float SlotSize = 74f;

        /// <summary>Square build carousel cell. Large enough that the icon reads at a glance.</summary>
        public const float BuildSlot = 92f;

        /// <summary>Bottom dock height for the build carousel (Genshin / Valheim style).</summary>
        public const float BuildDockHeight = 148f;

        /// <summary>How thick a generated 9-sliced border is, in sprite texels.</summary>
        public const int SpriteBorder = 10;
    }
}