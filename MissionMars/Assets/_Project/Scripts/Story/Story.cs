using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MissionMars
{
    /// <summary>The whole mission, start to finish:
    /// title → launch from Earth → crash on Mars → wake up → solar storm, run to the bunker →
    /// salvage MOXIE + ice machine → 2-year timer starts → chase rocket parts, solve puzzles → repair rocket → fly home.</summary>
    public class Story : MonoBehaviour
    {
        Camera cineCam;
        PlayerController player; Survival suit; Hud hud; Screens screens; Robot bot;
        public int DaysLeft = 780; float dayAcc; public bool ClockRunning; public float SecondsPerDay = 0.9f;
        public bool FlareActive; public int WindowsMissed; List<int> hazardQueue = new List<int>(); float hazardTimer = 999;
        public int Sol = 1;
        public FlareEvent CurrentFlare; readonly List<FlareEvent> pool = new List<FlareEvent>();
        bool inside, busy, partsHunt, storm2Done;
        int partsInstalled;
        Waypoint wpBunker, wpMoxie, wpWater, wpRocket;
        readonly Dictionary<RocketPart, Waypoint> partWps = new Dictionary<RocketPart, Waypoint>();
        bool lookedEarth;
        public static bool QuickStart;

        public void Init(Camera cine, Screens s) { cineCam = cine; screens = s; Game.Story = this; }

        public string ClockText()
        {
            if (!ClockRunning && DaysLeft >= 780) return $"SOL {Sol}  ·  <color={HC.Hex(HC.Amber)}>CRASH SITE</color>";
            return DaysLeft > 0 ? $"SOL {Sol}  ·  EARTH WINDOW <color={HC.Hex(HC.Cyan)}>T-{DaysLeft} DAYS</color>" : $"SOL {Sol}  ·  <color={HC.Hex(HC.Green)}>LAUNCH WINDOW OPEN</color>";
        }
        public string SunText()
        {
            bool live; string s = NasaData.SunStatus(out live);
            return (live ? "● LIVE NASA DONKI  " : "● NASA DONKI (archived)  ") + s.Replace("Real Sun: ", "").Replace("Sun data: archived", "real flares 2024");
        }

        void Update()
        {
            if (ClockRunning && DaysLeft > 0 && !Game.Paused && !UIState.Modal)
            {
                dayAcc += Time.deltaTime * (FlareActive && inside ? 4f : 1f); // sheltering from a flare costs time
                while (dayAcc > SecondsPerDay) { dayAcc -= SecondsPerDay; Tick(); }
            }
            UpdateTimerHolo();
            if (Game.InMenu && SpaceSet.I && SpaceSet.I.gameObject.activeSelf && cineCam && cineCam.enabled) SpaceSet.I.MenuTick();
            if (partsHunt && player && !busy) PartsUpdate();
        }

        void Tick()
        {
            DaysLeft--; Sol++;
            if (DaysLeft <= 0 && partsInstalled < MarsWorld.Parts.Length) StartCoroutine(MissedWindow());
        }

        IEnumerator QuitWithFlyAway(SpaceSet space)
        {
            screens.ShowTitle(false);
            Sfx.Play("whoosh", 0.5f);
            yield return space.FlyAway(1.3f);
            Application.Quit();
        }

        IEnumerator MissedWindow()
        {
            WindowsMissed++; DaysLeft = 780;
            hud.Warn("LAUNCH WINDOW MISSED", 5);
            yield return hud.SayWait("Oh no, Earth moved too far away! The next trip home is in 26 months. Let's be faster this time!", 4);
        }

        void UpdateTimerHolo()
        {
            string s = DaysLeft > 0 ? $"<size=55%>EARTH LAUNCH WINDOW</size>\nT-{DaysLeft} DAYS\n<size=45%>{DaysLeft / 30.4f:0.0} MONTHS</size>" : "<size=55%>LAUNCH WINDOW</size>\nOPEN";
            if (MarsWorld.TimerText) MarsWorld.TimerText.text = s;
            if (MarsWorld.TimerInside) MarsWorld.TimerInside.text = ClockRunning || DaysLeft < 780 ? s : "<size=55%>NASA DATA TERMINAL</size>\n<size=40%>PRESS E</size>";
        }

        // ================================================================== main flow
        public IEnumerator Run()
        {
            UIState.Reset();
            pool.AddRange(NasaData.FlarePool());
            // ---- TITLE ----
            Game.InMenu = true; UIState.Refresh();
            var space = SpaceSet.I;
            space.Enter(cineCam); space.MenuPose();
            AudioManager.I.Music("music_ambient", 0.5f, 2f);
            bool play = false; screens.OnPlay = () => play = true;
            screens.OnRestart = () => SceneManager.LoadScene(0);
            screens.OnQuit = () => StartCoroutine(QuitWithFlyAway(space));
            if (!QuickStart)
            {
                screens.ShowTitle(true);
                yield return Cinema.I.FromBlack(1.5f);
                while (!play) yield return null;
                screens.ShowTitle(false);
                Sfx.Play("whoosh", 0.5f);
                yield return space.FlyAway(); // the astronaut flies off to start the mission
            }
            else Cinema.I.SetBlack(0);
            Game.InMenu = false; UIState.Cinematic = true; UIState.Refresh();
            Sfx.Play("whoosh", 0.6f);
            yield return Cinema.I.ToBlack(0.8f);
            AudioManager.I.StopMusic(0.8f);
            pool.Clear(); pool.AddRange(NasaData.FlarePool()); // DONKI may have arrived while on the title screen

            // ---- INTRO ----
            Cinema.I.Letterbox(true, 0.01f);
            yield return space.Intro();
            Cinema.I.Letterbox(false, 0.01f);
            space.Exit(cineCam);
            cineCam.enabled = false;

            // ---- WAKE UP ----
            yield return WakeUp();
            // ---- STORM → BUNKER ----
            yield return FirstStorm();
            // ---- SALVAGE ----
            yield return Salvage();
            // ---- TIMER + PARTS ----
            yield return PartsIntro();
            while (partsInstalled < MarsWorld.Parts.Length) yield return null;
            // ---- LAUNCH ----
            yield return Launch();
        }

        // ================================================================== wake up
        IEnumerator WakeUp()
        {
            var spawn = MarsWorld.G(MarsWorld.WakeXZ, 0.1f);
            player = PlayerController.Create(spawn, 0); Game.Player = player; Game.Cam = player.Cam;
            Story.ConfigCam(player.Cam);
            var inter = player.gameObject.AddComponent<Interactor>(); inter.P = player;
            suit = player.gameObject.AddComponent<Survival>(); Game.Suit = suit;
            suit.O2 = 64; suit.Water = 70; suit.Health = 72;
            suit.OnCollapse = why => StartCoroutine(Collapse(why));
            hud = Game.Hud; hud.Interactor = inter;
            bot = Robot.Create(spawn + new Vector3(30, 20, 30));
            Game.Sky.TimeOfDay = 0.36f; Game.Sky.Apply();
            AudioManager.I.Wind = AudioManager.I.Loop("wind", 0.35f);

            // lying on the ground, looking up at the sky
            player.Frozen = true;
            player.HeadHeight = 0.35f; player.ExtraRoll = 0;
            var crash = MarsWorld.G(MarsWorld.CrashXZ, 3);
            player.LookAt(spawn + new Vector3(0, 30, 6));
            Cinema.I.Eyes(0); Cinema.I.SetBlack(0);
            PostFX.Dof.active = true; PostFX.Dof.gaussianStart.Override(0.1f); PostFX.Dof.gaussianEnd.Override(1f); PostFX.Dof.gaussianMaxRadius.Override(1.5f);
            Sfx.Play("heartbeat", 0.9f);
            yield return new WaitForSeconds(1.6f);
            Sfx.Play("breath", 0.8f);
            // blink open, close, open
            yield return Lids(0, 0.35f, 0.7f); yield return Lids(0.35f, 0, 0.35f);
            yield return new WaitForSeconds(0.6f);
            Sfx.Play("heartbeat", 0.7f);
            yield return Lids(0, 0.7f, 0.9f); yield return Lids(0.7f, 0.25f, 0.4f); yield return Lids(0.25f, 1f, 1.2f);
            Tween.Run(3f, t => { PostFX.Dof.gaussianMaxRadius.Override(Mathf.Lerp(1.5f, 0f, t)); }, () => PostFX.Dof.active = false);
            Cinema.I.Caption("Ugh... where am I?", 3);
            yield return new WaitForSeconds(2.2f);
            // sit up and stand, turning toward the burning wreck
            float t0 = 0, d = 2.6f; float fromYaw = player.YawDeg, fromPitch = player.PitchDeg;
            var toDir = crash - (spawn + Vector3.up * 1.6f);
            float toYaw = Mathf.Atan2(toDir.x, toDir.z) * Mathf.Rad2Deg, toPitch = 4;
            Sfx.Play("step", 0.6f);
            while (t0 < d)
            {
                t0 += Time.deltaTime; float k = Tween.EaseInOut(t0 / d);
                player.HeadHeight = Mathf.Lerp(0.35f, 1.62f, k);
                player.ExtraRoll = Mathf.Sin(k * Mathf.PI) * 12;
                player.Teleport(spawn, Mathf.LerpAngle(fromYaw, toYaw, k), Mathf.Lerp(fromPitch, toPitch, k));
                yield return null;
            }
            player.ExtraRoll = 0;
            Cinema.I.Caption("The ship... it's destroyed.", 3);
            yield return new WaitForSeconds(2.5f);
            UIState.Cinematic = false; UIState.Refresh();
            player.Frozen = false;
            hud.Show(true, 1.2f);
            suit.Active = true;
            AudioManager.I.Music("music_ambient", 0.35f, 3f);
            // NOVA arrives
            bot.Teleport(player.transform.position + new Vector3(12, 8, 18));
            yield return new WaitForSeconds(1.0f);
            yield return hud.SayWait("Commander! You're awake! I'm NOVA, your helper robot. Our ship crashed — but you made it!");
            hud.Objective("LOOK AROUND", "Move the MOUSE to look. Find the burning rocket. Then find EARTH in the sky!");
            yield return hud.SayWait("See that blue dot in the sky? That's EARTH, our home! The grey rock is PHOBOS, a moon of Mars.");
            float w = 0;
            var earthDir = MarsWorld.Earth.GetComponent<SkyBody>().Dir;
            while (w < 20 && !lookedEarth)
            {
                w += Time.deltaTime;
                if (Vector3.Dot(player.Cam.transform.forward, earthDir) > 0.99f) lookedEarth = true;
                yield return null;
            }
            if (lookedEarth) { yield return new WaitForSeconds(1.5f); yield return hud.SayWait("That's home. It's so far away… We need a plan to get back."); }
        }

        IEnumerator Lids(float a, float b, float d)
        {
            float t = 0; while (t < d) { t += Time.deltaTime; Cinema.I.Eyes(Mathf.Lerp(a, b, Mathf.SmoothStep(0, 1, t / d))); yield return null; }
            Cinema.I.Eyes(b);
        }

        public static void ConfigCam(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        // ================================================================== storm
        FlareEvent PickFlare(bool biggest)
        {
            if (pool.Count == 0) pool.AddRange(NasaData.Archive);
            FlareEvent best = pool[0];
            if (biggest) { foreach (var f in pool) if (string.CompareOrdinal(f.Class, best.Class) > 0 && f.Class[0] >= best.Class[0]) best = f; }
            else best = pool[Random.Range(0, pool.Count)];
            pool.Remove(best);
            return best;
        }

        string FlareLine(FlareEvent f) => f.Live
            ? $"LIVE NASA DONKI DATA: real {f.Class} flare, {f.Date}"
            : $"NASA DONKI DATA (archived): real {f.Class} flare, {f.Date}";

        IEnumerator FirstStorm()
        {
            CurrentFlare = PickFlare(true);
            Sfx.Play("alarm", 0.7f);
            hud.Warn($"SOLAR FLARE {CurrentFlare.Class} DETECTED — RADIATION STORM", 6);
            yield return hud.SayWait($"WARNING! NASA just measured a {CurrentFlare.Size} solar flare — class {CurrentFlare.Class}! A radiation storm is coming.", 3);
            hud.Toast("REAL SPACE WEATHER", FlareLine(CurrentFlare), "flare", HC.Amber);
            wpBunker = hud.AddWaypoint("BUNKER", () => MarsWorld.BunkerOutside, HC.Green);
            hud.Objective("GET TO THE BUNKER!", "Space rays are hurting you! Hide UNDERGROUND: Mars soil blocks the rays. Follow the GREEN marker.");
            hud.Say("Mars has no magnetic shield like Earth. Run to the bunker — hold SHIFT to run!", 6);
            // storm ramps up
            StartCoroutine(StormRamp(Mathf.Clamp(0.55f * CurrentFlare.Strength, 0.4f, 0.9f), 4f));
            bot.Lead = MarsWorld.BunkerOutside + Vector3.up * 2.5f;
            bool entered = false;
            MarsWorld.BunkerDoor.OnUse = () => entered = true;
            while (!entered) yield return null;
            bot.Lead = null;
            yield return EnterBunker();
            hud.RemoveWaypoint(wpBunker); wpBunker = null;
            hud.ClearObjective();
            // time passes: storm ends (kept short — about 1.5 seconds)
            yield return Cinema.I.ToBlack(0.3f);
            suit.Storm = 0; StopAllStorms();
            Cinema.I.Caption("A few hours later — the storm is over.", 1.5f);
            Game.Sky.TimeOfDay = 0.45f;
            yield return new WaitForSeconds(0.9f);
            yield return Cinema.I.FromBlack(0.3f);
            hud.Toast("YOU SURVIVED THE STORM", $"Real flare {CurrentFlare.Class} ({CurrentFlare.Date}). 2 metres of Mars soil above the shelter blocked the radiation.", "shield", HC.Green);
            MarsWorld.AlarmLight.intensity = 3; MarsWorld.AlarmLight.GetComponent<Blink>().SetBase(3);
            Sfx.Play("alarm", 0.5f);
            yield return hud.SayWait("Safe! But the bunker has no machines — air and water are running out!", 3f);
        }

        Coroutine stormCo;
        IEnumerator StormRamp(float to, float d)
        {
            float from = suit.Storm, t = 0;
            while (t < d) { t += Time.deltaTime; suit.Storm = Mathf.Lerp(from, to, t / d); yield return null; }
            suit.Storm = to;
        }
        void StopAllStorms() { suit.Storm = 0; }

        // ================================================================== bunker in/out
        IEnumerator EnterBunker()
        {
            busy = true;
            player.Frozen = true;
            Sfx.Play("thud", 0.5f);
            yield return Cinema.I.ToBlack(0.35f);
            Sfx.Play("whoosh", 0.4f, 0.6f);
            MarsWorld.SetSurface(false); MarsWorld.Interior.SetActive(true);
            Game.Sky.Mode = SkyMode.Interior; Game.Sky.Apply();
            player.Teleport(MarsWorld.InteriorSpawn, MarsWorld.InteriorYaw);
            bot.Teleport(MarsWorld.InteriorSpawn + new Vector3(1.2f, 1.8f, 1.5f));
            suit.Inside = true; inside = true;
            if (AudioManager.I.Wind) AudioManager.I.FadeSource(AudioManager.I.Wind, 0.05f, 0.5f);
            yield return Cinema.I.FromBlack(0.4f);
            player.Frozen = false; busy = false;
        }

        IEnumerator ExitBunker()
        {
            busy = true;
            player.Frozen = true;
            yield return Cinema.I.ToBlack(0.35f);
            Sfx.Play("whoosh", 0.4f, 0.6f);
            MarsWorld.Interior.SetActive(false); MarsWorld.SetSurface(true);
            Game.Sky.Mode = SkyMode.Surface; Game.Sky.Apply();
            player.Teleport(MarsWorld.BunkerOutside, MarsWorld.BunkerOutsideYaw);
            bot.Teleport(MarsWorld.BunkerOutside + new Vector3(1.5f, 2, 1.5f));
            suit.Inside = false; inside = false;
            if (AudioManager.I.Wind) AudioManager.I.FadeSource(AudioManager.I.Wind, 0.35f, 0.5f);
            yield return Cinema.I.FromBlack(0.4f);
            player.Frozen = false; busy = false;
        }

        void WireDoors()
        {
            MarsWorld.BunkerDoor.OnUse = () => { if (!busy) StartCoroutine(EnterBunker()); };
            MarsWorld.ExitDoor.OnUse = () => { if (!busy) StartCoroutine(ExitBunker()); };
        }

        // ================================================================== salvage
        IEnumerator Salvage()
        {
            WireDoors();
            hud.Objective("SALVAGE THE MACHINES", "Go back to the rocket. Bring the <b>AIR MAKER</b> and the <b>WATER MAKER</b> to the bunker. Carry one at a time!");
            yield return hud.SayWait("Our rocket brought 2 machines: MOXIE makes AIR to breathe. The other turns ICE into WATER.");
            hud.Say("They're at the crash site. Bring them here and plug them in. Walk to the door and press E to go outside.", 6);
            wpMoxie = hud.AddWaypoint("MOXIE", () => MarsWorld.MoxieCrate ? MarsWorld.MoxieCrate.transform.position : Vector3.zero, HC.Cyan);
            wpWater = hud.AddWaypoint("ICE MACHINE", () => MarsWorld.WaterCrate ? MarsWorld.WaterCrate.transform.position : Vector3.zero, new Color(0.5f, 0.7f, 1f));
            wpBunker = hud.AddWaypoint("BUNKER", () => MarsWorld.BunkerOutside, HC.Green); wpBunker.On = false;
            foreach (var p in MarsWorld.Pouches) { var pp = p; pp.OnUse = () => { suit.Drink(35); Sfx.Play("pickup", 0.6f); hud.Toast("LIFE SUPPORT +35", "Emergency air & water pack from the ship.", "water", HC.Cyan); pp.gameObject.SetActive(false); }; }
            MarsWorld.Tap.OnUse = () => { suit.Drink(100); Sfx.Play("powerup", 0.5f); hud.Toast("LIFE SUPPORT UP", "Fresh water melted from Mars ice.", "water", HC.Cyan); };
            MarsWorld.DataConsole.OnUse = () => screens.OpenData(false);

            bool moxieHome = false, waterHome = false;
            MarsWorld.MoxieCrate.OnUse = () => TryPickMachine("MOXIE", "MOXIE Oxygen Maker", "air", HC.Cyan, MarsWorld.MoxieCrate, wpMoxie);
            MarsWorld.WaterCrate.OnUse = () => TryPickMachine("ICE", "Ice-to-Water Machine", "water", new Color(0.5f, 0.7f, 1f), MarsWorld.WaterCrate, wpWater);
            MarsWorld.MoxieSlot.enabled = true; MarsWorld.WaterSlot.enabled = true;
            MarsWorld.MoxieSlot.OnUse = () => { if (suit.Has("MOXIE")) { suit.Remove("MOXIE"); moxieHome = true; StartCoroutine(InstallMoxie()); } else hud.Say("We need to bring MOXIE from the crash site first."); };
            MarsWorld.WaterSlot.OnUse = () => { if (suit.Has("ICE")) { suit.Remove("ICE"); waterHome = true; StartCoroutine(InstallWater()); } else hud.Say("We need to bring the Ice-to-Water machine from the crash site first."); };
            float tipT = 0;
            while (!(moxieHome && waterHome))
            {
                wpBunker.On = suit.CarryingHeavy;
                tipT += Time.deltaTime;
                if (tipT > 50 && !inside && suit.O2 < 35) { tipT = 0; hud.Say("Your oxygen is low! Go back into the bunker — or bring MOXIE so it can make more."); }
                yield return null;
            }
            hud.RemoveWaypoint(wpBunker); wpBunker = null;
            MarsWorld.AlarmLight.GetComponent<Blink>().SetBase(0); MarsWorld.AlarmLight.intensity = 0;
            yield return new WaitForSeconds(2.5f);
        }

        void TryPickMachine(string id, string name, string icon, Color c, Interactable crate, Waypoint wp)
        {
            if (suit.CarryingHeavy) { hud.Say("You can only carry one heavy machine at a time. Take this one to the bunker first!"); return; }
            suit.Add(new Item(id, name, icon, c, true));
            crate.gameObject.SetActive(false);
            hud.RemoveWaypoint(wp);
            hud.Toast("PICKED UP: " + name.ToUpper(), "Heavy! You walk slower. Bring it to the bunker.", icon, c);
            hud.Say(id == "MOXIE" ? "Got MOXIE! Now carry it to the bunker — follow the green marker." : "Got the Ice machine! Carry it to the bunker.", 4);
            if (id == "MOXIE") StartCoroutine(screens.ShowInfo("MOXIE OXYGEN MAKER", "air", HC.Cyan,
                @"<b>1.</b>  A pump sucks in Mars air. It is mostly carbon dioxide (CO2).
<b>2.</b>  MOXIE heats the air until it is very hot (800 °C).
<b>3.</b>  A special plate splits CO2 and keeps the <color=#7FE3FF>OXYGEN (O2)</color>.
<b>4.</b>  The oxygen fills your bunker so you can breathe.

<color=#9FB6D6>Carry it to the blue MOXIE spot in the bunker and press E.</color>",
                "Read more in the Junior Astronaut Handbook, page 10"));
            else StartCoroutine(screens.ShowInfo("ICE-TO-WATER MACHINE", "water", new Color(0.5f, 0.7f, 1f),
                @"<b>1.</b>  A drill digs down to the ice hidden under the Mars ground.
<b>2.</b>  A heater melts the ice into <color=#7FE3FF>WATER</color>.
<b>3.</b>  Filters clean the water so it is safe to drink.
<b>4.</b>  Drink from the tap in the bunker (press E) to fill your LIFE SUPPORT bar.

<color=#9FB6D6>Carry it to the blue WATER spot in the bunker and press E.</color>",
                "Read more in the Junior Astronaut Handbook, page 10"));
        }

        IEnumerator InstallMoxie()
        {
            MarsWorld.MoxieGhost.SetActive(false); MarsWorld.MoxieInstalled.SetActive(true);
            suit.MoxieOn = true; Sfx.Play("build", 0.7f); Sfx.Play("powerup", 0.6f);
            hud.Toast("MOXIE ONLINE", "CO2 in → O2 out. Oxygen refills inside the bunker.", "air", HC.Cyan);
            yield return hud.SayWait("MOXIE works! It makes oxygen from Mars air. NASA really did this on Mars in 2021!");
        }
        IEnumerator InstallWater()
        {
            MarsWorld.WaterGhost.SetActive(false); MarsWorld.WaterInstalled.SetActive(true);
            suit.WaterOn = true; Sfx.Play("build", 0.7f); Sfx.Play("powerup", 0.6f);
            hud.Toast("WATER MACHINE ONLINE", "Melts underground ice. Press E at the tap to drink.", "water", HC.Cyan);
            yield return hud.SayWait("Water is flowing! NASA found ice under the ground on Mars. Press E at the tap to drink.");
        }

        // ================================================================== parts hunt
        IEnumerator PartsIntro()
        {
            yield return hud.SayWait("Now, the big question: how do we get HOME?");
            MarsWorld.TimerHolo.SetActive(true);
            ClockRunning = true; DaysLeft = 779;
            hud.Toast("EARTH LAUNCH WINDOW", "Earth and Mars line up every ~26 months. Next window: in 2 years.", "clock", HC.Cyan);
            yield return hud.SayWait("Earth and Mars come close only once every 2 years. Watch the clock on the bunker!");
            yield return hud.SayWait("Look, a MARS COLONY is nearby! Grow food in the glass dome, call Earth from the dish tower and check the solar panels.");
            yield return hud.SayWait("Our old rocket is broken. Let's BUILD a new one! Find 3 rocket parts. Follow the coloured lights!");
            hud.Objective("BUILD A NEW ROCKET", "Find 3 rocket parts (follow the coloured lights). Solve a puzzle for each one. Bring them to the LAUNCH PAD.\nCome back to the bunker for air and water.", "parts");
            foreach (var p in MarsWorld.Parts)
            {
                var pp = p;
                p.Go.SetActive(true);
                p.Use.OnUse = () => { if (!busy && !pp.Moving && !pp.Found && pp.Stage >= pp.Path.Length - 1) StartCoroutine(CatchPart(pp)); };
                partWps[p] = hud.AddWaypoint(p.Name.ToUpper(), () => pp.Go ? pp.Go.transform.position : Vector3.zero, p.Color);
            }
            wpRocket = hud.AddWaypoint("LAUNCH PAD", () => MarsWorld.Rocket.position, HC.Amber); wpRocket.On = false;
            MarsWorld.RocketUse.enabled = true;
            MarsWorld.RocketUse.OnUse = () => StartCoroutine(InstallParts());
            hazardQueue = new List<int> { 0, 1, 2, 0 };
            for (int i = hazardQueue.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (hazardQueue[i], hazardQueue[j]) = (hazardQueue[j], hazardQueue[i]); }
            hazardTimer = Random.Range(35f, 60f);
            partsHunt = true;
        }

        bool firstFlee = true, dustDone, meteorDone; public bool Hazard;
        void PartsUpdate()
        {
            var pp = player.transform.position;
            foreach (var p in MarsWorld.Parts)
            {
                if (p.Found || p.Moving || p.Stage >= p.Path.Length - 1) continue;
                if (Vector3.Distance(pp, p.Go.transform.position) < 13f) StartCoroutine(Flee(p));
            }
            int carried = 0; foreach (var p in MarsWorld.Parts) if (p.Found && !p.Installed) carried++;
            if (wpRocket != null) wpRocket.On = carried > 0;
            // easy "press E" when you are simply NEAR a part (no need to aim)
            string fp = null;
            if (!busy && !UIState.Modal && !inside)
            {
                RocketPart near = null; float best = 8f;
                foreach (var p in MarsWorld.Parts) { if (p.Found || p.Moving || p.Stage < p.Path.Length - 1 || !p.Go.activeSelf) continue; float d = Vector3.Distance(pp, p.Go.transform.position); if (d < best) { best = d; near = p; } }
                bool aiming = hud.Interactor && hud.Interactor.Current;
                if (near != null) { fp = $"SOLVE PUZZLE  <color=#7FE3FF>{near.Name}</color>"; if (Input.GetKeyDown(KeyCode.E) && !aiming && !busy) StartCoroutine(CatchPart(near)); }
                else if (carried > 0 && Vector3.Distance(pp, MarsWorld.Rocket.position) < 15f) { fp = "<color=#FFB347>INSTALL DETAILS ON THE NEW ROCKET</color>"; if (Input.GetKeyDown(KeyCode.E) && !aiming) StartCoroutine(InstallParts()); }
            }
            hud.ForcedPrompt = fp;
            if (Hazard || inside || busy) return;
            // challenges while exploring: dust storm, meteor shower, a second real solar flare
            // random order + random timing every game: 0 = solar flare, 1 = dust storm, 2 = meteor shower
            hazardTimer -= Time.deltaTime;
            if (hazardTimer <= 0 && hazardQueue.Count > 0)
            {
                int h = hazardQueue[0]; hazardQueue.RemoveAt(0); hazardTimer = Random.Range(50f, 85f);
                StartCoroutine(h == 0 ? SecondStorm() : h == 1 ? DustStorm() : MeteorShower());
            }
        }
        int FoundCount() { int n = 0; foreach (var p in MarsWorld.Parts) if (p.Found) n++; return n; }

        IEnumerator Flee(RocketPart p)
        {
            p.Moving = true;
            var from = p.Go.transform.position; p.Stage++;
            var to = MarsWorld.G(p.Path[p.Stage], 0.4f);
            if (firstFlee) { firstFlee = false; hud.Say("Whoa, a gust of wind blew it away! Don't worry, it only happens once. Follow the beam!", 4); }
            else Sfx.Play("whoosh", 0.6f, 1.2f);
            FX.DustBlast(from, 1.2f);
            var model = p.Go.transform.Find("Model");
            float t = 0, d = 2.6f;
            while (t < d)
            {
                t += Time.deltaTime; float k = Tween.EaseInOut(t / d);
                p.Go.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 9f;
                if (model) model.Rotate(new Vector3(420, 200, 90) * Time.deltaTime, Space.Self);
                yield return null;
            }
            if (model) model.localRotation = Quaternion.identity;
            FX.DustBlast(to, 0.8f);
            Sfx.Play("thud", 0.5f);
            p.Moving = false;
            hud.Say($"The {p.Name} is stuck between rocks now. Walk up to it and press E to solve its puzzle!", 5);
        }

        IEnumerator CatchPart(RocketPart p)
        {
            busy = true;
            bool ok = false;
            yield return screens.RunPuzzle(p, r => ok = r);
            p.Found = true;
            suit.Add(new Item(p.Id, p.Name, p.Icon, p.Color));
            p.Go.SetActive(false);
            if (partWps.TryGetValue(p, out var w)) { hud.RemoveWaypoint(w); partWps.Remove(p); }
            hud.Toast("FOUND: " + p.Name.ToUpper(), "Take it to the LAUNCH PAD (orange marker).", p.Icon, p.Color);
            busy = false;
        }

        IEnumerator InstallParts()
        {
            int added = 0;
            foreach (var p in MarsWorld.Parts)
            {
                if (!p.Found || p.Installed) continue;
                p.Installed = true; added++; partsInstalled++;
                suit.Remove(p.Id);
                MarsWorld.BuildSection(p.Section);
                Sfx.Play("build", 0.8f);
                yield return new WaitForSeconds(0.6f);
            }
            if (added == 0) { hud.Say("Find a detail first. Follow the coloured beams!"); yield break; }
            int total = MarsWorld.Parts.Length;
            hud.Toast($"NEW ROCKET: {partsInstalled} / {total} BUILT", partsInstalled < total ? "Keep going!" : "The rocket is complete!", "rocket", HC.Amber);
            if (partsInstalled < total) hud.Objective("BUILD A NEW ROCKET", $"{partsInstalled} of {total} sections built. Follow the coloured beams to the next detail!", "parts");
        }

        // ---------------- challenges ----------------
        public IEnumerator DustStorm()
        {
            Hazard = true;
            hud.Warn("DUST STORM APPROACHING", 5, false);
            yield return hud.SayWait("Dust storm! It's hard to see. Follow your markers. In 2018 a storm like this covered ALL of Mars!", 3);
            var wind = FX.Make("StormWind", player.transform.position, player.transform, FX.Alpha, ps =>
            {
                var m = ps.main; m.startLifetime = 1.6f; m.startSpeed = 0; m.startSize = new ParticleSystem.MinMaxCurve(2f, 6f); m.maxParticles = 900;
                m.startColor = new Color(0.75f, 0.45f, 0.28f, 0.5f); m.simulationSpace = ParticleSystemSimulationSpace.World;
                var e = ps.emission; e.rateOverTime = 360;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(40, 8, 40); s.position = new Vector3(0, 3, 0);
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World; v.x = new ParticleSystem.MinMaxCurve(18, 30); v.y = new ParticleSystem.MinMaxCurve(-1, 1); v.z = new ParticleSystem.MinMaxCurve(4, 9);
            });
            var streaks = FX.Make("StormStreaks", player.transform.position, player.transform, FX.Add, ps =>
            {
                var m = ps.main; m.startLifetime = 0.6f; m.startSpeed = 0; m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f); m.maxParticles = 1200;
                m.startColor = new Color(1f, 0.7f, 0.45f, 0.35f); m.simulationSpace = ParticleSystemSimulationSpace.World;
                var e = ps.emission; e.rateOverTime = 900;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(30, 6, 30); s.position = new Vector3(0, 2, 0);
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World; v.x = new ParticleSystem.MinMaxCurve(35, 50); v.y = new ParticleSystem.MinMaxCurve(-2, 0); v.z = new ParticleSystem.MinMaxCurve(6, 12);
                var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.08f;
            });
            if (AudioManager.I.Wind) AudioManager.I.FadeSource(AudioManager.I.Wind, 1f, 3f);
            // the storm is OUTSIDE only: hide the dust and haze while the player is in the bunker
            void Level(float k)
            {
                bool show = !inside;
                if (wind.gameObject.activeSelf != show) { wind.gameObject.SetActive(show); streaks.gameObject.SetActive(show); if (show) { wind.Play(); streaks.Play(); } }
                Game.Sky.DustStorm = show ? k : 0;
            }
            float t = 0; while (t < 4) { t += Time.deltaTime; Level(t / 4); yield return null; }
            suit.O2Drain = 0.42f;
            t = 0; while (t < 40) { t += Time.deltaTime; Level(1); if (!inside) player.Shake(0.05f, 0.1f); yield return null; }
            t = 0; while (t < 5) { t += Time.deltaTime; Level(1 - t / 5); yield return null; }
            wind.gameObject.SetActive(true); streaks.gameObject.SetActive(true);
            Game.Sky.DustStorm = 0; suit.O2Drain = 0.32f;
            wind.Stop(); streaks.Stop(); Destroy(wind.gameObject, 3); Destroy(streaks.gameObject, 2);
            if (AudioManager.I.Wind) AudioManager.I.FadeSource(AudioManager.I.Wind, inside ? 0.05f : 0.35f, 3f);
            hud.Say("The storm has passed. On real Mars, dust storms can last for weeks!", 4);
            Hazard = false;
        }

        public IEnumerator MeteorShower()
        {
            Hazard = true;
            hud.Warn("METEOR SHOWER!", 5);
            yield return hud.SayWait("Space rocks are falling! Mars has thin air, so they don't burn up. Run away from the RED circles!", 3);
            float t = 0;
            while (t < 24)
            {
                if (!inside && !UIState.Modal)
                {
                    var p = player.transform.position;
                    var fwd = Vector3.ProjectOnPlane(player.Cam.transform.forward, Vector3.up).normalized;
                    var off = Random.insideUnitCircle * 22f;
                    var hit = new Vector3(p.x + off.x, 0, p.z + off.y) + fwd * Random.Range(4f, 18f);
                    if (Random.value < 0.3f) hit = new Vector3(p.x, 0, p.z) + fwd * Random.Range(1f, 6f); // some come close: dodge!
                    hit.y = MarsTerrain.H(hit.x, hit.z);
                    StartCoroutine(Meteor(hit));
                }
                float w = Random.Range(2.0f, 3.4f); t += w;
                yield return new WaitForSeconds(w);
            }
            yield return new WaitForSeconds(3);
            hud.Say("The shower is over. NASA's orbiters really do spot brand-new craters on Mars every year!", 4);
            Hazard = false;
        }

        IEnumerator Meteor(Vector3 hit)
        {
            // warning circle on the ground
            var ring = ProcMesh.Prim(PrimitiveType.Cylinder, "MeteorWarning", null, hit + Vector3.up * 0.08f, new Vector3(9, 0.02f, 9), R.Holo(new Color(1f, 0.15f, 0.1f), 1.8f, 0.35f), null, false);
            ring.AddComponent<Spin>().Speed = 120;
            var start = hit + new Vector3(Random.Range(-60f, 60f), 140, Random.Range(-60f, 60f));
            int kind = Random.Range(1, 5);
            GameObject rock = R.Model("SF_Asteroid" + kind) ? Util.PlaceModel("SF_Asteroid" + kind, start, Random.Range(1.2f, 2.6f), Random.Range(0, 360f))
                                                              : ProcMesh.Prim(PrimitiveType.Sphere, "Meteor", null, start, Vector3.one * 1.6f, R.Rock, null, false);
            FX.Fire(start, 1.4f, rock.transform);
            var trail = FX.Smoke(start, 1.2f, rock.transform, 0.08f);
            var trailMain = trail.main; trailMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var gl = new GameObject("Glow").AddComponent<Light>(); gl.transform.SetParent(rock.transform, false); gl.type = LightType.Point; gl.range = 25; gl.intensity = 6; gl.color = new Color(1f, 0.55f, 0.2f);
            Sfx.Play("whoosh", 0.35f, 0.5f);
            float t = 0, d = 2.2f;
            while (t < d) { t += Time.deltaTime; float k = t / d; rock.transform.position = Vector3.Lerp(start, hit, k * k); rock.transform.Rotate(200 * Time.deltaTime, 90 * Time.deltaTime, 0); yield return null; }
            Destroy(ring);
            FX.Explosion(hit + Vector3.up * 0.5f, 0.6f);
            FX.DustBlast(hit, 1.1f);
            Sfx.At("explosion", hit, 0.9f, 1.2f);
            // the rock stays in a fresh little crater
            foreach (var ps in rock.GetComponentsInChildren<ParticleSystem>()) ps.Stop();
            Destroy(gl.gameObject);
            rock.transform.position = hit + Vector3.up * 0.2f;
            var crater = ProcMesh.MeshObj("NewCrater", ProcMesh.Dome(20, 4, 0), R.Lit(new Color(0.32f, 0.17f, 0.1f), 0.05f), null);
            crater.transform.position = hit + Vector3.up * 0.02f; crater.transform.localScale = new Vector3(2.2f, 0.1f, 2.2f);
            Destroy(rock, 40); Destroy(crater, 40);
            if (player && !inside && !UIState.Modal)
            {
                float dist = Vector3.Distance(player.transform.position, hit);
                if (dist < 18) player.Shake(0.4f * (1 - dist / 18), 0.4f);
                if (dist < 5.5f) { suit.Health = Mathf.Max(5, suit.Health - 14); hud.Damage(0.7f); hud.Warn("HIT BY METEOR DEBRIS!", 2); }
            }
        }

        public IEnumerator SecondStorm()
        {
            Hazard = true;
            var f = PickFlare(false); CurrentFlare = f;
            hud.Warn($"SOLAR FLARE {f.Class} — RADIATION RISING", 6);
            yield return hud.SayWait($"NASA reports a {f.Class} solar flare! Your choice: SHELTER in the bunker (safe, but time passes 4x faster while you wait) or KEEP GOING (you save time, but you take radiation).", 4);
            hud.Toast("REAL SPACE WEATHER", FlareLine(f), "flare", HC.Amber);
            var wpS = hud.AddWaypoint("SHELTER", () => MarsWorld.BunkerOutside, HC.Green);
            float dose0 = suit.DoseMSv; int sol0 = Sol; float inSec = 0, outSec = 0;
            float strength = Mathf.Clamp(0.45f * f.Strength, 0.35f, 0.75f);
            FlareActive = true;
            yield return StormRamp(strength, 3f);
            float t = 0;
            while (t < 26) { t += Time.deltaTime; if (inside) inSec += Time.deltaTime; else outSec += Time.deltaTime; yield return null; }
            yield return StormRamp(0, 3f);
            FlareActive = false;
            hud.RemoveWaypoint(wpS);
            float got = suit.DoseMSv - dose0; int daysUsed = Mathf.Max(0, Sol - sol0);
            float ifOutside = strength * 1.2f * 29f;
            bool sheltered = inSec > outSec;
            string body = $@"<b>The flare:</b> class {f.Class} on {f.Date}. {(f.Live ? "LIVE" : "Archived")} data from NASA DONKI: this flare really happened!
<b>Your choice:</b> <color=#7FE3FF>{(sheltered ? "SHELTER" : "KEEP GOING")}</color>
<b>Time used:</b> {daysUsed} days of your launch window
<b>Radiation you got:</b> {got:0.0} mSv   <color=#9FB6D6>(staying outside the whole time: about {ifOutside:0} mSv)</color>
<b>Compare:</b> 1 chest X-ray is about 0.1 mSv, so you got about {Mathf.Round(got / 0.1f):0} X-rays.

<color=#9FB6D6>Real fact: in May 2024 NASA's Curiosity rover measured one big solar storm on Mars. It was like 30 chest X-rays at once.</color>";
            yield return screens.ShowInfo("FLARE REPORT", "flare", HC.Amber, body, "Read more in the Junior Astronaut Handbook, page 11 (Mars Dangers)");
            Hazard = false;
        }

        // ================================================================== collapse (never game over)
        IEnumerator Collapse(string why)
        {
            busy = true; player.Frozen = true;
            Sfx.Play("heartbeat", 1f);
            yield return Lids(1, 0, 1.2f);
            Cinema.I.SetBlack(1); Cinema.I.Eyes(1);
            if (!inside)
            {
                MarsWorld.SetSurface(false); MarsWorld.Interior.SetActive(true);
                Game.Sky.Mode = SkyMode.Interior; Game.Sky.Apply();
                player.Teleport(MarsWorld.InteriorSpawn + new Vector3(0, 0, 1), MarsWorld.InteriorYaw);
                bot.Teleport(MarsWorld.InteriorSpawn + new Vector3(1.2f, 1.8f, 2));
                suit.Inside = true; inside = true;
            }
            suit.Storm = 0;
            suit.Restore(70, 70, 70);
            yield return new WaitForSeconds(1.2f);
            yield return Cinema.I.FromBlack(1f);
            player.Frozen = false; busy = false;
            string tip = why == "air" ? "Your LIFE SUPPORT ran out. Tip: watch the blue bar and come back to the bunker before it is empty."
                : "Too much RADIATION. Tip: when NASA warns of a solar flare, take cover in the bunker!";
            yield return hud.SayWait("Commander! I carried you back to the bunker. You're OK now. " + tip, 6);
        }

        // ================================================================== launch
        IEnumerator Launch()
        {
            partsHunt = false; hud.ForcedPrompt = null;
            hud.ClearWaypoints();
            hud.Objective("WAIT FOR THE LAUNCH WINDOW", "Your rocket is ready! Watch the clock on the bunker.");
            yield return hud.SayWait("The rocket is complete! Now we wait for Earth to come around. Let's speed up time…");
            // time-lapse to day 0
            int start = DaysLeft, solStart = Sol; float t = 0, d = 6f;
            ClockRunning = false;
            Sfx.Play("whoosh", 0.5f, 0.5f);
            var co = StartCoroutine(SkyCycle(d));
            while (t < d) { t += Time.deltaTime; DaysLeft = Mathf.RoundToInt(Mathf.Lerp(start, 0, t / d)); Sol = solStart + (start - DaysLeft); yield return null; }
            DaysLeft = 0;
            Sfx.Play("success", 0.8f);
            hud.Warn("LAUNCH WINDOW OPEN", 4, false);
            var wpConsole = hud.AddWaypoint("LAUNCH CONSOLE", () => MarsWorld.LaunchConsole.transform.position, HC.Green);
            hud.Objective("GO HOME!", "Walk to the screen next to the rocket and press E. Let's fly home!");
            yield return hud.SayWait("The window is OPEN! Earth is lined up. Go to the launch console!");
            bool go = false;
            MarsWorld.LaunchConsole.enabled = true; MarsWorld.LaunchConsole.OnUse = () => go = true;
            while (!go) yield return null;
            hud.RemoveWaypoint(wpConsole);
            // countdown on the pad
            UIState.Cinematic = true; UIState.Refresh(); player.Frozen = true;
            hud.Show(false, 0.6f);
            var exh = FX.Exhaust(MarsWorld.Rocket, Vector3.zero, 1.8f); exh.transform.rotation = Quaternion.Euler(90, 0, 0); exh.Stop();
            AudioManager.I.Music("music_hope", 0.6f, 2f);
            for (int i = 5; i >= 1; i--) { Cinema.I.Caption($"LAUNCH IN  {i}", 0.9f); Sfx.Play("beep", 0.6f, i == 1 ? 1.4f : 1); yield return new WaitForSeconds(1f); }
            exh.Play(); Sfx.Play("explosion", 0.7f, 0.6f);
            var roar = AudioManager.I.Loop("rocket", 0.8f);
            FX.DustBlast(MarsWorld.Rocket.position, 2f); FX.Explosion(MarsWorld.Rocket.position + Vector3.up * 1.5f, 1.2f);
            Cinema.I.Caption("LIFT-OFF!", 2);
            float tt = 0; var r0 = MarsWorld.Rocket.position;
            while (tt < 4) { tt += Time.deltaTime; MarsWorld.Rocket.position = r0 + Vector3.up * tt * tt * 3; player.Shake(0.4f, 0.2f); player.LookAt(MarsWorld.Rocket.position); yield return null; }
            yield return Cinema.I.ToBlack(1f);
            AudioManager.I.FadeSource(roar, 0, 1); Destroy(roar.gameObject, 1.5f);
            // flight home
            player.Cam.enabled = false; cineCam.enabled = true; Game.Cam = cineCam;
            SpaceSet.I.Enter(cineCam);
            Cinema.I.Letterbox(true, 0.01f);
            yield return SpaceSet.I.Ending();
            Cinema.I.Letterbox(false, 0.6f);
            int days = Sol;
            screens.ShowEnding($"You survived <color={HC.Hex(HC.Cyan)}>{days} sols</color> on Mars and flew home.\n" +
                $"Radiation dose: <color={HC.Hex(HC.Amber)}>{suit.DoseMSv:0} mSv</color> of the 600 mSv NASA limit  ·  Launch windows missed: {WindowsMissed}\n" +
                $"Puzzles solved first try: <color={HC.Hex(HC.Green)}>{screens.FirstTry} / {screens.Total}</color>\n" +
                $"<size=20>Real solar flares you faced came from NASA DONKI. Real Mars missions plan around these same launch windows.</size>");
        }

        IEnumerator SkyCycle(float d)
        {
            float t = 0, from = Game.Sky.TimeOfDay;
            while (t < d) { t += Time.deltaTime; Game.Sky.TimeOfDay = from + (t / d) * 6f; yield return null; }
            Game.Sky.TimeOfDay = 0.45f;
        }
    }
}
