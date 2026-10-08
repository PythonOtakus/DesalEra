using UnityEngine;

namespace DesalEra.Unity
{
    /// <summary>
    /// Timings and offsets for leaving the deck and climbing back aboard, read off the
    /// baked clips. DeckEdgeTests samples the clips against these, so a re-bake that
    /// moves a key pose fails a test instead of silently putting the survivor's hands in
    /// the air above the deck.
    /// </summary>
    public static class EdgeTransition
    {
        // --- Off the edge: the second half of the standing jump ---

        /// <summary>Jump clip time the drop starts from: just before the hips peak at 0.78 s.</summary>
        public const float JumpFallStart = 0.70f;

        /// <summary>Jump clip time the feet come down; timed to meet the water.</summary>
        public const float JumpTouchdown = 1.09f;

        /// <summary>Hips above the feet at touchdown, so the fall ends with the feet at the waterline.</summary>
        public const float TouchdownHipsAboveFeet = 0.80f;

        /// <summary>Upward speed when stepping off, m/s. A small hop clears the deck lip.</summary>
        public const float DropHopSpeed = 1.2f;

        /// <summary>Least speed away from the deck, m/s, so a slow walk off the edge still clears it.</summary>
        public const float DropMinOutwardSpeed = 1.6f;

        public const float Gravity = 9.81f;

        /// <summary>Roughly how long the splash takes to come to rest at swimming depth.</summary>
        public const float SplashSettleSeconds = 0.6f;

        // --- Back aboard: one ladder cycle out of the water, then the finish over the edge ---

        /// <summary>
        /// LadderClimbLoop time at which the finish takes over. The cycle raises the hips
        /// and the loop wraps back down at 1.63 s; handing over before that, with room for
        /// the cross-fade, keeps the body rising throughout.
        /// </summary>
        public const float ClimbLoopHandover = 1.20f;

        /// <summary>Hips above the clip origin as LadderClimbFinish begins, hands on the edge.</summary>
        public const float ClimbFinishGripHips = 0.80f;

        /// <summary>How much higher the clip origin is when LadderClimbFinish ends standing up.</summary>
        public const float ClimbFinishRise = 0.97f;

        /// <summary>How far forward the body ends LadderClimbFinish.</summary>
        public const float ClimbFinishReach = 0.90f;

        /// <summary>
        /// How far out from the edge the clip origin sits while climbing over it. The clip's
        /// toes rest on the ladder about 0.4 m in front of the origin, so this puts them
        /// against the side of the raft.
        /// </summary>
        public const float ClimbEdgeOffset = 0.40f;

        // --- Hands on the deck. The clip grips ladder rails that stand 0.2-0.7 m above the
        // platform; the raft has none, so the hands are pulled down onto the planks. ---

        /// <summary>How far onto the deck the wrists are placed.</summary>
        public const float HandInset = 0.10f;

        /// <summary>Wrist height above the planks with the palm flat.</summary>
        public const float HandAboveDeck = 0.05f;

        /// <summary>Sideways distance of each hand from the body's centre line.</summary>
        public const float HandSpread = 0.20f;

        /// <summary>
        /// The survivor's arms reach about 0.4 m, while the clip hangs the shoulders 0.5 m
        /// back from the edge and up to 0.7 m above the rail they grip. With the hands
        /// planted the body is pulled in toward the side of the raft until the shoulders
        /// are this share of an arm's length behind the hands, then lowered until the hands
        /// reach.
        /// </summary>
        public const float ReachForwardShare = 0.6f;

        /// <summary>LadderClimbLoop time the hands start reaching for the deck.</summary>
        public const float HandsReachStart = 0.40f;

        /// <summary>
        /// LadderClimbFinish time the hands start pushing off. The body rises back onto the
        /// clip as they let go, so they must be released before the foot comes over the
        /// edge at 1.8 s or it would step through the deck.
        /// </summary>
        public const float HandsReleaseStart = 1.3f;

        /// <summary>LadderClimbFinish time the hands are back on the clip and the body is on its own path.</summary>
        public const float HandsReleaseEnd = 1.8f;

        /// <summary>Playback speed of both ladder clips; at 1 the four-second finish drags.</summary>
        public const float ClimbPlayback = 1.3f;

        /// <summary>
        /// Seconds to fall <paramref name="drop"/> metres after leaving with upward speed
        /// <paramref name="hop"/>.
        /// </summary>
        public static float FallSeconds(float drop, float hop)
        {
            return (hop + Mathf.Sqrt(hop * hop + 2f * Gravity * Mathf.Max(0f, drop))) / Gravity;
        }

        /// <summary>Jump playback speed that brings the feet down after <paramref name="fallSeconds"/>.</summary>
        public static float DropPlayback(float fallSeconds)
        {
            return Mathf.Clamp((JumpTouchdown - JumpFallStart) / Mathf.Max(fallSeconds, 1e-3f), 0.3f, 1.5f);
        }

        /// <summary>
        /// How firmly the hands are held on the deck: rising through the ladder cycle,
        /// full through the haul over the edge, released as the survivor stands.
        /// </summary>
        public static float HandsOnDeck(float clipTime, bool finishing)
        {
            if (!finishing)
                return Smooth((clipTime - HandsReachStart) / (ClimbLoopHandover - HandsReachStart));
            return 1f - Smooth((clipTime - HandsReleaseStart) / (HandsReleaseEnd - HandsReleaseStart));
        }

        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
