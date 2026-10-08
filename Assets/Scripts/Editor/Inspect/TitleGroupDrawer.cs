using DesalEra.Unity.Inspect;
using UnityEditor;
using UnityEngine;

namespace DesalEra.EditorTools.Inspect
{
    [CustomPropertyDrawer(typeof(TitleGroupAttribute))]
    public sealed class TitleGroupDrawer : DecoratorDrawer
    {
        public override float GetHeight()
        {
            return EditorGUIUtility.singleLineHeight + 6f;
        }

        public override void OnGUI(Rect position)
        {
            var attr = (TitleGroupAttribute)attribute;
            position.y += 4f;
            position.height = EditorGUIUtility.singleLineHeight;
            var style = attr.Bold ? EditorStyles.boldLabel : EditorStyles.label;
            EditorGUI.LabelField(position, attr.Title, style);
        }
    }
}
