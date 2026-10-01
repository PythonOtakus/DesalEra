using System;
using CrazyAquarium.Structure;
using UnityEngine;

namespace CrazyAquarium.Unity
{
    /// <summary>
    /// Renders a <see cref="StructureSolver"/> as boxes, one per member,
    /// coloured by how close it is to failing.
    ///
    /// The whole point of this component is that it owns no simulation state. The
    /// solver decides what survives and what carries what; this only draws the answer.
    /// That split is what lets the simulation be tested headless and the visuals be
    /// thrown away and rewritten without touching the model.
    /// </summary>
    public sealed class StructureView : MonoBehaviour
    {
        [Header("Geometry")]
        [SerializeField] private float memberThicknessM = 0.18f;

        [Header("Stress colours")]
        [Tooltip("Green, nominal. Red channel is the lowest of the three by design.")]
        [SerializeField] private Color intactColor = new Color(0.36f, 0.62f, 0.56f);

        [Tooltip("Amber, nearing the limit. Must stay below the critical colour in red.")]
        [SerializeField] private Color strainedColor = new Color(0.88f, 0.72f, 0.24f);

        [Tooltip("Red, at the limit. The reddest of the three, or the ramp reads backwards.")]
        [SerializeField] private Color criticalColor = new Color(0.98f, 0.24f, 0.18f);

        /// <summary>Exposed so tests can assert on the ramp endpoints.</summary>
        public Color IntactColor => intactColor;
        public Color StrainedColor => strainedColor;
        public Color CriticalColor => criticalColor;

        [Header("Water")]
        [SerializeField] private float waterThicknessM = 0.05f;

        private Mesh _unitCube;
        private Material _memberMaterial;
        private Material _waterMaterial;
        private GameObject _waterPlane;

        /// <summary>Number of members drawn on the last rebuild.</summary>
        public int RenderedMemberCount { get; private set; }

        private void OnDestroy()
        {
            SafeDestroy(_memberMaterial);
            SafeDestroy(_waterMaterial);
            SafeDestroy(_unitCube);
        }

        /// <summary>
        /// Destroys in play mode and immediately in edit mode.
        ///
        /// The view is exercised by EditMode tests, where plain Destroy is an error:
        /// it defers to end-of-frame, which never arrives outside play mode, so the
        /// old objects survived and every rebuild accumulated another copy of the
        /// structure. This is why the idempotency test exists.
        /// </summary>
        private static void SafeDestroy(UnityEngine.Object target)
        {
            if (target == null) return;

            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        /// <summary>
        /// Rebuilds the visual representation from the solver's current state.
        /// Call after any structural edit or after a solve that changed failures.
        /// </summary>
        public void Rebuild(StructureSolver solver, SolveReport report)
        {
            if (solver == null) throw new ArgumentNullException(nameof(solver));

            EnsureResources();
            ClearMembers();
            EnsureWater(solver, report);

            foreach (Member member in solver.Members)
            {
                if (member.IsFailed) continue;
                SpawnMember(solver, member);
            }

            RenderedMemberCount = CountLiveMembers(solver);
            _ = report;
        }

        private static int CountLiveMembers(StructureSolver solver)
        {
            int n = 0;
            foreach (Member m in solver.Members)
            {
                if (!m.IsFailed) n++;
            }
            return n;
        }

        private void SpawnMember(StructureSolver solver, Member member)
        {
            Vector3 from = solver.JointAt(member.JointA).Position;
            Vector3 to = solver.JointAt(member.JointB).Position;
            Vector3 mid = (from + to) * 0.5f;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 1e-4f) return;

            var go = new GameObject($"member_{member.Id}_{member.Material}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = mid;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta / length);
            go.transform.localScale = new Vector3(memberThicknessM, length, memberThicknessM);

            go.AddComponent<MeshFilter>().sharedMesh = _unitCube;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _memberMaterial;
            renderer.sharedMaterial.color = ColorFor(member);

            // Colour has to live on the instance, not the shared material, or every
            // member would inherit whichever colour was assigned last.
            go.AddComponent<StructureMemberTag>().MemberId = member.Id;
        }

        /// <summary>
        /// Teal-to-red by utilization. Anything past 1.0 has already failed and is
        /// not drawn, so 1.0 is the top of the visible range.
        ///
        /// Each segment lerps in linear space rather than taking sRGB components
        /// directly, because a naive per-channel lerp between a teal and an amber
        /// dips the red channel partway through and the beam visibly goes *less*
        /// alarming as it loads up.
        /// </summary>
        public Color ColorFor(Member member)
        {
            float t = Mathf.Clamp01(member.Utilization);

            Color from;
            Color to;
            float localT;

            if (t < 0.6f)
            {
                from = intactColor;
                to = strainedColor;
                localT = t / 0.6f;
            }
            else
            {
                from = strainedColor;
                to = criticalColor;
                localT = (t - 0.6f) / 0.4f;
            }

            return Color.Lerp(from.linear, to.linear, localT).gamma;
        }

        private void ClearMembers()
        {
            // Snapshot the children first: SafeDestroy detaches them immediately in
            // edit mode, which would mutate transform.childCount mid-loop.
            var doomed = new System.Collections.Generic.List<GameObject>(transform.childCount);
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child == _waterPlane) continue;
                doomed.Add(child);
            }

            foreach (GameObject child in doomed) SafeDestroy(child);
        }

        private void EnsureWater(StructureSolver solver, SolveReport report)
        {
            if (report == null || !report.IsFloating) return;

            if (_waterPlane == null)
            {
                _waterPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _waterPlane.name = "waterline";
                _waterPlane.transform.SetParent(transform, worldPositionStays: false);
                _waterPlane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _waterPlane.transform.localScale = new Vector3(60f, 60f, waterThicknessM);
                _waterPlane.GetComponent<MeshRenderer>().sharedMaterial = _waterMaterial;

                // A quad is only a visual reference, never a collider, or it would
                // fight the structure's own collision.
                Destroy(_waterPlane.GetComponent<Collider>());
            }

            _waterPlane.transform.localPosition = new Vector3(0f, solver.WaterLevelY, 0f);
        }

        private void EnsureResources()
        {
            if (_unitCube == null) _unitCube = BuildUnitCube();
            if (_memberMaterial == null) _memberMaterial = BuildUnlitMaterial();
            if (_waterMaterial == null) _waterMaterial = BuildUnlitMaterial();
        }

        private static Material BuildUnlitMaterial()
        {
            // Unlit on purpose. The prototype is about reading structure, not lighting,
            // and a lit material here would hide the stress colours under dark water.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "StructureViewMember" };
            return material;
        }

        private static Mesh BuildUnitCube()
        {
            var mesh = new Mesh { name = "StructureViewUnitCube" };

            // A unit cube spanning -0.5..0.5, so a transform scale of thickness x
            // length x thickness gives the member its real-world dimensions.
            Vector3[] corners =
            {
                new(-0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, -0.5f),
                new(0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f),
                new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f),
                new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f)
            };

            int[] triangles =
            {
                0, 2, 1, 0, 3, 2, // back
                4, 5, 6, 4, 6, 7, // front
                0, 1, 5, 0, 5, 4, // bottom
                3, 7, 6, 3, 6, 2, // top
                0, 4, 7, 0, 7, 3, // left
                1, 2, 6, 1, 6, 5  // right
            };

            mesh.vertices = corners;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>
    /// Marks a rendered box as standing for a specific solver member, so a click or a
    /// later gameplay query can map a collider back to graph data.
    /// </summary>
    public sealed class StructureMemberTag : MonoBehaviour
    {
        public int MemberId = -1;
    }
}
