using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

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
    }
}
