using System.Collections.Generic;
using UnityEngine;

namespace DesalEra.Unity
{
    /// <summary>
    /// Procedural meshes for placeable members. Profiles are piece-specific so a pontoon
    /// reads as a drum and a deck as a plank, while UVs stay in metres so PBR maps tile
    /// at a constant grain size.
    /// </summary>
    public static class MeshFactory
    {
        public enum Profile
        {
            Beam,
            Plank,
            Post,
            Drum,
            Angle,
            Still
        }

        public static Profile ProfileFor(string pieceName)
        {
            switch (pieceName)
            {
                case "Deck": return Profile.Plank;
                case "Column": return Profile.Post;
                case "Pontoon": return Profile.Drum;
                case "Brace": return Profile.Angle;
                case "Still": return Profile.Still;
                default: return Profile.Beam;
            }
        }

        public static Mesh ForProfile(Profile profile, float thickness, float length, float uvMetresPerTile,
                                      string name = null)
        {
            float inv = 1f / Mathf.Max(uvMetresPerTile, 0.01f);
            switch (profile)
            {
                case Profile.Plank:
                    return Plank(thickness * 2.1f, thickness * 0.38f, length, inv, name ?? "Plank");
                case Profile.Post:
                    return Cylinder(thickness * 0.5f, length, 14, inv, name ?? "Post", rim: true);
                case Profile.Drum:
                    return Drum(thickness * 0.5f, length, 18, inv, name ?? "Drum");
                case Profile.Angle:
                    return AngleIron(thickness * 0.7f, thickness * 0.14f, length, inv, name ?? "Angle");
                case Profile.Still:
                    return StillVessel(thickness * 0.55f, length, inv, name ?? "Still");
                default:
                    return BeveledBeam(thickness, thickness, length, inv, name ?? "Beam");
            }
        }

        /// <summary>Legacy box beam — kept for callers that still want a plain prism.</summary>
        public static Mesh Beam(Vector3 size, float uvMetresPerTile, string name = "Beam")
        {
            float inv = 1f / Mathf.Max(uvMetresPerTile, 0.01f);
            return BeveledBeam(size.x, size.z, size.y, inv, name);
        }

        /// <summary>Square post with a small chamfer so edges catch light instead of looking CAD-hard.</summary>
        private static Mesh BeveledBeam(float width, float depth, float length, float inv, string name)
        {
            float hw = width * 0.5f;
            float hd = depth * 0.5f;
            float bevel = Mathf.Min(hw, hd) * 0.32f;
            var profile = new[]
            {
                new Vector2(-(hw - bevel), -hd),
                new Vector2(hw - bevel, -hd),
                new Vector2(hw, -(hd - bevel)),
                new Vector2(hw, hd - bevel),
                new Vector2(hw - bevel, hd),
                new Vector2(-(hw - bevel), hd),
                new Vector2(-hw, hd - bevel),
                new Vector2(-hw, -(hd - bevel))
            };
            return Extrude(profile, length, inv, name, caps: true);
        }

        /// <summary>
        /// Flat panel for roofs: local X = width, local Y = length, local Z = thickness,
        /// the same axes as a deck plank so the same orientation code applies.
        /// </summary>
        public static Mesh Slab(float width, float thickness, float length, float uvMetresPerTile)
        {
            return Plank(width, thickness, length, 1f / Mathf.Max(uvMetresPerTile, 0.01f), "RoofSlab");
        }

        /// <summary>Wide, thin deck board — reads as lumber rather than a square stick.</summary>
        private static Mesh Plank(float width, float depth, float length, float inv, string name)
        {
            float hw = width * 0.5f;
            float hd = depth * 0.5f;
            float bevel = Mathf.Min(hw, hd) * 0.40f;
            var profile = new[]
            {
                new Vector2(-(hw - bevel), -hd),
                new Vector2(hw - bevel, -hd),
                new Vector2(hw, -(hd - bevel)),
                new Vector2(hw, hd - bevel),
                new Vector2(hw - bevel, hd),
                new Vector2(-(hw - bevel), hd),
                new Vector2(-hw, hd - bevel),
                new Vector2(-hw, -(hd - bevel))
            };
            return Extrude(profile, length, inv, name, caps: true);
        }

        /// <summary>L-section brace: two thin legs, the classic angle-iron silhouette.</summary>
        private static Mesh AngleIron(float leg, float stock, float length, float inv, string name)
        {
            float t = stock;
            float L = leg;
            // Outer L, centred roughly on the angle apex.
            var profile = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(L, 0f),
                new Vector2(L, t),
                new Vector2(t, t),
                new Vector2(t, L),
                new Vector2(0f, L)
            };
            // Shift so the member axis sits near the centroid of the L.
            Vector2 shift = new Vector2(-L * 0.28f, -L * 0.28f);
            for (int i = 0; i < profile.Length; i++) profile[i] += shift;
            return Extrude(profile, length, inv, name, caps: true);
        }

