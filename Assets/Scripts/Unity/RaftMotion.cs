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

        /// <summary>World-space point on a raft-local horizontal plane at <paramref name="localY"/>.</summary>
        public Vector3 SurfacePoint(Vector3 worldXZ, float localY)
        {
            Vector3 local = transform.InverseTransformPoint(new Vector3(worldXZ.x, 0f, worldXZ.z));
            local.y = localY;
            return transform.TransformPoint(local);
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
