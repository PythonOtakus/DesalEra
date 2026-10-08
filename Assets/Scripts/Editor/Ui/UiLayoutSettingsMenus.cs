using DesalEra.Unity.Ui;
using UnityEditor;
using UnityEngine;

namespace DesalEra.EditorTools
{
    public static class UiLayoutSettingsMenus
    {
        private const string AssetPath = "Assets/Resources/UiLayoutSettings.asset";

        [MenuItem("DesalEra/UI/选中 Ui 布局 (Play)")]
        public static void SelectUiLayout()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[UiLayout] 请先进入 Play；选 Hierarchy 的 GameEntry/Ui（UiRoot）。");
                return;
            }

            var root = Object.FindObjectOfType<UiRoot>();
            if (root == null)
            {
                Debug.LogWarning("[UiLayout] 未找到 UiRoot。");
                return;
            }

            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root.gameObject);
        }

        [MenuItem("DesalEra/UI/选中 Ui 布局 (Play)", true)]
        private static bool SelectUiLayoutValidate() => Application.isPlaying;

        [MenuItem("DesalEra/UI/创建布局配置资源")]
        public static void CreateAsset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var existing = AssetDatabase.LoadAssetAtPath<UiLayoutSettings>(AssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("[UiLayout] 资源已存在: " + AssetPath);
                return;
            }

            var asset = ScriptableObject.CreateInstance<UiLayoutSettings>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log("[UiLayout] 已创建 " + AssetPath);
        }
    }
}
