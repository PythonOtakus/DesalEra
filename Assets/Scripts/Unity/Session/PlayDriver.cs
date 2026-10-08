using System;
using System.Text;
using DesalEra.Game;
using DesalEra.Unity.Ui;
using UnityEngine;

namespace DesalEra.Unity.Session
{
    /// <summary>
    /// Single entry point for player operations. Input, session replay, and unity-cli
    /// all go through here so every action is recordable and scriptable the same way.
    /// </summary>
    public static class PlayDriver
    {
        public static event Action<PlayActionRecord> ActionPerformed;

        public static PlayerController Player =>
            UnityEngine.Object.FindObjectOfType<PlayerController>();

        public static GameBootstrap World =>
            UnityEngine.Object.FindObjectOfType<GameBootstrap>();

        public static ThirdPersonCamera CameraOrbit =>
            UnityEngine.Object.FindObjectOfType<ThirdPersonCamera>();

        public static HudPresenter Hud =>
            UnityEngine.Object.FindObjectOfType<HudPresenter>();

        public static string Execute(PlayActionRecord action)
        {
            if (action == null) return "null action";
            if (!Application.isPlaying) return "not in play mode";

            string result;
            switch (action.kind)
            {
                case PlayActionKind.Move:
                    result = ApplyMove(action.h, action.v, action.sprint);
                    break;
                case PlayActionKind.Look:
                    result = ApplyLook(action.yaw, action.pitch, action.distance);
                    break;
                case PlayActionKind.Select:
                    result = ApplySelect(action.index);
                    break;
                case PlayActionKind.Build:
                    result = ApplyBuild(action.cellX, action.cellY, action.index, action.level, action.facing);
                    break;
                case PlayActionKind.Dismantle:
                    result = ApplyDismantle(action.cellX, action.cellY, action.level);
                    break;
                case PlayActionKind.Rotate:
                    result = Player == null ? "no player" : Player.SetBuildFacing(action.facing);
                    break;
                case PlayActionKind.Level:
                    result = Player == null ? "no player" : Player.SetBuildLevel(action.level);
                    break;
                case PlayActionKind.Eat:
                    result = ApplyEat();
                    break;
                case PlayActionKind.Teleport:
                    result = ApplyTeleport(new Vector3(action.x, action.y, action.z));
                    break;
                case PlayActionKind.Ui:
                    result = ApplyUi(action.panel);
                    break;
                case PlayActionKind.Wait:
                    result = "wait " + action.seconds.ToString("F2") + "s";
                    break;
                case PlayActionKind.Note:
                    result = action.text ?? "";
                    break;
                default:
                    result = "unknown kind: " + action.kind;
                    break;
            }

            action.result = result;
            Notify(action);
            return result;
        }

        private static string ApplyMove(float h, float v, bool sprint)
        {
            var player = Player;
            if (player == null) return "no player";
            player.SetMoveIntent(h, v, sprint);
            return $"move h={h:F2} v={v:F2} sprint={sprint}";
        }

        private static string ApplyLook(float yaw, float pitch, float distance)
        {
            var cam = CameraOrbit;
            if (cam == null) return "no camera";
            cam.SetOrbit(yaw, pitch, distance);
            return $"look yaw={yaw:F1} pitch={pitch:F1} dist={(distance < 0f ? cam.Distance : distance):F1}";
        }

        private static string ApplySelect(int index)
        {
            var player = Player;
            return player == null ? "no player" : player.SelectPieceAt(index);
        }

        private static string ApplyBuild(int cellX, int cellY, int pieceIndex, int level, int facing)
        {
            var player = Player;
            if (player == null) return "no player";
            if (pieceIndex >= 0) player.SelectPieceAt(pieceIndex);
            return player.BuildAt(new Vector2Int(cellX, cellY), level, facing);
        }

        private static string ApplyDismantle(int cellX, int cellY, int level)
        {
            var player = Player;
            return player == null ? "no player" : player.DismantleAt(new Vector2Int(cellX, cellY), level);
        }

        private static string ApplyEat()
        {
            var player = Player;
            return player == null ? "no player" : player.EatRationNow();
        }

        private static string ApplyTeleport(Vector3 world)
        {
            var player = Player;
            return player == null ? "no player" : player.TeleportTo(world);
        }

        private static string ApplyUi(string panel)
        {
            var hud = Hud;
            return hud == null ? "no hud" : hud.ShowPanel(panel);
        }

