using DesalEra.Unity;
using NUnit.Framework;
using UnityEngine;

namespace DesalEra.Tests
{
    /// <summary>
    /// Pure band-selection tests for locomotion hysteresis. These do not need a
    /// PlayableGraph — only the threshold math that used to thrash Idle/Walk/Run.
    /// </summary>
    public sealed class AnimationBandTests
    {
        private GameObject _host;
        private PlayerAnimator _anim;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("AnimBandHost");
            _host.AddComponent<PlayerAvatar>();
            _anim = _host.AddComponent<PlayerAnimator>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [Test]
        public void BandForSpeed_EntersWalkAtThresholdFromIdle()
        {
            Assert.AreEqual("Walk", _anim.BandForSpeed(0.4f, "Idle"));
        }

        [Test]
        public void BandForSpeed_HoldsWalkBelowRawThreshold()
        {
            Assert.AreEqual("Walk", _anim.BandForSpeed(0.3f, "Walk"),
                "exit margin must keep Walk until speed falls further");
        }

        [Test]
        public void BandForSpeed_LeavesWalkWhenClearlyIdle()
        {
            Assert.AreEqual("Idle", _anim.BandForSpeed(0.1f, "Walk"));
        }

        [Test]
        public void BandForSpeed_RunHysteresis()
        {
            Assert.AreEqual("Run", _anim.BandForSpeed(3.8f, "Walk"));
            Assert.AreEqual("Run", _anim.BandForSpeed(3.5f, "Run"),
                "run exit margin must hold Run just below the enter threshold");
            Assert.AreEqual("Walk", _anim.BandForSpeed(3.0f, "Run"));
        }

        [TestCase("Idle")]
        [TestCase("Walk")]
        [TestCase("Run")]
        [TestCase("SwimIdle")]
        [TestCase("SwimForward")]
        public void CyclicClips_AreLooping(string state)
        {
            var clip = Resources.Load<AnimationClip>("Survivor_" + state);
            Assert.IsNotNull(clip, state + " clip missing");
            Assert.IsTrue(clip.isLooping,
                state + " must loop; a Playables clip with loopTime off freezes on its last frame");
        }

        [TestCase("SwimForward", false)]
        [TestCase("Idle", true)]
        [TestCase("Walk", true)]
        public void BakedClip_KeepsTheSourceBodyOrientation(string state, bool upright)
        {
            var clip = Resources.Load<AnimationClip>("Survivor_" + state);
            var model = Resources.Load<GameObject>("Survivor");
            Assert.IsNotNull(clip, state + " clip missing");
            Assert.IsNotNull(model, "survivor model missing");

            var body = Object.Instantiate(model);
            try
            {
                Transform hips = null, head = null;
                foreach (var t in body.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Hips") hips = t;
                    if (t.name == "Head") head = t;
                }

                clip.SampleAnimation(body, clip.length * 0.37f);
                float rise = (head.position - hips.position).normalized.y;

                if (upright)
                    Assert.Greater(rise, 0.9f, state + " should stand upright");
                else
                    Assert.Less(rise, 0.6f, state + " should lie face down, as authored");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }
    }
}
