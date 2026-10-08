using System;
using UnityEngine;

namespace DesalEra.Unity.Inspect
{
    /// <summary>
    /// Inspector 分组标题（可中文）。画在下一个字段上方，用法近似 Odin TitleGroup / Unity Header。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class TitleGroupAttribute : PropertyAttribute
    {
        public string Title { get; }
        public bool Bold { get; }

        public TitleGroupAttribute(string title, bool bold = true)
        {
            Title = title ?? string.Empty;
            Bold = bold;
            order = -1000; // draw before the field
        }
    }
}