        private static Mesh Cylinder(float radius, float length, int sides, float inv, string name, bool rim)
        {
            var profile = Circle(radius, sides);
            Mesh mesh = Extrude(profile, length, inv, name, caps: true);
            if (!rim) return mesh;

            // Thin end rings so a timber post reads as cut lumber, not a smooth pipe.
            Mesh bandTop = Extrude(Circle(radius * 1.06f, sides), length * 0.04f, inv, name + "_RimT", caps: true);
            Mesh bandBot = Extrude(Circle(radius * 1.06f, sides), length * 0.04f, inv, name + "_RimB", caps: true);
            return Combine(mesh,
                           Translate(bandTop, new Vector3(0f, length * 0.48f, 0f)),
                           Translate(bandBot, new Vector3(0f, -length * 0.48f, 0f)));
        }

        /// <summary>Salvaged fuel drum: fat cylinder with two hoop ridges and domed end caps.</summary>
        private static Mesh Drum(float radius, float length, int sides, float inv, string name)
        {
            Mesh body = Extrude(Circle(radius, sides), length * 0.92f, inv, name + "_Body", caps: true);
            Mesh hoopA = Extrude(Circle(radius * 1.05f, sides), length * 0.04f, inv, name + "_HoopA", caps: true);
            Mesh hoopB = Extrude(Circle(radius * 1.05f, sides), length * 0.04f, inv, name + "_HoopB", caps: true);
            Mesh lid = Extrude(Circle(radius * 0.98f, sides), length * 0.06f, inv, name + "_Lid", caps: true);
            return Combine(body,
                           Translate(hoopA, new Vector3(0f, length * 0.28f, 0f)),
                           Translate(hoopB, new Vector3(0f, -length * 0.28f, 0f)),
                           Translate(lid, new Vector3(0f, length * 0.47f, 0f)));
        }

        /// <summary>Desalination still: tank body, neck, and a small side pipe.</summary>
        private static Mesh StillVessel(float radius, float length, float inv, string name)
        {
            float bodyLen = length * 0.62f;
            float neckLen = length * 0.28f;
            Mesh body = Extrude(Circle(radius, 14), bodyLen, inv, name + "_Tank", caps: true);
            Mesh neck = Extrude(Circle(radius * 0.38f, 10), neckLen, inv, name + "_Neck", caps: true);
            Mesh flange = Extrude(Circle(radius * 0.55f, 12), length * 0.05f, inv, name + "_Flange", caps: true);
            Mesh spout = Extrude(Circle(radius * 0.16f, 8), radius * 0.9f, inv, name + "_Spout", caps: true);

            // Spout runs sideways; Extrude is along Y, so rotate after.
            spout = RotateX(spout, 90f);
            return Combine(Translate(body, new Vector3(0f, -length * 0.12f, 0f)),
                           Translate(neck, new Vector3(0f, length * 0.28f, 0f)),
                           Translate(flange, new Vector3(0f, length * 0.12f, 0f)),
                           Translate(spout, new Vector3(radius * 0.85f, -length * 0.05f, 0f)));
        }

