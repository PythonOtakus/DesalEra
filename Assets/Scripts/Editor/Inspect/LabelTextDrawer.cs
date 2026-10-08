using System.Reflection;
using DesalEra.Unity.Inspect;
using UnityEditor;
using UnityEngine;

namespace DesalEra.EditorTools.Inspect
{
    /// <summary>
    /// LabelText 会抢走默认 Drawer，故在此兼容 Range / Multiline。
    /// </summary>
    [CustomPropertyDrawer(typeof(LabelTextAttribute))]
    public sealed class LabelTextDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.String && HasMultiline(out int lines))
                return EditorGUIUtility.singleLineHeight * Mathf.Max(2, lines)
                       + EditorGUIUtility.standardVerticalSpacing;
            return EditorGUI.GetPropertyHeight(property, LabelContent(), true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = LabelContent();
            EditorGUI.BeginProperty(position, label, property);

            if (TryDrawRange(position, property, label))
            {
                EditorGUI.EndProperty();
                return;
            }

            if (property.propertyType == SerializedPropertyType.String && HasMultiline(out int lines))
            {
                Rect labelRect = new Rect(position.x, position.y, position.width,
                    EditorGUIUtility.singleLineHeight);
                EditorGUI.PrefixLabel(labelRect, label);
                Rect area = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight,
                    position.width, EditorGUIUtility.singleLineHeight * Mathf.Max(1, lines - 1));
                property.stringValue = EditorGUI.TextArea(area, property.stringValue);
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.PropertyField(position, property, label, true);
            EditorGUI.EndProperty();
        }

        private GUIContent LabelContent()
        {
            var attr = (LabelTextAttribute)attribute;
            return new GUIContent(attr.Text, attr.Tooltip);
        }

        private bool TryDrawRange(Rect position, SerializedProperty property, GUIContent label)
        {
            var range = fieldInfo?.GetCustomAttribute<RangeAttribute>(true);
            if (range == null) return false;

            if (property.propertyType == SerializedPropertyType.Float)
            {
                EditorGUI.Slider(position, property, range.min, range.max, label);
                return true;
            }

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                EditorGUI.IntSlider(position, property, (int)range.min, (int)range.max, label);
                return true;
            }

            return false;
        }

        private bool HasMultiline(out int lines)
        {
            lines = 0;
            var multi = fieldInfo?.GetCustomAttribute<MultilineAttribute>(true);
            if (multi == null) return false;
            lines = multi.lines;
            return true;
        }
    }
}
