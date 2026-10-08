using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DesalEra.EditorTools.Inspect
{
    /// <summary>
    /// Play 中不要调用 AssetDatabase.Save*：会刷新资源库，弄坏 StreamingAssets
    /// 运行时解码的贴图（甲板变黑）。只 SetDirty，退出 Play 时再统一写盘。
    /// </summary>
    [InitializeOnLoad]
    public static class PlaySafeAssetSave
    {
        private static readonly HashSet<Object> Pending = new HashSet<Object>();

        static PlaySafeAssetSave()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void Mark(Object asset)
        {
            if (asset == null || !EditorUtility.IsPersistent(asset)) return;
            EditorUtility.SetDirty(asset);
            if (Application.isPlaying)
            {
                Pending.Add(asset);
                return;
            }

            AssetDatabase.SaveAssetIfDirty(asset);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 退出 Play 前写盘，否则内存里对 SO 的改动会被磁盘旧值盖掉。
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            Flush();
        }

        public static void Flush()
        {
            if (Pending.Count == 0) return;
            foreach (Object asset in Pending)
            {
                if (asset == null) continue;
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
            Pending.Clear();
        }
    }
}