        private static Vector2[] Circle(float radius, int sides)
        {
            var pts = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            }
            return pts;
        }

        /// <summary>
        /// Extrudes a closed XZ profile along Y, centred on the origin. Side UVs use
        /// perimeter arc length × axial metres so the same texture density as Beam.
        /// </summary>
        private static Mesh Extrude(Vector2[] profile, float length, float inv, string name, bool caps)
        {
            int n = profile.Length;

            float signedArea = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = profile[i], q = profile[(i + 1) % n];
                signedArea += p.x * q.y - q.x * p.y;
            }
            if (signedArea < 0f)
            {
                profile = (Vector2[])profile.Clone();
                System.Array.Reverse(profile);
            }
            float half = length * 0.5f;
            var vertices = new List<Vector3>(n * 4 + n * 2);
            var uvs = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(n * 6 + n * 3);

            float[] cum = new float[n + 1];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = profile[i];
                Vector2 b = profile[(i + 1) % n];
                cum[i + 1] = cum[i] + Vector2.Distance(a, b);
            }

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 b0 = new Vector3(profile[i].x, -half, profile[i].y);
                Vector3 b1 = new Vector3(profile[j].x, -half, profile[j].y);
                Vector3 t0 = new Vector3(profile[i].x, half, profile[i].y);
                Vector3 t1 = new Vector3(profile[j].x, half, profile[j].y);

                int v = vertices.Count;
                vertices.Add(b0); vertices.Add(b1); vertices.Add(t1); vertices.Add(t0);

                float u0 = cum[i] * inv;
                float u1 = cum[i + 1] * inv;
                float v0 = 0f;
                float v1 = length * inv;
                uvs.Add(new Vector2(u0, v0));
                uvs.Add(new Vector2(u1, v0));
                uvs.Add(new Vector2(u1, v1));
                uvs.Add(new Vector2(u0, v1));

                // Profiles run counter-clockwise in (x, z). Unity's front face is clockwise
                // seen from outside, so the quad goes b0 → t1 → b1; the other order puts
                // every normal inside the member and lights it from within.
                triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 1);
                triangles.Add(v); triangles.Add(v + 3); triangles.Add(v + 2);
            }

            if (caps)
            {
                AddCap(vertices, uvs, triangles, profile, -half, inv, flip: false);
                AddCap(vertices, uvs, triangles, profile, half, inv, flip: true);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCap(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
                                   Vector2[] profile, float y, float inv, bool flip)
        {
            int n = profile.Length;
            int centre = vertices.Count;
            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < n; i++) centroid += profile[i];
            centroid /= n;

            vertices.Add(new Vector3(centroid.x, y, centroid.y));
            uvs.Add(new Vector2(centroid.x * inv, centroid.y * inv));

            int ring = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                vertices.Add(new Vector3(profile[i].x, y, profile[i].y));
                uvs.Add(new Vector2(profile[i].x * inv, profile[i].y * inv));
            }

            for (int i = 0; i < n; i++)
            {
                int a = ring + i;
                int b = ring + (i + 1) % n;
                if (flip)
                {
                    triangles.Add(centre); triangles.Add(b); triangles.Add(a);
                }
                else
                {
                    triangles.Add(centre); triangles.Add(a); triangles.Add(b);
                }
            }
        }

        /// <summary>Mutates in place — caller must own the mesh.</summary>
        private static Mesh Translate(Mesh mesh, Vector3 offset)
        {
            Vector3[] verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] += offset;
            mesh.vertices = verts;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Mutates in place — caller must own the mesh.</summary>
        private static Mesh RotateX(Mesh mesh, float degrees)
        {
            Quaternion q = Quaternion.Euler(degrees, 0f, 0f);
            Vector3[] verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] = q * verts[i];
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Discard(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);
        }

        private static Mesh Combine(params Mesh[] parts)
        {
            var combine = new CombineInstance[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                combine[i].mesh = parts[i];
                combine[i].transform = Matrix4x4.identity;
            }

            var mesh = new Mesh { name = parts[0].name };
            mesh.CombineMeshes(combine, mergeSubMeshes: true, useMatrices: false);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            foreach (Mesh part in parts) Discard(part);
            return mesh;
        }

        /// <summary>
        /// Dense XZ grid for Gerstner displacement. A single quad cannot carry swell —
        /// host oceans tessellate or grid the surface so wavelengths have vertices to move.
        ///
        /// When <paramref name="innerHoleM"/> &gt; 0, quads whose centre lies inside that
        /// square are skipped — used for the far sea ring so it does not double-draw
        /// over the near field (transparent Z-fight reads as a checkerboard).
        /// </summary>
        public static Mesh WaterPlane(float sizeM, string name = "Water", int segments = 128,
                                      float innerHoleM = 0f)
        {
            segments = Mathf.Clamp(segments, 8, 512);
            int vertsPerSide = segments + 1;
            int vertCount = vertsPerSide * vertsPerSide;
            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];

            float h = sizeM * 0.5f;
            float step = sizeM / segments;
            float hole = Mathf.Max(0f, innerHoleM) * 0.5f;
            // Circular clip — a square disc shows a straight join line against the sky.
            float radius = h;
            float radiusSq = radius * radius;

            for (int z = 0; z < vertsPerSide; z++)
            {
                for (int x = 0; x < vertsPerSide; x++)
                {
                    int i = z * vertsPerSide + x;
                    float px = -h + x * step;
                    float pz = -h + z * step;
                    vertices[i] = new Vector3(px, 0f, pz);
                    uvs[i] = new Vector2(x / (float)segments * sizeM, z / (float)segments * sizeM);
                }
            }

            var triangles = new List<int>(segments * segments * 6);
            for (int z = 0; z < segments; z++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int i = z * vertsPerSide + x;
                    float cx = -h + (x + 0.5f) * step;
                    float cz = -h + (z + 0.5f) * step;
                    if (cx * cx + cz * cz > radiusSq) continue;
                    if (hole > 0f && Mathf.Abs(cx) < hole && Mathf.Abs(cz) < hole) continue;

                    triangles.Add(i);
                    triangles.Add(i + vertsPerSide);
                    triangles.Add(i + 1);
                    triangles.Add(i + 1);
                    triangles.Add(i + vertsPerSide);
                    triangles.Add(i + vertsPerSide + 1);
                }
            }

            var mesh = new Mesh { name = name };
            if (vertCount > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Pickup(string name = "Pickup")
        {
            var mesh = new Mesh { name = name };
            const int segments = 8;
            const int rings = 4;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

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
