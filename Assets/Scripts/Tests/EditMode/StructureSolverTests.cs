using CrazyAquarium.Samples;
using CrazyAquarium.Structure;
using NUnit.Framework;
using UnityEngine;

namespace CrazyAquarium.Tests
{
    /// <summary>
    /// Feasibility tests for the core claim of the design: a vertical floating
    /// structure can be modelled deterministically in engine-free C#, tall stacks
    /// load their base harder than short ones, overstressed members cascade, and an
    /// off-centre mass capsizes the structure.
    ///
    /// If these pass, the physics-built-city pillar is viable without a bespoke
    /// physics engine. If LoadFlow or Cascade fails, the project needs a different core.
    ///
    /// This file runs both inside Unity's EditMode runner and under plain dotnet test.
    /// The second path matters: it lets the simulation be verified without paying the
    /// editor's memory cost, so balance and load-flow changes can be iterated in
    /// seconds rather than minutes.
    /// </summary>
    public sealed class StructureSolverTests
    {
        [Test]
        public void Solver_ProducesResultsWithoutAScene()
        {
            StructureSolver solver = Structures.Raft();
            SolveReport report = solver.Solve();

            Assert.Greater(solver.Members.Count, 0);
            Assert.Greater(report.TotalMassKg, 0f);
        }

        [Test]
        public void Buoyancy_PlasticPontoonsFloatTheRaft()
        {
            SolveReport report = Structures.Raft(pontoon: MaterialKind.Plastic).Solve();

            Assert.Greater(report.BuoyancyKn, 0f, "plastic is lighter than water and must generate lift");
            Assert.IsTrue(report.IsFloating,
                $"net force {report.NetVerticalForceKn:F2} kN, expected the raft to float");
        }

        [Test]
        public void Buoyancy_SteelPontoonsSinkTheRaft()
        {
            SolveReport report = Structures.Raft(pontoon: MaterialKind.Steel).Solve();

            Assert.Greater(report.WeightKn, report.BuoyancyKn,
                "steel is denser than water and cannot hold the raft up");
            Assert.IsFalse(report.IsFloating);
        }

        [Test]
        public void LoadFlow_LoadFallsOffWithHeight()
        {
            StructureSolver solver = Structures.Tower(levels: 8);
            solver.Solve();
            float[] loads = Structures.ColumnLoads(solver, 8);

            for (int i = 1; i < loads.Length; i++)
            {
                Assert.Less(loads[i], loads[i - 1],
                    $"storey {i} ({loads[i]:F2} kN) must carry less than storey {i - 1} ({loads[i - 1]:F2} kN)");
            }
        }

        [Test]
        public void LoadFlow_BaseScalesWithTowerHeight()
        {
            float shortBase = BaseLoad(2);
            float mediumBase = BaseLoad(8);
            float tallBase = BaseLoad(16);

            Assert.Greater(mediumBase, shortBase * 2f,
                $"8 storeys ({mediumBase:F1} kN) must load the base well past 2 storeys ({shortBase:F1} kN)");
            Assert.Greater(tallBase, mediumBase,
                $"16 storeys ({tallBase:F1} kN) must exceed 8 storeys ({mediumBase:F1} kN)");
        }

        [Test]
        public void LoadFlow_AnchoredBaseReceivesTheFullWeight()
        {
            StructureSolver solver = Structures.Tower(levels: 10);
            SolveReport report = solver.Solve();

            float baseLoad = Structures.ColumnLoads(solver, 10)[0];
            float totalWeight = report.WeightKn;

            // The base column must carry essentially the whole tower. This is the
            // assertion that catches the anchor-skipping bug where every base
            // column read a token 0.4 kN regardless of height.
            Assert.Greater(baseLoad, totalWeight * 0.8f,
                $"base carries {baseLoad:F1} kN but the tower weighs {totalWeight:F1} kN");
        }

        [Test]
        public void Bending_LevelBeamsBendUnderTheirOwnWeight()
        {
            StructureSolver solver = Structures.Tower(levels: 6);
            solver.Solve();

            // Members are added in pairs: even indices are columns, odd are beams.
            float beamBending = solver.Members[1].BendingMomentKnM;
            Assert.Greater(beamBending, 0f, "a deck beam must bend under what rests on it");
        }

