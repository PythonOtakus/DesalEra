using DesalEra.Unity.Session;
using UnityEngine;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// The per-frame half of the HUD: survival bars, situational chips, and the panel
    /// hotkeys.
    ///
    /// Separate from <see cref="UiRoot"/> because wiring and per-frame work want to change
    /// for different reasons. The wiring is written once and never touched again; this part
    /// is where every new readout goes, and keeping them apart means adding a stat does not
    /// mean re-reading how the interface is assembled.
    ///
    /// The vitals are pushed every frame rather than diffed. Four bars and three chips cost
    /// nothing, and a dirty-flag scheme would be a second source of truth about when the
    /// world changed.
    /// </summary>
    public sealed class HudPresenter : MonoBehaviour
    {
        private UiRoot _ui;
        private GameBootstrap _world;
        private PlayerController _player;
        private bool _initialised;

        public void Initialise(UiRoot ui, GameBootstrap world, PlayerController player)
        {
            _ui = ui;
            _world = world;
            _player = player;
            _ui.MiniMap?.Bind(world, player);
            _initialised = true;
        }

        private void Update()
        {
            if (!_initialised || _ui == null || _world == null) return;

            HandleHotkeys();
            _ui.Vitals.Refresh(_world.Raft.Survival, _world.Raft.Inventory);
            if (_ui.ResourceStrip != null)
            {
                _ui.ResourceStrip.SetSelected(_player != null ? _player.SelectedPiece : null);
                _ui.ResourceStrip.Refresh(_world.Raft.Inventory);
            }
            _ui.MiniMap?.Refresh();
            RefreshStatus();
            RefreshReticle();
        }

        /// <summary>
        /// Panels are toggled rather than held open, so the mouse is free again the moment
        /// the player closes one. TAB because that is where the pack lives in every game
        /// with one, ESC because it is the reflex.
        /// </summary>
        private void HandleHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
                PlayDriver.Ui(_ui.AnyScreenOpen() ? "none" : "pack");
            if (Input.GetKeyDown(KeyCode.Escape))
                PlayDriver.Ui("none");
        }

        /// <summary>
        /// Open a named panel, or pass null / "none" / "" to close. Used by input and CLI.
        /// </summary>
        public string ShowPanel(string id)
        {
            if (_ui == null) return "no ui";
            if (string.IsNullOrEmpty(id) || id == "none" || id == "close")
            {
                _ui.ShowOnly(null);
                return "closed";
            }

            _ui.ShowOnly(id);
            InventoryView pack = _ui.GetScreen<InventoryView>("pack");
            if (pack != null) pack.Refresh();
            if (id == "materials" && _player != null && _world != null)
            {
                MaterialPickerView picker = _ui.GetScreen<MaterialPickerView>("materials");
                if (picker != null && _player.SelectedPiece != null)
                    picker.Open(_player.SelectedPiece, _world.Raft.Inventory);
            }
            return "open " + id;
        }

        private void RefreshStatus()
        {
            bool sheltered = _world.PlayerSheltered;
            _ui.Status.Set("sheltered", sheltered);

            bool storm = _world.IsStormActive;
            _ui.Status.Set("storm", storm,
                           storm ? $"风暴 {_world.Raft.WindLoadKnPerM:0} kN/m · {(sheltered ? "已遮蔽" : "暴露中")}"
                                 : "平静");
            _ui.Status.Tone("storm", storm && !sheltered ? UiTheme.Bad : UiTheme.Warn);

            RefreshBuildContext();
        }

        /// <summary>
        /// The dock header says where the next piece goes: storey and facing. Legality
        /// lives on the reticle so the player need not look at the dock to place.
        /// </summary>
        private void RefreshBuildContext()
        {
            if (_player == null || _ui.BuildBar == null) return;

            string where = $"{UiCopy.Piece(_player.SelectedPiece?.Name)} · 第 {_player.BuildLevel} 层 · 朝{UiCopy.Facing(PlayerController.FacingName(_player.BuildFacing))}";
            if (_player.IsInWater)
            {
                _ui.BuildBar.SetContext(where + " · 回到甲板才能建造", UiTheme.TextMuted);
                return;
            }
            _ui.BuildBar.SetContext(where, UiTheme.Accent);
        }

        private void RefreshReticle()
        {
            if (_ui.Reticle == null || _player == null)
            {
                _ui.Reticle?.Hide();
                return;
            }

            if (_player.IsInWater || _player.PreviewPlan == null || _ui.AnyScreenOpen())
            {
                _ui.Reticle.Hide();
                return;
            }

            string problem = _player.PreviewProblem;
            string shortReason = problem == null ? null : UiCopy.Notice(problem).TrimEnd('。');
            if (shortReason != null && shortReason.Length > 18)
                shortReason = shortReason.Substring(0, 18) + "…";
            _ui.Reticle.Show(problem == null, shortReason);
        }
    }
}