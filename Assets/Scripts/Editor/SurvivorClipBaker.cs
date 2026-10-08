using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DesalEra.EditorTools
{
    /// <summary>
    /// Bakes the survivor's animation clips from the Meshy FBX files into standalone
    /// .anim assets under Resources.
    ///
    /// This only produces clips. It deliberately does not author an AnimatorController:
    /// states created by script do not survive reimport, so a controller built here was
    /// playable in the editor and empty in every build. PlayerAnimator drives the baked
    /// clips through a Playables graph instead, which has no asset to serialise.
    ///
    /// An AnimationClip inside an FBX is a sub-asset, not a Component, so it cannot be
    /// loaded from Resources; baking it to a .anim file fixes that. The harder part is
    /// that Meshy exports the animations and the character as separate rigs that agree
    /// on the skeleton but not on anything written in the file:
    ///
    /// Names. The animation prefixes bones with "mixamorig:" and numbers the spine from
    /// the hips up (Spine, Spine1, Spine2); the character numbers it from the chest down
    /// (Spine02, Spine01, Spine). Matching by name alone drives the chest with the
    /// lower back and drops the rest of the spine.
    ///
    /// Rest pose. The transforms an animation FBX imports with are not the bind pose but
    /// a frame of the motion, so each file has a different "rest". Retargeting as a
    /// delta from that rest subtracts the very pose the clip is about: the swim export
    /// rests lying face down, so its stroke baked as an upright figure paddling in
    /// place, and the walk exports rest with arms down, which left the character in a
    /// T-pose. The bind poses themselves are identical (checked against the skinned
    /// export, joint for joint), so the pose is copied in world space instead: each
    /// frame is sampled on the source rig and every character bone is given the world
    /// rotation of its counterpart. No rest pose from the animation file is ever read.
    ///
    /// None of this shows up in the curves. A bake can bind every bone, report nothing
    /// unresolved and still pose the character wrongly; it has to be checked by sampling
    /// the result and comparing body directions against the source.
    /// </summary>
    public static class SurvivorClipBaker
    {
        private const string SourceFolder = "Assets/Art/Characters/Survivor/Animations";
        private const string OutputFolder = "Assets/Resources";
        private const string CharacterModelPath = "Assets/Art/Characters/Survivor/Models/Survivor.fbx";

        // Stripped from every animation bone name before matching.
        private const string BonePrefix = "mixamorig:";

        /// <summary>The bone whose position is animated as well as its rotation.</summary>
        private const string RootBone = "Hips";

        /// <summary>
        /// Character bone names that differ from the animation rig's. Everything not
        /// listed matches by name, ignoring case.
        /// </summary>
        private static readonly Dictionary<string, string> AnimationNameFor =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "Spine02", "Spine" },
                { "Spine01", "Spine1" },
                { "Spine", "Spine2" },
            };

        /// <summary>States that cycle until gameplay leaves them; everything else is one-shot.</summary>
        public static readonly HashSet<string> LoopingStates = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "Idle", "Walk", "Run", "SwimIdle", "SwimForward", "LadderClimbLoop", "RopeHangIdle",
        };

        /// <summary>
        /// Menu entry for re-baking after the source FBX or the character model changes.
        /// The baked assets are generated output and are committed, so a fresh clone has
        /// working animation without needing an editor step first.
        /// </summary>
        [MenuItem("DesalEra/Bake Survivor Animation")]
        public static void BakeFromMenu()
        {
            Debug.Log("[DesalEra] " + BakeAll());
        }

        public static string BakeAll()
        {
            // Replacing a clip asset invalidates whatever a running player already
            // resolved from Resources, and the only symptom is a character that silently
            // stops animating. Baking is an edit-time step anyway.
            if (EditorApplication.isPlaying)
            {
                const string message = "ABORTED: cannot bake while in play mode; exit play and re-run";
                Debug.LogError("[DesalEra] " + message);
                return message;
            }

            var characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (characterPrefab == null)
            {
                Debug.LogError($"[DesalEra] character model missing at {CharacterModelPath}");
                return "ABORTED: no character rig";
            }

            var log = new System.Text.StringBuilder();

            // Every FBX dropped into the folder is baked, rather than a list kept in code.
            // Meshy delivers one archive per animation, each holding a single clip, so a
            // hardcoded list meant editing and recompiling the baker for every download.
            var sources = DiscoverSources();
            log.Append("sources=").Append(sources.Count).Append(" ");

            var character = Object.Instantiate(characterPrefab);
            character.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                foreach (string fbxName in sources)
                    Bake(fbxName, StateNameFor(fbxName), character, log);
            }
            finally
            {
                Object.DestroyImmediate(character);
            }

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        /// <summary>
        /// Base names of every animation FBX in the source folder, sorted so a bake is
        /// reproducible and the log reads in a stable order.
        /// </summary>
        private static List<string> DiscoverSources()
        {
            var names = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { SourceFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                names.Add(System.IO.Path.GetFileNameWithoutExtension(path));
            }

            names.Sort(System.StringComparer.Ordinal);
            return names;
        }

        /// <summary>
        /// "Survivor_Walk" becomes "Walk", which is both the clip name and the state name
        /// PlayerAnimator plays it under.
        /// </summary>
        private static string StateNameFor(string fbxName)
        {
            const string prefix = "Survivor_";
            return fbxName.StartsWith(prefix, System.StringComparison.Ordinal)
                ? fbxName.Substring(prefix.Length)
                : fbxName;
        }

        private static string StripPrefix(string name)
        {
            return name.StartsWith(BonePrefix, System.StringComparison.OrdinalIgnoreCase)
                ? name.Substring(BonePrefix.Length)
                : name;
        }

        private static string RelativePath(Transform node, Transform root)
        {
            var stack = new List<string>();
            for (var cur = node; cur != null && cur != root; cur = cur.parent)
                stack.Add(cur.name);

            stack.Reverse();
            return string.Join("/", stack);
        }

        /// <summary>One character bone and the animation bone that drives it.</summary>
        private sealed class BonePair
        {
            public Transform Character;
            public Transform Source;
            public string Path;
            public bool AnimatePosition;
            public readonly List<Keyframe>[] Rotation = NewChannels(4);
            public readonly List<Keyframe>[] Position = NewChannels(3);
            public Quaternion Previous;
            public bool HasPrevious;

            private static List<Keyframe>[] NewChannels(int count)
            {
                var channels = new List<Keyframe>[count];
                for (int i = 0; i < count; i++) channels[i] = new List<Keyframe>();
                return channels;
            }
        }

        /// <summary>
        /// Pairs every character bone with its animation counterpart, parents first so a
        /// bone's world rotation is set after its parent's when sampling.
        /// </summary>
        private static List<BonePair> PairBones(GameObject character, GameObject source, out List<string> unmatched)
        {
            var sourceByName = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var t in source.GetComponentsInChildren<Transform>(true))
            {
                if (t == source.transform) continue;
                sourceByName[StripPrefix(t.name)] = t;
            }

            var pairs = new List<BonePair>();
            unmatched = new List<string>();

            // GetComponentsInChildren walks depth first, which is the parents-first
            // order sampling depends on.
            foreach (var t in character.GetComponentsInChildren<Transform>(true))
            {
                if (t == character.transform) continue;
                if (t.GetComponent<Renderer>() != null) continue;
                if (t.name == "Armature") continue;

                string wanted = AnimationNameFor.TryGetValue(t.name, out string alias) ? alias : t.name;
                if (!sourceByName.TryGetValue(wanted, out Transform match))
                {
                    unmatched.Add(t.name);
                    continue;
                }

                pairs.Add(new BonePair
                {
                    Character = t,
                    Source = match,
                    Path = RelativePath(t, character.transform),
                    AnimatePosition = t.name.Equals(RootBone, System.StringComparison.OrdinalIgnoreCase),
                });
            }

            return pairs;
        }

        private static void Bake(string fbxName, string clipName, GameObject character,
            System.Text.StringBuilder log)
        {
            string fbxPath = SourceFolder + "/" + fbxName + ".fbx";
            string outPath = OutputFolder + "/" + fbxName + ".anim";

            var clip = FindClip(fbxPath);
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (clip == null || sourcePrefab == null)
            {
                log.Append(clipName).Append(": NO CLIP in ").Append(fbxName).Append(" || ");
                return;
            }

            var source = Object.Instantiate(sourcePrefab);
            source.hideFlags = HideFlags.HideAndDontSave;

            // Bones that a previous clip moved but this one does not would otherwise keep
            // that clip's last pose, and the unanimated ones are supposed to hold bind.
            var bind = new Dictionary<Transform, (Vector3, Quaternion)>();
            foreach (var t in character.GetComponentsInChildren<Transform>(true))
                bind[t] = (t.localPosition, t.localRotation);

            List<BonePair> pairs;
            List<string> unmatched;
            try
            {
                pairs = PairBones(character, source, out unmatched);

                float rate = clip.frameRate > 0f ? clip.frameRate : 30f;
                int frames = Mathf.Max(1, Mathf.RoundToInt(clip.length * rate));

                for (int f = 0; f <= frames; f++)
                {
                    float time = clip.length * f / frames;
                    clip.SampleAnimation(source, time);

                    foreach (var pair in pairs)
                    {
                        pair.Character.rotation = pair.Source.rotation;
                        if (pair.AnimatePosition) pair.Character.position = pair.Source.position;
                        Record(pair, time);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(source);
                foreach (var entry in bind)
                {
                    entry.Key.localPosition = entry.Value.Item1;
                    entry.Key.localRotation = entry.Value.Item2;
                }
            }

            // Mecanim clip, not a legacy one: Animator and the legacy Animation component
            // do not interoperate, and Animator is the supported path for an imported rig.
            var baked = new AnimationClip
            {
                name = clipName,
                legacy = false,
                frameRate = clip.frameRate > 0f ? clip.frameRate : 30f,
                wrapMode = WrapMode.Loop
            };

            // The Meshy FBX exports ship with loopTime off, and Mecanim / Playables ignore
            // wrapMode, so without this every cyclic state played once and froze on its
            // last frame.
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = LoopingStates.Contains(clipName);
            AnimationUtility.SetAnimationClipSettings(baked, settings);

            foreach (var pair in pairs)
            {
                SetCurve(baked, pair.Path, "m_LocalRotation.x", pair.Rotation[0]);
                SetCurve(baked, pair.Path, "m_LocalRotation.y", pair.Rotation[1]);
                SetCurve(baked, pair.Path, "m_LocalRotation.z", pair.Rotation[2]);
                SetCurve(baked, pair.Path, "m_LocalRotation.w", pair.Rotation[3]);
                if (!pair.AnimatePosition) continue;
                SetCurve(baked, pair.Path, "m_LocalPosition.x", pair.Position[0]);
                SetCurve(baked, pair.Path, "m_LocalPosition.y", pair.Position[1]);
                SetCurve(baked, pair.Path, "m_LocalPosition.z", pair.Position[2]);
            }

            // Re-baking over an existing file leaves the old curves in place, so the asset
            // is deleted first. A curve whose bone has since been renamed would otherwise
            // linger and keep driving something.
            AssetDatabase.DeleteAsset(outPath);
            AssetDatabase.CreateAsset(baked, outPath);

            log.Append(clipName).Append(": ").Append(clip.length.ToString("F2")).Append("s ")
               .Append(pairs.Count).Append(" bones");
            if (unmatched.Count > 0)
                log.Append(", unmatched [").Append(string.Join(", ", unmatched)).Append("]");
            log.Append(" || ");
        }

        private static void Record(BonePair pair, float time)
        {
            Quaternion q = pair.Character.localRotation;

            // q and -q are the same rotation, but interpolating between keys of opposite
            // sign takes the long way round and spins the bone a full turn mid-frame.
            if (pair.HasPrevious && Quaternion.Dot(pair.Previous, q) < 0f)
                q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            pair.Previous = q;
            pair.HasPrevious = true;

            pair.Rotation[0].Add(new Keyframe(time, q.x));
            pair.Rotation[1].Add(new Keyframe(time, q.y));
            pair.Rotation[2].Add(new Keyframe(time, q.z));
            pair.Rotation[3].Add(new Keyframe(time, q.w));

            if (!pair.AnimatePosition) return;
            Vector3 p = pair.Character.localPosition;
            pair.Position[0].Add(new Keyframe(time, p.x));
            pair.Position[1].Add(new Keyframe(time, p.y));
            pair.Position[2].Add(new Keyframe(time, p.z));
        }

        private static void SetCurve(AnimationClip clip, string path, string property, List<Keyframe> keys)
        {
            if (keys.Count == 0) return;

            var curve = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);

            AnimationUtility.SetEditorCurve(clip, new EditorCurveBinding
            {
                path = path,
                type = typeof(Transform),
                propertyName = property
            }, curve);
        }

        /// <summary>
        /// Picks the real clip over Meshy's "__preview__" twin. Both decode the same
        /// motion, but the unprefixed name survives a re-export while the prefixed one
        /// embeds an FBX path.
        /// </summary>
        private static AnimationClip FindClip(string assetPath)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                var clip = obj as AnimationClip;
                if (clip != null && !clip.name.StartsWith("__preview__")) return clip;
            }

            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                var clip = obj as AnimationClip;
                if (clip != null) return clip;
            }

            return null;
        }
    }
}