        [Test]
        public void Bending_CantileverBeamsCarryMomentWithoutAxialLoad()
        {
            StructureSolver solver = Structures.Tower(levels: 6);
            solver.Solve();

            Member beam = solver.Members[1];
            Assert.IsTrue(beam.IsCantilever, "the deck beam fixture must be a cantilever");
            Assert.AreEqual(0f, beam.AxialLoadKn, 1e-4f,
                "a cantilever's far end is a free tip, so it carries no axial load");

            // Without a dedicated cantilever moment this reads zero forever, which
            // would let a player hang an unbounded deck off a single joint.
            Assert.Greater(beam.BendingMomentKnM, 0f,
                "a cantilever must bend under its own weight even with nothing on its tip");
            Assert.Greater(beam.Utilization, 0f,
                "a cantilever beam's strength check cannot be permanently bypassed");
        }

        [Test]
        public void Bending_LongerCantileverBendsMoreThanShorter()
        {
            StructureSolver shortArm = Structures.Tower(levels: 3, deckArmLength: 1.5f);
            StructureSolver longArm = Structures.Tower(levels: 3, deckArmLength: 6f);
            shortArm.Solve();
            longArm.Solve();

            float shortBend = shortArm.Members[1].BendingMomentKnM;
            float longBend = longArm.Members[1].BendingMomentKnM;

            // Self-weight scales with L and the lever arm scales with L, so the
            // moment goes as L^2: 4x the arm gives 16x the moment.
            Assert.Greater(longBend, shortBend * 12f,
                $"a 6 m arm ({longBend:F2} kN*m) must bend far more than a 1.5 m arm ({shortBend:F2} kN*m)");
            Assert.Less(longBend, shortBend * 20f,
                "and it must stay quadratic, not explode faster than L^2");
        }

        [Test]
        public void Bending_TipLoadIncreasesCantileverMoment()
        {
            StructureSolver bare = Structures.Tower(levels: 3, deckArmLength: 3f);
            StructureSolver loaded = Structures.Tower(levels: 3, deckArmLength: 3f);

            // Hang a heavy column off the first deck tip. It has to connect to the
            // tip joint itself, otherwise its mass never reaches the cantilever.
            int tipJoint = bare.Members[1].JointB;
            int tipIndex = loaded.Joints.FindIndex(j =>
                j.Position == bare.JointAt(tipJoint).Position);
            Assert.GreaterOrEqual(tipIndex, 0, "the two towers must share a layout up to the tip");

            Vector3 tip = loaded.JointAt(tipIndex).Position;
            int foot = loaded.AddJoint(tip + new Vector3(0f, -2f, 0f));
            loaded.AddMember(tipIndex, foot, MaterialKind.Steel, 0.09f);

            bare.Solve();
            loaded.Solve();

            Assert.Greater(loaded.Members[1].BendingMomentKnM, bare.Members[1].BendingMomentKnM,
                "a load hanging off the tip must add moment to the cantilever");
        }

        [Test]
        public void Bending_HeavierCantileverMaterialBendsMoreUnderItsOwnWeight()
        {
            StructureSolver wood = Structures.Tower(levels: 4, deck: MaterialKind.Wood);
            StructureSolver concrete = Structures.Tower(levels: 4, deck: MaterialKind.Concrete);
            wood.Solve();
            concrete.Solve();

            Assert.Greater(concrete.Members[1].BendingMomentKnM, wood.Members[1].BendingMomentKnM,
                "a denser beam bends more under the same cantilever arm");

            // Higher moment but not proportionally higher capacity. Wood wins on
            // bending strength-to-weight here, so concrete is further from its limit
            // as a cantilever yet further from its limit overall once the denser
            // column stack below is counted. That trade-off between the arm and the
            // supporting structure is the actual design space.
            Assert.Greater(concrete.Members[1].Utilization, wood.Members[1].Utilization,
                "concrete's arm bends more than wood's and so must sit closer to its own limit");

            int woodFailures = wood.SolveToEquilibrium().FailedMemberCount;
            int concreteFailures = concrete.SolveToEquilibrium().FailedMemberCount;
            Assert.LessOrEqual(concreteFailures, woodFailures,
                "the stiffer deck must not make the 4-storey tower collapse more often");
        }

