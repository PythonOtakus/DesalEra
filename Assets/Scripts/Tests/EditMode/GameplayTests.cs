using DesalEra.Game;
using DesalEra.Structure;
using NUnit.Framework;
using UnityEngine;

// UnityEngine defines a physics Joint, and UnityEngine.Vector3 has no Round.
using Joint = DesalEra.Structure.Joint;

namespace DesalEra.Tests
{
    /// <summary>
    /// Tests for the playable loop: survival pressure, salvage economy, and building.
    ///
    /// All of it runs in a plain dotnet process, which is the point. Survival and
    /// economy numbers are the most frequently tuned part of a game like this, and
    /// launching the editor to check that a 20-minute walk still ends with the player
    /// alive makes iteration needlessly slow.
    /// </summary>
    public sealed class GameplayTests
    {
        // --- survival ---

        [Test]
        public void Survival_ReservesDrainIndependently()
        {
            var s = new SurvivalModel();
            s.Advance(30f);

            Assert.Less(s.Water, s.Food,
                "thirst must bite faster than hunger, or players manage only food");
            Assert.Greater(s.Health, 0f);
            Assert.IsFalse(s.IsDead);
        }

        [Test]
        public void Survival_ExertionCostsMoreThanResting()
        {
            var resting = new SurvivalModel();
            var working = new SurvivalModel();

            resting.Advance(10f, exerting: false);
            working.Advance(10f, exerting: true);

            Assert.Less(working.Food, resting.Food, "working must cost more food");
            Assert.Less(working.Water, resting.Water, "working must cost more water");
            Assert.Less(working.Stamina, resting.Stamina, "working must cost stamina");
        }

        [Test]
        public void Survival_StaminaRecoversOnlyWhenIdle()
        {
            var s = new SurvivalModel();
            s.Set(Vital.Stamina, 10f);
            s.Advance(1f, exerting: false);
            Assert.Greater(s.Stamina, 10f, "standing still must restore stamina");

            s.Set(Vital.Stamina, 50f);
            s.Advance(1f, exerting: true);
            Assert.Less(s.Stamina, 50f, "exerting must drain stamina");
        }

        [Test]
        public void Survival_HealthOnlyBleedsWhenAReservoirIsEmpty()
        {
            var s = new SurvivalModel();
            s.Advance(20f);
            Assert.AreEqual(SurvivalModel.MaxValue, s.Health, 0.01f,
                "draining food and water alone must not damage health");

            s.Set(Vital.Food, 0f);
            s.Advance(5f);
            Assert.Less(s.Health, SurvivalModel.MaxValue,
                "an empty food store must start costing health");
        }

        [Test]
        public void Survival_IgnoringBothReservoirsIsFatal()
        {
            var s = new SurvivalModel();
            s.Set(Vital.Food, 0f);
            s.Set(Vital.Water, 0f);

            s.Advance(60f);

            Assert.IsTrue(s.IsDead, "a player who never eats or drinks must die");
        }

        [Test]
        public void Survival_DeathIsRecoverableOnlyByReset()
        {
            var s = new SurvivalModel();
            s.Set(Vital.Health, 1f);
            s.Set(Vital.Water, 0f);
            s.Advance(5f);
            Assert.IsTrue(s.IsDead);

            s.ConsumeRation();
            Assert.IsTrue(s.IsDead, "a ration must not resurrect a dead player");

            s.Advance(10f);
            Assert.AreEqual(0f, s.Health, "and a dead model must stop simulating");

            s.Reset();
            Assert.IsFalse(s.IsDead);
            Assert.AreEqual(SurvivalModel.MaxValue, s.Health, 0.01f);
        }

        [Test]
        public void Survival_HealingRequiresBothReservoirs()
        {
            var s = new SurvivalModel();
            s.Set(Vital.Health, 40f);
            s.Set(Vital.Food, 50f);
            s.Set(Vital.Water, 0f);
            s.TryHeal(5f);
            Assert.AreEqual(40f, s.Health, 0.01f, "cannot heal while dehydrated");

            s.Set(Vital.Water, 50f);
            s.TryHeal(5f);
            Assert.Greater(s.Health, 40f, "rest with both reserves up must heal");
        }

