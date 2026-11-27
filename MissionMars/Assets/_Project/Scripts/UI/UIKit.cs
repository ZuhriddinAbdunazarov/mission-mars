using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MissionMars
{
    public static class Pal
    {
        public static readonly Color Accent = new Color(1f, 0.48f, 0.22f);
        public static readonly Color Cyan = new Color(0.37f, 0.89f, 0.95f);
        public static readonly Color Bg = new Color(0.035f, 0.045f, 0.065f, 0.82f);
        public static readonly Color BgSoft = new Color(0.05f, 0.065f, 0.09f, 0.6f);
        public static readonly Color Text = new Color(0.93f, 0.95f, 0.97f);
        public static readonly Color Dim = new Color(0.60f, 0.67f, 0.74f);
        public static readonly Color Good = new Color(0.36f, 0.95f, 0.55f);
        public static readonly Color Bad = new Color(1f, 0.32f, 0.30f);
        public static readonly Color Warn = new Color(1f, 0.78f, 0.25f);
        public static readonly Color Paper = new Color(0.95f, 0.91f, 0.82f);
        public static readonly Color Ink = new Color(0.16f, 0.12f, 0.10f);
        public static readonly Color InkAccent = new Color(0.66f, 0.24f, 0.08f);
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    /// <summary>Procedurally generated UI sprites (no art files needed, crisp at any resolution).</summary>
    public static class UISprites
    {
        static Sprite _round, _roundOutline, _circle, _ring, _glow, _vignette, _diamond, _check, _hgrad, _crack, _frost, _corner, _soft, _visor, _cross;

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            return t;
        }

        static float RoundRectSDF(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
            float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        static Sprite MakeRound(float stroke)
        {
            int s = 96; float r = 22;
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = RoundRectSDF(x + 0.5f, y + 0.5f, s, s, r);
                    float a = Mathf.Clamp01(0.5f - d);
                    if (stroke > 0) a *= Mathf.Clamp01(d + stroke + 0.5f);
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, s, s), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
        }

        static Sprite Radial(int s, Func<float, float> f)
        {
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2 - 1, dy = (y + 0.5f) / s * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(f(d)) * 255));
                }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, s, s), Vector2.one * 0.5f);
        }

        public static Sprite Round => _round ? _round : (_round = MakeRound(0));
        public static Sprite RoundOutline => _roundOutline ? _roundOutline : (_roundOutline = MakeRound(2.2f));
        public static Sprite Circle => _circle ? _circle : (_circle = Radial(128, d => (1 - d) * 64f));
        public static Sprite Ring => _ring ? _ring : (_ring = Radial(128, d => Mathf.Min((1 - d) * 64f, (d - 0.82f) * 64f)));
        public static Sprite Glow => _glow ? _glow : (_glow = Radial(128, d => Mathf.Pow(Mathf.Clamp01(1 - d), 2.2f)));
        public static Sprite Soft => _soft ? _soft : (_soft = Radial(64, d => Mathf.SmoothStep(1, 0, d)));
        public static Sprite Vignette => _vignette ? _vignette : (_vignette = Radial(256, d => Mathf.SmoothStep(0, 1, (d - 0.55f) / 0.75f)));

        /// <summary>Helmet visor frame: dark curved edges.</summary>
        public static Sprite Visor
        {
            get
            {
                if (_visor) return _visor;
                int w = 512, h = 288; var t = NewTex(w, h); var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w * 2 - 1, v = (y + 0.5f) / h * 2 - 1;
                        float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 2.6f) + Mathf.Pow(Mathf.Abs(v) * 1.08f, 2.6f), 1 / 2.6f);
                        float a = Mathf.SmoothStep(0, 1, (d - 0.86f) / 0.22f);
                        px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                t.SetPixels32(px); t.Apply();
                return _visor = Sprite.Create(t, new Rect(0, 0, w, h), Vector2.one * 0.5f);
            }
        }

        public static Sprite Diamond
        {
            get
            {
                if (_diamond) return _diamond;
                int s = 64; var t = NewTex(s, s); var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = Mathf.Abs(x + 0.5f - s / 2f) + Mathf.Abs(y + 0.5f - s / 2f);
                        float a = Mathf.Clamp01(s / 2f - 2 - d);
                        float inner = Mathf.Clamp01(d - (s / 2f - 9));
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(a * Mathf.Max(inner, 0.25f) * 255));
                    }
                t.SetPixels32(px); t.Apply();
                return _diamond = Sprite.Create(t, new Rect(0, 0, s, s), Vector2.one * 0.5f);
            }
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude);
            return (pa - ba * h).magnitude;
        }

        public static Sprite Check
        {
            get
            {
                if (_check) return _check;
                int s = 64; var t = NewTex(s, s); var px = new Color32[s * s];
                Vector2 a = new Vector2(14, 32), b = new Vector2(27, 19), c = new Vector2(50, 46);
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float d = Mathf.Min(SegDist(p, a, b), SegDist(p, b, c));
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(5.5f - d) * 255));
                    }
                t.SetPixels32(px); t.Apply();
                return _check = Sprite.Create(t, new Rect(0, 0, s, s), Vector2.one * 0.5f);
            }
        }

        public static Sprite Cross
        {
            get
            {
                if (_cross) return _cross;
                int s = 64; var t = NewTex(s, s); var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float d = Mathf.Min(SegDist(p, new Vector2(16, 16), new Vector2(48, 48)), SegDist(p, new Vector2(16, 48), new Vector2(48, 16)));
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(5f - d) * 255));
                    }
                t.SetPixels32(px); t.Apply();
                return _cross = Sprite.Create(t, new Rect(0, 0, s, s), Vector2.one * 0.5f);
            }
        }

        /// <summary>L-shaped corner bracket used for sci-fi framing.</summary>
        public static Sprite Corner
        {
            get
            {
                if (_corner) return _corner;
                int s = 32; var t = NewTex(s, s); var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        bool on = (y >= s - 3 && x < s) || (x < 3 && y < s);
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(on ? 255 : 0));
                    }
                t.SetPixels32(px); t.Apply();
                return _corner = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0, 1));
            }
        }

        public static Sprite HGradient
        {
            get
            {
                if (_hgrad) return _hgrad;
                int w = 128, h = 4; var t = NewTex(w, h); var px = new Color32[w * h];
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, (byte)(255 * (1 - x / (float)w)));
                t.SetPixels32(px); t.Apply();
                return _hgrad = Sprite.Create(t, new Rect(0, 0, w, h), Vector2.one * 0.5f);
            }
        }

        /// <summary>Cracked visor overlay — random branching lines.</summary>
        public static Sprite Crack
        {
            get
            {
                if (_crack) return _crack;
                int w = 1024, h = 576; var t = NewTex(w, h);
                var px = new Color32[w * h];
                var rng = new System.Random(7);
                void Line(float x0, float y0, float ang, float len, int depth, float width)
                {
                    float x = x0, y = y0;
                    for (int i = 0; i < len; i++)
                    {
                        ang += (float)(rng.NextDouble() - 0.5) * 0.25f;
                        x += Mathf.Cos(ang); y += Mathf.Sin(ang);
                        int r = Mathf.CeilToInt(width);
                        for (int oy = -r; oy <= r; oy++)
                            for (int ox = -r; ox <= r; ox++)
                            {
                                int ix = (int)x + ox, iy = (int)y + oy;
                                if (ix < 0 || iy < 0 || ix >= w || iy >= h) continue;
                                float d = Mathf.Sqrt(ox * ox + oy * oy);
                                byte a = (byte)(Mathf.Clamp01(width + 0.5f - d) * 230);
                                if (a > px[iy * w + ix].a) px[iy * w + ix] = new Color32(255, 255, 255, a);
                            }
                        if (depth < 3 && rng.NextDouble() < 0.012) Line(x, y, ang + (float)(rng.NextDouble() - 0.5) * 2f, len * 0.5f, depth + 1, width * 0.7f);
                    }
                }
                float cx = w * 0.82f, cy = h * 0.78f;
                for (int k = 0; k < 7; k++) Line(cx, cy, k / 7f * Mathf.PI * 2 + 0.3f, 160 + (float)rng.NextDouble() * 220, 0, 0.9f);
                t.SetPixels32(px); t.Apply();
                return _crack = Sprite.Create(t, new Rect(0, 0, w, h), Vector2.one * 0.5f);
            }
        }

        /// <summary>Frost overlay: noisy crystals concentrated at the edges.</summary>
        public static Sprite Frost
        {
            get
            {
                if (_frost) return _frost;
                int w = 512, h = 288; var t = NewTex(w, h); var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w * 2 - 1, v = (y + 0.5f) / h * 2 - 1;
                        float edge = Mathf.Clamp01(Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) * 1.3f - 0.55f);
                        float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.6f + Mathf.PerlinNoise(x * 0.21f, y * 0.21f) * 0.4f;
                        float a = Mathf.Clamp01(edge * 1.6f * n * 1.4f);
                        px[y * w + x] = new Color32(220, 240, 255, (byte)(a * 255));
                    }
                t.SetPixels32(px); t.Apply();
                return _frost = Sprite.Create(t, new Rect(0, 0, w, h), Vector2.one * 0.5f);
            }
        }
    }

    public static class UIX
    {
        public static RectTransform RT(this GameObject g) { var r = g.GetComponent<RectTransform>(); return r ? r : g.AddComponent<RectTransform>(); }

        public static RectTransform Set(this RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }
        public static RectTransform At(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size) => rt.Set(anchor, anchor, anchor, pos, size);
        public static RectTransform Fill(this RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = Vector2.one * 0.5f;
            rt.offsetMin = Vector2.one * inset; rt.offsetMax = -Vector2.one * inset; return rt;
        }

        public static GameObject Go(string name, Transform parent)
        {
            var g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            g.layer = 5;
            return g;
        }

        public static Image Img(Transform parent, string name, Color c, Sprite s = null, bool raycast = false)
        {
            var g = Go(name, parent);
            var i = g.AddComponent<Image>();
            i.sprite = s; i.color = c; i.raycastTarget = raycast;
            if (s != null && s.border != Vector4.zero) i.type = Image.Type.Sliced;
            return i;
        }

        public static Image Panel(Transform parent, string name, Color? c = null, bool outline = true, Color? outlineColor = null)
        {
            var p = Img(parent, name, c ?? Pal.Bg, UISprites.Round, true);
            if (outline)
            {
                var o = Img(p.transform, "Outline", outlineColor ?? new Color(1, 1, 1, 0.09f), UISprites.RoundOutline);
                o.rectTransform.Fill();
            }
            return p;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, TMP_FontAsset font = null, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, string name = "Text")
        {
            var g = Go(name, parent);
            var t = g.AddComponent<TextMeshProUGUI>();
            t.font = font ? font : R.Body;
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false; t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public static Button Button(Transform parent, string label, Action onClick, float w = 320, float h = 56, float fontSize = 24, Color? accent = null)
        {
            var bg = Panel(parent, "Btn_" + label, new Color(0.06f, 0.08f, 0.11f, 0.75f), true, new Color(1, 1, 1, 0.12f));
            bg.rectTransform.sizeDelta = new Vector2(w, h);
            var b = bg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var txt = Text(bg.transform, label, fontSize, Pal.Text, R.Bold, TextAlignmentOptions.Center, "Label");
            txt.rectTransform.Fill(); txt.characterSpacing = 4;
            var fx = bg.gameObject.AddComponent<UIButtonFx>();
            fx.Init(bg, txt, accent ?? Pal.Accent);
            b.onClick.AddListener(() => { Sfx.Play("click"); onClick?.Invoke(); });
            return b;
        }

        public static void Corners(Transform parent, Color c, float size = 18)
        {
            Vector2[] anchors = { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
            float[] rot = { 0, -90, 180, 90 };
            for (int i = 0; i < 4; i++)
            {
                var rt = Img(parent, "Corner" + i, c, UISprites.Corner).rectTransform;
                rt.anchorMin = rt.anchorMax = anchors[i]; rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = Vector2.one * size; rt.anchoredPosition = Vector2.zero;
                rt.localEulerAngles = new Vector3(0, 0, rot[i]);
            }
        }

        public static CanvasGroup Group(this Component c)
        {
            var g = c.GetComponent<CanvasGroup>();
            return g ? g : c.gameObject.AddComponent<CanvasGroup>();
        }
    }

    public class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Image bg; TextMeshProUGUI label; Color accent; bool hover; float t; Image bar;
        Color baseBg; public bool Locked;
        public void Init(Image b, TextMeshProUGUI l, Color a)
        {
            bg = b; label = l; accent = a; baseBg = b.color;
            bar = UIX.Img(b.transform, "HoverBar", a, UISprites.Round);
            bar.rectTransform.Set(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(4, -18));
        }
        public void SetBase(Color c) { baseBg = c; }
        public void OnPointerEnter(PointerEventData e) { if (Locked) return; hover = true; } // no sound on hover
        public void OnPointerExit(PointerEventData e) { hover = false; }
        void OnDisable() { hover = false; t = 0; transform.localScale = Vector3.one; }
        void Update()
        {
            t = Mathf.MoveTowards(t, hover && !Locked ? 1 : 0, Time.unscaledDeltaTime * 8);
            float e = t * t * (3 - 2 * t);
            if (bg) bg.color = Color.Lerp(baseBg, new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 0.9f), e);
            if (bar) { bar.color = new Color(accent.r, accent.g, accent.b, e); }
            if (label) label.color = Color.Lerp(Pal.Text, Color.white, e);
            transform.localScale = Vector3.one * (1 + 0.025f * e);
        }
    }

    /// <summary>Lightweight coroutine tweening on unscaled time.</summary>
    public static class Tween
    {
        public static float EaseOut(float t) => 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 3);
        public static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2; }
        public static float Back(float t) { t = Mathf.Clamp01(t); const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2); }

        public static Coroutine Run(float dur, Action<float> step, Action done = null) => Game.Runner.StartCoroutine(Co(dur, step, done));
        static IEnumerator Co(float dur, Action<float> step, Action done)
        {
            float t = 0;
            while (t < dur) { t += Time.unscaledDeltaTime; step(Mathf.Clamp01(t / dur)); yield return null; }
            step(1); done?.Invoke();
        }

        public static Coroutine Fade(CanvasGroup g, float to, float dur, Action done = null)
        {
            float from = g.alpha;
            return Run(dur, t => { if (g) g.alpha = Mathf.Lerp(from, to, EaseInOut(t)); }, done);
        }
    }
}
