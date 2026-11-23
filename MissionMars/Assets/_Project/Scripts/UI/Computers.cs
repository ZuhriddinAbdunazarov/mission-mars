using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    /// <summary>The two computers kids can sit at:
    /// 1) NAVIGATION (habitat): Sun, Earth and Mars moving on their real orbits, today's real distance (NASA JPL),
    ///    the radio delay, the next close approach, the ~7-month trip and a fast-forward button.
    /// 2) WEATHER (weather lab): real Mars weather from NASA InSight as a chart, real space weather from NASA DONKI,
    ///    and a forecast quiz.</summary>
    public class Computers : MonoBehaviour
    {
        public static Computers I; public static int ClosedFrame = -10;
        CanvasGroup group; RectTransform card; TextMeshProUGUI title, info; RectTransform view;
        bool open; float openedAt; Action onClose;
        // navigation
        RectTransform earth, mars, link; TextMeshProUGUI earthLbl, marsLbl, dateLbl; double dayShift; bool warp; Button warpBtn; TextMeshProUGUI warpTxt;
        readonly List<GameObject> navOnly = new List<GameObject>(), wxOnly = new List<GameObject>();
        // weather quiz
        readonly List<Button> quiz = new List<Button>(); TextMeshProUGUI quizTxt; bool quizDone;

        public static Computers Create()
        {
            var g = new GameObject("Computers");
            var cv = g.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 60;
            var cs = g.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
            g.AddComponent<GraphicRaycaster>();
            I = g.AddComponent<Computers>(); I.Build((RectTransform)g.transform); return I;
        }

        /// <summary>Place a UI element by its CENTRE (UIX.At puts the pivot on the anchor).</summary>
        static RectTransform Put(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size) { rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size; return rt; }

        void Build(RectTransform root)
        {
            group = root.gameObject.AddComponent<CanvasGroup>();
            var dim = UIX.Img(root, "Dim", new Color(0, 0.01f, 0.02f, 0.8f), null, true); dim.rectTransform.Fill();
            var c = UIX.Img(root, "Card", new Color(0.02f, 0.035f, 0.05f, 1f), UISprites.Round, true); card = c.rectTransform; card.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 900));
            var o = UIX.Img(card, "Line", new Color(0.45f, 0.9f, 1f, 0.4f), UISprites.RoundOutline); o.rectTransform.Fill();
            title = UIX.Text(card, "", 44, HC.Text, R.Title); title.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(50, -30), new Vector2(-100, 60));
            view = UIX.Go("View", card).RT(); view.Set(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(40, 10), new Vector2(820, -200));
            info = UIX.Text(card, "", 27, HC.Text, R.KidBody); info.rectTransform.Set(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -110), new Vector2(700, 620));
            var close = UIX.Button(card, "CLOSE  (E)", Close, 300, 60, 24, HC.Cyan); ((RectTransform)close.transform).At(new Vector2(1, 0), new Vector2(-210, 55), new Vector2(300, 60));

            // ---- navigation view: Sun, orbits, planets ----
            GameObject Nav(GameObject g) { navOnly.Add(g); return g; }
            var center = new Vector2(410, 0);
            var glow = UIX.Img(view, "SunGlow", new Color(1f, 0.7f, 0.2f, 0.35f), UISprites.Glow); Put(glow.rectTransform, new Vector2(0, 0.5f), center, new Vector2(170, 170)); Nav(glow.gameObject);
            var sun = UIX.Img(view, "Sun", new Color(1f, 0.85f, 0.35f), UISprites.Circle); Put(sun.rectTransform, new Vector2(0, 0.5f), center, new Vector2(46, 46)); Nav(sun.gameObject);
            var sunLbl = UIX.Text(view, "SUN", 20, new Color(1f, 0.85f, 0.4f), R.Bold, TextAlignmentOptions.Center); Put(sunLbl.rectTransform, new Vector2(0, 0.5f), center + new Vector2(0, -40), new Vector2(100, 26)); Nav(sunLbl.gameObject);
            // thin dotted orbits
            foreach (var (r, col) in new[] { (EarthR, new Color(0.4f, 0.7f, 1f, 0.7f)), (MarsR, new Color(1f, 0.55f, 0.35f, 0.7f)) })
                for (int i = 0; i < 90; i++)
                {
                    float a = i * Mathf.PI * 2 / 90; var dot = UIX.Img(view, "OrbitDot", col, UISprites.Circle);
                    Put(dot.rectTransform, new Vector2(0, 0.5f), center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, new Vector2(4, 4)); Nav(dot.gameObject);
                }
            link = UIX.Img(view, "Link", new Color(1f, 1f, 1f, 0.55f)).rectTransform; link.pivot = new Vector2(0, 0.5f); Nav(link.gameObject);
            earth = UIX.Img(view, "Earth", new Color(0.35f, 0.65f, 1f), UISprites.Circle).rectTransform; earth.anchorMin = earth.anchorMax = new Vector2(0, 0.5f); earth.sizeDelta = new Vector2(30, 30); Nav(earth.gameObject);
            mars = UIX.Img(view, "Mars", new Color(1f, 0.45f, 0.25f), UISprites.Circle).rectTransform; mars.anchorMin = mars.anchorMax = new Vector2(0, 0.5f); mars.sizeDelta = new Vector2(24, 24); Nav(mars.gameObject);
            earthLbl = UIX.Text(view, "EARTH", 22, new Color(0.6f, 0.85f, 1f), R.Bold, TextAlignmentOptions.Center); earthLbl.rectTransform.anchorMin = earthLbl.rectTransform.anchorMax = new Vector2(0, 0.5f); earthLbl.rectTransform.sizeDelta = new Vector2(160, 30); Nav(earthLbl.gameObject);
            marsLbl = UIX.Text(view, "MARS", 22, new Color(1f, 0.65f, 0.45f), R.Bold, TextAlignmentOptions.Center); marsLbl.rectTransform.anchorMin = marsLbl.rectTransform.anchorMax = new Vector2(0, 0.5f); marsLbl.rectTransform.sizeDelta = new Vector2(160, 30); Nav(marsLbl.gameObject);
            dateLbl = UIX.Text(view, "", 26, HC.Amber, R.Bold, TextAlignmentOptions.Center); Put(dateLbl.rectTransform, new Vector2(0, 0), new Vector2(410, 40), new Vector2(700, 40)); Nav(dateLbl.gameObject);
            warpBtn = UIX.Button(card, "FAST FORWARD", () => warp = !warp, 340, 60, 24, HC.Amber); ((RectTransform)warpBtn.transform).At(new Vector2(1, 0), new Vector2(-560, 55), new Vector2(340, 60)); Nav(warpBtn.gameObject);
            warpTxt = warpBtn.GetComponentInChildren<TextMeshProUGUI>();
            var reset = UIX.Button(card, "TODAY", () => { warp = false; dayShift = 0; }, 200, 60, 24, HC.Green); ((RectTransform)reset.transform).At(new Vector2(1, 0), new Vector2(-940, 55), new Vector2(200, 60)); Nav(reset.gameObject);

            // ---- weather view: InSight chart + quiz ----
            GameObject Wx(GameObject g) { wxOnly.Add(g); return g; }
            var chartBg = UIX.Img(view, "Chart", new Color(1, 1, 1, 0.04f), UISprites.Round); chartBg.rectTransform.Set(new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(-10, -20)); Wx(chartBg.gameObject);
            quizTxt = UIX.Text(view, "", 26, HC.Text, R.KidBody); quizTxt.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0, 1), new Vector2(10, -10), new Vector2(-20, -10)); Wx(quizTxt.gameObject);
            for (int i = 0; i < 3; i++)
            {
                int k = i; var b = UIX.Button(view, "", () => Answer(k), 250, 70, 26, HC.Cyan);
                ((RectTransform)b.transform).At(new Vector2(0, 0), new Vector2(135 + i * 270, 80), new Vector2(250, 70)); quiz.Add(b); Wx(b.gameObject);
            }
            group.alpha = 0; group.blocksRaycasts = false; group.interactable = false; gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ open / close
        void Show(Action closed)
        {
            onClose = closed; open = true; openedAt = Time.unscaledTime;
            gameObject.SetActive(true); group.alpha = 1; group.blocksRaycasts = true; group.interactable = true;
            UIState.Push(); Sfx.Play("beep", 0.6f, 1.2f);
        }
        public void Close()
        {
            if (!open) return; open = false; warp = false; ClosedFrame = Time.frameCount;
            group.alpha = 0; group.blocksRaycasts = false; group.interactable = false; gameObject.SetActive(false);
            UIState.Pop(); onClose?.Invoke();
        }

        public void OpenOrbit(Action closed = null)
        {
            foreach (var g in navOnly) g.SetActive(true); foreach (var g in wxOnly) g.SetActive(false);
            title.text = "NAVIGATION: EARTH & MARS"; dayShift = 0; warp = false;
            Show(closed); UpdateOrbit();
        }

        public void OpenWeather(Action closed = null)
        {
            foreach (var g in navOnly) g.SetActive(false); foreach (var g in wxOnly) g.SetActive(true);
            title.text = "MARS WEATHER STATION";
            BuildWeather();
            Show(closed);
        }

        void Update()
        {
            if (!open) return;
            if (Time.unscaledTime - openedAt > 0.4f && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))) { Close(); return; }
            if (navOnly[0].activeSelf)
            {
                if (warp) dayShift += Time.unscaledDeltaTime * 45; // 45 days every second
                if (warpTxt) warpTxt.text = warp ? "PAUSE" : "FAST FORWARD";
                UpdateOrbit();
            }
        }

        // ------------------------------------------------------------------ navigation (orbits)
        const float EarthR = 150, MarsR = 228; // Mars is 1.52 times farther from the Sun than Earth
        void UpdateOrbit()
        {
            var date = DateTime.UtcNow.AddDays(dayShift);
            double d = (date - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays;
            // mean longitudes (simple circular orbits, good enough to show where the planets are)
            double le = (100.464 + 0.98560910 * d) * Mathf.Deg2Rad, lm = (355.433 + 0.52403840 * d) * Mathf.Deg2Rad;
            var center = new Vector2(410, 0);
            Vector2 pe = center + new Vector2((float)Math.Cos(le), (float)Math.Sin(le)) * EarthR, pm = center + new Vector2((float)Math.Cos(lm), (float)Math.Sin(lm)) * MarsR;
            earth.anchoredPosition = pe; mars.anchoredPosition = pm;
            earthLbl.rectTransform.anchoredPosition = pe + new Vector2(0, 28); marsLbl.rectTransform.anchoredPosition = pm + new Vector2(0, 26);
            var dir = pm - pe; link.anchoredPosition = pe; link.anchorMin = link.anchorMax = new Vector2(0, 0.5f); link.sizeDelta = new Vector2(dir.magnitude, 3); link.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            double km = NasaLive.FromTable(date); bool real = !double.IsNaN(km);
            if (!real) km = dir.magnitude / EarthR * 149.6e6; // outside the JPL table: use the picture
            if (dayShift == 0 && NasaLive.I) km = NasaLive.I.DistanceKm;
            double min = km / 299792.458 / 60;
            dateLbl.text = date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) + (dayShift > 0.5 ? "  (future)" : "  (today)");
            string closeTxt = NasaLive.NextClosest(out var cd, out var ck) ? $"<b>{cd.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}</b> ({ck / 1e6:0} million km)" : "about every 26 months";
            var arrive = date.AddDays(210);
            var story = Game.Story;
            info.text =
                $"<color=#7FE3FF>DISTANCE EARTH - MARS</color>\n<size=44><b>{km / 1e6:0} million km</b></size>\n" +
                $"<size=21><color=#9FB6D6>{(real ? "NASA/JPL Horizons data" : "estimate")}</color></size>\n\n" +
                $"<color=#7FE3FF>RADIO MESSAGE TIME</color>\n<b>{min:0} minutes</b> one way, even at the speed of light\n\n" +
                $"<color=#7FE3FF>NEXT CLOSE APPROACH</color>\n{closeTxt}\n\n" +
                $"<color=#7FE3FF>TRIP TIME</color>\nabout <b>7 months (210 days)</b>. Leave on this date and you land on <b>{arrive.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}</b>.\n\n" +
                (story ? $"<color=#FFB347>YOUR LAUNCH WINDOW: T-{story.DaysLeft} days</color>" : "");
            if (!open) return;
        }

        // ------------------------------------------------------------------ weather (InSight + DONKI + quiz)
        readonly List<GameObject> bars = new List<GameObject>();
        int correct;
        void BuildWeather()
        {
            foreach (var b in bars) Destroy(b); bars.Clear();
            var api = NasaApi.I; var sols = api && api.Weather != null && api.Weather.Length > 0 ? api.Weather : null;
            var chart = (RectTransform)wxOnly[0].transform;
            float lo = -110, hi = 10; // °C axis
            void Lbl(string s, Vector2 pos, Color c, float size = 18, float w = 120) { var t = UIX.Text(chart, s, size, c, R.Bold, TextAlignmentOptions.Center); Put(t.rectTransform, new Vector2(0, 0), pos, new Vector2(w, 28)); bars.Add(t.gameObject); }
            Lbl("REAL MARS TEMPERATURES (NASA InSight)", new Vector2(400, 320), HC.Cyan, 22, 700);
            if (sols != null)
            {
                int n = Mathf.Min(7, sols.Length);
                for (int i = 0; i < n; i++)
                {
                    var s = sols[sols.Length - n + i]; float x = 90 + i * 105;
                    float y0 = Mathf.InverseLerp(lo, hi, s.tempMinC) * 200 + 70, y1 = Mathf.InverseLerp(lo, hi, s.tempMaxC) * 200 + 70;
                    var bar = UIX.Img(chart, "Bar", new Color(1f, 0.6f, 0.3f), UISprites.Round); Put(bar.rectTransform, new Vector2(0, 0), new Vector2(x, (y0 + y1) / 2), new Vector2(46, y1 - y0)); bars.Add(bar.gameObject);
                    Lbl($"{s.tempMaxC:0}°", new Vector2(x, y1 + 16), new Color(1f, 0.75f, 0.45f));
                    Lbl($"{s.tempMinC:0}°", new Vector2(x, y0 - 16), new Color(0.6f, 0.8f, 1f));
                    Lbl($"sol {s.sol}", new Vector2(x, 30), HC.Dim, 16);
                }
            }
            var w = api ? api.LastSol : null;
            string space = api && api.CmeCount30d >= 0 ? $"{api.CmeCount30d} clouds of hot gas left the Sun this month (NASA DONKI)" : "Sun data: saved NASA DONKI flares";
            info.text = w == null ? "Weather data loading…" :
                $"<color=#7FE3FF>TODAY AT THE STATION</color>\nDay <b>{w.tempMaxC:0} °C</b>, night <b>{w.tempMinC:0} °C</b>\n\n" +
                $"<color=#7FE3FF>AIR PRESSURE</color>\n<b>{w.pressurePa:0} Pa</b>. On Earth it is 101 325 Pa: Mars air is <b>130 times thinner</b>!\n\n" +
                $"<color=#7FE3FF>WIND</color>\n<b>{w.windMs:0.0} m/s</b> from the {w.windDir}\n\n" +
                $"<color=#7FE3FF>SPACE WEATHER</color>\n{space}\n\n" +
                "<size=21><color=#9FB6D6>Real measurements by NASA's InSight lander on Mars. Scientists use them to warn astronauts about cold nights and dust storms.</color></size>";
            // forecast quiz: what will tonight be like?
            if (w != null && !quizDone)
            {
                quizTxt.text = "<b>YOUR FORECAST:</b> how cold will it be TONIGHT at the base?";
                var opts = new[] { Mathf.Round(w.tempMinC), 15f, -20f }; correct = 0;
                // shuffle the 3 answers
                for (int i = 2; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (opts[i], opts[j]) = (opts[j], opts[i]); if (correct == i) correct = j; else if (correct == j) correct = i; }
                for (int i = 0; i < 3; i++) { quiz[i].gameObject.SetActive(true); quiz[i].GetComponentInChildren<TextMeshProUGUI>().text = $"{opts[i]:0} °C"; }
            }
            else { foreach (var q in quiz) q.gameObject.SetActive(false); quizTxt.text = quizDone ? "<color=#7FE07F>Forecast done! You are a Mars weather scientist.</color>" : ""; }
        }

        void Answer(int i)
        {
            if (quizDone) return;
            if (i == correct)
            {
                quizDone = true; Sfx.Play("success", 0.7f);
                quizTxt.text = "<color=#7FE07F><b>CORRECT!</b> Mars nights are colder than Antarctica. Thin air cannot keep the day's heat in.</color>";
                foreach (var q in quiz) q.gameObject.SetActive(false);
                AstroPass.Award("forecast", "Mars weather forecast", 2);
            }
            else { Sfx.Play("error", 0.5f); quizTxt.text = "Not quite. Look at the blue numbers in the chart: those are the NIGHT temperatures. Try again!"; }
        }
    }
}
