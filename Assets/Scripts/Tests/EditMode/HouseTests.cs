using DesalEra.Game;
using DesalEra.Structure;
using NUnit.Framework;
using UnityEngine;

namespace DesalEra.Tests
{
    /// <summary>
    /// Phase two of shelter building: walls, floors, stairs, the placement preview and
    /// the collapse chain. See docs/shelter-building.md.
    /// </summary>
    public sealed class HouseTests
    {
        private static readonly Vector2Int SW = new Vector2Int(-1, -1);

        private static readonly Vector2Int[] SquareCorners =
        {
            new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(0, 0)
        };

        private static RaftState RichRaft()
        {
            var raft = new RaftState();
            raft.Inventory.Add(ResourceKind.Plank, 200);
            raft.Inventory.Add(ResourceKind.Scrap, 200);
            raft.Inventory.Add(ResourceKind.Metal, 200);
            return raft;
        }

        private static RaftState RaftWithFloor()
        {
            RaftState raft = RichRaft();
            foreach (Vector2Int cell in SquareCorners)
                Assert.IsNull(raft.TryPlace(BuildPiece.Column(), cell), $"column at {cell}");
            Assert.IsNull(raft.TryPlace(BuildPiece.Roof(), SW, level: 1));
            return raft;
        }

        private static float WindSumX(RaftState raft)
        {
            raft.WindLoadKnPerM = 5f;
            raft.SolveWind();
            float sum = 0f;
            for (int i = 0; i < raft.Gravity.Joints.Count; i++) sum += raft.Truss.ExternalForceAt(i).x;
            return sum;
        }

        // --- walls ---

        [Test]
        public void WallNeedsAColumnAtBothEnds()
        {
            RaftState raft = RichRaft();
            Assert.IsNull(raft.TryPlace(BuildPiece.Column(), SW));
            Assert.AreEqual("a wall needs a column at both ends",
                raft.TryPlace(BuildPiece.Wall(), SW, 0, facing: 0));

            Assert.IsNull(raft.TryPlace(BuildPiece.Column(), new Vector2Int(0, -1)));
            Assert.IsNull(raft.TryPlace(BuildPiece.Wall(), SW, 0, facing: 0));
        }

        [Test]
        public void WestFacingWallIsTheSamePanelAsTheEastFacingNeighbour()
        {
            RaftState raft = RaftWithFloor();
            Assert.IsNull(raft.TryPlace(BuildPiece.Wall(), SW, 0, facing: 0));
            Assert.AreEqual("that platform is already occupied",
                raft.TryPlace(BuildPiece.Wall(), new Vector2Int(0, -1), 0, facing: 2));
        }

        [Test]
        public void WallFacingTheWindCatchesItsFullArea()
        {
            RaftState bare = RaftWithFloor();
            RaftState walled = RaftWithFloor();
            // A north-running wall faces east-west, square to the default +X wind.
            Assert.IsNull(walled.TryPlace(BuildPiece.Wall(), SW, 0, facing: 1));

            float expected = 5f / TrussSolver.PanelBayWidthM * RaftState.CellSize * RaftState.StoreyHeightM;
            Assert.AreEqual(expected, WindSumX(walled) - WindSumX(bare), 0.01f);
        }

        [Test]
        public void WallEdgeOnToTheWindCatchesNothing()
        {
            RaftState bare = RaftWithFloor();
            RaftState walled = RaftWithFloor();
            Assert.IsNull(walled.TryPlace(BuildPiece.Wall(), SW, 0, facing: 0));
            Assert.AreEqual(WindSumX(bare), WindSumX(walled), 0.01f);
        }

        [Test]
        public void RepeatSolvesDoNotStackPanelWind()
        {
            RaftState raft = RaftWithFloor();
            Assert.IsNull(raft.TryPlace(BuildPiece.Wall(), SW, 0, facing: 1));
            float first = WindSumX(raft);
            Assert.AreEqual(first, WindSumX(raft), 0.01f);
        }

        // --- floors and stairs ---

