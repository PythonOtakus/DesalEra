#if UNITY_EDITOR
using System.Text;
using UnityEngine;

namespace DesalEra.Unity.Session
{
    /// <summary>
    /// Editor-only left-centre readout of the same fields <see cref="PlaySessionRecorder"/>
    /// writes into each <see cref="PlaySnapshot"/>, so what you see live lines up with
    /// the recording JSON. F3 toggles it. Compiled out of player builds.
    /// </summary>
    public sealed class PlayStateOverlay : MonoBehaviour
    {
        [SerializeField] private float refreshInterval = 0.1f;
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;
        [SerializeField] private bool visible = true;
        private readonly StringBuilder _text = new StringBuilder(512);
        private float _nextRefresh;
        private GUIStyle _style;
        private GUIStyle _box;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;
            if (!visible || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            Rebuild();
        }

        private void Rebuild()
        {
            var rec = PlaySessionRecorder.Instance;
            bool recording = rec != null && rec.IsRecording;
            var s = PlayDriver.Capture(recording ? rec.Elapsed() : -1f);

            _text.Clear();
            if (recording)
                _text.AppendLine($"<color=#ff5555>● REC</color> {rec.CurrentSessionId}  a={rec.ActionCount} s={rec.SnapshotCount}");
            else if (rec != null && rec.IsReplaying)
                _text.AppendLine("<color=#55aaff>▶ REPLAY</color>");
            else
                _text.AppendLine("<color=#999999>not recording</color>");

            _text.AppendLine($"t        {s.t:F2}");
            _text.AppendLine($"pos      {s.x:F2}, {s.y:F2}, {s.z:F2}");
            _text.AppendLine($"mode     {s.mode}");
            _text.AppendLine($"anim     {s.anim}");
            _text.AppendLine($"facing   {s.facingX:F2}, {s.facingZ:F2}");
            _text.AppendLine($"soleGap  {s.soleGap:F3}  surface {s.surfaceY:F2}");
            _text.AppendLine($"cam      yaw {s.yaw:F1}  pitch {s.pitch:F1}  dist {s.distance:F1}");
            _text.AppendLine($"selected {s.selected}  buildLevel {s.buildLevel}  buildFacing {s.buildFacing}");
            _text.AppendLine($"storm    {s.storm}  sheltered {s.sheltered}");
            _text.AppendLine($"vitals   food {s.food:F1}  water {s.water:F1}");
            _text.AppendLine($"         health {s.health:F1}  stamina {s.stamina:F1}");
            _text.AppendLine($"items    plank {s.plank}  scrap {s.scrap}  metal {s.metal}");
            _text.AppendLine($"         food {s.foodQty}  water {s.waterQty}");
            _text.AppendLine($"members  {s.members}");
            if (!string.IsNullOrEmpty(s.status)) _text.Append($"status   {s.status}");
        }

        private void OnGUI()
        {
            if (!visible || _text.Length == 0) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New" }, 13),
                    fontSize = 13,
                    richText = true,
                    wordWrap = false,
                };
                _style.normal.textColor = Color.white;
                _box = new GUIStyle(GUI.skin.box);
                _box.normal.background = Texture2D.whiteTexture;
            }

            var content = new GUIContent(_text.ToString());
            Vector2 size = _style.CalcSize(content);
            const float pad = 8f, margin = 10f;
            float h = size.y + pad * 2f;
            var rect = new Rect(margin, (Screen.height - h) * 0.5f, size.x + pad * 2f, h);

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Box(rect, GUIContent.none, _box);
            GUI.color = prev;
            GUI.Label(new Rect(rect.x + pad, rect.y + pad, size.x, size.y), content, _style);
        }
    }
}
#endif
