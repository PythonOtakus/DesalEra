using DesalEra.Game;
using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Bottom-centre survival strip on Status.png: health → water → food → stamina.
    /// 每行：图标 | 标签文字 | 进度条 | 数值文字（标签与数值独立控件，与条同 Y 居中）。
    /// </summary>
    public sealed class VitalRowView : MonoBehaviour
    {
        private readonly System.Collections.Generic.Dictionary<Vital, Image> _bars =
            new System.Collections.Generic.Dictionary<Vital, Image>();
        private readonly System.Collections.Generic.Dictionary<Vital, Text> _numbers =
            new System.Collections.Generic.Dictionary<Vital, Text>();
        private readonly System.Collections.Generic.Dictionary<Vital, Text> _labels =
            new System.Collections.Generic.Dictionary<Vital, Text>();

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

            Image panel = UiSkins.Panel(transform, UiSkins.Kind.Status, out _);
            Vector2 size = UiSkins.Size(UiSkins.Kind.Status, UiSkins.StatusWidth);
            UiFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                             new Vector2(UiSkins.VitalsCenterOffsetX(), UiSkins.BottomY), size);

            UiLayoutSettings L = UiLayout.Active;
            var content = UiFactory.Container(panel.transform, "Content");
            UiFactory.Stretch(content);
            UiSkins.ApplyContentPad(content, UiSkins.Kind.Status, size);
            var stack = UiFactory.Vertical(content, L.statusRowSpacing);
            stack.childForceExpandHeight = true;
            stack.childAlignment = TextAnchor.MiddleCenter;

            float gaps = L.statusRowSpacing * 3f;
            float rowH = (size.y * (1f - UiSkins.ContentPad(UiSkins.Kind.Status).y
                                    - UiSkins.ContentPad(UiSkins.Kind.Status).w) - gaps) / 4f;

            AddRow(content, "生命", Vital.Health, UiTheme.VitalHealth, rowH);
            AddRow(content, "水分", Vital.Water, UiTheme.VitalWater, rowH);
            AddRow(content, "饱食", Vital.Food, UiTheme.VitalFood, rowH);
            AddRow(content, "耐力", Vital.Stamina, UiTheme.VitalStamina, rowH);
        }

        private void AddRow(Transform parent, string label, Vital vital, Color fill, float rowH)
        {
            UiLayoutSettings L = UiLayout.Active;

            RectTransform row = UiFactory.Container(parent, "Row_" + label);
            var rowLe = row.gameObject.AddComponent<LayoutElement>();
            rowLe.preferredHeight = rowH;
            rowLe.flexibleHeight = 1f;
            rowLe.minHeight = 24f;

            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = L.statusItemSpacing;
            rowLayout.padding = new RectOffset(2, 2, 0, 0);
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            float iconSide = Mathf.Clamp(rowH * 0.7f, 18f, 28f);
            Image icon = UiFactory.Sprite(row, UiSprites.VitalIcon(vital, 28), fill, Image.Type.Simple);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconLe = icon.gameObject.AddComponent<LayoutElement>();
            iconLe.preferredWidth = iconSide;
            iconLe.preferredHeight = iconSide;
            iconLe.minWidth = iconSide;
            iconLe.minHeight = iconSide;
            iconLe.flexibleWidth = 0f;

            // 标签：独立 Text，与行垂直居中（与进度条同 Y）。
            UiTextStyle labelStyle = L.statusLabel;
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.preferredHeight = 0f;
            Text caption = UiFactory.Text(row, label, labelStyle, UiTheme.TextPrimary);
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            caption.verticalOverflow = VerticalWrapMode.Overflow;
            var captionLe = caption.gameObject.GetComponent<LayoutElement>()
                            ?? caption.gameObject.AddComponent<LayoutElement>();
            captionLe.preferredWidth = Mathf.Max(36f, labelStyle.size * 2.2f);
            captionLe.flexibleWidth = 0f;
            captionLe.minWidth = 32f;
            // 取消 Text() 默认的半高锚点，交给 HorizontalLayout 撑满行高并垂直居中字形。
            UiFactory.Stretch(caption.rectTransform);
            caption.rectTransform.offsetMin = Vector2.zero;
            caption.rectTransform.offsetMax = Vector2.zero;

            float barH = Mathf.Max(12f, rowH * 0.56f);
            RectTransform track = UiFactory.Container(row, "Track");
            var trackLe = track.gameObject.AddComponent<LayoutElement>();
            trackLe.flexibleWidth = 1f;
            trackLe.minWidth = 40f;
            trackLe.preferredHeight = barH;
            trackLe.flexibleHeight = 0f;

            var well = UiFactory.Sprite(track, UiSprites.BarWell(16), UiTheme.Well, Image.Type.Sliced);
            UiFactory.Stretch(well.rectTransform);
            well.raycastTarget = false;

            Image bar = UiFactory.Sprite(well.transform, UiSprites.BarWell(16), fill, Image.Type.Sliced);
            bar.rectTransform.anchorMin = Vector2.zero;
            bar.rectTransform.anchorMax = Vector2.one;
            bar.rectTransform.offsetMin = Vector2.zero;
            bar.rectTransform.offsetMax = Vector2.zero;
            bar.raycastTarget = false;

            // 数值：独立 Text，与标签/进度条同 Y 居中。
            UiTextStyle numberStyle = L.statusNumber;
            numberStyle.alignment = TextAnchor.MiddleRight;
            numberStyle.preferredHeight = 0f;
            Text number = UiFactory.Text(row, string.Empty, numberStyle, fill);
            number.horizontalOverflow = HorizontalWrapMode.Overflow;
            number.verticalOverflow = VerticalWrapMode.Overflow;
            var numberLe = number.gameObject.GetComponent<LayoutElement>()
                           ?? number.gameObject.AddComponent<LayoutElement>();
            numberLe.preferredWidth = Mathf.Max(28f, numberStyle.size * 1.8f);
            numberLe.flexibleWidth = 0f;
            numberLe.minWidth = 24f;
            UiFactory.Stretch(number.rectTransform);
            number.rectTransform.offsetMin = Vector2.zero;
            number.rectTransform.offsetMax = Vector2.zero;
            number.gameObject.SetActive(false);

            _bars[vital] = bar;
            _numbers[vital] = number;
            _labels[vital] = caption;
        }

        public void Refresh(SurvivalModel survival, Inventory inventory)
        {
            if (survival == null) return;
            foreach (var entry in _bars)
            {
                float value = survival.Get(entry.Key);
                float fraction = Mathf.Clamp01(value / SurvivalModel.MaxValue);
                UiFactory.SetFill(entry.Value, fraction);
                Text num = _numbers[entry.Key];
                bool show = fraction < UiTheme.VitalNumberThreshold;
                if (num.gameObject.activeSelf != show) num.gameObject.SetActive(show);
                if (show) num.text = Mathf.CeilToInt(value).ToString();
            }
        }
    }
}
