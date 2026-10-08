using System;

namespace DesalEra.Unity.Inspect
{
    /// <summary>字段/内嵌对象变更后调用无参方法。近似 Odin OnValueChanged。</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class OnValueChangedAttribute : Attribute
    {
        public string MethodName { get; }

        public OnValueChangedAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }
}
