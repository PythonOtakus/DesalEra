using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesalEra.Structure
{
    /// <summary>
    /// The structural graph: joints, members, and the load/buoyancy solver.
    ///
    /// This is the kill-or-live prototype for the whole project. The design question
    /// it answers is whether "real physics vertical building" is achievable as a
    /// deterministic, tunable, agent-editable model rather than a PhysX ragdoll.
    ///
    /// Model, in one paragraph:
    ///   - Weight accumulates by flowing through the joint graph, so everything
    ///     resting on a joint transfers its mass into the members below it.
    ///   - Members whose axial load or bending exceeds material capacity fail and
    ///     shed their load, which can push neighbours over their own limits. That
    ///     cascade is the collapse.
    ///   - Buoyancy comes from members whose material is lighter than water, scaled
    ///     by how much of the member sits below the waterline.
    ///   - If the center of mass drifts horizontally away from the center of
    ///     buoyancy, the structure capsizes. This is the "slow tilt" from the brief.
    /// </summary>
    public sealed class StructureSolver
    {
        public const float WaterDensityKgPerM3 = 1000f;

        /// <summary>Beyond this offset the structure is considered capsizing.</summary>
        public const float CapsizeOffsetToleranceM = 2.5f;

        /// <summary>Bounds the load-flow fixpoint so a pathological graph cannot hang.</summary>
        private const int MaxLoadFlowSteps = 8192;

        public readonly List<Joint> Joints = new List<Joint>();
        public readonly List<Member> Members = new List<Member>();

        private int _nextJointId;
        private int _nextMemberId;

        public float WaterLevelY;
        public float GravityMPerS2 = 9.81f;

        /// <summary>
        /// Global multiplier on buoyancy, the tuning knob for "how many pontoons
        /// do I actually need". 1.0 is physically neutral.
        /// </summary>
        public float BuoyancyScale = 1f;

        /// <summary>
        /// Load amplification. Physics-accurate values make towers either trivially
        /// safe or instantly collapse; this is the designer-facing dial and it is
        /// what turns the model into a game system instead of an engineering tool.
        /// </summary>
        public float LoadScale = 1f;

        /// <summary>
        /// Adds a joint and returns its index. Indices are what members reference,
        /// which keeps the graph free of dangling object references.
        /// </summary>
        public int AddJoint(Vector3 position, bool anchored = false)
        {
            Joints.Add(new Joint
            {
                Id = _nextJointId++,
                Position = position,
                IsAnchored = anchored
            });
            return Joints.Count - 1;
        }

        public Joint JointAt(int index) => Joints[index];

        public Member AddMember(int jointA, int jointB, MaterialKind material, float crossSectionAreaM2 = 0.09f)
        {
            return AddMember(jointA, jointB, material, crossSectionAreaM2, cantilever: false);
        }

        public Member AddMember(
            int jointA,
            int jointB,
            MaterialKind material,
            float crossSectionAreaM2,
            bool cantilever)
        {
            if (jointA < 0 || jointA >= Joints.Count) throw new ArgumentOutOfRangeException(nameof(jointA));
            if (jointB < 0 || jointB >= Joints.Count) throw new ArgumentOutOfRangeException(nameof(jointB));
            if (jointA == jointB) throw new ArgumentException("a member needs two distinct joints");

            Vector3 a = Joints[jointA].Position;
            Vector3 b = Joints[jointB].Position;
            float length = (b - a).magnitude;
            if (length < 1e-3f) throw new ArgumentException("member length must be non-zero");

            var member = new Member
            {
                Id = _nextMemberId++,
                JointA = jointA,
                JointB = jointB,
                Midpoint = (a + b) * 0.5f,
                LengthM = length,
                CrossSectionAreaM2 = crossSectionAreaM2,
                Material = material,
                IsCantilever = cantilever
            };
            member.RecomputeDerived();
            Members.Add(member);
            return member;
        }

        public void RecomputeAllDerived()
        {
            foreach (Member m in Members)
            {
                m.RecomputeDerived();
                m.RecomputeAxis(Joints[m.JointA].Position, Joints[m.JointB].Position);
            }
        }

        public int LiveMemberCount
        {
            get
            {
                int n = 0;
                foreach (Member m in Members)
                {
                    if (!m.IsFailed) n++;
                }
                return n;
            }
        }

        /// <summary>
        /// Runs the solver until the structure stops changing or the iteration
        /// budget runs out. Every collapse cascades, so this normally converges in
        /// two or three passes.
        /// </summary>
        public SolveReport SolveToEquilibrium(int maxIterations = 16)
        {
            SolveReport report = Solve();
            int totalFailed = 0;

            for (int i = 0; i < maxIterations; i++)
            {
                report = Solve();
                int failedThisPass = CollapseOverstressed(report);
                totalFailed += failedThisPass;
                if (failedThisPass == 0)
                {
                    report.IterationCount = i + 1;
                    report.FailedMemberCount = totalFailed;
                    return report;
                }
            }

            report.IterationCount = maxIterations;
            report.FailedMemberCount = totalFailed;
            return report;
        }

        public SolveReport Solve()
        {
            var report = new SolveReport();
            if (Members.Count == 0) return report;

            AccumulateMassAndBuoyancy(report);
            PropagateLoad();
            ComputeCantileverBending();
            ComputeCapsize(report);
            return report;
        }

        private void AccumulateMassAndBuoyancy(SolveReport report)
        {
            float totalMass = 0f;
            float totalBuoyantVolume = 0f;
            Vector3 massWeighted = Vector3.zero;
            Vector3 buoyancyWeighted = Vector3.zero;

            for (int i = 0; i < Joints.Count; i++) Joints[i].AccumulatedMassKg = 0f;

            foreach (Member m in Members)
            {
                if (m.IsFailed) continue;

                totalMass += m.MassKg;
                massWeighted += m.Midpoint * m.MassKg;

                float submerged = SubmergedFraction(m);
                float volume = m.TotalBuoyantVolumeM3 * submerged * BuoyancyScale;
                totalBuoyantVolume += volume;
                buoyancyWeighted += m.Midpoint * volume;

                // A cantilever hangs entirely off its root joint; a two-supported
                // member splits its weight between both ends.
                if (m.IsCantilever)
                {
                    Joints[m.JointA].AccumulatedMassKg += m.MassKg;
                }
                else
                {
                    Joints[m.JointA].AccumulatedMassKg += m.MassKg * 0.5f;
                    Joints[m.JointB].AccumulatedMassKg += m.MassKg * 0.5f;
                }
            }

            report.TotalMassKg = totalMass;
            report.BuoyantVolumeM3 = totalBuoyantVolume;
            report.BuoyancyKn = totalBuoyantVolume * WaterDensityKgPerM3 * GravityMPerS2 / 1000f;
            report.WeightKn = totalMass * GravityMPerS2 / 1000f;
            report.NetVerticalForceKn = report.BuoyancyKn - report.WeightKn;
            report.IsFloating = report.NetVerticalForceKn >= 0f;

            report.CenterOfMass = totalMass > 1e-6f ? massWeighted / totalMass : Vector3.zero;
            report.CenterOfBuoyancy = totalBuoyantVolume > 1e-6f
                ? buoyancyWeighted / totalBuoyantVolume
                : Vector3.zero;
        }

        /// <summary>Fraction of a member's length below the waterline, 0 to 1.</summary>
        private float SubmergedFraction(Member m)
        {
            if (m.Midpoint.y <= WaterLevelY) return 1f;

            float half = m.LengthM * 0.5f;
            if (m.Midpoint.y - half >= WaterLevelY) return 0f;

            return Mathf.Clamp01((m.Midpoint.y + half - WaterLevelY) / m.LengthM);
        }

        /// <summary>
        /// Load flow as a fixpoint, accumulating MASS in kilograms and converting to
        /// force only at the reporting boundary.
        ///
        /// Two bugs shaped this. First, mixing kg and kN in one accumulator made the
        /// base carry exactly one storey no matter how tall the tower got. Second,
        /// once that was fixed, a strictly top-down walk still lost the far half of
        /// every horizontal member: a deck beam's weight landed on its outer joint,
        /// which is a dead end, so it never reached the column underneath.
        ///
        /// A worklist fixes the second case, because mass can travel sideways along a
        /// deck and then down a column. Four rules keep it terminating and correct:
        ///   - descending members pass mass down in proportion to how much of their
        ///     span lies below the current joint,
        ///   - level members hand mass to their far joint exactly once, so a ring of
        ///     beams settles instead of circulating forever,
        ///   - every member tracks what it has already delivered and only sends the
        ///     increment, so re-processing a joint never double counts,
        ///   - cantilevers deliver nothing onward, because their far end is a free
        ///     tip and mass sent there would be stranded in mid-air.
        /// </summary>
        private void PropagateLoad()
        {
            for (int i = 0; i < Members.Count; i++)
            {
                Members[i].AxialLoadKn = 0f;
                Members[i].BendingMomentKnM = 0f;
                Members[i].DeliveredMassKg = 0f;
                Members[i].LevelMassDelivered = false;
            }

            var queue = new List<int>(Joints.Count);
            var queued = new bool[Joints.Count];

            for (int i = 0; i < Joints.Count; i++)
            {
                Joints[i].AccumulatedLoadKn = 0f;
                queue.Add(i);
                queued[i] = true;
            }

            int head = 0;
            int steps = 0;

            while (head < queue.Count && steps++ < MaxLoadFlowSteps)
            {
                int jointIndex = queue[head++];
                queued[jointIndex] = false;

                Joint j = Joints[jointIndex];
                float supportedMassKg = j.AccumulatedMassKg;
                j.AccumulatedLoadKn = supportedMassKg * GravityMPerS2 / 1000f * LoadScale;

                for (int mi = 0; mi < Members.Count; mi++)
                {
                    Member m = Members[mi];
                    if (m.IsFailed) continue;
                    if (m.IsCantilever) continue;
                    if (m.JointA != jointIndex && m.JointB != jointIndex) continue;

                    int otherIndex = m.JointA == jointIndex ? m.JointB : m.JointA;
                    bool otherIsAnchor = Joints[otherIndex].IsAnchored;

                    float dropY = j.Position.y - Joints[otherIndex].Position.y;

                    // Only members that descend from this joint, or sit level with it,
                    // can be carrying anything the joint holds. A member that climbs
                    // away from the joint is handled when its upper end is processed,
                    // so touching it here would double count and, worse, would treat
                    // every upward column as if it were a bending beam.
                    if (dropY < -1e-3f) continue;

                    bool descends = dropY > 1e-3f;
                    if (!descends && m.LevelMassDelivered) continue;

                    float targetMassKg = descends
                        ? supportedMassKg * (dropY / m.LengthM)
                        : supportedMassKg * 0.5f;

                    float increment = targetMassKg - m.DeliveredMassKg;
                    if (increment <= 1e-6f) continue;

                    m.DeliveredMassKg = targetMassKg;
                    m.AxialLoadKn = m.DeliveredMassKg * GravityMPerS2 / 1000f * LoadScale;

                    if (!descends)
                    {
                        m.LevelMassDelivered = true;
                        // A beam takes bending from what rests on it; worst at mid-span.
                        m.BendingMomentKnM = m.DeliveredMassKg * GravityMPerS2 / 1000f
                                             * LoadScale * m.LengthM * 0.5f;
                    }

                    Joints[otherIndex].AccumulatedMassKg += increment;

                    // An anchor terminates the flow: it still records what it is
                    // carrying, but nothing propagates below it. Skipping anchors
                    // outright was what left every base column reading 0.4 kN.
                    if (otherIsAnchor) continue;

                    if (!queued[otherIndex])
                    {
                        queued[otherIndex] = true;
                        queue.Add(otherIndex);
                    }
                }
            }
        }

        /// <summary>
        /// A cantilever carries no axial load, so its whole strength check has to come
        /// from bending. Skipping this left cantilever beams reporting a utilization
        /// of zero no matter how long they were, which would let a player hang an
        /// unbounded deck off a single joint.
        ///
        /// The moment is w*L^2/2 for a uniformly loaded cantilever, plus whatever the
        /// tip joint is holding, which acts as a point load at the full lever arm.
        /// </summary>
        private void ComputeCantileverBending()
        {
            foreach (Member m in Members)
            {
                if (m.IsFailed || !m.IsCantilever) continue;

                m.AxialLoadKn = 0f;

                float selfWeightKn = m.MassKg * GravityMPerS2 / 1000f * LoadScale;
                float tipLoadKn = Joints[m.JointB].AccumulatedMassKg
                                  * GravityMPerS2 / 1000f * LoadScale;

                m.BendingMomentKnM = selfWeightKn * m.LengthM * 0.5f
                                     + tipLoadKn * m.LengthM;
            }
        }

        private void ComputeCapsize(SolveReport report)
        {
            Vector3 delta = report.CenterOfMass - report.CenterOfBuoyancy;
            report.CapsizeOffsetM = new Vector3(delta.x, 0f, delta.z).magnitude;
            report.CapsizeTorqueKnM = report.CapsizeOffsetM * report.BuoyancyKn;
            report.IsCapsizing = report.CapsizeOffsetM > CapsizeOffsetToleranceM;
        }

        private int CollapseOverstressed(SolveReport report)
        {
            int count = 0;
            foreach (Member m in Members)
            {
                if (m.IsFailed) continue;
                if (m.Utilization <= 1f) continue;

                m.IsFailed = true;
                count++;
                report.FailedMembers.Add(m);
            }
            return count;
        }

        public void ResetFailures()
        {
            foreach (Member m in Members) m.IsFailed = false;
        }
    }
}
