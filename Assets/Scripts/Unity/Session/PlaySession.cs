using System;
using System.IO;
using UnityEngine;

namespace DesalEra.Unity.Session
{
    /// <summary>
    /// One player-facing operation that can be recorded, replayed, and invoked from
    /// unity-cli. Keep kinds stable — session JSON files and CLI tool names both key off
    /// these strings.
    /// </summary>
    public static class PlayActionKind
    {
        public const string Move = "move";
        public const string Look = "look";
        public const string Select = "select";
        public const string Build = "build";
        public const string Dismantle = "dismantle";
        public const string Rotate = "rotate";
        public const string Level = "level";
        public const string Eat = "eat";
        public const string Teleport = "teleport";
        public const string Ui = "ui";
        public const string Wait = "wait";
        public const string Note = "note";
    }

    [Serializable]
    public sealed class PlayActionRecord
    {
        public string kind;
        public float t;
        public float h;
        public float v;
        public bool sprint;
        public float yaw;
        public float pitch;
        public float distance;
        public int index;
        public int cellX;
        public int cellY;
        public int level;
        public int facing;
        public float x;
        public float y;
        public float z;
        public string panel;
        public float seconds;
        public string text;
        public string result;
    }

    [Serializable]
    public sealed class PlaySnapshot
    {
        public float t;
        public float x;
        public float y;
        public float z;
        public float yaw;
        public float pitch;
        public float distance;
        public string mode;
        public string anim;
        public int selected;
        public string status;
        public float food;
        public float water;
        public float health;
        public float stamina;
        public int plank;
        public int scrap;
        public int metal;
        public int foodQty;
        public int waterQty;
        public int members;
        public float facingX;
        public float facingZ;
        public float soleGap;

        /// <summary>Raft-local height of the surface underfoot: deck, floor or stair tread.</summary>
        public float surfaceY;
        public bool sheltered;
        public bool storm;
        public int buildLevel;
        public int buildFacing;
    }

    [Serializable]
    public sealed class PlaySession
    {
        public string id;
        public string startedAt;
        public string stoppedAt;
        public float duration;
        public string notes;
        public PlayActionRecord[] actions = Array.Empty<PlayActionRecord>();
        public PlaySnapshot[] snapshots = Array.Empty<PlaySnapshot>();

        public static string DirectoryPath =>
            Path.Combine(Application.dataPath, "..", "SessionRecordings");

        public string FilePath => Path.Combine(DirectoryPath, id + ".json");

        public void Save()
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, JsonUtility.ToJson(this, prettyPrint: true));
        }

        public static PlaySession Load(string pathOrId)
        {
            string path = pathOrId;
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (!Path.IsPathRooted(path))
                    path = Path.Combine(DirectoryPath, path + ".json");
            }
            else if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(DirectoryPath, path);
            }

            if (!File.Exists(path))
                throw new FileNotFoundException("Play session not found", path);

            var session = JsonUtility.FromJson<PlaySession>(File.ReadAllText(path));
            if (session == null) throw new InvalidDataException("Invalid session JSON: " + path);
            return session;
        }

        public static string NewId() =>
            "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }
}
