using System.Collections.Generic;
using CrazyAquarium.Game;
using CrazyAquarium.Structure;
using UnityEngine;

// UnityEngine also defines a physics Joint.
using Joint = CrazyAquarium.Structure.Joint;

namespace CrazyAquarium.Unity
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
        [SerializeField] private float seaSizeM = 240f;
        [SerializeField] private int pickupCount = 14;
        [SerializeField] private float pickupRingMin = 12f;
        [SerializeField] private float pickupRingMax = 34f;
        [SerializeField] private float pickupRespawnSeconds = 45f;

        [Header("Simulation")]
        [Tooltip("Real seconds per simulated minute. Drives survival pressure.")]
        [SerializeField] private float realSecondsPerGameMinute = 6f;

        [SerializeField] private float stormIntervalSeconds = 75f;
        [SerializeField] private float calmWindKnPerM = 1.2f;
        [SerializeField] private float stormWindKnPerM = 14f;

        public RaftState Raft { get; private set; }
        public MaterialLibrary Materials { get; private set; }

        private readonly List<GameObject> _raftObjects = new List<GameObject>();
        private readonly List<Pickup> _pickups = new List<Pickup>();
        private readonly Dictionary<int, GameObject> _memberObjects = new Dictionary<int, GameObject>();

        private Mesh _columnMesh;
        private Mesh _pickupMesh;
        private Material _waterMaterial;

        private float _elapsedRealSeconds;
        private float _nextStormAt;
        private bool _stormActive;

        /// <summary>Set by the storm cycle, read by the player controller for feedback.</summary>
        public bool IsStormActive => _stormActive;

        public float SurvivalMinutesPerRealSecond => 1f / Mathf.Max(realSecondsPerGameMinute, 0.1f);

        private void Awake()
        {
            Raft = new RaftState();
            Materials = gameObject.AddComponent<MaterialLibrary>();
            _columnMesh = MeshFactory.UnitColumn();
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
            var sea = new GameObject("Sea");
            sea.transform.SetParent(transform, worldPositionStays: false);
            sea.transform.localPosition = new Vector3(0f, RaftState.WaterLevelY, 0f);

            var filter = sea.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshFactory.WaterPlane(seaSizeM, "SeaPlane");

            var renderer = sea.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _waterMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private Material BuildWaterMaterial()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                        ?? Shader.Find("Standard")
                                        ?? Shader.Find("Legacy Shaders/Diffuse"))
            {
                name = "Mat_Sea",
                color = new Color(0.09f, 0.20f, 0.24f, 1f)
            };

            // A hint of specular so the swell catches light, but nothing that reads as
            // a simulated ocean. Water dynamics is its own task.
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.72f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.72f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.15f);
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

            foreach (Member member in Raft.Gravity.Members)
            {
                GameObject go = BuildMember(member);
                _raftObjects.Add(go);
                if (go != null) _memberObjects[member.Id] = go;
            }
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

            var go = new GameObject($"member_{member.Id}_{member.Material}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta / length);
            go.transform.localScale = new Vector3(thickness, length, thickness);

            go.AddComponent<MeshFilter>().sharedMesh = _columnMesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.Get(MaterialKeyFor(member));
            renderer.sharedMaterial.color = StressColor(member);

            return go;
        }

        private static string MaterialKeyFor(Member member)
        {
            switch (member.Material)
            {
                case MaterialKind.Steel: return "rust";
                case MaterialKind.Concrete: return "concrete";
                case MaterialKind.Plastic: return "rust";
                default: return "timber";
            }
        }

        /// <summary>
        /// Green to red by utilization, mirroring the palette the structure view uses.
        /// Shared materials mean the tint has to be applied per renderer, which is
        /// cheap here because the raft is tens of members, not thousands.
        /// </summary>
        private static Color StressColor(Member member)
        {
            float t = Mathf.Clamp01(member.Utilization);
            if (t < 0.6f) return Color.Lerp(Color.white, new Color(1f, 0.85f, 0.5f), t / 0.6f);
            return Color.Lerp(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.45f, 0.4f), (t - 0.6f) / 0.4f);
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
            return new Vector3(flat.x, RaftState.WaterLevelY + 0.35f, flat.z);
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
            renderer.sharedMaterial = Materials.Get(PickupKeyFor(kind));
            renderer.material.color = PickupTint(kind);

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

            UpdateStorm(deltaReal);
        }

        private void UpdateStorm(float deltaReal)
        {
            if (_stormActive && _elapsedRealSeconds >= _nextStormAt + 12f) EndStorm();
            if (!_stormActive && _elapsedRealSeconds >= _nextStormAt) BeginStorm();
        }

        private void BeginStorm()
        {
            _stormActive = true;
            Raft.WindLoadKnPerM = stormWindKnPerM;
            _nextStormAt = _elapsedRealSeconds + stormIntervalSeconds;
        }

        private void EndStorm()
        {
            _stormActive = false;
            Raft.WindLoadKnPerM = calmWindKnPerM;
        }

        /// <summary>
        /// Runs both analyses and refreshes the visuals. Called by the storm cycle and
        /// after any build, never every frame: the truss solve is a dense matrix
        /// decomposition and has no business running at 60 Hz.
        /// </summary>
        public void Reanalyse()
        {
            if (Raft == null) return;

            TrussReport wind = Raft.SolveWind();
            SolveReport buoyancy = Raft.SolveBuoyancy();

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
            if (_waterMaterial != null) SafeDestroy(_waterMaterial);
            if (_columnMesh != null) SafeDestroy(_columnMesh);
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
