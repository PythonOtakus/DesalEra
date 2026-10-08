using DesalEra.Game;
using DesalEra.Unity;
using NUnit.Framework;
using UnityEngine;

namespace DesalEra.Tests
{
    /// <summary>
    /// Jumping off and climbing back aboard: where the edge is, and whether the clip
    /// poses the transition is timed against are still where the baked clips put them.
    /// </summary>
    public sealed class DeckEdgeTests
    {
        private static float Half => RaftState.CellSize;

        [Test]
        public void NearestDeckEdge_FromTheWaterPointsBackOntoTheDeck()
        {
            var raft = new RaftState();
            Assert.IsTrue(raft.NearestDeckEdge(new Vector3(Half + 0.4f, 0f, 0.5f), out Vector3 edge, out Vector3 inward));

            Assert.AreEqual(Half, edge.x, 1e-3f, "closest point lies on the east side");
            Assert.AreEqual(0.5f, edge.z, 1e-3f);
            Assert.AreEqual(-1f, inward.x, 1e-3f, "inward from the east side is west");
            Assert.AreEqual(0f, inward.y, 1e-6f);
        }

        [Test]
        public void NearestDeckEdge_InwardPointsInsideOnEverySide()
        {
            var raft = new RaftState();
            var outside = new[]
            {
                new Vector3(Half + 0.3f, 0f, 0f), new Vector3(-Half - 0.3f, 0f, 0f),
                new Vector3(0f, 0f, Half + 0.3f), new Vector3(0f, 0f, -Half - 0.3f),
            };

            foreach (Vector3 p in outside)
            {
                Assert.IsTrue(raft.NearestDeckEdge(p, out Vector3 edge, out Vector3 inward));
                Assert.IsTrue(raft.IsOverDeck(edge + inward * 0.5f, 0f), $"0.5 m inward from {edge} is deck");
                Assert.IsFalse(raft.IsOverDeck(edge - inward * 0.5f, 0f), $"0.5 m outward from {edge} is water");
            }
        }

        [Test]
        public void FallSeconds_GrowsWithTheDrop()
        {
            float low = EdgeTransition.FallSeconds(1f, EdgeTransition.DropHopSpeed);
            float high = EdgeTransition.FallSeconds(2f, EdgeTransition.DropHopSpeed);
            Assert.Greater(high, low);
            Assert.AreEqual(2f * EdgeTransition.DropHopSpeed / EdgeTransition.Gravity,
                EdgeTransition.FallSeconds(0f, EdgeTransition.DropHopSpeed), 1e-4f, "a hop lands back at its own height");
        }

        [Test]
        public void JumpClip_FallsFromThePeakToTouchdown()
        {
            Sample("Jump", body =>
            {
                float peak = Hips(body, EdgeTransition.JumpFallStart);
                Assert.Greater(peak, Hips(body, 0f) + 0.25f, "the drop starts near the top of the jump");
                Assert.Greater(Feet(body, EdgeTransition.JumpFallStart), 0.5f, "feet tucked up at the start of the drop");
                Assert.Less(Feet(body, EdgeTransition.JumpTouchdown), 0.2f, "feet down at touchdown");
                Assert.AreEqual(EdgeTransition.TouchdownHipsAboveFeet,
                    Hips(body, EdgeTransition.JumpTouchdown) - Feet(body, EdgeTransition.JumpTouchdown), 0.08f);
            });
        }

        [Test]
        public void LadderLoop_StillRisesAtTheHandover()
        {
            Sample("LadderClimbLoop", body =>
            {
                Assert.Greater(Hips(body, EdgeTransition.ClimbLoopHandover), Hips(body, 0f) + 0.15f,
                    "the finish must take over while the cycle is still lifting the body, before it wraps");
            });
        }

