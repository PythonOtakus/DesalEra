using System.Collections.Generic;
using DesalEra.Game;
using DesalEra.Structure;
using UnityEngine;

// UnityEngine also defines a physics Joint.
using Joint = DesalEra.Structure.Joint;

namespace DesalEra.Unity
{
    /// <summary>
    /// Builds the entire playable scene at runtime: raft, sea, salvage pickups, player
    /// and camera.
    ///
    /// Nothing here lives in a .unity file. That is a deliberate constraint. A Unity
    /// scene is thousands of lines of GUID-linked YAML, which is effectively uneditable
    /// by an agent and unpleasant to review by hand. Assembling the world from code
    /// means the scene is diffable, reviewable and reproducible, and it is the same
    /// reason the simulation lives in its own engine-free assembly.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Tuning")]
        // Large enough that fog eats the rim before the circular edge can read as a seam.
        [SerializeField] private float farSeaSizeM = 900f;
        [SerializeField] private int farSeaSegments = 420;
        [SerializeField] private int pickupCount = 14;
        [SerializeField] private float pickupRingMin = 12f;
        [SerializeField] private float pickupRingMax = 34f;
        [SerializeField] private float pickupRespawnSeconds = 45f;

        [Header("Simulation")]
        [Tooltip("Real seconds per simulated minute. Drives survival pressure.")]
        [SerializeField] private float realSecondsPerGameMinute = 6f;

        [SerializeField] private float stormIntervalSeconds = 75f;
        [SerializeField] private float stormDurationSeconds = 12f;
        [SerializeField] private float calmWindKnPerM = 1.2f;
        [SerializeField] private float stormWindKnPerM = 14f;

        public RaftState Raft { get; private set; }
        public MaterialLibrary Materials { get; private set; }

        private readonly List<GameObject> _raftObjects = new List<GameObject>();
        private readonly List<Pickup> _pickups = new List<Pickup>();
        private readonly Dictionary<int, GameObject> _memberObjects = new Dictionary<int, GameObject>();

        /// <summary>
        /// One mesh per distinct profile and size. The raft uses a handful of lengths, so
        /// this collapses many members onto shared meshes while keeping metre-scaled UVs.
        /// </summary>
        private readonly Dictionary<(int, int, int, int), Mesh> _memberMeshes =
            new Dictionary<(int, int, int, int), Mesh>();

        private Mesh _pickupMesh;
        private Material _waterMaterial;
        private Transform _raftRoot;
        private RaftMotion _raftMotion;

        /// <summary>Live sea material — lighting binds the panoramic sky into this.</summary>
        public Material WaterMaterial => _waterMaterial;

        /// <summary>Moving deck frame — members parent here; player samples heave from it.</summary>
        public RaftMotion RaftMotion => _raftMotion;

        private float _elapsedRealSeconds;
        private float _nextStormAt;
        private bool _stormActive;

        /// <summary>Set by the storm cycle, read by the player controller for feedback.</summary>
        public bool IsStormActive => _stormActive;

        /// <summary>Written each frame by the player controller; drives weather damage and the HUD chip.</summary>
        public bool PlayerSheltered { get; set; }

        public float SurvivalMinutesPerRealSecond => 1f / Mathf.Max(realSecondsPerGameMinute, 0.1f);

        private void Awake()
        {
            Initialise();
        }

        /// <summary>
        /// Builds the non-visual half of the world: the solver state, the material
        /// library and the shared meshes. Public so an EditMode test can drive it, since
        /// tests do not get an Awake.
        /// </summary>
        public void Initialise()
        {
            Raft = new RaftState();
            Materials = gameObject.AddComponent<MaterialLibrary>();
            _pickupMesh = MeshFactory.Pickup();
            _waterMaterial = BuildWaterMaterial();
        }

        private void Start()
        {
            BuildSea();
            BuildRaft();
            BuildPickups();
            _nextStormAt = stormIntervalSeconds;
        }

        // --- world ---

