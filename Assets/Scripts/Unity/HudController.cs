using System.Collections.Generic;
using System.Text;
using CrazyAquarium.Game;
using UnityEngine;

namespace CrazyAquarium.Unity
{
    /// <summary>
    /// On-screen readout. Immediate-mode GUI, built entirely in code, because a Unity
    /// UI needs a scene and a canvas asset to live in and this project has neither by
    /// design.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] private float referenceHeight = 900f;

        private GameBootstrap _world;
        private PlayerController _player;
        private readonly StringBuilder _line = new StringBuilder(256);

        private GUIStyle _label;
        private GUIStyle _big;
        private Texture2D _panel;

        public void Initialise(GameBootstrap world, PlayerController player)
        {
            _world = world;
            _player = player;
        }

        private void OnDestroy()
        {
            if (_panel != null) Destroy(_panel);
        }

        private void EnsureStyles()
        {
            if (_label != null) return;

            _label = new GUIStyle
            {
                fontSize = Mathf.RoundToInt(18f * referenceHeight / Screen.height),
                normal = { textColor = new Color(0.92f, 0.94f, 0.95f) },
                wordWrap = true
            };
            _label.normal.textColor = new Color(0.92f, 0.94f, 0.95f);

            _big = new GUIStyle(_label)
            {
                fontSize = Mathf.RoundToInt(30f * referenceHeight / Screen.height),
                fontStyle = FontStyle.Bold
            };
            _big.normal.textColor = Color.white;

            _panel = new Texture2D(1, 1);
            _panel.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
            _panel.Apply();
        }

        private void OnGUI()
        {
            if (_world == null || _world.Raft == null) return;
            EnsureStyles();

            float scale = referenceHeight / Mathf.Max(Screen.height, 1f);
            float pad = 18f * scale;

            DrawVitals(new Rect(pad, pad, 320f * scale, 150f * scale));
            DrawBuildBar(new Rect(pad, Screen.height - pad - 92f * scale, 520f * scale, 92f * scale));
            DrawStatus(new Rect(pad, Screen.height - pad - 150f * scale, 700f * scale, 52f * scale));
        }

        private void DrawVitals(Rect area)
        {
            SurvivalModel s = _world.Raft.Survival;
            RaftState raft = _world.Raft;

            GUI.Label(area, "", _label);
            var prev = GUI.skin.box;
            GUI.DrawTexture(area, _panel);

            _line.Clear();
            _line.Append($"SURVIVOR   {s.Describe()}");
            _line.Append($"\nSTORES     {raft.Inventory.Describe()}");
            _line.Append($"\nRAFT       {raft.SolveBuoyancy().NetVerticalForceKn,6:F0} kN spare"
                         + (raft.SolveBuoyancy().IsFloating ? "" : "  SINKS"));
            _line.Append($"\nPIECES     {raft.MemberCount}   FAILED {raft.CountFailedPieces()}");
            _line.Append($"\nWEATHER    {(_world.IsStormActive ? "STORM" : "calm")}"
                         + $"   wind {raft.WindLoadKnPerM:F1} kN/m");

            GUI.Label(new Rect(area.x + 10f, area.y + 6f, area.width, area.height), _line.ToString(), _label);
        }

        private void DrawBuildBar(Rect area)
        {
            GUI.DrawTexture(area, _panel);

            _line.Clear();
            _line.Append("BUILD  ");
            BuildPiece[] palette =
            {
                BuildPiece.Deck(), BuildPiece.Column(), BuildPiece.Pontoon(),
                BuildPiece.Brace(), BuildPiece.Still()
            };

            for (int i = 0; i < palette.Length; i++)
            {
                bool affordable = CanAfford(palette[i]);
                string mark = i == _player.SelectedIndex ? "[" : "";
                string end = i == _player.SelectedIndex ? "]" : "";
                _line.Append(mark);
                _line.Append(affordable ? palette[i].Name : palette[i].Name + "*");
                _line.Append(end);
                _line.Append("  ");
            }

            _line.Append($"\nLMB build   RMB dismantle   scroll change piece   SHIFT sprint   E ration");
            _line.Append("   * not affordable");

            GUI.Label(new Rect(area.x + 10f, area.y + 8f, area.width, area.height), _line.ToString(), _label);
        }

        private bool CanAfford(BuildPiece piece)
        {
            foreach (KeyValuePair<ResourceKind, int> entry in piece.Cost)
            {
                if (_world.Raft.Inventory.Get(entry.Key) < entry.Value) return false;
            }
            return true;
        }

        private void DrawStatus(Rect area)
        {
            if (string.IsNullOrEmpty(_player.StatusLine)) return;
            GUI.DrawTexture(area, _panel);
            GUI.Label(new Rect(area.x + 10f, area.y + 6f, area.width, area.height), _player.StatusLine, _big);
        }
    }
}
