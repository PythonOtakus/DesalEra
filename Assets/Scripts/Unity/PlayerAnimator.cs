using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DesalEra.Unity
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

        [Header("Hysteresis")]
        [Tooltip("Extra margin below walkThreshold before Idle wins, to stop Idle/Walk flicker.")]
        [SerializeField] private float walkExitMargin = 0.18f;

        [Tooltip("Extra margin below runThreshold before Walk wins, to stop Walk/Run flicker.")]
        [SerializeField] private float runExitMargin = 0.45f;

        [Header("Blending")]
        [Tooltip("Seconds to blend between one clip and the next.")]
        [SerializeField] private float crossFadeSeconds = 0.22f;

        [Header("Synthetic root motion (in-place clips)")]
        [Tooltip("Metres the survivor should travel per full SwimForward loop. Source clips are in-place.")]
        [SerializeField] private float swimForwardCycleMeters = 2.6f;

        [SerializeField] private float walkCycleMeters = 1.15f;
        [SerializeField] private float runCycleMeters = 2.0f;

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

        // Last band chosen by UpdateForSpeed. Kept across frames so hysteresis can
        // resist thrashing when speed sits on a threshold.
        private string _speedBand = "Idle";

        // The input that owns the output, and the one fading out under it. Exactly two
        // weights are ever touched, which is what makes the blend verifiable by reading
        // two numbers rather than auditing a list of every input touched so far.
        private int _activeInput = -1;
        private int _fadingFrom = -1;
        private int _fadingTo = -1;
        private float _fadeElapsed;

        // Used to turn in-place clip progress into planar travel.
        private string _motionState = string.Empty;
        private float _motionClipTime;

        /// <summary>True when the locomotion clips were found and bound.</summary>
        public bool HasClips => _graphBuilt;

        /// <summary>Clip currently selected, for the HUD and tests.</summary>
        public string CurrentState => _currentState;

        /// <summary>Metres covered by one loop of the current clip at playback speed 1.</summary>
        public float CurrentCycleMeters => CycleMetersFor(_currentState);

        /// <summary>
        /// Mixer weight of a state, 0 to 1. Lets placement that depends on the pose
        /// follow a cross-fade instead of jumping at its start.
        /// </summary>
        public float WeightOf(string state)
        {
            if (!_graphBuilt || !_inputByState.TryGetValue(state, out int input)) return 0f;
            return _mixer.GetInputWeight(input);
        }

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
                Debug.LogWarning("[DesalEra] survivor clips missing for: " +
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
                Debug.LogWarning("[DesalEra] survivor has no locomotion clips; " +
                                 "the character will stand still but the game is unaffected");
                return false;
            }

            Select("Idle", instant: true);
            return true;
        }

        /// <summary>
        /// Selects a clip from the player's speed. Speed rather than the input axes, so
        /// walking into a wall stops the legs. Hysteresis keeps the band from flicking
        /// when acceleration crosses a threshold every other frame.
        /// </summary>
        public void UpdateForSpeed(float speedMPerS)
        {
            if (!_graphBuilt) return;

            _speedBand = BandForSpeed(speedMPerS, _speedBand);
            Select(_speedBand);
        }

        /// <summary>
        /// Idle / Walk / Run band for a speed, with exit margins so leaving a band is
        /// harder than entering it. Exposed for EditMode tests.
        /// </summary>
        public string BandForSpeed(float speedMPerS, string currentBand)
        {
            switch (currentBand)
            {
                case "Run":
                    if (speedMPerS >= runThreshold - runExitMargin) return "Run";
                    return speedMPerS >= walkThreshold ? "Walk" : "Idle";

                case "Walk":
                    if (speedMPerS >= runThreshold) return "Run";
                    if (speedMPerS >= walkThreshold - walkExitMargin) return "Walk";
                    return "Idle";

                default:
                    if (speedMPerS >= runThreshold) return "Run";
                    if (speedMPerS >= walkThreshold) return "Walk";
                    return "Idle";
            }
        }

        /// <summary>
        /// Starts a clip by state name, for gameplay that has a state the speed rules
        /// cannot express. Unknown names are ignored rather than throwing, because this is
        /// called from movement code that must not be able to break the frame.
        /// </summary>
        public bool Play(string state, bool instant = false)
        {
            if (!_graphBuilt) return false;
            if (!_inputByState.ContainsKey(state)) return false;

            Select(state, instant);
            return true;
        }

        /// <summary>
        /// Scales the active clip's playback. Used so swim/walk strokes stay locked to
        /// travel speed instead of skating at a fixed anim rate.
        /// </summary>
        public void SetPlaybackSpeed(float speed)
        {
            if (!_graphBuilt || _activeInput < 0) return;
            if (!_playableByInput.TryGetValue(_activeInput, out var playable)) return;
            playable.SetSpeed(Mathf.Clamp(speed, 0.2f, 2.2f));
        }

        /// <summary>
        /// Planar displacement implied by clip time advance since the last call. Source
        /// survivor clips are in-place (no root XZ curves), so travel is synthesised from
        /// <see cref="swimForwardCycleMeters"/> / walk / run cycle lengths.
        /// </summary>
        public Vector3 ConsumePlanarAnimDelta(Vector3 facing)
        {
            if (!_graphBuilt || _activeInput < 0) return Vector3.zero;
            if (!_playableByInput.TryGetValue(_activeInput, out var playable)) return Vector3.zero;

            float meters = CycleMetersFor(_currentState);
            if (meters <= 0f)
            {
                _motionState = _currentState;
                _motionClipTime = (float)playable.GetTime();
                return Vector3.zero;
            }

            var clip = playable.GetAnimationClip();
            float length = clip != null ? clip.length : 0f;
            if (length < 1e-4f) return Vector3.zero;

            float time = (float)playable.GetTime();
            if (_motionState != _currentState)
            {
                _motionState = _currentState;
                _motionClipTime = time;
                return Vector3.zero;
            }

            float delta = time - _motionClipTime;
            // Loop wrap: time jumped backwards across the clip boundary.
            if (delta < -length * 0.5f) delta += length;
            if (delta < 0f) delta = 0f;
            _motionClipTime = time;

            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-6f) return Vector3.zero;
            return facing.normalized * (delta / length) * meters;
        }

        private float CycleMetersFor(string state)
        {
            switch (state)
            {
                case "SwimForward": return swimForwardCycleMeters;
                case "Walk": return walkCycleMeters;
                case "Run": return runCycleMeters;
                default: return 0f;
            }
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
            _motionState = string.Empty;

            if (instant || crossFadeSeconds <= 0f || _activeInput < 0)
            {
                SnapTo(to);
                return;
            }

            // Interrupt an in-flight blend cleanly: keep only the current active weight
            // so a third clip cannot leave a stranded non-zero input from the old fade.
            for (int i = 0; i < _mixer.GetInputCount(); i++)
                _mixer.SetInputWeight(i, i == _activeInput ? 1f : 0f);

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
            _motionState = string.Empty;

            if (_playableByInput.TryGetValue(to, out var playable))
            {
                playable.SetTime(0d);
                playable.SetSpeed(1d);
            }
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