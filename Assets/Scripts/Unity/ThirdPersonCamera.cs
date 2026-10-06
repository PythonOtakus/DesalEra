using UnityEngine;

namespace DesalEra.Unity
{
    /// <summary>
    /// Third-person orbit camera.
    ///
    /// Replaces a fixed follow offset, which is what made the game read as a top-down
    /// builder: the survivor could never be seen from the front, could never be looked at
    /// from a low angle, and the framing never changed. Orbiting is the single change
    /// that moves the feel from an SLG toward a third-person action game.
    ///
    /// Look is on right-mouse drag rather than a locked pointer on purpose. A locked
    /// pointer is the purist choice, but building is placed with the left button and a
    /// hidden cursor makes that impossible. Drag-to-look keeps both hands on the mouse
    /// and leaves the left button free.
    ///
    /// The wheel zooms, which is why build pieces moved to the number keys: one meaning
    /// per input, and the wheel is worth more as a camera control.
    /// </summary>
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Framing")]
        [Tooltip("Height above the survivor's feet that the camera orbits around.")]
        [SerializeField] private float pivotHeight = 1.45f;

        [Tooltip("Lateral offset, so the survivor sits off-centre instead of dead middle.")]
        [SerializeField] private float shoulderOffset = 0.6f;

        [SerializeField] private float minPitch = -14f;
        [SerializeField] private float maxPitch = 74f;

        [Header("Distance")]
        [SerializeField] private float distance = 7f;
        [SerializeField] private float minDistance = 2.4f;
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private float zoomSpeed = 6f;

        [Header("Sensitivity")]
        [SerializeField] private float yawSensitivity = 0.14f;
        [SerializeField] private float pitchSensitivity = 0.11f;

        [Header("Feel")]
        [Tooltip("Higher is tighter. The pivot lags the survivor slightly, which reads as weight.")]
        [SerializeField] private float followLerp = 12f;

        [Tooltip("Radius used when pulling the camera in past geometry.")]
        [SerializeField] private float collisionRadius = 0.28f;

        [Header("Click detection")]
        [Tooltip("Cursor travel, in pixels, that turns a right-button press into a drag rather than a click.")]
        [SerializeField] private float clickMaxTravel = 6f;

        [Tooltip("Longest press still counted as a click, in seconds.")]
        [SerializeField] private float clickMaxDuration = 0.4f;

        private Transform _target;
        private Camera _camera;
        private float _yaw;
        private float _pitch = 18f;
        private Vector3 _pivot;

        // The right button does two jobs: dragging looks the camera, and a plain click
        // dismantles. Unity offers no way to tell those apart directly, so the press is
        // tracked by hand. GetMouseButtonDown cannot be combined with !GetMouseButton to
        // separate them -- on the frame the button goes down both are true, so the test
        // is never satisfied and dismantle silently stops working.
        private bool _lookHeld;
        private Vector2 _pressPosition;
        private float _pressTime;
        private bool _clickedThisFrame;

        /// <summary>
        /// Horizontal forward, for camera-relative movement. Flattened so that looking up
        /// does not tilt the movement plane into the ground.
        /// </summary>
        public Vector3 FlatForward
        {
            get
            {
                Vector3 forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
                return forward.sqrMagnitude < 1e-6f ? Vector3.forward : forward.normalized;
            }
        }

        public Vector3 FlatRight
        {
            get
            {
                Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
                return right.sqrMagnitude < 1e-6f ? Vector3.right : right.normalized;
            }
        }

        public float Distance => distance;

        public void Initialise(Transform target, Camera camera)
        {
            _target = target;
            _camera = camera;

            _pivot = PivotPoint();
            ApplyTransform(1f);
        }

        private Vector3 PivotPoint()
        {
            Vector3 point = _target.position + Vector3.up * pivotHeight;
            point += Quaternion.Euler(0f, _yaw, 0f) * Vector3.right * shoulderOffset;
            return point;
        }

        /// <summary>
        /// True once per right-button press that did not turn into a look drag. The
        /// player controller uses it to dismantle, so a click and a drag stay distinct.
        /// </summary>
        public bool ConsumeLookClick()
        {
            if (!_clickedThisFrame) return false;
            _clickedThisFrame = false;
            return true;
        }

        private void Update()
        {
            if (_target == null || _camera == null) return;

            HandleLook();
            HandleZoom();

            _pivot = Vector3.Lerp(_pivot, PivotPoint(), 1f - Mathf.Exp(-followLerp * Time.deltaTime));
            ApplyTransform(1f);
        }

        private void HandleLook()
        {
            // Right button only. Responding to bare mouse movement would swing the camera
            // whenever the player moved the cursor toward the build button.
            if (Input.GetMouseButtonDown(1))
            {
                _lookHeld = true;
                _pressPosition = Input.mousePosition;
                _pressTime = Time.time;
            }

            if (_lookHeld && Input.GetMouseButtonUp(1))
            {
                _lookHeld = false;

                float travel = Vector2.Distance(_pressPosition, Input.mousePosition);
                if (travel <= clickMaxTravel && Time.time - _pressTime <= clickMaxDuration)
                {
                    _clickedThisFrame = true;
                }
            }

            if (!Input.GetMouseButton(1)) return;

            _yaw += Input.GetAxis("Mouse X") * yawSensitivity * 12f;
            _pitch -= Input.GetAxis("Mouse Y") * pitchSensitivity * 12f;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.0001f) return;

            distance = Mathf.Clamp(
                distance - scroll * zoomSpeed * 4f,
                minDistance,
                maxDistance);
        }

        private void ApplyTransform(float lerp)
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 offset = rotation * Vector3.back * distance;

            // Pull in rather than let the camera sit inside geometry. Nothing in the
            // greybox carries a collider yet, so this does nothing until members do; it
            // is here so the camera does not start clipping the moment they do.
            float allowed = distance;
            if (Physics.SphereCast(_pivot, collisionRadius, offset.normalized,
                                   out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                allowed = Mathf.Max(minDistance * 0.5f, hit.distance - 0.05f);
            }

            Vector3 desired = _pivot + rotation * Vector3.back * allowed;

            if (lerp >= 1f) _camera.transform.position = desired;
            else _camera.transform.position = Vector3.Lerp(_camera.transform.position, desired, lerp);

            // Rotation is applied directly rather than smoothed. Smoothing the framing
            // while the player is dragging makes the camera feel like it is lagging
            // behind the intent.
            _camera.transform.rotation = rotation;
        }
    }
}