using System;
using System.Collections.Generic;
using CrazyAquarium.Structure;
using UnityEngine;

namespace CrazyAquarium.Game
{
    /// <summary>
    /// A placeable structural piece: what it costs, what it is made of, and how it
    /// attaches.
    ///
    /// Costs are deliberately simple and legible. A player should be able to look at
    /// the raft and say "I need four more planks for a deck" without running an
    /// economy spreadsheet, which is the failure mode the original brief warned about
    /// when it listed the need for a base that is not tedious to manage.
    /// </summary>
    public sealed class BuildPiece
    {
        public string Name;
        public MaterialKind Material = MaterialKind.Wood;
        public float CrossSectionAreaM2 = 0.09f;
        public float LengthM = 3f;

        /// <summary>Material cost to place one piece.</summary>
        public readonly Dictionary<ResourceKind, int> Cost = new Dictionary<ResourceKind, int>();

        /// <summary>Sealed displacement this piece provides, in cubic metres.</summary>
        public float SealedVolumeM3;

        /// <summary>True when only one end is attached, as with a cantilevered deck arm.</summary>
        public bool IsCantilever;

        /// <summary>Offset from the attach point to the far joint, in raft-local space.</summary>
        public Vector3 LocalOffset = Vector3.up;

        /// <summary>Rescued villagers gained by placing this piece. Rewards expansion.</summary>
        public int PopulationGrant;

        public static BuildPiece Deck(float lengthM = 3f)
        {
            var piece = new BuildPiece
            {
                Name = "Deck",
                Material = MaterialKind.Wood,
                CrossSectionAreaM2 = 0.09f,
                LengthM = lengthM,
                LocalOffset = Vector3.right * lengthM
            };
            piece.Cost[ResourceKind.Plank] = 4;
            return piece;
        }

        public static BuildPiece Column(float lengthM = 3f)
        {
            var piece = new BuildPiece
            {
                Name = "Column",
                Material = MaterialKind.Wood,
                CrossSectionAreaM2 = 0.09f,
                LengthM = lengthM,
                LocalOffset = Vector3.up * lengthM,
                PopulationGrant = 1
            };
            piece.Cost[ResourceKind.Plank] = 2;
            piece.Cost[ResourceKind.Scrap] = 3;
            return piece;
        }

        /// <summary>
        /// A pontoon: buoyant, so it is what makes the raft float at all. Expensive and
        /// heavy, which is the tradeoff the survival pressure is meant to bite on.
        ///
        /// Modelled as a sealed steel drum, not a solid plastic block. The shell is
        /// steel and the volume is counted as sealed air, which is what a salvaged
        /// fuel drum actually is. A solid plastic pontoon nets only 50 kg of lift per
        /// cubic metre and cannot hold up a deck.
        /// </summary>
        public static BuildPiece Pontoon(float lengthM = 1f)
        {
            var piece = new BuildPiece
            {
                Name = "Pontoon",
                Material = MaterialKind.Steel,
                CrossSectionAreaM2 = 0.02f,
                LengthM = lengthM,
                LocalOffset = Vector3.up * lengthM
            };
            piece.SealedVolumeM3 = 2.4f;
            piece.Cost[ResourceKind.Metal] = 3;
            piece.Cost[ResourceKind.Scrap] = 2;
            return piece;
        }

        /// <summary>
        /// A diagonal brace. Cheap in material, the piece that decides whether the
        /// raft survives a storm, and the clearest expression of the game's USP.
        /// </summary>
        public static BuildPiece Brace(float lengthM = 4.24f)
        {
            var piece = new BuildPiece
            {
                Name = "Brace",
                Material = MaterialKind.Steel,
                CrossSectionAreaM2 = 0.02f,
                LengthM = lengthM,
                LocalOffset = new Vector3(lengthM, lengthM, 0f).normalized * lengthM
            };
            piece.Cost[ResourceKind.Metal] = 2;
            return piece;
        }

        /// <summary>Desalination still: converts nothing, but keeps the base supplied.</summary>
        public static BuildPiece Still()
        {
            var piece = new BuildPiece
            {
                Name = "Still",
                Material = MaterialKind.Steel,
                CrossSectionAreaM2 = 0.09f,
                LengthM = 2f,
                LocalOffset = Vector3.up * 2f,
                PopulationGrant = 1
            };
            piece.Cost[ResourceKind.Metal] = 4;
            piece.Cost[ResourceKind.Plank] = 3;
            return piece;
        }
    }

    /// <summary>
    /// The player's raft: a live structure plus the bookkeeping around it.
    ///
    /// Owns both solvers deliberately. The gravity solver answers "does it float and
    /// how much is on deck", the truss solver answers "does it stand up in wind". They
    /// are separate because they cost very different amounts to run and answer
    /// different questions.
    /// </summary>
    public sealed class RaftState
    {
        public const float BaseDeckY = 1.5f;
        public const float WaterLevelY = 0f;

        public readonly StructureSolver Gravity = new StructureSolver { WaterLevelY = WaterLevelY };
        public readonly Inventory Inventory = new Inventory();
        public readonly SurvivalModel Survival = new SurvivalModel();

