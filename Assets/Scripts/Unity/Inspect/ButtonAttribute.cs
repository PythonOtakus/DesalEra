using System;

namespace DesalEra.Unity.Inspect
{
    /// <summary>在 Inspector 画按钮并调用无参方法。近似 Odin Button。</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ButtonAttribute : Attribute
    {
        public string Name { get; }
        public int Height { get; set; } = 28;
        /// <summary>调用后对 target（及可选引用字段）标脏并 AssetDatabase.SaveAssets。</summary>
        public bool SaveAssets { get; set; }

        public ButtonAttribute(string name = null)
        {
            Name = name;
        }
    }
}
