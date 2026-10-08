using System;
using System.Collections.Generic;
using DesalEra.Structure;
using UnityEngine;

// UnityEngine also defines a physics Joint.
using Joint = DesalEra.Structure.Joint;

namespace DesalEra.Game
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

        /// <summary>
        /// A roof over one grid square, resting on four column tops. Structurally it is
        /// the two diagonals of the square -- an in-plane X brace that also carries the
        /// boards' weight -- and it is drawn as a slab. LengthM is the diagonal.
        /// </summary>
        public static BuildPiece Roof()
        {
            var piece = new BuildPiece
            {
                Name = "Roof",
                Material = MaterialKind.Wood,
                CrossSectionAreaM2 = 0.04f,
                LengthM = RaftState.CellSize * 1.41421356f,
                LocalOffset = Vector3.zero
            };
            piece.Cost[ResourceKind.Plank] = 3;
            piece.Cost[ResourceKind.Scrap] = 1;
            return piece;
        }

        /// <summary>
        /// A wall panel filling one storey between two columns. Structurally it is a
        /// single diagonal -- a boarded panel works as a shear brace -- and it is the one
        /// piece that catches wind over its whole face. LengthM is the diagonal.
        /// </summary>
        public static BuildPiece Wall()
        {
            var piece = new BuildPiece
            {
                Name = "Wall",
                Material = MaterialKind.Wood,
                CrossSectionAreaM2 = 0.03f,
                LengthM = RaftState.CellSize * 1.41421356f,
                LocalOffset = new Vector3(RaftState.CellSize, RaftState.StoreyHeightM, 0f)
            };
            piece.Cost[ResourceKind.Plank] = 3;
            return piece;
        }

        /// <summary>
        /// A flight of stairs climbing one storey over one grid step, from a grid point
        /// up to the column top beside it. One inclined member, drawn as steps.
        /// </summary>
        public static BuildPiece Stairs()
        {
            var piece = new BuildPiece
            {
                Name = "Stairs",
                Material = MaterialKind.Wood,
                CrossSectionAreaM2 = 0.04f,
                LengthM = RaftState.CellSize * 1.41421356f,
                LocalOffset = new Vector3(RaftState.CellSize, RaftState.StoreyHeightM, 0f)
            };
            piece.Cost[ResourceKind.Plank] = 4;
            piece.Cost[ResourceKind.Scrap] = 1;
            return piece;
        }
    }

    /// <summary>
    /// What a placement would add, worked out before anything is spent: the socket it
    /// takes and the member segments it would create. The build preview draws these
    /// whether or not the placement is allowed.
    /// </summary>
    public sealed class PlacementPlan
    {
        public BuildPiece Piece;
        public PlacementKey Key;

        /// <summary>Facing after normalisation, 0..3.</summary>
        public int Facing;

        public readonly List<(Vector3 From, Vector3 To)> Segments = new List<(Vector3, Vector3)>();
    }

    /// <summary>
    /// Where a piece sits: a grid point, a storey, and a socket naming which kind of
    /// member at that point it is. A column and a deck beam share a grid point -- they
    /// share a joint in the structure -- so occupancy has to be per socket, not per cell.
    /// </summary>
    public readonly struct PlacementKey : IEquatable<PlacementKey>
    {
        public readonly Vector2Int Cell;
        public readonly int Level;
        public readonly string Socket;

        public PlacementKey(Vector2Int cell, int level, string socket)
        {
            Cell = cell;
            Level = level;
            Socket = socket ?? string.Empty;
        }

        public bool Equals(PlacementKey other) =>
            Cell == other.Cell && Level == other.Level && string.Equals(Socket, other.Socket, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is PlacementKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Cell.GetHashCode();
                h = h * 31 + Level;
                return h * 31 + StringComparer.Ordinal.GetHashCode(Socket);
            }
        }

        public override string ToString() => $"{Cell.x},{Cell.y} L{Level} {Socket}";
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
        /// <summary>Deck standing height above the still-water plane.</summary>
        public const float BaseDeckY = 1.5f;

        /// <summary>World Y of the still-water plane (sea mesh rests here).</summary>
        public const float WaterLevelY = 0f;

        /// <summary>
        /// How far keel anchors sit below still water. Was 0.6 m — too shallow once
        /// swell amplitude reached ~1 m; posts read as resting on a film of water.
        /// </summary>
        public const float KeelDepthM = 1.5f;

        public readonly StructureSolver Gravity = new StructureSolver { WaterLevelY = WaterLevelY };
        public readonly Inventory Inventory = new Inventory();
        public readonly SurvivalModel Survival = new SurvivalModel();

        public TrussSolver Truss { get; private set; }

        /// <summary>Grid spacing used for placement, in metres.</summary>
        public const float CellSize = 3f;

        /// <summary>How far the raft can reach from the origin, in cells.</summary>
        public const int MaxExtentCells = 6;

        /// <summary>Height of one storey, matching a column, in metres.</summary>
        public const float StoreyHeightM = 3f;

        /// <summary>Highest storey a piece may be placed on (0 = deck level).</summary>
        public const int MaxLevel = 2;

        public const string SpawnSocket = "Spawn";
        public const string VerticalSocket = "V";
        public const string RoofSocket = "R";
        public const string WallXSocket = "WX";
        public const string WallZSocket = "WZ";
        public const string StairsSocketPrefix = "S";

        /// <summary>Height of a floor slab's walking surface above the joints it rests on.</summary>
        public const float FloorTopAboveJointM = 0.22f;

        /// <summary>Height of a stair tread's walking surface above the stair's centreline.</summary>
        public const float StairTreadAboveLineM = 0.12f;

        /// <summary>Half the walkable width of a flight of stairs.</summary>
        public const float StairHalfWidthM = 0.7f;

        // Insertion-ordered so dismantling takes the newest piece at a point first.
        private readonly Dictionary<PlacementKey, List<int>> _placed = new Dictionary<PlacementKey, List<int>>();
        private readonly Dictionary<PlacementKey, BuildPiece> _pieceByKey = new Dictionary<PlacementKey, BuildPiece>();
        private readonly List<PlacementKey> _placementOrder = new List<PlacementKey>();
        private readonly Dictionary<int, BuildPiece> _pieceByMember = new Dictionary<int, BuildPiece>();

        /// <summary>Pieces removed by the last <see cref="CollapseUnsupported"/> call.</summary>
        public int LastCollapsed { get; private set; }

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
            // Keel at -KeelDepthM → deck at BaseDeckY is a ~3.0 m column. A 0.02 m2
            // thin steel shell is ~470 kg each — still weldable salvage, not a solid
            // billet. Four sealed drums at 2.4 m3 still dominate weight and leave
            // spare buoyancy for a few decks before the raft sits heavy.
            const float pontoonArea = 0.02f;
            const float sealedPerPontoon = 2.4f;

            float half = CellSize;
            float keel = WaterLevelY - KeelDepthM;

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

            // The origin is the spawn point. It occupies its own socket so it counts as
            // structure for adjacency, without blocking a column or deck at that point.
            Record(new PlacementKey(Vector2Int.zero, 0, SpawnSocket), new List<int>());

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

        private void Record(PlacementKey key, List<int> memberIds)
        {
            _placed[key] = memberIds;
            _placementOrder.Add(key);
        }

        private void Forget(PlacementKey key)
        {
            _placed.Remove(key);
            _pieceByKey.Remove(key);
            _placementOrder.Remove(key);
        }

        /// <summary>Every placement, oldest first. The spawn marker is included.</summary>
        public IReadOnlyList<PlacementKey> Placements => _placementOrder;

        /// <summary>The piece placed at a key, if any.</summary>
        public bool TryGetPlacedPiece(PlacementKey key, out BuildPiece piece) =>
            _pieceByKey.TryGetValue(key, out piece);

        /// <summary>True when anything occupies the grid point at deck level.</summary>
        public bool IsCellOccupied(Vector2Int cell) => IsCellOccupied(cell, 0);

        public bool IsCellOccupied(Vector2Int cell, int level)
        {
            foreach (PlacementKey key in _placementOrder)
            {
                if (key.Cell == cell && key.Level == level) return true;
            }
            return false;
        }

        public bool IsOccupied(PlacementKey key) => _placed.ContainsKey(key);

        public bool IsWithinBounds(Vector2Int cell)
        {
            return Mathf.Abs(cell.x) <= MaxExtentCells && Mathf.Abs(cell.y) <= MaxExtentCells;
        }

        /// <summary>
        /// True when <paramref name="cell"/> shares an edge with an occupied deck-level
        /// cell. Stops floating islands of deck appearing metres from the raft.
        /// </summary>
        public bool IsAdjacentToStructure(Vector2Int cell)
        {
            if (_placementOrder.Count == 0) return true;

            return IsCellOccupied(new Vector2Int(cell.x - 1, cell.y))
                || IsCellOccupied(new Vector2Int(cell.x + 1, cell.y))
                || IsCellOccupied(new Vector2Int(cell.x, cell.y - 1))
                || IsCellOccupied(new Vector2Int(cell.x, cell.y + 1));
        }

        private bool TouchesStructure(Vector2Int cell) =>
            IsCellOccupied(cell) || IsAdjacentToStructure(cell) || HasLiveJointAt(WorldOf(cell));

        public int OccupiedCellCount
        {
            get
            {
                var cells = new HashSet<Vector2Int>();
                foreach (PlacementKey key in _placementOrder)
                {
                    if (key.Level == 0) cells.Add(key.Cell);
                }
                return cells.Count;
            }
        }

        /// <summary>The build piece that produced a member, if any. Used for visuals.</summary>
        public bool TryGetPiece(int memberId, out BuildPiece piece) =>
            _pieceByMember.TryGetValue(memberId, out piece);

        /// <summary>Every roof square, keyed by its south-west grid point and storey.</summary>
        public IEnumerable<PlacementKey> Roofs
        {
            get
            {
                foreach (PlacementKey key in _placementOrder)
                {
                    if (key.Socket == RoofSocket) yield return key;
                }
            }
        }

        /// <summary>0 = east (+X), 1 = north (+Z), 2 = west (-X), 3 = south (-Z).</summary>
        public static int NormaliseFacing(int facing) => ((facing % 4) + 4) % 4;

        /// <summary>
        /// Rotates a piece's offset about the vertical axis. East leaves it untouched, so
        /// the unrotated palette keeps its original meaning.
        /// </summary>
        public static Vector3 RotateOffset(Vector3 offset, int facing)
        {
            Vector3 r = Quaternion.Euler(0f, -90f * NormaliseFacing(facing), 0f) * offset;
            // Snap away float noise so the tip lands exactly on an existing joint.
            return new Vector3(Mathf.Round(r.x * 1e4f) / 1e4f, Mathf.Round(r.y * 1e4f) / 1e4f,
                               Mathf.Round(r.z * 1e4f) / 1e4f);
        }

        /// <summary>
        /// Socket a piece occupies, normalising its root so the same physical member
        /// always maps to the same key: a west-facing deck is the east-facing deck of the
        /// grid point to its west.
        /// </summary>
        public static PlacementKey KeyFor(BuildPiece piece, Vector2Int cell, int level, int facing)
        {
            int f = NormaliseFacing(facing);
            switch (piece.Name)
            {
                case "Deck":
                case "Wall":
                    if (f == 2) { cell.x -= 1; f = 0; }
                    else if (f == 3) { cell.y -= 1; f = 1; }
                    if (piece.Name == "Wall") return new PlacementKey(cell, level, f == 0 ? WallXSocket : WallZSocket);
                    return new PlacementKey(cell, level, f == 0 ? "BX" : "BZ");
                case "Brace":
                    return new PlacementKey(cell, level, "D" + f);
                case "Stairs":
                    return new PlacementKey(cell, level, StairsSocketPrefix + f);
                case "Roof":
                    return new PlacementKey(cell, level, RoofSocket);
                default:
                    return new PlacementKey(cell, level, VerticalSocket);
            }
        }

        /// <summary>Facing encoded in a socket, for pieces whose socket records one.</summary>
        public static int FacingOf(PlacementKey key)
        {
            switch (key.Socket)
            {
                case "BX":
                case WallXSocket: return 0;
                case "BZ":
                case WallZSocket: return 1;
            }
            if (key.Socket.Length == 2 && (key.Socket[0] == 'D' || key.Socket[0] == 'S')
                && char.IsDigit(key.Socket[1]))
                return NormaliseFacing(key.Socket[1] - '0');
            return 0;
        }

        /// <summary>Grid step for a facing: east, north, west, south.</summary>
        public static Vector2Int FacingStep(int facing)
        {
            switch (NormaliseFacing(facing))
            {
                case 1: return new Vector2Int(0, 1);
                case 2: return new Vector2Int(-1, 0);
                case 3: return new Vector2Int(0, -1);
                default: return new Vector2Int(1, 0);
            }
        }

        public static bool IsWallSocket(string socket) => socket == WallXSocket || socket == WallZSocket;

        public static bool IsStairsSocket(string socket) =>
            socket.Length == 2 && socket[0] == 'S' && char.IsDigit(socket[1]);

        /// <summary>
        /// The member segments a piece at this key would have. Pure geometry: whether the
        /// placement is allowed is a separate question.
        /// </summary>
        public static void SegmentsFor(BuildPiece piece, PlacementKey key, List<(Vector3, Vector3)> into)
        {
            into.Clear();
            int f = FacingOf(key);
            Vector3 root = WorldOf(key.Cell, key.Level);

            switch (piece.Name)
            {
                case "Roof":
                {
                    Vector3[] c = RoofCorners(key.Cell, key.Level);
                    into.Add((c[0], c[3]));
                    into.Add((c[1], c[2]));
                    return;
                }
                case "Wall":
                {
                    // One diagonal of the panel, bottom of one column to the top of the other.
                    Vector3 far = WorldOf(key.Cell + FacingStep(f), key.Level + 1);
                    into.Add((root, far));
                    return;
                }
                case "Stairs":
                    into.Add((root, WorldOf(key.Cell + FacingStep(f), key.Level + 1)));
                    return;
                default:
                    into.Add((root, root + RotateOffset(piece.LocalOffset, f)));
                    return;
            }
        }

        /// <summary>Bottom-start, bottom-end, top-start, top-end corners of a wall panel.</summary>
        public static Vector3[] WallCorners(PlacementKey key)
        {
            Vector2Int end = key.Cell + FacingStep(FacingOf(key));
            return new[]
            {
                WorldOf(key.Cell, key.Level),
                WorldOf(end, key.Level),
                WorldOf(key.Cell, key.Level + 1),
                WorldOf(end, key.Level + 1),
            };
        }

        /// <summary>
        /// Attempts to place a piece whose root joint is at the given grid point, storey
        /// and facing.
        ///
        /// Returns null on success, or a reason the placement was rejected. The
        /// caller can show the reason, which is what makes a refused build teachable
        /// instead of just silently failing.
        /// </summary>
        public string TryPlace(BuildPiece piece, Vector2Int cell, int level = 0, int facing = 0)
        {
            string error = CanPlace(piece, cell, level, facing, out PlacementPlan plan);
            if (error != null) return error;
            if (!Inventory.TrySpend(piece.Cost)) return "not enough resources";

            var ids = new List<int>();
            foreach ((Vector3 from, Vector3 to) in plan.Segments)
            {
                ids.Add(AddPieceMember(piece, from, to, plan.Key));
            }

            Record(plan.Key, ids);
            _pieceByKey[plan.Key] = piece;

            if (piece.PopulationGrant > 0)
            {
                Inventory.Add(ResourceKind.Water, piece.PopulationGrant * 2);
            }

            Gravity.RecomputeAllDerived();
            RefreshTruss();
            return null;
        }

        /// <summary>
        /// Checks a placement without changing anything. Returns null when it would
        /// succeed, or the reason it would be refused. <paramref name="plan"/> is filled
        /// in either way whenever the piece and level are valid, so a refused placement
        /// can still be previewed where it would have gone.
        /// </summary>
        public string CanPlace(BuildPiece piece, Vector2Int cell, int level, int facing, out PlacementPlan plan)
        {
            plan = null;
            if (piece == null) return "no piece specified";
            if (level < 0 || level > MaxLevel) return "invalid build level";

            PlacementKey key = KeyFor(piece, cell, level, facing);
            plan = new PlacementPlan { Piece = piece, Key = key, Facing = FacingOf(key) };
            if (piece.Name != "Deck" && piece.Name != "Wall") plan.Facing = NormaliseFacing(facing);
            SegmentsFor(piece, key, plan.Segments);

            if (!IsWithinBounds(cell)) return "outside the buildable area";
            if (piece.Name == "Roof" && level < 1) return "a roof must sit on top of columns";
            if (piece.Name == "Pontoon" && level > 0) return "pontoons only go at deck level";
            if (_placed.ContainsKey(key)) return "that platform is already occupied";

            string support = SupportProblem(piece, key, cell, null);
            if (support != null) return support;

            if (!CanAfford(piece.Cost)) return "not enough resources";
            return null;
        }

        private bool CanAfford(IReadOnlyDictionary<ResourceKind, int> cost)
        {
            foreach (KeyValuePair<ResourceKind, int> pair in cost)
            {
                if (!Inventory.Has(pair.Key, pair.Value)) return false;
            }
            return true;
        }

        /// <summary>
        /// Why a piece at this key would not be held up, or null if it would be.
        ///
        /// Deck-level pieces only have to touch the raft. Anything that hangs off the
        /// frame -- raised pieces, walls, stairs -- needs its attachment joints to be
        /// grounded: joined to the deck through standing members. Members listed in
        /// <paramref name="exclude"/> do not count, so a standing piece cannot hold
        /// itself up when the collapse check re-asks this question.
        /// </summary>
        private string SupportProblem(BuildPiece piece, PlacementKey key, Vector2Int requestedCell, ICollection<int> exclude)
        {
            string name = piece.Name;
            int level = key.Level;
            bool hangs = level > 0 || name == "Wall" || name == "Stairs";

            if (!hangs)
            {
                return TouchesStructure(requestedCell) || TouchesStructure(key.Cell)
                    ? null
                    : "must connect to the existing structure";
            }

            HashSet<int> grounded = GroundedJoints(exclude);
            bool Held(Vector3 p) => IsGrounded(p, grounded);

            switch (name)
            {
                case "Roof":
                    foreach (Vector3 corner in RoofCorners(key.Cell, level))
                    {
                        if (!Held(corner)) return "a roof needs a column at every corner";
                    }
                    return null;

                case "Wall":
                {
                    // Literally a column at each end, not merely grounded corners: a panel
                    // and a roof can otherwise hold each other's corners up in mid-air.
                    Vector3[] c = WallCorners(key);
                    if (!Held(c[0]) || !Held(c[1]) || !HasLiveMemberBetween(c[0], c[2], exclude)
                        || !HasLiveMemberBetween(c[1], c[3], exclude))
                        return "a wall needs a column at both ends";
                    return null;
                }

                case "Stairs":
                {
                    Vector3 foot = WorldOf(key.Cell, level);
                    Vector3 head = WorldOf(key.Cell + FacingStep(FacingOf(key)), level + 1);
                    bool footHeld = level == 0 ? TouchesStructure(key.Cell) : Held(foot);
                    if (!footHeld) return "stairs need something to stand on at the bottom";
                    if (!Held(head)) return "stairs need a column top to climb to";
                    return null;
                }

                default:
                {
                    var segments = new List<(Vector3, Vector3)>();
                    SegmentsFor(piece, key, segments);
                    (Vector3 root, Vector3 tip) = segments[0];
                    if (!Held(root)) return "needs support below; build a column first";
                    if (name == "Deck" && !Held(tip)) return "a raised beam needs support at both ends";
                    return null;
                }
            }
        }

        /// <summary>
        /// Joints joined to the raft's base through standing members. The base is every
        /// joint at deck level -- the deck is one walking surface, even where it is
        /// drawn as a frame -- plus the keel anchors.
        /// </summary>
        private HashSet<int> GroundedJoints(ICollection<int> exclude)
        {
            var grounded = new HashSet<int>();
            var queue = new Queue<int>();
            for (int i = 0; i < Gravity.Joints.Count; i++)
            {
                Joint j = Gravity.Joints[i];
                if (j.IsAnchored || Mathf.Abs(j.Position.y - BaseDeckY) <= DeckLevelTolerance)
                {
                    grounded.Add(i);
                    queue.Enqueue(i);
                }
            }

            var adjacency = new Dictionary<int, List<int>>();
            foreach (Member m in Gravity.Members)
            {
                if (m.IsFailed || (exclude != null && exclude.Contains(m.Id))) continue;
                AddEdge(adjacency, m.JointA, m.JointB);
                AddEdge(adjacency, m.JointB, m.JointA);
            }

            while (queue.Count > 0)
            {
                int j = queue.Dequeue();
                if (!adjacency.TryGetValue(j, out List<int> next)) continue;
                foreach (int k in next)
                {
                    if (grounded.Add(k)) queue.Enqueue(k);
                }
            }
            return grounded;
        }

        private static void AddEdge(Dictionary<int, List<int>> adjacency, int from, int to)
        {
            if (!adjacency.TryGetValue(from, out List<int> list))
            {
                list = new List<int>();
                adjacency[from] = list;
            }
            list.Add(to);
        }

        private bool IsGrounded(Vector3 position, HashSet<int> grounded)
        {
            int joint = FindJoint(position);
            return joint >= 0 && grounded.Contains(joint);
        }

        private bool HasLiveMemberBetween(Vector3 a, Vector3 b, ICollection<int> exclude)
        {
            int ja = FindJoint(a), jb = FindJoint(b);
            if (ja < 0 || jb < 0) return false;
            foreach (Member m in Gravity.Members)
            {
                if (m.IsFailed || (exclude != null && exclude.Contains(m.Id))) continue;
                if ((m.JointA == ja && m.JointB == jb) || (m.JointA == jb && m.JointB == ja)) return true;
            }
            return false;
        }

        private int FindJoint(Vector3 position)
        {
            for (int i = 0; i < Gravity.Joints.Count; i++)
            {
                if ((Gravity.Joints[i].Position - position).sqrMagnitude < 1e-4f) return i;
            }
            return -1;
        }

        /// <summary>
        /// Removes every piece that is no longer held up, repeating until nothing else
        /// falls: losing a column drops the roof on it, and that roof may have been the
        /// only thing holding a column on the storey above. Pieces whose members have
        /// all failed -- in a storm, say -- go too, so their socket can be rebuilt.
        /// Fallen pieces are lost, not refunded. Returns how many fell.
        /// </summary>
        public int CollapseUnsupported()
        {
            int fallen = 0;
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (PlacementKey key in _placementOrder.ToArray())
                {
                    if (key.Socket == SpawnSocket) continue;
                    if (!_placed.TryGetValue(key, out List<int> ids)) continue;

                    bool allFailed = ids.Count > 0;
                    foreach (int id in ids)
                    {
                        Member m = FindMember(id);
                        if (m != null && !m.IsFailed) { allFailed = false; break; }
                    }

                    if (!allFailed)
                    {
                        if (!_pieceByKey.TryGetValue(key, out BuildPiece piece)) continue;
                        bool hangs = key.Level > 0 || piece.Name == "Wall" || piece.Name == "Stairs";
                        if (!hangs) continue;
                        if (SupportProblem(piece, key, key.Cell, ids) == null) continue;
                    }

                    foreach (int id in ids)
                    {
                        Member m = FindMember(id);
                        if (m != null) m.IsFailed = true;
                        _pieceByMember.Remove(id);
                    }
                    Forget(key);
                    fallen++;
                    changed = true;
                }
            }

            LastCollapsed = fallen;
            if (fallen > 0)
            {
                Gravity.RecomputeAllDerived();
                RefreshTruss();
            }
            return fallen;
        }

        private int AddPieceMember(BuildPiece piece, Vector3 from, Vector3 to, PlacementKey key)
        {
            int a = FindOrCreateJoint(from);
            int b = FindOrCreateJoint(to);
            Member member = Gravity.AddMember(a, b, piece.Material, piece.CrossSectionAreaM2, piece.IsCantilever);
            member.SealedVolumeM3 = piece.SealedVolumeM3;

            if (piece.Name == "Wall")
            {
                member.WindAreaM2 = CellSize * StoreyHeightM;
                Vector3 along = (WorldOf(key.Cell + FacingStep(FacingOf(key)), 0) - WorldOf(key.Cell, 0)).normalized;
                member.WindNormal = Vector3.Cross(along, Vector3.up).normalized;
            }

            _pieceByMember[member.Id] = piece;
            return member.Id;
        }

        /// <summary>SW, SE, NW, NE corners of the roof square whose SW grid point is <paramref name="cell"/>.</summary>
        public static Vector3[] RoofCorners(Vector2Int cell, int level)
        {
            return new[]
            {
                WorldOf(cell, level),
                WorldOf(new Vector2Int(cell.x + 1, cell.y), level),
                WorldOf(new Vector2Int(cell.x, cell.y + 1), level),
                WorldOf(new Vector2Int(cell.x + 1, cell.y + 1), level),
            };
        }

        /// <summary>
        /// True when a joint at this position is held by at least one standing member.
        /// Dismantled members keep their joints, so mere existence is not support.
        /// </summary>
        public bool HasLiveJointAt(Vector3 position)
        {
            int joint = -1;
            for (int i = 0; i < Gravity.Joints.Count; i++)
            {
                if ((Gravity.Joints[i].Position - position).sqrMagnitude < 1e-4f) { joint = i; break; }
            }
            if (joint < 0) return false;

            foreach (Member m in Gravity.Members)
            {
                if (!m.IsFailed && (m.JointA == joint || m.JointB == joint)) return true;
            }
            return false;
        }

        /// <summary>
        /// Removes the newest piece at a grid point and storey and refunds part of its
        /// cost. The refund is deliberately partial, so demolishing is never a free undo.
        /// </summary>
        public string TryDismantle(Vector2Int cell, int level = 0)
        {
            bool spawnHere = false;
            for (int i = _placementOrder.Count - 1; i >= 0; i--)
            {
                PlacementKey key = _placementOrder[i];
                if (key.Cell != cell || key.Level != level) continue;
                if (key.Socket == SpawnSocket) { spawnHere = true; continue; }
                if (key.Socket == RoofSocket) continue;
                return Dismantle(key);
            }

            // Nothing standing at the point itself: fall back to the newest roof whose
            // square has this point as a corner, so a roof can be removed by aiming at it.
            for (int i = _placementOrder.Count - 1; i >= 0; i--)
            {
                PlacementKey key = _placementOrder[i];
                if (key.Socket != RoofSocket || key.Level != level) continue;
                int dx = cell.x - key.Cell.x, dz = cell.y - key.Cell.y;
                if (dx >= 0 && dx <= 1 && dz >= 0 && dz <= 1) return Dismantle(key);
            }

            return spawnHere ? "That is the spawn deck." : "nothing to dismantle here";
        }

        /// <summary>Removes the roof whose south-west grid point is <paramref name="cell"/>.</summary>
        public string TryDismantleRoof(Vector2Int cell, int level)
        {
            var key = new PlacementKey(cell, level, RoofSocket);
            return _placed.ContainsKey(key) ? Dismantle(key) : "nothing to dismantle here";
        }

        private string Dismantle(PlacementKey key)
        {
            List<int> ids = _placed[key];
            bool refunded = false;

            foreach (int memberId in ids)
            {
                Member member = FindMember(memberId);
                if (member == null) continue;

                if (!refunded && _pieceByMember.TryGetValue(memberId, out BuildPiece piece))
                {
                    Inventory.RefundFraction(piece.Cost);
                    refunded = true;
                }

                member.IsFailed = true;
                _pieceByMember.Remove(memberId);
            }

            Forget(key);

            // A failed member keeps its joints, so the graph stays consistent for
            // later re-solving. Pruning would renumber everything and invalidate the
            // placement map, which is not worth the memory it saves at this scale.
            RefreshTruss();
            CollapseUnsupported();
            return null;
        }

        /// <summary>
        /// True when the position is under a roof: its horizontal projection lies in a
        /// roof square and the roof is above it.
        /// </summary>
        public bool IsSheltered(Vector3 position)
        {
            const float edge = 0.15f;
            foreach (PlacementKey roof in Roofs)
            {
                float x0 = roof.Cell.x * CellSize - edge, x1 = (roof.Cell.x + 1) * CellSize + edge;
                float z0 = roof.Cell.y * CellSize - edge, z1 = (roof.Cell.y + 1) * CellSize + edge;
                float roofY = BaseDeckY + roof.Level * StoreyHeightM;

                if (position.x >= x0 && position.x <= x1 && position.z >= z0 && position.z <= z1
                    && position.y < roofY - 0.5f)
                    return true;
            }
            return false;
        }

        /// <summary>How high a survivor can step up in one go, in metres.</summary>
        public const float StepUpM = 0.6f;

        /// <summary>
        /// The highest raised walking surface -- a floor slab or a stair tread -- under a
        /// raft-local position whose top is no higher than <paramref name="maxY"/>.
        /// Capping by height is what lets a survivor walk under a floor without being
        /// lifted onto it, and climb only by way of the stairs.
        /// </summary>
        public bool TryGetRaisedSurface(Vector3 position, float maxY, out float surfaceY)
        {
            const float edge = 0.15f;
            surfaceY = float.NegativeInfinity;

            foreach (PlacementKey key in _placementOrder)
            {
                float y;
                if (key.Socket == RoofSocket)
                {
                    float x0 = key.Cell.x * CellSize - edge, x1 = (key.Cell.x + 1) * CellSize + edge;
                    float z0 = key.Cell.y * CellSize - edge, z1 = (key.Cell.y + 1) * CellSize + edge;
                    if (position.x < x0 || position.x > x1 || position.z < z0 || position.z > z1) continue;
                    y = BaseDeckY + key.Level * StoreyHeightM + FloorTopAboveJointM;
                }
                else if (IsStairsSocket(key.Socket))
                {
                    Vector3 foot = WorldOf(key.Cell, key.Level);
                    Vector3 head = WorldOf(key.Cell + FacingStep(FacingOf(key)), key.Level + 1);
                    var run = new Vector2(head.x - foot.x, head.z - foot.z);
                    float runLength = run.magnitude;
                    Vector2 along = run / runLength;
                    var offset = new Vector2(position.x - foot.x, position.z - foot.z);

                    float t = Vector2.Dot(offset, along) / runLength;
                    float lateral = Mathf.Abs(along.x * offset.y - along.y * offset.x);
                    if (t < -0.05f || t > 1.05f || lateral > StairHalfWidthM) continue;
                    y = Mathf.Lerp(foot.y, head.y, Mathf.Clamp01(t)) + StairTreadAboveLineM;
                }
                else continue;

                if (y <= maxY + 1e-4f && y > surfaceY) surfaceY = y;
            }

            return !float.IsNegativeInfinity(surfaceY);
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
            int joint = Gravity.AddJoint(position);
            SplitMembersThrough(joint);
            return joint;
        }

        /// <summary>
        /// A new joint that lands part-way along a standing member -- a column on the
        /// middle of a 6 m starting beam -- splits that member in two, so the column
        /// bears on the beam. Without this the column's foot is a joint nothing else
        /// touches, and the column, and any roof on it, float free of the raft.
        ///
        /// The original member keeps its id and becomes the first half, so the
        /// placement map and the piece lookup still find it; the second half is
        /// registered alongside it.
        /// </summary>
        private void SplitMembersThrough(int joint)
        {
            Vector3 p = Gravity.Joints[joint].Position;
            int count = Gravity.Members.Count;
            for (int i = 0; i < count; i++)
            {
                Member m = Gravity.Members[i];
                if (m.IsFailed) continue;

                Vector3 a = Gravity.Joints[m.JointA].Position;
                Vector3 b = Gravity.Joints[m.JointB].Position;
                Vector3 ab = b - a;
                float lengthSq = ab.sqrMagnitude;
                if (lengthSq < 1e-6f) continue;

                float t = Vector3.Dot(p - a, ab) / lengthSq;
                if (t <= 0.01f || t >= 0.99f) continue;
                if ((a + ab * t - p).sqrMagnitude > 1e-4f) continue;

                int far = m.JointB;
                m.JointB = joint;
                m.LengthM = (p - a).magnitude;
                m.Midpoint = (a + p) * 0.5f;
                Member second = Gravity.AddMember(joint, far, m.Material, m.CrossSectionAreaM2, m.IsCantilever);
                second.SealedVolumeM3 = m.SealedVolumeM3 * (1f - t);
                m.SealedVolumeM3 *= t;
                second.WindAreaM2 = m.WindAreaM2 * (1f - t);
                second.WindNormal = m.WindNormal;
                m.WindAreaM2 *= t;

                if (_pieceByMember.TryGetValue(m.Id, out BuildPiece piece)) _pieceByMember[second.Id] = piece;
                foreach (List<int> ids in _placed.Values)
                {
                    if (ids.Contains(m.Id)) ids.Add(second.Id);
                }
            }
            Gravity.RecomputeAllDerived();
        }

        public static Vector3 WorldOf(Vector2Int cell) => WorldOf(cell, 0);

        public static Vector3 WorldOf(Vector2Int cell, int level)
        {
            return new Vector3(cell.x * CellSize, BaseDeckY + level * StoreyHeightM, cell.y * CellSize);
        }

        /// <summary>How far a joint may sit from deck level and still count as deck.</summary>
        private const float DeckLevelTolerance = 0.35f;

        /// <summary>How far outside the deck footprint the survivor still counts as aboard.</summary>
        private const float DeckMargin = 0.6f;

        /// <summary>
        /// True when a world position is standing on the raft's deck.
        ///
        /// Tested against the deck's footprint rather than against its beams, because
        /// beams are one-dimensional and the opening raft's deck is a 6 m perimeter frame
        /// with nothing across the middle. Measuring to the beams reports the spawn point
        /// -- the origin, the middle of the raft -- as open sea.
        ///
        /// The footprint is the convex hull of every joint at deck level, so it grows as
        /// the player places deck. Testing a hull rather than a bounding box keeps the
        /// corners honest for the rectangular growth this raft actually does.
        /// </summary>
        public bool IsOverDeck(Vector3 worldPos, float margin = DeckMargin)
        {
            List<Vector2> hull = DeckFootprint();
            if (hull.Count < 3) return false;

            var point = new Vector2(worldPos.x, worldPos.z);

            // A point is inside a convex polygon when it is left of every directed edge.
            bool inside = true;
            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 a = hull[i];
                Vector2 b = hull[(i + 1) % hull.Count];
                if (Cross(b - a, point - a) < 0f) { inside = false; break; }
            }

            if (inside) return true;

            // Outside the hull, allow a step onto the edge. Without this the survivor would
            // have to land exactly inside the outline to get back aboard.
            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 a = hull[i];
                Vector2 b = hull[(i + 1) % hull.Count];
                if (DistanceSqToSegment(point, a, b) <= margin * margin) return true;
            }

            return false;
        }

        /// <summary>
        /// The closest point on the deck outline to a position, and the horizontal
        /// direction from there onto the deck. Where a survivor in the water climbs out.
        /// Returns false when there is no deck.
        /// </summary>
        public bool NearestDeckEdge(Vector3 position, out Vector3 edge, out Vector3 inward)
        {
            edge = position;
            inward = Vector3.zero;

            List<Vector2> hull = DeckFootprint();
            if (hull.Count < 3) return false;

            var point = new Vector2(position.x, position.z);
            float best = float.MaxValue;
            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 a = hull[i];
                Vector2 side = hull[(i + 1) % hull.Count] - a;
                float t = Mathf.Clamp01(Vector2.Dot(point - a, side) / side.sqrMagnitude);
                Vector2 closest = a + side * t;
                float distanceSq = (point - closest).sqrMagnitude;
                if (distanceSq >= best) continue;

                best = distanceSq;
                // The hull winds counter-clockwise, so the left normal points inside.
                Vector2 normal = new Vector2(-side.y, side.x).normalized;
                edge = new Vector3(closest.x, position.y, closest.y);
                inward = new Vector3(normal.x, 0f, normal.y);
            }
            return true;
        }

        /// <summary>
        /// The deck's outline in the XZ plane, as a convex hull over deck-level joints.
        ///
        /// Columns, pontoons and braces do not contribute: their joints are not at deck
        /// level, so this describes the horizontal walking surface and not the whole raft.
        /// </summary>
        public List<Vector2> DeckFootprint()
        {
            var points = new List<Vector2>();

            foreach (Joint joint in Gravity.Joints)
            {
                if (Mathf.Abs(joint.Position.y - BaseDeckY) > DeckLevelTolerance) continue;
                points.Add(new Vector2(joint.Position.x, joint.Position.z));
            }

            return ConvexHull(points);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>
        /// Andrew's monotone chain. Small enough to keep here rather than take a geometry
        /// dependency for one convex hull.
        /// </summary>
        private static List<Vector2> ConvexHull(List<Vector2> input)
        {
            var points = new List<Vector2>(input);
            points.RemoveAll(p => float.IsNaN(p.x) || float.IsNaN(p.y));
            if (points.Count < 3) return points;

            points.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

            var hull = new List<Vector2>(points.Count * 2);

            foreach (Vector2 p in points)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 2]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }

            int lower = hull.Count + 1;
            for (int i = points.Count - 2; i >= 0; i--)
            {
                Vector2 p = points[i];
                while (hull.Count >= lower && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 2]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }

            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        /// <summary>Squared distance from a point to a segment.</summary>
        private static float DistanceSqToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-6f) return (point - a).sqrMagnitude;

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            Vector2 closest = a + ab * t;
            return (point - closest).sqrMagnitude;
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