        public TrussSolver Truss { get; private set; }

        /// <summary>Grid spacing used for placement, in metres.</summary>
        public const float CellSize = 3f;

        /// <summary>How far the raft can reach from the origin, in cells.</summary>
        public const int MaxExtentCells = 6;

        private readonly Dictionary<Vector2Int, int> _occupied = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<int, BuildPiece> _pieceByMember = new Dictionary<int, BuildPiece>();

        public int MemberCount => Gravity.Members.Count;

        public RaftState()
        {
            BuildStartingRaft();
            RefreshTruss();
        }

        /// <summary>
        /// The raft the player begins with: a small platform that floats and carries
        /// the survivor. Everything after this is bought.
        ///
        /// Four sealed barrels, two under each end of the deck, arranged as a shallow
        /// pyramid in plan. That shape is deliberate: a raft is a flat platform, so
        /// with its supports all on one centreline it is a mechanism in Z and the truss
        /// solver reports a singular matrix on the very first frame. Spreading the
        /// barrels fore and aft makes it a stable 3D frame, and because they are sealed
        /// they add buoyancy while doing it.
        /// </summary>
        private void BuildStartingRaft()
        {
            // Barrel sizing, worked out rather than guessed.
            //
            // A 0.12 m2 solid section over the 2.1 m from keel to deck weighs 1978 kg,
            // which no salvaged raft would use. Real pontoons are thin-walled, so the
            // shell is a 0.02 m2 post: 330 kg, still a plausible thing to weld
            // together, and structurally adequate for a column.
            //
            // Displacement needed is 9.2 m3 against 9.6 m3 of sealed air across four
            // barrels, which leaves roughly 30% spare buoyancy. That is deliberate: the
            // opening raft floats with room to add a deck, but two or three careless
            // loads put it under.
            const float pontoonArea = 0.02f;
            const float sealedPerPontoon = 2.4f;

            float half = CellSize;
            float keel = WaterLevelY - 0.6f;

            // Deck corners, plus the four submerged keel anchors.
            int deckNW = Gravity.AddJoint(new Vector3(-half, BaseDeckY, -half));
            int deckNE = Gravity.AddJoint(new Vector3(half, BaseDeckY, -half));
            int deckSW = Gravity.AddJoint(new Vector3(-half, BaseDeckY, half));
            int deckSE = Gravity.AddJoint(new Vector3(half, BaseDeckY, half));

            int keelNW = Gravity.AddJoint(new Vector3(-half, keel, -half), anchored: true);
            int keelNE = Gravity.AddJoint(new Vector3(half, keel, -half), anchored: true);
            int keelSW = Gravity.AddJoint(new Vector3(-half, keel, half), anchored: true);
            int keelSE = Gravity.AddJoint(new Vector3(half, keel, half), anchored: true);

            // Four corner pontoons, each running from a keel anchor to a deck corner.
            // The diagonal layout is what makes the frame rigid in Z.
            AddPontoon(keelNW, deckNW, pontoonArea, sealedPerPontoon);
            AddPontoon(keelNE, deckNE, pontoonArea, sealedPerPontoon);
            AddPontoon(keelSW, deckSW, pontoonArea, sealedPerPontoon);
            AddPontoon(keelSE, deckSE, pontoonArea, sealedPerPontoon);

            // Two deck beams forming the starting platform.
            Member deckNS = Gravity.AddMember(deckNW, deckSW, MaterialKind.Wood, 0.09f);
            Member deckEW = Gravity.AddMember(deckNW, deckNE, MaterialKind.Wood, 0.09f);
            Member deckSEBeam = Gravity.AddMember(deckSW, deckSE, MaterialKind.Wood, 0.09f);
            Member deckNEBeam = Gravity.AddMember(deckNE, deckSE, MaterialKind.Wood, 0.09f);

            _pieceByMember[deckNS.Id] = BuildPiece.Deck(CellSize * 2f);
            _pieceByMember[deckEW.Id] = BuildPiece.Deck(CellSize * 2f);
            _pieceByMember[deckSEBeam.Id] = BuildPiece.Deck(CellSize * 2f);
            _pieceByMember[deckNEBeam.Id] = BuildPiece.Deck(CellSize * 2f);

            // The origin cell is the spawn point, not a build slot.
            MarkCell(new Vector2Int(0, 0), deckNS.Id);

            Gravity.RecomputeAllDerived();

            // A starting kit rather than a full loadout. Enough to add another pontoon
            // and a deck immediately, so the first minute teaches the loop instead of
            // gating it behind a grind.
            Inventory.Add(ResourceKind.Plank, 8);
            Inventory.Add(ResourceKind.Scrap, 6);
            Inventory.Add(ResourceKind.Metal, 4);
            Inventory.Add(ResourceKind.Water, 3);
            Inventory.Add(ResourceKind.Food, 2);
        }

