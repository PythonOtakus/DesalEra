using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CrazyAquarium.Unity
{
    /// <summary>
    /// Plays the survivor's clips through a Playables graph, choosing between them from
    /// movement speed.
    ///
    /// This does not use an AnimatorController. A controller authored from script turned
    /// out to be unusable: the states an AnimatorStateMachine creates in script are not
    /// registered as proper sub-assets, so they survive in the asset file but are dropped
    /// the moment the asset is reimported. The editor plays them from the live object, so
    /// every play-mode test passed, and a build shipped a controller with one layer and no
    /// states -- leaving the survivor frozen in its bind T-pose with no visible cause. A
    /// graph has no asset and nothing to serialise, so the editor and a build run the same
    /// code and cannot disagree.
    ///
    /// A mixer also fits how this is driven. States were already being selected in code
    /// rather than by transition thresholds, so the only thing a controller was providing
    /// was somewhere for the clips to live.
    /// </summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        [Header("Speed thresholds (metres per second)")]
        [SerializeField] private float walkThreshold = 0.4f;
        [SerializeField] private float runThreshold = 3.8f;

        [Header("Blending")]
        [Tooltip("Seconds to blend between one clip and the next.")]
        [SerializeField] private float crossFadeSeconds = 0.18f;

        /// <summary>
        /// Clips baked for states the gameplay does not reach yet. Swimming needs the
        /// player to be able to leave the deck, and the ladder and rope clips need
        /// structures to climb and hang from. They are wired up so enabling either is a
        /// gameplay change rather than an animation change.
        /// </summary>
        private static readonly string[] SpareStates =
        {
            "Jump", "JumpObstacle", "SwimIdle", "SwimForward",
            "LadderMountStart", "LadderClimbLoop", "LadderClimbFinish",
            "RopeHangIdle", "RopeSwingToGround",
        };

        private PlayerAvatar _avatar;

        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private bool _graphBuilt;

        private readonly Dictionary<string, int> _inputByState = new Dictionary<string, int>();
        private readonly Dictionary<int, AnimationClipPlayable> _playableByInput = new Dictionary<int, AnimationClipPlayable>();

        private string _currentState = string.Empty;

        // The input that owns the output, and the one fading out under it. Exactly two
        // weights are ever touched, which is what makes the blend verifiable by reading
        // two numbers rather than auditing a list of every input touched so far.
        private int _activeInput = -1;
        private int _fadingFrom = -1;
        private int _fadingTo = -1;
        private float _fadeElapsed;

        /// <summary>True when the locomotion clips were found and bound.</summary>
        public bool HasClips => _graphBuilt;

        /// <summary>Clip currently selected, for the HUD and tests.</summary>
        public string CurrentState => _currentState;

        private void Awake()
        {
            _avatar = GetComponent<PlayerAvatar>();
        }

        /// <summary>
        /// Builds the graph. Deferred out of Awake because the avatar instantiates its
        /// model in Initialise, which runs after Awake, so the model root does not exist
        /// yet at this point.
        ///
        /// Returns false when the locomotion clips are missing, which is not fatal: the
        /// game stays playable with a static survivor and the HUD says so.
        /// </summary>
        public bool Initialise()
        {
            GameObject host = _avatar != null ? _avatar.ModelRoot : gameObject;
            if (host == null) return false;

            var animator = host.GetComponent<Animator>();
            if (animator == null) animator = host.AddComponent<Animator>();

            // The animator exists only as the output target for the graph. A controller
            // here would fight the playable graph for control of the skeleton.
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _graph = PlayableGraph.Create("SurvivorAnimation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            var output = AnimationPlayableOutput.Create(_graph, "Animation", animator);

            var wanted = new List<string> { "Idle", "Walk", "Run" };
            wanted.AddRange(SpareStates);

            int inputs = wanted.Count;
            _mixer = AnimationMixerPlayable.Create(_graph, inputs);
            output.SetSourcePlayable(_mixer);

            var missing = new List<string>();

            for (int i = 0; i < inputs; i++)
            {
                string state = wanted[i];
                var clip = Resources.Load<AnimationClip>("Survivor_" + state);

                if (clip == null)
                {
                    missing.Add(state);
                    continue;
                }

                var playable = AnimationClipPlayable.Create(_graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);

                _graph.Connect(playable, 0, _mixer, i);
                _mixer.SetInputWeight(i, 0f);
                _inputByState[state] = i;
                _playableByInput[i] = playable;
            }

            if (missing.Count > 0)
            {
                Debug.LogWarning("[CrazyAquarium] survivor clips missing for: " +
                                 string.Join(", ", missing.ToArray()) +
                                 ". Re-run SurvivorClipBaker; those states will not play.");
            }

            // Locomotion is the minimum for the character to be a character rather than a
            // prop. Spare states are optional.
            bool hasLocomotion = _inputByState.ContainsKey("Idle")
                              && _inputByState.ContainsKey("Walk")
                              && _inputByState.ContainsKey("Run");

            _graph.Play();
            _graphBuilt = hasLocomotion;

            if (!_graphBuilt)
            {
                Debug.LogWarning("[CrazyAquarium] survivor has no locomotion clips; " +
                                 "the character will stand still but the game is unaffected");
                return false;
            }

            Select("Idle", instant: true);
            return true;
        }

        /// <summary>
        /// Selects a clip from the player's speed. Speed rather than the input axes, so
        /// walking into a wall stops the legs.
        /// </summary>
        public void UpdateForSpeed(float speedMPerS)
        {
            if (!_graphBuilt) return;

            if (speedMPerS >= runThreshold) Select("Run");
            else if (speedMPerS >= walkThreshold) Select("Walk");
            else Select("Idle");
        }

        /// <summary>
        /// Starts a clip by state name, for gameplay that has a state the speed rules
        /// cannot express. Unknown names are ignored rather than throwing, because this is
        /// called from movement code that must not be able to break the frame.
        /// </summary>
        public bool Play(string state)
        {
            if (!_graphBuilt) return false;
            if (!_inputByState.ContainsKey(state)) return false;

            Select(state);
            return true;
        }

        private void Select(string state, bool instant = false)
        {
            if (state == _currentState) return;

            if (!_inputByState.TryGetValue(state, out int to))
            {
                // Idle exists whenever locomotion does, but a spare state can be missing if
                // its clip failed to bake. Falling back keeps the character animated.
                if (state != "Idle" && _inputByState.ContainsKey("Idle"))
                {
                    Select("Idle", instant);
                }
                return;
            }

            // Moving to the clip that is already fading in is not a state change, and
            // treating it as one used to leave _fadingFrom equal to _fadingTo. The blend
            // then finished by zeroing the very input it had just raised to full weight,
            // which silenced the character with no error anywhere: every weight read 0.
            if (to == _activeInput) return;

            _currentState = state;

            if (instant || crossFadeSeconds <= 0f || _activeInput < 0)
            {
                SnapTo(to);
                return;
            }

            // Start the incoming clip from its beginning. Starting it wherever the previous
            // play left it produces a visible jump on every state change.
            if (_playableByInput.TryGetValue(to, out var incoming))
            {
                incoming.SetTime(0d);
                incoming.SetSpeed(1d);
            }

            _fadingFrom = _activeInput;
            _fadingTo = to;
            _activeInput = to;
            _fadeElapsed = 0f;

            _mixer.SetInputWeight(to, 0f);
        }

        /// <summary>
        /// Puts the mixer straight onto one input with no blend.
        /// </summary>
        private void SnapTo(int to)
        {
            for (int i = 0; i < _mixer.GetInputCount(); i++) _mixer.SetInputWeight(i, 0f);

            _mixer.SetInputWeight(to, 1f);
            _activeInput = to;
            _fadingFrom = -1;
            _fadingTo = -1;
            _fadeElapsed = 0f;
        }

        private void Update()
        {
            if (!_graphBuilt || _fadingTo < 0) return;

            _fadeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_fadeElapsed / crossFadeSeconds);

            if (_fadingFrom >= 0) _mixer.SetInputWeight(_fadingFrom, 1f - t);
            _mixer.SetInputWeight(_fadingTo, t);

            if (t < 1f) return;

            if (_fadingFrom >= 0)
            {
                _mixer.SetInputWeight(_fadingFrom, 0f);
                _fadingFrom = -1;
            }

            _fadingTo = -1;
        }

        private void OnDestroy()
        {
            // Destroying an already-invalid graph throws, and the avatar can be torn down
            // twice when the scene reloads.
            if (_graphBuilt || _graph.IsValid()) _graph.Destroy();
            _graphBuilt = false;
        }
    }
}