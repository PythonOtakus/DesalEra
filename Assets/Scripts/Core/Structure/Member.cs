using UnityEngine;

namespace CrazyAquarium.Structure
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

        /// <summary>Axial load in kilonewtons.</summary>
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

        /// <summary>0 = unloaded, 1 = at capacity, above 1 = over capacity.</summary>
        public float Utilization
        {
            get
            {
                float axialCap = AxialCapacityKn;
                float bendCap = BendingCapacityKnM;
                float axialUtil = axialCap <= 0f ? float.MaxValue : Mathf.Abs(AxialLoadKn) / axialCap;
                float bendUtil = bendCap <= 0f ? float.MaxValue : BendingMomentKnM / bendCap;
                return Mathf.Max(axialUtil, bendUtil);
            }
        }

        public void RecomputeDerived()
        {
            VolumeM3 = CrossSectionAreaM2 * LengthM;
            MassKg = MaterialProperties.DensityKgPerM3(Material) * VolumeM3;

            // Only materials lighter than water contribute net buoyancy.
            BuoyantVolumeM3 = MaterialProperties.DensityKgPerM3(Material) < WaterDensityKgPerM3
                ? VolumeM3
                : 0f;
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
