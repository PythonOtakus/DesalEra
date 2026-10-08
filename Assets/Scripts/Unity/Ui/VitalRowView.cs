using DesalEra.Game;
using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// The four survival stats as bars, plus the resource counts as a strip.
    ///
    /// These were two lines of IMGUI text. Bars were not decoration: with four numbers in a
    /// row, a falling value and a rising one look identical, and the player cannot act on
    /// what they cannot see moving. Colour only marks the crisis state, so the bar's length
    /// stays the primary channel.
    /// </summary>
    public sealed class VitalRowView : MonoBehaviour
    {
        private readonly System.Collections.Generic.Dictionary<Vital, Image> _bars =
            new System.Collections.Generic.Dictionary<Vital, Image>();

        private Text _clock;
        private Text _resourceStrip;
        private readonly ResourceChip[] _chips = new ResourceChip[5];

        private struct ResourceChip
        {
            public Image Icon;
            public Text Count;
        }

        public static VitalRowView Create(Transform parent)
        {
            var go = new GameObject("VitalRow", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var view = go.AddComponent<VitalRowView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            UiFactory.FillParent(transform);

            Image panel = UiFactory.Sprite(transform, UiSprites.Panel(), UiTheme.Panel);
            UiFactory.Anchor(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                             new Vector2(UiTheme.PanelPadding, -UiTheme.PanelPadding),
                             new Vector2(360f, 136f));

            // A hairline frame, so the panel does not read as a hole in the world.
            Image border = UiFactory.Sprite(panel.transform, UiSprites.Outline(), UiTheme.Hairline);
            UiFactory.Stretch(border.rectTransform);
            border.raycastTarget = false;

            var content = UiFactory.Container(panel.transform, "Content");
            UiFactory.Stretch(content);
            // Leave the bottom strip free for the resource chips.
            content.offsetMin = new Vector2(UiTheme.PanelPadding, 34f);
            content.offsetMax = new Vector2(-UiTheme.PanelPadding, -UiTheme.PanelPadding);

            UiFactory.Vertical(content, 4f);

            AddRow(content, "食物", Vital.Food, new Color(0.78f, 0.56f, 0.32f));
            AddRow(content, "饮水", Vital.Water, new Color(0.42f, 0.68f, 0.84f));
            AddRow(content, "生命", Vital.Health, new Color(0.80f, 0.34f, 0.32f));
            AddRow(content, "体力", Vital.Stamina, new Color(0.52f, 0.72f, 0.48f));

            _clock = UiFactory.Text(content, "0 分", UiTheme.FontSmall, UiTheme.TextMuted, TextAnchor.MiddleRight);
            _clock.rectTransform.offsetMin = new Vector2(0f, 0f);
            _clock.rectTransform.offsetMax = new Vector2(-UiTheme.PanelPadding, 0f);
            _clock.horizontalOverflow = HorizontalWrapMode.Overflow;

            BuildResourceStrip(panel.rectTransform);
        }

        private void AddRow(Transform parent, string label, Vital vital, Color fill)
        {
            RectTransform row = UiFactory.Container(parent, "Row_" + label);
            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 22f;

            Text caption = UiFactory.Text(row, label, UiTheme.FontSmall, UiTheme.TextMuted, TextAnchor.MiddleLeft);
            caption.rectTransform.anchorMin = new Vector2(0f, 0f);
            caption.rectTransform.anchorMax = new Vector2(0f, 1f);
            caption.rectTransform.sizeDelta = new Vector2(56f, 0f);
            caption.rectTransform.offsetMin = Vector2.zero;
            caption.rectTransform.offsetMax = Vector2.zero;

            RectTransform track = UiFactory.Container(row, "Track");
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.pivot = new Vector2(0f, 0.5f);
            // Offsets, not a negative sizeDelta: the row's width comes from the layout
            // group, and a negative sizeDelta against stretched anchors resolves to
            // nothing at all until the group has measured, which it has not yet.
            track.offsetMin = new Vector2(56f, -5f);
            track.offsetMax = new Vector2(-4f, 5f);

            var well = UiFactory.Sprite(track, UiSprites.Solid(), UiTheme.Well, Image.Type.Simple);
            UiFactory.Stretch(well.rectTransform);
            well.raycastTarget = false;

            Image bar = UiFactory.Sprite(well.transform, UiSprites.Solid(), fill, Image.Type.Simple);
            bar.rectTransform.anchorMin = Vector2.zero;
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.offsetMin = Vector2.zero;
            bar.rectTransform.offsetMax = Vector2.zero;
            bar.raycastTarget = false;

            _bars[vital] = bar;
        }

        /// <summary>
        /// Counts live at the bottom of the same panel. They are here rather than in the
        /// inventory screen because a cost the player cannot compare against their
        /// holdings is a cost they cannot plan around.
        /// </summary>
        private void BuildResourceStrip(RectTransform panel)
        {
            RectTransform strip = UiFactory.Container(panel, "Resources");
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(1f, 0f);
            strip.pivot = new Vector2(0.5f, 0f);
            strip.sizeDelta = new Vector2(0f, 30f);
            strip.anchoredPosition = new Vector2(0f, 10f);

            UiFactory.Horizontal(strip, 10f);

            int index = 0;
            foreach (ResourceKind kind in new[] { ResourceKind.Plank, ResourceKind.Scrap, ResourceKind.Metal, ResourceKind.Water, ResourceKind.Food })
            {
                RectTransform cell = UiFactory.Container(strip, "Res_" + kind);
                var layout = cell.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = 62f;
                layout.preferredHeight = 26f;

                Image icon = UiFactory.Sprite(cell, UiSprites.ResourceIcon(kind, 24), UiTheme.ResourceColor(kind), Image.Type.Simple);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform ir = icon.rectTransform;
                ir.sizeDelta = new Vector2(22f, 22f);
                ir.anchorMin = new Vector2(0f, 0.5f);
                ir.anchorMax = new Vector2(0f, 0.5f);
                ir.pivot = new Vector2(0f, 0.5f);
                ir.anchoredPosition = Vector2.zero;

                Text count = UiFactory.Text(cell, "0", UiTheme.FontBody, UiTheme.TextPrimary, TextAnchor.MiddleLeft);
                count.rectTransform.offsetMin = new Vector2(26f, 0f);
                count.rectTransform.offsetMax = new Vector2(0f, 0f);
                count.horizontalOverflow = HorizontalWrapMode.Overflow;

                _chips[index].Icon = icon;
                _chips[index].Count = count;
                index++;
            }
        }

        public void Refresh(SurvivalModel survival, Inventory inventory)
        {
            if (survival != null)
            {
                foreach (var entry in _bars)
                {
                    float fraction = Mathf.Clamp01(survival.Get(entry.Key) / SurvivalModel.MaxValue);
                    UiFactory.SetFill(entry.Value, fraction);
                }

                _clock.text = $"{survival.ElapsedMinutes:F0} 分" + (survival.IsDead ? "  已死亡" : string.Empty);
                _clock.color = survival.IsDead ? UiTheme.Bad
                              : survival.IsInCrisis ? UiTheme.Warn
                              : UiTheme.TextMuted;
            }

            if (inventory == null) return;

            int index = 0;
            foreach (ResourceKind kind in new[] { ResourceKind.Plank, ResourceKind.Scrap, ResourceKind.Metal, ResourceKind.Water, ResourceKind.Food })
            {
                int have = inventory.Get(kind);
                _chips[index].Count.text = have.ToString();
                _chips[index].Count.color = have == 0 ? UiTheme.TextDisabled : UiTheme.TextPrimary;
                index++;
            }
        }
    }
}