        /// <summary>
        /// Records an action that was already applied (e.g. from keyboard input) without
        /// running it again.
        /// </summary>
        public static void Notify(PlayActionRecord action)
        {
            if (action == null) return;
            ActionPerformed?.Invoke(action);
        }

        public static string Move(float h, float v, bool sprint)
        {
            var action = new PlayActionRecord
            {
                kind = PlayActionKind.Move,
                h = h,
                v = v,
                sprint = sprint
            };
            return Execute(action);
        }

        public static string Look(float yaw, float pitch, float distance = -1f)
        {
            return Execute(new PlayActionRecord
            {
                kind = PlayActionKind.Look,
                yaw = yaw,
                pitch = pitch,
                distance = distance
            });
        }

        public static string Select(int index)
        {
            return Execute(new PlayActionRecord { kind = PlayActionKind.Select, index = index });
        }

        /// <summary>
        /// Places a piece. Negative level / facing mean "whatever the player has set",
        /// resolved here so the recording always stores the concrete values.
        /// </summary>
        public static string Build(int cellX, int cellY, int pieceIndex = -1, int level = -1, int facing = -1)
        {
            var player = Player;
            return Execute(new PlayActionRecord
            {
                kind = PlayActionKind.Build,
                cellX = cellX,
                cellY = cellY,
                index = pieceIndex,
                level = level >= 0 ? level : player != null ? player.BuildLevel : 0,
                facing = facing >= 0 ? facing : player != null ? player.BuildFacing : 0
            });
        }

        public static string Dismantle(int cellX, int cellY, int level = -1)
        {
            var player = Player;
            return Execute(new PlayActionRecord
            {
                kind = PlayActionKind.Dismantle,
                cellX = cellX,
                cellY = cellY,
                level = level >= 0 ? level : player != null ? player.BuildLevel : 0
            });
        }

        public static string Rotate(int facing)
        {
            return Execute(new PlayActionRecord { kind = PlayActionKind.Rotate, facing = RaftState.NormaliseFacing(facing) });
        }

        public static string Level(int level)
        {
            return Execute(new PlayActionRecord { kind = PlayActionKind.Level, level = level });
        }

        public static string Eat()
        {
            return Execute(new PlayActionRecord { kind = PlayActionKind.Eat });
        }

        public static string Teleport(Vector3 world)
        {
            return Execute(new PlayActionRecord
            {
                kind = PlayActionKind.Teleport,
                x = world.x,
                y = world.y,
                z = world.z
            });
        }

        public static string Ui(string panel)
        {
            return Execute(new PlayActionRecord { kind = PlayActionKind.Ui, panel = panel });
        }

        public static PlaySnapshot Capture(float t = -1f)
        {
            var player = Player;
            var world = World;
            var cam = CameraOrbit;
            var snap = new PlaySnapshot { t = t >= 0f ? t : Time.time };

            if (player != null)
            {
                Vector3 p = player.transform.position;
                snap.x = p.x;
                snap.y = p.y;
                snap.z = p.z;
                snap.mode = player.Mode.ToString();
                snap.anim = player.Animator != null ? player.Animator.CurrentState : "";
                snap.selected = player.SelectedIndex;
                snap.status = player.StatusLine ?? "";
                if (player.Avatar != null)
                {
                    Vector3 f = player.Avatar.FacingDirection;
                    snap.facingX = f.x;
                    snap.facingZ = f.z;
                    var renderer = player.Avatar.ModelRoot != null
                        ? player.Avatar.ModelRoot.GetComponentInChildren<SkinnedMeshRenderer>()
                        : null;
                    if (renderer != null)
                    {
                        if (player.IsInWater)
                        {
                            // Gap of mesh bottom vs local swell (negative = submerged).
                            snap.soleGap = renderer.bounds.min.y - SeaWave.HeightAt(p);
                        }
                        else if (world != null && world.RaftMotion != null)
                        {
                            float surfaceY = world.RaftMotion.SurfacePoint(p, player.SurfaceLocalY).y;
                            snap.soleGap = renderer.bounds.min.y - surfaceY;
                        }
                    }
                }
            }

            if (cam != null)
            {
                snap.yaw = cam.Yaw;
                snap.pitch = cam.Pitch;
                snap.distance = cam.Distance;
            }

            if (world != null && world.Raft != null)
            {
                var s = world.Raft.Survival;
                snap.food = s.Food;
                snap.water = s.Water;
                snap.health = s.Health;
                snap.stamina = s.Stamina;
                var inv = world.Raft.Inventory;
                snap.plank = inv.Get(ResourceKind.Plank);
                snap.scrap = inv.Get(ResourceKind.Scrap);
                snap.metal = inv.Get(ResourceKind.Metal);
                snap.foodQty = inv.Get(ResourceKind.Food);
                snap.waterQty = inv.Get(ResourceKind.Water);
                snap.members = world.Raft.MemberCount;
            }

            if (world != null) snap.storm = world.IsStormActive;
            if (player != null)
            {
                snap.sheltered = player.IsSheltered;
                snap.surfaceY = player.SurfaceLocalY;
                snap.buildLevel = player.BuildLevel;
                snap.buildFacing = player.BuildFacing;
            }

            return snap;
        }