        private void BuildSea()
        {
            // One continuous disc — a near/far ring left a square seam (hole gap +
            // mismatched distance-fade amplitudes) that read as a hard join line.
            // Vertex budget stays on a single grid; distance fade softens far swells.
            var sea = new GameObject("Sea");
            sea.transform.SetParent(transform, worldPositionStays: false);
            sea.transform.localPosition = new Vector3(0f, RaftState.WaterLevelY, 0f);

            var filter = sea.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshFactory.WaterPlane(farSeaSizeM, "SeaMesh", farSeaSegments);

            var renderer = sea.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _waterMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            sea.AddComponent<SeaSurface>().Initialise(_waterMaterial, this);
        }

        /// <summary>
        /// Feeds the same equirectangular sky the skybox uses, so Fresnel reflections
        /// show real clouds instead of a flat procedural grey that matched the body.
        /// </summary>
        public void BindSkyReflection(Texture skyPanorama, float rotationDeg = 120f)
        {
            if (_waterMaterial == null || skyPanorama == null) return;
            if (_waterMaterial.HasProperty("_SkyTex"))
                _waterMaterial.SetTexture("_SkyTex", skyPanorama);
            if (_waterMaterial.HasProperty("_SkyRotation"))
                _waterMaterial.SetFloat("_SkyRotation", rotationDeg);
        }

