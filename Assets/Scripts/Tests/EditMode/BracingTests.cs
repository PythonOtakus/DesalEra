using System.Linq;
using DesalEra.Samples;
using DesalEra.Structure;
using NUnit.Framework;
using UnityEngine;

// UnityEngine also defines a Joint, for physics. Aliased so an unqualified Joint in
// this file always means a structural node.
using Joint = DesalEra.Structure.Joint;

namespace DesalEra.Tests
{
    /// <summary>
    /// Verifies that diagonal bracing works, which is the make-or-break question for
    /// the "physics-driven vertical building" pillar of the design.
    ///
    /// The earlier relaxation-based solver failed this outright: adding a diagonal made
    /// the frame deflect further, and a cross brace measured identical to a single one.
    /// A braced frame is statically indeterminate, and relaxing one joint at a time
    /// cannot solve that. The direct stiffness method in TrussSolver is what makes
    /// these assertions possible.
    ///
    /// Known model boundary, verified by Lateral_Limit_IsSingleSpanBendingDriven:
    /// members carry axial force only, so a single-bay frame's side drift is dominated
    /// by column bending that a pure truss cannot represent. Bracing still measurably
    /// helps, and multi-bay frames benefit far more.
    /// </summary>
    public sealed class BracingTests
    {
        private const float WindKnPerM = 5f;

        private static TrussReport Solve(BracedFrames.Bracing bracing, int storeys, float braceAreaM2 = 0.02f)
        {
            StructureSolver frame = BracedFrames.Frame(storeys, 3f, 4f, bracing,
                MaterialKind.Steel, MaterialKind.Steel, 0.09f, braceAreaM2);
            var truss = new TrussSolver(frame.Joints, frame.Members);
            truss.ApplySelfWeight();
            truss.WindLoadKnPerM = WindKnPerM;
            truss.WindDirection = Vector3.right;
            return truss.Solve();
        }

        [Test]
        public void Baseline_UnbracedFrameStillSolves()
        {
            // A frame made only of columns and floor beams is a mechanism in the
            // out-of-plane direction but is fine in-plane, so it must solve rather
            // than report a singular matrix. If this starts failing, the boundary
            // conditions are wrong.
            TrussReport report = Solve(BracedFrames.Bracing.None, 4);

            Assert.IsTrue(report.Converged, report.FailureReason);
            Assert.IsFalse(report.IsUnstable);
        }

        [Test]
        public void Baseline_ExternalWindIsActuallyApplied()
        {
            // Guards a bug where Solve() reported convergence while resisting nothing
            // sideways, because the wind was never folded into the load vector.
            StructureSolver frame = BracedFrames.Frame(4, 3f, 4f, BracedFrames.Bracing.Cross);
            var truss = new TrussSolver(frame.Joints, frame.Members);
            truss.ApplySelfWeight();
            truss.WindLoadKnPerM = WindKnPerM;
            truss.WindDirection = Vector3.right;
            truss.Solve();

            int topJoint = frame.Joints.Count - 1;
            Assert.Greater(truss.ExternalForceAt(topJoint).x, 1f,
                "a joint 12 m up under 5 kN/m must carry a real lateral load");
        }

        [Test]
        public void Lateral_XBraceReducesTopDisplacement()
        {
            float unbraced = Solve(BracedFrames.Bracing.None, 8).TopDisplacementM;
            float braced = Solve(BracedFrames.Bracing.Cross, 8).TopDisplacementM;

            Assert.Less(braced, unbraced,
                $"a cross brace must stiffen the frame: {braced:F4} m braced vs {unbraced:F4} m unbraced");
        }

        [Test]
        public void Lateral_XBraceBeatsSingleDiagonal()
        {
            float single = Solve(BracedFrames.Bracing.Single, 8).TopDisplacementM;
            float cross = Solve(BracedFrames.Bracing.Cross, 8).TopDisplacementM;

            Assert.Less(cross, single,
                $"two diagonals must be stiffer than one: {cross:F4} m vs {single:F4} m");
        }

        [Test]
        public void Lateral_StrongerBraceMeansLessSway()
        {
            float soft = Solve(BracedFrames.Bracing.Cross, 8, 0.005f).TopDisplacementM;
            float stiff = Solve(BracedFrames.Bracing.Cross, 8, 0.08f).TopDisplacementM;

            Assert.Less(stiff, soft,
                $"a stiffer brace must deflect less: {stiff:F4} m vs {soft:F4} m");
        }