        public static string Analyse(PlaySession session)
        {
            if (session == null) return "no session";
            var sb = new StringBuilder();
            sb.AppendLine("id=" + session.id);
            sb.AppendLine("duration=" + session.duration.ToString("F1") + "s");
            sb.AppendLine("actions=" + (session.actions?.Length ?? 0));
            sb.AppendLine("snapshots=" + (session.snapshots?.Length ?? 0));

            int builds = 0, dismantles = 0, eats = 0, moves = 0, looks = 0;
            if (session.actions != null)
            {
                foreach (var a in session.actions)
                {
                    switch (a.kind)
                    {
                        case PlayActionKind.Build: builds++; break;
                        case PlayActionKind.Dismantle: dismantles++; break;
                        case PlayActionKind.Eat: eats++; break;
                        case PlayActionKind.Move: moves++; break;
                        case PlayActionKind.Look: looks++; break;
                    }
                }
            }

            sb.AppendLine($"counts move={moves} look={looks} build={builds} dismantle={dismantles} eat={eats}");

            if (session.snapshots != null && session.snapshots.Length > 0)
            {
                var first = session.snapshots[0];
                var last = session.snapshots[session.snapshots.Length - 1];
                sb.AppendLine($"path start=({first.x:F1},{first.z:F1}) end=({last.x:F1},{last.z:F1})");
                sb.AppendLine($"vitals start H/F/W/S={first.health:F0}/{first.food:F0}/{first.water:F0}/{first.stamina:F0}");
                sb.AppendLine($"vitals end   H/F/W/S={last.health:F0}/{last.food:F0}/{last.water:F0}/{last.stamina:F0}");
                sb.AppendLine($"soleGap end={last.soleGap:F3} mode={last.mode} anim={last.anim}");

                float maxSole = float.MinValue;
                foreach (var s in session.snapshots)
                    if (s.soleGap > maxSole) maxSole = s.soleGap;
                sb.AppendLine($"soleGap max={maxSole:F3}");

                int stormExposed = 0, stormSheltered = 0;
                foreach (var s in session.snapshots)
                {
                    if (!s.storm) continue;
                    if (s.sheltered) stormSheltered++; else stormExposed++;
                }
                if (stormExposed + stormSheltered > 0)
                    sb.AppendLine($"storm snapshots exposed={stormExposed} sheltered={stormSheltered}");
            }

            if (session.actions != null)
            {
                sb.AppendLine("--- actions ---");
                int n = Mathf.Min(session.actions.Length, 40);
                for (int i = 0; i < n; i++)
                {
                    var a = session.actions[i];
                    sb.Append(a.t.ToString("F2")).Append(' ').Append(a.kind);
                    if (a.kind == PlayActionKind.Build || a.kind == PlayActionKind.Dismantle)
                        sb.Append(" cell=").Append(a.cellX).Append(',').Append(a.cellY)
                          .Append(" L").Append(a.level).Append(" f").Append(a.facing);
                    if (a.kind == PlayActionKind.Move)
                        sb.Append(" h=").Append(a.h.ToString("F1")).Append(" v=").Append(a.v.ToString("F1"));
                    if (!string.IsNullOrEmpty(a.result)) sb.Append(" => ").Append(a.result);
                    sb.AppendLine();
                }
                if (session.actions.Length > n)
                    sb.AppendLine("... +" + (session.actions.Length - n) + " more");
            }

            return sb.ToString();
        }
    }
}
