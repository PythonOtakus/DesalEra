using System;

namespace DesalEra.Unity.Inspect
{
    /// <summary>Inspector 绘制前调用一次无参方法（每帧重绘前也会调用，方法宜幂等）。</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class OnInspectorInitAttribute : Attribute
    {
    }
}
