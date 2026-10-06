using DesalEra.Structure;
using UnityEngine;

namespace DesalEra.Samples
{
    /// <summary>
    /// Builders for the structure shapes the prototype needs to exercise the solver.
    /// Everything is deterministic so tests can assert on exact numbers.
    ///
    /// Named Structures rather than Prefabs on purpose: a type called Prefabs reads
    /// like scene prefabs, which these are not. They are code-built graph fixtures.
    /// </summary>
    public static class Structures
    {
        /// <summary>
        /// A rectangular grid of pontoon columns plus a deck on top.
        /// This is the "raft" the player starts from, so it must float.
        /// </summary>
        public static StructureSolver Raft(
            int width = 2,
            int depth = 2,
            int columnHeight = 1,
            float spacing = 2f,
            MaterialKind pontoon = MaterialKind.Plastic,
            MaterialKind deck = MaterialKind.Wood)
        {
            var solver = new StructureSolver { WaterLevelY = 0f };

            var baseJoints = new int[width, depth];
            var topJoints = new int[width, depth];

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    float px = (x - (width - 1) * 0.5f) * spacing;
                    float pz = (z - (depth - 1) * 0.5f) * spacing;
                    baseJoints[x, z] = solver.AddJoint(new Vector3(px, -0.5f, pz));
                    topJoints[x, z] = solver.AddJoint(new Vector3(px, -0.5f + columnHeight, pz));
                }
            }

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    solver.AddMember(baseJoints[x, z], topJoints[x, z], pontoon, 0.12f);
                }
            }

            // Deck planks spanning each row and column of the top ring.
            for (int x = 0; x + 1 < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    solver.AddMember(topJoints[x, z], topJoints[x + 1, z], deck);
                }
            }

            for (int z = 0; z + 1 < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    solver.AddMember(topJoints[x, z], topJoints[x, z + 1], deck);
                }
            }

            solver.RecomputeAllDerived();
            return solver;
        }

        /// <summary>
        /// A single vertical column of storeys, each one a column capped by a
        /// cantilevered deck beam. This is the shape that answers the central design
        /// question: does stacking tall actually load the base harder?
        ///
        /// Members are added bottom-up in pairs, so even indices are the columns and
        /// odd indices are the deck beams. Tests rely on that ordering.
        /// </summary>
        /// <param name="levels">Number of storeys.</param>
        /// <param name="storeyHeight">Floor-to-floor height in metres.</param>
        /// <param name="column">Material for the vertical load-bearing members.</param>
        /// <param name="deck">Material for the cantilevered deck arms.</param>
        /// <param name="deckArmLength">How far each deck reaches out along +X.</param>
        /// <param name="columnAreaM2">Column cross-section. 0.09 m2 is a 300 mm post.</param>
        /// <param name="deckAreaM2">
        /// Deck cross-section. 0.05 m2 is a plank, not a girder. An earlier fixture used
        /// 0.09 m2 of steel here, which made every deck weigh a tonne and correctly
        /// overloaded the column below it. Fixture geometry has to be plausible or the
        /// material curve reads as harsh when the structure is merely silly.
        /// </param>
        public static StructureSolver Tower(
            int levels = 6,
            float storeyHeight = 3f,
            MaterialKind column = MaterialKind.Wood,
            MaterialKind deck = MaterialKind.Wood,
            float deckArmLength = 1.5f,
            float columnAreaM2 = 0.09f,
            float deckAreaM2 = 0.05f)
        {
            var solver = new StructureSolver { WaterLevelY = 0f };

            var columnJoints = new int[levels + 1];
            for (int i = 0; i <= levels; i++)
            {
                columnJoints[i] = solver.AddJoint(new Vector3(0f, i * storeyHeight, 0f), anchored: i == 0);
            }

            for (int i = 0; i < levels; i++)
            {
                solver.AddMember(columnJoints[i], columnJoints[i + 1], column, columnAreaM2);

                int cap = solver.AddJoint(new Vector3(deckArmLength, (i + 1) * storeyHeight, 0f));
                solver.AddMember(columnJoints[i + 1], cap, deck, deckAreaM2, cantilever: true);
            }

            solver.RecomputeAllDerived();
            return solver;
        }

        /// <summary>
        /// The axial loads of a Tower's column members, bottom-up. Helper so tests do
        /// not hardcode the interleaved member ordering.
        /// </summary>
        public static float[] ColumnLoads(StructureSolver solver, int levels)
        {
            var loads = new float[levels];
            for (int i = 0; i < levels; i++) loads[i] = solver.Members[i * 2].AxialLoadKn;
            return loads;
        }

        /// <summary>
        /// A balanced pontoon raft carrying a heavy slab deliberately offset to one
        /// side, used to verify the capsize detector fires on center-of-mass drift.
        /// </summary>
        public static StructureSolver LopsidedRaft(float offsetX = 6f)
        {
            var solver = new StructureSolver { WaterLevelY = 0f };

            int leftA = solver.AddJoint(new Vector3(-1f, -0.5f, 0f));
            int leftB = solver.AddJoint(new Vector3(-1f, 0.5f, 0f));
            int rightA = solver.AddJoint(new Vector3(1f, -0.5f, 0f));
            int rightB = solver.AddJoint(new Vector3(1f, 0.5f, 0f));

            solver.AddMember(leftA, leftB, MaterialKind.Plastic, 0.12f);
            solver.AddMember(rightA, rightB, MaterialKind.Plastic, 0.12f);

            int deckCenter = solver.AddJoint(new Vector3(0f, 0.5f, 0f));
            solver.AddMember(leftB, deckCenter, MaterialKind.Wood);
            solver.AddMember(rightB, deckCenter, MaterialKind.Wood);

            int heavyAnchor = solver.AddJoint(new Vector3(offsetX, 0.5f, 0f));
            solver.AddMember(deckCenter, heavyAnchor, MaterialKind.Steel, 0.2f);

            solver.RecomputeAllDerived();
            return solver;
        }
    }
}
