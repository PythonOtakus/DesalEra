using UnityEngine;

namespace CrazyAquarium.Unity
{
    /// <summary>
    /// Plays the survivor's walk and run clips based on movement speed.
    ///
    /// Uses Mecanim with a controller baked by SurvivorClipBaker. The legacy Animation
    /// component was tried first and abandoned: with the generated rig it advanced its
    /// clock without moving a single bone, and its state time accumulated past the clip
    /// length instead of looping. Animator is the supported path for an imported rig and
    /// drives the skeleton through the same transform hierarchy the mesh is bound to.
    ///
    /// The controller is authored with states but no transitions. One comparison against
    /// the run threshold already expresses a two-state machine, and a parameter-driven
    /// transition would only move that comparison somewhere less readable.
    /// </summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        private const string ControllerResource = "Survivor";

        [Header("Speed thresholds (metres per second)")]
        [SerializeField] private float walkThreshold = 0.4f;
        [SerializeField] private float runThreshold = 3.8f;

        [Header("Blending")]
        [Tooltip("Seconds to blend between walk and run.")]
        [SerializeField] private float crossFadeSeconds = 0.18f;

        private PlayerAvatar _avatar;
        private Animator _animator;
        private int _walkHash;
        private int _runHash;
        private bool _ready;
        private string _currentState = string.Empty;

        /// <summary>True when a controller with a walk state was bound.</summary>
        public bool HasClips => _ready;

        /// <summary>State currently selected, for the HUD and tests.</summary>
        public string CurrentState => _currentState;

        private void Awake()
        {
            _avatar = GetComponent<PlayerAvatar>();
        }

        /// <summary>
        /// Binds the baked controller. Deferred out of Awake because the avatar
        /// instantiates its model in Initialise, which runs after Awake, so the model root
        /// does not exist yet at this point.
        /// </summary>
        public bool Initialise()
        {
            GameObject host = _avatar != null ? _avatar.ModelRoot : gameObject;
            if (host == null) return false;

            var controller = Resources.Load<RuntimeAnimatorController>(ControllerResource);
            if (controller == null)
            {
                Debug.LogWarning("[CrazyAquarium] survivor animator controller missing; run " +
                                 "SurvivorClipBaker.BakeAll(). The survivor will not animate.");
                return false;
            }

            _animator = host.GetComponent<Animator>();
            if (_animator == null) _animator = host.AddComponent<Animator>();
            _animator.runtimeAnimatorController = controller;
            _animator.applyRootMotion = false;

            // The survivor regularly leaves the camera frustum while the camera holds a
            // fixed follow offset, and a culled pose snaps back visibly on return.
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _walkHash = Animator.StringToHash("Walk");
            _runHash = Animator.StringToHash("Run");

            _animator.Play(_walkHash, 0, 0f);
            _animator.speed = 0f;
            _currentState = "Idle";

            // Animator.Play on a state the controller does not contain is silently
            // dropped, which would leave the survivor stuck in its imported pose with no
            // error anywhere. HasState is the runtime check for that.
            _ready = _animator.HasState(0, _walkHash);
            if (!_ready)
            {
                Debug.LogWarning("[CrazyAquarium] survivor controller has no Walk state; " +
                                 "re-run SurvivorClipBaker.BakeAll(). The survivor will not animate.");
            }

            return _ready;
        }

        /// <summary>
        /// Selects a clip from the player's speed. Speed rather than the input axes, so
        /// walking into a wall stops the legs.
        /// </summary>
        public void UpdateForSpeed(float speedMPerS)
        {
            if (!_ready) return;

            if (speedMPerS >= runThreshold)
            {
                Play(_runHash, "Run", 1f);
            }
            else if (speedMPerS >= walkThreshold)
            {
                Play(_walkHash, "Walk", 1f);
            }
            else
            {
                // There is no generated idle clip. Holding the walk pose by setting the
                // animator speed to zero reads as standing still, where switching to an
                // absent idle state would read as a character frozen mid-stride.
                Play(_walkHash, "Idle", 0f);
            }
        }

        private void Play(int stateHash, string stateName, float animatorSpeed)
        {
            if (_animator.speed != animatorSpeed || _currentState != stateName)
            {
                // Re-issuing the same state every frame would restart its timeline each
                // frame, pinning the survivor in its first pose.
                if (_currentState != stateName) _animator.Play(stateHash, 0, 0f);
                _currentState = stateName;
            }

            _animator.speed = animatorSpeed;
        }
    }
}