        [Test]
        public void RoofDoublesAsAFloor()
        {
            RaftState raft = RaftWithFloor();
            var centre = new Vector3(-1.5f, 0f, -1.5f);
            float floorY = RaftState.BaseDeckY + RaftState.StoreyHeightM + RaftState.FloorTopAboveJointM;

            Assert.IsTrue(raft.TryGetRaisedSurface(centre, 100f, out float y));
            Assert.AreEqual(floorY, y, 1e-3f);
            Assert.IsFalse(raft.TryGetRaisedSurface(centre, RaftState.BaseDeckY + RaftState.StepUpM, out _),
                "walking under the floor must not lift the survivor onto it");
        }

        [Test]
        public void StairsNeedAColumnTopToClimbTo()
        {
            RaftState raft = RichRaft();
            Assert.AreEqual("stairs need a column top to climb to",
                raft.TryPlace(BuildPiece.Stairs(), new Vector2Int(1, -1), 0, facing: 2));
            Assert.IsNull(raft.TryPlace(BuildPiece.Column(), new Vector2Int(0, -1)));
            Assert.IsNull(raft.TryPlace(BuildPiece.Stairs(), new Vector2Int(1, -1), 0, facing: 2));
        }

        [Test]
        public void StairsRampClimbsOneStorey()
        {
            RaftState raft = RaftWithFloor();
            Assert.IsNull(raft.TryPlace(BuildPiece.Stairs(), new Vector2Int(1, -1), 0, facing: 2));

            float foot = RaftState.BaseDeckY + RaftState.StairTreadAboveLineM;
            float step = RaftState.StoreyHeightM / RaftState.CellSize;

            Assert.IsTrue(raft.TryGetRaisedSurface(new Vector3(3f, 0f, -3f), foot, out float atFoot));
            Assert.AreEqual(foot, atFoot, 1e-3f);
            Assert.IsTrue(raft.TryGetRaisedSurface(new Vector3(1.5f, 0f, -3.3f), 100f, out float mid));
            Assert.AreEqual(foot + 1.5f * step, mid, 1e-3f);
            Assert.IsFalse(raft.TryGetRaisedSurface(new Vector3(1.5f, 0f, -4.5f), 100f, out _),
                "beside the flight is not on it");
        }

        [Test]
        public void TopOfTheStairsIsWithinAStepOfTheFloor()
        {
            float head = RaftState.BaseDeckY + RaftState.StoreyHeightM + RaftState.StairTreadAboveLineM;
            float floor = RaftState.BaseDeckY + RaftState.StoreyHeightM + RaftState.FloorTopAboveJointM;
            Assert.LessOrEqual(floor - head, RaftState.StepUpM);
            float deck = RaftState.BaseDeckY + 0.07f;
            Assert.LessOrEqual(RaftState.BaseDeckY + RaftState.StairTreadAboveLineM - deck, RaftState.StepUpM);
        }

        [Test]
        public void UpperRoofSheltersTheFirstFloor()
        {
            RaftState raft = RaftWithFloor();
            var onFloor = new Vector3(-1.5f, RaftState.BaseDeckY + RaftState.StoreyHeightM + RaftState.FloorTopAboveJointM, -1.5f);
            Assert.IsFalse(raft.IsSheltered(onFloor), "the floor underfoot is not a roof overhead");

            foreach (Vector2Int cell in SquareCorners)
                Assert.IsNull(raft.TryPlace(BuildPiece.Column(), cell, level: 1), $"upper column at {cell}");
            Assert.IsNull(raft.TryPlace(BuildPiece.Roof(), SW, level: 2));
            Assert.IsTrue(raft.IsSheltered(onFloor));
        }

        // --- preview ---

        [Test]
        public void CanPlaceChangesNothing()
        {
            RaftState raft = RichRaft();
            int planks = raft.Inventory.Get(ResourceKind.Plank);
            int members = raft.MemberCount;

            Assert.IsNull(raft.CanPlace(BuildPiece.Column(), SW, 0, 0, out PlacementPlan plan));
            Assert.AreEqual(1, plan.Segments.Count);
            Assert.AreEqual(planks, raft.Inventory.Get(ResourceKind.Plank));
            Assert.AreEqual(members, raft.MemberCount);
        }

