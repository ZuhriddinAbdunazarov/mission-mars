using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MissionMars
{
    /// <summary>The story: Prologue (crash) → Act I (salvage, observatory, radiation, night) → Act II (MOXIE, monitor,
    /// handbook) → Act III (four modules + questions) → Finale (launch window).</summary>
    public class Director : MonoBehaviour
    {
        public static int PendingChapter = -1;
        MenuUI menu; PauseUI pause; TelescopeView telescope;
        int days = 780; int sol = 1;
        readonly HashSet<string> answered = new HashSet<string>();
        readonly HashSet<string> factsShown = new HashSet<string>();
        bool running;
        Coroutine story;

        PlayerController P => Game.Player;
        HUD Hud => Game.Hud;
        SuitSystems Suit => Game.Suit;
        Cinema Cin => Cinema.I;

        public void Init(MenuUI m, PauseUI p, TelescopeView t)
        {
            menu = m; pause = p; telescope = t;
            menu.OnStart = ch => { if (!running) story = StartCoroutine(Begin(ch)); };
            menu.OnQuit = () => { Application.Quit(); };
            pause.OnRestart = () => { PendingChapter = 0; Reload(); };
            pause.OnMenu = () => { PendingChapter = -1; Reload(); };
            Suit.OnO2Empty = () => StartCoroutine(O2Rescue());
        }

        void Reload() { Time.timeScale = 1; UIState.Reset(); SceneManager.LoadScene(0); }

        // ================================================================== MENU
        public IEnumerator Menu()
        {
            Game.InMenu = true; UIState.Refresh();
            P.Frozen = true;
            Hud.ShowHUD(false, 0);
            SpaceSet.I.Enter(P.Cam);
            P.Cam.transform.SetParent(null);
            SpaceSet.I.MenuPose();
            AudioManager.I.Music("music_ambient", 0.55f, 2f);
            if (PendingChapter >= 0) { int ch = PendingChapter; PendingChapter = -1; story = StartCoroutine(Begin(ch)); yield break; }
            menu.Show(true);
            yield return Cin.FromBlack(1.6f);
            while (Game.InMenu) { if (!running) SpaceSet.I.MenuTick(); yield return null; }
        }

        IEnumerator Begin(int chapter)
        {
            running = true;
            menu.Show(false);
            if (HandbookUI.I.IsOpen) HandbookUI.I.Close();
            yield return Cin.ToBlack(0.8f);
            Game.InMenu = false; UIState.Refresh();
            if (chapter == 0)
            {
                UIState.Cinematic = true; UIState.Refresh();
                Cin.Letterbox(true, 0.01f);
                yield return SpaceSet.I.Intro();
                Cin.Letterbox(false, 0.01f);
                UIState.Cinematic = false;
            }
            SpaceSet.I.Exit(P.Cam);
            P.Cam.transform.SetParent(P.Head, false); P.Cam.transform.localPosition = Vector3.zero; P.Cam.transform.localRotation = Quaternion.identity;
            P.Cam.fieldOfView = 68;
            Prepare(Mathf.Max(1, chapter));
            if (chapter <= 1) yield return ActI();
            if (chapter <= 2) yield return ActII();
            if (chapter <= 3) yield return ActIII();
            yield return Finale(chapter >= 4);
        }

        void Update()
        {
            if (Game.InMenu || UIState.Cinematic) return;
            if (Input.GetKeyDown(KeyCode.Escape) && !HandbookUI.I.IsOpen && !QuestionOpen) { if (pause.IsOpen) pause.Close(); else pause.Open(); }
            if (Input.GetKeyDown(KeyCode.H) && !pause.IsOpen && !HandbookUI.I.IsOpen && !QuestionOpen)
            {
                if (Game.HasHandbook) HandbookUI.I.Open();
                else Hud.Toast("You haven't found the Handbook yet", Pal.Warn);
            }
        }
        bool QuestionOpen;

        // ================================================================== helpers
        IEnumerator WaitUse(Interactable i)
        {
            bool used = false; i.enabled = true; i.gameObject.SetActive(true);
            i.OnUse = () => used = true;
            while (!used) yield return null;
            i.OnUse = null;
        }

        IEnumerator Ask(string module, IList<Question> qs)
        {
            QuestionOpen = true;
            yield return QuestionUI.I.Ask(module, qs, q => { answered.Add(q.Id); RefreshChecklist(); });
            QuestionOpen = false;
        }

        void FactOnce(string key, string title, string body)
        {
            if (factsShown.Contains(key)) return;
            factsShown.Add(key); Hud.Fact(title, body);
        }

        void SetDays(int d) { days = d; Hud.SetClock(d); if (World.Monitor) World.Monitor.Days = d; }
        void SetSol(int s) { sol = s; Hud.SetSol($"SOL {s}  ·  JEZERO CRATER, MARS"); if (World.Monitor) World.Monitor.SetSol($"SOL {s}"); }

        IEnumerator CountDays(int to, float dur)
        {
            int from = days; float t = 0;
            while (t < dur) { t += Time.deltaTime; SetDays(Mathf.RoundToInt(Mathf.Lerp(from, to, Tween.EaseInOut(t / dur)))); if (Random.value < 0.3f) Sfx.Play("type", 0.15f); yield return null; }
            SetDays(to);
        }

        void RefreshChecklist()
        {
            var items = new List<(string, bool)>();
            foreach (var q in HandbookContent.Questions)
            {
                string label = q.Id switch
                {
                    "moxie1" => "MOXIE · Martian air",
                    "moxie2" => "MOXIE · Electrolysis",
                    "valve1" => "Fuel valve · Methane",
                    "valve2" => "Fuel valve · Escape velocity",
                    "board1" => "Computer · Radiation",
                    "board2" => "Computer · Bit flips",
                    "tracker1" => "Star tracker · Stars",
                    "tracker2" => "Star tracker · Sol length",
                    "antenna1" => "Antenna · Light delay",
                    "antenna2" => "Antenna · Relay orbiter",
                    _ => "Launch · Window timing",
                };
                items.Add((label, answered.Contains(q.Id)));
            }
            Hud.SetChecklist(items, $"MISSION CHECKLIST  ·  {answered.Count}/{HandbookContent.Questions.Length}");
        }

        void SetOutside(bool outside)
        {
            Suit.Outside = outside;
            Game.Sky.Mode = outside ? SkyMode.Surface : SkyMode.Interior;
            World.Interior.SetActive(!outside || World.Interior.activeSelf);
            if (World.AmbientDust) World.AmbientDust.gameObject.SetActive(outside);
            AudioManager.I.FadeSource(AudioManager.I.Wind, outside ? 0.55f : 0.06f, 1.5f);
            AudioManager.I.FadeSource(AudioManager.I.Hum, outside ? 0f : (Suit.MoxieOnline ? 0.35f : 0.05f), 1.5f);
        }

        /// <summary>Put the world in the state expected at the start of a chapter (for chapter select).</summary>
        void Prepare(int ch)
        {
            Hud.ShowHUD(true, 0.01f);
            Suit.Active = false;
            World.Interior.SetActive(true);
            if (ch >= 2)
            {
                foreach (var s in World.Salvage) s.SetActive(false);
                World.ObsBuilt.SetActive(true); World.ObsBlueprint.SetActive(false);
            }
            if (ch >= 3)
            {
                PowerHab(true);
                World.HandbookProp.SetActive(false);
                Game.HasHandbook = true;
                answered.Add("moxie1"); answered.Add("moxie2");
                Hud.VisorOn = true; Hud.SetCracked(false);
                SetDays(779); SetSol(2);
                Hud.ShowClock(true);
            }
            if (ch >= 4)
            {
                foreach (var p in World.Parts) { p.Found = true; p.Installed = true; }
                foreach (var q in HandbookContent.Questions) if (q.Id != "launch1") answered.Add(q.Id);
                foreach (var s in World.RocketSlots) { s.SetActive(true); s.GetComponent<Renderer>().material = R.Emissive(Color.black, new Color(0.3f, 2f, 0.8f)); }
                SetDays(32); SetSol(749);
            }
        }

        void PowerHab(bool instant)
        {
            Suit.MoxieOnline = true;
            World.MoxieGlow.SetActive(true); World.MoxieLight.intensity = 3;
            foreach (var l in World.InteriorLights) l.intensity = 2.4f;
            World.EmergencyLight.GetComponent<Blink>().SetBase(0); World.EmergencyLight.intensity = 0;
            if (World.Monitor) Game.Runner.StartCoroutine(World.Monitor.PowerOn());
            Hud.VisorOn = false;
        }

        // ================================================================== ACT I
        IEnumerator ActI()
        {
            SetSol(1); SetDays(780);
            Game.Sky.TimeOfDay = 0.60f; Game.Sky.Mode = SkyMode.Surface; Game.Sky.Apply();
            P.Teleport(World.SpawnPos, World.SpawnYaw, -70);
            P.Frozen = true; SetOutside(true);
            Suit.O2 = 78; Suit.Sickness = 0; Suit.Burns = 0;
            Hud.VisorOn = true; Hud.SetCracked(false);
            UIState.Cinematic = true; UIState.Refresh();
            Cin.SetBlack(1);
            yield return Cin.TitleCard("ACT I", "THE CRASH", "Sol 1  ·  Jezero Crater, Mars");
            AudioManager.I.Music("music_ambient", 0.4f, 4f);
            Sfx.Play("breath", 0.7f);
            yield return Cin.FromBlack(3f);
            // get up
            float t = 0;
            while (t < 3.2f) { t += Time.deltaTime; P.Teleport(World.SpawnPos, World.SpawnYaw, Mathf.Lerp(-70, 4, Tween.EaseInOut(t / 3.2f))); yield return null; }
            yield return Hud.Say("SUIT AI", "Suit systems rebooting...  Oxygen: 78%.  Visor integrity: COMPROMISED.", 1.2f);
            Hud.SetCracked(true); Sfx.Play("thud", 0.6f); P.Shake(0.4f, 0.4f);
            Hud.ShowVitals(true); Suit.Active = true;
            yield return Hud.Say("ASTRONAUT", "Mission Control, do you copy?  ...Nothing. The comms array is gone.", 1.2f);
            UIState.Cinematic = false; UIState.Refresh(); P.Frozen = false;
            Hud.SetHints("WASD MOVE   ·   SHIFT SPRINT   ·   SPACE JUMP   ·   E INTERACT");
            Hud.SayAsync("SUIT AI", "Recommendation: salvage usable materials from the wreckage.", 2.5f);

            // ---- Scene 2: scavenging
            int got = 0, need = World.Salvage.Count;
            Hud.SetObjective("Salvage useful parts from the wreckage", $"0 / {need}");
            var markers = new Dictionary<GameObject, Marker>();
            foreach (var s in World.Salvage)
            {
                var go = s; var it = go.GetComponent<Interactable>();
                markers[go] = Hud.AddMarker(go.transform.position, it.Label, Pal.Accent);
                it.OnUse = () =>
                {
                    got++; Sfx.Play("pickup", 0.8f);
                    FX.BurstSparks(go.transform.position + Vector3.up * 0.4f, new Color(1f, 0.7f, 0.3f));
                    Hud.Toast("+ " + it.Label, Pal.Accent);
                    Hud.RemoveMarker(markers[go]);
                    Hud.SetProgress($"{got} / {need}");
                    go.SetActive(false);
                    switch (it.Label)
                    {
                        case "Hull Plate": FactOnce("hull", "Spacecraft hulls", "Rocket hulls are mostly aluminium alloys — light but strong. On Mars, metal sheets can block wind-blown dust, but NOT high-energy radiation."); break;
                        case "Optical Lens": FactOnce("lens", "Lenses & telescopes", "A telescope collects light with a lens (refractor) or a mirror (reflector). The bigger the lens, the more light it gathers and the fainter the objects you can see."); break;
                        case "Wiring Bundle": FactOnce("wire", "Cold electronics", "At −80 °C plastic insulation turns brittle and batteries lose power. Mars hardware must be heated — rovers use small heaters to survive the night."); break;
                        case "Solar Panel": FactOnce("solar", "Sunlight on Mars", "Mars receives only about 43% of the sunlight Earth gets, so Martian solar panels must be bigger — and kept free of dust."); break;
                    }
                };
            }
            while (got < need) yield return null;
            Hud.SetHints("H HANDBOOK (when found)   ·   ESC PAUSE");
            yield return Hud.Say("ASTRONAUT", "Hull metal, lenses, wire... Enough for an observatory. I need to see where I am.", 1f);

            // ---- Scene 3: observatory
            World.ObsBlueprint.SetActive(true);
            var obsM = Hud.AddMarker(World.ObsBlueprint.transform.position + Vector3.up * 2, "Observatory site", Pal.Cyan);
            Hud.SetObjective("Build an observatory on the ridge");
            yield return WaitUse(World.ObsBuild);
            Hud.RemoveMarker(obsM);
            yield return BuildObservatory();
            FactOnce("obs", "Astronomy from Mars", "Mars has thin air and few clouds — great for stargazing. From here Earth looks like a bright blue 'evening star', just like Venus does from Earth.");
            Hud.SetObjective("Look through the telescope");
            Game.Sky.TimeOfDay = Mathf.Max(Game.Sky.TimeOfDay, 0.64f);
            yield return WaitUse(World.TelescopeUse);
            World.TelescopeUse.enabled = false;
            Game.Sky.TimeOfDay = 0.66f;
            yield return telescope.Sequence(P, World.TelescopeEye);

            // ---- Scene 4: radiation & nightfall
            Sfx.Play("alarm", 0.8f);
            Hud.Warn("SOLAR PARTICLE EVENT", 6f);
            var alarm = AudioManager.I.Loop("alarm", 0.35f);
            t = 0; while (t < 2.5f) { t += Time.deltaTime; Suit.SolarEvent = t / 2.5f; PostFX.Chroma.intensity.Override(0.06f + t / 2.5f * 0.5f); yield return null; }
            yield return Hud.Say("SUIT AI", "WARNING: extreme radiation dose. UV is burning through the cracked visor. Seek shelter immediately!", 0.8f);
            FactOnce("rad", "Why radiation is deadly here", "Earth's magnetic field and thick air shield us. Mars lost its global magnetic field billions of years ago — solar particles hit the surface almost unblocked.");
            Hud.SayAsync("SUIT AI", "Detected: supply habitat pre-landed by an earlier cargo mission. Go!", 2f);
            var habM = Hud.AddMarker(World.HabDoor.transform.position + Vector3.up, "Habitat", Pal.Good);
            Hud.SetObjective("Reach the habitat before nightfall");
            Destroy(alarm.gameObject, 6f);
            var sunset = StartCoroutine(Game.Sky.Timelapse(0.8f, 40f));
            Game.Runner.StartCoroutine(SunsetLines());
            yield return WaitUse(World.HabDoor);
            StopCoroutine(sunset);
            Hud.RemoveMarker(habM);
            Sfx.Play("thud", 0.6f);
            yield return Cin.ToBlack(0.7f);
            Suit.SolarEvent = 0; PostFX.Chroma.intensity.Override(0.06f);
            Game.Sky.TimeOfDay = 0.82f;
            Hud.ClearObjective();
        }

        IEnumerator SunsetLines()
        {
            yield return new WaitForSeconds(9f);
            if (!Suit.Outside) yield break;
            Hud.SayAsync("ASTRONAUT", "The sun is setting... and the sky around it is turning blue. Mars sunsets are blue.", 2f);
            FactOnce("blue", "Blue sunsets", "Fine Martian dust scatters blue light forward, toward the viewer, so the sky around the setting Sun glows blue — the opposite of Earth.");
            yield return new WaitForSeconds(14f);
            if (!Suit.Outside) yield break;
            Hud.SayAsync("SUIT AI", "Outside temperature falling below −60 °C. Suit heaters at maximum.", 2f);
        }

        IEnumerator BuildObservatory()
        {
            P.Frozen = true;
            World.ObsBlueprint.SetActive(false);
            World.ObsBuilt.SetActive(true);
            var pieces = World.ObsPieces;
            var finals = new Vector3[pieces.Length];
            for (int i = 0; i < pieces.Length; i++) { finals[i] = pieces[i].localScale; pieces[i].localScale = Vector3.zero; }
            for (int i = 0; i < pieces.Length; i++)
            {
                var pc = pieces[i]; var fs = finals[i];
                Sfx.Play("build", 0.7f, Random.Range(0.9f, 1.1f));
                FX.BurstSparks(pc.position, new Color(1f, 0.75f, 0.35f), 25);
                float t = 0; while (t < 0.45f) { t += Time.deltaTime; pc.localScale = fs * Tween.Back(t / 0.45f); yield return null; }
                pc.localScale = fs;
                P.Shake(0.15f, 0.15f);
            }
            Sfx.Play("success", 0.6f);
            Hud.Toast("Observatory built", Pal.Good);
            P.Frozen = false;
        }

        // ================================================================== ACT II
        IEnumerator ActII()
        {
            SetSol(1);
            World.Interior.SetActive(true);
            SetOutside(false);
            Game.Sky.TimeOfDay = 0.85f;
            P.Teleport(World.InteriorSpawn, World.InteriorYaw, 0);
            Suit.O2 = Mathf.Min(Suit.O2, 34); Suit.Sickness = Mathf.Max(Suit.Sickness, 55); Suit.Burns = Mathf.Max(Suit.Burns, 0.5f);
            Suit.Active = true; Hud.ShowVitals(true);
            foreach (var l in World.InteriorLights) l.intensity = 0;
            UIState.Cinematic = true; UIState.Refresh(); P.Frozen = true;
            yield return Cin.TitleCard("ACT II", "LIFE SUPPORT", "Sol 1, night  ·  Inside the habitat");
            yield return Cin.FromBlack(1.5f);
            UIState.Cinematic = false; UIState.Refresh(); P.Frozen = false;
            yield return Hud.Say("ASTRONAUT", "Dark... and freezing. My oxygen is running out.", 0.8f);

            // ---- Scene 5: MOXIE
            var mM = Hud.AddMarker(World.Moxie.transform.position + Vector3.up * 1.2f, "MOXIE", Pal.Cyan);
            Hud.SetObjective("Connect the MOXIE oxygen generator to the solar batteries");
            yield return WaitUse(World.Moxie);
            Hud.RemoveMarker(mM);
            World.Moxie.enabled = false;
            Sfx.Play("powerup", 0.8f);
            World.MoxieGlow.SetActive(true);
            FX.Mist(World.Moxie.transform.position + Vector3.up * 1.6f, World.Moxie.transform, new Color(0.7f, 0.9f, 1f, 0.5f));
            float t = 0;
            while (t < 2f) { t += Time.deltaTime; World.MoxieLight.intensity = t * 1.5f; foreach (var l in World.InteriorLights) l.intensity = Mathf.Lerp(0, 2.4f, t / 2); yield return null; }
            World.EmergencyLight.GetComponent<Blink>().SetBase(0); World.EmergencyLight.intensity = 0;
            Suit.MoxieOnline = true;
            AudioManager.I.FadeSource(AudioManager.I.Hum, 0.35f, 2f);
            FactOnce("moxie", "MOXIE is real!", "NASA's Perseverance rover carried MOXIE to Mars. It split Martian CO<sub>2</sub> to make oxygen — 122 grams in total, at up to 12 grams per hour.");
            yield return Hud.Say("SUIT AI", "MOXIE online. Converting Martian carbon dioxide into breathable oxygen.", 1f);
            yield return new WaitForSeconds(1.2f);
            Hud.VisorOn = false; Sfx.Play("breath", 0.9f);
            yield return Hud.Say("ASTRONAUT", "Helmet off... I can breathe. Now these burns.", 1f);

            Hud.SetObjective("Treat your radiation burns with the medical kit");
            var mk = Hud.AddMarker(World.Medkit.transform.position, "Medical Kit", Pal.Bad);
            yield return WaitUse(World.Medkit);
            Hud.RemoveMarker(mk); World.Medkit.enabled = false;
            Sfx.Play("success", 0.5f); Hud.Toast("Burns treated", Pal.Good);
            Tween.Run(2f, k => { Suit.Sickness = Mathf.Lerp(55, 8, k); Suit.Burns = Mathf.Lerp(0.5f, 0, k); });

            // ---- Scene 6: monitor + 2-year timer
            yield return new WaitForSeconds(0.6f);
            P.Frozen = true;
            Vector3 monPos = World.Monitor.transform.position;
            Quaternion from = P.Head.rotation; float tt = 0;
            yield return World.Monitor.PowerOn();
            World.Monitor.SetStatus("ESCAPE VEHICLE STATUS\n\n  FUEL PUMP VALVE ..... MISSING\n  FLIGHT COMPUTER ..... MISSING\n  STAR TRACKER ........ MISSING\n  RADIO ANTENNA ....... MISSING");
            World.Monitor.SetFeed(NasaApi.I ? NasaApi.I.FlareSummary() : "");
            P.LookAt(monPos);
            P.Frozen = false;
            Hud.ShowClock(true); SetDays(780);
            yield return Hud.Say("SUIT AI", "Earth and Mars line up for a return flight only about once every 26 months. Next launch window: 780 days.", 1.2f);
            FactOnce("window", "Launch windows", "Earth laps Mars in its orbit. The fuel-efficient path between them only lines up about every 26 months — miss it and you wait for the next one.");
            yield return Hud.Say("ASTRONAUT", "The escape rocket on the launch pad still works — but four modules are missing. The crash scattered them across the crater.", 1f);

            // ---- Scene 8: the Handbook
            Hud.SetObjective("Search the cargo crate");
            var cm = Hud.AddMarker(World.Crate.transform.position + Vector3.up, "Cargo crate", Pal.Accent);
            yield return WaitUse(World.Crate);
            Hud.RemoveMarker(cm); World.Crate.enabled = false;
            World.HandbookProp.SetActive(false);
            Game.HasHandbook = true;
            Sfx.Play("success", 0.6f);
            Hud.Toast("Field Handbook acquired", Pal.Good);
            yield return Hud.Say("ASTRONAUT", "\"The Colonist & Engineer's Field Handbook.\" Everything about surviving here... and fixing rockets.", 0.8f);
            HandbookUI.I.Open(0);
            while (HandbookUI.I.IsOpen) yield return null;
            Hud.SetHints("H HANDBOOK   ·   ESC PAUSE");

            // ---- Scene 9 tutorial: calibrate MOXIE
            yield return Hud.Say("SUIT AI", "Every recovered module must pass a calibration check. Use the Handbook to find the answers.", 0.8f);
            Hud.SetObjective("Calibrate MOXIE for full oxygen output");
            Hud.ShowChecklist(true); RefreshChecklist();
            World.Moxie.Verb = "Calibrate"; World.Moxie.HoldTime = 0;
            var mM2 = Hud.AddMarker(World.Moxie.transform.position + Vector3.up * 1.2f, "MOXIE", Pal.Cyan);
            yield return WaitUse(World.Moxie);
            Hud.RemoveMarker(mM2); World.Moxie.enabled = false;
            yield return Ask("MOXIE OXYGEN GENERATOR", new[] { HandbookContent.Questions[0], HandbookContent.Questions[1] });
            Hud.Toast("MOXIE at full output", Pal.Good);

            // rest
            Hud.SetObjective("Rest until sunrise");
            var bm = Hud.AddMarker(World.Bed.transform.position, "Bunk", Pal.Dim);
            yield return WaitUse(World.Bed);
            Hud.RemoveMarker(bm); World.Bed.enabled = false;
            yield return Cin.ToBlack(1f);
            Hud.ClearObjective();
            SetSol(2); SetDays(779);
        }

        // ================================================================== ACT III
        IEnumerator ActIII()
        {
            Game.Sky.TimeOfDay = 0.31f;
            World.Interior.SetActive(false);
            SetOutside(true);
            P.Teleport(World.HabOutsideSpawn, World.HabOutsideYaw, 0);
            Hud.VisorOn = true; Hud.SetCracked(false);
            Suit.O2 = 100; Suit.Active = true; Suit.Sickness = Mathf.Min(Suit.Sickness, 8); Suit.Burns = 0;
            Hud.ShowVitals(true); Hud.ShowClock(true); Hud.ShowChecklist(true); RefreshChecklist();
            Hud.SetHints("H HANDBOOK   ·   ESC PAUSE");
            foreach (var c in World.O2Cans)
            {
                var can = c; can.SetActive(true);
                var it = can.GetComponent<Interactable>();
                it.OnUse = () => { Suit.Refill(45); Sfx.Play("pickup", 0.7f); Hud.Toast("+45% Oxygen", Pal.Cyan); can.SetActive(false); };
            }
            World.RoverScan.OnUse = () => { Sfx.Play("beep"); Hud.Fact("Perseverance rover", "NASA's car-sized rover landed in Jezero Crater on 18 Feb 2021. It collects rock samples from an ancient lake bed and carried the MOXIE oxygen experiment.", 12); };
            World.HeliScan.OnUse = () => { Sfx.Play("beep"); Hud.Fact("Ingenuity helicopter", "On 19 April 2021 Ingenuity made the first powered, controlled flight on another planet. Planned for 5 flights, it flew 72!", 12); };

            UIState.Cinematic = true; UIState.Refresh(); P.Frozen = true;
            yield return Cin.TitleCard("ACT III", "THE QUEST", $"Sol {sol}  ·  Recover the four missing modules");
            AudioManager.I.Music("music_ambient", 0.4f, 3f);
            yield return Cin.FromBlack(1.5f);
            UIState.Cinematic = false; UIState.Refresh(); P.Frozen = false;
            yield return Hud.Say("SUIT AI", "Visor patched with a hull plate. Scanner shows four module beacons. O<sub>2</sub> canisters are marked in cyan.", 0.8f);
            FactOnce("o2", "Oxygen budget", "Your suit leaks a little oxygen every minute. Sprinting burns more. Grab the cyan O<sub>2</sub> canisters scattered around the crater.");

            int found = 0;
            foreach (var p in World.Parts)
            {
                var part = p;
                if (part.Found) { found++; continue; }
                part.Go.SetActive(true);
                part.Marker = Hud.AddMarker(part.Go.transform.position + Vector3.up * 1.5f, part.Name, Pal.Accent);
                part.Use.OnUse = () => { if (!busy) StartCoroutine(Recover(part)); };
            }
            Hud.SetObjective("Recover the missing rocket modules", $"{found} / 4");
            var rm = Hud.AddMarker(World.RoverScan.transform.position + Vector3.up * 2, "Perseverance", new Color(0.8f, 0.85f, 0.9f));
            while (true)
            {
                found = 0; foreach (var p in World.Parts) if (p.Found) found++;
                if (found >= 4) break;
                yield return null;
            }
            Hud.RemoveMarker(rm);
            yield return Hud.Say("ASTRONAUT", "That's all four modules. Back to the launch pad!", 0.8f);

            // install
            World.RocketInstall.enabled = true;
            var rk = Hud.AddMarker(World.Rocket.position + Vector3.up * 3, "Escape rocket", Pal.Good);
            Hud.SetObjective("Install the modules on the escape rocket");
            yield return WaitUse(World.RocketInstall);
            World.RocketInstall.enabled = false;
            Hud.RemoveMarker(rk);
            for (int i = 0; i < 4; i++)
            {
                var s = World.RocketSlots[i]; s.SetActive(true);
                Sfx.Play("build", 0.8f); FX.BurstSparks(s.transform.position, new Color(0.4f, 1f, 0.6f), 30);
                s.GetComponent<Renderer>().material = R.Emissive(Color.black, new Color(0.3f, 2f, 0.8f));
                World.Parts[i].Installed = true;
                Hud.Toast(World.Parts[i].Name + " installed", Pal.Good);
                yield return new WaitForSeconds(0.6f);
            }
            if (World.Monitor) World.Monitor.SetStatus("ESCAPE VEHICLE STATUS\n\n  ALL MODULES ......... INSTALLED\n  READY FOR LAUNCH WINDOW");
            Sfx.Play("success", 0.7f);
        }

        bool busy;
        IEnumerator Recover(Part part)
        {
            busy = true;
            Sfx.Play("beep");
            yield return Ask(part.Name.ToUpper(), part.Qs);
            part.Found = true;
            FX.BurstSparks(part.Go.transform.position + Vector3.up * 0.6f, new Color(0.4f, 1f, 0.6f), 50);
            Sfx.Play("pickup", 0.9f);
            Hud.RemoveMarker(part.Marker);
            part.Go.SetActive(false);
            int found = 0; foreach (var p in World.Parts) if (p.Found) found++;
            Hud.Toast("+ " + part.Name, Pal.Good);
            Hud.SetProgress($"{found} / 4");
            // repairs take time: the mission clock advances
            int target = found >= 4 ? 32 : days - Random.Range(150, 190);
            SetSol(sol + (days - target));
            yield return CountDays(target, 2.2f);
            Hud.Toast($"Repairs took {(found >= 4 ? "months" : "weeks")}  ·  {days} days to launch window", Pal.Cyan);
            busy = false;
        }

        // ================================================================== FINALE
        IEnumerator Finale(bool direct)
        {
            if (direct || !Suit.Outside)
            {
                World.Interior.SetActive(false); SetOutside(true);
                Game.Sky.TimeOfDay = 0.45f;
                var c = World.LaunchConsole.transform.position;
                var spot = c + (c - World.Rocket.position).normalized * 4f; spot.y = MarsTerrain.H(spot.x, spot.z) + 0.1f;
                P.Teleport(spot, 0, 0); P.LookAt(c + Vector3.up);
                Hud.VisorOn = true; Hud.SetCracked(false);
                AudioManager.I.Music("music_ambient", 0.4f, 2f);
                yield return Cin.TitleCard("FINALE", "THE LAUNCH WINDOW", $"Sol {sol}  ·  All modules installed");
            }
            yield return Cin.FromBlack(0.8f);
            Suit.Active = true; Hud.ShowVitals(true); Hud.ShowClock(true); Hud.ShowChecklist(true); RefreshChecklist();
            SetDays(days > 40 ? 32 : days);
            World.LaunchConsole.enabled = true;
            var cm = Hud.AddMarker(World.LaunchConsole.transform.position + Vector3.up * 1.5f, "Launch console", Pal.Good);
            Hud.SetObjective("Program the launch at the console");
            yield return WaitUse(World.LaunchConsole);
            Hud.RemoveMarker(cm); World.LaunchConsole.enabled = false;
            yield return Ask("LAUNCH COMPUTER", new[] { HandbookContent.Questions[10] });

            // waiting for the window: days fly by
            Hud.SetObjective("Wait for the launch window", "");
            UIState.Cinematic = true; UIState.Refresh(); P.Frozen = true;
            Cin.Letterbox(true);
            yield return Hud.Say("SUIT AI", "Launch program accepted. Waiting for planetary alignment...", 0.5f);
            float t = 0; float startTod = Game.Sky.TimeOfDay; int startDays = days;
            while (t < 7f)
            {
                t += Time.deltaTime; float k = t / 7f;
                Game.Sky.TimeOfDay = startTod + k * k * 6f; // days spin by
                SetDays(Mathf.CeilToInt(Mathf.Lerp(startDays, 0, k)));
                if (Random.value < 0.25f) Sfx.Play("type", 0.15f);
                yield return null;
            }
            Game.Sky.TimeOfDay = 0.74f;
            SetDays(0);
            Hud.Warn("LAUNCH WINDOW OPEN", 4f);
            Sfx.Play("success", 0.9f);
            AudioManager.I.Music("music_hope", 0.6f, 2f);
            yield return Hud.Say("ASTRONAUT", "Launch window open. Time to go home.", 1f);

            // ---- Scene 10: liftoff
            yield return Cin.ToBlack(0.8f);
            Hud.ShowHUD(false, 0.2f); Hud.VisorOn = false;
            var cam = P.Cam; cam.transform.SetParent(null);
            Vector3 pad = World.Rocket.position;
            Vector3 camPos = pad + new Vector3(-38, 6, -30); camPos.y = MarsTerrain.H(camPos.x, camPos.z) + 2.5f;
            cam.transform.position = camPos; cam.transform.LookAt(pad + Vector3.up * 12); cam.fieldOfView = 50;
            yield return Cin.FromBlack(1f);
            yield return new WaitForSeconds(0.6f);
            Cin.Caption("3 ...", 0.8f); Sfx.Play("beep", 0.8f); yield return new WaitForSeconds(1f);
            Cin.Caption("2 ...", 0.8f); Sfx.Play("beep", 0.8f); yield return new WaitForSeconds(1f);
            Cin.Caption("1 ...", 0.8f); Sfx.Play("beep", 0.8f); yield return new WaitForSeconds(1f);
            Cin.Caption("IGNITION", 2f);
            var rb = Util.Bounds(World.Rocket.gameObject);
            var exh = FX.Exhaust(World.Rocket, World.Rocket.InverseTransformPoint(new Vector3(rb.center.x, rb.min.y + 0.5f, rb.center.z)), 1.6f);
            var el = new GameObject("IgnitionLight").AddComponent<Light>(); el.transform.SetParent(World.Rocket, false); el.transform.position = new Vector3(rb.center.x, rb.min.y - 1, rb.center.z);
            el.type = LightType.Point; el.range = 60; el.intensity = 12; el.color = new Color(1f, 0.6f, 0.25f);
            FX.DustBlast(new Vector3(rb.center.x, rb.min.y, rb.center.z), 2.2f);
            var dustLoop = FX.DustBlast(new Vector3(rb.center.x, rb.min.y, rb.center.z), 1.5f, true);
            var roar = AudioManager.I.Loop("rocket", 0f); AudioManager.I.FadeSource(roar, 1f, 1f);
            Sfx.Play("explosion", 0.6f);
            t = 0; float y0 = World.Rocket.position.y;
            while (t < 9f)
            {
                t += Time.deltaTime;
                float h = t < 1.5f ? 0 : Mathf.Pow(t - 1.5f, 2.2f) * 2.2f;
                World.Rocket.position = new Vector3(World.Rocket.position.x, y0 + h, World.Rocket.position.z);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(World.Rocket.position + Vector3.up * 10 - cam.transform.position), Time.deltaTime * 3);
                cam.transform.position = camPos + Random.insideUnitSphere * Mathf.Lerp(0.25f, 0.05f, t / 9f);
                if (t > 3) { var em = dustLoop.emission; em.rateOverTime = Mathf.Lerp(60, 0, (t - 3) / 3); }
                yield return null;
            }
            yield return Cin.ToBlack(1.2f);
            AudioManager.I.FadeSource(roar, 0, 1f); Destroy(roar.gameObject, 1.5f);

            // space
            SpaceSet.I.Enter(cam);
            Cin.Letterbox(true, 0.01f);
            Cin.Caption("Leaving Mars  ·  Next stop: Earth", 6f);
            yield return SpaceSet.I.Ending();
            Cin.Letterbox(false);
            UIState.Cinematic = false; Game.InMenu = true; UIState.Refresh();
            int total = HandbookContent.Questions.Length;
            ResultsUI.Show(Mathf.Min(QuestionUI.I.CorrectFirstTry, total), total, QuestionUI.I.Mistakes, Suit.DoseMSv + 210f,
                () => { PendingChapter = -1; Reload(); }, () => { PendingChapter = 0; Reload(); });
            while (true) { SpaceSet.I.Ship.position += SpaceSet.I.Ship.forward * Time.deltaTime * 80; yield return null; }
        }

        IEnumerator O2Rescue()
        {
            if (!Suit.Outside || UIState.Cinematic) yield break;
            P.Frozen = true;
            Hud.Warn("OXYGEN DEPLETED", 3f);
            yield return Cin.ToBlack(1.2f);
            P.Teleport(World.HabOutsideSpawn, World.HabOutsideYaw, 0);
            Suit.O2 = 70;
            yield return new WaitForSeconds(0.5f);
            yield return Cin.FromBlack(1.2f);
            P.Frozen = false;
            Hud.SayAsync("SUIT AI", "Emergency return protocol: you were guided back to the habitat. Watch your oxygen — collect canisters!", 2f);
        }
    }
}
