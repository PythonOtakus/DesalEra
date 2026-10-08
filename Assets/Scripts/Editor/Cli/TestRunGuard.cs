using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DesalEra.EditorTools.Cli
{
    /// <summary>
    /// unity-cli 0.4.1 waits for RunFinished only. When the test framework aborts a run instead
    /// (e.g. an EditMode run started during Play mode), RunFinished never fires, the connector's
    /// command lock is never released and every later command times out. A domain reload
    /// recreates that lock, so on a failed run we leave Play mode and request one. Runs that stall
    /// without reporting a failure need the Ctrl+Alt+R menu item instead.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class TestRunGuard : IErrorCallbacks
    {
        static TestRunGuard()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.hideFlags = HideFlags.HideAndDontSave;
            api.RegisterCallbacks(new TestRunGuard());
        }

        public void OnError(string message)
        {
            Debug.LogWarning($"[DesalEra] Test run aborted ({message}); reloading scripts to release the unity-cli connector.");
            ReloadScripts();
        }

        // Manual escape hatch for a wedged connector: no CLI command can get through, but a hotkey can.
        [MenuItem("DesalEra/Reload Scripts %&r")]
        static void ReloadScripts()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.playModeStateChanged += ReloadOnEditMode;
                EditorApplication.isPlaying = false;
                return;
            }
            EditorUtility.RequestScriptReload();
        }

        static void ReloadOnEditMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= ReloadOnEditMode;
            EditorUtility.RequestScriptReload();
        }

        public void RunStarted(ITestAdaptor testsToRun) { }
        public void RunFinished(ITestResultAdaptor result) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
    }
}