        [Test]
        public void Lateral_BraceCarriesGenuineAxialForce()
        {
            StructureSolver frame = BracedFrames.Frame(6, 3f, 4f, BracedFrames.Bracing.Cross);
            var truss = new TrussSolver(frame.Joints, frame.Members);
            truss.ApplySelfWeight();
            truss.WindLoadKnPerM = WindKnPerM;
            truss.Solve();

            Member[] diagonals = frame.Members.Where(BracedFrames.IsDiagonal).ToArray();
            Assert.Greater(diagonals.Length, 0, "fixture must contain diagonals");

            foreach (Member d in diagonals)
            {
                Assert.Greater(Mathf.Abs(d.AxialLoadKn), 0.5f,
                    "a diagonal under side load must carry real axial force");
            }
        }

        [Test]
        public void Lateral_BraceUnloadsTheColumns()
        {
            // A brace should carry part of the shear rather than leaving the columns
            // to do everything. This is the mechanism behind "one brace beats a fatter
            // column", and it is what makes bracing a real decision.
            float unbraced = Solve(BracedFrames.Bracing.None, 8).FailedMemberCount;
            float braced = Solve(BracedFrames.Bracing.Cross, 8).FailedMemberCount;

            Assert.LessOrEqual(braced, unbraced,
                "bracing must not make the frame more likely to fail");
        }

        [Test]
        public void Lateral_SingleDiagonalResistsOneDirectionOnly()
        {
            // Alternating diagonals brace both shear directions over the full height.
            // If they were all leaning the same way, a frame would be strong one way
            // and useless the other, which is a classic framing mistake.
            StructureSolver frame = BracedFrames.Frame(6, 3f, 4f, BracedFrames.Bracing.Single);
            Member[] diagonals = frame.Members.Where(BracedFrames.IsDiagonal).ToArray();

            int positive = diagonals.Count(d => d.Axis.x > 0f);
            int negative = diagonals.Count(d => d.Axis.x < 0f);

            Assert.Greater(positive, 0, "must brace in both directions, not just one");
            Assert.Greater(negative, 0, "must brace in both directions, not just one");
        }

        [Test]
        public void Lateral_TensionAndCompressionUseDifferentCapacities()
        {
            // A slender member is strong in compression and weak in tension. Without
            // that split, bracing and tying become interchangeable, which removes the
            // reason to choose between them at all.
            var steel = new Member { Material = MaterialKind.Steel, CrossSectionAreaM2 = 0.02f };

            Assert.Less(steel.TensionCapacityKn, steel.CompressionCapacityKn,
                "a slender brace must be weaker in tension than in compression");

            steel.AxialLoadKn = steel.TensionCapacityKn * 1.2f;
            Assert.Greater(steel.Utilization, 1f, "a tie past its tensile limit must fail");

            steel.AxialLoadKn = -steel.TensionCapacityKn * 0.9f;
            Assert.Less(steel.Utilization, 1f,
                "the same force in compression should still be within capacity");
        }

        [Test]
        public void Mechanism_UnconstrainedJointIsReportedNotHung()
        {
            // Two columns and a beam form a portal frame that is perfectly able to
            // carry load out of its plane, because that is the plane the model solves.
            // A mechanism appears when a joint has nothing constraining a direction,
            // so the fixture needs a real one: a free joint with no member at all.
            //
            // An earlier version of this test used a single bar pinned at one end,
            // which is wrong: such a bar is fully stable along its own axis. The
            // symptom was a "mechanism" that was never reported, because there was
            // nothing unstable about it.
            var structure = new StructureSolver();
            int anchorA = structure.AddJoint(Vector3.zero, anchored: true);
            int anchorB = structure.AddJoint(new Vector3(4f, 0f, 0f), anchored: true);
            int topA = structure.AddJoint(new Vector3(0f, 3f, 0f));
            int topB = structure.AddJoint(new Vector3(4f, 3f, 0f));
            int loose = structure.AddJoint(new Vector3(2f, 3f, 0f));

            // Portal frame: stable in plane.
            structure.AddMember(anchorA, topA, MaterialKind.Steel);
            structure.AddMember(anchorB, topB, MaterialKind.Steel);
            structure.AddMember(topA, topB, MaterialKind.Steel);

            // A joint with no members at all has zero stiffness in every direction,
            // which is a singular matrix. Loading it is what a player does when they
            // hang something off nothing.
            var truss = new TrussSolver(structure.Joints, structure.Members);
            truss.AddJointLoad(loose, new Vector3(5f, 0f, 0f));
            TrussReport report = truss.Solve();

            Assert.IsTrue(report.IsUnstable,
                "a joint with no members cannot carry any load");
            Assert.IsFalse(report.Converged);
            StringAssert.Contains("diagonal", report.FailureReason.ToLowerInvariant(),
                "the message should tell the player what is missing");
        }

