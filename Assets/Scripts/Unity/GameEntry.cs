using System.IO;
using DesalEra.Unity.Session;
using UnityEngine;
using UnityEngine.Rendering;

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
        [SerializeField] private Vector3 lightDirection = new Vector3(0.55f, -0.75f, 0.35f);
        [SerializeField] private Color skyColour = new Color(0.55f, 0.62f, 0.68f);
        [SerializeField] private Color fogColour = new Color(0.64f, 0.68f, 0.72f);
        [SerializeField] private float fogDensity = 0.024f;
        [SerializeField] private string skyboxFile = "sky/sky_DaySkyHDRI070B.jpg";

        private ThirdPersonCamera _orbit;
        private GameBootstrap _world;
        private PlayerController _player;
        private Ui.UiRoot _ui;
        private bool _hudEventsHooked;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            BuildWorld();
            BuildLighting();
            BuildUi();
        }

        /// <summary>
        /// The interface, built after the world so it can read live state from it.
        ///
        /// It is a component rather than a scene object for the same reason everything else
        /// is: a canvas hierarchy saved into the scene would be a few hundred lines of
        /// unreviewable YAML, in the one file this project refuses to hand-edit.
        /// </summary>
        private void BuildUi()
        {
            var uiGo = new GameObject("Ui");
            uiGo.transform.SetParent(transform, worldPositionStays: false);

            _ui = uiGo.AddComponent<Ui.UiRoot>();
            _ui.Initialise();

            // Layout Apply rebuilds HUD panels — re-wire dock / status after each rebuild.
            _ui.HudRebuilt += WireHudAfterRebuild;

            WireHudBindings();

            // The per-frame part of the HUD lives in its own component so this method stays
            // about wiring rather than about what happens sixty times a second.
            uiGo.AddComponent<Ui.HudPresenter>().Initialise(_ui, _world, _player);
        }

        private void WireHudAfterRebuild()
        {
            string reopen = _ui != null ? _ui.OverlayToReopen : null;
            WireHudBindings();
            var presenter = _ui != null ? _ui.GetComponent<Ui.HudPresenter>() : null;
            presenter?.Initialise(_ui, _world, _player);
            RestoreOverlayAfterRebuild(reopen);
        }

        private void RestoreOverlayAfterRebuild(string id)
        {
            if (_ui == null || string.IsNullOrEmpty(id)) return;
            var presenter = _ui.GetComponent<Ui.HudPresenter>();
            if (presenter != null)
                presenter.ShowPanel(id);
            else
                _ui.ShowOnly(id);
        }

        private void WireHudBindings()
        {
            var ui = _ui;
            var world = _world;
            var player = _player;
            if (ui == null || world == null || player == null) return;

            Ui.InventoryView pack = ui.Screen("pack", () => Ui.InventoryView.Create(ui.Overlay));
            Ui.MaterialPickerView picker = ui.Screen("materials", () => Ui.MaterialPickerView.Create(ui.Overlay));

            pack.Bind(world.Raft.Inventory, PlayerController.Pieces);

            ui.BuildBar.Bind(PlayerController.Pieces);
            ui.BuildBar.PieceChosen += piece =>
            {
                picker.Open(piece, world.Raft.Inventory);
                ui.ShowOnly("materials");
            };

            ui.Status.Declare("sheltered", "有遮蔽", Ui.UiTheme.Accent);
            ui.Status.Declare("storm", "风暴", Ui.UiTheme.Warn);

            // Inventory / status events are subscribed once on the world/player; handlers
            // always read the current ui.BuildBar reference so rebuilds stay wired.
            if (!_hudEventsHooked)
            {
                _hudEventsHooked = true;
                player.StatusChanged += message =>
                {
                    if (_ui == null) return;
                    string notice = Ui.UiCopy.Notice(message);
                    if (!string.IsNullOrEmpty(notice) && !notice.StartsWith("已选择")
                        && !notice.StartsWith("Selected"))
                        _ui.Notice.Show(notice, Ui.UiTheme.TextPrimary);
                    _ui.BuildBar.Refresh(world.Raft.Inventory, player.SelectedPiece);
                    _ui.ResourceStrip?.SetSelected(player.SelectedPiece);
                    _ui.ResourceStrip?.Refresh(world.Raft.Inventory);
                };

                world.Raft.Inventory.Changed += (_, __) =>
                {
                    if (_ui == null) return;
                    _ui.BuildBar.Refresh(world.Raft.Inventory, player.SelectedPiece);
                    _ui.ResourceStrip?.Refresh(world.Raft.Inventory);
                    _ui.GetScreen<Ui.InventoryView>("pack")?.Refresh();
                };
            }

            ui.BuildBar.Refresh(world.Raft.Inventory, player.SelectedPiece);
            ui.ResourceStrip?.SetSelected(player.SelectedPiece);
            ui.ResourceStrip?.Refresh(world.Raft.Inventory);
            pack.Refresh();
        }

        private void BuildWorld()
        {
            var world = new GameObject("World");
            _world = world.AddComponent<GameBootstrap>();

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(_world.transform, worldPositionStays: false);

            _player = playerGo.AddComponent<PlayerController>();
            var avatar = playerGo.AddComponent<PlayerAvatar>();
            var animator = playerGo.AddComponent<PlayerAnimator>();

            // The camera must exist before the player initialises, because movement is
            // camera-relative and would otherwise fall back to a fixed world axis on the
            // first frame.
            Camera camera = CreateCamera();
            _orbit = _world.gameObject.AddComponent<ThirdPersonCamera>();

            // Avatar + idle pose before spawn height: SurfaceHeight uses RootAboveSoleM,
            // which is only meaningful once the skinned idle bounds are available.
            avatar.Initialise(playerGo.transform);
            animator.Initialise();
            avatar.RecalculateSoleOffset();
            _player.Avatar = avatar;
            _player.Animator = animator;
            _player.Initialise(_world, _orbit);
            _world.gameObject.AddComponent<BuildPreview>().Initialise(_world, _player);

            _orbit.Initialise(playerGo.transform, camera);

            // Session recorder lives on the world root so menu / CLI can always find it.
            if (_world.GetComponent<PlaySessionRecorder>() == null)
                _world.gameObject.AddComponent<PlaySessionRecorder>();
#if UNITY_EDITOR
            if (_world.GetComponent<PlayStateOverlay>() == null)
                _world.gameObject.AddComponent<PlayStateOverlay>();
#endif

            _world.Reanalyse();
        }

        private Camera CreateCamera()
        {
            var cameraGo = new GameObject("MainCamera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = cameraFieldOfView;
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 400f;
            // Sea contact foam samples the scene depth at posts and hulls.
            camera.depthTextureMode |= DepthTextureMode.Depth;
            cameraGo.AddComponent<AudioListener>();
            return camera;
        }

        /// <summary>
        /// Skybox + keyed sun + fill light. Flat grey backgrounds erase form; a cloudy
        /// panoramic (CC0 ambientCG DaySkyHDRI068B) gives horizon, ambient bounce and
        /// a reason for metal/wood to catch light differently on each face.
        /// </summary>
        private void BuildLighting()
        {
            Material sky = LoadSkybox(skyboxFile, out Texture skyPanorama);
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 0.95f;
                DynamicGI.UpdateEnvironment();
                // Water must sample the same panorama — SpecCube alone was too muted
                // to read as a sky mirror against our grey body color.
                if (_world != null)
                    _world.BindSkyReflection(skyPanorama, rotationDeg: 120f);
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = skyColour;
                RenderSettings.ambientEquatorColor = skyColour * 0.8f;
                RenderSettings.ambientGroundColor = skyColour * 0.45f;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColour;
            RenderSettings.fogDensity = fogDensity;

            // Soft shadows are what make round posts and beveled planks read as volume;
            // without them the skybox alone cannot sell form.
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 90f;
            QualitySettings.shadowCascades = 2;

            var lightGo = new GameObject("Sun");
            lightGo.transform.SetParent(transform, worldPositionStays: false);
            lightGo.transform.localRotation = Quaternion.LookRotation(-lightDirection.normalized, Vector3.up);
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            // Soft overcast key — hard warm sun made the grey sky and teal sea fight.
            sun.intensity = 1.05f;
            sun.color = new Color(0.92f, 0.94f, 0.98f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;

            // Cool fill from the opposite side so cylinder sides read round instead of
            // silhouetting against a single hard key.
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(transform, worldPositionStays: false);
            fillGo.transform.localRotation = Quaternion.LookRotation(
                new Vector3(-lightDirection.x, -0.2f, -lightDirection.z).normalized, Vector3.up);
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.35f;
            fill.color = new Color(0.72f, 0.80f, 0.90f);
            fill.shadows = LightShadows.None;

            Camera main = Camera.main;
            if (main != null)
            {
                main.clearFlags = sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                main.backgroundColor = skyColour;
                main.allowHDR = true;
            }
        }

        /// <summary>
        /// Loads an equirectangular JPG from StreamingAssets as a Skybox/Panoramic material.
        /// Returns null when the file or shader is missing so lighting can fall back cleanly.
        /// </summary>
        private static Material LoadSkybox(string relativePath, out Texture panorama)
        {
            panorama = null;
            if (string.IsNullOrEmpty(relativePath)) return null;

            string full = Path.Combine(Application.streamingAssetsPath, "textures", relativePath);
            if (!File.Exists(full))
            {
                // Also accept paths that already include the textures/ prefix.
                full = Path.Combine(Application.streamingAssetsPath, relativePath);
            }

            if (!File.Exists(full)) return null;

            Shader shader = Shader.Find("Skybox/Panoramic")
                            ?? Shader.Find("Skybox/Cubemap")
                            ?? Shader.Find("Skybox/6 Sided");
            if (shader == null) return null;

            byte[] bytes = File.ReadAllBytes(full);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!texture.LoadImage(bytes, markNonReadable: false)) return null;

            texture.name = Path.GetFileName(relativePath);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 0;
            panorama = texture;

            var material = new Material(shader) { name = "Sky_DayOvercast" };
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            else if (material.HasProperty("_Tex")) material.SetTexture("_Tex", texture);
            if (material.HasProperty("_Exposure")) material.SetFloat("_Exposure", 1.05f);
            if (material.HasProperty("_Rotation")) material.SetFloat("_Rotation", 120f);
            // 0 = Latitude-Longitude mapping on Skybox/Panoramic.
            if (material.HasProperty("_Mapping")) material.SetFloat("_Mapping", 0f);
            return material;
        }
    }
}
