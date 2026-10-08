using System.Text;
using DesalEra.Unity;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace DesalEra.EditorTools
{
    /// <summary>
    /// Play-mode animation diagnostics exposed to unity-cli as <c>anim_diag</c>.
    ///
    /// Usage:
    ///   unity-cli anim_diag --params "{}"
    ///   unity-cli anim_diag --params "{\"check\":\"feet\"}"
    /// </summary>
    [UnityCliTool(Name = "anim_diag", Description = "Diagnose survivor feet height, facing vs travel, and locomotion clip state (play mode).")]
    public static class AnimDiagTool
    {
        public class Parameters
        {
            [ToolParameter("Check to run: all (default), feet, facing, state")]
            public string Check { get; set; } = "all";
        }

        public static object HandleCommand(JObject @params)
        {
            if (!Application.isPlaying)
                return new ErrorResponse("anim_diag requires play mode.");

            // CLI may omit --params entirely; ToolParams rejects null.
            var p = new ToolParams(@params ?? new JObject());
            string check = (p.Get("check") ?? "all").Trim().ToLowerInvariant();

            var player = Object.FindObjectOfType<PlayerController>();
            if (player == null)
                return new ErrorResponse("No PlayerController in the scene.");

            var avatar = player.Avatar;
            if (avatar == null || !avatar.IsLoaded)
                return new ErrorResponse("Survivor avatar is not loaded.");

            var world = Object.FindObjectOfType<GameBootstrap>();
            var report = new StringBuilder();
            bool ok = true;

            if (check == "all" || check == "feet")
                ok &= AppendFeet(report, player, avatar, world);

            if (check == "all" || check == "facing")
                ok &= AppendFacing(report, player, avatar);

            if (check == "all" || check == "state")
                ok &= AppendState(report, player);

            var data = new
            {
                ok,
                mode = player.Mode.ToString(),
                anim = player.Animator != null ? player.Animator.CurrentState : null,
                rootAboveSole = avatar.RootAboveSoleM,
                facing = avatar.FacingDirection.ToString("F2"),
                position = player.transform.position.ToString("F3"),
                detail = report.ToString()
            };

            return ok
                ? new SuccessResponse("anim_diag ok", data)
                : new ErrorResponse("anim_diag failed", data);
        }

        private static bool AppendFeet(StringBuilder report, PlayerController player, PlayerAvatar avatar, GameBootstrap world)
        {
            float deckY = RaftDeckY(world, player.transform.position);
            var renderer = avatar.ModelRoot.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer == null)
            {
                report.AppendLine("feet: FAIL no SkinnedMeshRenderer");
                return false;
            }

            // Compare soles to the visual plank top (centreline + half plank depth).
            float plankHalf = Mathf.Clamp(Mathf.Sqrt(0.09f) * 1.15f, 0.14f, 0.7f) * 0.38f * 0.5f;
            float soleGap = renderer.bounds.min.y - (deckY + plankHalf);
            // Allow a little sink into the deck plank and a little clearance for footwear.
            bool ok = soleGap > -0.08f && soleGap < 0.12f;
            report.Append("feet: ").Append(ok ? "OK" : "FAIL")
                .Append(" soleGap=").Append(soleGap.ToString("F3"))
                .Append(" deckY=").Append(deckY.ToString("F3"))
                .Append(" rootAboveSole=").Append(avatar.RootAboveSoleM.ToString("F3"))
                .AppendLine();
            return ok;
        }

        private static bool AppendFacing(StringBuilder report, PlayerController player, PlayerAvatar avatar)
        {
            // Settle facing toward +X the same way gameplay does.
            for (int i = 0; i < 40; i++) avatar.FaceTowards(Vector3.right);

            float align = Vector3.Dot(avatar.FacingDirection, Vector3.right);
            bool ok = align > 0.85f;
            report.Append("facing: ").Append(ok ? "OK" : "FAIL")
                .Append(" alignRight=").Append(align.ToString("F2"))
                .Append(" facing=").Append(avatar.FacingDirection.ToString("F2"))
                .AppendLine();
            return ok;
        }

        private static bool AppendState(StringBuilder report, PlayerController player)
        {
            var animator = player.Animator;
            if (animator == null || !animator.HasClips)
            {
                report.AppendLine("state: FAIL no clips");
                return false;
            }

            // Band hysteresis: at the walk threshold from Idle must enter Walk;
            // from Walk, a hair below the raw threshold must stay Walk.
            string fromIdle = animator.BandForSpeed(0.4f, "Idle");
            string stayWalk = animator.BandForSpeed(0.3f, "Walk");
            string leaveWalk = animator.BandForSpeed(0.1f, "Walk");
            string enterRun = animator.BandForSpeed(3.8f, "Walk");
            string stayRun = animator.BandForSpeed(3.5f, "Run");

            bool ok = fromIdle == "Walk"
                   && stayWalk == "Walk"
                   && leaveWalk == "Idle"
                   && enterRun == "Run"
                   && stayRun == "Run";

            report.Append("state: ").Append(ok ? "OK" : "FAIL")
                .Append(" current=").Append(animator.CurrentState)
                .Append(" fromIdle@0.4=").Append(fromIdle)
                .Append(" stayWalk@0.3=").Append(stayWalk)
                .Append(" leaveWalk@0.1=").Append(leaveWalk)
                .Append(" enterRun@3.8=").Append(enterRun)
                .Append(" stayRun@3.5=").Append(stayRun)
                .AppendLine();
            return ok;
        }

        private static float RaftDeckY(GameBootstrap world, Vector3 position)
        {
            if (world != null && world.RaftMotion != null)
                return world.RaftMotion.DeckPoint(position).y;
            return DesalEra.Game.RaftState.BaseDeckY;
        }
    }
}
