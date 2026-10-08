using DesalEra.Game;
using NUnit.Framework;
using UnityEngine;

namespace DesalEra.Tests
{
    /// <summary>
    /// Storeys, facing, roofs and shelter. See docs/shelter-building.md for the rules
    /// these pin down.
    /// </summary>
    public sealed class ShelterTests
    {
        private static readonly Vector2Int[] SquareCorners =
        {
            new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(0, 0)
        };

        private static RaftState RichRaft()
        {
            var raft = new RaftState();
            raft.Inventory.Add(ResourceKind.Plank, 100);
            raft.Inventory.Add(ResourceKind.Scrap, 100);
            raft.Inventory.Add(ResourceKind.Metal, 100);
            return raft;
        }

        private static RaftState RaftWithFourColumns()
        {
            RaftState raft = RichRaft();
            foreach (Vector2Int cell in SquareCorners)
                Assert.IsNull(raft.TryPlace(BuildPiece.Column(), cell), $"column at {cell}");
            return raft;
        }

        [Test]
        public void ColumnAndDeckShareAGridPoint()
        {
            RaftState raft = RichRaft();
            Assert.IsNull(raft.TryPlace(BuildPiece.Column(), new Vector2Int(1, 0)));
            Assert.IsNull(raft.TryPlace(BuildPiece.Deck(), new Vector2Int(1, 0)),
                "a column and a deck beam sit on the same joint and must not block each other");
            Assert.IsNotNull(raft.TryPlace(BuildPiece.Column(), new Vector2Int(1, 0)),
                "the same socket twice must still be refused");
        }

        [Test]
        public void SpawnPointAcceptsAColumn()
        {
            RaftState raft = RichRaft();
            Assert.IsNull(raft.TryPlace(BuildPiece.Column(), Vector2Int.zero));
            Assert.IsNull(raft.TryDismantle(Vector2Int.zero), "the column comes off first");
            Assert.AreEqual("That is the spawn deck.", raft.TryDismantle(Vector2Int.zero),
                "the spawn marker itself is never dismantled");
        }

        [Test]
        public void WestFacingDeckIsTheSameBeamAsTheEastFacingNeighbour()
        {
            RaftState raft = RichRaft();
            Assert.IsNull(raft.TryPlace(BuildPiece.Deck(), new Vector2Int(1, 0), 0, facing: 0));
            Assert.AreEqual("that platform is already occupied",
                raft.TryPlace(BuildPiece.Deck(), new Vector2Int(2, 0), 0, facing: 2));
        }

        [Test]
        public void FacingRotatesTheDeckDirection()
        {
            RaftState raft = RichRaft();
            int before = raft.MemberCount;
            Assert.IsNull(raft.TryPlace(BuildPiece.Deck(), new Vector2Int(1, 1), 0, facing: 1));
            Assert.AreEqual(before + 1, raft.MemberCount);

            var member = raft.Gravity.Members[raft.MemberCount - 1];
            Vector3 a = raft.Gravity.JointAt(member.JointA).Position;
            Vector3 b = raft.Gravity.JointAt(member.JointB).Position;
            Vector3 d = b - a;
            Assert.AreEqual(0f, d.x, 1e-3f);
            Assert.AreEqual(RaftState.CellSize, d.z, 1e-3f, "north-facing deck runs along +Z");
        }

        [Test]
        public void RaisedPiecesNeedSupport()
        {
            RaftState raft = RichRaft();
            Assert.AreEqual("needs support below; build a column first",
                raft.TryPlace(BuildPiece.Column(), new Vector2Int(1, 0), level: 1));

            Assert.IsNull(raft.TryPlace(BuildPiece.Column(), new Vector2Int(1, 0)));
            Assert.IsNull(raft.TryPlace(BuildPiece.Column(), new Vector2Int(1, 0), level: 1),
                "a column top supports the next storey");
        }

        [Test]
        public void RaisedBeamNeedsBothEnds()
        {
            RaftState raft = RichRaft();
            Assert.IsNull(raft.TryPlace(BuildPiece.Column(), new Vector2Int(1, 0)));
            Assert.AreEqual("a raised beam needs support at both ends",
                raft.TryPlace(BuildPiece.Deck(), new Vector2Int(1, 0), level: 1));

            Assert.IsNull(raft.TryPlace(BuildPiece.Column(), new Vector2Int(1, -1)));
            Assert.IsNull(raft.TryPlace(BuildPiece.Deck(), new Vector2Int(1, -1), level: 1, facing: 1));
        }

