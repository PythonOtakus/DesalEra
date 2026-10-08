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
            _initialised = true;
        }

        private void Update()
        {
            if (!_initialised || _ui == null || _world == null) return;

            HandleHotkeys();
            _ui.Vitals.Refresh(_world.Raft.Survival, _world.Raft.Inventory);
            RefreshStatus();
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
        /// The dock header says where the next piece goes and whether it can: storey,
        /// facing, then the preview's verdict. A refusal is readable before the click.
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
            if (_player.PreviewPlan == null)
            {
                _ui.BuildBar.SetContext(where, UiTheme.Accent);
                return;
            }

            string problem = _player.PreviewProblem;
            _ui.BuildBar.SetContext(problem == null ? where + " · 可放置" : where + " · " + UiCopy.Notice(problem).TrimEnd('。'),
                                    problem == null ? UiTheme.Good : UiTheme.Bad);
        }
    }
}