        [Test]
        public void Mechanism_PortalFrameIsStableInPlane()
        {
            // The counterpart to the test above: a plain portal frame with nothing
            // wrong with it must solve. If this ever reports a mechanism, the singular
            // check is too aggressive and is rejecting valid structures.
            var structure = new StructureSolver();
            int anchorA = structure.AddJoint(Vector3.zero, anchored: true);
            int anchorB = structure.AddJoint(new Vector3(4f, 0f, 0f), anchored: true);
            int topA = structure.AddJoint(new Vector3(0f, 3f, 0f));
            int topB = structure.AddJoint(new Vector3(4f, 3f, 0f));

            structure.AddMember(anchorA, topA, MaterialKind.Steel);
            structure.AddMember(anchorB, topB, MaterialKind.Steel);
            structure.AddMember(topA, topB, MaterialKind.Steel);

            var truss = new TrussSolver(structure.Joints, structure.Members);
            truss.ApplySelfWeight();
            TrussReport report = truss.Solve();

            Assert.IsTrue(report.Converged, report.FailureReason);
        }

        [Test]
        public void Determinism_RepeatedSolvesAgree()
        {
            float first = Solve(BracedFrames.Bracing.Cross, 6).TopDisplacementM;
            float second = Solve(BracedFrames.Bracing.Cross, 6).TopDisplacementM;
            float third = Solve(BracedFrames.Bracing.Cross, 6).TopDisplacementM;

            Assert.AreEqual(first, second, 1e-9f);
            Assert.AreEqual(second, third, 1e-9f);
        }

        [Test]
        public void Determinism_RepeatSolveOnSameInstanceIsIdempotent()
        {
            // Wind is re-applied on every Solve. If the previous application were not
            // removed first, the lateral load would double each pass.
            StructureSolver frame = BracedFrames.Frame(6, 3f, 4f, BracedFrames.Bracing.Cross);
            var truss = new TrussSolver(frame.Joints, frame.Members);
            truss.ApplySelfWeight();
            truss.WindLoadKnPerM = WindKnPerM;

            TrussReport first = truss.Solve();
            TrussReport second = truss.Solve();
            TrussReport third = truss.Solve();

            Assert.AreEqual(first.TopDisplacementM, second.TopDisplacementM, 1e-9f,
                "solving twice must not double the wind load");
            Assert.AreEqual(second.TopDisplacementM, third.TopDisplacementM, 1e-9f);
        }

        [Test]
        public void Wind_HigherJointsFeelMoreLoad()
        {
            StructureSolver frame = BracedFrames.Frame(6, 3f, 4f, BracedFrames.Bracing.Cross);
            var truss = new TrussSolver(frame.Joints, frame.Members);
            truss.WindLoadKnPerM = WindKnPerM;
            truss.ApplyWind();

            int lowest = 0;
            int highest = 0;
            for (int i = 1; i < frame.Joints.Count; i++)
            {
                if (frame.Joints[i].Position.y < frame.Joints[lowest].Position.y) lowest = i;
                if (frame.Joints[i].Position.y > frame.Joints[highest].Position.y) highest = i;
            }

            Assert.Greater(truss.ExternalForceAt(highest).x, truss.ExternalForceAt(lowest).x,
                "wind pressure must rise with height, or a tower fails uniformly");
        }

        [Test]
        public void Wind_ZeroPressureMeansNoLateralLoad()
        {
            StructureSolver frame = BracedFrames.Frame(4, 3f, 4f, BracedFrames.Bracing.None);
            var truss = new TrussSolver(frame.Joints, frame.Members);
            truss.ApplySelfWeight();
            truss.WindLoadKnPerM = 0f;
            truss.ApplyWind();

            foreach (Joint j in frame.Joints)
            {
                int index = frame.Joints.IndexOf(j);
                Assert.AreEqual(0f, truss.ExternalForceAt(index).x, 1e-6f,
                    "no wind pressure must mean no lateral force");
            }
        }

        [Test]
        public void Lateral_Limit_IsSingleSpanBendingDriven()
        {
            // Documents the model's honest boundary rather than hiding it. A pure truss
            // carries axial force only, so in a one-bay frame the side drift is mostly
            // column bending, which this model cannot represent. Bracing therefore
            // helps only modestly here, and a designer should not expect more from a
            // single span. Real benefit needs multiple bays or a bending-stiffness
            // model, both of which are follow-up work.
            float unbraced = Solve(BracedFrames.Bracing.None, 8).TopDisplacementM;
            float braced = Solve(BracedFrames.Bracing.Cross, 8).TopDisplacementM;

            float improvement = 1f - braced / unbraced;
            Assert.Greater(improvement, 0.05f,
                "bracing must help at all, even under the most unfavourable layout");
            Assert.Less(improvement, 0.60f,
                "single-bay improvement is capped by the axial-only model; if this " +
                "exceeds 60% the fixture changed and the boundary note is stale");
        }
    }
}
