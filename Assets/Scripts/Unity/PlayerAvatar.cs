using DesalEra.Game;
using UnityEngine;

namespace DesalEra.Unity
{
    /// <summary>
    /// Attaches the survivor model to the player.
    ///
    /// The model is loaded at runtime from Resources rather than referenced in a scene,
    /// for the same reason everything else here is built in code: a scene file is
    /// GUID-linked YAML that cannot be edited reliably by a person or an agent, and
    /// keeping the world diffable has paid for itself repeatedly.
    ///
    /// Scale and orientation are corrected at load time. The generated FBX is a 1.7 m
    /// standing figure in metres facing -Z, which does not match Unity's convention of
    /// a character facing +Z, and it arrives in a T-pose that reads as a mannequin
    /// rather than a survivor. Both are handled here so the import settings stay
    /// generic and the model can be regenerated without touching this file.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class PlayerAvatar : MonoBehaviour
    {
        private const string ResourcePath = "Survivor";

        [Header("Placement")]
        [Tooltip("Scales the loaded model. 1.0 keeps the generated figure's own height.")]
        [SerializeField] private float modelScale = 1f;

        [Tooltip("Extra yaw applied after LookRotation. This rig's bones face +Z, so leave at 0.")]
        [SerializeField] private float yawOffset = 0f;

        [Tooltip("Offset from the player's feet to the model's own origin.")]
        [SerializeField] private Vector3 positionOffset = new Vector3(0f, 0f, 0f);

        private GameObject _model;
        private SkinnedMeshRenderer _renderer;
        private Transform _playerTransform;
        private Transform _chest;

        /// <summary>
        /// When true the model is shifted horizontally so the chest stays over the
        /// player root. Swim clips lay the body out ~1.7 m ahead of the clip origin;
        /// without this, swapping SwimForward → SwimIdle (upright) snapped the visible
        /// body back toward the root and read as the survivor swimming backwards.
        /// </summary>
        public bool AnchorChestToRoot { get; set; }

        /// <summary>Height in metres, measured from the loaded mesh.</summary>
        public float ModelHeightM { get; private set; }

        /// <summary>
        /// How far above the soles the player root must sit so the mesh rests on the deck.
        /// Measured from skinned bounds after the idle pose is applied.
        /// </summary>
        public float RootAboveSoleM { get; private set; } = 0.05f;

        public bool IsLoaded => _model != null;

        /// <summary>
        /// Flattened world direction the survivor is facing. Matches headfront / model
        /// forward for this rig (not the inverse -Z guess that used to flip travel).
        /// </summary>
        public Vector3 FacingDirection
        {
            get
            {
                if (_model == null) return Vector3.forward;
                Vector3 f = _model.transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
            }
        }

        /// <summary>
        /// The instantiated model's own root. Legacy Animation resolves its curve paths
        /// relative to the component's GameObject, and the clip paths match the FBX root,
        /// so the animation component has to live here rather than on the player.
        /// </summary>
        public GameObject ModelRoot => _model;

        public void Initialise(Transform playerTransform)
        {
            _playerTransform = playerTransform;

            _model = Resources.Load<GameObject>(ResourcePath);
            if (_model == null)
            {
                Debug.LogWarning($"[DesalEra] survivor model not found at Resources/{ResourcePath}; " +
                                 "the player will be invisible but the game stays playable");
                return;
            }

            var instance = Object.Instantiate(_model, transform);
            instance.name = "SurvivorAvatar";

            // This rig's bones and headfront already face Unity +Z. An old 180° yaw
            // made the mesh look opposite the travel direction while the controller
            // still moved correctly — the classic "facing ≠ move" bug.
            instance.transform.localRotation = Quaternion.Euler(0f, yawOffset, 0f);
            instance.transform.localPosition = positionOffset;
            instance.transform.localScale = Vector3.one * modelScale;

            _model = instance;
            _renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Spine02") { _chest = t; break; }
                if (t.name == "Spine01" && _chest == null) _chest = t;
            }
            ApplyMaterial(_renderer);

            ModelHeightM = MeasureHeight(instance, _renderer, modelScale);
            RecalculateSoleOffset();
            Debug.Log($"[DesalEra] survivor loaded, skeleton {ModelHeightM:F2} m " +
                      $"(sole to head joint), mesh bounds {_renderer.bounds.size.y:F2} m " +
                      $"(includes hair and footwear), rootAboveSole {RootAboveSoleM:F3} m");
        }

