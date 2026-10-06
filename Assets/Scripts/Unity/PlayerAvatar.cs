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
    public sealed class PlayerAvatar : MonoBehaviour
    {
        private const string ResourcePath = "Survivor";

        [Header("Placement")]
        [Tooltip("Scales the loaded model. 1.0 keeps the generated figure's own height.")]
        [SerializeField] private float modelScale = 1f;

        [Tooltip("Turns the model to face the direction the controller calls forward.")]
        [SerializeField] private float yawOffset = 180f;

        [Tooltip("Offset from the player's feet to the model's own origin.")]
        [SerializeField] private Vector3 positionOffset = new Vector3(0f, 0f, 0f);

        private GameObject _model;
        private SkinnedMeshRenderer _renderer;
        private Transform _playerTransform;

        /// <summary>Height in metres, measured from the loaded mesh.</summary>
        public float ModelHeightM { get; private set; }

        public bool IsLoaded => _model != null;

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

            // The generated figure faces -Z; Unity's convention is +Z. Without this
            // turn the character walks backwards relative to its own limbs.
            instance.transform.localRotation = Quaternion.Euler(0f, yawOffset, 0f);
            instance.transform.localPosition = positionOffset;
            instance.transform.localScale = Vector3.one * modelScale;

            _model = instance;
            _renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            ApplyMaterial(_renderer);

            ModelHeightM = MeasureHeight(instance, _renderer, modelScale);
            Debug.Log($"[DesalEra] survivor loaded, skeleton {ModelHeightM:F2} m " +
                      $"(sole to head joint), mesh bounds {_renderer.bounds.size.y:F2} m " +
                      $"(includes hair and footwear)");
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
        /// </summary>
        public void FaceTowards(Vector3 worldDirection)
        {
            if (_model == null) return;
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            _model.transform.localRotation = Quaternion.Slerp(
                _model.transform.localRotation, target * Quaternion.Euler(0f, yawOffset, 0f), 0.25f);
        }
    }
}