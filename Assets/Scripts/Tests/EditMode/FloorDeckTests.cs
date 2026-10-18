using DesalEra.Game;
using NUnit.Framework;
using UnityEngine;

namespace DesalEra.Tests
{
    /// <summary>
    /// Steel beams vs wood floor planks: a plank needs a closed beam frame, and only
    /// planks are walkable. See docs/shelter-building.md.
    /// </summary>
    public sealed class FloorDeckTests
    {
        private static RaftState RichRaft()
        {
            var raft = new RaftState();
            raft.Inventory.Add(ResourceKind.Plank, 100);
            raft.Inventory.Add(ResourceKind.Scrap, 100);
            raft.Inventory.Add(ResourceKind.Metal, 100);
            return raft;
        }

        /// <summary>Cell just east of the starter platform: needs a new closed frame.</summary>
        private static readonly Vector2Int EastCell = new Vector2Int(1, 0);

        private static void EnsureBeam(RaftState raft, Vector2Int cell, int facing)
        {
            string error = raft.TryPlace(BuildPiece.Beam(), cell, 0, facing);
            Assert.IsTrue(error == null || error == "that platform is already occupied",
                $"beam at {cell} facing {facing}: {error}");
        }

        private static void CloseFrameAround(RaftState raft, Vector2Int cell)
        {
            EnsureBeam(raft, cell, facing: 0); // south BX
            EnsureBeam(raft, new Vector2Int(cell.x, cell.y + 1), facing: 0); // north BX
            EnsureBeam(raft, cell, facing: 1); // west BZ
            EnsureBeam(raft, new Vector2Int(cell.x + 1, cell.y), facing: 1); // east BZ
        }

        [Test]
        public void FloorRefusedWithoutBeams()
        {
            RaftState raft = RichRaft();
            Assert.AreEqual("need closed steel frame",
                raft.TryPlace(BuildPiece.Deck(), EastCell));
        }

        [Test]
        public void FloorRefusedWithThreeBeams()
        {
            RaftState raft = RichRaft();
            // Starter already has the west BZ of EastCell; add south + north only.
            EnsureBeam(raft, EastCell, facing: 0);
            EnsureBeam(raft, new Vector2Int(EastCell.x, EastCell.y + 1), facing: 0);
            Assert.AreEqual("need closed steel frame",
                raft.TryPlace(BuildPiece.Deck(), EastCell),
                "three sides are not a closed frame");
        }

        [Test]
        public void FloorAllowedOnClosedFrame()
        {
            RaftState raft = RichRaft();
            CloseFrameAround(raft, EastCell);
            Assert.IsTrue(raft.HasClosedBeamFrame(EastCell));
            Assert.IsNull(raft.TryPlace(BuildPiece.Deck(), EastCell));
            Assert.IsTrue(raft.HasFloorAt(EastCell));
        }

        [Test]
        public void BeamsAloneAreNotWalkable()
        {
            RaftState raft = RichRaft();
            CloseFrameAround(raft, EastCell);
            Vector3 centre = new Vector3(
                (EastCell.x + 0.5f) * RaftState.CellSize,
                RaftState.BaseDeckY,
                (EastCell.y + 0.5f) * RaftState.CellSize);
            Assert.IsFalse(raft.IsOverDeck(centre, 0f), "frame without a plank is open water");

            Assert.IsNull(raft.TryPlace(BuildPiece.Deck(), EastCell));
            Assert.IsTrue(raft.IsOverDeck(centre, 0f), "plank makes the cell walkable");
        }

        [Test]
        public void StarterFloorsAreWalkable()
        {
            var raft = new RaftState();
            Assert.IsTrue(raft.IsOverDeck(Vector3.zero, 0f), "spawn sits on a starter plank");
            Assert.IsTrue(raft.HasFloorAt(new Vector2Int(0, 0)));
            Assert.IsTrue(raft.HasClosedBeamFrame(new Vector2Int(0, 0)));
        }

        [Test]
        public void RemovingABeamCollapsesThePlank()
        {
            RaftState raft = RichRaft();
            CloseFrameAround(raft, EastCell);
            Assert.IsNull(raft.TryPlace(BuildPiece.Deck(), EastCell));

            // Pull the north BX (only that beam at the cell) so the floor loses its frame.
            Assert.IsNull(raft.TryDismantle(new Vector2Int(EastCell.x, EastCell.y + 1)));
            Assert.IsFalse(raft.HasFloorAt(EastCell), "plank falls when the frame opens");
            Assert.GreaterOrEqual(raft.LastCollapsed, 1);
        }
    }
}
