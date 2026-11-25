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

            BuildPower();

            // ---- weather view: InSight chart + quiz ----
            GameObject Wx(GameObject g) { wxOnly.Add(g); return g; }
            var chartBg = UIX.Img(view, "Chart", new Color(1, 1, 1, 0.04f), UISprites.Round); chartBg.rectTransform.Set(new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(-10, -20)); Wx(chartBg.gameObject);
            quizTxt = UIX.Text(view, "", 26, HC.Text, R.KidBody); quizTxt.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0, 1), new Vector2(10, -10), new Vector2(-20, -10)); Wx(quizTxt.gameObject);
            for (int i = 0; i < 3; i++)
            {
                int k = i; var b = UIX.Button(view, "", () => Answer(k), 250, 70, 26, HC.Cyan);
                ((RectTransform)b.transform).At(new Vector2(0, 0), new Vector2(135 + i * 270, 80), new Vector2(250, 70)); quiz.Add(b); Wx(b.gameObject);
            }
            BuildTerminal();
            group.alpha = 0; group.blocksRaycasts = false; group.interactable = false; gameObject.SetActive(false);
        }

        GameObject terminal, terminalButton;
        TMP_InputField command;
        TextMeshProUGUI transcript;
        readonly Queue<string> messages = new Queue<string>();

        void BuildTerminal()
        {
            terminalButton = UIX.Button(card, "MISSION TERMINAL", OpenTerminal, 310, 60, 24, HC.Cyan).gameObject;
            Put((RectTransform)terminalButton.transform, Vector2.zero, new Vector2(205, 65), new Vector2(310, 60));

            var panel = UIX.Img(card, "Mission terminal", new Color(0.025f, 0.045f, 0.06f), UISprites.Round, true);
            terminal = panel.gameObject;
            panel.rectTransform.Fill();
            var heading = UIX.Text(panel.transform, "MISSION TERMINAL", 42, HC.Cyan, R.Title);
            Put(heading.rectTransform, Vector2.up, new Vector2(765, -80), new Vector2(1430, 70));
            var help = UIX.Text(panel.transform, "Ask about Earth distance, closest approach, signal delay or Mars weather. Type SEND followed by a message to use the simulated Earth radio.", 28, HC.Text, R.KidBody);
            Put(help.rectTransform, Vector2.up, new Vector2(765, -178), new Vector2(1430, 100));
            transcript = UIX.Text(panel.transform, "", 27, HC.Text, R.KidBody);
            Put(transcript.rectTransform, Vector2.up, new Vector2(765, -465), new Vector2(1430, 450));
            transcript.richText = false;
            transcript.overflowMode = TextOverflowModes.Ellipsis;

            var input = UIX.Img(panel.transform, "Question", new Color(0.09f, 0.14f, 0.17f), UISprites.Round, true);
            Put(input.rectTransform, Vector2.zero, new Vector2(620, 125), new Vector2(1130, 76));
            var area = UIX.Go("Text area", input.transform).RT();
            area.Fill(); area.offsetMin = new Vector2(18, 4); area.offsetMax = new Vector2(-18, -4);
            area.gameObject.AddComponent<RectMask2D>();
            var value = UIX.Text(area, "", 28, Color.white, R.KidBody, TextAlignmentOptions.MidlineLeft);
            value.rectTransform.Fill(); value.enableWordWrapping = false; value.richText = false;
            var hint = UIX.Text(area, "How far is Earth?", 28, HC.Dim, R.KidBody, TextAlignmentOptions.MidlineLeft);
            hint.rectTransform.Fill();
            command = input.gameObject.AddComponent<TMP_InputField>();
            command.targetGraphic = input; command.textViewport = area; command.textComponent = value;
            command.placeholder = hint; command.characterLimit = 160; command.richText = false;
            command.lineType = TMP_InputField.LineType.SingleLine;
            command.onSubmit.AddListener(Submit);
            var submit = UIX.Button(panel.transform, "SUBMIT", () => Submit(command.text), 300, 76, 26, HC.Cyan);
            Put((RectTransform)submit.transform, Vector2.zero, new Vector2(1370, 125), new Vector2(300, 76));
            var close = UIX.Button(panel.transform, "CLOSE", Close, 180, 54, 22, HC.Cyan);
            Put((RectTransform)close.transform, Vector2.one, new Vector2(-150, -75), new Vector2(180, 54));
            terminal.SetActive(false);
        }

        public void OpenTerminal()
        {
            if (!open) Show(null);
            terminal.SetActive(true);
            transcript.text = string.Join("\n\n", messages);
            command.text = "";
            command.ActivateInputField();
        }

        void LogExchange(string text)
        {
            if (messages.Count == 3) messages.Dequeue();
            messages.Enqueue(text);
            transcript.text = string.Join("\n\n", messages);
        }

        void Submit(string raw)
        {
            string question = raw.Trim();
            if (question.Length == 0) return;
            command.text = "";
            var nasa = NasaLive.I;
            if (question.StartsWith("send ", StringComparison.OrdinalIgnoreCase))
            {
                string message = question.Substring(5).Trim();
                LogExchange($"Mars → Earth   {message}\nSimulated round trip: {2 * nasa.LightMinutes:0.0} seconds at 1 second per light-minute.");
                Game.Runner.StartCoroutine(RadioReply(message, (float)nasa.LightMinutes * 2));
            }
            else LogExchange(question + "\n" + AnswerQuestion(question));
            command.ActivateInputField();
        }

        System.Collections.IEnumerator RadioReply(string message, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            LogExchange($"Mission Control · simulated reply to “{message}”\n" + AnswerQuestion(message, true));
            AstroPass.Award("earthcall", "Called Earth", 1);
        }

        static string AnswerQuestion(string question, bool radio = false)
        {
            string q = question.ToLowerInvariant();
            var nasa = NasaLive.I;
            if (q.Contains("weather") || q.Contains("temperature") || q.Contains("cold") || q.Contains("wind") || q.Contains("pressure") || q.Contains("hot"))
            {
                var sol = NasaApi.I.LastSol;
                return $"NASA InSight archive · {sol.date}, sol {sol.sol}. Low {sol.tempMinC:0.#} °C, high {sol.tempMaxC:0.#} °C. Wind {sol.windMs:0.#} m/s {sol.windDir}. Pressure {sol.pressurePa:0} Pa.";
            }
            if (q.Contains("closest") || q.Contains("nearest") || q.Contains("approach"))
                return NasaLive.NextClosest(out var date, out var km)
                    ? $"Next closest Earth–Mars separation in the NASA/JPL daily table: {km:N0} km on {date:dd MMM yyyy} UTC."
                    : "The stored NASA/JPL table has no later close approach.";
            if (q.Contains("delay") || q.Contains("signal") || q.Contains("radio") || q.Contains("connection"))
                return $"Radio travel time: {nasa.DelayText} one way, {nasa.LightMinutes * 2:0.0} minutes round trip. Source: {nasa.SourceText}.";
            if (q.Contains("earth") || q.Contains("distance") || q.Contains("kilomet") || q.Contains("far"))
                return $"Earth–Mars separation: {nasa.DistanceKm:N0} km. {nasa.SourceText} · {nasa.DistanceDate}.";
            return radio ? "Message received. Mission Control reads you." : "Try “Mars weather”, “How far is Earth?”, “closest approach”, or “send Hello from Mars”.";
        }

        // ------------------------------------------------------------------ open / close
        void Show(Action closed)
        {
            terminal.SetActive(false); terminalButton.SetActive(!pwOnly[0].activeSelf);
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
            foreach (var g in navOnly) g.SetActive(true); foreach (var g in wxOnly) g.SetActive(false); foreach (var g in pwOnly) g.SetActive(false);
            title.text = "NAVIGATION: EARTH & MARS"; dayShift = 0; warp = false;
            Show(closed); UpdateOrbit();
        }

        // ------------------------------------------------------------------ night power planner
        /// <summary>Fired when the player's night power plan works (the story then plays the night).</summary>
        public static event Action PowerPlanOk;
        readonly List<GameObject> pwOnly = new List<GameObject>();
        // name, kWh for a 12-hour night, essential?, why
        static readonly (string name, float kwh, bool must, string why)[] Devices =
        {
            ("HEATER", 18, true, "Mars nights are -95 °C"),
            ("OXYGEN MAKER", 12, true, "we need air to breathe"),
            ("WATER MACHINE", 6, false, "the tank has water for tonight"),
            ("BASE LIGHTS", 3, false, "to see at night"),
            ("RADIO TO EARTH", 3, false, "talk to Mission Control"),
            ("LED GROW LIGHTS", 12, false, "plants can rest at night"),
            ("ROVER CHARGING", 24, false, "the rover can charge in the sun tomorrow"),
        };
        const float Battery = 40;
        readonly bool[] onOff = new bool[Devices.Length];
        readonly List<(Image bg, TextMeshProUGUI txt)> switches = new List<(Image, TextMeshProUGUI)>();
        Image energyFill; TextMeshProUGUI energyTxt, powerResult; bool powerSolved;

        void BuildPower()
        {
            GameObject Pw(GameObject g) { pwOnly.Add(g); return g; }
            for (int i = 0; i < Devices.Length; i++)
            {
                int k = i;
                var row = UIX.Img(view, "Switch", new Color(1, 1, 1, 0.06f), UISprites.Round, true); Put(row.rectTransform, new Vector2(0, 1), new Vector2(400, -40 - i * 66), new Vector2(780, 58)); Pw(row.gameObject);
                var b = row.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => Toggle(k));
                var t = UIX.Text(row.transform, "", 24, HC.Text, R.KidBody); t.rectTransform.Fill(); t.margin = new Vector4(20, 0, 20, 0); t.alignment = TextAlignmentOptions.MidlineLeft;
                switches.Add((row, t));
            }
            var track = UIX.Img(view, "Battery", new Color(1, 1, 1, 0.08f), UISprites.Round); Put(track.rectTransform, new Vector2(0, 0), new Vector2(400, 86), new Vector2(780, 44)); Pw(track.gameObject);
            energyFill = UIX.Img(track.transform, "Fill", HC.Green, UISprites.Round); energyFill.rectTransform.anchorMin = Vector2.zero; energyFill.rectTransform.anchorMax = new Vector2(0, 1); energyFill.rectTransform.pivot = new Vector2(0, 0.5f); energyFill.rectTransform.offsetMin = energyFill.rectTransform.offsetMax = Vector2.zero;
            energyTxt = UIX.Text(track.transform, "", 22, Color.white, R.Bold, TextAlignmentOptions.Center); energyTxt.rectTransform.Fill();
            var go = UIX.Button(view, "START THE NIGHT", RunNight, 360, 60, 24, HC.Amber); Put((RectTransform)go.transform, new Vector2(0, 0), new Vector2(400, 20), new Vector2(360, 56)); Pw(go.gameObject);
            powerResult = UIX.Text(card, "", 26, HC.Text, R.KidBody); powerResult.rectTransform.Set(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-50, 140), new Vector2(700, 200)); Pw(powerResult.gameObject);
        }

        void Toggle(int i) { onOff[i] = !onOff[i]; Sfx.Play("click", 0.6f, onOff[i] ? 1.2f : 0.8f); RefreshPower(); }

        public float NightDemand => Used();
        float Used() { float u = 0; for (int i = 0; i < Devices.Length; i++) if (onOff[i]) u += Devices[i].kwh; return u; }

        void RefreshPower()
        {
            for (int i = 0; i < Devices.Length; i++)
            {
                var d = Devices[i]; bool on = onOff[i];
                switches[i].bg.color = on ? new Color(0.25f, 0.7f, 0.4f, 0.35f) : new Color(1, 1, 1, 0.06f);
                switches[i].txt.text = $"<b>{(on ? "ON " : "OFF")}</b>   {d.name}  <color=#FFB347>{d.kwh:0} kWh</color>   <size=19><color=#9FB6D6>{d.why}</color></size>";
            }
            float u = Used();
            energyFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(u / Battery), 1);
            energyFill.color = u > Battery ? HC.Red : u > Battery * 0.85f ? HC.Amber : HC.Green;
            energyTxt.text = $"BATTERY: {u:0} of {Battery:0} kWh used tonight";
        }

        void RunNight()
        {
            float u = Used();
            if (!onOff[0]) { Sfx.Play("error", 0.6f); powerResult.text = "<color=#FF6A60><b>Brrr!</b></color> Without the HEATER the base freezes at -95 °C. Turn the heater ON."; return; }
            if (!onOff[1]) { Sfx.Play("error", 0.6f); powerResult.text = "<color=#FF6A60><b>No air!</b></color> The OXYGEN MAKER must stay ON all night."; return; }
            if (BaseExpedition.I && u>BaseExpedition.I.Charge) {powerResult.text=$"Battery is charging. Available {BaseExpedition.I.Charge:0.0} kWh; plan needs {u:0} kWh.";return;}
            if (u > Battery) { Sfx.Play("error", 0.6f); powerResult.text = $"<color=#FF6A60><b>Battery empty at 3 AM!</b></color> You need {u:0} kWh but the battery has only {Battery:0}. Turn something OFF."; return; }
            powerSolved = true; Sfx.Play("success", 0.8f);
            powerResult.text = $"<color=#7FE07F><b>GREAT PLAN!</b></color> {u:0} kWh used, {Battery - u:0} kWh left for the morning.\n<size=21>Real fact: NASA is building a small nuclear power plant for the Moon and Mars that works day AND night, even in dust storms.</size>";
            if(BaseExpedition.I) powerResult.text=$"Reserved {u:0} kWh for tonight. Sleep in a supplied bunk to run the night shift.";
            AstroPass.Award("power_plan", "Planned the night power", 3);
            PowerPlanOk?.Invoke();
        }

        public void OpenPower(Action closed = null)
        {
            foreach (var g in navOnly) g.SetActive(false); foreach (var g in wxOnly) g.SetActive(false); foreach (var g in pwOnly) g.SetActive(true);
            title.text = "NIGHT POWER PLANNER";
            info.text = "<color=#7FE3FF>NIGHT IS COMING</color>\nSolar panels make NO power at night. The base must live on its <b>battery</b> for about 12 hours.\n\n" +
                "<color=#7FE3FF>YOUR JOB</color>\nClick the machines to switch them ON or OFF. Keep the bar <color=#7FE07F>green</color>: you cannot use more than the battery holds!\n\n" +
                "<color=#9FB6D6><size=21>Mars gets only about half of Earth's sunlight, so every kilowatt-hour counts.</size></color>";
            if (!powerSolved) powerResult.text = "";
            RefreshPower();
            Show(closed);
        }

        public void OpenWeather(Action closed = null)
        {
            foreach (var g in navOnly) g.SetActive(false); foreach (var g in wxOnly) g.SetActive(true); foreach (var g in pwOnly) g.SetActive(false);
            title.text = "MARS WEATHER STATION";
            BuildWeather();
            Show(closed);
        }

        void Update()
        {
            if (!open) return;
            if (Time.unscaledTime - openedAt > 0.4f && ((!terminal.activeSelf && Input.GetKeyDown(KeyCode.E)) || Input.GetKeyDown(KeyCode.Escape))) { Close(); return; }
            if (terminal.activeSelf) return;
            if (navOnly.Count > 0 && navOnly[0].activeSelf)
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
                $"<color=#7FE3FF>INSIGHT ARCHIVE · SOL {w.sol} · {w.date}</color>\nDay <b>{w.tempMaxC:0} °C</b>, night <b>{w.tempMinC:0} °C</b>\n\n" +
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
