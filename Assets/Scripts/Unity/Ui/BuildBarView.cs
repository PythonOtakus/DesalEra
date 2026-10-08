using System.Collections.Generic;
using DesalEra.Game;
using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Bottom build dock in the style of a housing / placeable palette: clear centre view,
    /// large icon slots along the bottom edge, hotkeys and cost readable on the slot itself.
    ///
    /// Affordability is shown here rather than discovered on click. Dimmed slots cost the
    /// player nothing; a refusal toast after they have already committed the intent costs
    /// them a click and a moment of confusion.
    /// </summary>
    public sealed class BuildBarView : MonoBehaviour
    {
        private sealed class Slot
        {
            public BuildPiece Piece;
            public UiButton Button;
            public string CostText;
            public int Hotkey;
        }

        private readonly List<Slot> _slots = new List<Slot>();
        private RectTransform _row;
        private RectTransform _panel;
        private Text _category;
        private const float SlotSpacing = 10f;

        public event System.Action<BuildPiece> PieceChosen;

        public static BuildBarView Create(Transform parent)
        {
            var go = new GameObject("BuildBar", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var view = go.AddComponent<BuildBarView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            UiFactory.FillParent(transform);

            // Soft gradient strip so the dock reads as an edge chrome, not a floating card.
            Image scrim = UiFactory.Sprite(transform, UiSprites.Solid(), new Color(0.04f, 0.05f, 0.055f, 0.55f),
                                           Image.Type.Simple);
            UiFactory.Anchor(scrim.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                             Vector2.zero, new Vector2(1920f, UiTheme.BuildDockHeight + 36f));
            scrim.raycastTarget = false;

            // Dock width follows the palette (resized in Bind), not a wide empty tray.
            const float dockWidth = 560f;
            Image panel = UiFactory.Sprite(transform, UiSprites.Panel(), UiTheme.Panel);
            UiFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                             new Vector2(0f, 18f), new Vector2(dockWidth, UiTheme.BuildDockHeight));
            _panel = panel.rectTransform;

            Image border = UiFactory.Sprite(panel.transform, UiSprites.Outline(), UiTheme.Hairline);
            UiFactory.Stretch(border.rectTransform);
            border.raycastTarget = false;

            _category = UiFactory.Text(panel.transform, "结构", UiTheme.FontBody, UiTheme.Accent,
                                       TextAnchor.MiddleLeft);
            _category.rectTransform.anchorMin = new Vector2(0f, 1f);
            _category.rectTransform.anchorMax = new Vector2(1f, 1f);
            _category.rectTransform.pivot = new Vector2(0f, 1f);
            _category.rectTransform.sizeDelta = new Vector2(0f, 22f);
            _category.rectTransform.anchoredPosition = new Vector2(UiTheme.PanelPadding, -8f);
            _category.horizontalOverflow = HorizontalWrapMode.Overflow;

            _row = UiFactory.Container(panel.transform, "Slots");
            _row.anchorMin = new Vector2(0f, 0f);
            _row.anchorMax = new Vector2(1f, 1f);
            _row.offsetMin = new Vector2(UiTheme.PanelPadding, 12f);
            _row.offsetMax = new Vector2(-UiTheme.PanelPadding, -28f);
            var layout = UiFactory.Horizontal(_row, SlotSpacing, new RectOffset(0, 0, 0, 0));
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;

            // Hints live outside the dock, bottom-right — same corner as housing confirm keys.
            BuildHintStrip((RectTransform)transform);
        }

        /// <summary>
        /// Action chips sit bottom-right of the screen so the dock stays a pure palette and
        /// the centre of the view stays clear for the placement ghost.
        /// </summary>
        private static void BuildHintStrip(RectTransform root)
        {
            RectTransform strip = UiFactory.Container(root, "Hints");
            strip.anchorMin = new Vector2(1f, 0f);
            strip.anchorMax = new Vector2(1f, 0f);
            strip.pivot = new Vector2(1f, 0f);
            strip.sizeDelta = new Vector2(168f, 162f);
            strip.anchoredPosition = new Vector2(-UiTheme.PanelPadding, 24f);

            UiFactory.Vertical(strip, 6f);
            strip.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerRight;
            strip.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;

            AddHint(strip, "1-8 选择");
            AddHint(strip, "R 旋转朝向");
            AddHint(strip, "Q 切换层");
            AddHint(strip, "左键 放置");
            AddHint(strip, "右键 环视");
            AddHint(strip, "右键单击 拆除");
        }

        private static void AddHint(Transform parent, string label)
        {
            RectTransform chip = UiFactory.Container(parent, "Hint");
            var element = chip.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 168f;
            element.preferredHeight = 22f;
            element.flexibleWidth = 0f;

            Image bg = UiFactory.Sprite(chip, UiSprites.Panel(), UiTheme.PanelRaised);
            UiFactory.Stretch(bg.rectTransform);
            bg.raycastTarget = false;

            Text text = UiFactory.Text(chip, label, 14, UiTheme.TextPrimary, TextAnchor.MiddleCenter);
            UiFactory.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(8f, 0f);
            text.rectTransform.offsetMax = new Vector2(-8f, 0f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        /// <summary>
        /// Builds one square slot per piece. Called once; kept separate from Build so the
        /// bar can be rebound when the palette changes without recreating the dock.
        /// </summary>
        public void Bind(IReadOnlyList<BuildPiece> pieces)
        {
            if (_slots.Count > 0) return;

            if (_panel != null)
            {
                float width = pieces.Count * UiTheme.BuildSlot + Mathf.Max(0, pieces.Count - 1) * SlotSpacing
                              + UiTheme.PanelPadding * 2f;
                _panel.sizeDelta = new Vector2(width, _panel.sizeDelta.y);
            }

            for (int i = 0; i < pieces.Count; i++)
            {
                BuildPiece piece = pieces[i];
                var slot = new Slot
                {
                    Piece = piece,
                    CostText = CostLine(piece),
                    Hotkey = i + 1
                };

                slot.Button = UiButton.CreateCard(_row, UiCopy.Piece(piece.Name), slot.CostText,
                                                  UiSprites.ResourceIcon(PrimaryResource(piece), 48),
                                                  slot.Hotkey);
                slot.Button.Clicked += () => PieceChosen?.Invoke(piece);

                _slots.Add(slot);
            }
        }

        private static ResourceKind PrimaryResource(BuildPiece piece)
        {
            ResourceKind best = ResourceKind.Plank;
            int largest = -1;
            foreach (KeyValuePair<ResourceKind, int> entry in piece.Cost)
            {
                if (entry.Value > largest)
                {
                    largest = entry.Value;
                    best = entry.Key;
                }
            }

            return best;
        }

        /// <summary>"4板  3废" — short enough to sit under a 92px card.</summary>
        private static string CostLine(BuildPiece piece)
        {
            if (piece.Cost.Count == 0) return "打捞";

            List<string> parts = new List<string>();
            foreach (KeyValuePair<ResourceKind, int> entry in piece.Cost)
            {
                if (entry.Value <= 0) continue;
                parts.Add($"{entry.Value}{UiCopy.ResourceShort(entry.Key)}");
            }

            return parts.Count == 0 ? "打捞" : string.Join(" ", parts);
        }

        public void Refresh(Inventory inventory, BuildPiece selected)
        {
            foreach (Slot slot in _slots)
            {
                bool affordable = CanAfford(inventory, slot.Piece);
                slot.Button.SetInteractive(affordable);
                slot.Button.SetSelected(slot.Piece == selected);
                slot.Button.SetSubtitleTone(affordable ? UiTheme.TextMuted : UiTheme.Bad);
            }
        }

        public static bool CanAfford(Inventory inventory, BuildPiece piece)
        {
            if (inventory == null || piece == null) return false;
            foreach (KeyValuePair<ResourceKind, int> entry in piece.Cost)
            {
                if (inventory.Get(entry.Key) < entry.Value) return false;
            }

            return true;
        }

        public void SetHint(string hint)
        {
            if (_category != null && !string.IsNullOrEmpty(hint)) _category.text = hint;
        }

        /// <summary>The dock's header line: build level, facing and what the preview says.</summary>
        public void SetContext(string text, Color tone)
        {
            if (_category == null) return;
            if (_category.text != text) _category.text = text;
            _category.color = tone;
        }
    }
}
