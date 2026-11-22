using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    public class Puzzle
    {
        public string Q, Explain, Hint; public string[] A; public int Correct; public string Fact;
        public Puzzle(string q, string[] a, int c, string hint, string explain) { Q = q; A = a; Correct = c; Hint = hint; Explain = explain; }
    }

    public static class Puzzles
    {
        // Easy questions, two per rocket detail. Every answer is in the Junior Astronaut Handbook (the book).
        public static readonly Puzzle[][] Sets =
        {
            new[] { // Engine Nozzle (book: "Parts of a Rocket" + "How to Build a Rocket")
                new Puzzle("What does the ENGINE NOZZLE do?", new[]{ "Pushes gas DOWN, rocket goes UP", "Keeps the astronauts warm", "Stores food", "Talks to Earth" }, 0,
                    "Look at the bottom of a rocket during launch: fire comes out!", "Hot gas shoots down out of the nozzle, and that pushes the rocket up. Push down, go up!"),
                new Puzzle("Where do the ENGINES go on a rocket?", new[]{ "On the top", "At the bottom", "In the middle", "On the side door" }, 1,
                    "Fire comes out of the engines. Where does the fire come out?", "Engines always go at the BOTTOM. That's step 1 of building a rocket."),
                new Puzzle("A rocket flies like a balloon when you let it go. Why?", new[]{ "Air rushes out the back and pushes it forward", "Balloons are magic", "The wind blows it", "It is very light" }, 0,
                    "Look at page 6 of the book: Push Down, Go Up!", "Gas rushes out one way, and the rocket goes the other way. Scientists call it Newton's Third Law."),
                new Puzzle("Rockets are built…", new[]{ "From the top down", "From the bottom up", "Lying on the side", "In any order" }, 1,
                    "Like a tower of blocks!", "Rockets are built from the bottom up: engines first, then fuel, then the computer on top."),
            },
            new[] { // Fuel Valve
                new Puzzle("What does the FUEL VALVE do?", new[]{ "Turns on the lights", "Opens and closes the fuel, like a tap", "Makes oxygen", "Steers the rocket" }, 1,
                    "Think of the tap in your kitchen sink.", "The valve works like a tap: it lets fuel flow to the engines, or stops it."),
                new Puzzle("Which machine makes OXYGEN from Mars air?", new[]{ "MOXIE", "The fuel tank", "The radio", "The parachute" }, 0,
                    "It's the machine you carried into the bunker!", "MOXIE turns Mars air (carbon dioxide) into oxygen. NASA tested it on the Perseverance rover!"),
                new Puzzle("What is Mars air mostly made of?", new[]{ "Oxygen", "Carbon dioxide", "Helium", "Water" }, 1,
                    "It is the gas we breathe OUT.", "Mars air is about 95% carbon dioxide. We can't breathe it, so we need MOXIE."),
                new Puzzle("Where can astronauts find water on Mars?", new[]{ "In rivers", "From rain", "Ice under the ground", "In the oceans" }, 2,
                    "NASA's orbiters found it with radar.", "Mars has lots of ice hidden under the ground. We melt it into drinking water."),
            },
            new[] { // Flight Computer
                new Puzzle("The FLIGHT COMPUTER is like the rocket's…", new[]{ "Legs", "Lunchbox", "Brain", "Wheels" }, 2,
                    "Which part of YOU thinks and decides?", "The flight computer is the rocket's brain. It steers and decides when to turn the engines on."),
                new Puzzle("How often do Earth and Mars line up for the trip home?", new[]{ "Every day", "Every week", "About every 2 years", "Never" }, 2,
                    "Look at the timer on top of the bunker!", "Earth and Mars line up about every 26 months. That's the 2-year timer above your base."),
                new Puzzle("What do we call one day on Mars?", new[]{ "A moon", "A sol", "An orbit", "A week" }, 1,
                    "It sounds like the Latin word for Sun.", "A sol is 24 hours and 39 minutes, a little longer than an Earth day."),
                new Puzzle("How long does a radio message take from Mars to Earth?", new[]{ "1 second", "3 to 22 minutes", "1 whole day", "1 year" }, 1,
                    "Mars is VERY far away. Look at page 4.", "Radio is super fast, but Mars is so far away that a message takes 3 to 22 minutes."),
            },
        };
    }

    /// <summary>All full-screen menus: title, pause, puzzle, NASA data, ending.</summary>
    public class Screens : MonoBehaviour
    {
        public static Screens I;
        RectTransform root;
        CanvasGroup title, pause, puzzle, data, ending, controls;
        // puzzle
        TextMeshProUGUI pzHeader, pzTitle, pzQ, pzFeedback, pzCounter; readonly List<(Button b, Image bg, TextMeshProUGUI t)> pzBtns = new List<(Button, Image, TextMeshProUGUI)>();
        Button pzNext; int pzChosen = -1; bool pzContinue;
        public int FirstTry, Total;
        // ending
        TextMeshProUGUI endStats;
        public Action OnPlay, OnQuit, OnRestart;
        bool dataFromPause;

        public static Screens Create()
        {
            var g = new GameObject("Screens");
            var cv = g.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 50;
            var cs = g.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
            g.AddComponent<GraphicRaycaster>();
            var s = g.AddComponent<Screens>(); I = s; s.root = (RectTransform)g.transform; s.Build(); return s;
        }

        CanvasGroup Layer(string n, bool dim)
        {
            var g = UIX.Go(n, root).AddComponent<CanvasGroup>(); ((RectTransform)g.transform).Fill();
            if (dim)
            {
                var d = UIX.Img(g.transform, "Dim", new Color(0.0f, 0.01f, 0.02f, 0.78f), null, true); d.rectTransform.Fill();
                var v = UIX.Img(g.transform, "Vig", new Color(0, 0, 0, 0.6f), UISprites.Vignette); v.rectTransform.Fill(-40);
            }
            g.alpha = 0; g.blocksRaycasts = false; g.interactable = false; g.gameObject.SetActive(false);
            return g;
        }

        static Image Card(Transform p, Vector2 size, Color accent)
        {
            var c = UIX.Img(p, "Card", new Color(0.02f, 0.035f, 0.05f, 0.94f), UISprites.Round, true); c.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var o = UIX.Img(c.transform, "Line", new Color(accent.r, accent.g, accent.b, 0.35f), UISprites.RoundOutline); o.rectTransform.Fill();
            var top = UIX.Img(c.transform, "Top", new Color(accent.r, accent.g, accent.b, 0.1f), UISprites.HGradient); top.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(2, -2), new Vector2(-4, 100));
            UIX.Corners(c.transform, accent, 20);
            return c;
        }

        static Button Btn(Transform p, string label, Action a, Vector2 pos, float w = 380, float h = 64, Color? accent = null, Vector2? anchor = null)
        {
            var b = UIX.Button(p, label, a, w, h, 24, accent ?? HC.Cyan);
            var rt = (RectTransform)b.transform; rt.At(anchor ?? new Vector2(0.5f, 0.5f), pos, new Vector2(w, h));
            return b;
        }

        void Build()
        {
            // ---------------- TITLE ----------------
            title = Layer("Title", false);
            // dark on the left behind the menu, fading smoothly to clear (no hard edge)
            var grad = UIX.Img(title.transform, "LeftShade", new Color(0, 0, 0, 0.8f), UISprites.HGradient); grad.rectTransform.Set(new Vector2(0, 0), new Vector2(0.6f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var tp = UIX.Go("Panel", title.transform).RT(); tp.Set(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, 30), new Vector2(900, 700));
            var pill = UIX.Text(tp, "NASA SPACE APPS 2026  ·  JUNIOR ASTRONAUT MISSION TRAINER", 17, HC.Amber, R.Bold); pill.characterSpacing = 6;
            pill.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 26));
            var t1 = UIX.Text(tp, "MISSION MARS", 96, HC.Text, R.Title); t1.characterSpacing = 6; t1.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(-4, -40), new Vector2(0, 110));
            var t2 = UIX.Text(tp, "THE 2-YEAR WINDOW", 34, HC.Cyan, R.Bold); t2.characterSpacing = 22; t2.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(4, -152), new Vector2(0, 44));
            var t3 = UIX.Text(tp, "Your rocket crashed on Mars! Find air, water and a safe place to hide. Then fix your rocket and fly home.", 26, HC.Text, R.Body);
            t3.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -214), new Vector2(680, 80));
            Btn(tp, "START MISSION", () => OnPlay?.Invoke(), new Vector2(0, -350), 380, 70, HC.Amber, new Vector2(0, 1)).transform.SetAsLastSibling();
            Btn(tp, "NASA DATA", () => OpenData(false), new Vector2(0, -432), 380, 58, HC.Cyan, new Vector2(0, 1));
            Btn(tp, "HOW TO PLAY", () => Show(controls, true), new Vector2(0, -500), 380, 58, HC.Cyan, new Vector2(0, 1));
            Btn(tp, "HANDBOOK", () => Application.OpenURL(BookUrl), new Vector2(0, -568), 186, 58, HC.Green, new Vector2(0, 1));
            Btn(tp, "GITHUB", () => Application.OpenURL(GithubUrl), new Vector2(194, -568), 186, 58, HC.Green, new Vector2(0, 1));
            if (Application.platform != RuntimePlatform.WebGLPlayer) Btn(tp, "QUIT", () => { if (OnQuit != null) OnQuit(); else Application.Quit(); }, new Vector2(0, -636), 380, 58, HC.Cyan, new Vector2(0, 1));
            // speech bubble from the astronaut on the right
            var bub = UIX.Img(title.transform, "HelloBubble", new Color(1f, 1f, 1f, 0.94f), UISprites.Round);
            bub.rectTransform.Set(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520, 300), new Vector2(470, 120));
            var hi = UIX.Text(bub.transform, "<b>Hi, Junior Astronaut!</b>\nLet's learn about Mars together!", 28, new Color(0.07f, 0.1f, 0.17f), R.Body, TextAlignmentOptions.Center);
            hi.rectTransform.Fill();
            var tail = UIX.Img(bub.transform, "Tail", new Color(1f, 1f, 1f, 0.94f), UISprites.Diamond);
            tail.rectTransform.Set(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(40, 2), new Vector2(34, 34));
            var foot = UIX.Text(title.transform, "Made with real NASA data  ·  Astronaut model by Jungle Jim (CC-BY)  ·  Not an official NASA product", 16, new Color(1, 1, 1, 0.45f), R.Body, TextAlignmentOptions.Center);
            foot.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(-80, 24));

            // ---------------- CONTROLS ----------------
            controls = Layer("Controls", true);
            var cc = Card(controls.transform, new Vector2(820, 600), HC.Cyan);
            var ch = UIX.Text(cc.transform, "CONTROLS", 34, HC.Text, R.Title, TextAlignmentOptions.Center); ch.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(0, 50));
            string[,] rows = { { "W A S D", "Walk" }, { "MOUSE", "Look around" }, { "SHIFT", "Run (uses more oxygen)" }, { "SPACE", "Jump — Mars gravity is only 38%!" }, { "E", "Use / pick up / talk" }, { "1 – 6", "Choose an item in your hotbar" }, { "ENTER", "Skip NOVA's message" }, { "ESC", "Pause" } };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                var k = UIX.Text(cc.transform, rows[i, 0], 24, HC.Cyan, R.Bold, TextAlignmentOptions.Right); k.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -120 - i * 48), new Vector2(200, 40));
                var v = UIX.Text(cc.transform, rows[i, 1], 24, HC.Text, R.Body); v.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, -120 - i * 48), new Vector2(480, 40));
            }
            Btn(cc.transform, "BACK", () => Show(controls, false), new Vector2(0, 50), 260, 56, HC.Cyan, new Vector2(0.5f, 0));

            // ---------------- PAUSE ----------------
            pause = Layer("Pause", true);
            var pt = UIX.Text(pause.transform, "PAUSED", 60, HC.Text, R.Title, TextAlignmentOptions.Center); pt.characterSpacing = 20; pt.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(800, 80));
            Btn(pause.transform, "RESUME", () => TogglePause(false), new Vector2(0, 90), 400, 66, HC.Amber);
            Btn(pause.transform, "NASA DATA", () => OpenData(true), new Vector2(0, 10), 400, 60);
            Btn(pause.transform, "CONTROLS", () => Show(controls, true), new Vector2(0, -64), 400, 60);
            Btn(pause.transform, "RESTART MISSION", () => { TogglePause(false); OnRestart?.Invoke(); }, new Vector2(0, -138), 400, 60);
            if (Application.platform != RuntimePlatform.WebGLPlayer) Btn(pause.transform, "QUIT GAME", () => Application.Quit(), new Vector2(0, -212), 400, 60);

            // ---------------- PUZZLE ----------------
            puzzle = Layer("Puzzle", true);
            var pc = Card(puzzle.transform, new Vector2(1180, 700), HC.Amber);
            var pzHow = UIX.Text(pc.transform, "Click an answer, or press 1, 2, 3 or 4 on the keyboard.   Tip: every answer is in your Junior Astronaut Handbook!", 18, HC.Dim, R.Body); pzHow.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(56, -232), new Vector2(-112, 26));
            pzHeader = UIX.Text(pc.transform, "LOCKED PART  ·  SOLVE TO UNLOCK", 16, HC.Amber, R.Bold); pzHeader.characterSpacing = 8;
            pzHeader.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(56, -34), new Vector2(-112, 24));
            pzCounter = UIX.Text(pc.transform, "", 18, HC.Dim, R.Mono, TextAlignmentOptions.TopRight); pzCounter.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(56, -36), new Vector2(-112, 24));
            pzTitle = UIX.Text(pc.transform, "", 34, HC.Text, R.Title); pzTitle.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(56, -64), new Vector2(-112, 48));
            pzQ = UIX.Text(pc.transform, "", 36, HC.Text, R.Bold); pzQ.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(56, -140), new Vector2(-112, 100));
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var b = UIX.Button(pc.transform, "", () => PzChoose(idx), 528, 86, 26, HC.Cyan);
                var rt = (RectTransform)b.transform; rt.Set(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i % 2 == 0 ? -1 : 1) * 274, -272 - (i / 2) * 102), new Vector2(528, 88));
                var label = b.GetComponentInChildren<TextMeshProUGUI>(); label.alignment = TextAlignmentOptions.Center; label.characterSpacing = 0; label.font = R.Bold; label.fontSize = 27;
                pzBtns.Add((b, b.GetComponent<Image>(), label));
            }
            pzFeedback = UIX.Text(pc.transform, "", 24, HC.Text, R.Body, TextAlignmentOptions.Center); pzFeedback.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(-140, 80));
            pzNext = Btn(pc.transform, "CONTINUE", () => pzContinue = true, new Vector2(0, 50), 320, 58, HC.Green, new Vector2(0.5f, 0));

            // ---------------- DATA ----------------
            data = Layer("Data", true);
            var dc = Card(data.transform, new Vector2(1500, 990), HC.Cyan);
            var dh = UIX.Text(dc.transform, "REAL NASA DATA IN THIS GAME", 40, HC.Text, R.Title); dh.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -40), new Vector2(-120, 56));
            var ds = UIX.Text(dc.transform, "", 20, HC.Amber, R.Mono); ds.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -100), new Vector2(-120, 28)); dataStatus = ds;
            string[,] rowsD =
            {
                { "flare", "NASA DONKI — Space Weather Database", "The solar storm that sends you to the bunker is a REAL flare measured by NASA. Online: live flares from the last 30 days. Offline: real archived flares (e.g. X9.0, 3 Oct 2024). Bigger flare = stronger storm.", "Links your shelter decision to actual space weather." },
                { "shield", "Curiosity Rover — RAD instrument", "Your radiation meter uses Curiosity's measurement: about 0.67 mSv per sol on the surface. The thick bunker walls block most of it.", "Shows why Mars needs shielding: no magnetic field, thin air." },
                { "air", "Perseverance — MOXIE experiment", $"Mars air is {MarsFacts.CO2}% carbon dioxide (NASA fact sheet). MOXIE turned that CO2 into oxygen: 122 g in total, up to 12 g per hour. Your oxygen maker works the same way.", "Making air from Mars air is real NASA technology." },
                { "water", "Mars Reconnaissance Orbiter — SHARAD radar", "NASA radar found big sheets of ice under the ground. Your Ice-to-Water machine melts it.", "Water is the key resource for living on Mars." },
                { "clock", "NASA/JPL Horizons — where the planets are", $"Earth and Mars come close only every {MarsFacts.SynodicDays / 30.44f:0} months ({MarsFacts.SynodicDays:0} days). The 2-year timer above your shelter and the Earth-message delay use JPL's real planet positions.", "Why astronauts can't just fly home whenever they want." },
                { "robot", "NASA 3D Resources + NASA fact sheet", $"Official NASA models (Perseverance, Ingenuity, rockets). Mars facts: gravity is only 38% of Earth's, a sol lasts {MarsFacts.SolLength}, average {MarsFacts.MeanTempC} °C.", "Real hardware and real numbers kids can trust." },
            };
            // LIVE panel: today's real Earth–Mars distance (JPL Horizons via our research site's REST API), Mars clock (Mars24), NASA photos
            var live = UIX.Img(dc.transform, "Live", new Color(0.3f, 0.9f, 1f, 0.07f), UISprites.Round); live.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -138), new Vector2(-120, 172));
            var lh = UIX.Text(live.transform, "MARS TODAY", 20, HC.Cyan, R.Bold); lh.characterSpacing = 8; lh.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -12), new Vector2(700, 26));
            liveText = UIX.Text(live.transform, "", 20, HC.Text, R.Body); liveText.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -38), new Vector2(840, 130));
            for (int k = 0; k < livePhotos.Length; k++)
            {
                livePhotos[k] = new GameObject("Photo" + k).AddComponent<RawImage>(); livePhotos[k].transform.SetParent(live.transform, false);
                livePhotos[k].rectTransform.Set(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16 - (2 - k) * 168, 0), new Vector2(160, 126));
                livePhotos[k].color = new Color(1, 1, 1, 0.08f);
            }
            for (int i = 0; i < rowsD.GetLength(0); i++)
            {
                var row = UIX.Img(dc.transform, "Row", new Color(1, 1, 1, 0.035f), UISprites.Round); row.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -320 - i * 94), new Vector2(-120, 88));
                var ic = UIX.Img(row.transform, "Icon", HC.Cyan, Icons.Get(rowsD[i, 0])); ic.rectTransform.Set(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(44, 44));
                var n = UIX.Text(row.transform, rowsD[i, 1], 23, HC.Text, R.Bold); n.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(90, -8), new Vector2(-110, 28));
                var b = UIX.Text(row.transform, rowsD[i, 2] + $"  <color={HC.Hex(HC.Amber)}>Why: {rowsD[i, 3]}</color>", 17, HC.Dim, R.Body); b.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(90, -36), new Vector2(-110, 50));
            }
            Btn(dc.transform, "HANDBOOK", () => Application.OpenURL(BookUrl), new Vector2(-560, 40), 260, 56, HC.Green, new Vector2(0.5f, 0));
            Btn(dc.transform, "MARS & MOON EXPLORER", () => Application.OpenURL(NasaLive.ExplorerUrl), new Vector2(-170, 40), 420, 56, HC.Amber, new Vector2(0.5f, 0));
            Btn(dc.transform, "GITHUB", () => Application.OpenURL(GithubUrl), new Vector2(220, 40), 260, 56, HC.Green, new Vector2(0.5f, 0));
            Btn(dc.transform, "CLOSE", () => CloseData(), new Vector2(530, 40), 260, 56, HC.Cyan, new Vector2(0.5f, 0));
            GithubQr(dc.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -16), 104);

            // ---------------- ENDING ----------------
            ending = Layer("Ending", false);
            var eg = UIX.Img(ending.transform, "Shade", new Color(0, 0, 0, 0.55f), null, true); eg.rectTransform.Fill();
            var e1 = UIX.Text(ending.transform, "MISSION COMPLETE", 22, HC.Amber, R.Bold, TextAlignmentOptions.Center); e1.characterSpacing = 16; e1.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1200, 34));
            var e2 = UIX.Text(ending.transform, "WELCOME HOME", 104, HC.Text, R.Title, TextAlignmentOptions.Center); e2.characterSpacing = 10; e2.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1600, 120));
            endStats = UIX.Text(ending.transform, "", 26, HC.Dim, R.Body, TextAlignmentOptions.Center); endStats.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1200, 200));
            Btn(ending.transform, "PLAY AGAIN", () => { OnRestart?.Invoke(); }, new Vector2(-200, -170), 360, 66, HC.Amber);
            Btn(ending.transform, "NASA DATA", () => OpenData(false), new Vector2(200, -170), 360, 66);
            Btn(ending.transform, "READ THE HANDBOOK", () => Application.OpenURL(BookUrl), new Vector2(-200, -256), 360, 60, HC.Green);
            Btn(ending.transform, "SEE US ON GITHUB", () => Application.OpenURL(GithubUrl), new Vector2(200, -256), 360, 60, HC.Green);
            GithubQr(ending.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560, -200), 150);
        }
        TextMeshProUGUI dataStatus, liveText; readonly RawImage[] livePhotos = new RawImage[3]; bool liveHooked;

        /// <summary>Fills the MARS TODAY panel with the real numbers we have right now.</summary>
        void RefreshLive()
        {
            var nl = NasaLive.I; if (!nl || !liveText) return;
            Mars24.Now(out double ls, out double mtc, out double msd);
            string next = NasaLive.NextClosest(out var when, out var km) ? $"Next close approach: <b>{when:d MMM yyyy}</b>, {km / 1e6:0} million km." : "";
            string dot = nl.DistanceLive ? $"<color={HC.Hex(HC.Green)}>● LIVE</color>" : $"<color={HC.Hex(HC.Amber)}>● JPL TABLE</color>";
            liveText.text = $"{dot}  Mars is <b>{nl.DistanceText}</b> from Earth. A radio message takes <b>{nl.DelayText}</b> one way.\n" +
                $"On Mars right now: <b>{Mars24.Clock(mtc)}</b> (Mars time), <b>northern {Mars24.Season(ls)}</b>, Mars sol date {msd:0}. {next}\n" +
                Weather() +
                $"<size=16><color={HC.Hex(HC.Dim)}>Sources: NASA/JPL Horizons ({nl.SourceText}), NASA GISS Mars24, NASA InSight, NASA APOD + Image Library.</color></size>";
            // first picture = NASA Astronomy Picture of the Day (if it loaded), then NASA Image Library Mars photos
            var api = NasaApi.I; int slot = 0;
            if (api && api.ApodImage) { livePhotos[slot].texture = api.ApodImage; livePhotos[slot].color = Color.white; slot++; }
            for (int k = 0; slot < livePhotos.Length && k < nl.Photos.Length; k++)
                if (nl.Photos[k]) { livePhotos[slot].texture = nl.Photos[k]; livePhotos[slot].color = Color.white; slot++; }
        }

        /// <summary>One line of real Mars weather from NASA's InSight lander.</summary>
        static string Weather()
        {
            var api = NasaApi.I; if (!api) return "";
            var w = api.LastSol;
            string tag = api.WeatherLive ? $"<color={HC.Hex(HC.Green)}>● LIVE API</color>" : $"<color={HC.Hex(HC.Amber)}>● SAVED</color>";
            return $"{tag}  Mars weather by NASA InSight (sol {w.sol}): <b>{w.tempMinC:0} to {w.tempMaxC:0} °C</b>, air pressure {w.pressurePa:0} Pa (Earth: 101 325 Pa), wind {w.windMs:0.0} m/s.\n";
        }
        const string BookUrl = "https://github.com/ZuhriddinAbdunazarov/mission-mars/blob/main/Book/Junior_Astronaut_Handbook.pdf";
        const string GithubUrl = "https://github.com/ZuhriddinAbdunazarov/mission-mars";

        /// <summary>QR code (Resources/Tex/QR_GitHub.png) that opens the GitHub repo, with a caption under it.</summary>
        static void GithubQr(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float size)
        {
            var t = Resources.Load<Texture2D>("Tex/QR_GitHub");
            if (!t) return;
            var back = UIX.Img(parent, "QR", Color.white, Sprite.Create(t, new Rect(0, 0, t.width, t.height), Vector2.one * 0.5f));
            back.rectTransform.Set(anchor, anchor, pivot, pos, new Vector2(size, size));
            var cap = UIX.Text(back.transform, "SCAN: GITHUB", 15, HC.Dim, R.Bold, TextAlignmentOptions.Center);
            cap.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(40, 22));
        }

        // ---------------- info card (how a machine works + link to the book) ----------------
        CanvasGroup info; TextMeshProUGUI infoTitle, infoBody, infoBook; Image infoIcon; bool infoOk;
        void BuildInfo()
        {
            info = Layer("Info", true);
            var c = Card(info.transform, new Vector2(1100, 640), HC.Cyan);
            infoIcon = UIX.Img(c.transform, "Icon", HC.Cyan); infoIcon.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -40), new Vector2(64, 64)); infoIcon.preserveAspect = true;
            var k = UIX.Text(c.transform, "NEW EQUIPMENT  ·  HOW IT WORKS", 16, HC.Cyan, R.Bold); k.characterSpacing = 8; k.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(140, -40), new Vector2(-200, 24));
            infoTitle = UIX.Text(c.transform, "", 38, HC.Text, R.Title); infoTitle.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(140, -66), new Vector2(-200, 50));
            infoBody = UIX.Text(c.transform, "", 25, HC.Text, R.Body); infoBody.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(56, -150), new Vector2(-112, 340)); infoBody.lineSpacing = 8;
            var bb = UIX.Img(c.transform, "BookBox", new Color(1f, 0.68f, 0.25f, 0.14f), UISprites.Round); bb.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(-112, 56));
            infoBook = UIX.Text(bb.transform, "", 22, HC.Amber, R.Bold, TextAlignmentOptions.Center); infoBook.rectTransform.Fill();
            Btn(c.transform, "GOT IT!  (E)", () => infoOk = true, new Vector2(0, 40), 320, 60, HC.Green, new Vector2(0.5f, 0));
        }

        void CloseInfoForTour() { infoOk = true; }
        public IEnumerator ShowInfo(string title, string icon, Color col, string body, string book)
        {
            if (!info) BuildInfo();
            infoTitle.text = title; infoBody.text = body; infoBook.text = "BOOK:  " + book; infoIcon.sprite = Icons.Get(icon); infoIcon.color = col;
            UIState.Push(); Show(info, true); infoOk = false;
            Sfx.Play("notify", 0.6f);
            yield return new WaitForSecondsRealtime(0.4f);
            while (!infoOk) { if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return)) infoOk = true; yield return null; }
            Show(info, false); UIState.Pop();
        }

        void Show(CanvasGroup g, bool on)
        {
            if (on) { g.gameObject.SetActive(true); g.transform.SetAsLastSibling(); }
            g.blocksRaycasts = on; g.interactable = on;
            Tween.Fade(g, on ? 1 : 0, 0.3f, () => { if (!on && g) g.gameObject.SetActive(false); });
        }

        public void ShowTitle(bool on) { Show(title, on); }
        public bool PauseOpen => pause.blocksRaycasts;

        public void TogglePause(bool on)
        {
            if (on == PauseOpen) return;
            Show(pause, on);
            if (on) { UIState.Push(); Time.timeScale = 0; Game.Paused = true; }
            else { UIState.Pop(); Time.timeScale = 1; Game.Paused = false; }
        }

        public void OpenData(bool fromPause)
        {
            dataFromPause = fromPause;
            bool live; string sun = NasaData.SunStatus(out live);
            var api = NasaApi.I;
            var recent = NasaData.FlarePool(); string list = ""; for (int i = 0; i < Mathf.Min(4, recent.Count); i++) list += (i > 0 ? ",  " : "") + $"{recent[i].Class} ({recent[i].Date})";
            dataStatus.text = (live ? "● LIVE  " : "● ARCHIVED  ") + sun + "   ·   Real flares used: " + list + (api != null && api.CmeCount30d >= 0 ? $"   ·   {api.CmeCount30d} solar plasma clouds (CMEs) in 30 days" : "");
            dataStatus.color = live ? HC.Green : HC.Amber;
            if (!Game.InMenu && !fromPause && !PauseOpen) UIState.Push();
            RefreshLive();
            if (NasaLive.I && !liveHooked) { liveHooked = true; NasaLive.I.Updated += RefreshLive; }
            Show(data, true);
        }
        void CloseData()
        {
            Show(data, false);
            if (!Game.InMenu && !dataFromPause && !PauseOpen) UIState.Pop();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (controls.blocksRaycasts) { Show(controls, false); return; }
                if (data.blocksRaycasts) { CloseData(); return; }
                if (!Game.InMenu && !UIState.Cinematic && !puzzle.blocksRaycasts && !ending.blocksRaycasts) TogglePause(!PauseOpen);
            }
        }

        // ---------------- puzzle flow ----------------
        public IEnumerator RunPuzzle(RocketPart part, Action<bool> done)
        {
            var pool = new List<Puzzle>(Puzzles.Sets[part.Puzzle]);
            for (int i = pool.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
            var set = pool.GetRange(0, Mathf.Min(2, pool.Count)); // 2 random questions each game
            UIState.Push(); Show(puzzle, true);
            pzTitle.text = part.Name.ToUpper();
            pzTitle.color = part.Color;
            for (int qi = 0; qi < set.Count; qi++)
            {
                var pz = set[qi]; curPz = pz;
                pzCounter.text = $"LOCK {qi + 1} / {set.Count}";
                pzQ.text = pz.Q;
                pzFeedback.text = ""; pzNext.gameObject.SetActive(false);
                for (int i = 0; i < 4; i++) { pzBtns[i].t.text = $"<color=#7FE3FF>{i + 1}</color>   " + pz.A[i]; pzBtns[i].b.interactable = true; pzBtns[i].bg.color = new Color(0.06f, 0.08f, 0.11f, 0.75f); pzBtns[i].bg.GetComponent<UIButtonFx>().SetBase(pzBtns[i].bg.color); pzBtns[i].bg.GetComponent<UIButtonFx>().Locked = false; }
                bool first = true;
                while (true)
                {
                    pzChosen = -1;
                    while (pzChosen < 0) yield return null;
                    var btn = pzBtns[pzChosen];
                    if (pzChosen == pz.Correct)
                    {
                        Total++; if (first) FirstTry++;
                        var good = new Color(0.1f, 0.4f, 0.2f, 0.95f); btn.bg.color = good; btn.bg.GetComponent<UIButtonFx>().SetBase(good);
                        foreach (var b in pzBtns) { b.b.interactable = false; b.bg.GetComponent<UIButtonFx>().Locked = true; }
                        pzFeedback.text = $"<color={HC.Hex(HC.Green)}><b>CORRECT!</b></color>  {pz.Explain}";
                        Sfx.Play("success", 0.8f);
                        pzNext.gameObject.SetActive(true); pzContinue = false;
                        while (!pzContinue) yield return null;
                        break;
                    }
                    else
                    {
                        first = false;
                        var bad = new Color(0.45f, 0.08f, 0.06f, 0.95f); btn.bg.color = bad; btn.bg.GetComponent<UIButtonFx>().SetBase(bad);
                        btn.b.interactable = false; btn.bg.GetComponent<UIButtonFx>().Locked = true;
                        pzFeedback.text = $"<color={HC.Hex(HC.Amber)}><b>Not quite — try again!</b></color>  Hint: {pz.Hint}";
                        Sfx.Play("error", 0.6f);
                    }
                }
            }
            Show(puzzle, false); UIState.Pop();
            done?.Invoke(true);
        }
        void PzChoose(int i) { if (pzChosen < 0 && pzBtns[i].b.interactable) pzChosen = i; }
        Puzzle curPz;
        public void TourAnswer() { if (curPz != null) PzChoose(curPz.Correct); }
        public void TourContinue() { pzContinue = true; }
        void LateUpdate()
        {
            if (!puzzle.blocksRaycasts) return;
            for (int i = 0; i < 4; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) PzChoose(i);
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) && pzNext.gameObject.activeSelf) pzContinue = true;
        }

        public void ShowEnding(string stats)
        {
            endStats.text = stats;
            Game.InMenu = true; UIState.Refresh();
            Show(ending, true);
        }
        public void HideAll()
        {
            foreach (var g in new[] { title, pause, puzzle, data, ending, controls }) { g.alpha = 0; g.blocksRaycasts = false; g.interactable = false; g.gameObject.SetActive(false); }
        }
    }
}
