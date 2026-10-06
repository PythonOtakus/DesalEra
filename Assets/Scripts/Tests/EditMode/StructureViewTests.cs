using DesalEra.Structure;
using DesalEra.Unity;
using NUnit.Framework;
using UnityEngine;

namespace DesalEra.Tests
{
    /// <summary>
    /// Tests for the presentation layer. The solver is covered elsewhere; what matters
    /// here is that the view reports the solver's answer faithfully and holds no
    /// simulation state of its own.
    /// </summary>
    public sealed class StructureViewTests
    {
        [Test]
        public void Palette_RedChannelRisesMonotonically()
        {
            // Guards the ramp's direction. An earlier palette had amber at red 0.85
            // and critical at red 0.78, so a beam became visibly *less* alarming as it
            // neared failure. The whole readout depends on red rising with load.
            var view = CreateView();

            Assert.Less(view.IntactColor.r, view.StrainedColor.r,
                "amber must be redder than the intact colour");
            Assert.Less(view.StrainedColor.r, view.CriticalColor.r,
                "critical must be redder than amber, or the ramp reads backwards");
        }

        [Test]
        public void ColorFor_LowUtilizationIsIntact()
        {
            var view = CreateView();
            var member = new Member { Material = MaterialKind.Steel, CrossSectionAreaM2 = 0.09f };

            Color color = view.ColorFor(member);
            Assert.AreEqual(view.IntactColor.r, color.r, 0.01f,
                "an unloaded member must read as fully intact");
            Assert.AreEqual(view.IntactColor.g, color.g, 0.01f);
        }

        [Test]
        public void ColorFor_UtilizationRampsTowardFailure()
        {
            var view = CreateView();
            var member = new Member { Material = MaterialKind.Steel, CrossSectionAreaM2 = 0.09f };

            Color at0 = view.ColorFor(member);

            member.AxialLoadKn = member.AxialCapacityKn * 0.3f;
            Color at3 = view.ColorFor(member);

            member.AxialLoadKn = member.AxialCapacityKn * 0.9f;
            Color at9 = view.ColorFor(member);

            // Sampled inside the ramp rather than past its end. Both ends of the range
            // sit at the critical colour, so asserting between 0.9 and 1.2 compares two
            // identical values and passes or fails for no reason.
            Assert.Greater(at3.r, at0.r, "a loaded member must read redder than an intact one");
            Assert.Greater(at9.r, at3.r, "and redder still as it approaches the limit");
        }

        [Test]
        public void ColorFor_OverCapacityIsClampedNotWrapped()
        {
            var view = CreateView();
            var member = new Member { Material = MaterialKind.Wood, CrossSectionAreaM2 = 0.09f };

            member.AxialLoadKn = member.AxialCapacityKn * 50f;
            Color extreme = view.ColorFor(member);

            member.AxialLoadKn = member.AxialCapacityKn * 0.999f;
            Color justUnder = view.ColorFor(member);

            Assert.AreEqual(extreme.r, justUnder.r, 0.001f,
                "utilization past 1.0 must clamp to the critical colour, not wrap");
        }

        [Test]
        public void Rebuild_ThrowsOnNullSolver()
        {
            var view = CreateView();
            Assert.Throws<System.ArgumentNullException>(() => view.Rebuild(null, null));
        }