        [Test]
        public void Cascade_WeakMaterialUnderLoadFails()
        {
            SolveReport report = Structures.Tower(levels: 20, column: MaterialKind.Plastic)
                                        .SolveToEquilibrium();

            Assert.Greater(report.FailedMemberCount, 0,
                "a 20-storey plastic tower must not survive");
        }

        [Test]
        public void Cascade_CollapseSpansMultipleIterations()
        {
            SolveReport report = Structures.Tower(levels: 20, column: MaterialKind.Plastic)
                                        .SolveToEquilibrium();

            Assert.Greater(report.IterationCount, 1,
                "collapse must cascade across iterations, not resolve in a single pass");
            Assert.Greater(report.FailedMemberCount, 1,
                "one overloaded member should not take the entire tower down at once");
        }

        [Test]
        public void Cascade_MaterialChoiceMattersAtScale()
        {
            // Not a total order, and deliberately not asserted as one. Steel is the
            // strongest material per unit area but 13x denser than wood, so on a fixed
            // cross-section it builds a heavier tower and can fail where wood stands.
            // Real strength-to-weight ratios are not monotonic either: concrete beats
            // both. The design implication is that cross-section, not material, is the
            // primary lever, and material picks are about weight vs absolute strength.
            int plasticFailures = Structures.Tower(20, 3f, MaterialKind.Plastic).SolveToEquilibrium().FailedMemberCount;
            int woodFailures = Structures.Tower(20, 3f, MaterialKind.Wood).SolveToEquilibrium().FailedMemberCount;
            int steelFailures = Structures.Tower(20, 3f, MaterialKind.Steel).SolveToEquilibrium().FailedMemberCount;

            Assert.Less(woodFailures, plasticFailures,
                "wood must outperform plastic on an identical structure");
            Assert.Greater(steelFailures, woodFailures,
                "steel is 13x denser, so on a fixed section it must build a heavier, worse tower");
        }

        [Test]
        public void Cascade_StrengthToWeightRatioIsNotMonotonic()
        {
            // A finding worth pinning: concrete has the best strength-to-weight of the
            // four, beating both wood and steel. If a later tuning pass makes the
            // materials strictly ordered, this test is the reminder that the current
            // values are the ones that produced a physically interesting trade-off.
            float wood = MaterialProperties.AxialCapacityKnPerM2(MaterialKind.Wood)
                         / MaterialProperties.DensityKgPerM3(MaterialKind.Wood);
            float steel = MaterialProperties.AxialCapacityKnPerM2(MaterialKind.Steel)
                          / MaterialProperties.DensityKgPerM3(MaterialKind.Steel);
            float concrete = MaterialProperties.AxialCapacityKnPerM2(MaterialKind.Concrete)
                             / MaterialProperties.DensityKgPerM3(MaterialKind.Concrete);

            Assert.Greater(concrete, steel,
                "concrete must beat steel on strength-to-weight");
            Assert.Greater(concrete, wood,
                "concrete must beat wood on strength-to-weight");
        }

        [Test]
        public void CrossSection_WiderMembersAreStrongerNotHeavier()
        {
            // Guards a real regression. Capacities used to be flat constants per
            // material, so a 0.25 m^2 column was exactly as strong as a 0.05 m^2 one
            // while weighing five times as much, and widening a column made the
            // structure fail. Capacity must scale with cross-section.
            int previousFailures = int.MaxValue;
            Member widest = null;

            foreach (float area in new[] { 0.05f, 0.09f, 0.15f, 0.25f, 0.4f })
            {
                StructureSolver solver = Structures.Tower(12, 3f, MaterialKind.Wood,
                    MaterialKind.Wood, 1.5f, area, 0.05f);
                int failures = solver.SolveToEquilibrium().FailedMemberCount;

                Assert.LessOrEqual(failures, previousFailures,
                    $"widening columns to {area} m^2 must not increase failures");
                previousFailures = failures;
                widest = solver.Members[0];
            }

            Assert.Greater(widest.AxialCapacityKn, 0f);
        }