        [Test]
        public void LadderFinish_EndsStandingOnTheEdgeItGrips()
        {
            float standingHips = 0f;
            Sample("Idle", body => standingHips = Hips(body, 0f));

            Sample("LadderClimbFinish", body =>
            {
                // Sampling at exactly the length wraps to the first frame.
                float end = body.Clip.length - 0.02f;
                Assert.AreEqual(EdgeTransition.ClimbFinishGripHips, Hips(body, 0.3f), 0.06f, "hips as the finish fades in");
                Assert.AreEqual(EdgeTransition.ClimbFinishRise, Hips(body, end) - standingHips, 0.05f, "rise");
                Assert.AreEqual(EdgeTransition.ClimbFinishReach, Forward(body, end), 0.06f, "forward reach");
                Assert.Greater(EdgeTransition.ClimbFinishReach, EdgeTransition.ClimbEdgeOffset,
                    "the climb must end on the deck side of the edge");
            });
        }

        [Test]
        public void HandsStayOnTheDeckThroughTheHandover()
        {
            Assert.AreEqual(1f, EdgeTransition.HandsOnDeck(EdgeTransition.ClimbLoopHandover, false), 1e-4f);
            Assert.AreEqual(1f, EdgeTransition.HandsOnDeck(0f, true), 1e-4f);
            Assert.AreEqual(0f, EdgeTransition.HandsOnDeck(EdgeTransition.HandsReleaseEnd, true), 1e-4f);
        }

        [Test]
        public void LadderFinish_HandsLetGoBeforeTheFootComesOver()
        {
            float standingFeet = 0f;
            Sample("Idle", body => standingFeet = Feet(body, 0f));

            Sample("LadderClimbFinish", body =>
            {
                // Releasing lifts the body back onto the clip; a foot already over the deck
                // would be pushed up through the planks.
                float deck = EdgeTransition.ClimbFinishRise + standingFeet;
                float footOver = -1f;
                for (float t = 0f; t < body.Clip.length && footOver < 0f; t += 0.02f)
                {
                    body.Clip.SampleAnimation(body.Root, t);
                    float highest = Mathf.Max(body.LeftFoot.position.y, body.RightFoot.position.y)
                                  - body.Root.transform.position.y;
                    if (highest > deck) footOver = t;
                }

                Assert.Greater(footOver, 0f, "a foot comes up onto the deck");
                Assert.Less(EdgeTransition.HandsOnDeck(footOver, true), 0.25f,
                    $"hands still {EdgeTransition.HandsOnDeck(footOver, true):F2} planted when a foot comes over at {footOver:F2} s");
            });
        }

        private sealed class Body
        {
            public GameObject Root;
            public AnimationClip Clip;
            public Transform Hips;
            public Transform LeftFoot;
            public Transform RightFoot;
        }

        private static void Sample(string state, System.Action<Body> check)
        {
            var clip = Resources.Load<AnimationClip>("Survivor_" + state);
            var model = Resources.Load<GameObject>("Survivor");
            Assert.IsNotNull(clip, state + " clip missing");
            Assert.IsNotNull(model, "survivor model missing");

            var body = new Body { Root = Object.Instantiate(model), Clip = clip };
            try
            {
                foreach (var t in body.Root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Hips") body.Hips = t;
                    if (t.name == "LeftFoot") body.LeftFoot = t;
                    if (t.name == "RightFoot") body.RightFoot = t;
                }
                check(body);
            }
            finally
            {
                Object.DestroyImmediate(body.Root);
            }
        }

        private static float Hips(Body body, float time)
        {
            body.Clip.SampleAnimation(body.Root, time);
            return body.Hips.position.y - body.Root.transform.position.y;
        }

        private static float Feet(Body body, float time)
        {
            body.Clip.SampleAnimation(body.Root, time);
            return Mathf.Min(body.LeftFoot.position.y, body.RightFoot.position.y) - body.Root.transform.position.y;
        }

        private static float Forward(Body body, float time)
        {
            body.Clip.SampleAnimation(body.Root, time);
            return Vector3.Dot(body.Hips.position - body.Root.transform.position, body.Root.transform.forward);
        }
    }
}
