using System;

namespace DesalEra.Unity.Inspect
{
    /// <summary>内嵌绘制引用的 UnityEngine.Object（常与 ShowInInspector 联用）。近似 Odin InlineEditor。</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class InlineEditorAttribute : Attribute
    {
        public bool DrawHeader { get; set; } = true;
    }
}