        [Test]
        public void RefusedPlacementStillHasAPlan()
        {
            RaftState raft = RichRaft();
            Assert.AreEqual("a roof needs a column at every corner",
                raft.CanPlace(BuildPiece.Roof(), SW, 1, 0, out PlacementPlan plan));
            Assert.IsNotNull(plan, "the preview draws a refused piece in red, so it needs the geometry");
            Assert.AreEqual(2, plan.Segments.Count);
        }

        [Test]
        public void CanPlaceReportsShortResources()
        {
            var raft = new RaftState();
            raft.Inventory.Clear();
            Assert.AreEqual("not enough resources",
                raft.CanPlace(BuildPiece.Deck(), new Vector2Int(1, 0), 0, 0, out _));
        }

        // --- collapse ---

        [Test]
        public void RemovingAColumnDropsWhatItHeld()
        {
            RaftState raft = RaftWithFloor();
            Assert.IsNull(raft.TryPlace(BuildPiece.Column(), new Vector2Int(0, 0), level: 1));

            Assert.IsNull(raft.TryDismantle(new Vector2Int(0, 0)));
            Assert.AreEqual(2, raft.LastCollapsed, "the floor, then the column standing on it");
            Assert.IsFalse(raft.IsSheltered(new Vector3(-1.5f, RaftState.BaseDeckY, -1.5f)));
            Assert.IsFalse(raft.IsOccupied(new PlacementKey(SW, 1, RaftState.RoofSocket)));
        }

        [Test]
        public void CollapsedPiecesAreNotRefunded()
        {
            RaftState raft = RaftWithFloor();
            int before = raft.Inventory.Get(ResourceKind.Plank);

            var probe = new Inventory();
            probe.RefundFraction(BuildPiece.Column().Cost);

            Assert.IsNull(raft.TryDismantle(new Vector2Int(0, 0)));
            Assert.AreEqual(1, raft.LastCollapsed);
            Assert.AreEqual(before + probe.Get(ResourceKind.Plank), raft.Inventory.Get(ResourceKind.Plank),
                "only the dismantled column refunds; the roof that fell is lost");
        }

        [Test]
        public void WallFallsWithItsColumn()
        {
            RaftState raft = RaftWithFloor();
            Assert.IsNull(raft.TryPlace(BuildPiece.Wall(), SW, 0, facing: 0));
            Assert.IsNull(raft.TryDismantle(new Vector2Int(0, -1)));
            Assert.IsFalse(raft.IsOccupied(new PlacementKey(SW, 0, RaftState.WallXSocket)));
        }

        [Test]
        public void PieceBrokenByWindIsClearedForRebuilding()
        {
            RaftState raft = RaftWithFloor();
            Assert.IsNull(raft.TryPlace(BuildPiece.Wall(), SW, 0, facing: 0));
            raft.Gravity.Members[raft.MemberCount - 1].IsFailed = true;

            Assert.AreEqual(1, raft.CollapseUnsupported());
            Assert.IsNull(raft.TryPlace(BuildPiece.Wall(), SW, 0, facing: 0), "the socket is free again");
        }

        [Test]
        public void StandingStructureDoesNotCollapse()
        {
            RaftState raft = RaftWithFloor();
            Assert.IsNull(raft.TryPlace(BuildPiece.Wall(), SW, 0, facing: 1));
            Assert.IsNull(raft.TryPlace(BuildPiece.Stairs(), new Vector2Int(1, -1), 0, facing: 2));
            Assert.AreEqual(0, raft.CollapseUnsupported());
        }

        // --- the whole house ---

