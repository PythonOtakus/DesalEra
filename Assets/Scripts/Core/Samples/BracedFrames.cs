using DesalEra.Structure;
using UnityEngine;

namespace DesalEra.Samples
{
    /// <summary>
    /// Braced-frame fixtures for lateral analysis. These are 2D frames in the XY
    /// plane with diagonals, which is the shape that exposes whether a brace system
    /// actually resists side load.
    /// </summary>
    public static class BracedFrames
    {
        public enum Bracing
        {
            /// <summary>Columns and floor beams only. A mechanism under side load.</summary>
            None,

            /// <summary>One diagonal per storey, alternating direction.</summary>
            Single,

            /// <summary>Both diagonals per storey, forming a truss.</summary>
            Cross,

            /// <summary>A single knee brace at the base of every column.</summary>
            KneeBrace
        }

        /// <summary>
        /// Builds a rectangular tower frame of <paramref name="storeys"/> storeys,
        /// each storey <paramref name="storeyHeightM"/> tall and
        /// <paramref name="bayWidthM"/> wide, with the requested bracing.
        ///
        /// Column joints run up the left and right edges. Members are appended in a
        /// fixed order so tests can identify them by index.
        /// </summary>
        public static StructureSolver Frame(
            int storeys = 4,
            float storeyHeightM = 3f,
            float bayWidthM = 4f,
            Bracing bracing = Bracing.Single,
            MaterialKind columnMaterial = MaterialKind.Steel,
            MaterialKind braceMaterial = MaterialKind.Steel,
            float columnAreaM2 = 0.09f,
            float braceAreaM2 = 0.02f)
        {
            var solver = new StructureSolver { WaterLevelY = 0f };

            // left[i] and right[i] are the column joints at height i * storey.
            var left = new int[storeys + 1];
            var right = new int[storeys + 1];

            for (int i = 0; i <= storeys; i++)
            {
                float y = i * storeyHeightM;
                left[i] = solver.AddJoint(new Vector3(0f, y, 0f), anchored: i == 0);
                right[i] = solver.AddJoint(new Vector3(bayWidthM, y, 0f), anchored: i == 0);
            }

            for (int i = 0; i < storeys; i++)
            {
                // Columns first, so even indices in each storey pair are columns.
                solver.AddMember(left[i], left[i + 1], columnMaterial, columnAreaM2);
                solver.AddMember(right[i], right[i + 1], columnMaterial, columnAreaM2);

                // The floor beam closes each level.
                solver.AddMember(left[i + 1], right[i + 1], columnMaterial, columnAreaM2);

                switch (bracing)
                {
                    case Bracing.None:
                        break;

                    case Bracing.Single:
                        // Alternate the lean so the frame is braced against both shear
                        // directions over its height. The second form reverses the
                        // endpoints: without that, both diagonals point the same way
                        // in local axes and the frame resists only one direction.
                        if (i % 2 == 0)
                            solver.AddMember(left[i], right[i + 1], braceMaterial, braceAreaM2);
                        else
                            solver.AddMember(right[i], left[i + 1], braceMaterial, braceAreaM2);
                        break;

                    case Bracing.Cross:
                        solver.AddMember(left[i], right[i + 1], braceMaterial, braceAreaM2);
                        solver.AddMember(right[i], left[i + 1], braceMaterial, braceAreaM2);
                        break;

                    case Bracing.KneeBrace:
                        // A short diagonal from each base corner up the far column,
                        // which is the cheapest way to stiffen a single-storey frame.
                        int kneeTarget = solver.AddJoint(new Vector3(bayWidthM, (i + 1) * storeyHeightM, 0f));
                        solver.AddMember(left[i], kneeTarget, braceMaterial, braceAreaM2);
                        break;
                }
            }

            solver.RecomputeAllDerived();
            return solver;
        }

        /// <summary>
        /// Loads every joint with its share of self weight, which the truss solver
        /// needs because it solves member forces, not mass flow.
        /// </summary>
        public static void ApplySelfWeight(StructureSolver solver, TrussSolver truss)
        {
            const float g = 9.81f;

            foreach (Member m in solver.Members)
            {
                if (m.IsFailed) continue;

                // Half the member's weight lands on each end. A cantilever is carried
                // entirely by its root, matching the gravity solver's convention.
                float share = m.IsCantilever ? 1f : 0.5f;
                Vector3 force = new Vector3(0f, -m.MassKg * g / 1000f * share, 0f);

                truss.AddJointLoad(m.JointA, force);
                if (!m.IsCantilever) truss.AddJointLoad(m.JointB, force);
            }
        }

        /// <summary>
        /// Index of the first member matching a predicate, for tests that need to name
        /// a specific structural role without depending on the exact ordering.
        /// </summary>
        public static int IndexOfFirst(StructureSolver solver, System.Func<Member, bool> predicate)
        {
            for (int i = 0; i < solver.Members.Count; i++)
            {
                if (predicate(solver.Members[i])) return i;
            }
            return -1;
        }

        /// <summary>True when a member is steep enough to be a column rather than a brace.</summary>
        public static bool IsColumn(Member m) => Mathf.Abs(m.Axis.y) > 0.9f;

        /// <summary>True when a member is shallow enough to be a floor beam.</summary>
        public static bool IsFloorBeam(Member m) => Mathf.Abs(m.Axis.y) < 0.1f;

        /// <summary>True when a member is diagonal.</summary>
        public static bool IsDiagonal(Member m)
        {
            float ax = Mathf.Abs(m.Axis.x);
            float ay = Mathf.Abs(m.Axis.y);
            return ax > 0.1f && ay > 0.1f;
        }
    }
}
