using DesalEra.Game;
using UnityEngine;

namespace DesalEra.Unity
{
    /// <summary>
    /// Low-frequency raft motion from the same Gerstner swell the sea draws.
    /// Heave + light pitch/roll only — chop stays in fragment normals so the deck
    /// does not vibrate like a drum skin.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class RaftMotion : MonoBehaviour
    {
        [SerializeField] private float heaveFollow = 4f;
        [SerializeField] private float tiltFollow = 3f;
        [SerializeField] private float maxTiltDeg = 6f;
        [SerializeField] private float heaveScale = 0.55f;

        private float _heave;
        private float _pitchDeg;
        private float _rollDeg;

        /// <summary>Smoothed vertical offset applied to the raft root (metres).</summary>
        public float HeaveY => _heave;

        /// <summary>World-space point on the moving deck plane (deck-local Y = BaseDeckY).</summary>
        public Vector3 DeckPoint(Vector3 worldXZ) => SurfacePoint(worldXZ, RaftState.BaseDeckY);

        /// <summary>
        /// World-space point on a raft-local horizontal plane at <paramref name="localY"/>,
        /// straight above or below <paramref name="worldXZ"/>.
        /// </summary>
        public Vector3 SurfacePoint(Vector3 worldXZ, float localY) =>
            transform.TransformPoint(LocalOnPlane(worldXZ, localY));

        /// <summary>
        /// Raft-local coordinates of where the world vertical through <paramref name="world"/>
        /// meets the raft-local plane at <paramref name="localY"/>.
        ///
        /// Independent of the point's own height. Converting the point itself would not be:
        /// on a tilted raft, points above each other map to local XZ that differ by
        /// height × sin(tilt), about 0.3 m between a survivor standing on the deck and the
        /// same survivor dropped to the waterline. At the deck edge that difference alone
        /// flipped them between deck and water every frame.
        /// </summary>
        public Vector3 LocalOnPlane(Vector3 world, float localY)
        {
            Vector3 normal = transform.up;
            Vector3 origin = transform.TransformPoint(new Vector3(0f, localY, 0f));
            float rise = Mathf.Max(normal.y, 1e-3f);
            float t = Vector3.Dot(origin - world, normal) / rise;
            Vector3 local = transform.InverseTransformPoint(world + Vector3.up * t);
            local.y = localY;
            return local;
        }

        private void LateUpdate()
        {
            float half = RaftState.CellSize;
            // Starting-raft footprint corners in root-local XZ (root sits at world origin).
            float hNw = SeaWave.HeightAt(transform.TransformPoint(new Vector3(-half, 0f, -half)));
            float hNe = SeaWave.HeightAt(transform.TransformPoint(new Vector3(half, 0f, -half)));
            float hSw = SeaWave.HeightAt(transform.TransformPoint(new Vector3(-half, 0f, half)));
            float hSe = SeaWave.HeightAt(transform.TransformPoint(new Vector3(half, 0f, half)));

            float targetHeave = (hNw + hNe + hSw + hSe) * 0.25f * heaveScale;
            // Pitch: nose (+Z) up positive when south edge higher. Roll: starboard (+X) up.
            float span = half * 2f;
            float targetPitch = Mathf.Clamp(
                Mathf.Atan2(((hSw + hSe) - (hNw + hNe)) * 0.5f * heaveScale, span) * Mathf.Rad2Deg,
                -maxTiltDeg, maxTiltDeg);
            float targetRoll = Mathf.Clamp(
                Mathf.Atan2(((hNe + hSe) - (hNw + hSw)) * 0.5f * heaveScale, span) * Mathf.Rad2Deg,
                -maxTiltDeg, maxTiltDeg);

            float dt = Time.deltaTime;
            _heave = Mathf.Lerp(_heave, targetHeave, 1f - Mathf.Exp(-heaveFollow * dt));
            _pitchDeg = Mathf.Lerp(_pitchDeg, targetPitch, 1f - Mathf.Exp(-tiltFollow * dt));
            _rollDeg = Mathf.Lerp(_rollDeg, targetRoll, 1f - Mathf.Exp(-tiltFollow * dt));

            transform.localPosition = new Vector3(0f, _heave, 0f);
            transform.localRotation = Quaternion.Euler(_pitchDeg, 0f, -_rollDeg);
        }
    }
}
