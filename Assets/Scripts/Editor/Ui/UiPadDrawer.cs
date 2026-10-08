using System.Reflection;
using DesalEra.Unity.Inspect;
using DesalEra.Unity.Ui;
using UnityEditor;
using UnityEngine;

namespace DesalEra.EditorTools
{
    /// <summary>
    /// Pad 有类型级 Drawer，会盖掉字段上的 LabelTextAttribute Drawer，故在此读取注解。
    /// </summary>
    [CustomPropertyDrawer(typeof(UiLayoutSettings.Pad))]
    public sealed class UiPadDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;
            return EditorGUIUtility.singleLineHeight * 5f
                   + EditorGUIUtility.standardVerticalSpacing * 4f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = ResolveLabel(label);
            EditorGUI.BeginProperty(position, label, property);

            Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            float step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            row.y += step;
            DrawChild(row, property.FindPropertyRelative("left"));
            row.y += step;
            DrawChild(row, property.FindPropertyRelative("bottom"));
            row.y += step;
            DrawChild(row, property.FindPropertyRelative("right"));
            row.y += step;
            DrawChild(row, property.FindPropertyRelative("top"));
            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }

        private GUIContent ResolveLabel(GUIContent fallback)
        {
            var attr = fieldInfo?.GetCustomAttribute<LabelTextAttribute>(true);
            if (attr != null) return new GUIContent(attr.Text, attr.Tooltip);
            return fallback;
        }

        private static void DrawChild(Rect row, SerializedProperty child)
        {
            if (child == null) return;
            // 子字段由 LabelTextDrawer 负责中文名；此处用默认 PropertyField 触发 Attribute Drawer。
            EditorGUI.PropertyField(row, child, true);
        }
    }
}
