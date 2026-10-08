using System.Reflection;
using DesalEra.Unity.Inspect;
using UnityEditor;
using UnityEngine;

namespace DesalEra.EditorTools.Inspect
{
    /// <summary>按注解绘制字段、内嵌对象与按钮（供通用 InspectEditor 使用）。</summary>
    public static class InspectDrawer
    {
        public static void Draw(SerializedObject serializedObject, Object target)
        {
            if (serializedObject == null || target == null) return;

            foreach (MethodInfo init in InspectTypeCache.GetInspectorInits(target.GetType()))
                InspectMemberResolver.Invoke(target, init);

            serializedObject.Update();
            DrawSerializedFields(serializedObject);
            bool changed = serializedObject.ApplyModifiedProperties();

            // 按钮放在长内嵌面板之前，避免被挤出视口。
            DrawButtons(target);

            foreach (MemberInfo member in InspectTypeCache.GetShowInInspector(target.GetType()))
            {
                if (DrawShowInInspector(target, member))
                    changed = true;
            }

            if (changed)
                InvokeOnValueChangedForSerialized(target, serializedObject);
        }

        private static void DrawSerializedFields(SerializedObject so)
        {
            string currentGroup = null;
            bool groupExpanded = true;
            Object target = so.targetObject;

            SerializedProperty prop = so.GetIterator();
            bool enter = true;
            while (prop.NextVisible(enter))
            {
                enter = false;
                if (prop.name == "m_Script")
                {
                    EndFoldoutGroup(ref currentGroup);
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(prop);
                    continue;
                }

                FieldInfo field = InspectorGui.FindField(target, prop);
                var foldout = field?.GetCustomAttribute<FoldoutGroupAttribute>(true);
                string groupName = foldout?.GroupName;

                if (groupName != currentGroup)
                {
                    EndFoldoutGroup(ref currentGroup);
                    if (!string.IsNullOrEmpty(groupName))
                    {
                        currentGroup = groupName;
                        bool defaultOpen = foldout == null || foldout.ExpandedByDefault;
                        groupExpanded = BeginFoldoutGroup(target, groupName, defaultOpen);
                    }
                }

                if (currentGroup != null && !groupExpanded)
                    continue;

                var enableIf = field?.GetCustomAttribute<EnableIfAttribute>(true);
                bool enabled = InspectMemberResolver.EvaluateEnableIf(target, enableIf);
                using (new EditorGUI.DisabledScope(!enabled))
                    InspectorGui.PropertyField(prop, field, true);
            }

            EndFoldoutGroup(ref currentGroup);
        }

        private static bool BeginFoldoutGroup(Object target, string groupName, bool defaultOpen)
        {
            string key = FoldoutKey(target, groupName);
            bool expanded = SessionState.GetBool(key, defaultOpen);
            expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, groupName);
            SessionState.SetBool(key, expanded);
            return expanded;
        }

        private static void EndFoldoutGroup(ref string currentGroup)
        {
            if (currentGroup == null) return;
            EditorGUILayout.EndFoldoutHeaderGroup();
            currentGroup = null;
        }

        private static string FoldoutKey(Object target, string groupName)
        {
            int id = target != null ? target.GetInstanceID() : 0;
            return "DesalEra.Inspect.Foldout." + id + "." + groupName;
        }

