using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesalEra.Structure
{
    public sealed class TrussReport
    {
        public bool Converged;
        public int Iterations;
        public int FailedMemberCount;

        /// <summary>Largest joint displacement found, in metres.</summary>
        public float MaxLateralDisplacementM;

        /// <summary>Displacement of the topmost joint, in metres.</summary>
        public float TopDisplacementM;

        /// <summary>
        /// True when the stiffness matrix is singular: the frame has no complete load
        /// path and is a mechanism. A structure with no diagonal behaves this way, and
        /// that is a message the player needs rather than a hang.
        /// </summary>
        public bool IsUnstable;

        /// <summary>Why the solve failed, for diagnostics.</summary>
        public string FailureReason;

        public readonly List<Member> FailedMembers = new List<Member>();

        /// <summary>
        /// The member that failed first, or null. When a build collapses this is the
        /// answer a player actually needs: which piece gave way.
        /// </summary>
        public Member FirstFailedMember;

        public readonly List<Vector3> JointDisplacements = new List<Vector3>();
    }

    /// <summary>
    /// Direct stiffness solver for pin-jointed space frames.
    ///
    /// The earlier relaxation approach was wrong for this job. A braced frame is
    /// statically indeterminate, and relaxing one joint at a time only shuttles force
    /// between neighbours until it settles on a solution that satisfies nothing. The
    /// symptom was unmistakable: adding a diagonal made the frame deflect further, and
    /// a cross brace measured the same as a single one, which is not how trusses work.
    ///
    /// This assembles the global stiffness matrix, solves K*u = f by Gaussian
    /// elimination with partial pivoting, then recovers member axial forces from the
    /// nodal displacements. The payoff is that displacement becomes a real physical
    /// quantity rather than a by-product of iteration, so "stiffer" is measurable
    /// instead of asserted.
    ///
    /// Supports tension and compression separately, which is what makes a brace a
    /// genuine design choice: a slender member is strong in compression and weak in
    /// tension, so an X-brace and a pair of ties are not interchangeable.
    /// </summary>
    public sealed class TrussSolver
    {
        private readonly List<Joint> _joints;
        private readonly List<Member> _members;
        private readonly List<Vector3> _externalForcesKn;
        private readonly List<Vector3> _displacementsM;

        // Exactly what the last ApplyWind added, per joint, so a repeat solve can
        // remove it again without recomputing from changed settings.
        private readonly List<Vector3> _lastWindVector = new List<Vector3>();

        /// <summary>Lateral load per metre of height, in kN/m. Applied along the wind direction.</summary>
        public float WindLoadKnPerM;

        public Vector3 WindDirection = Vector3.right;

        /// <summary>
        /// Scale applied to every displacement, letting a real frame's millimetre-scale
        /// movement read on screen. Presentation only; the stiffness comparison is
        /// unaffected because it is a uniform scale.
        /// </summary>
        public float DisplacementVisualScale = 1f;

        public TrussSolver(List<Joint> joints, List<Member> members)
        {
            _joints = joints ?? throw new ArgumentNullException(nameof(joints));
            _members = members ?? throw new ArgumentNullException(nameof(members));
            _externalForcesKn = new List<Vector3>(joints.Count);
            _displacementsM = new List<Vector3>(joints.Count);

            for (int i = 0; i < joints.Count; i++)
            {
                _externalForcesKn.Add(Vector3.zero);
                _displacementsM.Add(Vector3.zero);
            }

            RefreshAxes();
        }

        public void RefreshAxes()
        {
            foreach (Member m in _members)
            {
                m.RecomputeAxis(_joints[m.JointA].Position, _joints[m.JointB].Position);
            }
        }

        /// <summary>Read-only view of the members, for tests and inspection tooling.</summary>
        public IReadOnlyList<Member> Members => _members;

        /// <summary>Read-only view of the joints, for tests and inspection tooling.</summary>
        public IReadOnlyList<Joint> Joints => _joints;

        public void ClearLoads()
        {
            for (int i = 0; i < _externalForcesKn.Count; i++) _externalForcesKn[i] = Vector3.zero;

            // The wind record must go too, or a later ClearPreviousWind would subtract
            // from an already-zeroed load case.
            _lastWindVector.Clear();
        }

        /// <summary>
        /// Removes the wind contribution applied by the previous solve, leaving
        /// gravity and manual loads intact.
        ///
        /// This subtracts what was actually applied rather than recomputing it from
        /// the current wind settings. Recomputing looks equivalent but is not: on the
        /// first solve there is no previous wind, so subtracting a freshly computed
        /// value removed load that was never there and cancelled the real wind out.
        /// </summary>
        private void ClearPreviousWind()
        {
            if (_lastWindVector.Count == 0) return;

            for (int i = 0; i < _joints.Count && i < _lastWindVector.Count; i++)
            {
                _externalForcesKn[i] -= _lastWindVector[i];
            }
            _lastWindVector.Clear();
        }

        public void AddJointLoad(int jointIndex, Vector3 forceKn)
        {
            if (jointIndex < 0 || jointIndex >= _joints.Count)
                throw new ArgumentOutOfRangeException(nameof(jointIndex));
            _externalForcesKn[jointIndex] += forceKn;
        }

        public Vector3 ExternalForceAt(int jointIndex) => _externalForcesKn[jointIndex];
        public Vector3 DisplacementAt(int jointIndex) => _displacementsM[jointIndex];

        public float MaxDisplacementM
        {
            get
            {
                float max = 0f;
                for (int i = 0; i < _displacementsM.Count; i++)
                    max = Mathf.Max(max, _displacementsM[i].magnitude);
                return max * DisplacementVisualScale;
            }
        }

        /// <summary>
        /// Distributes each member's self weight onto its joints. A cantilever is
        /// carried entirely by its root, matching the gravity solver's convention.
        /// </summary>
        public void ApplySelfWeight(float gravityMPerS2 = 9.81f)
        {
            foreach (Member m in _members)
            {
                if (m.IsFailed) continue;

                float share = m.IsCantilever ? 1f : 0.5f;
                Vector3 force = Vector3.down * (m.MassKg * gravityMPerS2 / 1000f * share);

                AddJointLoad(m.JointA, force);
                if (!m.IsCantilever) AddJointLoad(m.JointB, force);
            }
        }

        /// <summary>
        /// Adds a lateral load that grows with height, so a tall frame is pushed harder
        /// at the top. Wind pressure rises with height, and using that profile is what
        /// makes an unbraced tower fail from the top down rather than uniformly.
        /// </summary>
        public void ApplyWind()
        {
            if (WindLoadKnPerM <= 0f) return;

            Vector3 dir = WindDirection.sqrMagnitude < 1e-8f
                ? Vector3.right
                : WindDirection.normalized;

            for (int i = 0; i < _joints.Count; i++)
            {
                float height = Mathf.Max(0f, _joints[i].Position.y);
                Vector3 applied = dir * (WindLoadKnPerM * height);
                _externalForcesKn[i] += applied;
                _lastWindVector.Add(applied);
            }
        }

        /// <summary>
        /// Assembles K, solves for displacement, then recovers axial forces.
        ///
        /// Anchored joints get identity rows: their displacement is held at zero and
        /// their rows carry the support reaction, which is the standard way to impose
        /// a boundary condition without conditioning the matrix.
        /// </summary>
        public TrussReport Solve()
        {
            var report = new TrussReport();
            report.JointDisplacements.Clear();

            // Wind is part of the load case, so it is applied here rather than being
            // left to the caller. Forgetting it produced a frame that reported
            // convergence while resisting nothing sideways. The previous application
            // is removed first so repeat solves stay idempotent.
            ClearPreviousWind();
            ApplyWind();

            int n = _joints.Count * 3;
            if (n == 0)
            {
                report.Converged = true;
                return report;
            }

            var stiffness = new double[n, n];
            var force = new double[n];

            for (int i = 0; i < n; i++) force[i] = _externalForcesKn[i / 3][i % 3];

            for (int mi = 0; mi < _members.Count; mi++)
            {
                Member m = _members[mi];
                if (m.IsFailed) continue;
                AssembleMember(m, stiffness);
            }

            // Boundary conditions. An anchored joint cannot move, so its three
            // displacement unknowns are replaced by zeros and its stiffness row is
            // forced to the identity, leaving room for the reaction force.
            for (int j = 0; j < _joints.Count; j++)
            {
                if (!_joints[j].IsAnchored) continue;
                for (int axis = 0; axis < 3; axis++)
                {
                    int row = j * 3 + axis;
                    for (int col = 0; col < n; col++) stiffness[row, col] = 0.0;
                    stiffness[row, row] = 1.0;
                    force[row] = 0.0;
                }
            }

            var displacement = new double[n];
            if (!SolveLinearSystem(stiffness, force, displacement, out string reason))
            {
                report.Converged = false;
                report.IsUnstable = true;
                report.FailureReason = reason;
                CollectFailures(report);
                return report;
            }

            for (int j = 0; j < _joints.Count; j++)
            {
                _displacementsM[j] = new Vector3(
                    (float)displacement[j * 3],
                    (float)displacement[j * 3 + 1],
                    (float)displacement[j * 3 + 2]);
            }

            RecoverAxialForces();

            report.Converged = true;
            report.Iterations = 1;
            report.MaxLateralDisplacementM = MaxDisplacementM;
            report.TopDisplacementM = TopDisplacementM;
            report.JointDisplacements.AddRange(_displacementsM);

            CollectFailures(report);
            return report;
        }

        private float TopDisplacementM
        {
            get
            {
                if (_joints.Count == 0) return 0f;
                int top = 0;
                for (int i = 1; i < _joints.Count; i++)
                    if (_joints[i].Position.y > _joints[top].Position.y) top = i;
                return _displacementsM[top].magnitude * DisplacementVisualScale;
            }
        }

        /// <summary>
        /// Standard 3D truss element: k = (EA/L) * [c c][s s] blocks, where c and s
        /// are the axis cosines. A member can only push or pull along its own axis,
        /// which is what makes a diagonal a diagonal.
        /// </summary>
        private void AssembleMember(Member m, double[,] k)
        {
            double ea = m.AxialRigidityKn;
            if (ea <= 0.0 || m.LengthM <= 1e-6f) return;

            double scale = ea / m.LengthM;
            double cx = m.Axis.x, cy = m.Axis.y, cz = m.Axis.z;

            int a = m.JointA * 3;
            int b = m.JointB * 3;

            // Direction cosine matrix d, with d[axis][axis] along the member and the
            // remaining two rows holding the off-axis directions.
            var d = new double[3, 3];
            d[0, 0] = cx; d[0, 1] = cy; d[0, 2] = cz;

            Vector3 helper = Math.Abs(cy) < 0.9 ? Vector3.up : Vector3.right;
            Vector3 t1 = (helper - m.Axis * Vector3.Dot(helper, m.Axis)).normalized;
            Vector3 t2 = Vector3.Cross(m.Axis, t1);
            d[1, 0] = t1.x; d[1, 1] = t1.y; d[1, 2] = t1.z;
            d[2, 0] = t2.x; d[2, 1] = t2.y; d[2, 2] = t2.z;

            for (int i = 0; i < 3; i++)
            {
                for (int jx = 0; jx < 3; jx++)
                {
                    for (int p = 0; p < 3; p++)
                    {
                        for (int q = 0; q < 3; q++)
                        {
                            double term = scale * d[0, i] * d[0, jx] * d[p, 0] * d[q, 0]
                                          + scale * d[1, i] * d[1, jx] * d[p, 1] * d[q, 1]
                                          + scale * d[2, i] * d[2, jx] * d[p, 2] * d[q, 2];
                            k[a + p, a + q] += term;
                            k[a + p, b + q] -= term;
                            k[b + p, a + q] -= term;
                            k[b + p, b + q] += term;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Axial force from the relative displacement of the two ends: a member
        /// stretched along its axis is in tension, shortened is in compression.
        /// </summary>
        private void RecoverAxialForces()
        {
            foreach (Member m in _members)
            {
                if (m.IsFailed) continue;
                if (m.AxialRigidityKn <= 0f || m.LengthM <= 1e-6f)
                {
                    m.AxialLoadKn = 0f;
                    continue;
                }

                Vector3 delta = _displacementsM[m.JointB] - _displacementsM[m.JointA];
                float extension = Vector3.Dot(delta, m.Axis);
                m.AxialLoadKn = (float)(m.AxialRigidityKn / m.LengthM * extension);
            }
        }

        /// <summary>
        /// Gaussian elimination with partial pivoting, solved in place.
        /// Returns false for a singular matrix, which is how a mechanism is detected.
        /// </summary>
        private static bool SolveLinearSystem(double[,] a, double[] b, double[] x, out string reason)
        {
            int n = b.Length;
            reason = null;

            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                double best = Math.Abs(a[col, col]);
                for (int row = col + 1; row < n; row++)
                {
                    double candidate = Math.Abs(a[row, col]);
                    if (candidate > best)
                    {
                        best = candidate;
                        pivot = row;
                    }
                }

                // A frame that cannot take a load in some direction has no stiffness
                // there at all. That is a mechanism, and the player needs to be told
                // the frame lacks a diagonal, not shown a singular-matrix stack trace.
                if (best < 1e-9)
                {
                    reason = "stiffness matrix is singular: the frame has no complete load path, " +
                             "so it needs a diagonal or a cross-brace";
                    return false;
                }

                if (pivot != col)
                {
                    for (int c = 0; c < n; c++)
                    {
                        double tmp = a[col, c];
                        a[col, c] = a[pivot, c];
                        a[pivot, c] = tmp;
                    }
                    double tmpB = b[col];
                    b[col] = b[pivot];
                    b[pivot] = tmpB;
                }

                double diagonal = a[col, col];
                for (int row = col + 1; row < n; row++)
                {
                    double factor = a[row, col] / diagonal;
                    if (Math.Abs(factor) < 1e-18) continue;

                    a[row, col] = 0.0;
                    for (int c = col + 1; c < n; c++) a[row, c] -= factor * a[col, c];
                    b[row] -= factor * b[col];
                }
            }

            for (int row = n - 1; row >= 0; row--)
            {
                double sum = b[row];
                for (int c = row + 1; c < n; c++) sum -= a[row, c] * x[c];
                x[row] = sum / a[row, row];
            }

            return true;
        }

        private void CollectFailures(TrussReport report)
        {
            report.FailedMembers.Clear();
            report.FirstFailedMember = null;

            foreach (Member m in _members)
            {
                if (m.IsFailed) continue;
                if (m.Utilization <= 1f) continue;

                m.IsFailed = true;
                report.FailedMembers.Add(m);
                if (report.FirstFailedMember == null) report.FirstFailedMember = m;
            }

            report.FailedMemberCount = report.FailedMembers.Count;
        }

        public void ResetFailures()
        {
            foreach (Member m in _members) m.IsFailed = false;
        }
    }
}
