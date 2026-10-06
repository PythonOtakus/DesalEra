using System.Text;
using UnityEditor;
using UnityEngine;

namespace DesalEra.EditorTools
{
    /// <summary>
    /// Reports the real dimensions of an imported model so the import scale can be set
    /// from measurement rather than assumption.
    ///
    /// Generators disagree about units. Meshy exports in centimetres, Mixamo in metres,
    /// and Blender defaults to metres. Setting globalScale by guess produces a
    /// character who is 100x too tall or too small, which is easy to miss in a
    /// screenshot and annoying to diagnose later.
    /// </summary>
    public static class ModelInspector
    {
        [MenuItem("DesalEra/Report Model Bounds")]
        public static void ReportAll()
        {
            var report = new StringBuilder();

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                report.AppendLine($"{path}");
                report.AppendLine($"  globalScale      {importer.globalScale}");
                report.AppendLine($"  useFileScale     {importer.useFileScale}");

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;

                Bounds bounds = new Bounds();
                bool first = true;
                MeshFilter[] filters = asset.GetComponentsInChildren<MeshFilter>(true);
                SkinnedMeshRenderer[] skinned = asset.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                foreach (MeshFilter filter in filters)
                {
                    if (filter.sharedMesh == null) continue;
                    if (first) { bounds = filter.sharedMesh.bounds; first = false; }
                    else bounds.Encapsulate(filter.sharedMesh.bounds);
                }

                foreach (SkinnedMeshRenderer renderer in skinned)
                {
                    if (renderer.sharedMesh == null) continue;
                    if (first) { bounds = renderer.localBounds; first = false; }
                    else bounds.Encapsulate(renderer.localBounds);
                }

                if (first) { report.AppendLine("  no mesh found"); continue; }

                Vector3 size = bounds.size;
                report.AppendLine($"  meshFilters      {filters.Length}");
                report.AppendLine($"  skinnedRenderers {skinned.Length}");
                report.AppendLine($"  bones            {asset.GetComponentsInChildren<Transform>(true).Length - 1}");
                report.AppendLine($"  local bounds min {bounds.min}");
                report.AppendLine($"  local bounds max {bounds.max}");
                report.AppendLine($"  SIZE (m)         {size.x:F3} x {size.y:F3} x {size.z:F3}");
                report.AppendLine();

                if (filters.Length == 0)
                {
                    report.AppendLine("  ^ no MeshFilter: this is a SkinnedMesh character. " +
                                      "Bounds are in local space, so multiply by globalScale to get world size.");
                    report.AppendLine();
                }
            }

            Debug.Log("[DesalEra] model report\n" + report);
            Debug.Log("[DesalEra] model report\n" + report);
        }
    }
}
