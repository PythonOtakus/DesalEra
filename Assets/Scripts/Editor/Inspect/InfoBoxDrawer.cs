using DesalEra.Unity.Inspect;
using UnityEditor;
using UnityEngine;

namespace DesalEra.EditorTools.Inspect
{
    [CustomPropertyDrawer(typeof(InfoBoxAttribute))]
    public sealed class InfoBoxDrawer : DecoratorDrawer
    {
        public override float GetHeight()
        {
            var attr = (InfoBoxAttribute)attribute;
            return EditorStyles.helpBox.CalcHeight(new GUIContent(attr.Message),
                       EditorGUIUtility.currentViewWidth - 40f) + 4f;
        }

        public override void OnGUI(Rect position)
        {
            var attr = (InfoBoxAttribute)attribute;
            MessageType type = MessageType.None;
            switch (attr.Type)
            {
                case InfoBoxType.Info: type = MessageType.Info; break;
                case InfoBoxType.Warning: type = MessageType.Warning; break;
                case InfoBoxType.Error: type = MessageType.Error; break;
            }
            position.y += 2f;
            position.height -= 2f;
            EditorGUI.HelpBox(position, attr.Message, type);
        }
    }
}
