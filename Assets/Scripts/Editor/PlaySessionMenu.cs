using System.IO;
using System.Reflection;
using DesalEra.Unity;
using DesalEra.Unity.Session;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DesalEra.EditorTools
{
    /// <summary>
    /// Play-session recording controls: a Rec button beside the editor Play controls,
    /// plus quiet menu items that only log (no dialogs).
    /// </summary>
    [InitializeOnLoad]
    public static class PlaySessionMenu
    {
        private const string MenuRoot = "DesalEra/Session/";
        private const string ToolbarElementName = "DesalEraSessionRec";

        private static ToolbarButton _recButton;
        private static bool _injected;

        static PlaySessionMenu()
        {
            EditorApplication.update += TryInjectToolbar;
            EditorApplication.playModeStateChanged += _ => RefreshRecButton();
            EditorApplication.delayCall += RefreshRecButton;
        }

        [MenuItem(MenuRoot + "Start Recording %#r", priority = 0)]
        public static void StartRecording()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[DesalEra] session: enter Play Mode before recording");
                RefreshRecButton();
                return;
            }

            try
            {
                string id = FindRecorder().StartRecording();
                Debug.Log("[DesalEra] session recording started: " + id);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[DesalEra] session start failed: " + ex.Message);
            }

            RefreshRecButton();
        }

        [MenuItem(MenuRoot + "Stop Recording And Save %#e", priority = 1)]
        public static void StopRecording()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[DesalEra] session: not in Play Mode");
                RefreshRecButton();
                return;
            }

            var rec = Object.FindObjectOfType<PlaySessionRecorder>();
            if (rec == null || !rec.IsRecording)
            {
                Debug.LogWarning("[DesalEra] session: no active recording");
                RefreshRecButton();
                return;
            }

            string path = rec.StopRecording();
            Debug.Log("[DesalEra] session saved: " + path);
            RefreshRecButton();
        }

        [MenuItem(MenuRoot + "Toggle Recording", priority = 2)]
        public static void ToggleRecording()
        {
            var rec = Object.FindObjectOfType<PlaySessionRecorder>();
            if (rec != null && rec.IsRecording) StopRecording();
            else StartRecording();
        }

        [MenuItem(MenuRoot + "Status", priority = 3)]
        public static void Status()
        {
            var rec = Object.FindObjectOfType<PlaySessionRecorder>();
            string msg = rec != null ? rec.Status() : "no recorder (enter Play Mode)";
            Debug.Log("[DesalEra] session status: " + msg);
        }

        [MenuItem(MenuRoot + "Open Recordings Folder", priority = 20)]
        public static void OpenFolder()
        {
            Directory.CreateDirectory(PlaySession.DirectoryPath);
            EditorUtility.RevealInFinder(PlaySession.DirectoryPath);
        }

        private static void TryInjectToolbar()
        {
            if (_injected && _recButton != null && _recButton.panel != null) return;

            VisualElement root = GetToolbarRoot();
            if (root == null) return;

            VisualElement playZone = root.Q("ToolbarZonePlayMode");
            if (playZone == null) return;

            // Domain reload can leave a stale child; replace it.
            VisualElement existing = playZone.Q(ToolbarElementName);
            existing?.RemoveFromHierarchy();

            var row = new VisualElement
            {
                name = ToolbarElementName,
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginLeft = 6
                }
            };

            _recButton = new ToolbarButton(ToggleRecording)
            {
                text = "Rec",
                tooltip = "Record play session for unity-cli replay (DesalEra)"
            };
            _recButton.style.minWidth = 44;
            row.Add(_recButton);
            playZone.Add(row);

            _injected = true;
            RefreshRecButton();
            EditorApplication.update -= TryInjectToolbar;
        }

        private static void RefreshRecButton()
        {
            if (_recButton == null) return;

            bool playing = EditorApplication.isPlaying;
            bool recording = false;
            if (playing)
            {
                var rec = Object.FindObjectOfType<PlaySessionRecorder>();
                recording = rec != null && rec.IsRecording;
            }

            _recButton.SetEnabled(playing);
            _recButton.text = recording ? "Stop" : "Rec";
            _recButton.tooltip = recording
                ? "Stop recording and save SessionRecordings/*.json"
                : "Start recording play session (requires Play Mode)";

            // Quiet visual cue while live.
            _recButton.style.backgroundColor = recording
                ? new Color(0.75f, 0.15f, 0.15f, 0.85f)
                : StyleKeyword.Null;
            _recButton.style.color = recording ? Color.white : StyleKeyword.Null;
        }

        private static VisualElement GetToolbarRoot()
        {
            var toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
            if (toolbarType == null) return null;

            object toolbar = null;
            PropertyInfo getProp = toolbarType.GetProperty("get", BindingFlags.Public | BindingFlags.Static);
            if (getProp != null) toolbar = getProp.GetValue(null);
            else
            {
                FieldInfo getField = toolbarType.GetField("get", BindingFlags.Public | BindingFlags.Static);
                if (getField != null) toolbar = getField.GetValue(null);
            }

            if (toolbar == null) return null;

            FieldInfo rootField = toolbarType.GetField("m_Root", BindingFlags.Instance | BindingFlags.NonPublic);
            return rootField?.GetValue(toolbar) as VisualElement;
        }

        private static PlaySessionRecorder FindRecorder()
        {
            var rec = Object.FindObjectOfType<PlaySessionRecorder>();
            if (rec != null) return rec;
            var host = Object.FindObjectOfType<GameBootstrap>();
            if (host == null) throw new System.InvalidOperationException("No GameBootstrap in play mode.");
            return host.gameObject.AddComponent<PlaySessionRecorder>();
        }
    }
}
