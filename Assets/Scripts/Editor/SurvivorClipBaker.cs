using System;
using System.Collections.Generic;
using System.IO;
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
    /// Two problems have to be solved for these clips to drive the character, and both
    /// are invisible until the model is actually rendered.
    ///
    /// Paths. An AnimationClip inside an FBX is a sub-asset, not a Component, so it
    /// cannot be reached by name at runtime and cannot be loaded from Resources. Baking
    /// it to a .anim file fixes that. Meshy also writes the animation and the character
    /// as separate exports whose rigs disagree on naming: the animation roots the rig at
    /// "target_character" and prefixes bones with "mixamorig:", while the character hangs
    /// "Armature" off the root with unprefixed bones. Every binding path is therefore
    /// resolved against the character rig that is actually in the project, by longest
    /// unambiguous suffix match.
    ///
    /// Bind pose. The two rigs do not even agree on their rest orientation -- LeftArm
    /// rests at (352,240,2) on the character and (59,180,344) in the animation. A clip
    /// stores absolute local rotations, so applying one rig's clip to the other's bones
    /// drives the arms straight over the head and bows the head at the chest. Each curve
    /// is therefore retargeted as a delta measured from the source rest pose and
    /// re-applied on the character's own rest pose:
    ///
    ///     out(t) = charRest * inverse(animRest) * src(t)
    ///
    /// This is the only part of the pipeline that cannot be verified by inspecting
    /// curves: a bake can bind every path correctly, report zero unresolved bindings,
    /// and still pose the character wrongly. It was confirmed by rendering the result.
    /// </summary>
    public static class SurvivorClipBaker
    {
        private const string SourceFolder = "Assets/Art/Characters/Survivor/Animations";
        private const string OutputFolder = "Assets/Resources";
        private const string CharacterModelPath = "Assets/Art/Characters/Survivor/Models/Survivor.fbx";

        // Stripped from every animation bone name before matching.
        private const string BonePrefix = "mixamorig:";

        /// <summary>
        /// Bones that make up one arm, proximal to distal, paired with the share of the
        /// swing each one carries.
        ///
        /// The swing is split rather than applied to the clavicle alone because a single
        /// 67 degree rotation tears the mesh open at the armpit. Vertices around the
        /// shoulder are weighted across the spine, the clavicle and the upper arm, so
        /// turning one of those joints by the full amount drags them apart. Handing each
        /// joint part of the rotation keeps every joint's deformation small, which is the
        /// same reason a rigger never corrects an A-pose on the shoulder alone.
        /// </summary>
        private static readonly (string Bone, float Weight)[] ArmChain =
        {
            ("LeftShoulder", 0.45f),
            ("LeftArm", 0.40f),
            ("LeftForeArm", 0.15f),
            ("RightShoulder", 0.45f),
            ("RightArm", 0.40f),
            ("RightForeArm", 0.15f),
        };

        /// <summary>
        /// How far the arms are swung from straight out towards straight down.
        ///
        /// 90 degrees would hang them against the body, which reads as stiff. Stopping
        /// short leaves a slight outward angle, the A-pose a standing person actually
        /// holds. Measured on this rig the rest pose points 0.98 along the character's
        /// left with only 0.19 of drop over a 0.54 m arm, so the swing needed is close to
        /// the full quarter turn.
        /// </summary>
        private const float ArmHangDegrees = 78f;

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
            // Rebuilding the controller deletes and recreates the asset, which invalidates
            // whatever a running player already resolved from Resources. The survivor is
            // then left with a destroyed controller reference that reads as null, and the
            // only symptom is a character that silently does not animate. Baking is an
            // edit-time step anyway.
            if (EditorApplication.isPlaying)
            {
                const string message = "ABORTED: cannot bake while in play mode; exit play and re-run";
                Debug.LogError("[DesalEra] " + message);
                return message;
            }

            var log = new System.Text.StringBuilder();

            var character = LoadRig(CharacterModelPath);
            if (character == null)
            {
                Debug.LogError($"[DesalEra] character model missing at {CharacterModelPath}");
                return "ABORTED: no character rig";
            }

            log.Append("character bones=").Append(character.Count).Append(" ");

            // Every FBX dropped into the folder is baked, rather than a list kept in code.
            // Meshy delivers one archive per animation, each holding a single clip, so a
            // hardcoded list meant editing and recompiling the baker for every new
            // download. The state name comes from the file name, which keeps the asset
            // name and the Animator state from drifting apart.
            var sources = DiscoverSources();
            log.Append("sources=").Append(sources.Count).Append(" ");

            foreach (string fbxName in sources)
            {
                Bake(fbxName, StateNameFor(fbxName), character, log);
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
        /// "Survivor_Walk" becomes "Walk", which is both the clip name and the Animator
        /// state name.
        /// </summary>
        /// <summary>States that cycle until gameplay leaves them; everything else is one-shot.</summary>
        public static readonly HashSet<string> LoopingStates = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "Idle", "Walk", "Run", "SwimIdle", "SwimForward", "LadderClimbLoop", "RopeHangIdle",
        };

        private static string StateNameFor(string fbxName)
        {
            const string prefix = "Survivor_";
            return fbxName.StartsWith(prefix, System.StringComparison.Ordinal)
                ? fbxName.Substring(prefix.Length)
                : fbxName;
        }

        /// <summary>
        /// Transform paths of a rig, relative to its own root, mapped to the transforms
        /// themselves so rest poses can be read straight off them.
        ///
        /// <paramref name="stripBonePrefix"/> is set for the animation rig only. ResolvePath
        /// strips "mixamorig:" before it looks anything up, so the source rig has to be
        /// keyed the same way or the two sides of the match cannot be compared. The
        /// character rig is never stripped: its keys are the paths written into the baked
        /// curves, so they have to stay exactly as Unity sees them.
        /// </summary>
        private static Dictionary<string, Transform> LoadRig(string assetPath, bool stripBonePrefix = false)
        {
            var rig = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null) return rig;

            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t == model.transform) continue;

                string path = RelativePath(t, model.transform);
                if (stripBonePrefix) path = StripPrefixPerSegment(path);

                rig[path] = t;
            }

            return rig;
        }

        private static string StripPrefixPerSegment(string path)
        {
            var segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].StartsWith(BonePrefix, System.StringComparison.OrdinalIgnoreCase))
                    segments[i] = segments[i].Substring(BonePrefix.Length);
            }

            return string.Join("/", segments);
        }

        private static string RelativePath(Transform node, Transform root)
        {
            var stack = new List<string>();
            for (var cur = node; cur != null && cur != root; cur = cur.parent)
                stack.Add(cur.name);

            stack.Reverse();
            return string.Join("/", stack);
        }

        /// <summary>
        /// Rotation that swings an arm bone's subtree partway towards hanging down.
        ///
        /// The axis is derived from the rig rather than dialled in: it is whatever axis
        /// takes the bone's actual direction to its child towards straight down. A
        /// hardcoded Euler here would be silently wrong for any other model and would
        /// encode nothing about why the number is what it is.
        ///
        /// The result is expressed in the bone's own frame so it can be post-multiplied
        /// onto the rest rotation, which rotates the whole subtree about the bone while
        /// leaving every joint angle inside it alone.
        /// </summary>
        private static Quaternion ArmSwingCorrection(Transform bone, float degrees)
        {
            if (bone == null || bone.childCount == 0) return Quaternion.identity;
            if (degrees <= 0f) return Quaternion.identity;

            Vector3 origin = bone.localToWorldMatrix.MultiplyPoint3x4(Vector3.zero);
            Vector3 tip = bone.GetChild(0).localToWorldMatrix.MultiplyPoint3x4(Vector3.zero);
            Vector3 current = tip - origin;

            // A zero-length child offset means the rig is not laid out the way this
            // correction assumes. Leaving the bone alone beats rotating about a
            // meaningless axis.
            if (current.sqrMagnitude < 1e-8f) return Quaternion.identity;

            current.Normalize();

            Vector3 down = Vector3.down;
            Vector3 axis = Vector3.Cross(current, down);

            // Already vertical, or the cross product vanishes. Nothing to swing.
            if (axis.sqrMagnitude < 1e-8f) return Quaternion.identity;
            axis.Normalize();

            // Never swing past vertical. The chain is rotated by every bone in sequence,
            // so a bone late in the chain may already be closer to hanging than the share
            // it was handed suggests.
            float angle = Mathf.Min(degrees, Vector3.Angle(current, down));
            if (angle <= 0f) return Quaternion.identity;

            // A rotation about a fixed axis scaled by a fraction is the same axis at the
            // scaled angle, so the local axis can be derived once from the world axis.
            Quaternion boneWorldInverse = Quaternion.Inverse(bone.localToWorldMatrix.rotation);
            Vector3 localAxis = boneWorldInverse * axis;

            return Quaternion.AngleAxis(angle, localAxis);
        }

        /// <summary>
        /// Rest rotation for a character bone, with its share of the arm swing folded in.
        /// </summary>
        private static Quaternion CorrectedRest(Transform bone)
        {
            Quaternion rest = bone.localRotation;

            foreach (var (name, weight) in ArmChain)
            {
                if (!bone.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) continue;
                rest *= ArmSwingCorrection(bone, ArmHangDegrees * weight);
                break;
            }

            return rest;
        }

        /// <summary>
        /// Maps one animation binding path onto the character rig. Returns null when no
        /// bone matches, or when the tail is ambiguous.
        ///
        /// Shorter trailing segments are tried after longer ones, so the animation's
        /// leading wrapper node is dropped without being named: "Hips/Spine" fails
        /// against a rig whose spine is "Armature/Hips/Spine02/Spine01/Spine" at full
        /// length but matches at one segment. Trying the longest tail first keeps a
        /// repeated bone name from resolving to the wrong one.
        /// </summary>
        private static string ResolvePath(string path, Dictionary<string, Transform> rig)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var segments = new List<string>();
            foreach (string raw in path.Split('/'))
            {
                string name = raw.StartsWith(BonePrefix, System.StringComparison.OrdinalIgnoreCase)
                    ? raw.Substring(BonePrefix.Length)
                    : raw;
                if (name.Length > 0) segments.Add(name);
            }

            for (int take = segments.Count; take >= 1; take--)
            {
                string found = null;
                bool ambiguous = false;

                foreach (string candidate in rig.Keys)
                {
                    var modelSegments = candidate.Split('/');
                    if (modelSegments.Length < take) continue;

                    bool allMatch = true;
                    for (int i = 0; i < take; i++)
                    {
                        if (!string.Equals(modelSegments[modelSegments.Length - take + i],
                                           segments[segments.Count - take + i],
                                           System.StringComparison.OrdinalIgnoreCase))
                        {
                            allMatch = false;
                            break;
                        }
                    }

                    if (!allMatch) continue;

                    // Two bones sharing a name in different subtrees would make this pick
                    // a target at random. Refusing is safer: a missing bone is obvious, a
                    // bone driven by the wrong subtree is not.
                    if (found != null) { ambiguous = true; break; }
                    found = candidate;
                }

                if (ambiguous) return null;
                if (found != null) return found;
            }

            return null;
        }

        private static void Bake(string fbxName, string clipName,
            Dictionary<string, Transform> character, System.Text.StringBuilder log)
        {
            string fbxPath = SourceFolder + "/" + fbxName + ".fbx";
            string outPath = OutputFolder + "/" + fbxName + ".anim";

            var source = FindClip(fbxPath);
            if (source == null)
            {
                log.Append(clipName).Append(": NO CLIP in ").Append(fbxName).Append(" || ");
                return;
            }

            var sourceRig = LoadRig(fbxPath, stripBonePrefix: true);
            if (sourceRig.Count == 0)
            {
                log.Append(clipName).Append(": no rig in ").Append(fbxName).Append(" || ");
                return;
            }

            // Group the source curves by bone path: rotation, position and scale for one
            // bone are four-plus curves that have to be read together to be retargeted.
            var byPath = new Dictionary<string, Dictionary<string, AnimationCurve>>(System.StringComparer.Ordinal);
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                if (curve == null) continue;

                if (!byPath.TryGetValue(binding.path, out var group))
                {
                    group = new Dictionary<string, AnimationCurve>();
                    byPath[binding.path] = group;
                }

                group[binding.propertyName] = curve;
            }

            // Mecanim clip, not a legacy one: Animator and the legacy Animation component
            // do not interoperate, and Animator is the supported path for an imported rig.
            var baked = new AnimationClip
            {
                name = clipName,
                legacy = false,
                frameRate = source.frameRate > 0f ? source.frameRate : 30f,
                wrapMode = WrapMode.Loop
            };

            // The Meshy FBX exports ship with loopTime off, and Mecanim / Playables ignore
            // wrapMode, so without this every cyclic state played once and froze on its
            // last frame.
            var settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = LoopingStates.Contains(clipName);
            AnimationUtility.SetAnimationClipSettings(baked, settings);

            int bones = 0, dropped = 0;
            var unresolved = new SortedSet<string>(System.StringComparer.Ordinal);

            foreach (var pair in byPath)
            {
                string charPath = ResolvePath(pair.Key, character);
                string animPath = ResolvePath(pair.Key, sourceRig);

                if (charPath == null || animPath == null)
                {
                    dropped++;
                    unresolved.Add(pair.Key);
                    continue;
                }

                Transform charBone = character[charPath];
                Transform animBone = sourceRig[animPath];

                WriteRetargetedRotation(baked, charPath, pair.Value, charBone, animBone);
                WriteRetargetedVector(baked, charPath, pair.Value, "m_LocalPosition",
                    charBone.localPosition, animBone.localPosition);
                WriteRetargetedVector(baked, charPath, pair.Value, "m_LocalScale",
                    charBone.localScale, animBone.localScale);

                bones++;
            }

            // Re-baking over an existing file leaves the old curves in place, so the asset
            // is deleted first. A curve whose bone has since been renamed would otherwise
            // linger and keep driving something.
            AssetDatabase.DeleteAsset(outPath);
            AssetDatabase.CreateAsset(baked, outPath);

            log.Append(clipName).Append(": ").Append(source.length.ToString("F2")).Append("s ")
               .Append(bones).Append(" bones retargeted, ").Append(dropped).Append(" unresolved");

            if (unresolved.Count > 0)
            {
                log.Append(" [");
                int shown = 0;
                foreach (string u in unresolved)
                {
                    if (shown++ >= 3) break;
                    log.Append(u).Append(" | ");
                }
                log.Append("]");
            }

            log.Append(" || ");
        }

        /// <summary>
        /// Rewrites one bone's rotation as a delta from the source rest pose, applied on
        /// top of the character's rest pose.
        /// </summary>
        private static void WriteRetargetedRotation(AnimationClip baked, string charPath,
            Dictionary<string, AnimationCurve> curves, Transform charBone, Transform animBone)
        {
            AnimationCurve cx, cy, cz, cw;
            if (!curves.TryGetValue("m_LocalRotation.x", out cx) ||
                !curves.TryGetValue("m_LocalRotation.y", out cy) ||
                !curves.TryGetValue("m_LocalRotation.z", out cz) ||
                !curves.TryGetValue("m_LocalRotation.w", out cw))
                return;

            Quaternion charRest = CorrectedRest(charBone);
            Quaternion animRest = animBone.localRotation;
            Quaternion animRestInverse = Quaternion.Inverse(animRest);

            var times = UnionOfTimes(cx, cy, cz, cw);
            var kx = new Keyframe[times.Count];
            var ky = new Keyframe[times.Count];
            var kz = new Keyframe[times.Count];
            var kw = new Keyframe[times.Count];

            for (int i = 0; i < times.Count; i++)
            {
                float t = times[i];

                var q = new Quaternion(cx.Evaluate(t), cy.Evaluate(t), cz.Evaluate(t), cw.Evaluate(t));
                float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                if (mag > 1e-6f) q = new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);

                Quaternion outRot = charRest * animRestInverse * q;
                outRot.Normalize();

                kx[i] = new Keyframe(t, outRot.x);
                ky[i] = new Keyframe(t, outRot.y);
                kz[i] = new Keyframe(t, outRot.z);
                kw[i] = new Keyframe(t, outRot.w);
            }

            SetCurve(baked, charPath, "m_LocalRotation.x", kx);
            SetCurve(baked, charPath, "m_LocalRotation.y", ky);
            SetCurve(baked, charPath, "m_LocalRotation.z", kz);
            SetCurve(baked, charPath, "m_LocalRotation.w", kw);
        }

        /// <summary>
        /// Rewrites a position or scale track the same way: the offset the clip applies on
        /// the source rig, applied on the character's own rest value.
        /// </summary>
        private static void WriteRetargetedVector(AnimationClip baked, string charPath,
            Dictionary<string, AnimationCurve> curves, string prefix, Vector3 charRest, Vector3 animRest)
        {
            AnimationCurve cx, cy, cz;
            if (!curves.TryGetValue(prefix + ".x", out cx) ||
                !curves.TryGetValue(prefix + ".y", out cy) ||
                !curves.TryGetValue(prefix + ".z", out cz))
                return;

            var times = UnionOfTimes(cx, cy, cz);
            var kx = new Keyframe[times.Count];
            var ky = new Keyframe[times.Count];
            var kz = new Keyframe[times.Count];

            for (int i = 0; i < times.Count; i++)
            {
                float t = times[i];
                Vector3 src = new Vector3(cx.Evaluate(t), cy.Evaluate(t), cz.Evaluate(t));
                Vector3 result = charRest + (src - animRest);

                kx[i] = new Keyframe(t, result.x);
                ky[i] = new Keyframe(t, result.y);
                kz[i] = new Keyframe(t, result.z);
            }

            SetCurve(baked, charPath, prefix + ".x", kx);
            SetCurve(baked, charPath, prefix + ".y", ky);
            SetCurve(baked, charPath, prefix + ".z", kz);
        }

        private static void SetCurve(AnimationClip clip, string path, string property, Keyframe[] keys)
        {
            if (keys.Length == 0) return;

            AnimationUtility.SetEditorCurve(clip, new EditorCurveBinding
            {
                path = path,
                type = typeof(Transform),
                propertyName = property
            }, new AnimationCurve(keys));
        }

        private static List<float> UnionOfTimes(params AnimationCurve[] curves)
        {
            var times = new SortedSet<float>();
            foreach (var curve in curves)
            {
                if (curve == null) continue;
                foreach (var key in curve.keys) times.Add(key.time);
            }

            return new List<float>(times);
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
