using System;
using System.Reflection;
using DesalEra.Unity.Inspect;
using UnityEngine;

namespace DesalEra.EditorTools.Inspect
{
    internal static class InspectMemberResolver
    {
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static bool EvaluateEnableIf(object target, EnableIfAttribute attr)
        {
            if (attr == null) return true;
            if (!string.IsNullOrEmpty(attr.MemberName))
                return EvaluateBoolMember(target, attr.MemberName);

            switch (attr.Mode)
            {
                case EnableIfMode.IsPlaying: return Application.isPlaying;
                case EnableIfMode.IsEditing: return !Application.isPlaying;
                default: return true;
            }
        }

        public static bool EvaluateBoolMember(object target, string name)
        {
            if (target == null || string.IsNullOrEmpty(name)) return false;
            Type type = target.GetType();

            PropertyInfo prop = type.GetProperty(name, Flags);
            if (prop != null && prop.PropertyType == typeof(bool) && prop.GetIndexParameters().Length == 0)
                return (bool)prop.GetValue(target);

            FieldInfo field = type.GetField(name, Flags);
            if (field != null && field.FieldType == typeof(bool))
                return (bool)field.GetValue(target);

            MethodInfo method = type.GetMethod(name, Flags, null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(bool))
                return (bool)method.Invoke(target, null);

            return false;
        }

        public static object GetMemberValue(object target, MemberInfo member)
        {
            switch (member)
            {
                case PropertyInfo p: return p.GetValue(target);
                case FieldInfo f: return f.GetValue(target);
                default: return null;
            }
        }

        public static MethodInfo FindMethod(object target, string name)
        {
            return target?.GetType().GetMethod(name, Flags, null, Type.EmptyTypes, null);
        }

        public static void Invoke(object target, MethodInfo method)
        {
            if (target == null || method == null) return;
            method.Invoke(target, null);
        }
    }
}
