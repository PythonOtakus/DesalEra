using System;

namespace DesalEra.Unity.Inspect
{
    public enum EnableIfMode
    {
        Always,
        IsPlaying,
        IsEditing
    }

    /// <summary>
    /// 控制按钮/字段是否可交互。memberName 指向 bool 属性/字段/无参方法；
    /// 或使用 <see cref="EnableIfMode"/>。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false)]
    public sealed class EnableIfAttribute : Attribute
    {
        public EnableIfMode Mode { get; }
        public string MemberName { get; }

        public EnableIfAttribute(EnableIfMode mode)
        {
            Mode = mode;
            MemberName = null;
        }

        public EnableIfAttribute(string memberName)
        {
            Mode = EnableIfMode.Always;
            MemberName = memberName;
        }
    }
}
