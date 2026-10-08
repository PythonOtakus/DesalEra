using UnityEditor;
using UnityEngine;

namespace DesalEra.EditorTools.Inspect
{
    /// <summary>
    /// 通用 Inspector：含 Button / FoldoutGroup / ShowInInspector 等时接管，
    /// 否则走默认绘制（LabelText 等仍由 PropertyDrawer 生效）。
    /// </summary>
    [CustomEditor(typeof(MonoBehaviour), true)]
    [CanEditMultipleObjects]
    public sealed class InspectMonoBehaviourEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (!InspectTypeCache.NeedsInspectEditor(target.GetType()))
            {
                DrawDefaultInspector();
                return;
            }

            InspectDrawer.Draw(serializedObject, target);
        }
    }

    [CustomEditor(typeof(ScriptableObject), true)]
    [CanEditMultipleObjects]
    public sealed class InspectScriptableObjectEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (!InspectTypeCache.NeedsInspectEditor(target.GetType()))
            {
                DrawDefaultInspector();
                return;
            }

            InspectDrawer.Draw(serializedObject, target);
        }
    }
}
