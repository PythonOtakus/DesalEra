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
        private int _idleHash;
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

            _idleHash = Animator.StringToHash("Idle");
            _walkHash = Animator.StringToHash("Walk");
            _runHash = Animator.StringToHash("Run");

            // Animator.Play on a state the controller does not contain is silently
            // dropped, which would leave the survivor stuck in its imported pose with no
            // error anywhere. HasState is the runtime check for that.
            _ready = _animator.HasState(0, _walkHash);
            if (!_ready)
            {
                Debug.LogWarning("[CrazyAquarium] survivor controller has no Walk state; " +
                                 "re-run SurvivorClipBaker.BakeAll(). The survivor will not animate.");
                return false;
            }

            // A real idle clip exists now, so the survivor stands rather than freezing
            // mid-stride. The controller's default state is already Idle; playing it
            // explicitly keeps the starting state independent of how the controller was
            // authored.
            Play(_idleHash, "Idle", 1f);
            return true;
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
                // Idle plays at full speed. It used to be the walk cycle held at animator
                // speed zero, which stood the survivor frozen mid-stride.
                Play(_idleHash, "Idle", 1f);
            }
        }

        /// <summary>
        /// Selects a state, cross-fading out of the previous one.
        ///
        /// The animator speed is always 1 now that there is a real idle clip, so the only
        /// thing that decides the survivor's motion is which state is playing.
        /// </summary>
        private void Play(int stateHash, string stateName, float animatorSpeed)
        {
            if (_currentState == stateName) return;

            _animator.CrossFadeInFixedTime(stateHash, crossFadeSeconds);
            _currentState = stateName;
            _animator.speed = animatorSpeed;
        }
    }
}