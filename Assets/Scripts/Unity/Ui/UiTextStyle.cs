using DesalEra.Unity.Inspect;
using UnityEngine;
using UnityEngine.UI;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// 单种文字的布局参数（字号、行距、对齐、加粗、行高）。各面板按角色各配一份。
    /// </summary>
    [System.Serializable]
    public struct UiTextStyle
    {
        [LabelText("字号")]
        public int size;

        [LabelText("行距", "Unity Text.lineSpacing，1 = 默认")]
        [Range(0.5f, 2.5f)]
        public float lineSpacing;

        [LabelText("对齐")]
        public TextAnchor alignment;

        [LabelText("加粗")]
        public bool bold;

        [LabelText("首选行高", "LayoutElement.preferredHeight；0 表示不强制")]
        public float preferredHeight;

        public UiTextStyle(int size, TextAnchor alignment = TextAnchor.MiddleLeft,
                           bool bold = false, float preferredHeight = 0f, float lineSpacing = 1f)
        {
            this.size = size;
            this.alignment = alignment;
            this.bold = bold;
            this.preferredHeight = preferredHeight;
            this.lineSpacing = lineSpacing;
        }

        public void ApplyTo(Text label)
        {
            if (label == null) return;
            label.fontSize = Mathf.Max(1, size);
            label.lineSpacing = lineSpacing <= 0f ? 1f : lineSpacing;
            label.alignment = alignment;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            if (label.resizeTextForBestFit)
                label.resizeTextMaxSize = Mathf.Max(1, size);
        }

        public void ApplyPreferredHeight(GameObject go)
        {
            if (go == null || preferredHeight <= 0f) return;
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
        }
    }
}
