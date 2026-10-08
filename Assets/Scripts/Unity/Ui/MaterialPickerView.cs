using System.Collections.Generic;
using DesalEra.Game;
using DesalEra.Structure;
using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Right-side material sheet for one piece, with the trade-off stated rather than implied.
    /// </summary>
    public sealed class MaterialPickerView : MonoBehaviour
    {
        private static readonly MaterialKind[] Offered =
        {
            MaterialKind.Wood,
            MaterialKind.Steel,
            MaterialKind.Concrete
        };

        private readonly Dictionary<MaterialKind, Row> _rows = new Dictionary<MaterialKind, Row>();
        private readonly List<Row> _order = new List<Row>();

        private Text _title;
        private Text _summary;
        private Text _footer;
        private RectTransform _body;
        private Inventory _inventory;
        private BuildPiece _piece;
        private MaterialKind _chosen;

        private sealed class Row
        {
            public MaterialKind Kind;
            public UiButton Button;
        }

        public static MaterialPickerView Create(Transform parent)
        {
            var go = new GameObject("MaterialPicker", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var view = go.AddComponent<MaterialPickerView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            UiFactory.FillParent(transform);

            Image veil = UiFactory.Sprite(transform, UiSprites.Solid(),
                                          new Color(0f, 0f, 0f, 0.28f), Image.Type.Simple);
            UiFactory.Stretch(veil.rectTransform);
            veil.raycastTarget = true;

            Image panel = UiFactory.Sprite(transform, UiSprites.Panel(), UiTheme.Panel);
            UiFactory.Anchor(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                             new Vector2(-24f, 20f), new Vector2(440f, 430f));

            Image border = UiFactory.Sprite(panel.transform, UiSprites.Outline(), UiTheme.Hairline);
            UiFactory.Stretch(border.rectTransform);
            border.raycastTarget = false;

            var content = UiFactory.Container(panel.transform, "Content");
            UiFactory.Stretch(content);
            content.offsetMin = new Vector2(16f, 14f);
            content.offsetMax = new Vector2(-16f, -14f);

            var stack = content.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 4f;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            stack.padding = new RectOffset(0, 0, 0, 0);

            Text eyebrow = UiFactory.Text(content, "材料", UiTheme.FontSmall, UiTheme.Accent,
                                          TextAnchor.MiddleLeft);
            eyebrow.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
            eyebrow.horizontalOverflow = HorizontalWrapMode.Overflow;

            _title = UiFactory.Text(content, "选择材料", UiTheme.FontHeading, UiTheme.TextPrimary);
            _title.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            _title.horizontalOverflow = HorizontalWrapMode.Overflow;

            _summary = UiFactory.Text(content, string.Empty, UiTheme.FontBody, UiTheme.TextMuted);
            _summary.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            _summary.lineSpacing = 1f;

            _body = UiFactory.Container(content, "Options");
            _body.gameObject.AddComponent<LayoutElement>().preferredHeight = 228f;

            var layout = _body.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            foreach (MaterialKind kind in Offered)
            {
                var row = new Row { Kind = kind };
                row.Button = UiButton.Create(_body, UiCopy.Material(kind),
                                             new Vector2(400f, 72f), Figures(kind));
                var element = row.Button.GetComponent<LayoutElement>();
                element.preferredWidth = 400f;
                element.preferredHeight = 72f;
                element.flexibleWidth = 1f;

                MaterialKind captured = kind;
                row.Button.Clicked += () => Choose(captured);

                _rows[kind] = row;
                _order.Add(row);
            }

            _footer = UiFactory.Text(content, string.Empty, UiTheme.FontBody, UiTheme.TextMuted);
            _footer.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            _footer.lineSpacing = 1.05f;

            Text close = UiFactory.Text(content, "ESC 关闭  ·  点击选择材料",
                                        UiTheme.FontSmall, UiTheme.TextMuted, TextAnchor.MiddleLeft);
            close.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            close.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private static string Figures(MaterialKind kind)
        {
            float density = MaterialProperties.DensityKgPerM3(kind);
            float strength = MaterialProperties.AxialCapacityKnPerM2(kind);
            return $"{density:0} kg/m³  ·  {strength:0} kN/m²";
        }

        public void Open(BuildPiece piece, Inventory inventory)
        {
            _piece = piece;
            _inventory = inventory;
            _chosen = piece.Material;

            _title.text = UiCopy.Piece(piece.Name);
            UpdateSummary();
            foreach (Row row in _order) row.Button.SetSelected(row.Kind == _chosen);
        }

        private void Choose(MaterialKind kind)
        {
            _chosen = kind;
            foreach (Row row in _order) row.Button.SetSelected(row.Kind == _chosen);
            UpdateSummary();
        }

        public BuildPiece Resolve()
        {
            if (_piece == null) return null;
            if (_piece.Material == _chosen) return _piece;

            var variant = new BuildPiece
            {
                Name = _piece.Name,
                Material = _chosen,
                CrossSectionAreaM2 = _piece.CrossSectionAreaM2,
                LengthM = _piece.LengthM,
                IsCantilever = _piece.IsCantilever,
                LocalOffset = _piece.LocalOffset,
                SealedVolumeM3 = _piece.SealedVolumeM3,
                PopulationGrant = _piece.PopulationGrant
            };

            foreach (KeyValuePair<ResourceKind, int> entry in _piece.Cost) variant.Cost[entry.Key] = entry.Value;
            return variant;
        }

        private void UpdateSummary()
        {
            if (_piece == null) return;

            float volume = _piece.CrossSectionAreaM2 * _piece.LengthM;
            float massKg = volume * MaterialProperties.DensityKgPerM3(_chosen);
            float capacityKn = MaterialProperties.MaxAxialLoadKn(_chosen, _piece.CrossSectionAreaM2);
            float liftKg = volume * 1025f * (_piece.SealedVolumeM3 > 0f ? _piece.SealedVolumeM3 / volume : 0.35f);

            string verdict;
            if (massKg > liftKg)
            {
                verdict = "<color=#D84D42>单独放下就会沉筏，请选更轻的材料。</color>";
            }
            else if (massKg > liftKg * 0.5f)
            {
                verdict = "<color=#E0B83D>偏重，备用浮力会明显下降。</color>";
            }
            else
            {
                verdict = "<color=#6BB070>够轻，可以放心使用。</color>";
            }

            _summary.text = $"{massKg:0} kg  ·  承重 {capacityKn:0} kN\n{verdict}";

            bool affordable = BuildBarView.CanAfford(_inventory, _piece);
            _footer.text = affordable
                ? $"消耗 {Cost(_piece)}  ·  资源充足"
                : $"消耗 {Cost(_piece)}  ·  <color=#D84D42>船上不够</color>";
        }

        private static string Cost(BuildPiece piece)
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<ResourceKind, int> entry in piece.Cost)
            {
                if (entry.Value > 0) parts.Add($"{entry.Value}{UiCopy.Resource(entry.Key)}");
            }

            return parts.Count == 0 ? "打捞" : string.Join(" ", parts);
        }
    }
}
