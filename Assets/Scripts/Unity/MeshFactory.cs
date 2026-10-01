using UnityEngine;

namespace CrazyAquarium.Unity
{
    /// <summary>
    /// Procedural meshes for the greybox, so the project needs no imported model
    /// assets and therefore no scene or prefab files to carry them.
    ///
    /// UVs are laid out in world units rather than 0-to-1, which lets one texture tile
    /// correctly across members of very different sizes. A 3 m column and a 0.3 m
    /// bracket then show the same grain size instead of one looking stretched.
    /// </summary>
    public static class MeshFactory
    {
        /// <summary>
        /// A box whose UVs are scaled by <paramref name="uvMetresPerTile"/>, so the
        /// texture keeps a constant physical size regardless of the box dimensions.
        /// </summary>
        public static Mesh Box(Vector3 size, float uvMetresPerTile = 2f, string name = "Box")
        {
            var mesh = new Mesh { name = name };

            Vector3 h = size * 0.5f;
            Vector3[] corners =
            {
                new(-h.x, -h.y, -h.z), new(h.x, -h.y, -h.z),
                new(h.x, h.y, -h.z), new(-h.x, h.y, -h.z),
                new(-h.x, -h.y, h.z), new(h.x, -h.y, h.z),
                new(h.x, h.y, h.z), new(-h.x, h.y, h.z)
            };

            int[] triangles =
            {
                0, 2, 1, 0, 3, 2, // -Z
                4, 5, 6, 4, 6, 7, // +Z
                0, 1, 5, 0, 5, 4, // -Y
                3, 7, 6, 3, 6, 2, // +Y
                0, 4, 7, 0, 7, 3, // -X
                1, 2, 6, 1, 6, 5  // +X
            };

            mesh.vertices = corners;
            mesh.triangles = triangles;

            // UVs in metres divided by the tile size, so texture density is uniform.
            var uv = new Vector2[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                uv[i] = new Vector2(corners[i].x, corners[i].y) / Mathf.Max(uvMetresPerTile, 0.01f);
            }
            mesh.uv = uv;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A cube scaled so its long axis runs from -0.5 to +0.5 along Y, ready to be
        /// oriented between two points. This is the workhorse for every structural
        /// member: position at the midpoint, rotate onto the axis, scale to length.
        /// </summary>
        public static Mesh UnitColumn(string name = "Column")
        {
            var mesh = new Mesh { name = name };
            float h = 0.5f;

            Vector3[] v =
            {
                new(-h, -h, -h), new(h, -h, -h), new(h, h, -h), new(-h, h, -h),
                new(-h, -h, h), new(h, -h, h), new(h, h, h), new(-h, h, h)
            };
            int[] t =
            {
                0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2,
                0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5
            };

            // Side faces get the real dimensions so the texture does not stretch when
            // the column is scaled to 3 m; cap faces get a square in metres.
            Vector2[] uv = new Vector2[8];
            uv[0] = new Vector2(0f, 0f);
            uv[1] = new Vector2(1f, 0f);
            uv[2] = new Vector2(1f, 1f);
            uv[3] = new Vector2(0f, 1f);
            uv[4] = new Vector2(0f, 0f);
            uv[5] = new Vector2(1f, 0f);
            uv[6] = new Vector2(1f, 1f);
            uv[7] = new Vector2(0f, 1f);

            mesh.vertices = v;
            mesh.triangles = t;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A rectangular water plane at the given height. Kept as a simple quad
        /// because this greybox is about reading structure, not simulating water: a
        /// real ocean is a large enough problem to deserve its own task.
        /// </summary>
        public static Mesh WaterPlane(float sizeM, string name = "Water")
        {
            var mesh = new Mesh { name = name };
            float h = sizeM * 0.5f;

            mesh.vertices = new[]
            {
                new Vector3(-h, 0f, -h), new(h, 0f, -h), new(h, 0f, h), new(-h, 0f, h)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(sizeM, 0f), new Vector2(sizeM, sizeM), new Vector2(0f, sizeM) };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A capsule-ish blob for salvage pickups: cheap, reads at distance.</summary>
        public static Mesh Pickup(string name = "Pickup")
        {
            var mesh = new Mesh { name = name };
            const int segments = 8;
            const int rings = 4;

            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();

            vertices.Add(new Vector3(0f, 0.5f, 0f));

            for (int r = 1; r <= rings; r++)
            {
                float phi = r / (float)rings * Mathf.PI;
                for (int s = 0; s < segments; s++)
                {
                    float theta = s / (float)segments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta) * 0.5f,
                        Mathf.Cos(phi) * 0.5f,
                        Mathf.Sin(phi) * Mathf.Sin(theta) * 0.5f));
                }
            }

            for (int s = 0; s < segments; s++)
            {
                triangles.Add(0);
                triangles.Add(1 + (s + 1) % segments);
                triangles.Add(1 + s);
            }

            for (int r = 0; r < rings - 1; r++)
            {
                int baseA = 1 + r * segments;
                int baseB = baseA + segments;
                for (int s = 0; s < segments; s++)
                {
                    int next = (s + 1) % segments;
                    triangles.Add(baseA + s);
                    triangles.Add(baseA + next);
                    triangles.Add(baseB + next);
                    triangles.Add(baseA + s);
                    triangles.Add(baseB + next);
                    triangles.Add(baseB + s);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
