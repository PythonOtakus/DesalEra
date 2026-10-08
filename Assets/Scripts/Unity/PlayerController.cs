using System.Collections.Generic;
using DesalEra.Game;
using DesalEra.Unity.Session;
using UnityEngine;

namespace DesalEra.Unity
{
    /// <summary>Where the survivor is standing relative to the raft.</summary>
    public enum LocomotionMode
    {
        /// <summary>On a deck the raft can stand on.</summary>
        OnDeck,

        /// <summary>In the water, swimming to salvage and back.</summary>
        InWater
    }

    /// <summary>
    /// Player movement, gathering, building and dismantling.
    ///
    /// The survivor walks the deck and swims in the water. Swimming is not decoration:
    /// salvage drifts 12 to 34 m out while the opening raft is 6 m across, so a survivor
    /// who could not leave the deck could only ever reach the inner ring and eight of the
    /// fourteen pickups were mathematically out of reach. Diving for drifted salvage is
    /// also part of the design this project is copying -- see docs/research.md 1.1 on
    /// scavenging the sea and exploring the sunken city.
    ///
    /// Which surface the survivor is on is asked of the raft, not decided here, so
    /// building more deck genuinely opens up more ground to walk on.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerController : MonoBehaviour
    {
[Header("Movement")]
        [SerializeField] private float moveSpeed = 5.5f;
        [SerializeField] private float sprintMultiplier = 1.7f;

        [Header("Acceleration")]
        [Tooltip("Metres per second squared. Instant start and stop is the clearest tell of a fixed-camera builder.")]
        [SerializeField] private float acceleration = 34f;
        [SerializeField] private float deceleration = 26f;

        [Header("Swimming")]
        [Tooltip("Extra stamina cost per second while swimming, on top of moving.")]
        [SerializeField] private float swimCostPerSecond = 1.1f;

        [Tooltip("How far out to sea the survivor may swim, in metres.")]
        [SerializeField] private float swimRadius = 90f;

        [Tooltip("Max turn rate while swimming (degrees/sec).")]
        [SerializeField] private float swimTurnDegreesPerSecond = 110f;

        [Tooltip("SwimForward playback speed at full stick.")]
        [SerializeField] private float swimPlaybackMax = 1.15f;

        [Tooltip("SwimForward playback speed at light stick.")]
        [SerializeField] private float swimPlaybackMin = 0.65f;

        [Header("Costs")]
        [Tooltip("Survival units spent per second of continuous work, before recovery.")]
        [SerializeField] private float workCostPerSecond = 1.6f;

        // How far below the local swell the player root sits while swimming, measured
        // against SeaWave rather than mean water so the survivor rides the drawn swell.
        // The clips are authored with the waterline at different heights: treading water
        // is upright with the head at 1.3 m, the stroke lies face down with the head at
        // the clip origin. One depth for both either drowns the swimmer or lifts the
        // treading body out to the waist.
        private const float SwimIdleRootBelowSurface = 0.85f;
        private const float SwimStrokeRootBelowSurface = 0.05f;
        private const float DefaultRootAboveSole = 0.05f;

        // How far past the deck outline the survivor counts as aboard: once aboard, and
        // to climb back on from the water.
        private const float StayAboardMarginM = 0.6f;
        private const float BoardMarginM = 0.3f;

        // Half of MeshFactory Plank depth (thickness * 0.38). Joints sit on the plank
        // centreline; soles rest on the top face.
        private static readonly float DeckPlankHalfDepthM =
            Mathf.Clamp(Mathf.Sqrt(0.09f) * 1.15f, 0.14f, 0.7f) * 0.38f * 0.5f;

        private GameBootstrap _world;
        private ThirdPersonCamera _camera;

        private BuildPiece _selectedPiece;
        private int _selectedIndex;
        private LocomotionMode _mode = LocomotionMode.OnDeck;
        private Vector3 _velocity;

        // When set, movement reads this instead of the keyboard — used by unity-cli
        // and session replay so scripted input does not fight the player.
        private bool _driveMove;
        private float _driveH;
        private float _driveV;
        private bool _driveSprint;

        // Last non-zero swim wish. Travel follows this, not the body facing, so a slow
        // turn cannot push the survivor sideways — and releasing the stick must not keep
        // applying stroke displacement along a lagging facing vector.
        private Vector3 _swimHeading = Vector3.forward;

private static readonly BuildPiece[] Palette =
        {
            BuildPiece.Deck(),
            BuildPiece.Column(),
            BuildPiece.Pontoon(),
            BuildPiece.Brace(),
            BuildPiece.Still(),
            BuildPiece.Roof(),
            BuildPiece.Wall(),
            BuildPiece.Stairs()
        };

        private int _buildFacing;
        private int _buildLevel;

        /// <summary>0 = east (+X), 1 = north (+Z), 2 = west (-X), 3 = south (-Z).</summary>
        public int BuildFacing => _buildFacing;

        /// <summary>Storey the next placement targets (0 = deck level).</summary>
        public int BuildLevel => _buildLevel;

        /// <summary>True while a roof is overhead. Recomputed every frame.</summary>
        public bool IsSheltered { get; private set; }

        /// <summary>
        /// The placeable pieces, for the build bar. Exposed rather than re-declared by the
        /// UI: two lists of pieces would drift, and the bar's affordability colouring
        /// would then be describing something the player cannot build.
        /// </summary>
        public static IReadOnlyList<BuildPiece> Pieces => Palette;

        public BuildPiece SelectedPiece => _selectedPiece;

        /// <summary>Deck or water. The HUD reads this to tell the player where they are.</summary>
        public LocomotionMode Mode => _mode;

        public bool IsInWater => _mode == LocomotionMode.InWater;

        /// <summary>The visible character, if one loaded.</summary>
        public PlayerAvatar Avatar { get; set; }

        /// <summary>The survivor's animation state, if clips were found.</summary>
        public PlayerAnimator Animator { get; set; }

        /// <summary>Index into the build palette, for the HUD to highlight.</summary>
        public int SelectedIndex => _selectedIndex;

        /// <summary>Current planar velocity, for diagnostics and session snapshots.</summary>
        public Vector3 PlanarVelocity => _velocity;

        private string _statusLine = string.Empty;

        /// <summary>
        /// The one-line feedback message. Raising an event from the setter means the HUD
        /// does not have to poll it, and none of the eleven places that write a message
        /// have to remember to also notify anyone.
        /// </summary>
        public string StatusLine
        {
            get => _statusLine;
            set
            {
                _statusLine = value;
                StatusChanged?.Invoke(value);
            }
        }

        /// <summary>Raised whenever <see cref="StatusLine"/> changes.</summary>
        public event System.Action<string> StatusChanged;

public void Initialise(GameBootstrap world, ThirdPersonCamera camera)
        {
            _world = world;
            _camera = camera;
            _selectedPiece = Palette[0];
            _world.StructureNotice += message => StatusLine = message;

            // Stand on a perimeter beam, not the empty cell centre — the starter raft is
            // a frame, and spawning at (0,0) left the survivor hanging over open air.
            Vector3 spawn = RaftState.WorldOf(Vector2Int.zero);
            spawn.z = -RaftState.CellSize;
            spawn.y = SurfaceHeight();
            transform.localPosition = spawn;

            foreach (Pickup pickup in FindObjectsOfType<Pickup>()) pickup.RegisterPlayer(transform);
        }

private void Update()
        {
            if (_world == null) return;

            HandleMovement();
            HandleBuildInput();
            ApplyWorkCost();

            // Evaluated every frame rather than as part of moving, because the deck can
            // stop existing under a survivor who is standing still: dismantling the piece
            // they are on leaves them floating, and a check that only ran while walking
            // would leave them on deck forever.
            UpdateMode(RaftLocal(transform.position));

            Vector3 feet = RaftLocal(transform.position);
            feet.y = _surfaceLocalY;
            IsSheltered = !IsInWater && _world.Raft.IsSheltered(feet);
            UpdatePreview();
            _world.PlayerSheltered = IsSheltered;

            // The raft has not moved yet this frame (RaftMotion runs in LateUpdate), so
            // this is where the survivor stands in the frame they were last planted in.
            _raftLocal = RaftLocal(transform.position);
        }

        // Where the survivor stands in raft coordinates, carried across the raft's
        // LateUpdate so they ride the deck rather than staying put in the world.
        private Vector3 _raftLocal;

        private Transform RaftFrame =>
            _world != null && _world.RaftMotion != null ? _world.RaftMotion.transform : null;

        /// <summary>
        /// Raft-local position of the survivor's footprint: where the vertical through
        /// them meets the surface they stand on, so it does not change with their height.
        /// RaftState works in these coordinates.
        /// </summary>
        private Vector3 RaftLocal(Vector3 world)
        {
            if (_world == null || _world.RaftMotion == null) return world;
            return _world.RaftMotion.LocalOnPlane(world, _surfaceLocalY);
        }

        private void LateUpdate()
        {
            // RaftMotion writes heave and tilt in LateUpdate. Re-plant after that so the
            // soles cannot lag a frame above the moving deck.
            if (_world == null || Avatar == null) return;
            Vector3 p = transform.position;

            // On deck the survivor rides the raft: tilting swings every deck point
            // sideways by up to 1.5 m * sin 6°, and holding world XZ fixed against that
            // read as sliding across the boards. In water the sea is the reference.
            Transform frame = RaftFrame;
            if (_mode == LocomotionMode.OnDeck && frame != null)
                p = frame.TransformPoint(_raftLocal);

            transform.position = p;
            p.y = SurfaceHeight();
            transform.position = p;
        }

        /// <summary>Drive movement from CLI / replay. Pass zeros to release control.</summary>
        public void SetMoveIntent(float h, float v, bool sprint)
        {
            _driveH = Mathf.Clamp(h, -1f, 1f);
            _driveV = Mathf.Clamp(v, -1f, 1f);
            _driveSprint = sprint;
            _driveMove = Mathf.Abs(_driveH) > 0.001f || Mathf.Abs(_driveV) > 0.001f || sprint;
        }

        public string SelectPieceAt(int index)
        {
            if (index < 0 || index >= Palette.Length) return "invalid piece index";
            SelectPiece(index);
            return StatusLine;
        }

        public string BuildAt(Vector2Int cell) => BuildAt(cell, _buildLevel, _buildFacing);

        public string BuildAt(Vector2Int cell, int level, int facing)
        {
            TryBuildAt(cell, level, facing);
            return StatusLine;
        }

        public string DismantleAt(Vector2Int cell) => DismantleAt(cell, _buildLevel);

        public string DismantleAt(Vector2Int cell, int level)
        {
            TryDismantleAt(cell, level);
            return StatusLine;
        }

        public string SetBuildFacing(int facing)
        {
            _buildFacing = RaftState.NormaliseFacing(facing);
            StatusLine = $"Facing {FacingName(_buildFacing)}.";
            return StatusLine;
        }

        public string SetBuildLevel(int level)
        {
            _buildLevel = Mathf.Clamp(level, 0, RaftState.MaxLevel);
            StatusLine = $"Build level {_buildLevel}.";
            return StatusLine;
        }

        public static string FacingName(int facing)
        {
            switch (RaftState.NormaliseFacing(facing))
            {
                case 1: return "north";
                case 2: return "west";
                case 3: return "south";
                default: return "east";
            }
        }

        /// <summary>Level a placement actually uses: a roof always goes at least one storey up.</summary>
        private int EffectiveLevel(BuildPiece piece, int level) =>
            piece != null && piece.Name == "Roof" ? Mathf.Max(1, level) : level;

        public string EatRationNow()
        {
            EatRation();
            return StatusLine;
        }

        /// <summary>
        /// Moves the survivor. The requested height picks the storey: the survivor lands
        /// on the highest surface within a step of it, so (x, 4.8, z) stands on a first
        /// floor and (x, 1.5, z) on the deck beneath it.
        /// </summary>
        public string TeleportTo(Vector3 world)
        {
            transform.position = world;
            float heave = _world != null && _world.RaftMotion != null ? _world.RaftMotion.HeaveY : 0f;
            float hint = Mathf.Max(DeckSurfaceLocalY, world.y - heave);
            UpdateMode(RaftLocal(transform.position));
            _surfaceLocalY = hint;
            UpdateMode(RaftLocal(transform.position));
            Vector3 p = transform.position;
            p.y = SurfaceHeight();
            transform.position = p;
            return $"teleport ({p.x:F2},{p.y:F2},{p.z:F2})";
        }

private void HandleMovement()
        {
            // Camera-relative so the controls follow where the player is looking. The
            // orbit camera supplies a flattened forward, so looking up does not tilt the
            // movement plane into the ground.
            float h = _driveMove ? _driveH : Input.GetAxisRaw("Horizontal");
            float v = _driveMove ? _driveV : Input.GetAxisRaw("Vertical");

            // Keyboard takes over again as soon as the player steers.
            if (_driveMove && (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f
                               || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f))
            {
                _driveMove = false;
                h = Input.GetAxisRaw("Horizontal");
                v = Input.GetAxisRaw("Vertical");
            }

            Vector3 forward = _camera != null ? _camera.FlatForward : Vector3.forward;
            Vector3 right = _camera != null ? _camera.FlatRight : Vector3.right;

            Vector3 wish = (forward * v + right * h);
            bool hasInput = wish.sqrMagnitude > 0.0001f;
            if (hasInput) wish.Normalize();

            if (_mode == LocomotionMode.InWater)
            {
                HandleSwimMovement(wish, hasInput);
                return;
            }

            // Sprint is a land skill. Letting the survivor sprint across open water would
            // make swimming a strictly worse deck and there would be no reason to build.
            bool sprinting = _driveMove ? _driveSprint : Input.GetKey(KeyCode.LeftShift);
            float targetSpeed = moveSpeed * (sprinting ? sprintMultiplier : 1f);

            Vector3 targetVelocity = hasInput ? wish * targetSpeed : Vector3.zero;

            // Ramp towards the target instead of snapping to it. Constant-velocity
            // movement stops the instant a key is released, which is exactly the feel of
            // a cursor-driven builder rather than a character.
            float rate = hasInput ? acceleration : deceleration;
            _velocity = Vector3.MoveTowards(_velocity, targetVelocity, rate * Time.deltaTime);

            float actualSpeed = _velocity.magnitude;

            if (Animator != null) ApplyLocomotionAnimation(actualSpeed);

            Vector3 next = transform.localPosition + _velocity * Time.deltaTime;
            next.y = SurfaceHeight();
            ClampToSwimRadius(ref next);

            // Face the wish heading while steering; fall back to velocity when coasting
            // so the body does not keep pointing at a released stick.
            if (Avatar != null && actualSpeed > 0.15f)
            {
                Vector3 face = hasInput ? wish : _velocity;
                Avatar.FaceTowards(face);
            }

            transform.localPosition = next;
        }

        /// <summary>
        /// Swim travel is driven by SwimForward clip progress (synthetic root motion).
        /// The baked Meshy clips are in-place — shoving the transform with land-style
        /// acceleration made strokes skate and look stiff.
        /// </summary>
        private void HandleSwimMovement(Vector3 wish, bool hasInput)
        {
            if (Avatar != null) Avatar.AnchorChestToRoot = true;

            if (hasInput)
            {
                _swimHeading = wish;
                if (Avatar != null)
                    Avatar.FaceTowards(wish, swimTurnDegreesPerSecond);

                if (Animator != null)
                {
                    Animator.Play("SwimForward");
                    float stick = Mathf.Clamp01(new Vector2(
                        _driveMove ? _driveH : Input.GetAxisRaw("Horizontal"),
                        _driveMove ? _driveV : Input.GetAxisRaw("Vertical")).magnitude);
                    if (stick < 0.01f) stick = 1f;
                    Animator.SetPlaybackSpeed(Mathf.Lerp(swimPlaybackMin, swimPlaybackMax, stick));
                }
            }
            else if (Animator != null)
            {
                // Cross-faded: the stroke is face down and treading is upright, so a snap
                // flips the body vertical in one frame. The slide this once caused is
                // held off by AnchorChestToRoot and by travel stopping with the input.
                Animator.Play("SwimIdle");
                Animator.SetPlaybackSpeed(1f);
            }

            Vector3 animDelta = Vector3.zero;
            if (hasInput && Animator != null)
            {
                animDelta = Animator.ConsumePlanarAnimDelta(_swimHeading);
                // Hard cap so a clip-time wrap can never shove a full metre in one frame.
                float maxStep = 3.2f * Time.deltaTime;
                float mag = animDelta.magnitude;
                if (mag > maxStep) animDelta *= maxStep / mag;
            }

            Vector3 next = transform.position + animDelta;
            next.y = SurfaceHeight();
            ClampToSwimRadius(ref next);

            Vector3 planar = next - transform.position;
            planar.y = 0f;
            _velocity = Time.deltaTime > 1e-6f ? planar / Time.deltaTime : Vector3.zero;

            transform.position = next;
        }

        private void ClampToSwimRadius(ref Vector3 next)
        {
            Vector2 flat = new Vector2(next.x, next.z);
            if (flat.magnitude <= swimRadius) return;

            flat = flat.normalized * swimRadius;
            next.x = flat.x;
            next.z = flat.y;
            _velocity = Vector3.zero;
        }

        /// <summary>
        /// The height of the player root for the current surface. The root sits
        /// <see cref="PlayerAvatar.RootAboveSoleM"/> above the soles so the mesh
        /// plants on the deck instead of hovering by a hard-coded eye offset.
        /// </summary>
        private float SurfaceHeight()
        {
            float sole = Avatar != null ? Avatar.RootAboveSoleM : DefaultRootAboveSole;

            if (_mode == LocomotionMode.InWater)
            {
                float stroke = Animator != null ? Animator.WeightOf("SwimForward") : 0f;
                return SeaWave.HeightAt(transform.position)
                     - Mathf.Lerp(SwimIdleRootBelowSurface, SwimStrokeRootBelowSurface, stroke);
            }

            // Ride the moving deck plane so feet stay planted while the raft heaves.
            // DeckPlankHalfDepthM lifts soles from the joint centreline onto the plank top.
            if (_world != null && _world.RaftMotion != null)
            {
                Vector3 surface = _world.RaftMotion.SurfacePoint(transform.position, _surfaceLocalY);
                return surface.y + sole;
            }

            return _surfaceLocalY + sole;
        }

        /// <summary>Raft-local height of the deck's walking surface: the top of the beams.</summary>
        public static float DeckSurfaceLocalY => RaftState.BaseDeckY + DeckPlankHalfDepthM;

        /// <summary>
        /// Raft-local height of whatever the survivor is standing on: the deck, a floor
        /// slab or a stair tread. Tracked rather than recomputed from scratch, because
        /// which surface is underfoot depends on where the survivor came from -- under a
        /// floor or on top of it.
        /// </summary>
        private float _surfaceLocalY = DeckSurfaceLocalY;

        /// <summary>Raft-local height of the surface underfoot, for diagnostics and tests.</summary>
        public float SurfaceLocalY => _surfaceLocalY;

        /// <summary>Picks the surface underfoot. Returns false when there is none (open water).</summary>
        private bool ResolveSurface(Vector3 position, out float surfaceY)
        {
            // Climbing out needs a firmer footing than staying aboard. With one edge the
            // swaying raft carries that edge back and forth under a survivor standing on
            // it, and they drop in and climb out with every swell.
            float margin = _mode == LocomotionMode.OnDeck ? StayAboardMarginM : BoardMarginM;
            bool overDeck = _world.Raft.IsOverDeck(position, margin);
            float reach = (_mode == LocomotionMode.OnDeck ? _surfaceLocalY : DeckSurfaceLocalY) + RaftState.StepUpM;

            surfaceY = overDeck ? DeckSurfaceLocalY : float.NegativeInfinity;
            if (_world.Raft.TryGetRaisedSurface(position, reach, out float raised) && raised > surfaceY)
                surfaceY = raised;
            return overDeck || !float.IsNegativeInfinity(surfaceY);
        }

        /// <summary>
        /// Switches between deck and water based on whether the survivor is over anything
        /// the raft can stand on.
        ///
        /// This only decides the mode; the height each mode implies is applied by the
        /// movement code, so the two cannot disagree about where the survivor is.
        /// </summary>
        private void UpdateMode(Vector3 position)
        {
            // The raft is built in GameBootstrap.Awake. Ordering between that and this
            // frame's Update is not something to depend on, and throwing here would repeat
            // every frame rather than once.
            if (_world == null || _world.Raft == null) return;

            bool overDeck = ResolveSurface(position, out float surfaceY);
            _surfaceLocalY = overDeck ? surfaceY : DeckSurfaceLocalY;

            if (_mode == LocomotionMode.OnDeck && !overDeck)
            {
                _mode = LocomotionMode.InWater;
                StatusLine = "In the water. Swim out to salvage, step back onto the deck to build.";
                // Snap — cross-fading Run into Swim reads as "still running on the water".
                if (Animator != null) Animator.Play("SwimIdle", instant: true);
            }
            else if (_mode == LocomotionMode.InWater && overDeck)
            {
                _mode = LocomotionMode.OnDeck;
                StatusLine = "Back on deck.";
                if (Avatar != null) Avatar.AnchorChestToRoot = false;
                if (Animator != null) Animator.Play("Idle", instant: true);
            }
        }

        /// <summary>
        /// Picks the clip for the current surface. On deck the speed thresholds decide;
        /// in water there is only swim forward and swim idle, so the speed rules do not
        /// apply.
        /// </summary>
        private void ApplyLocomotionAnimation(float speed)
        {
            if (_mode == LocomotionMode.InWater)
            {
                // Slightly higher threshold than land idle so tiny velocity noise after
                // leaving the deck does not thrash SwimIdle / SwimForward.
                Animator.Play(speed > 0.2f ? "SwimForward" : "SwimIdle");
                return;
            }

            Animator.UpdateForSpeed(speed);
        }

private void HandleBuildInput()
        {
            // A click on a UI panel is not a click on the world. Without this the build bar
            // and the pack sit on top of the deck, and picking a piece would also place it.
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // Number keys rather than the wheel. The wheel is worth more as camera zoom,
            // and one meaning per input beats a control that does two unrelated jobs.
            for (int i = 0; i < Palette.Length; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    PlayDriver.Select(i);
            }

            if (Input.GetKeyDown(KeyCode.R)) PlayDriver.Rotate(_buildFacing + 1);
            if (Input.GetKeyDown(KeyCode.Q)) PlayDriver.Level((_buildLevel + 1) % (RaftState.MaxLevel + 1));

            if (Input.GetMouseButtonDown(0) && _selectedPiece != null)
            {
                int level = EffectiveLevel(_selectedPiece, _buildLevel);
                Vector2Int cell = CellUnderMouse(level, _selectedPiece.Name == "Roof");
                PlayDriver.Build(cell.x, cell.y, _selectedIndex, level, _buildFacing);
            }

            // Dismantle shares the right button with look-drag, so the camera decides
            // whether the press was a click.
            if (_camera != null && _camera.ConsumeLookClick())
            {
                Vector2Int cell = CellUnderMouse(_buildLevel, squareCorner: false);
                PlayDriver.Dismantle(cell.x, cell.y, _buildLevel);
            }

            if (Input.GetKeyDown(KeyCode.E)) PlayDriver.Eat();
        }

        /// <summary>Where the selected piece would go under the mouse, or null when not building.</summary>
        public PlacementPlan PreviewPlan { get; private set; }

        /// <summary>Why the previewed placement would be refused, or null if it would succeed.</summary>
        public string PreviewProblem { get; private set; }

        /// <summary>When set, the preview targets this grid point instead of the mouse. For CLI screenshots.</summary>
        public Vector2Int? PreviewCellOverride { get; set; }

        private void UpdatePreview()
        {
            PreviewPlan = null;
            PreviewProblem = null;
            if (IsInWater || _selectedPiece == null) return;

            int level = EffectiveLevel(_selectedPiece, _buildLevel);
            Vector2Int cell;
            if (PreviewCellOverride.HasValue)
            {
                cell = PreviewCellOverride.Value;
            }
            else
            {
                if (Camera.main == null) return;
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

                Vector3 mouse = Input.mousePosition;
                if (mouse.x < 0f || mouse.y < 0f || mouse.x > Screen.width || mouse.y > Screen.height) return;
                cell = CellUnderMouse(level, _selectedPiece.Name == "Roof");
            }

            PreviewProblem = _world.Raft.CanPlace(_selectedPiece, cell, level, _buildFacing, out PlacementPlan plan);
            PreviewPlan = plan;
        }

        private void SelectPiece(int index)
        {
            if (index < 0 || index >= Palette.Length) return;
            _selectedIndex = index;
            _selectedPiece = Palette[index];
            StatusLine = $"Selected {_selectedPiece.Name}.";
        }

        private void TryBuildAt(Vector2Int cell, int level, int facing)
        {
            // Placing a deck thirty metres away while floating next to it is not building,
            // it is remote construction. Requiring the survivor to be on deck also keeps
            // the build grid meaningful, since the cell under the mouse is a raft cell
            // only when you are standing on the raft.
            if (IsInWater)
            {
                StatusLine = "Cannot build from the water. Get back on the deck.";
                return;
            }

            level = EffectiveLevel(_selectedPiece, level);
            string error = _world.Raft.TryPlace(_selectedPiece, cell, level, facing);
            StatusLine = error ?? $"Built {_selectedPiece.Name} at {cell.x},{cell.y}.";
            _world.Reanalyse();
        }

        private void TryDismantleAt(Vector2Int cell, int level)
        {
            if (IsInWater)
            {
                StatusLine = "Cannot dismantle from the water. Get back on the deck.";
                return;
            }

            string error = _world.Raft.TryDismantle(cell, level);
            int fallen = error == null ? _world.Raft.LastCollapsed : 0;
            StatusLine = error ?? (fallen > 0
                ? $"Dismantled at {cell.x},{cell.y}. {fallen} pieces collapsed."
                : $"Dismantled at {cell.x},{cell.y}.");
            _world.Reanalyse();
        }

        private void EatRation()
        {
            if (_world.Raft.Inventory.Get(ResourceKind.Food) <= 0)
            {
                StatusLine = "No rations aboard.";
                return;
            }
            _world.Raft.Inventory.Add(ResourceKind.Food, -1);
            _world.Raft.Survival.ConsumeRation();
            StatusLine = "Ate a ration.";
        }

        /// <summary>
        /// Which grid point the mouse is over, by intersecting the mouse ray with the
        /// plane of the target storey. Cheaper and more reliable than raycasting against
        /// every member, and it keeps placement aligned to the build grid. A roof keys
        /// off its square, so it takes the square's south-west corner instead of the
        /// nearest point.
        /// </summary>
        private Vector2Int CellUnderMouse(int level, bool squareCorner)
        {
            Camera camera = Camera.main;
            if (camera == null) return Vector2Int.zero;

            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, RaftState.WorldOf(Vector2Int.zero, level));

            if (!plane.Raycast(ray, out float distance)) return Vector2Int.zero;

            Vector3 point = ray.GetPoint(distance);
            if (squareCorner)
            {
                return new Vector2Int(
                    Mathf.FloorToInt(point.x / RaftState.CellSize),
                    Mathf.FloorToInt(point.z / RaftState.CellSize));
            }
            return new Vector2Int(
                Mathf.RoundToInt(point.x / RaftState.CellSize),
                Mathf.RoundToInt(point.z / RaftState.CellSize));
        }

        /// <summary>
        /// Building and gathering both cost stamina, so the player cannot stand still
        /// and accumulate an infinite base. Swimming costs more on top, because a
        /// survivor who could cross open water for free would never expand the raft.
        /// The cost is paid here rather than inside the model because it is an input
        /// concern, not a rules one.
        /// </summary>
        private void ApplyWorkCost()
        {
            bool moving = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f
                       || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;

            bool workingOnDeck = !IsInWater
                              && (Input.GetMouseButton(0) || Input.GetMouseButton(1) || moving);

            if (workingOnDeck) _world.Raft.Survival.Add(Vital.Stamina, -workCostPerSecond * Time.deltaTime);

            if (IsInWater && moving)
            {
                _world.Raft.Survival.Add(Vital.Stamina, -swimCostPerSecond * Time.deltaTime);
            }
        }
    }
}


