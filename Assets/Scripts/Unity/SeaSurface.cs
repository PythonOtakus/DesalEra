using UnityEngine;

namespace DesalEra.Unity
{
    /// <summary>
    /// Drives the Gerstner sea material: calm vs storm amplitude, and keeps the mesh
    /// bounds inflated so frustum culling does not clip wave crests.
    ///
    /// Host-game oceans are almost always a few summed Gerstner waves plus a detail
    /// chop and Fresnel sky reflection — that is the look this component is tuning,
    /// not a fluid sim.
    /// </summary>
    public sealed class SeaSurface : MonoBehaviour
    {
        private Material _material;
        private GameBootstrap _world;
        private float _amplitude = 1f;
        private MeshFilter _filter;

        [SerializeField] private float calmAmplitude = 1.05f;
        [SerializeField] private float stormAmplitude = 1.55f;
        [SerializeField] private float stormBlendSpeed = 0.55f;
        [SerializeField] private float calmSpeed = 1f;
        [SerializeField] private float stormSpeed = 1.25f;

        public void Initialise(Material material, GameBootstrap world)
        {
            _material = material;
            _world = world;
            _filter = GetComponent<MeshFilter>();
            Apply(immediate: true);
            ExpandBounds();
        }

        private void ExpandBounds()
        {
            // Vertex displacement is done in the shader, so Unity's static mesh bounds
            // would be a flat plane and distant crests would pop. Grow the AABB enough
            // for the tallest storm swell we allow.
            if (_filter == null || _filter.sharedMesh == null) return;
            Mesh mesh = _filter.sharedMesh;
            Bounds b = mesh.bounds;
            float pad = 4f;
            b.Expand(new Vector3(pad, pad * 2f, pad));
            mesh.bounds = b;
        }

        private void Update()
        {
            if (_material == null) return;
            Apply(immediate: false);
        }

        private void Apply(bool immediate)
        {
            bool storm = _world != null && _world.IsStormActive;
            float targetAmp = storm ? stormAmplitude : calmAmplitude;
            float targetSpeed = storm ? stormSpeed : calmSpeed;

            if (immediate)
            {
                _amplitude = targetAmp;
            }
            else
            {
                _amplitude = Mathf.Lerp(_amplitude, targetAmp, Time.deltaTime * stormBlendSpeed);
            }

            float speed = immediate
                ? targetSpeed
                : Mathf.Lerp(_material.GetFloat("_Speed"), targetSpeed,
                             Time.deltaTime * stormBlendSpeed);

            if (_world != null)
            {
                SeaWave.Amplitude = _amplitude;
                SeaWave.Speed = speed;
            }

            // Near drives SeaWave; far mirrors it so the ring seam stays continuous.
            _material.SetFloat("_Amplitude", SeaWave.Amplitude);
            _material.SetFloat("_Speed", SeaWave.Speed);
        }
    }
}
