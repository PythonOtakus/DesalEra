using UnityEngine;

namespace DesalEra.Unity
{
    /// <summary>
    /// CPU-side twin of the sea shader's height field, so pickups and other props can
    /// ride the same swell the GPU draws. Kept deliberately simple: long Gerstner only,
    /// no high-frequency chop (that lives in fragment normals and must not move verts).
    /// </summary>
    public static class SeaWave
    {
        public static float Amplitude = 1.05f;
        public static float Speed = 1f;
        public static float TimeOffset;

        // dir.xy, steepness, wavelength — matches BuildWaterMaterial defaults.
        private static readonly Vector4[] Waves =
        {
            new Vector4(0.95f, 0.31f, 0.16f, 38f),
            new Vector4(-0.55f, 0.84f, 0.13f, 24f),
            new Vector4(0.62f, -0.78f, 0.10f, 15f),
            new Vector4(-0.88f, -0.47f, 0.07f, 9.5f)
        };

        private static readonly float[] Phases = { 0f, 1.618f, 2.718f, 0.577f };

        public static float Height(Vector2 xz, float time)
        {
            float y = 0f;
            for (int i = 0; i < Waves.Length; i++)
            {
                Vector4 w = Waves[i];
                Vector2 d = new Vector2(w.x, w.y).normalized;
                float steep = Mathf.Clamp01(w.z);
                float lambda = Mathf.Max(w.w, 0.1f);
                float k = Mathf.PI * 2f / lambda;
                float c = Mathf.Sqrt(9.8f / k);
                float f = k * (Vector2.Dot(d, xz) - c * time * Speed) + Phases[i];
                float a = steep / k * Amplitude;
                y += a * Mathf.Sin(f);
            }

            return y;
        }

        public static float HeightAt(Vector3 worldPos)
        {
            return Height(new Vector2(worldPos.x, worldPos.z), Time.time + TimeOffset);
        }

        /// <summary>Unit normal from the swell height field — pickups tip with the surface.</summary>
        public static Vector3 NormalAt(Vector3 worldPos, float sampleM = 0.45f)
        {
            Vector2 xz = new Vector2(worldPos.x, worldPos.z);
            float t = Time.time + TimeOffset;
            float h = Height(xz, t);
            float hx = Height(xz + new Vector2(sampleM, 0f), t);
            float hz = Height(xz + new Vector2(0f, sampleM), t);
            return new Vector3(h - hx, sampleM, h - hz).normalized;
        }

        public static void ApplyToMaterial(Material material)
        {
            if (material == null) return;
            if (material.HasProperty("_Amplitude")) material.SetFloat("_Amplitude", Amplitude);
            if (material.HasProperty("_Speed")) material.SetFloat("_Speed", Speed);
            if (material.HasProperty("_Wave0")) material.SetVector("_Wave0", Waves[0]);
            if (material.HasProperty("_Wave1")) material.SetVector("_Wave1", Waves[1]);
            if (material.HasProperty("_Wave2")) material.SetVector("_Wave2", Waves[2]);
            if (material.HasProperty("_Wave3")) material.SetVector("_Wave3", Waves[3]);
        }
    }
}
