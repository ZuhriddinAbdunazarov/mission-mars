using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    /// <summary>Procedural Jezero-like crater basin: dunes, ridges, impact craters and a huge rim wall
    /// that doubles as the natural map boundary.</summary>
    public static class MarsTerrain
    {
        public const float Size = 900f;
        public const float MaxH = 160f;
        public const float Base = 22f;
        public static Terrain T;

        public struct Crater { public Vector2 c; public float r, depth, rim; }
        public struct Flat { public Vector2 c; public float r, fall; public float h; }

        public static readonly List<Crater> Craters = new List<Crater>();
        public static readonly List<Flat> Flats = new List<Flat>();

        static float Fbm(float x, float y, int oct)
        {
            float a = 0.5f, f = 1, s = 0;
            for (int i = 0; i < oct; i++) { s += a * (Mathf.PerlinNoise(x * f + 31.7f * i, y * f + 17.3f * i) * 2 - 1); f *= 2.03f; a *= 0.5f; }
            return s;
        }
        static float Ridged(float x, float y, int oct)
        {
            float a = 0.5f, f = 1, s = 0;
            for (int i = 0; i < oct; i++) { float n = 1 - Mathf.Abs(Mathf.PerlinNoise(x * f + 11.1f * i, y * f + 5.3f * i) * 2 - 1); s += a * n * n; f *= 2.1f; a *= 0.5f; }
            return s;
        }

        static void Layout()
        {
            Craters.Clear(); Flats.Clear();
            var rng = new System.Random(42);
            // named craters near the parts
            Craters.Add(new Crater { c = new Vector2(-122, 108), r = 30, depth = 7, rim = 3.2f });
            Craters.Add(new Crater { c = new Vector2(118, -132), r = 26, depth = 6, rim = 3f });
            Craters.Add(new Crater { c = new Vector2(-150, -10), r = 16, depth = 4, rim = 2 });
            Craters.Add(new Crater { c = new Vector2(95, 210), r = 22, depth = 5, rim = 2.5f });
            Craters.Add(new Crater { c = new Vector2(-40, -190), r = 40, depth = 8, rim = 4 });
            for (int i = 0; i < 26; i++)
            {
                var c = new Vector2((float)rng.NextDouble() * 640 - 320, (float)rng.NextDouble() * 640 - 320);
                if (c.magnitude < 130) continue;
                float r = 4 + (float)System.Math.Pow(rng.NextDouble(), 2.2) * 26;
                Craters.Add(new Crater { c = c, r = r, depth = r * 0.22f, rim = r * 0.09f });
            }
            // the outpost sits on a wide flat plain
            Flats.Add(new Flat { c = new Vector2(0, 0), r = 70, fall = 40, h = 0 });
            Flats.Add(new Flat { c = MarsWorld.IceXZ[MarsWorld.CaveIndex], r = 20, fall = 14, h = 0 }); // level ground under the ICE CAVE
        }

        /// <summary>Height in metres at world XZ (terrain centred on origin).</summary>
        public static float RawHeight(float x, float z)
        {
            float h = Base;
            float r = new Vector2(x, z).magnitude / (Size * 0.5f);
            // dunes & undulation
            h += Fbm(x * 0.008f, z * 0.008f, 5) * 3.2f;
            h += Ridged(x * 0.0035f + 3, z * 0.0035f + 7, 4) * 9f - 4f;
            // dune ripples in sandy lowlands
            h += Mathf.Sin((x * 0.6f + z * 0.8f) * 0.12f + Fbm(x * 0.02f, z * 0.02f, 2) * 3f) * 0.18f;
            // crater rim (Jezero wall) as world boundary
            float rimNoise = Fbm(x * 0.006f + 50, z * 0.006f, 5);
            h += Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.58f, 0.98f, r + rimNoise * 0.06f)) * (75f + rimNoise * 30f);
            h += Mathf.Clamp01(Mathf.InverseLerp(0.7f, 1f, r)) * Ridged(x * 0.012f, z * 0.012f, 4) * 18f;

            foreach (var c in Craters)
            {
                float d = Vector2.Distance(new Vector2(x, z), c.c) / c.r;
                if (d > 2.2f) continue;
                float bowl = d < 1 ? (d * d - 1) * c.depth : 0;
                float rim = c.rim * Mathf.Exp(-Mathf.Pow((d - 1) / 0.28f, 2));
                h += bowl + rim;
            }
            foreach (var f in Flats)
            {
                float d = Vector2.Distance(new Vector2(x, z), f.c);
                float k = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(f.r, f.r + f.fall, d));
                float target = FlatBase(f.c) + f.h;
                h = Mathf.Lerp(h, target, k);
            }
            var approach=(MarsWorld.WakeXZ-MarsWorld.BunkerXZ).normalized;
            var hatch=MarsWorld.BunkerXZ+approach*10.65f;
            var offset=new Vector2(x,z)-hatch;
            float lateral=Mathf.Abs(offset.x*approach.y-offset.y*approach.x);
            float forward=Vector2.Dot(offset,approach);
            float trench=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.55f,3.1f,lateral)))*
                Mathf.SmoothStep(0,1,Mathf.InverseLerp(-9,-4,forward))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,5,forward)));
            h-=.24f*trench;
            var skid = new Vector2(x, z) - MarsWorld.CrashXZ;
            float along = Mathf.InverseLerp(-16,-6,skid.y) * (1-Mathf.InverseLerp(4,8,skid.y));
            float furrow = Mathf.Exp(-skid.x*skid.x/3f);
            return h + along * (-.23f*furrow + .12f*Mathf.Exp(-Mathf.Pow((Mathf.Abs(skid.x)-2.1f)/.65f,2)));

        }

        static readonly Dictionary<Vector2, float> flatCache = new Dictionary<Vector2, float>();
        static float FlatBase(Vector2 c)
        {
            if (flatCache.TryGetValue(c, out var v)) return v;
            // height without flats, averaged
            float s = 0; int n = 0;
            for (int i = -2; i <= 2; i++) for (int j = -2; j <= 2; j++)
                {
                    float x = c.x + i * 4, z = c.y + j * 4; float h = Base;
                    h += Fbm(x * 0.008f, z * 0.008f, 5) * 3.2f + Ridged(x * 0.0035f + 3, z * 0.0035f + 7, 4) * 9f - 4f;
                    s += h; n++;
                }
            return flatCache[c] = s / n;
        }

        public static Terrain Build(Material terrainMat)
        {
            Layout(); flatCache.Clear();
            int res = 513;
            var td = new TerrainData { heightmapResolution = res };
            td.size = new Vector3(Size, MaxH, Size);
            var hs = new float[res, res];
            for (int j = 0; j < res; j++)
                for (int i = 0; i < res; i++)
                {
                    float x = i / (float)(res - 1) * Size - Size / 2, z = j / (float)(res - 1) * Size - Size / 2;
                    hs[j, i] = Mathf.Clamp01(RawHeight(x, z) / MaxH);
                }
            td.SetHeights(0, 0, hs);

            // ---- texture layers ----
            var layers = new[]
            {
                Layer("red_sand", new Vector2(11, 11), new Color(1.0f, 0.70f, 0.50f)),
                Layer("red_laterite_soil_stones", new Vector2(8, 8), new Color(1.0f, 0.74f, 0.58f)),
                Layer("rocky_terrain_02", new Vector2(14, 14), new Color(1.08f, 0.5f, 0.32f)),
                Layer("cracked_red_ground", new Vector2(9, 9), new Color(1.0f, 0.56f, 0.4f)),
            };
            td.terrainLayers = layers;
            int ar = 512; td.alphamapResolution = ar;
            var am = new float[ar, ar, 4];
            for (int j = 0; j < ar; j++)
                for (int i = 0; i < ar; i++)
                {
                    float u = i / (float)(ar - 1), v = j / (float)(ar - 1);
                    float x = u * Size - Size / 2, z = v * Size - Size / 2;
                    float steep = td.GetSteepness(u, v);
                    float hgt = td.GetInterpolatedHeight(u, v);
                    float n1 = Mathf.PerlinNoise(x * 0.02f + 9, z * 0.02f + 3);
                    float n2 = Mathf.PerlinNoise(x * 0.045f + 1, z * 0.045f + 8);
                    float rock = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(14, 30, steep)) + Mathf.Clamp01((hgt - 70) / 40f) * 0.6f;
                    float stones = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.45f, 0.7f, n1)) * 0.9f;
                    float cracked = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.62f, 0.8f, n2)) * 0.8f;
                    // crash scar
                    float crash = 0;
                    cracked = Mathf.Max(cracked, crash);
                    float sand = 1;
                    float[] w = { sand, stones, rock * 1.6f, cracked };
                    // normalise with priority to rock on slopes
                    float s = 0; for (int k = 0; k < 4; k++) { w[k] = Mathf.Max(0, w[k]); }
                    w[0] = Mathf.Max(0, 1 - Mathf.Max(w[1], Mathf.Max(w[2], w[3])));
                    for (int k = 0; k < 4; k++) s += w[k];
                    for (int k = 0; k < 4; k++) am[j, i, k] = w[k] / s;
                }
            td.SetAlphamaps(0, 0, am);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Mars Terrain";
            go.transform.position = new Vector3(-Size / 2, 0, -Size / 2);
            var t = go.GetComponent<Terrain>();
            t.materialTemplate = terrainMat;
            t.heightmapPixelError = 4;
            t.basemapDistance = 220;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            t.drawInstanced = false;
            T = t;
            return t;
        }

        static Texture2D _mask;
        /// <summary>Mask map: metallic 0, AO 1, height 0.5, smoothness 0.06 (matte soil). Without it URP uses diffuse alpha (=1) as smoothness and the ground looks wet.</summary>
        static Texture2D MaskTex
        {
            get
            {
                if (_mask) return _mask;
                _mask = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
                var c = new Color(0, 1, 0.5f, 0.06f); var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = c;
                _mask.SetPixels(px); _mask.Apply(); return _mask;
            }
        }

        static TerrainLayer Layer(string tex, Vector2 tile, Color tint)
        {
            var l = new TerrainLayer
            {
                diffuseTexture = R.Tex(tex + "_diff"),
                normalMapTexture = R.Tex(tex + "_nor"),
                tileSize = tile,
                normalScale = 0.55f,
                maskMapTexture = MaskTex,
                smoothness = 0.05f,
                metallic = 0,
                diffuseRemapMin = Vector4.zero,
                diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1),
            };
            return l;
        }

        public static float H(float x, float z) => T ? T.SampleHeight(new Vector3(x, 0, z)) + T.transform.position.y : RawHeight(x, z);
        public static Vector3 Ground(float x, float z, float up = 0) => new Vector3(x, H(x, z) + up, z);
        public static Vector3 Normal(float x, float z)
        {
            if (!T) return Vector3.up;
            var td = T.terrainData;
            return td.GetInterpolatedNormal((x + Size / 2) / Size, (z + Size / 2) / Size);
        }
    }
}
