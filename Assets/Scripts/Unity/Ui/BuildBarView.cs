using System.Collections.Generic;
using DesalEra.Game;
using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Bottom-left build dock on ProductList.png: eight wells, gold selection.
    /// </summary>
    public sealed class BuildBarView : MonoBehaviour
    {
        private sealed class Slot
        {
            public BuildPiece Piece;
            public UiButton Button;
            public string CostText;
        }

        private readonly List<Slot> _slots = new List<Slot>();
        private RectTransform _row;
        private RectTransform _panel;
        private Text _category;
        private Text _hint;
        private bool _skinned;
        private Vector2 _size;

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

            Image panel = UiSkins.Panel(transform, UiSkins.Kind.ProductList, out _skinned);
            _size = UiSkins.Size(UiSkins.Kind.ProductList, UiSkins.BuildBarWidth);
            UiFactory.Anchor(panel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                             new Vector2(UiTheme.ScreenMargin, UiSkins.BottomY), _size);
            _panel = panel.rectTransform;

            UiLayoutSettings L = UiLayout.Active;
            _category = UiFactory.Text(transform, string.Empty, L.buildCategory, UiTheme.Accent);
            RectTransform cat = _category.rectTransform;
            cat.anchorMin = new Vector2(0f, 0f);
            cat.anchorMax = new Vector2(0f, 0f);
            cat.pivot = new Vector2(0f, 0f);
            cat.sizeDelta = new Vector2(_size.x, 22f);
            cat.anchoredPosition = new Vector2(UiTheme.ScreenMargin,
                                               UiSkins.BottomY + _size.y + UiLayout.Active.categoryGap);
            _category.horizontalOverflow = HorizontalWrapMode.Overflow;

            _row = UiFactory.Container(panel.transform, "Slots");
            UiFactory.Stretch(_row);
            UiSkins.ApplyContentPad(_row, UiSkins.Kind.ProductList, _size);
            var layout = UiFactory.Horizontal(_row, _skinned ? 2f : 6f, new RectOffset(0, 0, 0, 0));
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            _hint = UiFactory.Text(transform, "1–8 选择  ·  R 旋转  ·  Q 换层  ·  左键放置",
                                   L.buildHint, UiTheme.TextMuted);
            RectTransform strip = _hint.rectTransform;
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(0f, 0f);
            strip.pivot = new Vector2(0f, 0f);
            strip.sizeDelta = new Vector2(_size.x, 20f);
            strip.anchoredPosition = new Vector2(UiTheme.ScreenMargin, UiLayout.Active.hintY);
            _hint.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        public void Bind(IReadOnlyList<BuildPiece> pieces)
        {
            if (_slots.Count > 0) return;

            if (_panel != null && !_skinned)
            {
                float width = pieces.Count * UiTheme.BuildSlot + Mathf.Max(0, pieces.Count - 1) * 6f + 20f;
                _panel.sizeDelta = new Vector2(width, _panel.sizeDelta.y);
            }

            for (int i = 0; i < pieces.Count; i++)
            {
                BuildPiece piece = pieces[i];
                var slot = new Slot
                {
                    Piece = piece,
                    CostText = CostLine(piece)
                };

                // One caption line under the icon so the ProductList wells are not cramped.
                string caption = $"{UiCopy.Piece(piece.Name)}\n{slot.CostText}";
                slot.Button = UiButton.CreateCard(_row, caption, null,
                                                  UiSprites.PieceIcon(piece.Name, 64), i + 1);
                slot.Button.SetSubtitleVisible(false);
                if (_skinned)
                {
                    slot.Button.SetPlateVisible(false);
                    var le = slot.Button.GetComponent<LayoutElement>();
                    if (le != null)
                    {
                        le.flexibleWidth = 1f;
                        le.flexibleHeight = 1f;
                        le.preferredWidth = -1f;
                        le.preferredHeight = -1f;
                        le.minWidth = 56f;
                        le.minHeight = 80f;
                    }
                }
                slot.Button.Clicked += () => PieceChosen?.Invoke(piece);
                _slots.Add(slot);
            }
        }

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
                bool chosen = slot.Piece == selected;
                slot.Button.SetInteractive(affordable);
                slot.Button.SetSelected(chosen);
                string caption = $"{UiCopy.Piece(slot.Piece.Name)}\n{slot.CostText}";
                slot.Button.SetTitle(caption);
                slot.Button.SetSubtitleVisible(false);
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

        public void SetContext(string text, Color tone)
        {
            if (_category == null) return;
            if (_category.text != text) _category.text = text;
            _category.color = tone;
        }
    }
}