        [Test]
        public void Survival_ReservesCanGoNegativeButHealthCannot()
        {
            var s = new SurvivalModel();
            s.Set(Vital.Food, 0f);
            s.Advance(30f);

            Assert.Less(s.Food, 0f, "a reserve must show how far past empty it is");
            Assert.GreaterOrEqual(s.Health, 0f, "health must bottom out at zero, not invert");
        }

        [Test]
        public void Survival_RationIsAStopgapNotASolution()
        {
            var s = new SurvivalModel();
            s.Set(Vital.Health, 30f);
            s.Set(Vital.Food, 0f);
            s.Set(Vital.Water, 0f);

            s.ConsumeRation();
            Assert.Greater(s.Food, 0f, "a ration must top up food");
            Assert.Greater(s.Health, 30f, "and buy some health back");

            s.Advance(120f);
            Assert.IsTrue(s.IsDead, "but one ration cannot sustain a player indefinitely");
        }

        // --- inventory ---

        [Test]
        public void Inventory_SpendIsAllOrNothing()
        {
            var inv = new Inventory();
            inv.Add(ResourceKind.Metal, 5);

            var tooExpensive = new System.Collections.Generic.Dictionary<ResourceKind, int>
            {
                { ResourceKind.Metal, 3 },
                { ResourceKind.Plank, 9 }
            };

            Assert.IsFalse(inv.TrySpend(tooExpensive));
            Assert.AreEqual(5, inv.Get(ResourceKind.Metal),
                "a refused purchase must not charge part of the cost");
        }

        [Test]
        public void Inventory_DismantleRefundsOnlyPart()
        {
            var inv = new Inventory();
            inv.Add(ResourceKind.Plank, 10);

            var cost = new System.Collections.Generic.Dictionary<ResourceKind, int> { { ResourceKind.Plank, 8 } };
            Assert.IsTrue(inv.TrySpend(cost));
            Assert.AreEqual(2, inv.Get(ResourceKind.Plank));

            int recovered = inv.RefundFraction(cost, 0.5f);
            Assert.AreEqual(4, recovered, "dismantling should return half the cost");
            Assert.AreEqual(6, inv.Get(ResourceKind.Plank));

            // A full refund would make demolition a free undo.
            Assert.Less(inv.Get(ResourceKind.Plank), 10);
        }

        // --- raft and building ---

        [Test]
        public void Raft_OpeningStateFloats()
        {
            var raft = new RaftState();
            SolveReport report = raft.SolveBuoyancy();

            Assert.IsTrue(report.IsFloating,
                $"the opening raft must float, net force was {report.NetVerticalForceKn:F1} kN");
        }

        [Test]
        public void Raft_KeelSitsDeepEnoughForSwell()
        {
            // Posts must reach below typical swell amplitude or the shelter reads as
            // perched on a film of water. KeelDepth covers ~1 m swell with margin.
            Assert.GreaterOrEqual(RaftState.KeelDepthM, 1.2f);
            Assert.Greater(RaftState.BaseDeckY, 0f);

            var raft = new RaftState();
            float minY = float.MaxValue;
            foreach (var joint in raft.Gravity.Joints)
                minY = Mathf.Min(minY, joint.Position.y);

            Assert.LessOrEqual(minY, RaftState.WaterLevelY - 1.2f,
                $"keel joint at {minY:F2} is too shallow under still water");
        }

        [Test]
        public void Raft_OpeningStateIsStructurallyStable()
        {
            var raft = new RaftState();
            TrussReport report = raft.SolveWind();

            Assert.IsTrue(report.Converged,
                $"the opening raft must not be a mechanism: {report.FailureReason}");
            Assert.IsFalse(report.IsUnstable);
        }

