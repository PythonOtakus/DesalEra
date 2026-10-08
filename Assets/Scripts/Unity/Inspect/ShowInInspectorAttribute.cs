using System;

namespace DesalEra.Unity.Inspect
{
    /// <summary>把非序列化属性/字段画进 Inspector。近似 Odin ShowInInspector。</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class ShowInInspectorAttribute : Attribute
    {
    }
}