        [Test]
        public void CrossSection_CapacityScalesWithArea()
        {
            Member thin = new Member { Material = MaterialKind.Steel, CrossSectionAreaM2 = 0.05f };
            Member thick = new Member { Material = MaterialKind.Steel, CrossSectionAreaM2 = 0.20f };

            Assert.AreEqual(4f, thick.AxialCapacityKn / thin.AxialCapacityKn, 0.01f,
                "axial capacity must scale linearly with cross-section");

            // Moment goes as area^1.5 through the section modulus, so 4x the area
            // gives 8x the bending capacity.
            Assert.AreEqual(8f, thick.BendingCapacityKnM / thin.BendingCapacityKnM, 0.1f,
                "bending capacity must scale with the section modulus");
        }

        [Test]
        public void Cascade_ShortWeakTowerStillStands()
        {
            // A plastic column is the weakest in the set at 20 kN axial, and the base
            // of any tower carries the most. Two storeys must still be buildable, or
            // the material curve is unusable as a starting point for players.
            SolveReport report = Structures.Tower(levels: 2, column: MaterialKind.Plastic)
                                        .SolveToEquilibrium();

            Assert.AreEqual(0, report.FailedMemberCount,
                "a 2-storey plastic structure must be buildable, or the model is too harsh to play");
        }

        [Test]
        public void Cascade_StockWoodTowerStandsAtReasonableHeight()
        {
            // The brief's opening raft has to be buildable out of the default
            // material, or the very first structure in the game collapses.
            SolveReport report = Structures.Tower(levels: 4).SolveToEquilibrium();

            Assert.AreEqual(0, report.FailedMemberCount,
                $"the default 4-storey wood tower must stand, but {report.FailedMemberCount} members failed");
        }

        [Test]
        public void Capsize_OffCentreMassIsDetected()
        {
            SolveReport report = Structures.LopsidedRaft(offsetX: 6f).Solve();

            Assert.Greater(report.CapsizeOffsetM, StructureSolver.CapsizeOffsetToleranceM,
                $"offset was {report.CapsizeOffsetM:F2} m, expected capsize");
            Assert.IsTrue(report.IsCapsizing);
        }

        [Test]
        public void Capsize_SymmetricRaftSitsLevel()
        {
            SolveReport report = Structures.Raft().Solve();

            Assert.Less(report.CapsizeOffsetM, StructureSolver.CapsizeOffsetToleranceM,
                "a symmetric raft must sit level");
            Assert.IsFalse(report.IsCapsizing);
        }

        [Test]
        public void Determinism_IdenticalInputsGiveIdenticalResults()
        {
            SolveReport first = Structures.Tower(levels: 6).SolveToEquilibrium();
            SolveReport second = Structures.Tower(levels: 6).SolveToEquilibrium();

            Assert.AreEqual(first.FailedMemberCount, second.FailedMemberCount);
            Assert.AreEqual(first.TotalMassKg, second.TotalMassKg, 1e-3f);
            Assert.AreEqual(first.CenterOfMass.x, second.CenterOfMass.x, 1e-5f);
            Assert.AreEqual(first.CenterOfMass.y, second.CenterOfMass.y, 1e-5f);
        }

        [Test]
        public void Tunability_LoadScaleMovesTheFailureThreshold()
        {
            int previous = -1;

            foreach (float scale in new[] { 0.5f, 1f, 2f, 4f, 8f })
            {
                StructureSolver solver = Structures.Tower(levels: 8, column: MaterialKind.Wood);
                solver.LoadScale = scale;
                int failures = solver.SolveToEquilibrium().FailedMemberCount;

                Assert.GreaterOrEqual(failures, previous,
                    $"LoadScale {scale} must not collapse fewer members than {previous}");
                previous = failures;
            }

            Assert.Greater(previous, 0, "the dial must reach a regime where the structure fails");
        }

        [Test]
        public void Tunability_BuoyancyScaleChangesTheFloatingThreshold()
        {
            StructureSolver lean = Structures.LopsidedRaft(2f);
            StructureSolver fat = Structures.LopsidedRaft(2f);
            fat.BuoyancyScale = 2.5f;

            SolveReport leanReport = lean.Solve();
            SolveReport fatReport = fat.Solve();

            Assert.Greater(fatReport.BuoyancyKn, leanReport.BuoyancyKn,
                "raising buoyancy must increase lift, it is the pontoon-count dial");
        }

        private static float BaseLoad(int levels)
        {
            StructureSolver solver = Structures.Tower(levels: levels);
            solver.Solve();
            return Structures.ColumnLoads(solver, levels)[0];
        }
    }
}

