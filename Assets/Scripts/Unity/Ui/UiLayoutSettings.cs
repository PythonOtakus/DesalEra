using DesalEra.Unity.Inspect;
using UnityEngine;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// HUD 布局：按控件 Foldout 分组；每组含尺寸 / 内边距 / 各文字角色的 <see cref="UiTextStyle"/>。
    /// </summary>
    [CreateAssetMenu(fileName = "UiLayoutSettings", menuName = "DesalEra/UI 布局配置", order = 10)]
    public sealed class UiLayoutSettings : ScriptableObject
    {
        [System.Serializable]
        public struct Pad
        {
            [LabelText("左", "相对面板宽度的比例 0–0.45")]
            [Range(0f, 0.45f)] public float left;

            [LabelText("下", "相对面板高度的比例 0–0.45")]
            [Range(0f, 0.45f)] public float bottom;

            [LabelText("右", "相对面板宽度的比例 0–0.45")]
            [Range(0f, 0.45f)] public float right;

            [LabelText("上", "相对面板高度的比例 0–0.45")]
            [Range(0f, 0.45f)] public float top;

            public Pad(float left, float bottom, float right, float top)
            {
                this.left = left;
                this.bottom = bottom;
                this.right = right;
                this.top = top;
            }

            public Vector4 ToVector4() => new Vector4(left, bottom, right, top);
        }

        // ── 屏幕 ──────────────────────────────────────────
        [FoldoutGroup("屏幕")]
        [LabelText("屏幕边距")]
        public float screenMargin = 24f;

        [FoldoutGroup("屏幕")]
        [LabelText("底栏离底距离")]
        public float bottomY = 28f;

        [FoldoutGroup("屏幕")]
        [LabelText("参考分辨率宽")]
        public float referenceWidth = 1920f;

        // ── 小地图 ────────────────────────────────────────
        [FoldoutGroup("小地图")]
        [LabelText("边长")]
        public float minimapSide = 260f;

        [FoldoutGroup("小地图")]
        [InfoBox("内边距为相对宽/高比例：左、下、右、上。", InfoBoxType.None)]
        [LabelText("内容内边距")]
        public Pad padMinimap = new Pad(0.11f, 0.14f, 0.11f, 0.11f);

        [FoldoutGroup("小地图")]
        [LabelText("指北文字")]
        public UiTextStyle minimapNorth = new UiTextStyle(22, TextAnchor.MiddleCenter, bold: true);

        [FoldoutGroup("小地图")]
        [LabelText("浪高文字")]
        public UiTextStyle minimapWave = new UiTextStyle(22, TextAnchor.MiddleCenter);

        // ── 建造栏 ────────────────────────────────────────
        [FoldoutGroup("建造栏")]
        [LabelText("宽度")]
        public float buildBarWidth = 800f;

        [FoldoutGroup("建造栏")]
        [LabelText("内容内边距")]
        public Pad padProductList = new Pad(0.032f, 0.11f, 0.032f, 0.11f);

        [FoldoutGroup("建造栏")]
        [LabelText("操作提示离底")]
        public float hintY = 6f;

        [FoldoutGroup("建造栏")]
        [LabelText("标题与底栏间距")]
        public float categoryGap = 6f;

        [FoldoutGroup("建造栏")]
        [LabelText("缺少皮肤时后备高度")]
        public float buildDockHeight = 160f;

        [FoldoutGroup("建造栏")]
        [LabelText("缺少皮肤时槽边长")]
        public float buildSlot = 92f;

        [FoldoutGroup("建造栏")]
        [LabelText("分类标题文字")]
        public UiTextStyle buildCategory = new UiTextStyle(18, TextAnchor.MiddleLeft);

        [FoldoutGroup("建造栏")]
        [LabelText("底栏提示文字")]
        public UiTextStyle buildHint = new UiTextStyle(18, TextAnchor.MiddleLeft);

        [FoldoutGroup("建造栏")]
        [LabelText("槽位标题文字")]
        public UiTextStyle buildCardTitle = new UiTextStyle(22, TextAnchor.MiddleCenter);

        [FoldoutGroup("建造栏")]
        [LabelText("槽位热键文字")]
        public UiTextStyle buildCardBadge = new UiTextStyle(14, TextAnchor.MiddleCenter);

        // ── 状态条 ────────────────────────────────────────
        [FoldoutGroup("状态条")]
        [LabelText("宽度")]
        public float statusWidth = 360f;

        [FoldoutGroup("状态条")]
        [LabelText("内容内边距")]
        public Pad padStatus = new Pad(0.07f, 0.14f, 0.07f, 0.14f);

        [FoldoutGroup("状态条")]
        [LabelText("缺少皮肤时后备尺寸")]
        public Vector2 vitalsSize = new Vector2(400f, 170f);

        [FoldoutGroup("状态条")]
        [LabelText("行间距", "四条维生之间的空隙")]
        public float statusRowSpacing = 6f;

        [FoldoutGroup("状态条")]
        [LabelText("行内间距", "图标 / 标签 / 进度条 / 数值之间的空隙")]
        public float statusItemSpacing = 6f;

        [FoldoutGroup("状态条")]
        [LabelText("维生标签文字")]
        public UiTextStyle statusLabel = new UiTextStyle(22, TextAnchor.MiddleLeft);

        [FoldoutGroup("状态条")]
        [LabelText("数值文字")]
        public UiTextStyle statusNumber = new UiTextStyle(18, TextAnchor.MiddleRight);

        // ── 资源条 ────────────────────────────────────────
        [FoldoutGroup("资源条")]
        [LabelText("宽度")]
        public float resourceWidth = 400f;

        [FoldoutGroup("资源条")]
        [LabelText("内容内边距")]
        public Pad padMaterialList = new Pad(0.035f, 0.12f, 0.035f, 0.12f);

        [FoldoutGroup("资源条")]
        [LabelText("数量文字")]
        public UiTextStyle resourceCount = new UiTextStyle(28, TextAnchor.MiddleCenter);

        // ── 详情面板 ──────────────────────────────────────
        [FoldoutGroup("详情面板")]
        [LabelText("宽度")]
        public float detailWidth = 460f;

        [FoldoutGroup("详情面板")]
        [LabelText("内容内边距")]
        public Pad padDetail = new Pad(0.09f, 0.07f, 0.09f, 0.055f);

        [FoldoutGroup("详情面板")]
        [LabelText("主栈间距")]
        public float detailStackSpacing = 10f;

        [FoldoutGroup("详情面板")]
        [LabelText("材料行高")]
        public float detailRowHeight = 76f;

        [FoldoutGroup("详情面板")]
        [LabelText("材料行间距")]
        public float detailRowSpacing = 8f;

        [FoldoutGroup("详情面板")]
        [LabelText("标题文字")]
        public UiTextStyle detailTitle = new UiTextStyle(36, TextAnchor.MiddleLeft, bold: true, preferredHeight: 52f);

        [FoldoutGroup("详情面板")]
        [LabelText("警告文字")]
        public UiTextStyle detailWarning = new UiTextStyle(22, TextAnchor.MiddleLeft, preferredHeight: 52f);

        [FoldoutGroup("详情面板")]
        [LabelText("消耗文字")]
        public UiTextStyle detailCost = new UiTextStyle(28, TextAnchor.MiddleLeft, preferredHeight: 40f);

        [FoldoutGroup("详情面板")]
        [LabelText("材料名文字")]
        public UiTextStyle detailRowTitle = new UiTextStyle(22, TextAnchor.MiddleLeft);

        [FoldoutGroup("详情面板")]
        [LabelText("材料说明文字")]
        public UiTextStyle detailRowSub = new UiTextStyle(20, TextAnchor.MiddleLeft);

        [FoldoutGroup("详情面板")]
        [LabelText("底栏提示文字")]
        public UiTextStyle detailHint = new UiTextStyle(18, TextAnchor.MiddleLeft, preferredHeight: 28f);

        // ── 其它（背包 / 通知等）──────────────────────────
        [FoldoutGroup("其它", expandedByDefault: false)]
        [LabelText("标题文字")]
        public UiTextStyle otherTitle = new UiTextStyle(36, TextAnchor.UpperLeft, bold: true);

        [FoldoutGroup("其它", expandedByDefault: false)]
        [LabelText("正文文字")]
        public UiTextStyle otherBody = new UiTextStyle(22, TextAnchor.UpperLeft);

        [FoldoutGroup("其它", expandedByDefault: false)]
        [LabelText("辅助文字")]
        public UiTextStyle otherSmall = new UiTextStyle(18, TextAnchor.UpperLeft);

        // ── 通用度量 ──────────────────────────────────────
        [FoldoutGroup("通用度量", expandedByDefault: false)]
        [LabelText("通用面板内边距")]
        public float panelPadding = 18f;

        [FoldoutGroup("通用度量", expandedByDefault: false)]
        [LabelText("行间距")]
        public float rowGap = 8f;

        [FoldoutGroup("通用度量", expandedByDefault: false)]
        [LabelText("圆角半径")]
        public int cornerRadius = 8;

        [FoldoutGroup("通用度量", expandedByDefault: false)]
        [LabelText("行高")]
        public float rowHeight = 32f;

        [FoldoutGroup("通用度量", expandedByDefault: false)]
        [LabelText("按钮高度")]
        public float buttonHeight = 72f;

        [FoldoutGroup("通用度量", expandedByDefault: false)]
        [LabelText("通用槽尺寸")]
        public float slotSize = 88f;

        [FoldoutGroup("通用度量", expandedByDefault: false)]
        [LabelText("精灵边框")]
        public int spriteBorder = 12;

        public Pad PadFor(UiSkins.Kind kind)
        {
            switch (kind)
            {
                case UiSkins.Kind.Minimap: return padMinimap;
                case UiSkins.Kind.ProductList: return padProductList;
                case UiSkins.Kind.Status: return padStatus;
                case UiSkins.Kind.MaterialList: return padMaterialList;
                case UiSkins.Kind.Detail: return padDetail;
                default: return new Pad(0.08f, 0.08f, 0.08f, 0.08f);
            }
        }

        public void CopyFrom(UiLayoutSettings other)
        {
            if (other == null) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(other), this);
        }
    }
}
