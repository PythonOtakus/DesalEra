using System.Collections.Generic;
using DesalEra.Game;
using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// The pack: every resource, what it is for, and what it would cost to build with.
    ///
    /// Two things here are answers to specific problems rather than decoration.
    ///
    /// First, each resource carries a plain-language line about what it does, because the
    /// brief asks for a base that is not tedious to manage and an inventory of five bare
    /// numbers is exactly tedious.
    ///
    /// Second, the panel also lists the pieces currently unaffordable and what is missing.
    /// A player who cannot build the brace they want does not need to be told the brace is
    /// greyed out; they need to be told they are two scrap short, and that the scrap is out
    /// on the water.
    /// </summary>
    public sealed class InventoryView : MonoBehaviour
    {
        private sealed class Row
        {
            public ResourceKind Kind;
            public Image Icon;
            public Text Count;
            public Text Note;
        }

        private readonly List<Row> _rows = new List<Row>();
        private Text _shortfall;
        private Inventory _inventory;
        private IReadOnlyList<BuildPiece> _pieces;

        public static InventoryView Create(Transform parent)
        {
            var go = new GameObject("Inventory", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var view = go.AddComponent<InventoryView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            UiFactory.FillParent(transform);

            Image veil = UiFactory.Sprite(transform, UiSprites.Solid(),
                                          new Color(0f, 0f, 0f, 0.35f), Image.Type.Simple);
            UiFactory.Stretch(veil.rectTransform);
            veil.raycastTarget = true;

            Image panel = UiFactory.Sprite(transform, UiSprites.Panel(), UiTheme.Panel);
            UiFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             Vector2.zero, new Vector2(560f, 520f));

            Image border = UiFactory.Sprite(panel.transform, UiSprites.Outline(), UiTheme.Hairline);
            UiFactory.Stretch(border.rectTransform);
            border.raycastTarget = false;

            var content = UiFactory.Container(panel.transform, "Content");
            UiFactory.Stretch(content);
            content.offsetMin = new Vector2(UiTheme.PanelPadding, UiTheme.PanelPadding);
            content.offsetMax = new Vector2(-UiTheme.PanelPadding, -UiTheme.PanelPadding);

            Text heading = UiFactory.Text(content, "背包", UiTheme.FontTitle, UiTheme.TextPrimary);
            var headingLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            headingLayout.spacing = 10f;

            var headingElement = heading.gameObject.AddComponent<LayoutElement>();
            headingElement.preferredHeight = 40f;

            BuildRows(content);

            _shortfall = UiFactory.Text(content, string.Empty, UiTheme.FontBody, UiTheme.Warn);
            var shortfallLayout = _shortfall.gameObject.AddComponent<LayoutElement>();
            shortfallLayout.preferredHeight = 46f;

            Text footer = UiFactory.Text(content, "TAB / ESC 关闭", UiTheme.FontSmall, UiTheme.TextMuted);
            var footerLayout = footer.gameObject.AddComponent<LayoutElement>();
            footerLayout.preferredHeight = 22f;
        }

        private void BuildRows(Transform parent)
        {
            foreach (ResourceKind kind in new[]
                     {
                         ResourceKind.Plank, ResourceKind.Scrap, ResourceKind.Metal,
                         ResourceKind.Water, ResourceKind.Food
                     })
            {
                var row = new Row { Kind = kind };

                RectTransform line = UiFactory.Container(parent, "Row_" + kind);
                var element = line.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 56f;
                var layout = line.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 12f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;

                RectTransform iconCell = UiFactory.Container(line, "Icon");
                var iconLayout = iconCell.gameObject.AddComponent<LayoutElement>();
                iconLayout.preferredWidth = 48f;
                iconLayout.preferredHeight = 48f;

                row.Icon = UiFactory.Sprite(iconCell, UiSprites.ResourceIcon(kind, 44),
                                             UiTheme.ResourceColor(kind), Image.Type.Simple);
                row.Icon.preserveAspect = true;
                row.Icon.raycastTarget = false;
                RectTransform ir = row.Icon.rectTransform;
                ir.anchorMin = new Vector2(0.5f, 0.5f);
                ir.anchorMax = new Vector2(0.5f, 0.5f);
                ir.sizeDelta = new Vector2(44f, 44f);
                ir.anchoredPosition = Vector2.zero;

                RectTransform textCell = UiFactory.Container(line, "Text");
                var textLayout = textCell.gameObject.AddComponent<LayoutElement>();
                textLayout.preferredWidth = 430f;
                textLayout.preferredHeight = 48f;

                Text name = UiFactory.Text(textCell, UiCopy.Resource(kind), UiTheme.FontBody, UiTheme.TextPrimary,
                                           TextAnchor.UpperLeft);
                name.rectTransform.anchorMin = new Vector2(0f, 1f);
                name.rectTransform.anchorMax = new Vector2(1f, 1f);
                name.rectTransform.pivot = new Vector2(0f, 1f);
                name.rectTransform.sizeDelta = new Vector2(0f, 24f);
                name.rectTransform.anchoredPosition = new Vector2(0f, 0f);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;

                row.Count = UiFactory.Text(textCell, "0", UiTheme.FontHeading, UiTheme.TextPrimary, TextAnchor.UpperRight);
                row.Count.rectTransform.anchorMin = new Vector2(1f, 1f);
                row.Count.rectTransform.anchorMax = new Vector2(1f, 1f);
                row.Count.rectTransform.pivot = new Vector2(1f, 1f);
                row.Count.rectTransform.sizeDelta = new Vector2(80f, 28f);
                row.Count.rectTransform.anchoredPosition = Vector2.zero;
                row.Count.horizontalOverflow = HorizontalWrapMode.Overflow;

                row.Note = UiFactory.Text(textCell, NoteFor(kind), UiTheme.FontSmall, UiTheme.TextMuted, TextAnchor.LowerLeft);
                row.Note.rectTransform.anchorMin = new Vector2(0f, 0f);
                row.Note.rectTransform.anchorMax = new Vector2(1f, 0f);
                row.Note.rectTransform.pivot = new Vector2(0f, 0f);
                row.Note.rectTransform.sizeDelta = new Vector2(0f, 22f);
                row.Note.rectTransform.anchoredPosition = Vector2.zero;

                _rows.Add(row);
            }
        }

        /// <summary>
        /// What the resource is for, in the player's terms. Taken from the enum's own
        /// documentation rather than invented, so the panel cannot describe a resource
        /// differently from how the code treats it.
        /// </summary>
        private static string NoteFor(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Plank: return "甲板与通用建材";
                case ResourceKind.Scrap: return "廉价填充，多用于立柱";
                case ResourceKind.Metal: return "坚固但重：斜撑、浮筒、淡化器";
                case ResourceKind.Water: return "饮用；每人每天约一份，高温更多";
                default: return "食用可恢复饱食，并少量补水";
            }
        }

        public void Bind(Inventory inventory, IReadOnlyList<BuildPiece> pieces)
        {
            _inventory = inventory;
            _pieces = pieces;
        }

        public void Refresh()
        {
            if (_inventory == null) return;

            foreach (Row row in _rows)
            {
                int have = _inventory.Get(row.Kind);
                row.Count.text = have.ToString();
                row.Count.color = have == 0 ? UiTheme.TextDisabled : UiTheme.TextPrimary;
            }

            _shortfall.text = ShortfallLine();
        }

        /// <summary>
        /// Lists the cheapest piece the player cannot afford and what is missing from it.
        /// Only the cheapest, on purpose: a list of everything out of reach is the same as no
        /// information at all, and the cheapest one is always the next reachable goal.
        /// </summary>
        private string ShortfallLine()
        {
            if (_pieces == null) return string.Empty;

            BuildPiece cheapest = null;
            Dictionary<ResourceKind, int> bestGap = null;
            int bestTotal = int.MaxValue;

            foreach (BuildPiece piece in _pieces)
            {
                if (BuildBarView.CanAfford(_inventory, piece)) continue;

                var gap = new Dictionary<ResourceKind, int>();
                int total = 0;
                foreach (KeyValuePair<ResourceKind, int> entry in piece.Cost)
                {
                    int missing = entry.Value - _inventory.Get(entry.Key);
                    if (missing <= 0) continue;
                    gap[entry.Key] = missing;
                    total += missing;
                }

                if (gap.Count == 0 || total >= bestTotal) continue;
                bestTotal = total;
                cheapest = piece;
                bestGap = gap;
            }

            if (cheapest == null) return "底栏构件都能负担得起。";

            List<string> parts = new List<string>();
            foreach (KeyValuePair<ResourceKind, int> entry in bestGap)
            {
                parts.Add($"还差 {entry.Value} {UiCopy.Resource(entry.Key)}");
            }

            return $"建造{UiCopy.Piece(cheapest.Name)}：{string.Join("，", parts)}。海里还有物资可打捞。";
        }
    }
}