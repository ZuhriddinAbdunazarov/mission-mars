using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    /// <summary>Kid-friendly icons drawn in code (white shapes, tinted by the Image colour).</summary>
    public static class Icons
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static bool Circle(Vector2 p, float x, float y, float r) => (p - new Vector2(x, y)).sqrMagnitude <= r * r;
        static bool Box(Vector2 p, float x, float y, float w, float h, float r = 0)
        {
            float dx = Mathf.Abs(p.x - x) - (w - r), dy = Mathf.Abs(p.y - y) - (h - r);
            return new Vector2(Mathf.Max(dx, 0), Mathf.Max(dy, 0)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0) <= r;
        }
        static bool Seg(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            var pa = p - a; var ba = b - a; float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude);
            return (pa - ba * h).magnitude <= r;
        }
        static bool Poly(Vector2 p, params float[] xy)
        {
            bool inside = false; int n = xy.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                if ((yi > p.y) != (yj > p.y) && p.x < (xj - xi) * (p.y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }
        static bool Ring(Vector2 p, float x, float y, float r, float w) { float d = (p - new Vector2(x, y)).magnitude; return d <= r && d >= r - w; }

        static float[] StarPts(int n, float ro, float ri, float rot = 90)
        {
            var a = new float[n * 4];
            for (int i = 0; i < n * 2; i++)
            {
                float ang = (rot + i * 180f / n) * Mathf.Deg2Rad; float r = i % 2 == 0 ? ro : ri;
                a[i * 2] = Mathf.Cos(ang) * r; a[i * 2 + 1] = Mathf.Sin(ang) * r;
            }
            return a;
        }

        static readonly Dictionary<string, Func<Vector2, bool>> shapes = new Dictionary<string, Func<Vector2, bool>>
        {
            ["power"] = p => Poly(p, 0.2f, 0.95f, -0.55f, -0.08f, -0.05f, -0.08f, -0.22f, -0.95f, 0.58f, 0.12f, 0.06f, 0.12f),
            ["air"] = p => Circle(p, -0.1f, -0.18f, 0.6f) && !Circle(p, -0.3f, 0.05f, 0.14f) || Circle(p, 0.55f, 0.5f, 0.3f) || Circle(p, -0.55f, 0.68f, 0.18f),
            ["food"] = p => { float c = 0.866f, sn = 0.5f; float x = p.x * c + p.y * sn, y = -p.x * sn + p.y * c; bool body = (x * x) / (0.86f * 0.86f) + (y * y) / (0.56f * 0.56f) <= 1 || Circle(p, -0.38f, -0.3f, 0.42f) || Circle(p, 0.42f, 0.28f, 0.4f); bool eye = Circle(p, -0.3f, -0.12f, 0.07f) || Circle(p, 0.18f, 0.12f, 0.07f) || Circle(p, 0.5f, 0.42f, 0.06f) || Circle(p, -0.05f, -0.42f, 0.06f); return body && !eye; }, // a potato (all the food in the game is Mars potatoes)
            ["health"] = p => { float x = p.x * 1.15f, y = p.y * 1.15f + 0.15f; float a = x * x + y * y - 1; return a * a * a - x * x * y * y * y <= 0; },
            ["shield"] = p => Poly(p, -0.75f, 0.85f, 0, 0.98f, 0.75f, 0.85f, 0.72f, 0.05f, 0, -0.95f, -0.72f, 0.05f) && !(Poly(p, -0.5f, 0.68f, 0, 0.78f, 0.5f, 0.68f, 0.48f, 0.08f, 0, -0.62f, -0.48f, 0.08f) && p.x > 0),
            ["science"] = p => Poly(p, StarPts(5, 0.98f, 0.42f)),
            ["battery"] = p => (Box(p, 0, -0.08f, 0.5f, 0.85f, 0.12f) && !Box(p, 0, -0.08f, 0.34f, 0.69f, 0.05f)) || Box(p, 0, 0.86f, 0.2f, 0.1f) || Box(p, 0, -0.38f, 0.26f, 0.32f, 0.04f),
            ["sun"] = p => Circle(p, 0, 0, 0.45f) || RaysHit(p, 8, 0.6f, 0.95f, 0.09f),
            ["flare"] = p => Circle(p, 0, 0, 0.38f) || Poly(p, StarPts(12, 0.98f, 0.5f, 0)),
            ["rocket"] = p => (Circle(p, 0, 0.1f, 0.38f) && p.y > -0.4f && Mathf.Abs(p.x) < 0.38f - Mathf.Max(0, p.y - 0.1f) * 0.6f) || Poly(p, -0.32f, -0.2f, -0.62f, -0.75f, -0.3f, -0.55f) || Poly(p, 0.32f, -0.2f, 0.62f, -0.75f, 0.3f, -0.55f) || Poly(p, -0.18f, -0.5f, 0.18f, -0.5f, 0, -0.98f),
            ["warn"] = p => Poly(p, 0, 0.95f, 0.98f, -0.8f, -0.98f, -0.8f) && !Box(p, 0, 0.12f, 0.08f, 0.38f, 0.06f) && !Circle(p, 0, -0.52f, 0.1f),
            ["play"] = p => Poly(p, -0.5f, 0.8f, 0.8f, 0, -0.5f, -0.8f),
            ["plus"] = p => Box(p, 0, 0, 0.8f, 0.2f, 0.1f) || Box(p, 0, 0, 0.2f, 0.8f, 0.1f),
            ["minus"] = p => Box(p, 0, 0, 0.8f, 0.2f, 0.1f),
            ["build"] = p => (Seg(p, new Vector2(-0.65f, -0.65f), new Vector2(0.35f, 0.35f), 0.16f)) || (Ring(p, 0.48f, 0.48f, 0.45f, 0.2f) && !Poly(p, 0.48f, 0.48f, 1.2f, 0.8f, 0.8f, 1.2f)),
            ["drill"] = p => Poly(p, -0.35f, 0.9f, 0.35f, 0.9f, 0.1f, -0.2f, -0.1f, -0.2f) || Poly(p, -0.12f, -0.2f, 0.12f, -0.2f, 0, -0.98f) || Box(p, 0, 0.6f, 0.55f, 0.08f),
            ["dust"] = p => Circle(p, -0.35f, 0.1f, 0.38f) || Circle(p, 0.2f, 0.25f, 0.45f) || Circle(p, 0.5f, -0.15f, 0.32f) || Box(p, 0.05f, -0.25f, 0.72f, 0.2f, 0.18f),
            ["robot"] = p => (Box(p, 0, -0.05f, 0.78f, 0.6f, 0.3f) && !Circle(p, -0.32f, 0, 0.17f) && !Circle(p, 0.32f, 0, 0.17f) && !Box(p, 0, -0.35f, 0.3f, 0.06f, 0.05f)) || Seg(p, new Vector2(0, 0.55f), new Vector2(0, 0.85f), 0.07f) || Circle(p, 0, 0.88f, 0.13f),
            ["crew"] = p => Circle(p, 0, 0.42f, 0.38f) && !Box(p, 0, 0.42f, 0.25f, 0.14f, 0.12f) || Box(p, 0, -0.55f, 0.55f, 0.38f, 0.3f),
            ["data"] = p => Ring(p, -0.5f, -0.5f, 0.55f, 0.16f) && p.x > -0.5f && p.y > -0.5f || Ring(p, -0.5f, -0.5f, 1.05f, 0.16f) && p.x > -0.5f && p.y > -0.5f || Circle(p, -0.4f, -0.4f, 0.2f),
            ["water"] = p => Circle(p, 0, -0.3f, 0.55f) || Poly(p, -0.48f, -0.05f, 0.48f, -0.05f, 0, 0.95f),
            ["check"] = p => Seg(p, new Vector2(-0.6f, 0), new Vector2(-0.15f, -0.5f), 0.16f) || Seg(p, new Vector2(-0.15f, -0.5f), new Vector2(0.7f, 0.55f), 0.16f),
            ["close"] = p => Seg(p, new Vector2(-0.6f, -0.6f), new Vector2(0.6f, 0.6f), 0.15f) || Seg(p, new Vector2(-0.6f, 0.6f), new Vector2(0.6f, -0.6f), 0.15f),
            ["clock"] = p => Ring(p, 0, 0, 0.9f, 0.16f) || Seg(p, Vector2.zero, new Vector2(0, 0.55f), 0.1f) || Seg(p, Vector2.zero, new Vector2(0.4f, 0), 0.1f),
            ["dot"] = p => Circle(p, 0, 0, 0.9f),
        };

        static bool RaysHit(Vector2 p, int n, float r0, float r1, float w)
        {
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (Seg(p, d * r0, d * r1, w)) return true;
            }
            return false;
        }

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s) && s) return s;
            if (!shapes.TryGetValue(name, out var f)) f = shapes["dot"];
            int N = 96; const int SS = 3;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            var p = new Vector2((x + (sx + 0.5f) / SS) / N * 2.1f - 1.05f, (y + (sy + 0.5f) / SS) / N * 2.1f - 1.05f);
                            if (f(p)) hit++;
                        }
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(hit * 255 / (SS * SS)));
                }
            tex.SetPixels32(px); tex.Apply(true);
            s = Sprite.Create(tex, new Rect(0, 0, N, N), Vector2.one * 0.5f, 100);
            cache[name] = s; return s;
        }
    }
}