        /// <summary>
        /// Re-measures how far the root sits above the mesh soles. Call after the
        /// idle clip has been applied so the pose matches what the player sees.
        /// </summary>
        public void RecalculateSoleOffset()
        {
            if (_model == null || _renderer == null) return;

            // Force the skinned mesh to update bounds for the current pose.
            _renderer.updateWhenOffscreen = true;
            float soleY = _renderer.bounds.min.y;
            float rootY = transform.position.y;
            float above = rootY - soleY;
            // Guard against a degenerate bounds read before the first skin update.
            RootAboveSoleM = above > 0.01f && above < 0.8f ? above : 0.05f;
        }

        /// <summary>
        /// Sole-to-head-joint height in metres, measured from the rig rather than from the
        /// mesh bounds.
        ///
        /// The two measurements are not interchangeable and neither is wrong on its own.
        /// Renderer bounds enclose whatever the mesh currently occupies, so this figure's
        /// bounds read roughly 2.15 m: about 0.5 m of that is hair standing above the head
        /// joint and the rest is sole below the ankle bone. Bounds are also pose
        /// dependent. Head-to-lowest-bone follows the skeleton and is stable, but it
        /// under-reports a person by the height of their neck, which is why the bounds
        /// figure is logged next to it rather than discarded.
        /// </summary>
        private static float MeasureHeight(GameObject model, SkinnedMeshRenderer renderer, float scale)
        {
            if (model != null)
            {
                Transform head = null;
                float lowest = float.MaxValue;

                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Head") head = t;
                    if (t.position.y < lowest) lowest = t.position.y;
                }

                if (head != null && lowest < float.MaxValue)
                    return head.position.y - lowest;
            }

            // A model with no skeleton cannot be measured this way; bounds are the best
            // remaining answer.
            return renderer != null ? renderer.bounds.size.y * scale : 0f;
        }

        /// <summary>
        /// Points the renderer's material at the game's runtime library.
        ///
        /// The FBX arrived with a Default-Material that carries none of the generated
        /// textures, so the character would otherwise render flat grey despite four
        /// maps being present on disk.
        /// </summary>
        private static void ApplyMaterial(SkinnedMeshRenderer renderer)
        {
            if (renderer == null) return;

            MaterialLibrary library = Object.FindObjectOfType<MaterialLibrary>();
            Material material = library != null
                ? library.Get("survivor")
                : null;

            if (material == null) return;

            var instance = new Material(material) { name = "Mat_Survivor" };

            // The generated maps are named after the original export, so bind them by
            // hand rather than relying on Unity's automatic matching, which keys off
            // material names inside the FBX that no longer exist after renaming.
            Texture2D albedo = Resources.Load<Texture2D>("Survivor_Albedo");
            if (albedo != null) SetMap(instance, albedo, "_BaseMap", "_MainTex");

            renderer.sharedMaterial = instance;
        }

        private static void SetMap(Material material, Texture2D texture, string urpName, string builtinName)
        {
            if (material.HasProperty(urpName)) material.SetTexture(urpName, texture);
            else if (material.HasProperty(builtinName)) material.SetTexture(builtinName, texture);
        }

        /// <summary>
        /// Turns the model to face the direction the controller is moving. Called from
        /// the controller rather than reading input here, so there is one source of
        /// truth for which way "forward" is.
        ///
        /// Uses world rotation so a future parent yaw cannot compound with LookRotation
        /// written into local space (that used to face the wrong way whenever the
        /// player root was not identity).
        /// </summary>
        /// <summary>
        /// Runs after the animation has posed the skeleton and after PlayerController
        /// placed the root, so the chest anchor uses this frame's pose.
        /// </summary>
        private void LateUpdate()
        {
            if (_model == null) return;

            Vector3 local = positionOffset;
            if (AnchorChestToRoot && _chest != null)
            {
                // Pose offset of the chest from the model origin, independent of where
                // the model currently sits; cancel its horizontal part.
                Vector3 poseOffset = _chest.position - _model.transform.position;
                poseOffset.y = 0f;
                local -= transform.InverseTransformVector(poseOffset);
                local.y = positionOffset.y;
            }

            _model.transform.localPosition = local;
        }

        public void FaceTowards(Vector3 worldDirection, float turnDegreesPerSecond = 0f)
        {
            if (_model == null) return;
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(worldDirection.normalized, Vector3.up)
                              * Quaternion.Euler(0f, yawOffset, 0f);

            // Fixed slerp factor is fine on land; swimming needs a capped turn rate so the
            // torso does not whip around faster than the stroke cycle.
            if (turnDegreesPerSecond > 0f)
            {
                _model.transform.rotation = Quaternion.RotateTowards(
                    _model.transform.rotation, target, turnDegreesPerSecond * Time.deltaTime);
            }
            else
            {
                _model.transform.rotation = Quaternion.Slerp(
                    _model.transform.rotation, target, 0.35f);
            }
        }
    }
}