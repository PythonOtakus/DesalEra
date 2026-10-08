using DesalEra.Game;
using UnityEngine;

namespace DesalEra.Unity
{
    /// <summary>
    /// Keeps a prop on the live sea surface. Without this, salvage spheres look pasted
    /// onto a scrolling texture rather than floating in the same water the player sees.
    /// </summary>
    public sealed class WaterBob : MonoBehaviour
    {
        [SerializeField] private float sinkM = 0.12f;
        [SerializeField] private float follow = 10f;

        private float _restY;

        private void Awake()
        {
            _restY = transform.position.y;
        }

        private void LateUpdate()
        {
            Vector3 p = transform.position;
            float target = RaftState.WaterLevelY + SeaWave.HeightAt(p) - sinkM;
            // Keep lateral placement; only ride heave so pickups do not drift off their ring.
            float y = Mathf.Lerp(p.y, target, 1f - Mathf.Exp(-follow * Time.deltaTime));
            transform.position = new Vector3(p.x, y, p.z);
        }
    }
}
