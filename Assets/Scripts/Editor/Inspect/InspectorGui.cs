using System.Reflection;
using DesalEra.Unity.Inspect;
using UnityEditor;
using UnityEngine;

namespace DesalEra.EditorTools.Inspect
{
    /// <summary>
    /// 绘制带 LabelText / 默认 displayName 的 SerializedProperty。
    /// 供自定义 Editor 遍历字段时使用（类型级 PropertyDrawer 会盖掉 Attribute Drawer 时尤其有用）。
    /// </summary>
    public static class InspectorGui
    {
        public static GUIContent ContentFor(SerializedProperty property, FieldInfo fieldInfo = null)
        {
            if (property == null) return GUIContent.none;

            LabelTextAttribute labelText = null;
            if (fieldInfo != null)
                labelText = fieldInfo.GetCustomAttribute<LabelTextAttribute>(true);

            // 嵌套字段：尝试从当前属性路径解析不到 FieldInfo 时，仍可用属性名匹配不到则回退。
            if (labelText != null)
                return new GUIContent(labelText.Text, labelText.Tooltip);

            return new GUIContent(property.displayName);
        }

        public static void PropertyField(SerializedProperty property, FieldInfo fieldInfo = null,
                                         bool includeChildren = true)
        {
            EditorGUILayout.PropertyField(property, ContentFor(property, fieldInfo), includeChildren);
        }

        public static void DrawDefaultInspectorChinese(SerializedObject so)
        {
            so.Update();
            SerializedProperty prop = so.GetIterator();
            bool enter = true;
            while (prop.NextVisible(enter))
            {
                enter = false;
                if (prop.name == "m_Script") continue;
                FieldInfo field = FindField(so.targetObject, prop);
                PropertyField(prop, field, true);
            }
            so.ApplyModifiedProperties();
        }

        public static FieldInfo FindField(Object target, SerializedProperty property)
        {
            if (target == null || property == null) return null;
            string path = property.propertyPath;
            // 简化：仅处理根字段与一层 "pad.left" 形式。
            Object obj = target;
            System.Type type = obj.GetType();
            string[] parts = path.Replace(".Array.data[", "[").Split('.');
            FieldInfo field = null;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                int bracket = part.IndexOf('[');
                if (bracket >= 0) part = part.Substring(0, bracket);
                field = type.GetField(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) return null;
                if (i < parts.Length - 1)
                {
                    type = field.FieldType;
                    if (type.IsArray) type = type.GetElementType();
                }
            }
            return field;
        }
    }
}
