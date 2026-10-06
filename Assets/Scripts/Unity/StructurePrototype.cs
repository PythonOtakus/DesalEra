using System;
using DesalEra.Samples;
using DesalEra.Structure;
using UnityEngine;

namespace DesalEra.Unity
{
    /// <summary>
    /// Drives the feasibility prototype: builds a structure, solves it, draws the
    /// result. Open the scene, press play, and the tower either stands or collapses
    /// in front of you.
    ///
    /// This is scaffolding for evaluating the structural model, not game code. It
    /// owns the solver instance only so the inspector can retune the dials live.
    /// </summary>
    public sealed class StructurePrototype : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] private int levels = 8;
        [SerializeField] private float storeyHeight = 3f;
        [SerializeField] private float deckArmLength = 1.5f;
        [SerializeField] private float columnAreaM2 = 0.09f;
        [SerializeField] private float deckAreaM2 = 0.05f;

        [Header("Materials")]
        [SerializeField] private MaterialKind columnMaterial = MaterialKind.Wood;
        [SerializeField] private MaterialKind deckMaterial = MaterialKind.Wood;

        [Header("Solver dials")]
        [Tooltip("Designer-facing load multiplier. 1.0 is physically neutral.")]
        [SerializeField, Range(0.25f, 8f)] private float loadScale = 1f;

        [Tooltip("Pontoon lift multiplier. Raise it to keep a heavy build afloat.")]
        [SerializeField, Range(0.25f, 4f)] private float buoyancyScale = 1f;

        [Header("Run")]
        [SerializeField] private bool solveOnStart = true;
        [SerializeField] private bool solveToEquilibrium = true;
        [SerializeField] private bool rebuildEveryFrame;

        private StructureSolver _solver;
        private StructureView _view;
        private SolveReport _report;

        public StructureSolver Solver => _solver;
        public SolveReport Report => _report;

        private void Awake()
        {
            EnsureView();
        }

        /// <summary>
        /// Resolves the view, adding one if absent. Awake does not run for objects
        /// created in edit mode, so Rebuild calls this too rather than assuming the
        /// reference is already wired.
        /// </summary>
        private StructureView EnsureView()
        {
            if (_view == null) _view = GetComponent<StructureView>();
            if (_view == null) _view = gameObject.AddComponent<StructureView>();
            return _view;
        }

        private void Start()
        {
            if (solveOnStart) Rebuild();
        }

        private void Update()
        {
            if (rebuildEveryFrame) Rebuild();
        }

        /// <summary>Rebuilds the graph, solves it, and redraws. Safe to call at any time.</summary>
        public void Rebuild()
        {
            StructureView view = EnsureView();

            _solver = Structures.Tower(
                levels, storeyHeight, columnMaterial, deckMaterial,
                deckArmLength, columnAreaM2, deckAreaM2);
            _solver.LoadScale = loadScale;
            _solver.BuoyancyScale = buoyancyScale;

            _report = solveToEquilibrium ? _solver.SolveToEquilibrium() : _solver.Solve();
            view.Rebuild(_solver, _report);
        }

        /// <summary>
        /// Builds a small side-by-side comparison of every material at the same
        /// height. Useful for eyeballing whether the material curve is playable
        /// before committing to it in a design document.
        /// </summary>
        public SolveReport[] BuildMaterialComparison(int comparisonLevels)
        {
            var reports = new SolveReport[4];
            int i = 0;
            foreach (MaterialKind material in new[]
                     {
                         MaterialKind.Plastic, MaterialKind.Wood,
                         MaterialKind.Concrete, MaterialKind.Steel
                     })
            {
                StructureSolver solver = Structures.Tower(
                    comparisonLevels, storeyHeight, material, material,
                    deckArmLength, columnAreaM2, deckAreaM2);
                solver.LoadScale = loadScale;
                reports[i++] = solver.SolveToEquilibrium();
            }
            return reports;
        }

        /// <summary>One-line human summary of the last solve, for on-screen debugging.</summary>
        public string DescribeLastSolve()
        {
            if (_report == null) return "not solved yet";
            return string.Format(
                "{0}: {1} members, {2} failed, {3:F1} kN, floating={4}, capsizing={5} (CoM offset {6:F2} m)",
                columnMaterial, _solver.Members.Count, _report.FailedMemberCount,
                _report.WeightKn, _report.IsFloating, _report.IsCapsizing, _report.CapsizeOffsetM);
        }

        private void OnValidate()
        {
            levels = Mathf.Max(1, levels);
            storeyHeight = Mathf.Max(0.5f, storeyHeight);
            deckArmLength = Mathf.Max(0.1f, deckArmLength);
            columnAreaM2 = Mathf.Max(0.001f, columnAreaM2);
            deckAreaM2 = Mathf.Max(0.001f, deckAreaM2);
        }
    }
}