        private static bool DrawShowInInspector(Object target, MemberInfo member)
        {
            object value = InspectMemberResolver.GetMemberValue(target, member);
            var labelAttr = member.GetCustomAttribute<LabelTextAttribute>(true);
            string label = labelAttr != null ? labelAttr.Text : ObjectNames.NicifyVariableName(member.Name);
            string tooltip = labelAttr?.Tooltip ?? string.Empty;

            var inline = member.GetCustomAttribute<InlineEditorAttribute>(true);
            if (inline != null && value is Object unityObj)
            {
                if (unityObj == null)
                {
                    EditorGUILayout.HelpBox(label + "：无对象", MessageType.None);
                    return false;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (inline.DrawHeader)
                    {
                        EditorGUILayout.LabelField(
                            Application.isPlaying ? label + "（Play · 实时）" : label,
                            EditorStyles.boldLabel);
                    }

                    var so = new SerializedObject(unityObj);
                    EditorGUI.BeginChangeCheck();
                    // 内嵌对象同样走 FoldoutGroup 等分组绘制。
                    DrawSerializedFields(so);
                    bool changed = EditorGUI.EndChangeCheck();
                    so.ApplyModifiedProperties();
                    if (changed)
                        InvokeOnValueChanged(target, member);
                    return changed;
                }
            }

            // 只读预览常见类型
            using (new EditorGUI.DisabledScope(true))
            {
                if (value is Object obj)
                    EditorGUILayout.ObjectField(new GUIContent(label, tooltip), obj, typeof(Object), true);
                else if (value != null)
                    EditorGUILayout.LabelField(new GUIContent(label, tooltip), new GUIContent(value.ToString()));
                else
                    EditorGUILayout.LabelField(new GUIContent(label, tooltip), new GUIContent("null"));
            }
            return false;
        }

        private static void DrawButtons(Object target)
        {
            MethodInfo[] buttons = InspectTypeCache.GetButtons(target.GetType());
            if (buttons.Length == 0) return;

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (MethodInfo method in buttons)
                {
                    var button = method.GetCustomAttribute<ButtonAttribute>(true);
                    var enableIf = method.GetCustomAttribute<EnableIfAttribute>(true);
                    bool enabled = InspectMemberResolver.EvaluateEnableIf(target, enableIf);
                    string name = string.IsNullOrEmpty(button.Name)
                        ? ObjectNames.NicifyVariableName(method.Name)
                        : button.Name;

                    using (new EditorGUI.DisabledScope(!enabled))
                    {
                        if (GUILayout.Button(name, GUILayout.Height(button.Height)))
                        {
                            InspectMemberResolver.Invoke(target, method);
                            if (button.SaveAssets)
                                SaveDirtyAssets(target);
                            // 勿对运行时 Component SetDirty / SaveAssets——Play 中会搞坏运行时贴图。
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 只处理工程内持久化 SO。Play 中经 <see cref="PlaySafeAssetSave"/> 延迟到退出时写盘。
        /// </summary>
        private static void SaveDirtyAssets(Object target)
        {
            var assets = new System.Collections.Generic.HashSet<Object>();
            CollectPersistentAssets(target, assets);
            if (target is Component comp)
            {
                foreach (Component sibling in comp.gameObject.GetComponents<Component>())
                    CollectPersistentAssets(sibling, assets);
            }

            foreach (Object asset in assets)
                PlaySafeAssetSave.Mark(asset);
        }

        private static void CollectPersistentAssets(Object obj,
            System.Collections.Generic.HashSet<Object> into)
        {
            if (obj == null) return;
            if (EditorUtility.IsPersistent(obj))
                into.Add(obj);

            foreach (FieldInfo field in obj.GetType().GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.GetValue(obj) is Object refObj && EditorUtility.IsPersistent(refObj))
                    into.Add(refObj);
            }
        }

        private static void InvokeOnValueChanged(Object target, MemberInfo member)
        {
            foreach (var attr in member.GetCustomAttributes<OnValueChangedAttribute>(true))
            {
                MethodInfo method = InspectMemberResolver.FindMethod(target, attr.MethodName);
                InspectMemberResolver.Invoke(target, method);
            }
        }

        private static void InvokeOnValueChangedForSerialized(Object target, SerializedObject so)
        {
            // 根字段带 OnValueChanged 时，任意序列化变更都触发（简化实现）。
            foreach (FieldInfo field in target.GetType().GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var attrs = field.GetCustomAttributes<OnValueChangedAttribute>(true);
                foreach (var attr in attrs)
                {
                    MethodInfo method = InspectMemberResolver.FindMethod(target, attr.MethodName);
                    InspectMemberResolver.Invoke(target, method);
                }
            }
        }
    }
}