        [Test]
        public void TwoStoreyHouseFloatsAndStandsInCalm()
        {
            RaftState raft = RaftWithFloor();
            Assert.IsNull(raft.TryPlace(BuildPiece.Wall(), SW, 0, facing: 0));
            Assert.IsNull(raft.TryPlace(BuildPiece.Wall(), SW, 0, facing: 1));
            Assert.IsNull(raft.TryPlace(BuildPiece.Wall(), new Vector2Int(-1, 0), 0, facing: 0));
            Assert.IsNull(raft.TryPlace(BuildPiece.Stairs(), new Vector2Int(1, -1), 0, facing: 2));
            foreach (Vector2Int cell in SquareCorners)
                Assert.IsNull(raft.TryPlace(BuildPiece.Column(), cell, level: 1));
            Assert.IsNull(raft.TryPlace(BuildPiece.Roof(), SW, level: 2));

            Assert.IsTrue(raft.SolveBuoyancy().IsFloating, "a two-storey house must not sink the starting raft");

            raft.WindLoadKnPerM = 1.2f;
            TrussReport report = raft.SolveWind();
            Assert.IsTrue(report.Converged, report.FailureReason);
            Assert.AreEqual(0, raft.CollapseUnsupported());
            Assert.AreEqual(0, raft.CountFailedPieces(), Describe(raft));
        }

        // --- structure ---

        [Test]
        public void ColumnOnABeamSplitsIt()
        {
            RaftState raft = RichRaft();
            int before = raft.MemberCount;
            Assert.IsNull(raft.TryPlace(BuildPiece.Column(), new Vector2Int(0, -1)));
            Assert.AreEqual(before + 2, raft.MemberCount, "the column, plus the far half of the 6 m beam it stands on");

            foreach (Member m in raft.Gravity.Members)
            {
                if (m.IsFailed) continue;
                raft.TryGetPiece(m.Id, out BuildPiece piece);
                if (piece?.Name == "Deck") Assert.AreEqual(m.LengthM, Vector3.Distance(
                    raft.Gravity.JointAt(m.JointA).Position, raft.Gravity.JointAt(m.JointB).Position), 1e-3f);
            }
        }

        [Test]
        public void RoofedFrameIsTiedIntoTheRaft()
        {
            RaftState raft = RaftWithFloor();
            raft.WindLoadKnPerM = 1.2f;
            TrussReport report = raft.SolveWind();
            Assert.IsTrue(report.Converged,
                "columns on the middle of a beam must bear on it, not float free: " + report.FailureReason);
        }

        [Test]
        public void BracingSavesTheRoofInAStorm()
        {
            const float storm = 10f;
            RaftState bare = RaftWithFloor();
            bare.WindLoadKnPerM = storm;
            bare.SolveWind();
            Assert.Greater(CountFailed(bare, "Roof"), 0, "an unbraced shelter loses a roof diagonal in a storm");

            RaftState braced = RaftWithFloor();
            (Vector2Int, int)[] braces =
            {
                (new Vector2Int(-1, -1), 0), (new Vector2Int(0, -1), 2), (new Vector2Int(-1, 0), 0), (new Vector2Int(0, 0), 2),
                (new Vector2Int(-1, -1), 1), (new Vector2Int(-1, 0), 3), (new Vector2Int(0, -1), 1), (new Vector2Int(0, 0), 3),
            };
            foreach ((Vector2Int cell, int facing) in braces)
                Assert.IsNull(braced.TryPlace(BuildPiece.Brace(), cell, 0, facing), $"brace {cell} {facing}");
            braced.WindLoadKnPerM = storm;
            braced.SolveWind();
            Assert.AreEqual(0, CountFailed(braced, "Roof"), Describe(braced));
        }

        private static int CountFailed(RaftState raft, string pieceName)
        {
            int n = 0;
            foreach (Member m in raft.Gravity.Members)
            {
                if (m.IsFailed && raft.TryGetPiece(m.Id, out BuildPiece piece) && piece.Name == pieceName) n++;
            }
            return n;
        }

        private static string Describe(RaftState raft)
        {
            var sb = new System.Text.StringBuilder();
            foreach (Member m in raft.Gravity.Members)
            {
                raft.TryGetPiece(m.Id, out BuildPiece piece);
                Vector3 a = raft.Gravity.JointAt(m.JointA).Position, b = raft.Gravity.JointAt(m.JointB).Position;
                sb.Append($"\n{m.Id} {piece?.Name} {a}->{b} failed={m.IsFailed} util={m.Utilization:0.00} axial={m.AxialLoadKn:0.0} bend={m.BendingMomentKnM:0.0}");
            }
            return sb.ToString();
        }
    }
}
