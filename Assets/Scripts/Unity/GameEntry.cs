using UnityEngine;

namespace DesalEra.Unity
{
    /// <summary>
    /// Single entry point. Drop this on one empty GameObject in an otherwise empty
    /// scene, or let it create everything itself, and the game runs.
    ///
    /// The whole world is assembled here at runtime rather than authored in a scene
    /// file. That keeps the project diffable and reviewable, which matters more here
    /// than the convenience of dragging things around in the inspector.
    /// </summary>
    public sealed class GameEntry : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private float cameraFieldOfView = 60f;

        [Header("Lighting")]
        [SerializeField] private Vector3 lightDirection = new Vector3(0.4f, -0.8f, 0.45f);
        [SerializeField] private Color skyColour = new Color(0.42f, 0.48f, 0.52f);
        [SerializeField] private Color fogColour = new Color(0.36f, 0.42f, 0.46f);
        [SerializeField] private float fogDensity = 0.018f;

        private ThirdPersonCamera _orbit;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            BuildWorld();
            BuildLighting();
        }

        private void BuildWorld()
        {
            var world = new GameObject("World");
            GameBootstrap bootstrap = world.AddComponent<GameBootstrap>();

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(world.transform, worldPositionStays: false);

            var player = playerGo.AddComponent<PlayerController>();
            var avatar = playerGo.AddComponent<PlayerAvatar>();
            var animator = playerGo.AddComponent<PlayerAnimator>();

            // The camera must exist before the player initialises, because movement is
            // camera-relative and would otherwise fall back to a fixed world axis on
            // the first frame.
            Camera camera = CreateCamera();
            _orbit = world.AddComponent<ThirdPersonCamera>();

            player.Initialise(bootstrap, _orbit);
            avatar.Initialise(playerGo.transform);
            animator.Initialise();
            player.Avatar = avatar;
            player.Animator = animator;

            _orbit.Initialise(playerGo.transform, camera);

            var hud = world.AddComponent<HudController>();
            hud.Initialise(bootstrap, player);

            bootstrap.Reanalyse();
        }

        private Camera CreateCamera()
        {
            var cameraGo = new GameObject("MainCamera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = cameraFieldOfView;
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 400f;
            cameraGo.AddComponent<AudioListener>();
            return camera;
        }

        /// <summary>
        /// A single directional light plus fog. No skybox asset: a flat background
        /// colour is honest about being a greybox and costs nothing to load.
        /// </summary>
        private void BuildLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = skyColour;
            RenderSettings.ambientEquatorColor = skyColour * 0.8f;
            RenderSettings.ambientGroundColor = skyColour * 0.45f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColour;
            RenderSettings.fogDensity = fogDensity;

            var lightGo = new GameObject("Sun");
            lightGo.transform.SetParent(transform, worldPositionStays: false);
            lightGo.transform.localRotation = Quaternion.LookRotation(-lightDirection.normalized, Vector3.up);
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.05f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;

            Camera.main.backgroundColor = skyColour;
        }
    }
}
