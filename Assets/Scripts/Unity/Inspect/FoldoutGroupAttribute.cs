using System;
using UnityEngine;

namespace DesalEra.Unity.Inspect
{
    /// <summary>
    /// 同名字段收入可折叠分组。近似 Odin FoldoutGroup。
    /// 需由 InspectEditor 接管绘制（勿只靠 DecoratorDrawer）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class FoldoutGroupAttribute : PropertyAttribute
    {
        public string GroupName { get; }
        public bool ExpandedByDefault { get; }

        public FoldoutGroupAttribute(string groupName, bool expandedByDefault = true)
        {
            GroupName = groupName ?? string.Empty;
            ExpandedByDefault = expandedByDefault;
            order = -1000;
        }
    }
}
