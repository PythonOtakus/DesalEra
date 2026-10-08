using DesalEra.Game;
using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Bottom-right resource glance on MaterialList.png (four wells).
    /// </summary>
    public sealed class ResourceStripView : MonoBehaviour
    {
        private static readonly ResourceKind[] Shown =
        {
            ResourceKind.Plank, ResourceKind.Metal, ResourceKind.Scrap, ResourceKind.Water
        };

        private readonly Text[] _counts = new Text[4];
        private BuildPiece _selected;

        public static ResourceStripView Create(Transform parent)
        {
            var go = new GameObject("ResourceStrip", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var view = go.AddComponent<ResourceStripView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            UiFactory.FillParent(transform);

            Image panel = UiSkins.Panel(transform, UiSkins.Kind.MaterialList, out bool skinned);
            Vector2 size = UiSkins.Size(UiSkins.Kind.MaterialList, UiSkins.ResourceWidth);
            UiFactory.Anchor(panel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                             new Vector2(-UiTheme.ScreenMargin, UiSkins.BottomY), size);

            var row = UiFactory.Container(panel.transform, "Row");
            UiFactory.Stretch(row);
            UiSkins.ApplyContentPad(row, UiSkins.Kind.MaterialList, size);
            var layout = UiFactory.Horizontal(row, skinned ? 3f : 10f);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            for (int i = 0; i < Shown.Length; i++)
            {
                ResourceKind kind = Shown[i];
                RectTransform cell = UiFactory.Container(row, "Res_" + kind);
                cell.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

                Image icon = UiFactory.Sprite(cell, UiSprites.ResourceIcon(kind, 40),
                                              UiTheme.ResourceColor(kind), Image.Type.Simple);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform ir = icon.rectTransform;
                ir.anchorMin = new Vector2(0.18f, 0.42f);
                ir.anchorMax = new Vector2(0.82f, 0.92f);
                ir.offsetMin = Vector2.zero;
                ir.offsetMax = Vector2.zero;

                Text count = UiFactory.Text(cell, "0", UiLayout.Active.resourceCount, UiTheme.TextPrimary);
                count.rectTransform.anchorMin = new Vector2(0.05f, 0.04f);
                count.rectTransform.anchorMax = new Vector2(0.95f, 0.42f);
                count.rectTransform.offsetMin = Vector2.zero;
                count.rectTransform.offsetMax = Vector2.zero;
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
                _counts[i] = count;
            }
        }

        public void SetSelected(BuildPiece piece) => _selected = piece;

        public void Refresh(Inventory inventory)
        {
            if (inventory == null) return;
            for (int i = 0; i < Shown.Length; i++)
            {
                int have = inventory.Get(Shown[i]);
                _counts[i].text = have.ToString();
                bool shortfall = _selected != null && _selected.Cost.TryGetValue(Shown[i], out int need)
                                 && have < need;
                _counts[i].color = shortfall ? UiTheme.Bad
                                 : have == 0 ? UiTheme.TextDisabled
                                 : UiTheme.TextPrimary;
            }
        }
    }
}