        [Test]
        public void Rebuild_SpawnsOneObjectPerLiveMember()
        {
            var host = new GameObject("viewHost");
            try
            {
                StructureSolver solver = DesalEra.Samples.Structures.Tower(levels: 4);
                SolveReport report = solver.Solve();

                var view = host.AddComponent<StructureView>();
                view.Rebuild(solver, report);

                Assert.AreEqual(8, view.RenderedMemberCount,
                    "a 4-storey tower has 4 columns plus 4 cantilevered decks");
                Assert.AreEqual(8, CountMemberChildren(view.transform), "and 8 objects in the graph");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Rebuild_SkipsFailedMembers()
        {
            var host = new GameObject("viewHost");
            try
            {
                StructureSolver solver = DesalEra.Samples.Structures.Tower(
                    levels: 20, column: MaterialKind.Plastic);
                SolveReport report = solver.SolveToEquilibrium();

                Assert.Greater(report.FailedMemberCount, 0, "fixture must actually collapse");

                var view = host.AddComponent<StructureView>();
                view.Rebuild(solver, report);

                Assert.AreEqual(
                    solver.Members.Count - report.FailedMemberCount,
                    view.RenderedMemberCount,
                    "collapsed members must not be drawn");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Rebuild_IsIdempotent()
        {
            var host = new GameObject("viewHost");
            try
            {
                StructureSolver solver = DesalEra.Samples.Structures.Tower(levels: 5);
                SolveReport report = solver.Solve();

                var view = host.AddComponent<StructureView>();
                view.Rebuild(solver, report);
                view.Rebuild(solver, report);
                view.Rebuild(solver, report);

                Assert.AreEqual(10, view.RenderedMemberCount,
                    "rebuilding must replace, not accumulate, member objects");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Prototype_ReportsFailuresForAnImpossibleBuild()
        {
            var host = new GameObject("protoHost");
            try
            {
                var proto = host.AddComponent<StructurePrototype>();
                SetPrivate(proto, "levels", 30);
                SetPrivate(proto, "columnMaterial", MaterialKind.Plastic);
                SetPrivate(proto, "deckMaterial", MaterialKind.Plastic);
                proto.Rebuild();

                Assert.Greater(proto.Report.FailedMemberCount, 0,
                    "a 30-storey plastic tower cannot stand");
                StringAssert.Contains("failed", proto.DescribeLastSolve());
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Prototype_KeepsAReasonableWoodTowerStanding()
        {
            var host = new GameObject("protoHost");
            try
            {
                var proto = host.AddComponent<StructurePrototype>();
                SetPrivate(proto, "levels", 4);
                SetPrivate(proto, "columnMaterial", MaterialKind.Wood);
                SetPrivate(proto, "deckMaterial", MaterialKind.Wood);
                proto.Rebuild();

                Assert.AreEqual(0, proto.Report.FailedMemberCount,
                    "the default build must stand, or the opening structure of the game collapses");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Prototype_MaterialComparisonCoversEveryMaterial()
        {
            var host = new GameObject("protoHost");
            try
            {
                var proto = host.AddComponent<StructurePrototype>();
                SolveReport[] reports = proto.BuildMaterialComparison(12);

                Assert.AreEqual(4, reports.Length);
                foreach (SolveReport report in reports)
                {
                    Assert.IsNotNull(report);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Prototype_LoadScaleDialChangesTheOutcome()
        {
            var host = new GameObject("protoHost");
            try
            {
                var proto = host.AddComponent<StructurePrototype>();
                SetPrivate(proto, "levels", 10);
                SetPrivate(proto, "columnMaterial", MaterialKind.Wood);
                SetPrivate(proto, "deckMaterial", MaterialKind.Wood);
                SetPrivate(proto, "loadScale", 1f);
                proto.Rebuild();
                int gentle = proto.Report.FailedMemberCount;

                SetPrivate(proto, "loadScale", 6f);
                proto.Rebuild();
                int harsh = proto.Report.FailedMemberCount;

                Assert.Greater(harsh, gentle, "the inspector dial must be wired to the solver");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static StructureView CreateView()
        {
            var host = new GameObject("viewHost");
            // Kept alive by the test's own host reference; destroyed with the scene.
            return host.AddComponent<StructureView>();
        }

        private static int CountMemberChildren(Transform parent)
        {
            int n = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).GetComponent<StructureMemberTag>() != null) n++;
            }
            return n;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            System.Reflection.FieldInfo info = target.GetType()
                .GetField(field, System.Reflection.BindingFlags.Instance
                                   | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"field {field} not found on {target.GetType().Name}");
            info.SetValue(target, value);
        }
    }
}
