using System.Collections.Generic;
using DesalEra.Game;
using DesalEra.Structure;
using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Right-centre build detail sheet: title → warning → cost → material rows → ESC.
    /// Layout and copy follow docs/build-ui-design.md.
    /// </summary>
    public sealed class MaterialPickerView : MonoBehaviour
    {
        private static readonly MaterialKind[] Offered =
        {
            MaterialKind.Wood, MaterialKind.Steel, MaterialKind.Concrete
        };

        private enum Risk { Safe, Heavy, Sinks }

        private readonly Dictionary<MaterialKind, Row> _rows = new Dictionary<MaterialKind, Row>();
        private readonly List<Row> _order = new List<Row>();

        private Text _title;
        private RectTransform _warnBox;
        private Text _warning;
        private Text _cost;
        private Inventory _inventory;
        private BuildPiece _piece;
        private MaterialKind _chosen;
        private bool _skinned;

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
            UiLayoutSettings L = UiLayout.Active;
            UiFactory.FillParent(transform);

            Image veil = UiFactory.Sprite(transform, UiSprites.Solid(),
                                          new Color(0f, 0f, 0f, 0.18f), Image.Type.Simple);
            UiFactory.Stretch(veil.rectTransform);
            veil.raycastTarget = true;

            Image panel = UiSkins.Panel(transform, UiSkins.Kind.Detail, out _skinned);
            Vector2 size = UiSkins.Size(UiSkins.Kind.Detail, UiSkins.DetailWidth);
            UiFactory.Anchor(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                             new Vector2(-UiTheme.ScreenMargin, 0f), size);

            var content = UiFactory.Container(panel.transform, "Content");
            UiFactory.Stretch(content);
            UiSkins.ApplyContentPad(content, UiSkins.Kind.Detail, size);

            var stack = content.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = L.detailStackSpacing;
            stack.padding = new RectOffset(0, 0, 0, 0);
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            _title = UiFactory.Text(content, "建造", L.detailTitle, UiTheme.TextPrimary);
            _title.horizontalOverflow = HorizontalWrapMode.Overflow;

            _warnBox = UiFactory.Container(content, "Warn");
            var warnLe = _warnBox.gameObject.AddComponent<LayoutElement>();
            warnLe.preferredHeight = L.detailWarning.preferredHeight > 0f
                ? L.detailWarning.preferredHeight : 52f;

            Image warnBg = UiFactory.Sprite(_warnBox, UiSprites.Panel(), new Color(0.45f, 0.12f, 0.10f, 0.55f));
            UiFactory.Stretch(warnBg.rectTransform);
            warnBg.raycastTarget = false;
            Image warnIcon = UiFactory.Sprite(_warnBox, UiSprites.WarnMark(28), Color.white, Image.Type.Simple);
            warnIcon.rectTransform.sizeDelta = new Vector2(26f, 26f);
            warnIcon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            warnIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            warnIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            warnIcon.rectTransform.anchoredPosition = new Vector2(10f, 0f);
            warnIcon.raycastTarget = false;
            _warning = UiFactory.Text(_warnBox, string.Empty, L.detailWarning, UiTheme.TextPrimary);
            _warning.rectTransform.offsetMin = new Vector2(42f, 4f);
            _warning.rectTransform.offsetMax = new Vector2(-10f, -4f);

            _cost = UiFactory.Text(content, "消耗：—", L.detailCost, UiTheme.TextPrimary);
            _cost.horizontalOverflow = HorizontalWrapMode.Overflow;

            float rowH = L.detailRowHeight;
            var body = UiFactory.Container(content, "Options");
            body.gameObject.AddComponent<LayoutElement>().preferredHeight =
                rowH * Offered.Length + L.detailRowSpacing * (Offered.Length - 1);
            var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = L.detailRowSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            float innerW = size.x * (1f - UiSkins.ContentPad(UiSkins.Kind.Detail).x
                                     - UiSkins.ContentPad(UiSkins.Kind.Detail).z);
            foreach (MaterialKind kind in Offered)
            {
                var row = new Row { Kind = kind };
                row.Button = UiButton.Create(body, UiCopy.Material(kind),
                                             new Vector2(innerW, rowH), Figures(kind),
                                             UiSprites.Panel(),
                                             L.detailRowTitle, L.detailRowSub);
                row.Button.SetIconColor(UiTheme.MaterialColor(kind));
                if (_skinned) row.Button.SetPlateVisible(false);
                var element = row.Button.GetComponent<LayoutElement>();
                element.preferredWidth = -1f;
                element.flexibleWidth = 1f;
                element.minWidth = innerW;
                element.preferredHeight = rowH;
                MaterialKind captured = kind;
                row.Button.Clicked += () => Choose(captured);
                _rows[kind] = row;
                _order.Add(row);
            }

            Text close = UiFactory.Text(content, "ESC 关闭 · 点击选择材料",
                                        L.detailHint, UiTheme.TextDisabled);
            close.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private static string Figures(MaterialKind kind)
        {
            float density = MaterialProperties.DensityKgPerM3(kind);
            float strength = MaterialProperties.AxialCapacityKnPerM2(kind);
            return $"{density:0} kg/m³ · 承重 {strength:0}";
        }

        public void Open(BuildPiece piece, Inventory inventory)
        {
            _piece = piece;
            _inventory = inventory;
            _chosen = piece.Material;
            _title.text = "木质" + UiCopy.Piece(piece.Name);
            if (piece.Material == MaterialKind.Steel) _title.text = "钢质" + UiCopy.Piece(piece.Name);
            if (piece.Material == MaterialKind.Concrete) _title.text = "混凝土" + UiCopy.Piece(piece.Name);
            RefreshRows();
            UpdateSummary();
        }

        private void Choose(MaterialKind kind)
        {
            if (Assess(kind) == Risk.Sinks) return;
            _chosen = kind;
            RefreshRows();
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

        private void RefreshRows()
        {
            foreach (Row row in _order)
            {
                Risk risk = Assess(row.Kind);
                bool sinks = risk == Risk.Sinks;
                row.Button.SetInteractive(!sinks);
                row.Button.SetSelected(row.Kind == _chosen && !sinks);
                row.Button.SetSubtitleTone(risk == Risk.Sinks ? UiTheme.Bad
                                          : risk == Risk.Heavy ? UiTheme.Warn
                                          : UiTheme.TextMuted);
            }
        }

        private void UpdateSummary()
        {
            if (_piece == null) return;

            if (Assess(_chosen) == Risk.Sinks)
            {
                foreach (MaterialKind kind in Offered)
                {
                    if (Assess(kind) != Risk.Sinks) { _chosen = kind; break; }
                }
                RefreshRows();
            }

            Risk steelRisk = Assess(MaterialKind.Steel);
            bool showWarn = steelRisk != Risk.Safe;
            _warnBox.gameObject.SetActive(showWarn);
            if (showWarn)
                _warning.text = steelRisk == Risk.Sinks
                    ? "钢材过重，可能导致沉筏"
                    : "钢材偏重，备用浮力会下降";

            _title.text = Prefix(_chosen) + UiCopy.Piece(_piece.Name);

            bool affordable = BuildBarView.CanAfford(_inventory, _piece);
            string cost = Cost(_piece);
            _cost.text = affordable
                ? $"消耗：{cost}"
                : $"消耗：{cost}  <color=#D84D42>不足</color>";
        }

        private static string Prefix(MaterialKind kind)
        {
            switch (kind)
            {
                case MaterialKind.Steel: return "钢质";
                case MaterialKind.Concrete: return "混凝土";
                default: return "木质";
            }
        }

        private Risk Assess(MaterialKind kind)
        {
            if (_piece == null) return Risk.Safe;
            float volume = Mathf.Max(1e-6f, _piece.CrossSectionAreaM2 * _piece.LengthM);
            float massKg = volume * MaterialProperties.DensityKgPerM3(kind);
            float woodKg = volume * MaterialProperties.DensityKgPerM3(MaterialKind.Wood);
            float liftKg = volume * 1025f * (_piece.SealedVolumeM3 > 0f ? _piece.SealedVolumeM3 / volume : 0.35f);
            if (massKg <= woodKg * 1.02f) return Risk.Safe;
            if (massKg > liftKg) return Risk.Sinks;
            if (massKg > liftKg * 0.5f) return Risk.Heavy;
            return Risk.Safe;
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