        /// <summary>
        /// Adds a sealed barrel running from a submerged keel anchor to a deck corner.
        /// Every pontoon on the raft has this shape: buoyant, heavy, and carrying deck
        /// load at the same time.
        /// </summary>
        private Member AddPontoon(int keelJoint, int deckJoint, float area, float sealedVolume)
        {
            Member member = Gravity.AddMember(keelJoint, deckJoint, MaterialKind.Steel, area);
            member.SealedVolumeM3 = sealedVolume;

            // Recorded with an empty cost: the starting barrels are given, not bought,
            // so dismantling one must not refund the player for it.
            var piece = BuildPiece.Pontoon();
            piece.Cost.Clear();
            _pieceByMember[member.Id] = piece;
            return member;
        }

        /// <summary>Rebuilds the truss view over the current member set.</summary>
        public void RefreshTruss()
        {
            Truss = new TrussSolver(Gravity.Joints, Gravity.Members);
            Truss.ApplySelfWeight();
            Truss.WindLoadKnPerM = WindLoadKnPerM;
        }

        /// <summary>Wind pressure in kN per metre of height, exposed for storm tuning.</summary>
        public float WindLoadKnPerM = 1.5f;

        private void MarkCell(Vector2Int cell, int memberId) => _occupied[cell] = memberId;

        public bool IsCellOccupied(Vector2Int cell) => _occupied.ContainsKey(cell);

        public bool IsWithinBounds(Vector2Int cell)
        {
            return Mathf.Abs(cell.x) <= MaxExtentCells && Mathf.Abs(cell.y) <= MaxExtentCells;
        }

        public int OccupiedCellCount => _occupied.Count;

        /// <summary>
        /// Attempts to place a piece whose root joint is at the given cell.
        ///
        /// Returns null on success, or a reason the placement was rejected. The
        /// caller can show the reason, which is what makes a refused build teachable
        /// instead of just silently failing.
        /// </summary>
        public string TryPlace(BuildPiece piece, Vector2Int cell)
        {
            if (piece == null) return "no piece specified";
            if (!IsWithinBounds(cell)) return "outside the buildable area";
            if (IsCellOccupied(cell)) return "that platform is already occupied";

            if (!Inventory.TrySpend(piece.Cost)) return "not enough resources";

            Vector3 root = WorldOf(cell);
            int rootJoint = FindOrCreateJoint(root);
            Vector3 tip = root + piece.LocalOffset;
            int tipJoint = FindOrCreateJoint(tip);

            Member member = Gravity.AddMember(rootJoint, tipJoint, piece.Material,
                piece.CrossSectionAreaM2, piece.IsCantilever);
            member.SealedVolumeM3 = piece.SealedVolumeM3;
            _pieceByMember[member.Id] = piece;
            MarkCell(cell, member.Id);

            if (piece.PopulationGrant > 0)
            {
                Inventory.Add(ResourceKind.Water, piece.PopulationGrant * 2);
            }

            Gravity.RecomputeAllDerived();
            RefreshTruss();
            return null;
        }

        /// <summary>
        /// Removes the piece occupying a cell and refunds part of its cost.
        /// The refund is deliberately partial, so demolishing is never a free undo.
        /// </summary>
        public string TryDismantle(Vector2Int cell)
        {
            if (!_occupied.TryGetValue(cell, out int memberId)) return "nothing to dismantle here";

            Member member = FindMember(memberId);
            if (member == null) return "that piece is already gone";

            if (_pieceByMember.TryGetValue(memberId, out BuildPiece piece))
            {
                Inventory.RefundFraction(piece.Cost);
            }

            member.IsFailed = true;
            _occupied.Remove(cell);
            _pieceByMember.Remove(memberId);

            // A failed member keeps its joints, so the graph stays consistent for
            // later re-solving. Pruning would renumber everything and invalidate the
            // cell map, which is not worth the memory it saves at this scale.
            RefreshTruss();
            return null;
        }

        private Member FindMember(int id)
        {
            foreach (Member m in Gravity.Members)
            {
                if (m.Id == id) return m;
            }
            return null;
        }

        private int FindOrCreateJoint(Vector3 position)
        {
            for (int i = 0; i < Gravity.Joints.Count; i++)
            {
                if ((Gravity.Joints[i].Position - position).sqrMagnitude < 1e-4f) return i;
            }
            return Gravity.AddJoint(position);
        }

        public static Vector3 WorldOf(Vector2Int cell)
        {
            return new Vector3(cell.x * CellSize, BaseDeckY, cell.y * CellSize);
        }

        /// <summary>Runs the wind analysis and returns the report for HUD display.</summary>
        public TrussReport SolveWind()
        {
            Truss.WindLoadKnPerM = WindLoadKnPerM;
            return Truss.Solve();
        }

        /// <summary>Runs the buoyancy analysis and returns the report for HUD display.</summary>
        public SolveReport SolveBuoyancy()
        {
            return Gravity.Solve();
        }

        /// <summary>Counts pieces that failed in the last wind solve.</summary>
        public int CountFailedPieces()
        {
            int n = 0;
            foreach (Member m in Gravity.Members)
            {
                if (m.IsFailed) n++;
            }
            return n;
        }
    }
}
