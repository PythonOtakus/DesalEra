using System.IO;
using System.Linq;
using DesalEra.Game;
using DesalEra.Unity;
using DesalEra.Unity.Session;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace DesalEra.EditorTools.Cli
{
    [UnityCliTool(Name = "session_start", Group = "play", Description = "Start recording a play session (no JSON params needed).")]
    public static class SessionStartTool
    {
        public static object HandleCommand(JObject @params)
        {
            try { return new SuccessResponse(SessionTool.RequireRecorderPublic().StartRecording()); }
            catch (System.Exception ex) { return new ErrorResponse(ex.Message); }
        }
    }

    [UnityCliTool(Name = "session_stop", Group = "play", Description = "Stop recording and save SessionRecordings/*.json.")]
    public static class SessionStopTool
    {
        public static object HandleCommand(JObject @params)
        {
            try { return new SuccessResponse(SessionTool.RequireRecorderPublic().StopRecording()); }
            catch (System.Exception ex) { return new ErrorResponse(ex.Message); }
        }
    }

    [UnityCliTool(Name = "session_status", Group = "play", Description = "Recording / replay status.")]
    public static class SessionStatusTool
    {
        public static object HandleCommand(JObject @params)
        {
            var rec = Object.FindObjectOfType<PlaySessionRecorder>();
            return new SuccessResponse(rec != null ? rec.Status() : "no recorder");
        }
    }

    [UnityCliTool(Name = "session_list", Group = "play", Description = "List saved session JSON files.")]
    public static class SessionListTool
    {
        public static object HandleCommand(JObject @params)
        {
            Directory.CreateDirectory(PlaySession.DirectoryPath);
            return new SuccessResponse("list", Directory.GetFiles(PlaySession.DirectoryPath, "*.json"));
        }
    }

    [UnityCliTool(Name = "session", Group = "play", Description = "Record / replay / analyse play sessions. Actions: start, stop, status, replay, analyse, list, snapshot.")]
    public static class SessionTool
    {
        internal static PlaySessionRecorder RequireRecorderPublic() => RequireRecorder();

        public class Parameters
        {
            [ToolParameter("Action: start|stop|status|replay|analyse|list|snapshot", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Session id or .json path (replay/analyse)")]
            public string Id { get; set; }

            [ToolParameter("Optional note when starting a recording")]
            public string Notes { get; set; }

            [ToolParameter("Replay time scale (default 1)")]
            public float TimeScale { get; set; } = 1f;
        }

        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params ?? new JObject());
                string action = (p.Get("action") ?? "").Trim().ToLowerInvariant();

                switch (action)
                {
                    case "start":
                        return Ok(RequireRecorder().StartRecording(p.Get("notes")));
                    case "stop":
                        return Ok(RequireRecorder().StopRecording());
                    case "status":
                    {
                        var rec = Object.FindObjectOfType<PlaySessionRecorder>();
                        return Ok(rec != null ? rec.Status() : "no recorder");
                    }
                    case "replay":
                    {
                        string id = p.Get("id");
                        if (string.IsNullOrEmpty(id)) return new ErrorResponse("id required");
                        float scale = p.GetFloat("time_scale", 1f) ?? 1f;
                        return Ok(RequireRecorder().Replay(id, scale));
                    }
                    case "analyse":
                    {
                        string id = p.Get("id");
                        if (string.IsNullOrEmpty(id)) return new ErrorResponse("id required");
                        var session = PlaySession.Load(id);
                        return new SuccessResponse("analyse", new { report = PlayDriver.Analyse(session) });
                    }
                    case "list":
                    {
                        Directory.CreateDirectory(PlaySession.DirectoryPath);
                        return new SuccessResponse("list", Directory.GetFiles(PlaySession.DirectoryPath, "*.json"));
                    }
                    case "snapshot":
                    {
                        if (!Application.isPlaying) return new ErrorResponse("not in play mode");
                        return new SuccessResponse("snapshot", PlayDriver.Capture());
                    }
                    default:
                        return new ErrorResponse("unknown action: " + action);
                }
            }
            catch (System.Exception ex)
            {
                return new ErrorResponse(ex.Message);
            }
        }

        private static PlaySessionRecorder RequireRecorder()
        {
            if (!Application.isPlaying)
                throw new System.InvalidOperationException("Play mode required.");
            var rec = Object.FindObjectOfType<PlaySessionRecorder>();
            if (rec != null) return rec;
            var host = Object.FindObjectOfType<GameBootstrap>();
            if (host == null) throw new System.InvalidOperationException("No GameBootstrap.");
            return host.gameObject.AddComponent<PlaySessionRecorder>();
        }

        private static object Ok(string message) => new SuccessResponse(message);
    }

    [UnityCliTool(Name = "session_analyse", Group = "play", Description = "Analyse a session. Optional id; defaults to the newest SessionRecordings/*.json.")]
    public static class SessionAnalyseTool
    {
        public class Parameters
        {
            [ToolParameter("Session id (optional — newest if omitted)")]
            public string Id { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params ?? new JObject());
                string id = p.Get("id");
                if (string.IsNullOrEmpty(id))
                {
                    Directory.CreateDirectory(PlaySession.DirectoryPath);
                    var latest = new DirectoryInfo(PlaySession.DirectoryPath)
                        .GetFiles("*.json")
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .FirstOrDefault();
                    if (latest == null) return new ErrorResponse("no sessions saved");
                    id = Path.GetFileNameWithoutExtension(latest.Name);
                }

                var session = PlaySession.Load(id);
                return new SuccessResponse("analyse " + id, new { report = PlayDriver.Analyse(session) });
            }
            catch (System.Exception ex)
            {
                return new ErrorResponse(ex.Message);
            }
        }
    }

    [UnityCliTool(Name = "player_forward", Group = "play", Description = "Walk forward (no JSON).")]
    public static class PlayerForwardTool
    {
        public static object HandleCommand(JObject @params) =>
            new SuccessResponse(PlayDriver.Move(0f, 1f, false), PlayDriver.Capture());
    }

    [UnityCliTool(Name = "player_back", Group = "play", Description = "Walk backward (no JSON).")]
    public static class PlayerBackTool
    {
        public static object HandleCommand(JObject @params) =>
            new SuccessResponse(PlayDriver.Move(0f, -1f, false), PlayDriver.Capture());
    }

    [UnityCliTool(Name = "player_left", Group = "play", Description = "Strafe left (no JSON).")]
    public static class PlayerLeftTool
    {
        public static object HandleCommand(JObject @params) =>
            new SuccessResponse(PlayDriver.Move(-1f, 0f, false), PlayDriver.Capture());
    }

    [UnityCliTool(Name = "player_right", Group = "play", Description = "Strafe right (no JSON).")]
    public static class PlayerRightTool
    {
        public static object HandleCommand(JObject @params) =>
            new SuccessResponse(PlayDriver.Move(1f, 0f, false), PlayDriver.Capture());
    }

    [UnityCliTool(Name = "player_stop", Group = "play", Description = "Stop move intent (no JSON).")]
    public static class PlayerStopTool
    {
        public static object HandleCommand(JObject @params) =>
            new SuccessResponse(PlayDriver.Move(0f, 0f, false), PlayDriver.Capture());
    }

    [UnityCliTool(Name = "player_sprint", Group = "play", Description = "Sprint forward (no JSON).")]
    public static class PlayerSprintTool
    {
        public static object HandleCommand(JObject @params) =>
            new SuccessResponse(PlayDriver.Move(0f, 1f, true), PlayDriver.Capture());
    }

    [UnityCliTool(Name = "player_move", Group = "play", Description = "Set survivor move intent (camera-relative). h/v in [-1,1], sprint bool. Prefer player_forward/stop if PowerShell strips JSON.")]
    public static class PlayerMoveTool
    {
        public class Parameters
        {
            [ToolParameter("Strafe axis -1..1", Required = true)]
            public float H { get; set; }

            [ToolParameter("Forward axis -1..1", Required = true)]
            public float V { get; set; }

            [ToolParameter("Sprint")]
            public bool Sprint { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params ?? new JObject());
            return new SuccessResponse(
                PlayDriver.Move(p.GetFloat("h", 0f) ?? 0f, p.GetFloat("v", 0f) ?? 0f, p.GetBool("sprint")),
                PlayDriver.Capture());
        }
    }

    [UnityCliTool(Name = "player_look", Group = "play", Description = "Set orbit camera yaw/pitch/distance.")]
    public static class PlayerLookTool
    {
        public class Parameters
        {
            [ToolParameter("Yaw degrees", Required = true)]
            public float Yaw { get; set; }

            [ToolParameter("Pitch degrees", Required = true)]
            public float Pitch { get; set; }

            [ToolParameter("Distance metres (optional)")]
            public float Distance { get; set; } = -1f;
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params ?? new JObject());
            return new SuccessResponse(
                PlayDriver.Look(
                    p.GetFloat("yaw", 0f) ?? 0f,
                    p.GetFloat("pitch", 18f) ?? 18f,
                    p.GetFloat("distance", -1f) ?? -1f),
                PlayDriver.Capture());
        }
    }

    [UnityCliTool(Name = "player_select", Group = "play", Description = "Select build palette index 0..7 (Deck/Column/Pontoon/Brace/Still/Roof/Wall/Stairs).")]
    public static class PlayerSelectTool
    {
        public class Parameters
        {
            [ToolParameter("Palette index", Required = true)]
            public int Index { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params ?? new JObject());
            return new SuccessResponse(PlayDriver.Select(p.GetInt("index", 0) ?? 0));
        }
    }

    [UnityCliTool(Name = "player_build", Group = "play", Description = "Place the selected (or given) piece at a raft cell.")]
    public static class PlayerBuildTool
    {
        public class Parameters
        {
            [ToolParameter("Cell X", Required = true)]
            public int CellX { get; set; }

            [ToolParameter("Cell Y", Required = true)]
            public int CellY { get; set; }

            [ToolParameter("Optional palette index before placing")]
            public int Index { get; set; } = -1;

            [ToolParameter("Storey 0-2; omit to use the player's current build level")]
            public int Level { get; set; } = -1;

            [ToolParameter("0=east 1=north 2=west 3=south; omit to use the current facing")]
            public int Facing { get; set; } = -1;
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params ?? new JObject());
            return new SuccessResponse(
                PlayDriver.Build(
                    p.GetInt("cell_x", 0) ?? 0,
                    p.GetInt("cell_y", 0) ?? 0,
                    p.GetInt("index", -1) ?? -1,
                    p.GetInt("level", -1) ?? -1,
                    p.GetInt("facing", -1) ?? -1),
                PlayDriver.Capture());
        }
    }

    [UnityCliTool(Name = "player_dismantle", Group = "play", Description = "Dismantle the piece at a raft cell.")]
    public static class PlayerDismantleTool
    {
        public class Parameters
        {
            [ToolParameter("Cell X", Required = true)]
            public int CellX { get; set; }

            [ToolParameter("Cell Y", Required = true)]
            public int CellY { get; set; }

            [ToolParameter("Storey 0-2; omit to use the player's current build level")]
            public int Level { get; set; } = -1;
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params ?? new JObject());
            return new SuccessResponse(
                PlayDriver.Dismantle(p.GetInt("cell_x", 0) ?? 0, p.GetInt("cell_y", 0) ?? 0,
                                     p.GetInt("level", -1) ?? -1),
                PlayDriver.Capture());
        }
    }

    [UnityCliTool(Name = "player_rotate", Group = "play", Description = "Rotate the build facing one step (or set it with facing=0-3).")]
    public static class PlayerRotateTool
    {
        public class Parameters
        {
            [ToolParameter("0=east 1=north 2=west 3=south; omit to rotate one step")]
            public int Facing { get; set; } = -1;
        }

        public static object HandleCommand(JObject @params)
        {
            if (!Application.isPlaying) return new ErrorResponse("not in play mode");
            var p = new ToolParams(@params ?? new JObject());
            int facing = p.GetInt("facing", -1) ?? -1;
            var player = PlayDriver.Player;
            if (facing < 0) facing = player != null ? player.BuildFacing + 1 : 0;
            return new SuccessResponse(PlayDriver.Rotate(facing), PlayDriver.Capture());
        }
    }

    [UnityCliTool(Name = "player_level", Group = "play", Description = "Cycle the build level 0-2 (or set it with level=N).")]
    public static class PlayerLevelTool
    {
        public class Parameters
        {
            [ToolParameter("Storey 0-2; omit to cycle")]
            public int Level { get; set; } = -1;
        }

        public static object HandleCommand(JObject @params)
        {
            if (!Application.isPlaying) return new ErrorResponse("not in play mode");
            var p = new ToolParams(@params ?? new JObject());
            int level = p.GetInt("level", -1) ?? -1;
            var player = PlayDriver.Player;
            if (level < 0) level = player != null ? (player.BuildLevel + 1) % (RaftState.MaxLevel + 1) : 0;
            return new SuccessResponse(PlayDriver.Level(level), PlayDriver.Capture());
        }
    }

    [UnityCliTool(Name = "shelter_demo", Group = "play", Description = "Build four columns and a roof on the starting raft, then stand under it. For verifying shelter end to end.")]
    public static class ShelterDemoTool
    {
        public static object HandleCommand(JObject @params)
        {
            if (!Application.isPlaying) return new ErrorResponse("not in play mode");
            var world = PlayDriver.World;
            if (world == null || world.Raft == null) return new ErrorResponse("no world");

            world.Raft.Inventory.Add(ResourceKind.Plank, 20);
            world.Raft.Inventory.Add(ResourceKind.Scrap, 20);

            var log = new System.Text.StringBuilder();
            foreach (var cell in new[] { new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(0, 0) })
                log.AppendLine(PlayDriver.Build(cell.x, cell.y, 1, 0, 0));
            log.AppendLine(PlayDriver.Build(-1, -1, 5, 1, 0));

            PlayDriver.Teleport(new Vector3(-1.5f, RaftState.BaseDeckY, -1.5f));
            return new SuccessResponse(log.ToString().TrimEnd(), PlayDriver.Capture());
        }
    }

    [UnityCliTool(Name = "house_demo", Group = "play", Description = "Build a two-storey house on the starting raft: columns, walls on three sides, first floor, stairs, upper columns and a roof. Then stand at the foot of the stairs.")]
    public static class HouseDemoTool
    {
        public static object HandleCommand(JObject @params)
        {
            if (!Application.isPlaying) return new ErrorResponse("not in play mode");
            var world = PlayDriver.World;
            if (world == null || world.Raft == null) return new ErrorResponse("no world");

            world.Raft.Inventory.Add(ResourceKind.Plank, 80);
            world.Raft.Inventory.Add(ResourceKind.Scrap, 40);

            var corners = new[] { new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(0, 0) };
            var log = new System.Text.StringBuilder();
            foreach (var cell in corners) log.AppendLine(PlayDriver.Build(cell.x, cell.y, 1, 0, 0));
            log.AppendLine(PlayDriver.Build(-1, -1, 5, 1, 0));
            log.AppendLine(PlayDriver.Build(-1, -1, 6, 0, 0));
            log.AppendLine(PlayDriver.Build(-1, -1, 6, 0, 1));
            log.AppendLine(PlayDriver.Build(-1, 0, 6, 0, 0));
            log.AppendLine(PlayDriver.Build(1, -1, 7, 0, 2));
            foreach (var cell in corners) log.AppendLine(PlayDriver.Build(cell.x, cell.y, 1, 1, 0));
            log.AppendLine(PlayDriver.Build(-1, -1, 5, 2, 0));

            PlayDriver.Teleport(new Vector3(2.6f, RaftState.BaseDeckY, -3f));
            return new SuccessResponse(log.ToString().TrimEnd(), PlayDriver.Capture());
        }
    }

    [UnityCliTool(Name = "world_storm", Group = "play", Description = "Start (on=true) or end (on=false) a storm now. The storm is solved immediately, so overloaded pieces fail and unsupported ones collapse.")]
    public static class WorldStormTool
    {
        public class Parameters
        {
            [ToolParameter("true to start a storm, false to end it", Required = true)]
            public bool On { get; set; }

            [ToolParameter("Optional storm wind in kN/m for this storm")]
            public float Wind { get; set; } = -1f;
        }

        public static object HandleCommand(JObject @params)
        {
            if (!Application.isPlaying) return new ErrorResponse("not in play mode");
            var world = PlayDriver.World;
            if (world == null || world.Raft == null) return new ErrorResponse("no world");

            var p = new ToolParams(@params ?? new JObject());
            bool on = p.GetBool("on");
            world.SetStorm(on);
            float wind = p.GetFloat("wind", -1f) ?? -1f;
            if (on && wind > 0f)
            {
                world.Raft.WindLoadKnPerM = wind;
                world.Reanalyse();
            }

            var report = world.LastWind;
            return new SuccessResponse(
                $"storm {(on ? "on" : "off")} wind={world.Raft.WindLoadKnPerM:0.#} kN/m failed={world.Raft.CountFailedPieces()} unstable={report?.IsUnstable}",
                PlayDriver.Capture());
        }
    }

    [UnityCliTool(Name = "player_preview", Group = "play", Description = "Aim the build preview at a grid point (cell_x, cell_y) instead of the mouse; clear=true hands it back to the mouse.")]
    public static class PlayerPreviewTool
    {
        public class Parameters
        {
            [ToolParameter("Cell X")]
            public int CellX { get; set; }

            [ToolParameter("Cell Y")]
            public int CellY { get; set; }

            [ToolParameter("Return the preview to the mouse")]
            public bool Clear { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (!Application.isPlaying) return new ErrorResponse("not in play mode");
            var player = PlayDriver.Player;
            if (player == null) return new ErrorResponse("no player");

            var p = new ToolParams(@params ?? new JObject());
            if (p.GetBool("clear"))
            {
                player.PreviewCellOverride = null;
                return new SuccessResponse("preview follows the mouse");
            }

            player.PreviewCellOverride = new Vector2Int(p.GetInt("cell_x", 0) ?? 0, p.GetInt("cell_y", 0) ?? 0);
            return new SuccessResponse(PreviewVerdict(player), PlayDriver.Capture());
        }

        private static string PreviewVerdict(DesalEra.Unity.PlayerController player)
        {
            var world = PlayDriver.World;
            int level = player.SelectedPiece != null && player.SelectedPiece.Name == "Roof"
                ? Mathf.Max(1, player.BuildLevel) : player.BuildLevel;
            string error = world.Raft.CanPlace(player.SelectedPiece, player.PreviewCellOverride.Value, level,
                                               player.BuildFacing, out _);
            return error == null ? "preview: placeable" : "preview: " + error;
        }
    }

    [UnityCliTool(Name = "player_eat", Group = "play", Description = "Eat one ration from inventory.")]
    public static class PlayerEatTool
    {
        public static object HandleCommand(JObject @params) =>
            new SuccessResponse(PlayDriver.Eat(), PlayDriver.Capture());
    }

    [UnityCliTool(Name = "player_teleport", Group = "play", Description = "Teleport the survivor to a world position.")]
    public static class PlayerTeleportTool
    {
        public class Parameters
        {
            [ToolParameter("World X", Required = true)]
            public float X { get; set; }

            [ToolParameter("World Y (omit to keep current height / surface follow)")]
            public float Y { get; set; }

            [ToolParameter("World Z", Required = true)]
            public float Z { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params ?? new JObject());
            float x = p.GetFloat("x", 0f) ?? 0f;
            float z = p.GetFloat("z", 0f) ?? 0f;
            float y;
            if (p.GetRaw("y") == null)
            {
                var player = PlayDriver.Player;
                y = player != null ? player.transform.position.y : RaftState.BaseDeckY;
            }
            else
            {
                y = p.GetFloat("y", 0f) ?? 0f;
            }

            return new SuccessResponse(PlayDriver.Teleport(new Vector3(x, y, z)), PlayDriver.Capture());
        }
    }

    [UnityCliTool(Name = "player_ui", Group = "play", Description = "Open/close HUD panels. panel=pack|none.")]
    public static class PlayerUiTool
    {
        public class Parameters
        {
            [ToolParameter("Panel id: pack, or none to close", Required = true)]
            public string Panel { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params ?? new JObject());
            return new SuccessResponse(PlayDriver.Ui(p.Get("panel")));
        }
    }

    [UnityCliTool(Name = "player_state", Group = "play", Description = "Capture current survivor / raft snapshot for diagnostics.")]
    public static class PlayerStateTool
    {
        public static object HandleCommand(JObject @params)
        {
            if (!Application.isPlaying) return new ErrorResponse("not in play mode");
            return new SuccessResponse("state", PlayDriver.Capture());
        }
    }

    [UnityCliTool(Name = "player_visual", Group = "play", Description = "Root vs. visible mesh position (head, chest, bounds centre) to diagnose visual jumps.")]
    public static class PlayerVisualTool
    {
        public static object HandleCommand(JObject @params)
        {
            if (!Application.isPlaying) return new ErrorResponse("not in play mode");
            var p = Object.FindObjectOfType<DesalEra.Unity.PlayerController>();
            if (p == null) return new ErrorResponse("no player");
            var smr = p.GetComponentInChildren<SkinnedMeshRenderer>();
            Vector3 head = Vector3.zero, chest = Vector3.zero;
            foreach (var t in p.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Head") head = t.position;
                else if (t.name == "Spine02") chest = t.position;
            }
            Vector3 root = p.transform.position;
            return new SuccessResponse("visual", new
            {
                anim = p.Animator != null ? p.Animator.CurrentState : "",
                root = new[] { root.x, root.y, root.z },
                head = new[] { head.x, head.y, head.z },
                chest = new[] { chest.x, chest.y, chest.z },
                boundsCentre = smr != null ? new[] { smr.bounds.center.x, smr.bounds.center.y, smr.bounds.center.z } : null,
            });
        }
    }
}
