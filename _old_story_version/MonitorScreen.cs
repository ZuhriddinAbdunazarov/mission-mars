using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    /// <summary>World-space CRT monitor inside the habitat: orbit diagram + launch-window countdown + vehicle status.</summary>
    public class MonitorScreen : MonoBehaviour
    {
        CanvasGroup content, boot; TextMeshProUGUI bootText, days, daysLabel, status, feed, clock, alignText;
        RectTransform earth, mars, orbitRoot; Image screenBg, glow;
        public int Days = 780;
        public float Align = 0; // 0..1 cosmetic
        static Sprite thinRing;
        Light screenLight;
        public static readonly Color Green = new Color(0.45f, 1f, 0.6f);

        static Sprite ThinRing()
        {
            if (thinRing) return thinRing;
            int s = 512; var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - s / 2f, dy = y + 0.5f - s / 2f; float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1.6f - Mathf.Abs(d - (s / 2f - 4)));
                    // dashed
                    float ang = Mathf.Atan2(dy, dx); if (Mathf.Sin(ang * 60) < -0.2f) a *= 0.25f;
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            return thinRing = Sprite.Create(t, new Rect(0, 0, s, s), Vector2.one * 0.5f);
        }

        static Sprite scan;
        static Sprite Scanlines()
        {
            if (scan) return scan;
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var px = new Color32[16]; for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(0, 0, 0, (byte)(y < 2 ? 70 : 0));
            t.SetPixels32(px); t.Apply();
            return scan = Sprite.Create(t, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        }

        public static MonitorScreen Create(Transform parent, Vector3 localPos, Vector3 localEuler, float widthMeters)
        {
            var g = new GameObject("Monitor");
            g.transform.SetParent(parent, false); g.transform.localPosition = localPos; g.transform.localEulerAngles = localEuler;
            var m = g.AddComponent<MonitorScreen>(); m.Build(widthMeters); return m;
        }

        void Build(float w)
        {
            // bezel
            var bez = ProcMesh.Prim(PrimitiveType.Cube, "Bezel", transform, new Vector3(0, 0, 0.04f), new Vector3(w + 0.12f, w * 0.625f + 0.12f, 0.08f), R.HullDark);
            var cvGo = new GameObject("Screen", typeof(RectTransform)); cvGo.transform.SetParent(transform, false);
            var cv = cvGo.AddComponent<Canvas>(); cv.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)cvGo.transform; rt.sizeDelta = new Vector2(1280, 800); rt.localScale = Vector3.one * (w / 1280f);
            rt.localPosition = new Vector3(0, 0, -0.005f); rt.localRotation = Quaternion.identity;
            screenBg = UIX.Img(rt, "Bg", new Color(0.005f, 0.012f, 0.01f)); screenBg.rectTransform.Fill();
            glow = UIX.Img(rt, "Glow", new Color(0.2f, 1f, 0.5f, 0.0f), UISprites.Glow); glow.rectTransform.Fill(-100);

            boot = UIX.Go("Boot", rt).AddComponent<CanvasGroup>(); ((RectTransform)boot.transform).Fill();
            bootText = UIX.Text(boot.transform, "", 30, Green, R.Mono); bootText.rectTransform.Fill(50);
            boot.alpha = 0;

            content = UIX.Go("Content", rt).AddComponent<CanvasGroup>(); var c = (RectTransform)content.transform; c.Fill();
            var head = UIX.Img(c, "Head", new Color(0.45f, 1f, 0.6f, 0.12f)); head.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 60));
            var ht = UIX.Text(head.transform, "HAB-OS 3.1  //  JEZERO SURVIVAL HABITAT", 26, Green, R.Mono, TextAlignmentOptions.MidlineLeft); ht.rectTransform.Fill(); ht.rectTransform.offsetMin = new Vector2(24, 0);
            clock = UIX.Text(head.transform, "SOL 1", 26, Green, R.Mono, TextAlignmentOptions.MidlineRight); clock.rectTransform.Fill(); clock.rectTransform.offsetMax = new Vector2(-24, 0);

            // orbit diagram
            orbitRoot = UIX.Go("Orbits", c).RT(); orbitRoot.At(new Vector2(0, 0.5f), new Vector2(340, -30), new Vector2(600, 600));
            var sun = UIX.Img(orbitRoot, "SunGlow", new Color(1f, 0.85f, 0.3f, 0.8f), UISprites.Glow); sun.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));
            var sunC = UIX.Img(orbitRoot, "Sun", new Color(1f, 0.92f, 0.5f), UISprites.Circle); sunC.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));
            var eo = UIX.Img(orbitRoot, "EarthOrbit", new Color(0.45f, 0.7f, 1f, 0.7f), ThinRing()); eo.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330, 330));
            var mo = UIX.Img(orbitRoot, "MarsOrbit", new Color(1f, 0.5f, 0.3f, 0.7f), ThinRing()); mo.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 520));
            earth = Planet(orbitRoot, "EARTH", new Color(0.4f, 0.75f, 1f));
            mars = Planet(orbitRoot, "MARS (YOU)", new Color(1f, 0.45f, 0.25f));
            alignText = UIX.Text(orbitRoot, "", 24, Green, R.Mono, TextAlignmentOptions.Center); alignText.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0, -30), new Vector2(600, 40));

            // right panel
            var lab = UIX.Text(c, "NEXT EARTH–MARS LAUNCH WINDOW", 26, Green * 0.85f, R.Mono); lab.rectTransform.At(new Vector2(0, 1), new Vector2(940, -110), new Vector2(600, 40));
            days = UIX.Text(c, "780", 150, Green, R.Title, TextAlignmentOptions.TopLeft); days.rectTransform.At(new Vector2(0, 1), new Vector2(940, -150), new Vector2(600, 170));
            daysLabel = UIX.Text(c, "DAYS  (~26 MONTHS)", 30, Green, R.Mono); daysLabel.rectTransform.At(new Vector2(0, 1), new Vector2(940, -315), new Vector2(600, 40));
            status = UIX.Text(c, "", 25, Green, R.Mono); status.rectTransform.At(new Vector2(0, 1), new Vector2(940, -380), new Vector2(600, 300));
            feed = UIX.Text(c, "", 20, Green * 0.8f, R.Mono); feed.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(24, 14), new Vector2(-48, 60));
            content.alpha = 0;
            foreach (var r in rt.GetComponentsInChildren<RectTransform>()) r.gameObject.layer = 0;

            var sl = UIX.Img(rt, "Scan", Color.white, Scanlines()); sl.rectTransform.Fill(); sl.type = Image.Type.Tiled; sl.pixelsPerUnitMultiplier = 0.5f;

            var lg = new GameObject("ScreenLight"); lg.transform.SetParent(transform, false); lg.transform.localPosition = new Vector3(0, 0, -0.6f);
            screenLight = lg.AddComponent<Light>(); screenLight.type = LightType.Point; screenLight.range = 4.5f; screenLight.color = new Color(0.4f, 1f, 0.6f); screenLight.intensity = 0;
        }

        RectTransform Planet(RectTransform p, string label, Color c)
        {
            var dot = UIX.Img(p, label, c, UISprites.Circle).rectTransform; dot.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));
            var g = UIX.Img(dot, "Glow", new Color(c.r, c.g, c.b, 0.5f), UISprites.Glow); g.rectTransform.Fill(-18);
            var t = UIX.Text(dot, label, 20, c, R.Mono, TextAlignmentOptions.Center); t.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, 24), new Vector2(200, 26));
            return dot;
        }

        public IEnumerator PowerOn()
        {
            Sfx.At("powerup", transform.position, 0.6f);
            // CRT flicker
            for (int i = 0; i < 6; i++) { screenBg.color = i % 2 == 0 ? new Color(0.1f, 0.25f, 0.15f) : new Color(0.005f, 0.012f, 0.01f); screenLight.intensity = i % 2 == 0 ? 2 : 0; yield return new WaitForSeconds(0.07f); }
            screenBg.color = new Color(0.005f, 0.02f, 0.012f); screenLight.intensity = 1.6f; glow.color = new Color(0.2f, 1f, 0.5f, 0.06f);
            boot.alpha = 1;
            string[] lines = {
                "HAB-OS 3.1 BOOT ............ OK",
                "POWER: SOLAR ARRAY ......... 62%",
                "LIFE SUPPORT: MOXIE ........ ONLINE",
                "COMMS: EARTH LINK .......... NO SIGNAL",
                "COMPUTING ORBITAL ALIGNMENT ...",
            };
            string s = "";
            foreach (var l in lines)
            {
                s += l + "\n"; bootText.text = s + "_";
                Sfx.At("beep", transform.position, 0.25f);
                yield return new WaitForSeconds(0.45f);
            }
            yield return new WaitForSeconds(0.5f);
            boot.alpha = 0; content.alpha = 1;
            Sfx.At("notify", transform.position, 0.5f);
        }

        public void SetStatus(string s) => status.text = s;
        public void SetFeed(string s) => feed.text = s;
        public void SetSol(string s) => clock.text = s;

        void Update()
        {
            if (!days) return;
            days.text = Days <= 0 ? "OPEN" : Days.ToString();
            daysLabel.text = Days <= 0 ? "LAUNCH WINDOW IS OPEN" : "DAYS  (~26 MONTHS)";
            float d = Days;
            float aE = (105 - d * 360f / 365.25f) * Mathf.Deg2Rad;
            float aM = (30 - d * 360f / 687f) * Mathf.Deg2Rad;
            earth.anchoredPosition = new Vector2(Mathf.Cos(aE), Mathf.Sin(aE)) * 165;
            mars.anchoredPosition = new Vector2(Mathf.Cos(aM), Mathf.Sin(aM)) * 260;
            alignText.text = Days <= 0 ? "PLANETS ALIGNED — TRANSFER ORBIT GO" : "WAITING FOR ALIGNMENT";
            if (screenLight && screenLight.intensity > 0) screenLight.intensity = 1.5f + Mathf.PerlinNoise(Time.time * 8, 0) * 0.3f;
        }
    }
}