        [Test]
        public void Raft_OpeningKitIsEnoughToBuild()
        {
            var raft = new RaftState();

            string error = raft.TryPlace(BuildPiece.Pontoon(), new Vector2Int(0, 1));
            Assert.IsNull(error,
                $"the starting kit must afford a pontoon immediately: {error}");
        }

        [Test]
        public void Raft_BuildingSpendsAndCannotRebuildOnTheSameCell()
        {
            var raft = new RaftState();
            int before = raft.MemberCount;

            // A deck corner, so the column lands on an existing joint and splits nothing.
            Assert.IsNull(raft.TryPlace(BuildPiece.Column(), new Vector2Int(1, 1)));
            Assert.AreEqual(before + 1, raft.MemberCount);

            string error = raft.TryPlace(BuildPiece.Column(), new Vector2Int(1, 1));
            Assert.IsNotNull(error, "a second piece on an occupied cell must be refused");
            Assert.AreEqual(before + 1, raft.MemberCount, "and must not add a member");
        }

        [Test]
        public void Raft_BuildingRefusedWhenUnaffordable()
        {
            var raft = new RaftState();
            raft.Inventory.Clear();
            int before = raft.MemberCount;

            string error = raft.TryPlace(BuildPiece.Brace(), new Vector2Int(1, 1));

            Assert.IsNotNull(error, "building with no resources must be refused");
            Assert.AreEqual(before, raft.MemberCount);
        }

        [Test]
        public void Raft_DismantleRemovesThePieceAndKeepsTheRaftAfloat()
        {
            var raft = new RaftState();
            Assert.IsNull(raft.TryPlace(BuildPiece.Pontoon(), new Vector2Int(0, 1)));
            int afterBuild = raft.MemberCount;

            string error = raft.TryDismantle(new Vector2Int(0, 1));
            Assert.IsNull(error, $"dismantling should work: {error}");
            Assert.IsTrue(raft.IsCellOccupied(new Vector2Int(0, 1)) == false);
            Assert.AreEqual(afterBuild, raft.MemberCount, "the graph keeps the failed member");

            Assert.IsTrue(raft.SolveBuoyancy().IsFloating,
                "removing a player-built pontoon must not sink a raft that started afloat");
        }

        [Test]
        public void Raft_BuildingOutsideTheAllowedAreaIsRefused()
        {
            var raft = new RaftState();
            raft.Inventory.Add(ResourceKind.Plank, 50);
            raft.Inventory.Add(ResourceKind.Scrap, 50);

            string error = raft.TryPlace(BuildPiece.Deck(), new Vector2Int(99, 99));
            Assert.IsNotNull(error, "the raft must have a buildable boundary");
        }

        [Test]
        public void Raft_BuildingMustTouchTheExistingStructure()
        {
            var raft = new RaftState();
            raft.Inventory.Add(ResourceKind.Plank, 50);
            int before = raft.MemberCount;

            // Two cells away with nothing in between — a floating island.
            string error = raft.TryPlace(BuildPiece.Deck(), new Vector2Int(2, 0));
            Assert.AreEqual("must connect to the existing structure", error);
            Assert.AreEqual(before, raft.MemberCount);

            Assert.IsNull(raft.TryPlace(BuildPiece.Deck(), new Vector2Int(1, 0)),
                "an edge-adjacent cell must still be allowed");
            Assert.IsNull(raft.TryPlace(BuildPiece.Deck(), new Vector2Int(2, 0)),
                "after bridging, the next cell becomes reachable");
        }

        [Test]
        public void Raft_NoCoincidentJointsAfterBuilding()
        {
            // Building must never leave two joints at the same position. A duplicate
            // produces a zero-length member, which never carries load, so the piece
            // would silently do nothing while the player paid for it.
            var raft = new RaftState();
            raft.Inventory.Add(ResourceKind.Plank, 40);
            raft.Inventory.Add(ResourceKind.Scrap, 40);
            raft.Inventory.Add(ResourceKind.Metal, 40);

            foreach (Vector2Int cell in new[]
                     {
                         new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1),
                         new Vector2Int(1, 1), new Vector2Int(-1, 0)
                     })
            {
                raft.TryPlace(BuildPiece.Column(), cell);
            }