        [Test]
        public void RoofRefusedAtDeckLevel()
        {
            RaftState raft = RaftWithFourColumns();
            Assert.AreEqual("a roof must sit on top of columns",
                raft.TryPlace(BuildPiece.Roof(), new Vector2Int(-1, -1), level: 0));
        }

        [Test]
        public void RoofNeedsAllFourCorners()
        {
            RaftState raft = RichRaft();
            for (int i = 0; i < 3; i++) Assert.IsNull(raft.TryPlace(BuildPiece.Column(), SquareCorners[i]));

            Assert.AreEqual("a roof needs a column at every corner",
                raft.TryPlace(BuildPiece.Roof(), new Vector2Int(-1, -1), level: 1));

            Assert.IsNull(raft.TryPlace(BuildPiece.Column(), SquareCorners[3]));
            Assert.IsNull(raft.TryPlace(BuildPiece.Roof(), new Vector2Int(-1, -1), level: 1));
        }

        [Test]
        public void DismantledColumnNoLongerSupports()
        {
            RaftState raft = RaftWithFourColumns();
            Assert.IsNull(raft.TryDismantle(new Vector2Int(0, 0)));
            Assert.AreEqual("a roof needs a column at every corner",
                raft.TryPlace(BuildPiece.Roof(), new Vector2Int(-1, -1), level: 1),
                "a removed column leaves its joint behind, which must not count as support");
        }

        [Test]
        public void ShelteredOnlyUnderTheRoof()
        {
            RaftState raft = RaftWithFourColumns();
            var deck = new Vector3(-1.5f, RaftState.BaseDeckY, -1.5f);
            Assert.IsFalse(raft.IsSheltered(deck), "no roof yet");

            Assert.IsNull(raft.TryPlace(BuildPiece.Roof(), new Vector2Int(-1, -1), level: 1));
            Assert.IsTrue(raft.IsSheltered(deck));
            Assert.IsFalse(raft.IsSheltered(new Vector3(1.5f, RaftState.BaseDeckY, 1.5f)), "outside the square");
            Assert.IsFalse(raft.IsSheltered(new Vector3(-1.5f, 6f, -1.5f)), "above the roof");
        }

        [Test]
        public void RoofCanBeDismantledByAimingAtACorner()
        {
            RaftState raft = RaftWithFourColumns();
            Assert.IsNull(raft.TryPlace(BuildPiece.Roof(), new Vector2Int(-1, -1), level: 1));
            Assert.IsNull(raft.TryDismantle(new Vector2Int(0, 0), level: 1));
            Assert.IsFalse(raft.IsSheltered(new Vector3(-1.5f, RaftState.BaseDeckY, -1.5f)));
        }

        [Test]
        public void RoofedFrameStillSolves()
        {
            RaftState raft = RaftWithFourColumns();
            Assert.IsNull(raft.TryPlace(BuildPiece.Roof(), new Vector2Int(-1, -1), level: 1));
            Assert.IsTrue(raft.SolveBuoyancy().IsFloating, "a roof must not sink the starting raft");
        }

        [Test]
        public void StormHurtsOnlyTheExposed()
        {
            var exposed = new SurvivalModel();
            var sheltered = new SurvivalModel();

            exposed.ApplyWeather(2f, storm: true, sheltered: false);
            sheltered.ApplyWeather(2f, storm: true, sheltered: true);

            Assert.Less(exposed.Health, SurvivalModel.MaxValue);
            Assert.Less(exposed.Stamina, SurvivalModel.MaxValue);
            Assert.AreEqual(SurvivalModel.MaxValue, sheltered.Health);
            Assert.AreEqual(SurvivalModel.MaxValue, sheltered.Stamina);
        }

        [Test]
        public void ShelterSpeedsRecoveryInCalm()
        {
            var open = new SurvivalModel();
            var roofed = new SurvivalModel();
            open.Set(Vital.Stamina, 20f);
            roofed.Set(Vital.Stamina, 20f);

            open.Advance(2f);
            roofed.Advance(2f);
            roofed.ApplyWeather(2f, storm: false, sheltered: true);

            Assert.Greater(roofed.Stamina, open.Stamina);
        }
    }
}
