using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    public static class ProcMesh
    {
        /// <summary>Noise-displaced icosphere rock with a flattened base.</summary>
        public static Mesh Rock(int seed, int subdiv = 2)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] v0 = { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            int[] f0 = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            foreach (var v in v0) verts.Add(v.normalized); tris.AddRange(f0);
            for (int s = 0; s < subdiv; s++)
            {
                var mid = new Dictionary<long, int>(); var nt = new List<int>();
                int Mid(int a, int b)
                {
                    long k = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                    if (mid.TryGetValue(k, out int i)) return i;
                    verts.Add(((verts[a] + verts[b]) * 0.5f).normalized); mid[k] = verts.Count - 1; return verts.Count - 1;
                }
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = nt;
            }
            var rng = new System.Random(seed);
            float ox = (float)rng.NextDouble() * 100, oy = (float)rng.NextDouble() * 100;
            float sx = 0.8f + (float)rng.NextDouble() * 0.6f, sz = 0.8f + (float)rng.NextDouble() * 0.6f, sy = 0.5f + (float)rng.NextDouble() * 0.35f;
            var uvs = new Vector2[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                float n = Mathf.PerlinNoise(v.x * 1.6f + ox, v.z * 1.6f + oy) * 0.45f + Mathf.PerlinNoise(v.y * 3.1f + oy, v.x * 3.1f + ox) * 0.2f;
                // faceted look: quantise slightly
                v *= 0.75f + n;
                v = new Vector3(v.x * sx, v.y * sy, v.z * sz);
                if (v.y < -0.15f) v.y = -0.15f + (v.y + 0.15f) * 0.2f;
                verts[i] = v;
                uvs[i] = new Vector2(Mathf.Atan2(v.x, v.z) / Mathf.PI * 1.5f, v.y * 1.5f);
            }
            var m = new Mesh { name = "Rock" + seed };
            m.SetVertices(verts); m.SetTriangles(tris, 0); m.uv = uvs;
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Hemisphere dome made of panels (UV sphere top half), open at the bottom.</summary>
        public static Mesh Dome(int seg = 24, int rings = 10, float open = 0f)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float phi = r / (float)rings * Mathf.PI * 0.5f;
                for (int s = 0; s <= seg; s++)
                {
                    float th = s / (float)seg * Mathf.PI * 2;
                    v.Add(new Vector3(Mathf.Cos(phi) * Mathf.Cos(th), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(th)));
                    uv.Add(new Vector2(s / (float)seg * 4, r / (float)rings * 2));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    // slit opening for the telescope
                    if (open > 0 && s < seg * open && r > 2) continue;
                    int a = r * (seg + 1) + s, b = a + seg + 1;
                    tr.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            var m = new Mesh { name = "Dome" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
            m.RecalculateNormals(); m.RecalculateTangents();
            return m;
        }

        static Mesh _sphere;
        /// <summary>High-resolution UV sphere (radius 0.5, equirect UVs like Unity's primitive) for planets.</summary>
        public static Mesh HiSphere
        {
            get
            {
                if (_sphere) return _sphere;
                int seg = 160, rings = 80;
                var v = new Vector3[(seg + 1) * (rings + 1)]; var n = new Vector3[v.Length]; var uv = new Vector2[v.Length];
                for (int r = 0; r <= rings; r++)
                    for (int s = 0; s <= seg; s++)
                    {
                        float lat = Mathf.PI * (r / (float)rings - 0.5f), lon = 2 * Mathf.PI * s / seg;
                        var d = new Vector3(-Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), -Mathf.Cos(lat) * Mathf.Sin(lon));
                        int i = r * (seg + 1) + s; v[i] = d * 0.5f; n[i] = d; uv[i] = new Vector2(s / (float)seg, r / (float)rings);
                    }
                var tr = new int[seg * rings * 6]; int k = 0;
                for (int r = 0; r < rings; r++)
                    for (int s = 0; s < seg; s++)
                    {
                        int a = r * (seg + 1) + s, b = a + seg + 1;
                        tr[k++] = a; tr[k++] = b; tr[k++] = a + 1; tr[k++] = a + 1; tr[k++] = b; tr[k++] = b + 1;
                    }
                _sphere = new Mesh { name = "HiSphere", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                _sphere.vertices = v; _sphere.normals = n; _sphere.uv = uv; _sphere.triangles = tr; _sphere.RecalculateBounds();
                return _sphere;
            }
        }

        public static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 lpos, Vector3 lscale, Material mat, Vector3? euler = null, bool collider = true)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = name;
            if (parent) g.transform.SetParent(parent, false);
            g.transform.localPosition = lpos; g.transform.localScale = lscale;
            if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(g.GetComponent<Collider>());
            return g;
        }

        public static GameObject MeshObj(string name, Mesh mesh, Material mat, Transform parent = null)
        {
            var g = new GameObject(name);
            if (parent) g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }
    }
}