            int duplicates = 0;
            var seen = new System.Collections.Generic.HashSet<Vector3>();
            foreach (Joint joint in raft.Gravity.Joints)
            {
                // Rounded by hand: UnityEngine.Vector3 has no Round, and the exact
                // positions matter less than catching two nodes at one spot.
                Vector3 p = joint.Position;
                var key = new Vector3(
                    Mathf.Round(p.x * 100f) / 100f,
                    Mathf.Round(p.y * 100f) / 100f,
                    Mathf.Round(p.z * 100f) / 100f);
                if (!seen.Add(key)) duplicates++;
            }

            Assert.AreEqual(0, duplicates,
                $"{duplicates} coincident joints: a placed piece did not reuse its neighbours");
        }

        [Test]
        public void Raft_EveryMemberHasNonZeroLength()
        {
            var raft = new RaftState();
            raft.Inventory.Add(ResourceKind.Plank, 40);
            raft.Inventory.Add(ResourceKind.Scrap, 40);

            raft.TryPlace(BuildPiece.Column(), new Vector2Int(0, 1));
            raft.TryPlace(BuildPiece.Deck(), new Vector2Int(1, 0));
            raft.TryPlace(BuildPiece.Pontoon(), new Vector2Int(1, 1));

            foreach (Member m in raft.Gravity.Members)
            {
                Assert.Greater(m.LengthM, 1e-3f,
                    $"member {m.Id} has zero length and can never carry load");
            }
        }

        [Test]
        public void Raft_BuoyancyDeterioratesAsLoadIsAdded()
        {
            var raft = new RaftState();
            raft.Inventory.Add(ResourceKind.Plank, 200);
            raft.Inventory.Add(ResourceKind.Scrap, 200);

            float startingMargin = raft.SolveBuoyancy().NetVerticalForceKn;
            int previous = startingMargin > 0f ? 1 : -1;

            // Heavy wooden decks with no extra pontoons must eat into the margin.
            for (int i = 1; i <= 4; i++)
            {
                raft.TryPlace(BuildPiece.Deck(RaftState.CellSize), new Vector2Int(i, 0));
                float margin = raft.SolveBuoyancy().NetVerticalForceKn;
                Assert.Less(margin, startingMargin,
                    "adding dead weight without buoyancy must reduce the margin");
                previous = margin > 0f ? 1 : -1;
            }
        }

        [Test]
        public void Raft_SealedPontoonsAreWhatProvideBuoyancy()
        {
            // Steel alone is denser than water, so a steel frame cannot float. Lift
            // comes only from the sealed air inside a pontoon, which is what a real
            // salvaged barrel is: a shell full of nothing.
            var member = new Member { Material = MaterialKind.Steel, CrossSectionAreaM2 = 0.02f, LengthM = 2.1f };
            member.RecomputeDerived();
            member.SealedVolumeM3 = 2.4f;

            Assert.AreEqual(0f, member.BuoyantVolumeM3, 1e-6f,
                "solid steel displaces nothing");
            Assert.AreEqual(2.4f, member.TotalBuoyantVolumeM3, 1e-6f,
                "all of a pontoon's lift comes from sealed air");

            float netLiftKg = member.TotalBuoyantVolumeM3 * 1000f - member.MassKg;
            Assert.Greater(netLiftKg, 0f, "a sealed barrel must be net buoyant");
        }

        [Test]
        public void Raft_StrongerWindBreaksMoreThanCalm()
        {
            var calm = new RaftState();
            calm.WindLoadKnPerM = 0.5f;
            var stormy = new RaftState();
            stormy.WindLoadKnPerM = 40f;

            int calmFailures = calm.SolveWind().FailedMemberCount;
            int stormFailures = stormy.SolveWind().FailedMemberCount;

            Assert.GreaterOrEqual(stormFailures, calmFailures,
                "a storm must never be safer than calm water");
        }
    }
}
