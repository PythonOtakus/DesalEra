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

        /// <summary>Panel multiply (frame sprite is pre-coloured).</summary>
        public static readonly Color Panel = new Color(1f, 1f, 1f, 0.92f);

        public static readonly Color PanelRaised = new Color(1f, 1f, 1f, 0.96f);

        public static readonly Color Well = new Color(0.08f, 0.06f, 0.04f, 0.92f);

        public static readonly Color MetalRim = new Color(0.35f, 0.32f, 0.28f, 0.95f);

        public static readonly Color Hairline = new Color(0.40f, 0.36f, 0.30f, 0.65f);

        // --- text (cream on timber, per build-ui-design) ---

        public static readonly Color TextPrimary = new Color(0.94f, 0.92f, 0.86f, 1f);
        public static readonly Color TextMuted = new Color(0.62f, 0.58f, 0.50f, 1f);
        public static readonly Color TextDisabled = new Color(0.42f, 0.40f, 0.38f, 0.85f);

        // --- interaction: warm gold selection (not teal) ---

        public static readonly Color Accent = new Color(0.86f, 0.68f, 0.28f, 1f);

        public static readonly Color AccentDim = new Color(0.48f, 0.36f, 0.14f, 1f);

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

        // --- vitals (industry-common mapping; do not reuse for selection) ---

        public static readonly Color VitalFood = new Color(0.86f, 0.58f, 0.28f);
        public static readonly Color VitalWater = new Color(0.38f, 0.68f, 0.88f);
        public static readonly Color VitalHealth = new Color(0.86f, 0.30f, 0.28f);
        public static readonly Color VitalStamina = new Color(0.46f, 0.74f, 0.42f);

        /// <summary>Fraction below which a vital shows its numeric value.</summary>
        public const float VitalNumberThreshold = 0.30f;

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

        // --- metrics (from UiLayoutSettings; tweak in Inspector / Play) ---

        public static float PanelPadding => UiLayout.Active.panelPadding;
        public static float RowGap => UiLayout.Active.rowGap;
        public static int CornerRadius => UiLayout.Active.cornerRadius;
        public static float ScreenMargin => UiLayout.Active.screenMargin;

        /// <summary>其它面板辅助字号；面板专用文字请用各 Foldout 里的 UiTextStyle。</summary>
        public static int FontSmall => UiLayout.Active.otherSmall.size;
        public static int FontBody => UiLayout.Active.otherBody.size;
        public static int FontHeading => UiLayout.Active.resourceCount.size;
        public static int FontTitle => UiLayout.Active.otherTitle.size;

        public static float RowHeight => UiLayout.Active.rowHeight;
        public static float ButtonHeight => UiLayout.Active.buttonHeight;
        public static float SlotSize => UiLayout.Active.slotSize;
        public static float BuildSlot => UiLayout.Active.buildSlot;
        public static float BuildDockHeight => UiLayout.Active.buildDockHeight;
        public static Vector2 VitalsSize => UiLayout.Active.vitalsSize;
        public static float MinimapSize => UiLayout.Active.minimapSide;
        public static int SpriteBorder => UiLayout.Active.spriteBorder;

        /// <summary>Outline colour for always-on labels against bright sea glare.</summary>
        public static readonly Color TextOutline = new Color(0.02f, 0.03f, 0.04f, 0.85f);
    }
}