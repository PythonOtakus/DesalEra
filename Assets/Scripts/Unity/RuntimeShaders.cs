using System.Collections.Generic;

namespace DesalEra.Unity
{
    /// <summary>
    /// Shader names the runtime resolves by name, gathered so a build can be made to keep
    /// them.
    ///
    /// Shader.Find only sees what the player actually shipped. The editor has every
    /// shader loaded, so a shader reached only through Shader.Find works in Play mode and
    /// is gone in a build, where the lookup returns null and the caller quietly falls
    /// through to something else. That is how the sea lost its Gerstner shader and the sky
    /// lost its panorama and both came out flat coloured: no exception, no magenta, no
    /// warning, just a different picture.
    ///
    /// Referencing these from the scene would fix it too, but the scene is generated and
    /// holds a single entry point, so the list belongs next to the lookups instead.
    /// </summary>
    public static class RuntimeShaders
    {
        public const string SeaWater = "DesalEra/SeaWater";
        public const string SkyboxPanoramic = "Skybox/Panoramic";

        /// <summary>
        /// Every shader name the runtime depends on. Shaders reached through a fallback
        /// chain are not listed: those resolve to a built-in that Unity ships by default,
        /// and a fallback that renders plainly is a better outcome than a missing sea.
        /// </summary>
        public static readonly string[] MustSurvive =
        {
            SeaWater,
            SkyboxPanoramic,
        };

        /// <summary>
        /// Asset path of a shader this project owns, or null for a built-in. Built-ins have
        /// no path and enter the build through the always-included list by reference.
        /// </summary>
        public static string AssetPathFor(string shaderName)
        {
            return shaderName == SeaWater ? "Assets/Shaders/SeaWater.shader" : null;
        }
    }
}