        private Material BuildWaterMaterial()
        {
            // Prefer the Gerstner sea shader; fall back to a shiny lit plane if it failed
            // to import so the greybox never blacks out.
            Shader sea = Shader.Find("DesalEra/SeaWater");
            if (sea != null)
            {
                var water = new Material(sea) { name = "Mat_Sea" };
                // Opaque-enough body so skybox cannot bleed through and kill Fresnel contrast.
                water.SetColor("_ShallowColor", new Color(0.08f, 0.24f, 0.30f, 0.96f));
                water.SetColor("_DeepColor", new Color(0.02f, 0.07f, 0.12f, 0.99f));
                water.SetColor("_HorizonColor", new Color(0.78f, 0.80f, 0.82f, 1f));
                water.SetColor("_SkyZenith", new Color(0.88f, 0.89f, 0.90f, 1f));
                water.SetColor("_FoamColor", new Color(0.88f, 0.90f, 0.91f, 0.4f));
                water.SetFloat("_FresnelPower", 3.2f);
                water.SetFloat("_FresnelBias", 0.01f);
                water.SetFloat("_ReflectionStrength", 1.0f);
                water.SetFloat("_ReflectionBlur", 1.0f);
                water.SetFloat("_Detail", 0.8f);
                water.SetFloat("_NormalStrength", 1.3f);
                water.SetFloat("_FoamAmount", 0.22f);
                water.SetFloat("_ContactFoam", 0.9f);
                water.SetFloat("_SkyRotation", 120f);
                water.SetFloat("_DistanceFade", 80f);
                water.SetFloat("_DistanceFadeEnd", 220f);
                SeaWave.Amplitude = 1.05f;
                SeaWave.Speed = 1f;
                SeaWave.ApplyToMaterial(water);
                return water;
            }

            var material = new Material(Shader.Find("Standard")
                                        ?? Shader.Find("Legacy Shaders/Diffuse"))
            {
                name = "Mat_Sea",
                color = new Color(0.14f, 0.28f, 0.34f, 1f)
            };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.85f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.85f);
            return material;
        }

        // --- raft ---

        /// <summary>Rebuilds every visual member from the solver graph.</summary>
        public void BuildRaft()
        {
            foreach (GameObject go in _raftObjects)
            {
                if (go != null) SafeDestroy(go);
            }
            _raftObjects.Clear();
            _memberObjects.Clear();

            if (_raftRoot == null)
            {
                var rootGo = new GameObject("RaftRoot");
                rootGo.transform.SetParent(transform, worldPositionStays: false);
                _raftRoot = rootGo.transform;
                _raftMotion = rootGo.AddComponent<RaftMotion>();
            }

            foreach (Member member in Raft.Gravity.Members)
            {
                GameObject go = BuildMember(member);
                _raftObjects.Add(go);
                if (go != null) _memberObjects[member.Id] = go;
            }

            foreach (PlacementKey key in Raft.Placements)
            {
                if (!Raft.TryGetPlacedPiece(key, out BuildPiece piece)) continue;
                GameObject visual = BuildPlacementVisual(piece, key, null);
                if (visual != null) _raftObjects.Add(visual);
            }

            GameObject floor = BuildDeckFloor();
            if (floor != null) _raftObjects.Add(floor);
        }

        private Mesh _floorMesh;

        /// <summary>
        /// Boards over the deck footprint. The survivor can stand anywhere inside the
        /// footprint (see <see cref="RaftState.IsOverDeck"/>), so drawing exactly that
        /// outline keeps what looks walkable and what is walkable the same; the opening
        /// raft is otherwise a bare frame with the survivor standing on open water.
        /// </summary>
        private GameObject BuildDeckFloor()
        {
            if (_floorMesh != null) SafeDestroy(_floorMesh);
            _floorMesh = null;

            List<Vector2> outline = Raft.DeckFootprint();
            if (outline.Count < 3) return null;

            const float thickness = 0.06f;
            // A centimetre under the beam tops, so the boards meet the frame without
            // z-fighting along the strip where they overlap.
            float top = PlayerController.DeckSurfaceLocalY - 0.01f;

            _floorMesh = MeshFactory.Floor(outline.ToArray(), thickness, Materials.TilingMetresFor("deck"));
            var go = new GameObject("deck_floor");
            go.transform.SetParent(RaftRoot, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0f, top - thickness * 0.5f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = _floorMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Materials.Get("deck");
            return go;
        }

        /// <summary>The raft root members and piece visuals hang from, so they ride the swell.</summary>
        public Transform RaftRoot => _raftRoot != null ? _raftRoot : transform;

        /// <summary>Pieces drawn as a whole rather than member by member.</summary>
        public static bool HasPlacementVisual(string pieceName) =>
            pieceName == "Roof" || pieceName == "Wall" || pieceName == "Stairs";

        /// <summary>
        /// Draws a roof slab, wall panel or flight of stairs at a placement. With a
        /// material override it draws a build-preview ghost instead. Returns null for
        /// pieces that are drawn as plain members.
        /// </summary>
        public GameObject BuildPlacementVisual(BuildPiece piece, PlacementKey key, Material overrideMaterial)
        {
            switch (piece.Name)
            {
                case "Roof": return BuildRoofSlab(key, overrideMaterial);
                case "Wall": return BuildWallPanel(key, overrideMaterial);
                case "Stairs": return BuildStairs(key, overrideMaterial);
                default: return null;
            }
        }

        private GameObject BuildRoofSlab(PlacementKey roof, Material overrideMaterial)
        {
            const float overhang = 0.25f;
            float thickness = RaftState.FloorTopAboveJointM - 0.1f;
            float span = RaftState.CellSize + overhang * 2f;

            Vector3 sw = RaftState.WorldOf(roof.Cell, roof.Level);
            // Sits on the column tops rather than through them.
            Vector3 centre = sw + new Vector3(RaftState.CellSize * 0.5f, thickness * 0.5f + 0.1f, RaftState.CellSize * 0.5f);

            // Slab local Z (thickness) → world up, local Y (length) → world +X.
            return SlabObject($"roof_{roof.Cell.x}_{roof.Cell.y}_L{roof.Level}", RaftRoot, centre,
                Quaternion.LookRotation(Vector3.up, Vector3.right), span, thickness, span, "deck", overrideMaterial);
        }

        private GameObject BuildWallPanel(PlacementKey wall, Material overrideMaterial)
        {
            const float thickness = 0.1f;
            Vector3[] c = RaftState.WallCorners(wall);
            Vector3 along = (c[1] - c[0]).normalized;
            Vector3 normal = Vector3.Cross(along, Vector3.up).normalized;
            Vector3 centre = (c[0] + c[3]) * 0.5f;

            // Local Y (length) → up, local Z (thickness) → the panel normal.
            return SlabObject($"wall_{wall.Cell.x}_{wall.Cell.y}_L{wall.Level}_{wall.Socket}", RaftRoot, centre,
                Quaternion.LookRotation(normal, Vector3.up),
                RaftState.CellSize - 0.2f, thickness, RaftState.StoreyHeightM - 0.1f, "timber", overrideMaterial);
        }

        private GameObject BuildStairs(PlacementKey stairs, Material overrideMaterial)
        {
            const int steps = 6;
            const float treadThickness = 0.12f;
            Vector3 foot = RaftState.WorldOf(stairs.Cell, stairs.Level);
            Vector3 head = RaftState.WorldOf(stairs.Cell + RaftState.FacingStep(RaftState.FacingOf(stairs)), stairs.Level + 1);
            Vector3 run = head - foot;
            Vector3 flat = new Vector3(run.x, 0f, run.z).normalized;
            Vector3 incline = run.normalized;
            Vector3 lateral = Vector3.Cross(Vector3.up, flat).normalized;
            Vector3 normal = Vector3.Cross(lateral, incline).normalized;

            var root = new GameObject($"stairs_{stairs.Cell.x}_{stairs.Cell.y}_L{stairs.Level}_{stairs.Socket}");
            root.transform.SetParent(RaftRoot, worldPositionStays: false);

            float width = RaftState.StairHalfWidthM * 2f;
            float depth = new Vector3(run.x, 0f, run.z).magnitude / steps + 0.05f;
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 0.5f) / steps;
                Vector3 centre = foot + run * t + Vector3.up * (RaftState.StairTreadAboveLineM - treadThickness * 0.5f);
                SlabObject($"tread_{i}", root.transform, centre, Quaternion.LookRotation(Vector3.up, flat),
                    width, treadThickness, depth, "deck", overrideMaterial);
            }

            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 centre = (foot + head) * 0.5f + lateral * (side * (RaftState.StairHalfWidthM - 0.04f))
                                 - normal * 0.08f;
                SlabObject("stringer", root.transform, centre, Quaternion.LookRotation(normal, incline),
                    0.08f, 0.28f, run.magnitude, "timber", overrideMaterial);
            }
            return root;
        }

        private GameObject SlabObject(string name, Transform parent, Vector3 localPosition, Quaternion localRotation,
                                      float width, float thickness, float length, string materialKey,
                                      Material overrideMaterial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;

            float tile = Materials.TilingMetresFor(materialKey);
            go.AddComponent<MeshFilter>().sharedMesh = SlabMesh(width, thickness, length, tile);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = overrideMaterial != null ? overrideMaterial : Materials.Get(materialKey);
            if (overrideMaterial != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return go;
        }

        private readonly Dictionary<(int, int, int, int), Mesh> _slabMeshes = new Dictionary<(int, int, int, int), Mesh>();

        private Mesh SlabMesh(float width, float thickness, float length, float tile)
        {
            var key = (Mathf.RoundToInt(width * 1000f), Mathf.RoundToInt(thickness * 1000f),
                       Mathf.RoundToInt(length * 1000f), Mathf.RoundToInt(tile * 100f));
            if (!_slabMeshes.TryGetValue(key, out Mesh mesh) || mesh == null)
            {
                mesh = MeshFactory.Slab(width, thickness, length, tile);
                _slabMeshes[key] = mesh;
            }
            return mesh;
        }

        /// <summary>
        /// Mesh for a plain member drawn at a given thickness, shared with the build
        /// preview so a ghost has the same silhouette as the piece it previews.
        /// </summary>
        public (Mesh mesh, Quaternion rotation) MemberShape(string pieceName, Vector3 from, Vector3 to, float thickness)
        {
            MeshFactory.Profile profile = MeshFactory.ProfileFor(pieceName);
            Vector3 delta = to - from;
            float length = Mathf.Max(delta.magnitude, 1e-3f);
            return (MemberMesh(profile, thickness, length, 1f), MemberRotation(profile, delta / length));
        }

        private GameObject BuildMember(Member member)
        {
            if (member.IsFailed) return null;

            Joint a = Raft.Gravity.JointAt(member.JointA);
            Joint b = Raft.Gravity.JointAt(member.JointB);
            Vector3 from = a.Position;
            Vector3 to = b.Position;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 1e-3f) return null;

            // Visual thickness tracks the real cross-section, so a heavy column looks
            // heavy. Clamped, because a 0.02 m2 brace drawn to scale would be a wire.
            float thickness = Mathf.Clamp(Mathf.Sqrt(member.CrossSectionAreaM2) * 1.15f, 0.14f, 0.7f);

            Raft.TryGetPiece(member.Id, out BuildPiece piece);
            string pieceName = piece?.Name;
            // Wall panels and stairs replace their member entirely; a roof keeps its
            // diagonals visible as joists under the slab.
            if (pieceName == "Wall" || pieceName == "Stairs") return null;
            MeshFactory.Profile profile = MeshFactory.ProfileFor(pieceName);

            // Visual bulk is larger than the structural shell area: a pontoon's sealed
            // volume is a drum, not a wire, and a still has to read as a tank at a glance.
            if (profile == MeshFactory.Profile.Drum)
                thickness = Mathf.Clamp(thickness * 2.4f, 0.45f, 1.2f);
            else if (profile == MeshFactory.Profile.Still)
                thickness = Mathf.Clamp(thickness * 1.8f, 0.35f, 1.0f);
            else if (profile == MeshFactory.Profile.Post)
                thickness = Mathf.Clamp(thickness * 1.25f, 0.2f, 0.75f);

            string materialKey = MaterialKeyFor(member, pieceName);
            float tileMetres = Materials.TilingMetresFor(materialKey);

            var go = new GameObject($"member_{member.Id}_{member.Material}");
            // Parent under the moving raft root so heave/tilt carries every post and beam.
            go.transform.SetParent(_raftRoot != null ? _raftRoot : transform, worldPositionStays: false);
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = MemberRotation(profile, delta / length);
            // No scale: the mesh is already the member's real size, which is what lets its
            // UVs be laid out in metres.

            go.AddComponent<MeshFilter>().sharedMesh = MemberMesh(profile, thickness, length, tileMetres);

            var renderer = go.AddComponent<MeshRenderer>();
            Material material = Materials.Get(materialKey);
            renderer.sharedMaterial = material;

            // The tint goes through a property block, not the material. Assigning
            // material.color here would write into the one instance the library hands
            // to every member of the same kind, so all of them would end up showing the
            // last member's stress and the readout would be meaningless.
            var block = new MaterialPropertyBlock();
            block.SetColor(ColorProperty(material), StressColor(member));
            renderer.SetPropertyBlock(block);

            return go;
        }

        /// <summary>
        /// Orients a member mesh so its extrusion (local Y) follows the joint axis.
        /// Deck planks also roll so their thin local-Z axis stays world-up — otherwise
        /// FromToRotation alone can stand the wide face vertically and the survivor
        /// looks suspended under the board's upper edge.
        /// </summary>
        private static Quaternion MemberRotation(MeshFactory.Profile profile, Vector3 direction)
        {
            if (profile != MeshFactory.Profile.Plank)
                return Quaternion.FromToRotation(Vector3.up, direction);

            Vector3 along = direction.normalized;
            Vector3 thin = Vector3.up;
            // Degenerate when a "deck" somehow stands vertical: fall back to plain align.
            if (Mathf.Abs(Vector3.Dot(along, thin)) > 0.95f)
                return Quaternion.FromToRotation(Vector3.up, along);

            Vector3 wide = Vector3.Cross(along, thin).normalized;
            thin = Vector3.Cross(wide, along).normalized;
            // Mesh axes: local X = width, local Y = length, local Z = thickness.
            return Quaternion.LookRotation(thin, along);
        }

        /// <summary>The tint slot for whichever lit shader is in use.</summary>
        private static string ColorProperty(Material material)
        {
            if (material == null) return "_Color";
            if (material.HasProperty("_BaseColor")) return "_BaseColor";
            return "_Color";
        }

        /// <summary>
        /// Mesh for this profile and size, built once and reused. Sizes are rounded into
        /// the cache key because the solver hands out floats that differ in the last bits
        /// for what is visually the same member.
        /// </summary>
        private Mesh MemberMesh(MeshFactory.Profile profile, float thickness, float length, float tileMetres)
        {
            var key = ((int)profile, Mathf.RoundToInt(thickness * 1000f),
                       Mathf.RoundToInt(length * 1000f), Mathf.RoundToInt(tileMetres * 100f));

            if (!_memberMeshes.TryGetValue(key, out Mesh mesh) || mesh == null)
            {
                mesh = MeshFactory.ForProfile(profile, thickness, length, tileMetres);
                _memberMeshes[key] = mesh;
            }

            return mesh;
        }

        private static string MaterialKeyFor(Member member, string pieceName)
        {
            // Decks use floorboard grain; posts use timber end-grain friendly maps.
            if (pieceName == "Deck") return member.Material == MaterialKind.Steel ? "rust"
                : member.Material == MaterialKind.Concrete ? "concrete" : "deck";

            switch (member.Material)
            {
                case MaterialKind.Steel: return "rust";
                case MaterialKind.Concrete: return "concrete";
                case MaterialKind.Plastic: return "rust";
                default: return "timber";
            }
        }

        /// <summary>
        /// Soft stress tint over the albedo. A hard colour wipe killed normal-map
        /// form; keeping most of the multiply near white lets wood/rust still read
        /// in relief while overload still warns.
        /// </summary>
        private static Color StressColor(Member member)
        {
            float t = Mathf.Clamp01(member.Utilization);
            if (t < 0.55f) return Color.Lerp(Color.white, new Color(1f, 0.92f, 0.78f), t * 0.45f);
            if (t < 0.85f) return Color.Lerp(new Color(1f, 0.92f, 0.78f), new Color(1f, 0.72f, 0.45f),
                                             (t - 0.55f) / 0.3f);
            return Color.Lerp(new Color(1f, 0.72f, 0.45f), new Color(1f, 0.48f, 0.42f),
                              (t - 0.85f) / 0.15f);
        }

        // --- pickups ---

        private void BuildPickups()
        {
            for (int i = 0; i < pickupCount; i++)
            {
                Vector3 position = PickupPosition(i);
                _pickups.Add(SpawnPickup(position, RandomResource(i)));
            }
        }

        private Vector3 PickupPosition(int index)
        {
            // Deterministic ring placement so the opening layout is the same every run
            // and a player can be told "drift is to the north-east".
            float angle = index * 137.5f * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(pickupRingMin, pickupRingMax, (index % 5) / 4f);
            var flat = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            return new Vector3(flat.x, RaftState.WaterLevelY + 0.15f, flat.z);
        }

        private static ResourceKind RandomResource(int index)
        {
            switch (index % 4)
            {
                case 0: return ResourceKind.Plank;
                case 1: return ResourceKind.Scrap;
                case 2: return ResourceKind.Metal;
                default: return index % 8 == 3 ? ResourceKind.Water : ResourceKind.Food;
            }
        }

        private Pickup SpawnPickup(Vector3 position, ResourceKind kind)
        {
            var go = new GameObject($"pickup_{kind}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * 0.8f;

            go.AddComponent<MeshFilter>().sharedMesh = _pickupMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            Material material = Materials.Get(PickupKeyFor(kind));
            renderer.sharedMaterial = material;

            // A property block rather than renderer.material: the latter clones a material
            // per pickup, and pickups respawn, so the clones pile up.
            var block = new MaterialPropertyBlock();
            block.SetColor(ColorProperty(material), PickupTint(kind));
            renderer.SetPropertyBlock(block);

            var pickup = go.AddComponent<Pickup>();
            pickup.Initialise(this, kind, position);
            return pickup;
        }

        private static string PickupKeyFor(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Plank:
                case ResourceKind.Scrap: return "timber";
                case ResourceKind.Metal: return "rust";
                default: return "plaster";
            }
        }

        private static Color PickupTint(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Water: return new Color(0.55f, 0.85f, 1f);
                case ResourceKind.Food: return new Color(1f, 0.78f, 0.5f);
                case ResourceKind.Metal: return new Color(0.95f, 0.75f, 0.6f);
                default: return Color.white;
            }
        }

        /// <summary>Called by a pickup when collected. Returns false if it was empty.</summary>
        public bool Collect(Pickup pickup)
        {
            if (pickup == null) return false;
            pickup.Collect();
            Raft.Inventory.Add(pickup.Kind, pickup.Amount);
            return true;
        }

        public void Respawn(Pickup pickup) => SpawnPickup(pickup.Origin, pickup.Kind);

        public void RespawnAllAfter(float seconds)
        {
            foreach (Pickup p in _pickups) p.ScheduleRespawn(seconds);
        }

        // --- simulation tick ---

        private void Update()
        {
            if (Raft == null) return;

            float deltaReal = Time.deltaTime;
            _elapsedRealSeconds += deltaReal;

            // Survival runs on game minutes, not real seconds, so the pace is tunable
            // in one place and does not drift with frame rate.
            float gameMinutes = deltaReal * SurvivalMinutesPerRealSecond;
            bool exerting = Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;
            Raft.Survival.Advance(gameMinutes, exerting);
            Raft.Survival.ApplyWeather(gameMinutes, _stormActive, PlayerSheltered);

            UpdateStorm(deltaReal);
        }

        private void UpdateStorm(float deltaReal)
        {
            // The end has its own deadline. Measuring it from the next storm's start made
            // each storm last a whole interval and the next begin the frame it ended.
            if (_stormActive && _elapsedRealSeconds >= _stormEndsAt) EndStorm();
            else if (!_stormActive && _elapsedRealSeconds >= _nextStormAt) BeginStorm();
        }

        private float _stormEndsAt;

        private void BeginStorm()
        {
            _stormActive = true;
            Raft.WindLoadKnPerM = stormWindKnPerM;
            _stormEndsAt = _elapsedRealSeconds + stormDurationSeconds;
            _nextStormAt = _elapsedRealSeconds + stormIntervalSeconds;
            // The storm is the load case the frame exists to survive, so it is solved
            // the moment it arrives; otherwise nothing could break until the next build.
            Reanalyse();
        }

        private void EndStorm()
        {
            _stormActive = false;
            Raft.WindLoadKnPerM = calmWindKnPerM;
            Reanalyse();
        }

        /// <summary>Starts or ends a storm now, for the CLI and tests. The regular cycle carries on from here.</summary>
        public void SetStorm(bool active)
        {
            if (active == _stormActive) return;
            if (active) BeginStorm();
            else
            {
                EndStorm();
                _nextStormAt = _elapsedRealSeconds + stormIntervalSeconds;
            }
        }

        /// <summary>Raised with a player-facing message when part of the structure falls.</summary>
        public event System.Action<string> StructureNotice;

        /// <summary>
        /// Runs both analyses and refreshes the visuals. Called by the storm cycle and
        /// after any build, never every frame: the truss solve is a dense matrix
        /// decomposition and has no business running at 60 Hz.
        /// </summary>
        public void Reanalyse()
        {
            if (Raft == null) return;

            TrussReport wind = Raft.SolveWind();
            int fallen = Raft.CollapseUnsupported();
            SolveReport buoyancy = Raft.SolveBuoyancy();
            if (fallen > 0) StructureNotice?.Invoke($"{fallen} pieces collapsed.");

            BuildRaft();
            LastWind = wind;
            LastBuoyancy = buoyancy;
        }

        public TrussReport LastWind { get; private set; }
        public SolveReport LastBuoyancy { get; private set; }

        private void OnDestroy()
        {
            foreach (GameObject go in _raftObjects)
            {
                if (go != null) SafeDestroy(go);
            }

            foreach (Mesh mesh in _memberMeshes.Values)
            {
                if (mesh != null) SafeDestroy(mesh);
            }
            _memberMeshes.Clear();

            foreach (Mesh mesh in _slabMeshes.Values)
            {
                if (mesh != null) SafeDestroy(mesh);
            }
            _slabMeshes.Clear();

            if (_floorMesh != null) SafeDestroy(_floorMesh);
            if (_waterMaterial != null) SafeDestroy(_waterMaterial);
            if (_pickupMesh != null) SafeDestroy(_pickupMesh);
        }

        internal static void SafeDestroy(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
