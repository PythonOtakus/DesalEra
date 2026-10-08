using DesalEra.Game;
using UnityEngine;

namespace DesalEra.Unity
{
    /// <summary>
    /// Draws a translucent ghost of the selected piece where a click would place it:
    /// green when the placement would succeed, red when it would be refused. The reason
    /// for a refusal is shown by the build dock; this only answers "where" and "yes/no".
    ///
    /// The ghost is rebuilt only when the target changes, and parented to the raft root
    /// so it rides the swell with the structure it previews.
    /// </summary>
    public sealed class BuildPreview : MonoBehaviour
    {
        private static readonly Color Valid = new Color(0.35f, 0.95f, 0.45f, 0.38f);
        private static readonly Color Invalid = new Color(1f, 0.28f, 0.22f, 0.38f);
        private const float MemberThickness = 0.32f;

        private GameBootstrap _world;
        private PlayerController _player;
        private Material _material;
        private GameObject _ghost;

        private BuildPiece _shownPiece;
        private PlacementKey _shownKey;
        private bool _shownValid;

        /// <summary>True while a ghost is drawn. For tests and diagnostics.</summary>
        public bool IsShowing => _ghost != null && _ghost.activeSelf;

        /// <summary>Whether the drawn ghost is the "allowed" colour.</summary>
        public bool ShowingValid => _shownValid;

        public void Initialise(GameBootstrap world, PlayerController player)
        {
            _world = world;
            _player = player;
            Shader shader = Shader.Find("Sprites/Default");
            _material = new Material(shader) { name = "Mat_BuildPreview", renderQueue = 3100 };
            _material.color = Valid;
        }

        private void LateUpdate()
        {
            if (_world == null || _player == null) return;

            PlacementPlan plan = _player.PreviewPlan;
            if (plan == null)
            {
                Hide();
                return;
            }

            bool valid = _player.PreviewProblem == null;
            if (_ghost == null || plan.Piece != _shownPiece || !plan.Key.Equals(_shownKey))
                Rebuild(plan);

            if (_ghost != null && !_ghost.activeSelf) _ghost.SetActive(true);
            if (valid != _shownValid || _material.color != (valid ? Valid : Invalid))
            {
                _shownValid = valid;
                _material.color = valid ? Valid : Invalid;
            }
        }

        private void Rebuild(PlacementPlan plan)
        {
            if (_ghost != null) GameBootstrap.SafeDestroy(_ghost);
            _shownPiece = plan.Piece;
            _shownKey = plan.Key;

            if (GameBootstrap.HasPlacementVisual(plan.Piece.Name))
            {
                _ghost = _world.BuildPlacementVisual(plan.Piece, plan.Key, _material);
            }
            else
            {
                _ghost = new GameObject("preview");
                _ghost.transform.SetParent(_world.RaftRoot, worldPositionStays: false);
                foreach ((Vector3 from, Vector3 to) in plan.Segments)
                    AddSegment(_ghost.transform, plan.Piece.Name, from, to);
            }

            if (_ghost != null) _ghost.name = "BuildPreview";
        }

        private void AddSegment(Transform parent, string pieceName, Vector3 from, Vector3 to)
        {
            (Mesh mesh, Quaternion rotation) = _world.MemberShape(pieceName, from, to, MemberThickness);
            var go = new GameObject("segment");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void Hide()
        {
            if (_ghost != null && _ghost.activeSelf) _ghost.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_ghost != null) GameBootstrap.SafeDestroy(_ghost);
            if (_material != null) GameBootstrap.SafeDestroy(_material);
        }
    }
}
