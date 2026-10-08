using System;
using System.Collections.Generic;
using System.Reflection;
using DesalEra.Unity.Inspect;
using UnityEngine;

namespace DesalEra.EditorTools.Inspect
{
    internal static class InspectTypeCache
    {
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Dictionary<Type, bool> NeedsCustom = new Dictionary<Type, bool>();

        /// <summary>
        /// 仅当存在需接管 Inspector 的注解时才用 InspectEditor（LabelText 等走 PropertyDrawer 即可）。
        /// </summary>
        public static bool NeedsInspectEditor(Type type)
        {
            if (type == null) return false;
            if (NeedsCustom.TryGetValue(type, out bool cached)) return cached;

            bool need = false;
            foreach (MemberInfo m in type.GetMembers(Flags))
            {
                if (m.IsDefined(typeof(ButtonAttribute), true)
                    || m.IsDefined(typeof(ShowInInspectorAttribute), true)
                    || m.IsDefined(typeof(InlineEditorAttribute), true)
                    || m.IsDefined(typeof(OnInspectorInitAttribute), true)
                    || m.IsDefined(typeof(FoldoutGroupAttribute), true))
                {
                    need = true;
                    break;
                }
            }

            NeedsCustom[type] = need;
            return need;
        }

        public static MethodInfo[] GetButtons(Type type)
        {
            var list = new List<MethodInfo>();
            foreach (MethodInfo m in type.GetMethods(Flags))
            {
                if (m.IsDefined(typeof(ButtonAttribute), true) && m.GetParameters().Length == 0)
                    list.Add(m);
            }
            return list.ToArray();
        }

        public static MethodInfo[] GetInspectorInits(Type type)
        {
            var list = new List<MethodInfo>();
            foreach (MethodInfo m in type.GetMethods(Flags))
            {
                if (m.IsDefined(typeof(OnInspectorInitAttribute), true) && m.GetParameters().Length == 0)
                    list.Add(m);
            }
            return list.ToArray();
        }

        public static MemberInfo[] GetShowInInspector(Type type)
        {
            var list = new List<MemberInfo>();
            foreach (PropertyInfo p in type.GetProperties(Flags))
            {
                if (p.IsDefined(typeof(ShowInInspectorAttribute), true) && p.GetIndexParameters().Length == 0)
                    list.Add(p);
            }
            foreach (FieldInfo f in type.GetFields(Flags))
            {
                if (f.IsDefined(typeof(ShowInInspectorAttribute), true) && f.IsDefined(typeof(SerializeField), true) == false)
                    list.Add(f);
            }
            return list.ToArray();
        }
    }
}
