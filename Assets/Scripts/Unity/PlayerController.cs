using CrazyAquarium.Game;
using UnityEngine;

namespace CrazyAquarium.Unity
{
    /// <summary>
    /// Player movement, gathering, building and dismantling.
    ///
    /// The player walks on the deck rather than swimming: this is a raft, and being
    /// able to fall off is a later problem. Keeping the character on the platform
    /// means the build preview can assume deck coordinates directly, which is what
    /// makes placement with a mouse tractable.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5.5f;
        [SerializeField] private float sprintMultiplier = 1.7f;

        [Header("Build")]
        [Tooltip("Deck half-extent the player is confined to, in cells.")]
        [SerializeField] private float walkRadiusCells = 5.2f;

        [Header("Costs")]
        [Tooltip("Survival units spent per second of continuous work, before recovery.")]
        [SerializeField] private float workCostPerSecond = 1.6f;

        private GameBootstrap _world;
        private Transform _cameraTransform;

        private BuildPiece _selectedPiece;
        private int _selectedIndex;

        private static readonly BuildPiece[] Palette =
        {
            BuildPiece.Deck(),
            BuildPiece.Column(),
            BuildPiece.Pontoon(),
            BuildPiece.Brace(),
            BuildPiece.Still()
        };

        public BuildPiece SelectedPiece => _selectedPiece;

        /// <summary>The visible character, if one loaded.</summary>
        public PlayerAvatar Avatar { get; set; }

        /// <summary>The survivor's animation state, if clips were found.</summary>
        public PlayerAnimator Animator { get; set; }

        /// <summary>Index into the build palette, for the HUD to highlight.</summary>
        public int SelectedIndex => _selectedIndex;

        public string StatusLine { get; private set; } = string.Empty;

        public void Initialise(GameBootstrap world, Transform cameraTransform)
        {
            _world = world;
            _cameraTransform = cameraTransform;
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
        }

        private void HandleMovement()
        {
            // Camera-relative so the controls follow what the player sees rather than
            // a fixed world axis, which is the usual first-session complaint.
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (_cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            }

            Vector3 move = (forward * v + right * h);
            if (move.sqrMagnitude < 0.0001f)
            {
                if (Animator != null) Animator.UpdateForSpeed(0f);
                return;
            }
            move.Normalize();

            bool sprinting = Input.GetKey(KeyCode.LeftShift);
            float speed = moveSpeed * (sprinting ? sprintMultiplier : 1f);

            // Animation is driven from the speed actually applied, not from the input
            // axes, so a walk into a wall does not keep the legs pumping.
            if (Animator != null) Animator.UpdateForSpeed(speed);

            Vector3 next = transform.localPosition + move * (speed * Time.deltaTime);

            next.y = RaftState.BaseDeckY + 0.6f;

            // Confine to the deck. A later task can add falling overboard as a
            // consequence; for now walking off a raft into the sea is just a bug.
            float limit = walkRadiusCells * RaftState.CellSize;
            next.x = Mathf.Clamp(next.x, -limit, limit);
            next.z = Mathf.Clamp(next.z, -limit, limit);

            // Turn the model to face the way it is travelling, so the character does
            // not slide sideways. Kept separate from the transform: the controller
            // owns position, the avatar owns orientation.
            if (Avatar != null) Avatar.FaceTowards(move);

            transform.localPosition = next;
        }

        private void HandleBuildInput()
        {
            if (Input.mouseScrollDelta.y != 0f)
            {
                int delta = (int)Mathf.Sign(Input.mouseScrollDelta.y);
                _selectedIndex = Mathf.Clamp(_selectedIndex - delta, 0, Palette.Length - 1);
                _selectedPiece = Palette[_selectedIndex];
            }

            if (Input.GetMouseButtonDown(0) && _selectedPiece != null) TryBuild();
            if (Input.GetMouseButtonDown(1)) TryDismantle();
            if (Input.GetKeyDown(KeyCode.E)) EatRation();
        }

        private void TryBuild()
        {
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
            if (_cameraTransform == null) return Vector2Int.zero;

            Ray ray = _cameraTransform.GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, RaftState.BaseDeckY, 0f));

            if (!plane.Raycast(ray, out float distance)) return Vector2Int.zero;

            Vector3 point = ray.GetPoint(distance);
            return new Vector2Int(
                Mathf.RoundToInt(point.x / RaftState.CellSize),
                Mathf.RoundToInt(point.z / RaftState.CellSize));
        }

        /// <summary>
        /// Building and gathering both cost stamina, so the player cannot stand still
        /// and accumulate an infinite base. The cost is paid here rather than inside
        /// the model because it is an input concern, not a rules one.
        /// </summary>
        private void ApplyWorkCost()
        {
            bool working = Input.GetMouseButton(0) || Input.GetMouseButton(1)
                           || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f;

            if (working) _world.Raft.Survival.Add(Vital.Stamina, -workCostPerSecond * Time.deltaTime);
        }
    }
}
