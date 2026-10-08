using System;
using UnityEngine;

namespace DesalEra.Unity.Inspect
{
    public enum InfoBoxType
    {
        None,
        Info,
        Warning,
        Error
    }

    /// <summary>字段上方的说明条，近似 Odin InfoBox。</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class InfoBoxAttribute : PropertyAttribute
    {
        public string Message { get; }
        public InfoBoxType Type { get; }

        public InfoBoxAttribute(string message, InfoBoxType type = InfoBoxType.Info)
        {
            Message = message ?? string.Empty;
            Type = type;
            order = -900;
        }
    }
}
