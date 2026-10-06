using DesalEra.Game;
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
        [Tooltip("Speed multiplier in the water. Swimming is slower than walking.")]
        [SerializeField] private float swimSpeedMultiplier = 0.5f;

        [Tooltip("Extra stamina cost per second while swimming, on top of moving.")]
        [SerializeField] private float swimCostPerSecond = 1.1f;

        [Tooltip("How far out to sea the survivor may swim, in metres.")]
        [SerializeField] private float swimRadius = 90f;

        [Header("Costs")]
        [Tooltip("Survival units spent per second of continuous work, before recovery.")]
        [SerializeField] private float workCostPerSecond = 1.6f;

        private const float DeckEyeHeight = 0.6f;
        private const float SwimEyeDepth = 0.25f;

        private GameBootstrap _world;
        private ThirdPersonCamera _camera;

        private BuildPiece _selectedPiece;
        private int _selectedIndex;
        private LocomotionMode _mode = LocomotionMode.OnDeck;
        private Vector3 _velocity;

        private static readonly BuildPiece[] Palette =
        {
            BuildPiece.Deck(),
            BuildPiece.Column(),
            BuildPiece.Pontoon(),
            BuildPiece.Brace(),
            BuildPiece.Still()
        };

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

        public string StatusLine { get; private set; } = string.Empty;

public void Initialise(GameBootstrap world, ThirdPersonCamera camera)
        {
            _world = world;
            _camera = camera;
            _selectedPiece = Palette[0];

            Vector3 spawn = RaftState.WorldOf(Vector2Int.zero) + Vector3.up * 0.6f;
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
            UpdateMode(transform.localPosition);
        }

private void HandleMovement()
        {
            // Camera-relative so the controls follow where the player is looking. The
            // orbit camera supplies a flattened forward, so looking up does not tilt the
            // movement plane into the ground.
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 forward = _camera != null ? _camera.FlatForward : Vector3.forward;
            Vector3 right = _camera != null ? _camera.FlatRight : Vector3.right;

            Vector3 wish = (forward * v + right * h);
            bool hasInput = wish.sqrMagnitude > 0.0001f;
            if (hasInput) wish.Normalize();

            bool swimming = _mode == LocomotionMode.InWater;

            // Sprint is a land skill. Letting the survivor sprint across open water would
            // make swimming a strictly worse deck and there would be no reason to build.
            bool sprinting = !swimming && Input.GetKey(KeyCode.LeftShift);
            float targetSpeed = moveSpeed
                              * (sprinting ? sprintMultiplier : 1f)
                              * (swimming ? swimSpeedMultiplier : 1f);

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

            // Confined to the sea. Swimming is unbounded otherwise, and drifting off the
            // water plane is unrecoverable because nothing brings the survivor back.
            Vector2 flat = new Vector2(next.x, next.z);
            if (flat.magnitude > swimRadius)
            {
                flat = flat.normalized * swimRadius;
                next.x = flat.x;
                next.z = flat.y;
                _velocity = Vector3.zero;
            }

            // Turn the model towards travel, but only while actually moving: spinning to
            // face a heading while standing still is a common and distracting tell.
            if (Avatar != null && actualSpeed > 0.15f) Avatar.FaceTowards(_velocity);

            transform.localPosition = next;
        }

/// <summary>
        /// The height the survivor's feet rest at for the current surface. One place, so
        /// the moving and standing paths cannot drift apart.
        /// </summary>
        private float SurfaceHeight()
        {
            return _mode == LocomotionMode.InWater
                ? RaftState.WaterLevelY - SwimEyeDepth
                : RaftState.BaseDeckY + DeckEyeHeight;
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

            bool overDeck = _world.Raft.IsOverDeck(position);

            if (_mode == LocomotionMode.OnDeck && !overDeck)
            {
                _mode = LocomotionMode.InWater;
                StatusLine = "In the water. Swim out to salvage, step back onto the deck to build.";
            }
            else if (_mode == LocomotionMode.InWater && overDeck)
            {
                _mode = LocomotionMode.OnDeck;
                StatusLine = "Back on deck.";
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
                Animator.Play(speed > 0.01f ? "SwimForward" : "SwimIdle");
                return;
            }

            Animator.UpdateForSpeed(speed);
        }

private void HandleBuildInput()
        {
            // Number keys rather than the wheel. The wheel is worth more as camera zoom,
            // and one meaning per input beats a control that does two unrelated jobs.
            for (int i = 0; i < Palette.Length; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SelectPiece(i);
            }

            if (Input.GetMouseButtonDown(0) && _selectedPiece != null) TryBuild();

            // Dismantle shares the right button with look-drag, so the camera decides
            // whether the press was a click.
            if (_camera != null && _camera.ConsumeLookClick()) TryDismantle();

            if (Input.GetKeyDown(KeyCode.E)) EatRation();
        }

        private void SelectPiece(int index)
        {
            if (index < 0 || index >= Palette.Length) return;
            _selectedIndex = index;
            _selectedPiece = Palette[index];
            StatusLine = $"Selected {_selectedPiece.Name}.";
        }

        private void TryBuild()
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

            Vector2Int cell = DeckCellUnderMouse();
            if (_world.Raft.IsCellOccupied(new Vector2Int(0, 0)) && cell == Vector2Int.zero)
            {
                StatusLine = "That is the spawn deck.";
                return;
            }

            string error = _world.Raft.TryPlace(_selectedPiece, cell);
            StatusLine = error ?? $"Built {_selectedPiece.Name} at {cell.x},{cell.y}.";
            _world.Reanalyse();
        }

        private void TryDismantle()
        {
            if (IsInWater)
            {
                StatusLine = "Cannot dismantle from the water. Get back on the deck.";
                return;
            }

            Vector2Int cell = DeckCellUnderMouse();
            string error = _world.Raft.TryDismantle(cell);
            StatusLine = error ?? $"Dismantled at {cell.x},{cell.y}.";
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
        /// Which deck cell the mouse is over, by intersecting the mouse ray with the
        /// deck plane. Cheaper and more reliable than raycasting against every
        /// member, and it keeps placement aligned to the build grid.
        /// </summary>
private Vector2Int DeckCellUnderMouse()
        {
            Camera camera = Camera.main;
            if (camera == null) return Vector2Int.zero;

            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, RaftState.BaseDeckY, 0f));

            if (!plane.Raycast(ray, out float distance)) return Vector2Int.zero;

            Vector3 point = ray.GetPoint(distance);
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


