using System.Collections.Generic;
using DesalEra.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DesalEra.Tests
{
    /// <summary>
    /// Guards the one asset rule a play-mode test cannot catch.
    ///
    /// A shader reached through Shader.Find is present in the editor and absent from a
    /// player unless something puts it in Always Included Shaders. Nothing about that is
    /// visible while playing: the lookup simply returns null and the caller takes a
    /// fallback, so the sea renders as a flat plane and the sky as a flat gradient with no
    /// error anywhere. These assertions are the equivalent of the pixel checks
    /// MaterialTests makes on a mislabelled texture channel — they pin the mistake down at
    /// the only moment it is still cheap.
    /// </summary>
    public sealed class AlwaysIncludedShaderTests
    {
        private static List<Shader> AlwaysIncluded()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            var list = new List<Shader>();

            foreach (var entry in settings)
            {
                if (!(entry is UnityEngine.Rendering.GraphicsSettings graphics)) continue;

                var serialized = new SerializedObject(graphics);
                var property = serialized.FindProperty("m_AlwaysIncludedShaders");
                if (property == null || !property.isArray) continue;

                for (int i = 0; i < property.arraySize; i++)
                {
                    var shader = property.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                    if (shader != null) list.Add(shader);
                }

                break;
            }

            return list;
        }

        [Test]
        public void EveryRuntimeShader_IsAlwaysIncluded()
        {
            var included = AlwaysIncluded();
            Assert.IsNotEmpty(included, "could not read Always Included Shaders from Graphics Settings");

            foreach (string name in RuntimeShaders.MustSurvive)
            {
                var shader = Shader.Find(name);
                Assert.IsNotNull(shader, name + " does not resolve; the player would fall back");

                Assert.IsTrue(included.Contains(shader),
                    name + " is looked up by name but is not in Always Included Shaders, " +
                    "so a build strips it and the game renders the fallback instead. " +
                    "Run DesalEra/Repair Always Included Shaders.");
            }
        }

        [Test]
        public void TheProjectsOwnShader_IsReachableByName()
        {
            // The list is only worth anything if the name still matches the file. A renamed
            // shader would otherwise leave the constant pointing at nothing and the repair
            // step would report a shader that does not exist.
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Shader>(
                    RuntimeShaders.AssetPathFor(RuntimeShaders.SeaWater)),
                "SeaWater shader missing from " + RuntimeShaders.AssetPathFor(RuntimeShaders.SeaWater));
        }
    }
}
