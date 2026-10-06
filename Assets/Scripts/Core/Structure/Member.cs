using UnityEngine;

namespace DesalEra.Structure
{
    /// <summary>
    /// A structural member: a beam or column spanning two joints.
    /// Pure data plus derived load metrics, so the whole solver stays testable
    /// without an engine running.
    /// </summary>
    public sealed class Member
    {
        public const float WaterDensityKgPerM3 = 1000f;

        public int Id;
        public int JointA;
        public int JointB;

        public Vector3 Midpoint;
        public float LengthM = 1f;
        public float CrossSectionAreaM2 = 0.09f;

        public MaterialKind Material = MaterialKind.Wood;
        public FailureMode Failure = FailureMode.Fracture;

        /// <summary>
        /// True when JointB is a free end rather than a support, as on a deck beam
        /// that sticks out past its column.
        ///
        /// A two-supported member splits its weight between both joints, but a
        /// cantilever carries all of it on the root. Without this flag every deck
        /// beam stranded half its mass at a dead-end joint, and a 10-storey tower
        /// reported the base carrying 67 kN of its own 120 kN.
        /// </summary>
        public bool IsCantilever;

        public float MassKg;
        public float VolumeM3;
        public float BuoyantVolumeM3;

        /// <summary>
        /// Extra sealed displacement in m3, for hollow members whose volume encloses
        /// air. A real salvage pontoon is a drum: the shell weighs little while the air
        /// inside displaces a barrel of water. Modelling bulk density alone gave solid
        /// plastic 50 kg of lift per cubic metre, which is correct for a plastic slab
        /// and useless for a barrel, and left the opening raft unable to float at any
        /// sane size.
        /// </summary>
        public float SealedVolumeM3;

        /// <summary>Total displacement: the material's own buoyancy plus sealed air.</summary>
        public float TotalBuoyantVolumeM3 => BuoyantVolumeM3 + SealedVolumeM3;

        /// <summary>Unit vector from JointA to JointB. The truss solver's axis.</summary>
        public Vector3 Axis;

        /// <summary>
        /// Axial rigidity EA in kN, the stiffness the truss solver relaxes against.
        /// kN rather than N because every other force in the model is in kN.
        /// </summary>
        public float AxialRigidityKn => MaterialProperties.YoungsModulusGPa(Material) * 1e6f * CrossSectionAreaM2 / 1000f;

        /// <summary>
        /// Axial capacity in compression, kN. Compression is stronger than tension
        /// for these materials, so a brace is a poor tie.
        /// </summary>
        public float CompressionCapacityKn => AxialCapacityKn;

        /// <summary>Axial capacity in tension, kN. Much the weaker direction.</summary>
        public float TensionCapacityKn => MaterialProperties.TensileCapacityKnPerM2(Material) * CrossSectionAreaM2;

        /// <summary>
        /// Axial load in kN. Positive is tension, negative compression. The truss
        /// solver writes this; the gravity-flow solver also uses it, so the sign
        /// convention holds across both.
        /// </summary>
        public float AxialLoadKn;

        /// <summary>Bending moment magnitude in kN*m.</summary>
        public float BendingMomentKnM;

        /// <summary>
        /// How much mass this member has already handed to the joint below it. The
        /// load solver works as a fixpoint, so it tracks what it has delivered and
        /// only ever sends the increment. Without this, re-processing a joint would
        /// deliver its full load again and the base would count load twice.
        /// </summary>
        public float DeliveredMassKg;

        /// <summary>True once a level member has handed its load sideways, so beams
        /// that form a ring settle instead of passing mass around forever.</summary>
        public bool LevelMassDelivered;

        public bool IsFailed;

        // Capacities scale with cross-section, so a wider member is genuinely
        // stronger rather than merely heavier.
        public float AxialCapacityKn => MaterialProperties.MaxAxialLoadKn(Material, CrossSectionAreaM2);

        public float BendingCapacityKnM => MaterialProperties.MaxBendingMomentKnM(Material, CrossSectionAreaM2);

        /// <summary>
        /// 0 = unloaded, 1 = at capacity in the governing direction, above 1 = failed.
        ///
        /// Tension and compression are compared against different capacities. Without
        /// that split a slender brace reports as strong in tension because it is
        /// rated for compression, and the whole point of choosing between an X-brace
        /// and a tie disappears.
        /// </summary>
        public float Utilization
        {
            get
            {
                float bendCap = BendingCapacityKnM;
                float bendUtil = bendCap <= 0f ? float.MaxValue : BendingMomentKnM / bendCap;

                if (AxialLoadKn >= 0f)
                {
                    float tensionCap = TensionCapacityKn;
                    float tensionUtil = tensionCap <= 0f ? float.MaxValue : AxialLoadKn / tensionCap;
                    return Mathf.Max(tensionUtil, bendUtil);
                }

                float compressionCap = CompressionCapacityKn;
                float compressionUtil = compressionCap <= 0f ? float.MaxValue : -AxialLoadKn / compressionCap;
                return Mathf.Max(compressionUtil, bendUtil);
            }
        }

        /// <summary>True when the member is being stretched rather than squeezed.</summary>
        public bool IsInTension => AxialLoadKn > 0f;

        public void RecomputeDerived()
        {
            VolumeM3 = CrossSectionAreaM2 * LengthM;
            MassKg = MaterialProperties.DensityKgPerM3(Material) * VolumeM3;

            // Only materials lighter than water contribute net buoyancy.
            BuoyantVolumeM3 = MaterialProperties.DensityKgPerM3(Material) < WaterDensityKgPerM3
                ? VolumeM3
                : 0f;
        }

        /// <summary>Recomputes the axis. Call after either joint moves.</summary>
        public void RecomputeAxis(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            Axis = delta.sqrMagnitude < 1e-10f ? Vector3.up : delta.normalized;
        }
    }

    /// <summary>
    /// A node where members meet. Joints accumulate the load of everything resting
    /// on them, which is what makes stacking tall actually get harder.
    /// </summary>
    public sealed class Joint
    {
        public int Id;
        public Vector3 Position;
        public bool IsAnchored;

        public float AccumulatedLoadKn;
        public float AccumulatedMassKg;
    }

    public enum MemberState
    {
        Intact,
        Strained,
        Failed
    }

    public sealed class SolveReport
    {
        public int IterationCount;
        public int FailedMemberCount;
        public bool IsFloating;
        public bool IsCapsizing;
        public Vector3 CenterOfMass;
        public Vector3 CenterOfBuoyancy;
        public float TotalMassKg;
        public float BuoyantVolumeM3;
        public float BuoyancyKn;
        public float WeightKn;
        public float NetVerticalForceKn;
        public float CapsizeTorqueKnM;

        /// <summary>Horizontal distance between center of mass and center of buoyancy.</summary>
        public float CapsizeOffsetM;

        public readonly System.Collections.Generic.List<Member> FailedMembers =
            new System.Collections.Generic.List<Member>();
    }
}
