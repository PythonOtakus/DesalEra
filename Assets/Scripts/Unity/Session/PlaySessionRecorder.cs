using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DesalEra.Unity.Session
{
    /// <summary>
    /// Records discrete player actions and periodic snapshots while in play mode.
    /// Start/stop from the DesalEra/Session menu or <c>unity-cli session --params</c>.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class PlaySessionRecorder : MonoBehaviour
    {
        [SerializeField] private float snapshotInterval = 0.25f;
        [SerializeField] private float lookDeltaThreshold = 2.5f;
        [SerializeField] private float moveSampleInterval = 0.15f;

        private readonly List<PlayActionRecord> _actions = new List<PlayActionRecord>();
        private readonly List<PlaySnapshot> _snapshots = new List<PlaySnapshot>();

        private bool _recording;
        private float _startedAt;
        private float _nextSnapshot;
        private float _nextMoveSample;
        private float _lastH;
        private float _lastV;
        private bool _lastSprint;
        private float _lastYaw;
        private float _lastPitch;
        private float _lastDistance;
        private string _sessionId;
        private Coroutine _replayRoutine;

        public static PlaySessionRecorder Instance { get; private set; }

        public bool IsRecording => _recording;
        public bool IsReplaying => _replayRoutine != null;
        public string CurrentSessionId => _sessionId;
        public int ActionCount => _actions.Count;
        public int SnapshotCount => _snapshots.Count;
        public string LastSavedPath { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            PlayDriver.ActionPerformed -= OnExternalAction;
        }

        private void OnEnable()
        {
            PlayDriver.ActionPerformed += OnExternalAction;
        }

        private void OnDisable()
        {
            PlayDriver.ActionPerformed -= OnExternalAction;
        }

        public string StartRecording(string notes = null)
        {
            if (_recording) return "already recording " + _sessionId;
            if (!Application.isPlaying) return "not in play mode";

            _actions.Clear();
            _snapshots.Clear();
            _sessionId = PlaySession.NewId();
            _startedAt = Time.time;
            _nextSnapshot = 0f;
            _nextMoveSample = 0f;
            _recording = true;

            var cam = PlayDriver.CameraOrbit;
            if (cam != null)
            {
                _lastYaw = cam.Yaw;
                _lastPitch = cam.Pitch;
                _lastDistance = cam.Distance;
            }

            CaptureSnapshot();
            Append(new PlayActionRecord
            {
                kind = PlayActionKind.Note,
                text = string.IsNullOrEmpty(notes) ? "recording started" : notes
            });

            Debug.Log("[DesalEra] session recording started: " + _sessionId);
            return _sessionId;
        }

        public string StopRecording()
        {
            if (!_recording) return "not recording";

            CaptureSnapshot();
            _recording = false;

            var session = BuildSession();
            session.Save();
            LastSavedPath = session.FilePath;
            Debug.Log("[DesalEra] session saved: " + session.FilePath);
            return session.FilePath;
        }

        public string Status()
        {
            if (IsReplaying) return "replaying";
            if (_recording)
                return $"recording id={_sessionId} t={Elapsed():F1}s actions={_actions.Count} snaps={_snapshots.Count}";
            return string.IsNullOrEmpty(LastSavedPath) ? "idle" : "idle last=" + LastSavedPath;
        }

        public string Replay(string pathOrId, float timeScale = 1f)
        {
            if (!Application.isPlaying) return "not in play mode";
            if (_recording) return "stop recording first";
            if (_replayRoutine != null) return "already replaying";

            PlaySession session;
            try { session = PlaySession.Load(pathOrId); }
            catch (System.Exception ex) { return ex.Message; }

            _replayRoutine = StartCoroutine(ReplayRoutine(session, Mathf.Max(0.05f, timeScale)));
            return "replaying " + session.id;
        }

        public void StopReplay()
        {
            if (_replayRoutine == null) return;
            StopCoroutine(_replayRoutine);
            _replayRoutine = null;
            PlayDriver.Move(0f, 0f, false);
        }

        private void Update()
        {
            if (!_recording) return;

            SampleInput();
            if (Time.time - _startedAt >= _nextSnapshot)
            {
                _nextSnapshot = Time.time - _startedAt + snapshotInterval;
                CaptureSnapshot();
            }
        }

        private void SampleInput()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            bool sprint = Input.GetKey(KeyCode.LeftShift);
            float now = Elapsed();

            bool moveChanged = !Mathf.Approximately(h, _lastH)
                            || !Mathf.Approximately(v, _lastV)
                            || sprint != _lastSprint;
            bool moving = Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f;

            if (moveChanged || (moving && now >= _nextMoveSample))
            {
                _lastH = h;
                _lastV = v;
                _lastSprint = sprint;
                _nextMoveSample = now + moveSampleInterval;
                Append(new PlayActionRecord
                {
                    kind = PlayActionKind.Move,
                    h = h,
                    v = v,
                    sprint = sprint
                });
            }

            var cam = PlayDriver.CameraOrbit;
            if (cam == null) return;

            if (Mathf.Abs(cam.Yaw - _lastYaw) >= lookDeltaThreshold
                || Mathf.Abs(cam.Pitch - _lastPitch) >= lookDeltaThreshold
                || Mathf.Abs(cam.Distance - _lastDistance) >= 0.35f)
            {
                _lastYaw = cam.Yaw;
                _lastPitch = cam.Pitch;
                _lastDistance = cam.Distance;
                Append(new PlayActionRecord
                {
                    kind = PlayActionKind.Look,
                    yaw = cam.Yaw,
                    pitch = cam.Pitch,
                    distance = cam.Distance
                });
            }
        }

        private void OnExternalAction(PlayActionRecord action)
        {
            if (!_recording || action == null) return;
            var copy = Clone(action);
            copy.t = Elapsed();
            _actions.Add(copy);
        }

        private void Append(PlayActionRecord action)
        {
            action.t = Elapsed();
            _actions.Add(action);
        }

        private void CaptureSnapshot()
        {
            _snapshots.Add(PlayDriver.Capture(Elapsed()));
        }

        public float Elapsed() => Time.time - _startedAt;

        private PlaySession BuildSession()
        {
            return new PlaySession
            {
                id = _sessionId,
                startedAt = System.DateTime.Now.AddSeconds(-Elapsed()).ToString("o"),
                stoppedAt = System.DateTime.Now.ToString("o"),
                duration = Elapsed(),
                actions = _actions.ToArray(),
                snapshots = _snapshots.ToArray()
            };
        }

        private IEnumerator ReplayRoutine(PlaySession session, float timeScale)
        {
            Debug.Log("[DesalEra] replaying " + session.id);
            float start = Time.time;
            int i = 0;
            var actions = session.actions ?? System.Array.Empty<PlayActionRecord>();

            while (i < actions.Length)
            {
                float target = start + actions[i].t / timeScale;
                while (Time.time < target)
                    yield return null;

                var action = actions[i];
                if (action.kind == PlayActionKind.Wait)
                    yield return new WaitForSeconds(action.seconds / timeScale);
                else
                    PlayDriver.Execute(Clone(action));

                i++;
            }

            PlayDriver.Move(0f, 0f, false);
            _replayRoutine = null;
            Debug.Log("[DesalEra] replay finished " + session.id);
        }

        private static PlayActionRecord Clone(PlayActionRecord a)
        {
            return new PlayActionRecord
            {
                kind = a.kind,
                t = a.t,
                h = a.h,
                v = a.v,
                sprint = a.sprint,
                yaw = a.yaw,
                pitch = a.pitch,
                distance = a.distance,
                index = a.index,
                cellX = a.cellX,
                cellY = a.cellY,
                level = a.level,
                facing = a.facing,
                x = a.x,
                y = a.y,
                z = a.z,
                panel = a.panel,
                seconds = a.seconds,
                text = a.text,
                result = a.result
            };
        }
    }
}
