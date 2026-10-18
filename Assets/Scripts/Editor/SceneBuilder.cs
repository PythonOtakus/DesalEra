using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using DesalEra.Unity;

namespace DesalEra.EditorTools
{
    /// <summary>
    /// Creates the playable scene and wires the entry point.
    ///
    /// The scene this produces holds exactly one GameObject with one component on it.
    /// Everything else, including the camera, lights, sea, raft and player, is
    /// constructed at runtime by <see cref="DesalEra.Unity.GameEntry"/>. That
    /// keeps the scene file small enough to read in a diff, which is the whole reason
    /// the world is not authored by hand in the inspector.
    ///
    /// Run from the menu, or headless:
    ///   Unity.exe -batchmode -executeMethod DesalEra.EditorTools.SceneBuilder.Build
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string BuildPath = "Builds/DesalEra.exe";
        private const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";

        [MenuItem("DesalEra/Build Playable Scene")]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var entryGo = new GameObject("GameEntry");
            entryGo.AddComponent<DesalEra.Unity.GameEntry>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[DesalEra] playable scene written to {ScenePath}");
        }

        [MenuItem("DesalEra/Build Windows Player")]
        public static void BuildPlayer()
        {
            Build();

            EnsureAlwaysIncludedShaders();

            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[DesalEra] player built: {BuildPath} ({summary.totalSize / 1048576} MB)");
            }
            else
            {
                Debug.LogError($"[DesalEra] build failed: {summary.result}, {summary.totalErrors} errors");
            }
        }

        /// <summary>
        /// Adds every shader in <see cref="RuntimeShaders"/> to Graphics Settings'
        /// Always Included Shaders, which is the only thing that keeps a shader reached
        /// through Shader.Find alive in a player.
        ///
        /// It runs on every player build rather than being a one-off manual step, because
        /// the failure it prevents is invisible until someone ships: stripping a shader
        /// changes no error, no warning and no log line. It only makes the sea and the sky
        /// come out flat, and only in the build, where a play-mode check cannot see it.
        /// Adding the entry is also cheaper than the alternative, which is a shader variant
        /// nobody notices missing.
        ///
        /// Idempotent, so an already-correct project file is left alone rather than
        /// rewritten.
        /// </summary>
        [MenuItem("DesalEra/Repair Always Included Shaders")]
        public static void EnsureAlwaysIncludedShaders()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath(GraphicsSettingsPath)
                .OfType<UnityEngine.Rendering.GraphicsSettings>()
                .FirstOrDefault();

            if (settings == null)
            {
                Debug.LogError($"[DesalEra] {GraphicsSettingsPath} could not be loaded; " +
                               "shaders the player looks up by name will be stripped");
                return;
            }

            var serialized = new SerializedObject(settings);
            var included = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (included == null || !included.isArray)
            {
                Debug.LogError("[DesalEra] m_AlwaysIncludedShaders not found in Graphics Settings");
                return;
            }

            var present = new HashSet<Object>();
            for (int i = 0; i < included.arraySize; i++)
                present.Add(included.GetArrayElementAtIndex(i).objectReferenceValue);

            var added = new List<string>();

            foreach (string name in RuntimeShaders.MustSurvive)
            {
                Shader shader = Resolve(name);
                if (shader == null)
                {
                    Debug.LogError($"[DesalEra] shader '{name}' not found; add it to " +
                                   "Always Included Shaders by hand or the player will lose it");
                    continue;
                }

                if (present.Contains(shader)) continue;

                included.InsertArrayElementAtIndex(included.arraySize);
                included.GetArrayElementAtIndex(included.arraySize - 1).objectReferenceValue = shader;
                present.Add(shader);
                added.Add(name);
            }

            if (added.Count == 0)
            {
                Debug.Log("[DesalEra] always-included shaders already complete");
                return;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DesalEra] added to Always Included Shaders: {string.Join(", ", added.ToArray())}");
        }

        private static Shader Resolve(string shaderName)
        {
            string path = RuntimeShaders.AssetPathFor(shaderName);
            if (path != null) return AssetDatabase.LoadAssetAtPath<Shader>(path);

            // Built-in shaders are not assets, so Shader.Find is the only way to get a
            // reference that Graphics Settings will serialise.
            return Shader.Find(shaderName);
        }
    }
}
