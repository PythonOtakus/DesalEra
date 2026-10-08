using System;
using UnityEngine;

namespace DesalEra.Unity.Inspect
{
    /// <summary>
    /// Inspector 显示名（可中文）。用法同 Odin 的 LabelText。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class LabelTextAttribute : PropertyAttribute
    {
        public string Text { get; }
        public string Tooltip { get; }

        public LabelTextAttribute(string text, string tooltip = null)
        {
            Text = text ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
        }
    }
}
