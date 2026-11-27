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
        /// <summary>The game starts on today's real date. Every sol (Mars day = 24 h 39 min) moves the calendar on.</summary>
        public System.DateTime StartDate = System.DateTime.UtcNow.Date;
        public System.DateTime Date => StartDate.AddDays((Sol - 1) * 1.0275);
        public System.DateTime WindowDate; bool calendarOn; float solAcc;
        public float SecondsPerSol = 15f; // before the countdown: a new sol every 15 seconds of play
        public FlareEvent CurrentFlare; readonly List<FlareEvent> pool = new List<FlareEvent>();
        bool inside, busy, partsHunt, storm2Done;
        int partsInstalled;
        Waypoint wpBunker, wpMoxie, wpWater, wpRocket;
        readonly Dictionary<RocketPart, Waypoint> partWps = new Dictionary<RocketPart, Waypoint>();
        bool lookedEarth;
        public static bool QuickStart;
        public static bool AutoContinue; // test mode: pick CONTINUE by itself

        public void Init(Camera cine, Screens s) { cineCam = cine; screens = s; Game.Story = this; }

        public string ClockText()
        {
            if (Game.ExploreBase) return $"SOL {Sol}  |  MARS BASE";
            string day = Date.ToString("d MMM yyyy").ToUpper();
            if (!ClockRunning) return $"SOL {Sol}  ·  {day}";
            return DaysLeft > 0 ? $"SOL {Sol}  ·  {day}  ·  <color={HC.Hex(HC.Cyan)}>T-{DaysLeft}</color>" : $"SOL {Sol}  ·  <color={HC.Hex(HC.Green)}>WINDOW OPEN</color>";
        }
        public string SunText()
        {
            bool live; string s = NasaData.SunStatus(out live);
            string sun = !live ? "Sun: 2024 data" : s.Contains("VERY") ? "Sun: very active" : s.Contains("active") ? "Sun: active" : "Sun: calm";
            // outside temperature = real readings of NASA's InSight lander; Sun = real flares from NASA DONKI
            string temp = Game.Sky ? $"{Game.Sky.OutsideTempC:0}°C" : "";
            return $"● {temp} NASA InSight  ·  {sun}";
        }

        void Update()
        {
            if (ClockRunning && DaysLeft > 0 && !Game.Paused && !UIState.Modal)
            {
                dayAcc += Time.deltaTime * (FlareActive && inside ? 4f : 1f); // sheltering from a flare costs time
                while (dayAcc > SecondsPerDay) { dayAcc -= SecondsPerDay; Tick(); }
            }
            else if (calendarOn && !ClockRunning && !Game.Paused && !UIState.Modal && !UIState.Cinematic && player && !player.Frozen)
            {
                solAcc += Time.deltaTime;
                if (solAcc > SecondsPerSol) { solAcc = 0; Sol++; }
            }
            UpdateTimerHolo();
            if (Game.InMenu && SpaceSet.I && SpaceSet.I.gameObject.activeSelf && cineCam && cineCam.enabled) SpaceSet.I.MenuTick();
            if (partsHunt && player && !busy) PartsUpdate();
            if (iceOn && player && !busy && !inside && !UIState.Modal) IceUpdate();
            // the oxygen-leak alarm happens once, the first time you come back into the shelter during the rocket hunt
            if (partsHunt) huntTime += Time.deltaTime;
            if (partsHunt && inside && !inGreenhouse && !busy && !Hazard && !leakDone && huntTime > 40 && MarsWorld.PowerOn && suit && suit.MoxieOn)
            { leakDone = true; StartCoroutine(AirLeak()); }
        }


        // ================================================================== guided tour of the Mars base
        /// <summary>NOVA explains the base from zero: the camera turns to each building while NOVA says what it is for.</summary>
        IEnumerator BaseTour()
        {
            if (MarsWorld.TheRover && MarsWorld.TheRover.Driving) MarsWorld.TheRover.Exit();
            busy = true; player.Frozen = true;
            var eye = player.Cam.transform;
            Vector3 look = eye.position + eye.forward * 20f;
            IEnumerator TurnTo(Vector3 target)
            {
                Vector3 from = look; float t = 0;
                while (t < 1f) { t += Time.deltaTime / 0.9f; look = Vector3.Lerp(from, target, Tween.EaseInOut(t)); player.LookAt(look); yield return null; }
            }
            yield return TurnTo(MarsWorld.MarsBase.position + Vector3.up * 12f);
            yield return hud.SayWait("Welcome to the MARS BASE! Let me show you around.", 2.5f);
            foreach (var (pos, line) in MarsWorld.BaseStops)
            {
                yield return TurnTo(pos - Vector3.up * 4f);
                yield return hud.SayWait(line, 3.5f);
            }
            yield return hud.SayWait("Now it's your turn: walk up to a building and press E!", 2.5f);
            player.Frozen = false; busy = false;
        }

        // ================================================================== guide helpers
        Guide guide => Guide.I;
        bool wearingSuit = true;
        bool NeedSuit => curRoom != null && curRoom == MarsWorld.HabitatRoom && !wearingSuit && MarsWorld.Locker;
        string ExitLabel() => NeedSuit ? "PUT ON SPACESUIT" : "EXIT DOOR";
        Vector3 ExitPos()
        {
            if (NeedSuit) return MarsWorld.Locker.transform.position;
            if (curRoom != null) return curRoom.ExitPanel.position;
            if (inGreenhouse && Greenhouse.I) return Greenhouse.I.ExitPanel.position;
            return MarsWorld.ExitDoorPanel ? MarsWorld.ExitDoorPanel.position : MarsWorld.InteriorSpawn;
        }
        float Dist(Vector3 a) => player ? Vector3.Distance(player.transform.position, a) : 999f;

        // ================================================================== MISSION 4: water from Mars ice
        int waterReady, dugIce; bool melting, drankIce;
        int IceCount() { int n = 0; foreach (var it in suit.Items) if (it.Id == "ICEBLOCK") n++; return n; }
        GameObject IceSite()
        {
            if (MarsWorld.CaveIce) return MarsWorld.CaveIce; // the ice is inside the ICE CAVE
            GameObject best = null; float bd = 1e9f;
            foreach (var g in MarsWorld.IceBlocks) { float d = Vector3.Distance(g.transform.position, MarsWorld.BunkerOutside); if (d < bd) { bd = d; best = g; } }
            return best;
        }
        void DigIce(GameObject site)
        {
            if (!suit.Add(new Item("ICEBLOCK", "Mars ice", "water", new Color(0.65f, 0.9f, 1f)))) { hud.Say("Your bag is full! Bring the ice to the water machine first."); return; }
            dugIce++;
            FX.BurstSparks(site.transform.position + Vector3.up, new Color(0.7f, 0.95f, 1f), 50); FX.DustBlast(site.transform.position, 0.5f);
            Sfx.Play("pickup", 0.8f, 1.3f);
            hud.Toast($"ICE DUG OUT: {IceCount()} IN YOUR BAG", "Ice from under the Mars dust. Melt it to get water!", "water", HC.Cyan);
        }
        void UseWaterMachine()
        {
            if (waterReady > 0)
            {
                waterReady--; drankIce = true; suit.Drink(100); Sfx.Play("powerup", 0.5f);
                hud.Toast("CLEAN WATER!", "You drank water made from Mars ice. Life support is full.", "water", HC.Cyan);
                return;
            }
            if (melting) { hud.Say("Still melting the ice. Wait a moment!"); return; }
            int n = IceCount();
            if (n == 0) { hud.Say("The water tank is EMPTY. Dig ICE at the blue ice field and bring it here."); return; }
            suit.Remove("ICEBLOCK"); StartCoroutine(MeltIce(n));
        }
        IEnumerator MeltIce(int n)
        {
            melting = true; Sfx.Play("build", 0.6f); Sfx.Play("hum", 0.4f);
            hud.Toast("MELTING THE ICE…", "1. Heat melts the ice.  2. Filters clean the water.", "water", HC.Cyan);
            yield return new WaitForSeconds(5f);
            melting = false; waterReady += n; Sfx.Play("success", 0.6f);
            hud.Say("Ding! Clean water is ready. Press E on the water machine to DRINK!", 4);
        }
        IEnumerator IceWater()
        {
            var site = IceSite(); if (!site) yield break;
            foreach (var g in MarsWorld.IceBlocks)
            {
                var gg = g; gg.SetActive(true);
                if (!gg.GetComponent<BoxCollider>()) { var bc = gg.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.center = new Vector3(0, 1f, 0); bc.size = new Vector3(3f, 2.2f, 3f); } // small touch zone (the light beam is 50 m tall)
                var dig = Interactable.Add(gg, "HOLD E to dig", "Mars ice", null, 1.5f);
                dig.HoldTime = 1.5f; dig.Range = 5f; dig.OnUse = () => DigIce(gg);
            }
            MarsWorld.Tap.Verb = "Use"; MarsWorld.Tap.Label = "WATER MACHINE (put ice in / drink)";
            guide.Mission(4, "ice", "GET WATER FROM ICE", "Go into the ICE CAVE", "HOLD E to dig 2 pieces of ice", "Bring the ice to the SHELTER",
                "Put the ice in the WATER MACHINE", "Wait: it melts the ice", "DRINK the clean water");
            yield return hud.SayWait("The water machine is in, but its tank is EMPTY. On Mars, water is FROZEN. NASA found ice under the dust, and scientists think dark caves can keep ice too: the Sun never reaches it there. Let's go to the ICE CAVE!", 6);
            while (!drankIce)
            {
                int have = IceCount();
                if (waterReady > 0)
                    guide.Focus(5, "The water is clean! Press E on the WATER MACHINE to DRINK it.", () => MarsWorld.Tap.transform.position, "WATER MACHINE", () => MarsWorld.BunkerOutside);
                else if (melting)
                    guide.Focus(4, "Wait a few seconds: the machine melts the ice, then filters clean the water.");
                else if (have >= 2 || (have > 0 && dugIce >= 2))
                {
                    if (!(inside && !inGreenhouse)) guide.Focus(2, "You have the ice! Bring it to the SHELTER and go inside.", () => MarsWorld.BunkerOutside, "SHELTER");
                    else guide.Focus(3, "Press E on the WATER MACHINE to put the ice in.", () => MarsWorld.Tap.transform.position, "WATER MACHINE");
                }
                else if (inside || Dist(site.transform.position) > 9f)
                    guide.Focus(0, MarsWorld.CaveIce ? "Follow the arrow to the ICE CAVE and walk inside. Take the ROVER: it is far! A lamp lights the tunnel." : "Follow the arrow to the blue ICE field. Take the ROVER: it is far! NASA found ice just under the Mars dust.", () => site.transform.position, MarsWorld.CaveIce ? "ICE CAVE" : "ICE FIELD");
                else
                    guide.Focus(1, $"HOLD E on the blue ice to dig it out. You have {have} of 2.", () => site.transform.position, "ICE");
                yield return null;
            }
            guide.Complete();
            AstroPass.Award("ice", "Made water from Mars ice", 3);
            yield return hud.SayWait("Fresh water from Mars ice! Water is too heavy to bring from Earth, so future astronauts will do exactly this.", 5);
        }

        // ================================================================== MISSION 5: visit the Mars base
        IEnumerator VisitBase()
        {
            if (!MarsWorld.MarsBase) yield break;
            yield return hud.SayWait("Air and water work! Now let's visit the MARS BASE next door. People live and work there.", 4);
            guide.Mission(5, "base", "VISIT THE MARS BASE", "Go to the MARS BASE", "Watch NOVA's tour");
            guide.Focus(0, "Go outside and follow the green arrow to the MARS BASE. You can walk, or drive the rover!", () => MarsWorld.MarsBase.position, "MARS BASE");
            while (inside || busy || Hazard || UIState.Modal || Dist(MarsWorld.MarsBase.position) > MarsWorld.BaseSize * 0.9f) yield return null;
            guide.Focus(1, "Watch the tour: NOVA shows you every building.");
            AstroPass.Award("base", "Visited the Mars base", 1);
            yield return BaseTour();
            guide.Complete();
            yield return new WaitForSeconds(1f);
        }

        // ================================================================== MISSION 5: grow food in the plantation
        IEnumerator GrowFood()
        {
            var gh = Greenhouse.I; if (!gh || !MarsWorld.GreenhouseDoor) yield break;
            guide.Mission(6, "food", "GROW AND COOK FOOD", "Go into the PLANTATION", gh.ModelMode ? "Switch on the grow LIGHTS" : "Plant the SEEDS",
                "Turn on the WATER PUMP", "Wait: the potatoes grow", "HARVEST them (into your bag)", "COOK them in the HABITAT kitchen", "EAT your meal!");
            yield return hud.SayWait("Astronauts can't bring food for years. They must GROW it! Let's go to the glass PLANTATION.", 4);
            ateMeal = false;
            while (!ateMeal)
            {
                if (suit.Has("MEAL"))
                    guide.Focus(6, "Your baked potatoes are ready! Press E at the KITCHEN to EAT them.", () => MarsWorld.Kitchen.transform.position, "KITCHEN", () => MarsWorld.HabitatOutside);
                else if (cooking)
                    guide.Focus(5, "The oven is baking your potatoes. Wait a few seconds!");
                else if (suit.Has("POTATO"))
                    guide.Focus(5, "Potatoes are in your bag! Go into the HABITAT and press E at the KITCHEN to cook them.", () => MarsWorld.Kitchen.transform.position, "KITCHEN", () => MarsWorld.HabitatOutside);
                else if (!(inGreenhouse && curRoom == null))
                    guide.Focus(0, "Go to the glass PLANTATION and press E at its door. Plants need light, water and soil.", () => MarsWorld.GreenhouseOutside, "PLANTATION");
                else switch (gh.State)
                {
                    case 0: guide.Focus(1, gh.ModelMode ? "Press E on the glowing LED switch. Plants need LIGHT to grow, and Mars is far from the Sun." : "Press E on a soil bed to plant the seeds.", gh.TargetPos, gh.ModelMode ? "LED SWITCH" : "SOIL BED"); break;
                    case 1: guide.Focus(2, "Press E on the WATER PUMP. The water comes from melted Mars ice!", gh.TargetPos, "WATER PUMP"); break;
                    case 2: guide.Focus(3, "Wait a few seconds. The potatoes are growing!"); break;
                    case 3: guide.Focus(4, "The potatoes are ready! Press E on a soil bed to HARVEST them.", gh.TargetPos, "SOIL BED"); break;
                    default: guide.Focus(5, "Press E on the harvest crate to EAT your potatoes. Food gives you energy!", gh.TargetPos, "HARVEST CRATE"); break;
                }
                yield return null;
            }
            guide.Complete();
            while (busy || UIState.Modal || hud.Talking) yield return null;
        }

        // ================================================================== MISSION 6: plan the night power, then sleep
        bool slept;
        IEnumerator NightAndSleep()
        {
            if (!MarsWorld.SolarPanel) yield break;
            while (busy || UIState.Modal) yield return null;
            yield return Game.Sky.Timelapse(0.7f, 3f); // the Sun goes down
            hud.Warn("SUNSET: SOLAR PANELS STOPPING", 4, false);
            yield return hud.SayWait("Night is coming! Solar panels make no power in the dark. Tonight the base lives on its BATTERY.", 4);
            guide.Mission(7, "night", "SURVIVE THE NIGHT", "Go to the SOLAR PANELS", "Plan the night power", "SLEEP in a bunk bed");
            bool ok = false; System.Action done = () => ok = true;
            Computers.PowerPlanOk += done;
            while (!ok)
            {
                if (inside || Dist(MarsWorld.SolarPanel.position) > 9f)
                    guide.Focus(0, "Go to the SOLAR PANELS. At night they make no power, so we must plan the battery.", () => MarsWorld.SolarPanel.position, "SOLAR PANELS");
                else guide.Focus(1, "Press E on a solar panel. Choose what the battery powers tonight. AIR and HEAT come first!", () => MarsWorld.SolarPanel.position, "SOLAR PANELS");
                yield return null;
            }
            Computers.PowerPlanOk -= done;
            while (UIState.Modal) yield return null; // wait until the planner is closed
            slept = false;
            var bunk = MarsWorld.Bunk;
            if (bunk) guide.Focus(2, "Great plan! Now go into the HABITAT and press E on a bunk bed to sleep until morning.", () => bunk.transform.position, "BUNK BED", () => MarsWorld.HabitatOutside);
            else { slept = true; }
            while (!slept) yield return null;
            guide.Complete();
            hud.Toast("GOOD MORNING!", "Your power plan worked. The Sun is up: the solar panels charge the battery again.", "sun", HC.Amber);
            AstroPass.Award("night", "Survived the night", 2);
            yield return new WaitForSeconds(1.5f);
        }
        // ================================================================== mission: fix the oxygen leak
        float huntTime; bool leakDone; Waypoint wpBase;
        public IEnumerator AirLeak()
        {
            Hazard = true;
            yield return new WaitForSeconds(2f);
            MarsWorld.LeakSteam.Play();
            var hiss = AudioManager.I.Loop("wind", 0.7f, MarsWorld.LeakSteam.transform, 1, 18); hiss.pitch = 2.4f;
            MarsWorld.AlarmLight.intensity = 3; MarsWorld.AlarmLight.GetComponent<Blink>().SetBase(3);
            hud.Warn("AIR LEAK! FIX IT FAST!", 5);
            var wp = hud.AddWaypoint("AIR LEAK", () => MarsWorld.LeakPos, HC.Red);
            guide.Urgent("AIR LEAK!", "HOLD E on the steaming pipe", "Air is leaking out! Find the white steam and HOLD E on the pipe to patch it.", () => MarsWorld.LeakPos, "AIR LEAK");
            hud.Say("ALARM! Air is leaking out of that pipe! Look for the white steam and hold E to patch it, quick!", 5);
            bool fixedIt = false; float t = 0;
            MarsWorld.LeakValve.enabled = true; MarsWorld.LeakValve.OnUse = () => fixedIt = true;
            while (!fixedIt)
            {
                t += Time.deltaTime;
                if (inside && !UIState.Modal) suit.O2 = Mathf.Max(8, suit.O2 - 10f * Time.deltaTime); // the air rushes out
                yield return null;
            }
            MarsWorld.LeakValve.enabled = false;
            MarsWorld.LeakSteam.Stop();
            AudioManager.I.FadeSource(hiss, 0, 0.5f); Destroy(hiss.gameObject, 1f);
            MarsWorld.AlarmLight.GetComponent<Blink>().SetBase(0); MarsWorld.AlarmLight.intensity = 0;
            hud.RemoveWaypoint(wp);
            Sfx.Play("build", 0.7f); Sfx.Play("powerup", 0.5f);
            hud.Toast("LEAK FIXED!", "MOXIE will refill the air now.", "air", HC.Green);
            AstroPass.Award("leak", t < 25 ? "Super-fast leak fix" : "Leak fixed", t < 25 ? 3 : 2);
            guide.EndUrgent();
            yield return hud.SayWait("You saved our air! Real fact: in 2020, astronauts on the Space Station found a tiny air leak by watching floating TEA LEAVES drift toward the hole!", 6);
            Hazard = false;
        }

        void ChooseWindow(System.DateTime after)
        {
            if (NasaLive.NextClosest(after, out var w, out _)) WindowDate = w; else WindowDate = after.AddDays(780);
            DaysLeft = Mathf.Max(1, (int)(WindowDate - Date).TotalDays);
            SecondsPerDay = Mathf.Clamp(180f / DaysLeft, 0.2f, 6f); // about 3 minutes of play
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
            WindowsMissed++;
            var missed = WindowDate;
            ChooseWindow(missed.AddDays(45));
            hud.Warn("LAUNCH WINDOW MISSED", 5);
            yield return hud.SayWait($"Oh no! The window of {missed:d MMMM yyyy} closed and Earth is moving away. NASA JPL says the next one is {WindowDate:d MMMM yyyy}: about 2 years more on Mars! Let's be faster!", 6);
        }

        void UpdateTimerHolo()
        {
            string s = DaysLeft > 0 ? $"<size=55%>EARTH LAUNCH WINDOW · {WindowDate:d MMM yyyy}</size>\nT-{DaysLeft} DAYS\n<size=45%>{DaysLeft / 30.4f:0.0} MONTHS</size>" : "<size=55%>LAUNCH WINDOW</size>\nOPEN";
            if (MarsWorld.TimerText) MarsWorld.TimerText.text = s;
            if (MarsWorld.TimerInside) MarsWorld.TimerInside.text = ClockRunning || DaysLeft < 780 ? s : "<size=55%>NASA DATA TERMINAL</size>\n<size=40%>PRESS E</size>";
        }

        // ================================================================== main flow
        public IEnumerator Run()
        {
            UIState.Reset();
            Voice.Warm(); // the browser loads its voices a moment after the page opens
            pool.AddRange(NasaData.FlarePool());
            // ---- TITLE ----
            Game.InMenu = true; UIState.Refresh();
            var space = SpaceSet.I;
            space.Enter(cineCam); space.MenuPose();
            AudioManager.I.StopMusic(0.1f); // the title screen is quiet (no music)
            bool play = false; screens.OnPlay = () => { play = true; resume = null; SaveGame.Clear(); };
            screens.OnRestart = () => SceneManager.LoadScene(0);
            screens.OnQuit = () => StartCoroutine(QuitWithFlyAway(space));
            // a saved game on this device: offer CONTINUE
            var saved = QuickStart || Game.ExploreBase ? null : SaveGame.Load();
            if (saved != null) screens.SetContinue(saved.cp, saved.stars, () => { play = true; resume = saved; });
            if (saved != null && AutoContinue) { play = true; resume = saved; }
            if (!QuickStart && !Game.ExploreBase)
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
            if (!Game.ExploreBase && resume == null) yield return space.Intro();
            Cinema.I.Letterbox(false, 0.01f);
            space.Exit(cineCam);
            cineCam.enabled = false;
            if (Game.ExploreBase) cineCam.GetComponent<AudioListener>().enabled = false;

            int cp = 3;
            if (resume != null) { cp = resume.cp; yield return Resume(resume); }
            else
            {
                // ---- WAKE UP ----
                yield return WakeUp();
                if (Game.ExploreBase) yield break;
                calendarOn = true; // from now on the days go by
                // ---- 1 STORM → SHELTER, 2 POWER ----
                yield return FirstStorm();
                Checkpoint(3);
            }
            // the game saves after every mission, so leaving the website does not lose the progress
            if (cp <= 3) { yield return Salvage(); Checkpoint(4); }          // 3 AIR + WATER
            if (cp <= 4) { yield return IceWater(); Checkpoint(5); }         // 4 ICE → WATER
            if (cp <= 5) { yield return VisitBase(); Checkpoint(6); }        // 5 MARS BASE
            if (cp <= 6) { yield return GrowFood(); Checkpoint(7); }         // 6 FOOD
            if (cp <= 7) { yield return NightAndSleep(); Checkpoint(8); }    // 7 NIGHT
            if (cp <= 8)
            {                                                                  // 8 ROCKET PARTS
                yield return PartsIntro();
                while (partsInstalled < MarsWorld.Parts.Length) yield return null;
                while (busy) yield return null;
                Checkpoint(9);
            }
            // ---- 9 LAUNCH ----
            yield return Launch();
        }

        // ================================================================== save / continue
        SaveData resume;
        void Checkpoint(int nextMission)
        {
            if (QuickStart || Game.ExploreBase) return;
            SaveGame.Write(new SaveData
            {
                cp = nextMission, sol = Sol, startDate = StartDate.ToString("yyyy-MM-dd"),
                stars = AstroPass.I ? AstroPass.I.Stars : 0, done = AstroPass.DoneList(),
                o2 = suit ? suit.O2 : 100, health = suit ? suit.Health : 100, dose = suit ? suit.DoseMSv : 0,
                windowsMissed = WindowsMissed,
            });
        }

        /// <summary>CONTINUE: build the world as it was after the saved mission, then start in the shelter.</summary>
        IEnumerator Resume(SaveData s)
        {
            var spawn = MarsWorld.G(MarsWorld.WakeXZ, 0.1f);
            SetupPlayer(spawn);
            if (System.DateTime.TryParse(s.startDate, out var sd)) StartDate = sd;
            Sol = Mathf.Max(1, s.sol); WindowsMissed = s.windowsMissed;
            AstroPass.Restore(s.stars, s.done);
            suit.O2 = Mathf.Max(60, s.o2); suit.Health = Mathf.Max(60, s.health); suit.DoseMSv = s.dose;
            codeOk = true; lookedEarth = true; wearingSuit = true;
            // missions 1 + 2: the shelter has power
            MarsWorld.SetPower(1);
            if (s.cp == 3) { MarsWorld.AlarmLight.intensity = 3; MarsWorld.AlarmLight.GetComponent<Blink>().SetBase(3); }
            if (s.cp >= 4)
            {   // mission 3: air and water machines are in, the rover is unlocked, every door works
                MarsWorld.MoxieGhost.SetActive(false); MarsWorld.MoxieInstalled.SetActive(true);
                MarsWorld.WaterGhost.SetActive(false); MarsWorld.WaterInstalled.SetActive(true);
                suit.MoxieOn = true; suit.WaterOn = true;
                MarsWorld.MoxieCrate.gameObject.SetActive(false); MarsWorld.WaterCrate.gameObject.SetActive(false);
                MarsWorld.MoxieSlot.enabled = false; MarsWorld.WaterSlot.enabled = false;
                if (MarsWorld.TheRover) MarsWorld.TheRover.Use.enabled = true;
                MarsWorld.Tap.OnUse = UseWaterMachine;
                MarsWorld.DataConsole.OnUse = () => screens.OpenData(false);
                foreach (var p in MarsWorld.Pouches) p.gameObject.SetActive(false);
                WireDoors();
            }
            if (s.cp >= 5) drankIce = true;
            if (s.cp >= 7) ateMeal = true;
            if (s.cp >= 9)
            {   // mission 8: the new rocket is built
                foreach (var p in MarsWorld.Parts) { p.Found = true; p.Installed = true; if (p.Go) p.Go.SetActive(false); MarsWorld.BuildSection(p.Section); }
                partsInstalled = MarsWorld.Parts.Length;
                MarsWorld.TimerHolo.SetActive(true);
                ChooseWindow(Date.AddDays(45)); ClockRunning = true;
            }
            // start inside the shelter, safe
            MarsWorld.SetSurface(false); MarsWorld.Interior.SetActive(true);
            Game.Sky.Mode = SkyMode.Interior; Game.Sky.Apply();
            player.Teleport(MarsWorld.InteriorSpawn, MarsWorld.InteriorYaw);
            bot.Teleport(MarsWorld.InteriorSpawn + new Vector3(1.2f, 1.8f, 1.5f)); bot.gameObject.SetActive(false);
            suit.Inside = true; inside = true;
            if (AudioManager.I.Wind) AudioManager.I.FadeSource(AudioManager.I.Wind, 0.05f, 0.1f);
            Guide.Exit = ExitPos; Guide.ExitName = ExitLabel;
            calendarOn = true;
            UIState.Cinematic = false; UIState.Refresh();
            Cinema.I.Eyes(1);
            yield return Cinema.I.FromBlack(0.8f);
            player.Frozen = false;
            hud.Show(true, 0.6f);
            suit.Active = true;
            AudioManager.I.Music("music_ambient", 0.35f, 3f);
            yield return hud.SayWait($"Welcome back, Commander! Your game was saved. Let's go on with MISSION {s.cp}!", 4);
        }

        // ================================================================== wake up
        IEnumerator WakeUp()
        {
            var spawn = MarsWorld.G(MarsWorld.WakeXZ, 0.1f);
            SetupPlayer(spawn);

            if (Game.ExploreBase)
            {
                var rover = MarsWorld.TheRover;
                var plaza = MarsWorld.MarsBase.TransformPoint(new Vector3(4, 0, 16));
                rover.Park(plaza, MarsWorld.MarsBase.eulerAngles.y + 30);
                rover.Use.enabled = true;
                var from = rover.transform.position + rover.transform.forward * 3.6f + rover.transform.right * 2.2f;
                player.Teleport(MarsWorld.G(from.x, from.z, 0.1f), 0);
                player.LookAt(rover.transform.position + Vector3.up * 1.4f);
                bot.Teleport(player.transform.position + Vector3.up * 2 + player.transform.right * 3);
                WireDoors();
                MarsWorld.SetPower(0);
                UIState.Cinematic = false; UIState.Refresh();
                player.Frozen = false;
                Cinema.I.SetBlack(0); Cinema.I.Eyes(1);
                hud.Show(true);
                gameObject.AddComponent<BaseExpedition>().Begin();
                yield break;
            }
            yield return WakeCinematic(spawn);
        }

        /// <summary>The astronaut, the suit, the HUD, NOVA and the Mars wind (new game and CONTINUE).</summary>
        void SetupPlayer(Vector3 spawn)
        {
            player = PlayerController.Create(spawn, 0); Game.Player = player; Game.Cam = player.Cam;
            Story.ConfigCam(player.Cam);
            var inter = player.gameObject.AddComponent<Interactor>(); inter.P = player;
            suit = player.gameObject.AddComponent<Survival>(); Game.Suit = suit;
            suit.O2 = 64; suit.Water = 70; suit.Health = 72;
            suit.OnCollapse = why => StartCoroutine(Collapse(why));
            hud = Game.Hud; hud.Interactor = inter;
            AstroPass.Create(hud);
            MarsWorld.SetPower(0); // the crash knocked out the shelter's power: the first job inside is to turn it back on
            if (MarsWorld.TheRover) MarsWorld.TheRover.Use.enabled = false; // the rover is unlocked after the storm
            bot = Robot.Create(spawn + new Vector3(30, 20, 30));
            Game.Sky.TimeOfDay = 0.36f; Game.Sky.Apply();
            AudioManager.I.Wind = AudioManager.I.Loop("wind", 0.35f);
        }

        IEnumerator WakeCinematic(Vector3 spawn)
        {
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
            if (NasaApi.I) { var ins = NasaApi.I.LastSol; yield return hud.SayWait($"Brrr! NASA's InSight lander measured real Mars weather: {ins.tempMaxC:0} °C in the day and {ins.tempMinC:0} °C at night. Your suit keeps you warm!", 5); }
            Guide.Exit = ExitPos; Guide.ExitName = ExitLabel;
            guide.Note("LOOK AROUND", "Move the MOUSE. Find EARTH: the blue dot in the sky!", "Move the mouse to look around. Earth is the small blue dot in the sky.");
            yield return hud.SayWait("See that blue dot in the sky? That's EARTH, our home! The grey rock is PHOBOS, a moon of Mars.");
            float w = 0;
            var earthDir = MarsWorld.Earth.GetComponent<SkyBody>().Dir;
            while (w < 20 && !lookedEarth)
            {
                w += Time.deltaTime;
                if (Vector3.Dot(player.Cam.transform.forward, earthDir) > 0.99f) lookedEarth = true;
                yield return null;
            }
            if (lookedEarth) yield return new WaitForSeconds(1.5f);
            { var nl = NasaLive.I; if (nl) yield return hud.SayWait($"NASA's JPL says Earth is {nl.DistanceText} away from us TODAY. Even light needs {nl.LightMinutes:0} minutes to get there!", 5); }
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
            guide.Mission(1, "storm", "HIDE FROM THE STORM", "Run to the SHELTER (hold SHIFT)", "Press E and type the DOOR CODE");
            guide.Focus(0, "Space rays are coming! Run to the SHELTER: Mars soil on top of it blocks the rays. Follow the green arrow and hold SHIFT to run.", () => MarsWorld.BunkerOutside, "SHELTER");
            hud.Say("Mars has no magnetic shield like Earth. Run to the bunker — hold SHIFT to run!", 6);
            // storm ramps up
            StartCoroutine(StormRamp(Mathf.Clamp(0.55f * CurrentFlare.Strength, 0.4f, 0.9f), 4f));
            bot.Lead = MarsWorld.BunkerOutside + Vector3.up * 2.5f;
            bool entered = false;
            MarsWorld.BunkerDoor.OnUse = () => { if (!askingCode) StartCoroutine(DoorCode(() => entered = true)); };
            // until mission 3 the shelter's inside door explains instead of doing nothing
            MarsWorld.ExitDoor.OnUse = () => hud.Say("Stay inside for now, Commander! The storm is dangerous and the shelter needs POWER first.", 4);
            // "inside": NOVA carried you into the shelter (air or radiation ran out on the way) — that counts too
            while (!entered && !inside)
            {
                if (askingCode) guide.Focus(1, "Type the door code 1 1 2 2. It is at the end of your Junior Astronaut Handbook!");
                else if (Dist(MarsWorld.BunkerOutside) < 7f) guide.Focus(1, "Press E at the big door, then type the door code.", () => MarsWorld.BunkerOutside, "SHELTER DOOR");
                yield return null;
            }
            guide.Complete();
            bot.Lead = null;
            if (inside) { while (busy) yield return null; }
            else yield return EnterBunker();
            // time passes: storm ends (kept short — about 1.5 seconds)
            yield return Cinema.I.ToBlack(0.3f);
            suit.Storm = 0; StopAllStorms();
            Cinema.I.Caption("A few hours later — the storm is over.", 1.5f);
            Game.Sky.TimeOfDay = 0.45f;
            yield return new WaitForSeconds(0.9f);
            yield return Cinema.I.FromBlack(0.3f);
            hud.Toast("YOU SURVIVED THE STORM", $"Real flare {CurrentFlare.Class} ({CurrentFlare.Date}). 2 metres of Mars soil above the shelter blocked the radiation.", "shield", HC.Green);
            AstroPass.Award("storm", "Hid from the space storm", 2);
            yield return PowerUp();
            MarsWorld.AlarmLight.intensity = 3; MarsWorld.AlarmLight.GetComponent<Blink>().SetBase(3);
            Sfx.Play("alarm", 0.5f);
            yield return hud.SayWait("Lights are on! But the shelter has no machines: air and water are running out!", 3f);
        }

        // ================================================================== mission: power up the shelter
        IEnumerator PowerUp()
        {
            yield return hud.SayWait("Phew, we're safe! But it's so DARK in here. The crash knocked out our POWER!");
            guide.Mission(2, "power", "TURN ON THE POWER", "Plug in the BATTERY cable", "HOLD E on the MAIN SWITCH");
            guide.Focus(0, "Press E on the BATTERY to plug in the cable. The battery keeps energy from the solar panels.", () => MarsWorld.BatteryPlug.transform.position, "BATTERY");
            Waypoint wp = null;
            bool plugged = false, on = false;
            MarsWorld.BatteryPlug.enabled = true;
            MarsWorld.BatteryPlug.OnUse = () => { if (plugged) return; plugged = true; MarsWorld.BatteryPlug.enabled = false; MarsWorld.Cable.SetActive(true); Sfx.Play("build", 0.7f); };
            while (!plugged) yield return null;
            hud.RemoveWaypoint(wp);
            hud.Say("Cable in! This battery stores energy from the SOLAR PANELS on top of the shelter. Now hold E on the MAIN SWITCH!", 5);
            guide.Focus(1, "Now HOLD E on the MAIN SWITCH until the ring is full. It sends power to the lights and machines.", () => MarsWorld.PowerSwitch.transform.position, "MAIN SWITCH");
            MarsWorld.PowerSwitch.enabled = true; MarsWorld.PowerSwitch.OnUse = () => on = true;
            while (!on) yield return null;
            MarsWorld.PowerSwitch.enabled = false; guide.Complete();
            // the lights flicker... and come on!
            Sfx.Play("powerup", 0.8f);
            foreach (var k in new[] { 0.35f, 0f, 0.6f, 0.1f, 1f }) { MarsWorld.SetPower(k); Sfx.Play("click", 0.6f, 0.7f + k); yield return new WaitForSeconds(0.18f); }
            MarsWorld.SetPower(1);
            Sfx.Play("hum", 0.4f);
            hud.Toast("POWER ON!", "Solar panels charge the battery in the day. At night the battery keeps the lights on.", "power", HC.Amber);
            AstroPass.Award("power", "Power restored", 2);
            yield return new WaitForSeconds(1.2f);
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
        bool codeOk, askingCode;
        /// <summary>The first time at the shelter you must type the door code (1122, last page of the Handbook).</summary>
        IEnumerator DoorCode(System.Action ok)
        {
            if (codeOk) { ok(); yield break; }
            askingCode = true;
            hud.Say("The door is locked! Type the code: it is at the END of your Junior Astronaut Handbook.", 5);
            yield return Keypad.Ask();
            codeOk = true; askingCode = false;
            AstroPass.Award("code", "Opened the shelter with the secret code", 1);
            ok();
        }

        IEnumerator EnterBunker()
        {
            busy = true;
            player.Frozen = true;
            var outer = MarsWorld.BunkerDoorPanel.GetComponent<SlideDoor>();
            yield return outer.Move(true); // the door slides open...
            yield return WalkThroughLock(MarsWorld.Vestibule.TransformPoint(new Vector3(0,.06f,1.4f)), MarsWorld.BunkerOutsideYaw+180);
            yield return outer.Move(false);
            yield return MarsWorld.InnerHatch.Move(true);
            yield return Cinema.I.WhiteOut(0.35f);
            MarsWorld.InnerHatch.Snap(false);
            outer.Snap(false);              // ...and shuts behind you
            MarsWorld.ExitDoorPanel.GetComponent<SlideDoor>().Snap(false);
            Sfx.Play("thud", 0.4f, 1.2f);
            Sfx.Play("whoosh", 0.4f, 0.6f);
            MarsWorld.SetSurface(false); MarsWorld.Interior.SetActive(true);
            Game.Sky.Mode = SkyMode.Interior; Game.Sky.Apply();
            player.Teleport(MarsWorld.InteriorSpawn, MarsWorld.InteriorYaw);
            bot.Teleport(MarsWorld.InteriorSpawn + new Vector3(1.2f, 1.8f, 1.5f)); bot.gameObject.SetActive(false); // NOVA waits outside
            suit.Inside = true; inside = true;
            if (AudioManager.I.Wind) AudioManager.I.FadeSource(AudioManager.I.Wind, 0.05f, 0.5f);
            yield return Cinema.I.WhiteIn(0.4f);
            player.Frozen = false; busy = false;
        }

        IEnumerator ExitBunker()
        {
            busy = true;
            player.Frozen = true;
            var inner = MarsWorld.ExitDoorPanel.GetComponent<SlideDoor>();
            yield return inner.Move(true);
            yield return Cinema.I.WhiteOut(0.35f);
            inner.Snap(false);
            Sfx.Play("whoosh", 0.4f, 0.6f);
            MarsWorld.Interior.SetActive(false); MarsWorld.SetSurface(true);
            var outer = MarsWorld.BunkerDoorPanel.GetComponent<SlideDoor>();
            outer.Snap(false);

            Game.Sky.Mode = SkyMode.Surface; Game.Sky.Apply();
            player.Teleport(MarsWorld.Vestibule.TransformPoint(new Vector3(0,.06f,1.4f)), MarsWorld.BunkerOutsideYaw);
            bot.Teleport(MarsWorld.BunkerOutside + new Vector3(1.5f, 2, 1.5f)); bot.gameObject.SetActive(true);
            suit.Inside = false; inside = false;
            if (AudioManager.I.Wind) AudioManager.I.FadeSource(AudioManager.I.Wind, 0.35f, 0.5f);
            yield return Cinema.I.WhiteIn(0.4f);
            yield return outer.Move(true);
            yield return WalkThroughLock(MarsWorld.BunkerOutside, MarsWorld.BunkerOutsideYaw);
            Game.Runner.StartCoroutine(CloseLater(outer));
            player.Frozen = false; busy = false;
        }

        IEnumerator WalkThroughLock(Vector3 end, float yaw)
        {
            var start=player.transform.position;
            var angle=player.YawDeg;
            for(float t=0;t<1;t+=Time.deltaTime/1.1f)
            {
                float k=Mathf.SmoothStep(0,1,t);
                player.Teleport(Vector3.Lerp(start,end,k),Mathf.LerpAngle(angle,yaw,k));
                yield return null;
            }
            player.Teleport(end,yaw);
        }
        static IEnumerator CloseLater(SlideDoor d)
        {
            yield return new WaitForSeconds(1.1f);
            if (d && d.gameObject.activeInHierarchy) yield return d.Move(false);
        }

        // ================================================================== the PLANTATION (walk-in greenhouse)
        bool inGreenhouse; string savedObjTitle, savedObjBody, savedObjId;
        IEnumerator EnterGreenhouse()
        {
            var gh = Greenhouse.I; if (!gh) yield break;
            busy = true; player.Frozen = true;
            savedObjTitle = hud.LastTitle; savedObjBody = hud.LastBody; savedObjId = hud.ObjectiveId;
            var outer = MarsWorld.GreenhouseDoorPanel ? MarsWorld.GreenhouseDoorPanel.GetComponent<SlideDoor>() : null;
            if (outer) yield return outer.Move(true); // the door slides open...
            yield return Cinema.I.WhiteOut(0.35f);
            if (outer) outer.Snap(false);              // ...and shuts behind you
            MarsWorld.SetSurface(false); gh.Room.SetActive(true);
            Game.Sky.Mode = SkyMode.Interior; Game.Sky.InteriorBright = 1.3f; Game.Sky.Apply();
            gh.ExitPanel.GetComponent<SlideDoor>().Snap(false);
            player.Teleport(gh.Spawn, gh.SpawnYaw);
            bot.Teleport(gh.Spawn + new Vector3(1.2f, 1.8f, 1.5f));
            suit.Inside = true; inside = true; inGreenhouse = true;
            player.GetComponent<CharacterController>().radius = 0.28f; // slimmer between the plant beds
            if (gh.BedsCollider) Physics.IgnoreCollision(player.GetComponent<CharacterController>(), gh.BedsCollider, true); // walk between the plants
            bot.gameObject.SetActive(false); // NOVA waits outside
            if (AudioManager.I.Wind) AudioManager.I.FadeSource(AudioManager.I.Wind, 0.05f, 0.5f);
            yield return Cinema.I.WhiteIn(0.4f);
            player.Frozen = false; busy = false;
            StartCoroutine(gh.OnEnter(hud));
        }

        IEnumerator ExitGreenhouse()
        {
            var gh = Greenhouse.I; if (!gh) yield break;
            busy = true; player.Frozen = true;
            yield return gh.ExitPanel.GetComponent<SlideDoor>().Move(true);
            yield return Cinema.I.WhiteOut(0.35f);
            gh.Room.SetActive(false); MarsWorld.SetSurface(true);
            Game.Sky.Mode = SkyMode.Surface; Game.Sky.InteriorBright = MarsWorld.PowerOn ? 1f : 0.35f; Game.Sky.Apply();
            player.Teleport(MarsWorld.GreenhouseOutside, MarsWorld.GreenhouseOutsideYaw);
            bot.Teleport(MarsWorld.GreenhouseOutside + new Vector3(1.5f, 2, 1.5f));
            suit.Inside = false; inside = false; inGreenhouse = false;
            player.GetComponent<CharacterController>().radius = 0.38f; bot.gameObject.SetActive(true);
            if (AudioManager.I.Wind) AudioManager.I.FadeSource(AudioManager.I.Wind, 0.35f, 0.5f);
            yield return Cinema.I.WhiteIn(0.4f);
            player.Frozen = false; busy = false;
        }

        // ================================================================== HABITAT + WEATHER LAB (walk-in rooms)
        ModelRoom curRoom; Vector3 roomOutside; float roomOutsideYaw;
        IEnumerator EnterRoom(ModelRoom room, Vector3 outside, string hello)
        {
            if (room == null) yield break;
            busy = true; player.Frozen = true;
            savedObjTitle = hud.LastTitle; savedObjBody = hud.LastBody; savedObjId = hud.ObjectiveId;
            Sfx.Play("whoosh", 0.4f, 1.3f);
            yield return Cinema.I.WhiteOut(0.35f);
            MarsWorld.SetSurface(false); room.Room.SetActive(true);
            Game.Sky.Mode = SkyMode.Interior; Game.Sky.InteriorBright = 1.4f; Game.Sky.Apply();
            room.ExitPanel.GetComponent<SlideDoor>().Snap(false);
            player.Teleport(room.Spawn, room.SpawnYaw);
            bot.Teleport(room.Spawn + Vector3.up * 1.8f);
            suit.Inside = true; inside = true; inGreenhouse = true; curRoom = room; roomOutside = outside; roomOutsideYaw = MarsWorld.GreenhouseOutsideYaw;
            player.GetComponent<CharacterController>().radius = 0.3f; bot.gameObject.SetActive(false); // NOVA waits outside
            if (AudioManager.I.Wind) AudioManager.I.FadeSource(AudioManager.I.Wind, 0.05f, 0.5f);
            yield return Cinema.I.WhiteIn(0.4f);
            player.Frozen = false; busy = false;
            if (!string.IsNullOrEmpty(hello)) hud.Say(hello, 5);
            if (room == MarsWorld.HabitatRoom)
            {
                wearingSuit = false;
                if (MarsWorld.LockerSuits != null) foreach (var g in MarsWorld.LockerSuits) if (g) g.SetActive(true);
                hud.Toast("HELMET OFF", "The habitat has air. Put your SPACESUIT on again before you go outside!", "air", HC.Cyan);
            }
        }

        /// <summary>The spacesuit locker: put on a suit (helmet sealed, air tank full, suit battery charged).</summary>
        IEnumerator SuitUp()
        {
            if (wearingSuit) { suit.O2 = 100; hud.Toast("SPACESUIT CHECK", "Helmet sealed, air tank full. You are ready to go outside!", "air", HC.Cyan); yield break; }
            busy = true; player.Frozen = true;
            yield return MarsWorld.CabinetOpen(true);   // the glass doors swing open...
            yield return new WaitForSeconds(0.35f);
            yield return Cinema.I.ToBlack(0.35f);
            if (MarsWorld.LockerSuits != null && MarsWorld.LockerSuits.Length > 0 && MarsWorld.LockerSuits[0]) MarsWorld.LockerSuits[0].SetActive(false); // you take one suit
            yield return MarsWorld.CabinetOpen(false, 0.05f); // ...and are shut again when you look
            Sfx.Play("powerup", 0.5f); Sfx.Play("click", 0.6f, 0.8f);
            Cinema.I.Caption("Boots… gloves… helmet… CLICK! Air: ON.", 1.6f);
            wearingSuit = true; suit.O2 = 100;
            yield return new WaitForSeconds(1.2f);
            yield return Cinema.I.FromBlack(0.35f);
            hud.Toast("SPACESUIT ON", "Helmet sealed, air tank full, suit battery charged. A spacesuit is a tiny spaceship!", "air", HC.Cyan);
            AstroPass.Award("suit", "Put on a spacesuit", 1);
            player.Frozen = false; busy = false;
        }

        IEnumerator ExitRoom()
        {
            var room = curRoom; if (room == null || (BaseExpedition.I && !BaseExpedition.I.CanExit())) yield break;
            if (NeedSuit) { hud.Say("Wait! Outside there is no air to breathe. Put on your SPACESUIT first: press E at the spacesuit locker.", 5); Sfx.Play("alarm", 0.3f, 1.5f); yield break; }
            busy = true; player.Frozen = true;
            yield return room.ExitPanel.GetComponent<SlideDoor>().Move(true);
            yield return Cinema.I.WhiteOut(0.35f);
            room.Room.SetActive(false); MarsWorld.SetSurface(true);
            Game.Sky.Mode = SkyMode.Surface; Game.Sky.InteriorBright = MarsWorld.PowerOn ? 1f : 0.35f; Game.Sky.Apply();
            player.Teleport(roomOutside, roomOutsideYaw);
            bot.Teleport(roomOutside + new Vector3(1.5f, 2, 1.5f));
            suit.Inside = false; inside = false; inGreenhouse = false; curRoom = null; bot.gameObject.SetActive(true);
            player.GetComponent<CharacterController>().radius = 0.38f;
            if (AudioManager.I.Wind) AudioManager.I.FadeSource(AudioManager.I.Wind, 0.35f, 0.5f);
            yield return Cinema.I.WhiteIn(0.4f);
            player.Frozen = false; busy = false;
        }

        /// <summary>Lie down ON the bed (head on the pillow, looking up), the eyes close slowly, one sol passes, you wake up and get up.</summary>
        IEnumerator Sleep(Interactable bed)
        {
            busy = true; player.Frozen = true;
            var from = player.transform.position; float fromYaw = player.YawDeg, h0 = player.HeadHeight;
            // the lower bunk: lie along the long side of the bed, head at one end
            var b = Util.Bounds(bed.gameObject);
            bool alongX = b.size.x > b.size.z; var axis = alongX ? Vector3.right : Vector3.forward;
            float len = alongX ? b.size.x : b.size.z;
            var headSpot = b.center - axis * (len * 0.5f - 0.4f); headSpot.y = b.min.y + 0.32f;
            float feetYaw = Quaternion.LookRotation(axis).eulerAngles.y;
            player.Resting = true;
            Sfx.Play("breath", 0.6f);
            // climb in: move onto the mattress and lie back
            for (float t = 0; t < 1; t += Time.deltaTime / 1.2f)
            {
                float k = Tween.EaseInOut(t);
                player.HeadHeight = Mathf.Lerp(h0, 0.25f, k);
                player.Teleport(Vector3.Lerp(from, headSpot, k), Mathf.LerpAngle(fromYaw, feetYaw, k), Mathf.Lerp(0, -80, k));
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);
            // the eyes close slowly
            yield return Lids(1, 0.35f, 1.4f); yield return Lids(0.35f, 0.6f, 0.5f); yield return Lids(0.6f, 0, 1.6f);
            Cinema.I.SetBlack(1); Cinema.I.Caption("Zzz… one sol (24 hours 39 minutes) passes.", 2.2f);
            Sol++; if (ClockRunning && DaysLeft > 1) DaysLeft--;
            if (Game.Sky.Day < 0.3f) { Game.Sky.TimeOfDay = 0.32f; Game.Sky.Apply(); } // you wake up in the morning
            suit.Restore(100, 100, 100); suit.O2 = 100; suit.Health = 100;
            yield return new WaitForSeconds(2.2f);
            Cinema.I.SetBlack(0); Cinema.I.Eyes(0);
            yield return Lids(0, 1, 1.4f);
            yield return new WaitForSeconds(0.6f);
            // get up next to the bed
            for (float t = 0; t < 1; t += Time.deltaTime / 1.0f)
            {
                float k = Tween.EaseInOut(t);
                player.HeadHeight = Mathf.Lerp(0.25f, h0, k);
                player.Teleport(Vector3.Lerp(headSpot, from, k), Mathf.LerpAngle(feetYaw, fromYaw, k), Mathf.Lerp(-80, 0, k));
                yield return null;
            }
            player.HeadHeight = h0; player.Teleport(from, fromYaw); player.Resting = false;
            hud.Toast("GOOD MORNING, SOL " + Sol, "Rested: health and life support are full. A Mars day is called a SOL.", "clock", HC.Cyan);
            AstroPass.Award("sleep", "Slept on Mars", 1);
            player.Frozen = false; busy = false; slept = true;
        }

        // ---- the habitat kitchen: raw potatoes → oven (5 s) → baked potatoes → eat
        bool cooking, ateMeal;
        void UseKitchen()
        {
            if (cooking) { hud.Say("The oven is still baking. Wait a moment!"); return; }
            if (suit.Has("MEAL"))
            {
                suit.Remove("MEAL"); ateMeal = true;
                suit.Drink(40); suit.Health = Mathf.Min(100, suit.Health + 25); Sfx.Play("success", 0.7f);
                hud.Toast("YUM! BAKED MARS POTATOES", "Food gives you energy. Health and life support up!", "food", HC.Green);
                AstroPass.Award("farm", "Grew, cooked and ate your own food", 3);
                StartCoroutine(FoodFacts());
                return;
            }
            if (suit.Has("POTATO")) { suit.Remove("POTATO"); StartCoroutine(Cook()); return; }
            suit.Drink(20); Sfx.Play("pickup", 0.7f);
            hud.Toast("A SNACK", "Space food from Earth. Grow potatoes in the PLANTATION for a real meal!", "food", HC.Green);
        }
        IEnumerator Cook()
        {
            cooking = true; Sfx.Play("build", 0.5f); Sfx.Play("hum", 0.4f);
            hud.Toast("COOKING…", "The oven bakes your potatoes. 5 seconds!", "food", HC.Amber);
            yield return new WaitForSeconds(5f);
            cooking = false;
            suit.Add(new Item("MEAL", "Baked potatoes", "food", new Color(1f, 0.75f, 0.35f)));
            Sfx.Play("success", 0.6f);
            hud.Say("Ding! Your baked potatoes are ready. Press E at the KITCHEN again to EAT them!", 4);
        }
        IEnumerator FoodFacts()
        {
            yield return hud.SayWait("Real fact: in 2015 astronauts on the Space Station ate lettuce they grew in NASA's Veggie garden, under pink LED lights!", 5);
            yield return hud.SayWait("And in 2017 scientists in Peru grew potatoes in a box that copied Mars. Potatoes could really feed astronauts!", 5);
        }

        void WireRooms()
        {
            if (MarsWorld.HabitatDoor) MarsWorld.HabitatDoor.OnUse = () => { if (!busy) StartCoroutine(EnterRoom(MarsWorld.HabitatRoom, MarsWorld.HabitatOutside, "This is the HABITAT, our home. Sleep in the bunk beds, eat in the kitchen and check Earth and Mars on the navigation computer!")); };
            if (MarsWorld.WeatherDoor) MarsWorld.WeatherDoor.OnUse = () => { if (!busy) StartCoroutine(EnterRoom(MarsWorld.WeatherRoom, MarsWorld.WeatherOutside, "The WEATHER LAB! Sit at the weather computer: it shows REAL Mars weather from NASA. Can you make a forecast?")); };
            foreach (var r in new[] { MarsWorld.HabitatRoom, MarsWorld.WeatherRoom }) if (r != null) r.ExitDoor.OnUse = () => { if (!busy) StartCoroutine(ExitRoom()); };
            if (MarsWorld.Bunk) { MarsWorld.Bunk.Range = 2.0f; MarsWorld.Bunk.Verb = "Lie down in"; MarsWorld.Bunk.OnUse = () => { if (!busy) StartCoroutine(Sleep(MarsWorld.Bunk)); }; }
            if (MarsWorld.Kitchen) { MarsWorld.Kitchen.Verb = "Cook / eat at"; MarsWorld.Kitchen.Label = "the KITCHEN"; MarsWorld.Kitchen.OnUse = UseKitchen; }
            if (MarsWorld.Locker) MarsWorld.Locker.OnUse = () => { if (!busy) StartCoroutine(SuitUp()); };
            if (MarsWorld.NavComputer) MarsWorld.NavComputer.OnUse = () => { Computers.I.OpenOrbit(); AstroPass.Award("navigator", "Used the navigation computer", 2); };
            if (MarsWorld.WeatherComputer) MarsWorld.WeatherComputer.OnUse = () => Computers.I.OpenWeather();
            if (MarsWorld.Racks) MarsWorld.Racks.OnUse = () => hud.Toast("INSTRUMENT RACKS", "Real fact: NASA's InSight lander felt 1,319 marsquakes. Mars still shakes inside!", "science", HC.Amber);
        }

        void WireDoors()
        {
            MarsWorld.BunkerDoor.OnUse = () => { if (!busy) StartCoroutine(EnterBunker()); };
            MarsWorld.ExitDoor.OnUse = () => { if (!busy) StartCoroutine(ExitBunker()); };
            if (MarsWorld.GreenhouseDoor) MarsWorld.GreenhouseDoor.OnUse = () => { if (!busy) StartCoroutine(EnterGreenhouse()); };
            if (Greenhouse.I) Greenhouse.I.ExitDoor.OnUse = () => { if (!busy) StartCoroutine(ExitGreenhouse()); };
            WireRooms();
        }

        // ================================================================== salvage
        IEnumerator Salvage()
        {
            WireDoors();
            guide.Mission(3, "machines", "MAKE AIR AND WATER", "Get in the ROVER", "Pick up MOXIE (air maker)", "Pick up the WATER MAKER", "Drive back to the SHELTER", "Put both machines in their spots");
            if (MarsWorld.TheRover) guide.Focus(0, "The machines are HEAVY. Take the ROVER! Go outside, walk to it and press E.", () => MarsWorld.TheRover.transform.position, "ROVER");
            // the rover works as soon as the mission shows (not only after NOVA finishes talking)
            var rover = MarsWorld.TheRover; Waypoint wpRover = null; bool drove = false;
            if (rover)
            {
                rover.Use.enabled = true;
                rover.OnFirstDrive = () =>
                {
                    drove = true;
                    hud.Say("Vroom! W and S to drive, A and D to steer, E to get out. Real astronauts drove a rover on the Moon in 1971!", 6);
                    AstroPass.Award("rover", "First rover drive", 1);
                };
            }
            if (!drove) yield return hud.SayWait("Our rocket brought 2 machines: MOXIE makes AIR to breathe. The other turns ICE into WATER.");
            if (!drove) yield return hud.SayWait("They're at the crash site. They are HEAVY, so take the ROVER parked outside the shelter!", 4);
            if (!drove) hud.Say("Walk to the door and press E to go outside. Then press E next to the rover to drive.", 6);

            foreach (var p in MarsWorld.Pouches) { var pp = p; pp.OnUse = () => { suit.Drink(35); Sfx.Play("pickup", 0.6f); hud.Toast("LIFE SUPPORT +35", "Emergency air & water pack from the ship.", "water", HC.Cyan); pp.gameObject.SetActive(false); }; }
            MarsWorld.Tap.OnUse = UseWaterMachine;
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
                bool haveM = moxieHome || suit.Has("MOXIE"), haveW = waterHome || suit.Has("ICE");
                if (!drove && !haveM && !haveW && rover)
                    guide.Focus(0, "The machines are HEAVY. Take the ROVER! Walk to it and press E. Then W and S to drive, A and D to steer.", () => rover.transform.position, "ROVER");
                else if (!haveM)
                    guide.Focus(1, "MOXIE makes AIR from Mars air. Go to the crash site and press E on the MOXIE box.", () => MarsWorld.MoxieCrate.transform.position, "MOXIE");
                else if (!haveW)
                    guide.Focus(2, "The WATER MAKER turns Mars ice into water. Press E on its box to pick it up too.", () => MarsWorld.WaterCrate.transform.position, "WATER MAKER");
                else if (!inside)
                    guide.Focus(3, "Great! Bring both machines back to the SHELTER and go inside (press E at the door).", () => MarsWorld.BunkerOutside, "SHELTER");
                else if (!moxieHome)
                    guide.Focus(4, "Press E on the blue MOXIE spot to put the air maker in.", () => MarsWorld.MoxieSlot.transform.position, "MOXIE SPOT", () => MarsWorld.BunkerOutside);
                else
                    guide.Focus(4, "Press E on the WATER spot to put the water maker in.", () => MarsWorld.WaterSlot.transform.position, "WATER SPOT", () => MarsWorld.BunkerOutside);
                tipT += Time.deltaTime;
                if (tipT > 50 && !inside && suit.O2 < 35) { tipT = 0; hud.Say("Your oxygen is low! Go back into the bunker — or bring MOXIE so it can make more."); }
                yield return null;
            }
            guide.Complete();
            MarsWorld.AlarmLight.GetComponent<Blink>().SetBase(0); MarsWorld.AlarmLight.intensity = 0;
            yield return new WaitForSeconds(2.5f);
            hud.RemoveWaypoint(wpRover);

        }

        // ================================================================== bonus mission: collect ice with the rover
        bool iceOn; int iceGot; readonly Dictionary<GameObject, Waypoint> iceWps = new Dictionary<GameObject, Waypoint>();
        void StartIce()
        {
            if (MarsWorld.IceBlocks.Count == 0) return;
            iceOn = true;
            foreach (var g in MarsWorld.IceBlocks) g.SetActive(true);
        }
        void IceUpdate()
        {
            var pp = player.transform.position;
            foreach (var g in MarsWorld.IceBlocks)
            {
                if (!g.activeSelf || Vector3.Distance(pp, g.transform.position) > 5.5f) continue;
                g.SetActive(false);
                if (iceWps.TryGetValue(g, out var w)) { hud.RemoveWaypoint(w); iceWps.Remove(g); }
                iceGot++;
                suit.Drink(15);
                FX.BurstSparks(g.transform.position + Vector3.up, new Color(0.6f, 0.9f, 1f), 70);
                Sfx.Play("pickup", 0.8f, 1.3f);
                hud.Toast($"ICE {iceGot} / 3", "Melted ice = water to drink and air to breathe. Life support +15", "water", HC.Cyan);
                if (iceGot >= MarsWorld.IceBlocks.Count)
                {
                    iceOn = false;
                    AstroPass.Award("ice", "Ice hunter", 3);
                    hud.Say("All the ice! NASA's orbiters found ice just under the dust in many places on Mars. Future astronauts will melt it for water!", 6);
                }
                break;
            }
        }

        void TryPickMachine(string id, string name, string icon, Color c, Interactable crate, Waypoint wp)
        {
            if (suit.Items.Count >= Survival.Slots) { hud.Say("Your bag is full! Bring your things to the shelter first."); return; }
            suit.Add(new Item(id, name, icon, c, true));
            crate.gameObject.SetActive(false);
            hud.RemoveWaypoint(wp);
            hud.Toast("PICKED UP: " + name.ToUpper(), "Heavy! You walk slower. Bring it to the bunker.", icon, c);
            hud.Say(id == "MOXIE" ? "Got MOXIE! Now carry it to the bunker — follow the green marker." : "Got the Ice machine! Carry it to the bunker.", 4);
            if (id == "MOXIE") StartCoroutine(screens.ShowInfo("MOXIE OXYGEN MAKER", "air", HC.Cyan,
                @"<b>1.</b>  A pump sucks in Mars air. It is 95% carbon dioxide (CO2), says the NASA Mars fact sheet.
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
            AstroPass.Award("moxie", "Air maker installed", 1);
            yield return hud.SayWait("MOXIE works! It makes oxygen from Mars air. NASA really did this on Mars in 2021!");
        }
        IEnumerator InstallWater()
        {
            MarsWorld.WaterGhost.SetActive(false); MarsWorld.WaterInstalled.SetActive(true);
            suit.WaterOn = true; Sfx.Play("build", 0.7f); Sfx.Play("powerup", 0.6f);
            hud.Toast("WATER MACHINE ONLINE", "Melts underground ice. Press E at the tap to drink.", "water", HC.Cyan);
            AstroPass.Award("water", "Water maker installed", 1);
            yield return hud.SayWait("Water is flowing! NASA found ice under the ground on Mars. Press E at the tap to drink.");
        }

        // ================================================================== parts hunt
        IEnumerator PartsIntro()
        {
            yield return hud.SayWait("Now, the big question: how do we get HOME?");
            MarsWorld.TimerHolo.SetActive(true);
            ChooseWindow(Date.AddDays(45)); ClockRunning = true;
            hud.Toast("EARTH LAUNCH WINDOW", $"Real date from NASA JPL: {WindowDate:d MMMM yyyy}. That is {DaysLeft} days from today!", "clock", HC.Cyan);
            yield return hud.SayWait($"Earth and Mars come close only once every 2 years. NASA JPL says the next time is {WindowDate:d MMMM yyyy}. That is our trip HOME!", 5);
            yield return hud.SayWait($"We have {DaysLeft} days. Days go fast here, so watch the clock! If the rocket is not ready in time, we wait 2 more years.", 5);
            yield return hud.SayWait("Our old rocket is broken. Let's BUILD a new one! Find 3 rocket parts. Follow the coloured lights!");
            { var rowsP = new List<string>(); foreach (var rp in MarsWorld.Parts) rowsP.Add("Find the " + rp.Name.ToUpper()); rowsP.Add("Bring them to the LAUNCH PAD");
              guide.Mission(8, "rocket", "BUILD A NEW ROCKET", rowsP.ToArray()); }
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
            if (guide.Key == "rocket")
            {
                RocketPart nearest = null; int ni = -1; float nd = 1e9f;
                for (int i = 0; i < MarsWorld.Parts.Length; i++) { var p = MarsWorld.Parts[i]; if (p.Found) { guide.Done(i); continue; } float d = Vector3.Distance(pp, p.Go.transform.position); if (d < nd) { nd = d; nearest = p; ni = i; } }
                if (carried > 0 || nearest == null)
                    guide.Focus(MarsWorld.Parts.Length, "Take the rocket part to the LAUNCH PAD and press E there to build it onto the rocket.", () => MarsWorld.Rocket.position, "LAUNCH PAD", null, false);
                else
                    guide.Focus(ni, $"Find the {nearest.Name.ToUpper()}: follow its coloured light beam. Walk up to it and press E to solve its puzzle.", () => nearest.Go.transform.position, nearest.Name.ToUpper(), null, false);
            }
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
            AstroPass.Award("part_" + p.Id, ok ? p.Name + " (first try!)" : p.Name, ok ? 2 : 1);
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

        }

        // ---------------- challenges ----------------
        public IEnumerator DustStorm()
        {
            Hazard = true;
            hud.Warn("DUST STORM APPROACHING", 5, false);
            guide.Urgent("DUST STORM!", "Keep going: follow the arrow", "A dust storm! It is hard to see, so follow the big arrow. In 2018 a storm like this covered ALL of Mars!");
            yield return hud.SayWait("Dust storm! It's hard to see. Follow the big arrow. In 2018 a storm like this covered ALL of Mars!", 3);
            guide.EndUrgent();
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
            AstroPass.Award("dust", "Survived the dust storm", 1);
            Hazard = false;
        }

        int meteorHits;
        public IEnumerator MeteorShower()
        {
            Hazard = true;
            hud.Warn("METEOR SHOWER!", 5);
            guide.Urgent("METEOR SHOWER!", "Run away from the RED circles", "Space rocks are falling! Run away from the RED circles on the ground.");
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
            guide.EndUrgent();
            hud.Say("The shower is over. NASA's orbiters really do spot brand-new craters on Mars every year!", 4);
            AstroPass.Award("meteor", meteorHits == 0 ? "Perfect meteor dodge" : "Meteor dodger", meteorHits == 0 ? 3 : meteorHits == 1 ? 2 : 1);
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
                if (dist < 5.5f) { meteorHits++; suit.Health = Mathf.Max(5, suit.Health - 14); hud.Damage(0.7f); hud.Warn("HIT BY METEOR DEBRIS!", 2); }
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
            guide.Urgent("SOLAR FLARE!", "Hide in the SHELTER, or keep going", "Space rays! Hide in the SHELTER to be safe, or keep going and take some radiation. You choose!", () => MarsWorld.BunkerOutside, "SHELTER");
            float dose0 = suit.DoseMSv; int sol0 = Sol; float inSec = 0, outSec = 0;
            float strength = Mathf.Clamp(0.45f * f.Strength, 0.35f, 0.75f);
            FlareActive = true;
            yield return StormRamp(strength, 3f);
            float t = 0;
            while (t < 26) { t += Time.deltaTime; if (inside) inSec += Time.deltaTime; else outSec += Time.deltaTime; yield return null; }
            yield return StormRamp(0, 3f);
            FlareActive = false;
            hud.RemoveWaypoint(wpS); guide.EndUrgent();
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
            AstroPass.Award("flare2", sheltered ? "Smart flare shelter" : "Flare survived", sheltered ? 2 : 1);
            Hazard = false;
        }

        // ================================================================== collapse (never game over)
        IEnumerator Collapse(string why)
        {
            if (MarsWorld.TheRover && MarsWorld.TheRover.Driving) MarsWorld.TheRover.Exit();
            busy = true; player.Frozen = true;
            Sfx.Play("heartbeat", 1f);
            yield return Lids(1, 0, 1.2f);
            Cinema.I.SetBlack(1); Cinema.I.Eyes(1);
            if (!inside)
            {
                MarsWorld.SetSurface(false); MarsWorld.Interior.SetActive(true);
                Game.Sky.Mode = SkyMode.Interior; Game.Sky.Apply();
                player.Teleport(MarsWorld.InteriorSpawn + new Vector3(0, 0, 1), MarsWorld.InteriorYaw);
                bot.Teleport(MarsWorld.InteriorSpawn + new Vector3(1.2f, 1.8f, 2)); bot.gameObject.SetActive(false);
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
            partsHunt = false; hud.ForcedPrompt = null; iceOn = false;
            if (MarsWorld.TheRover && MarsWorld.TheRover.Driving) MarsWorld.TheRover.Exit();
            hud.ClearWaypoints();
            guide.Mission(9, "home", "FLY HOME", "Wait for the LAUNCH WINDOW", "Press E at the LAUNCH CONSOLE");
            guide.Focus(0, "Earth and Mars must line up before we can fly. Watch the clock!");
            yield return hud.SayWait("The rocket is complete! Now we wait for Earth to come around. Let's speed up time…");
            // the calendar quickly flips to the launch-window date (the sky does not flash)
            int start = DaysLeft, solStart = Sol; float t = 0, d = Mathf.Clamp(start * 0.04f, 1.5f, 3.5f);
            ClockRunning = false;
            Sfx.Play("whoosh", 0.5f, 0.5f);
            Cinema.I.Caption($"Waiting for the launch window: {WindowDate:d MMMM yyyy}", d + 0.5f);
            while (t < d) { t += Time.deltaTime; DaysLeft = Mathf.RoundToInt(Mathf.Lerp(start, 0, t / d)); Sol = solStart + (start - DaysLeft); yield return null; }
            DaysLeft = 0;
            Sfx.Play("success", 0.8f);
            hud.Warn("LAUNCH WINDOW OPEN", 4, false);
            var wpConsole = hud.AddWaypoint("LAUNCH CONSOLE", () => MarsWorld.LaunchConsole.transform.position, HC.Green);
            guide.Focus(1, "The window is OPEN! Walk to the screen next to the rocket and press E. Let's fly home!", () => MarsWorld.LaunchConsole.transform.position, "LAUNCH CONSOLE");
            yield return hud.SayWait("The window is OPEN! Earth is lined up. Go to the launch console!");
            bool go = false;
            MarsWorld.LaunchConsole.enabled = true; MarsWorld.LaunchConsole.OnUse = () => go = true;
            while (!go) yield return null;
            hud.RemoveWaypoint(wpConsole); guide.Complete(); guide.Hide();
            yield return LaunchMovie();
            // flight home
            player.Cam.enabled = false; cineCam.enabled = true; Game.Cam = cineCam;
            SpaceSet.I.Enter(cineCam);
            Cinema.I.Letterbox(true, 0.01f);
            yield return SpaceSet.I.Ending();
            Cinema.I.Letterbox(false, 0.6f);
            int days = Sol;
            SaveGame.Clear(); // the mission is complete: next time starts fresh
            AstroPass.AwardSilent("launch", 3);
            string rank = AstroPass.I ? $"ASTRONAUT PASS: <color={HC.Hex(HC.Amber)}>{AstroPass.I.RankName}</color> with {AstroPass.I.Stars} stars\n" : "";
            screens.ShowEnding(rank + $"You survived <color={HC.Hex(HC.Cyan)}>{days} sols</color> on Mars and flew home.\n" +
                $"Radiation dose: <color={HC.Hex(HC.Amber)}>{suit.DoseMSv:0} mSv</color> of the 600 mSv NASA limit  ·  Launch windows missed: {WindowsMissed}\n" +
                $"Puzzles solved first try: <color={HC.Hex(HC.Green)}>{screens.FirstTry} / {screens.Total}</color>\n" +
                $"<size=20>Real solar flares you faced came from NASA DONKI. Real Mars missions plan around these same launch windows.</size>");
        }

        /// <summary>The end movie: the commander rides the tower lift, walks along the crew arm into the rocket, the hatch closes,
        /// countdown and lift-off, then a camera next to the engine looks back down at the Mars base you built.</summary>
        IEnumerator LaunchMovie()
        {
            UIState.Cinematic = true; UIState.Refresh(); player.Frozen = true;
            hud.Show(false, 0.6f);
            AudioManager.I.StopMusic(1.5f); // no music while the rocket flies: only the engines
            yield return Cinema.I.ToBlack(0.5f);
            Cinema.I.Letterbox(true, 0.4f);
            // the film happens outside: make sure the Mars surface is shown (and no room is) whatever happened before
            if (MarsWorld.Interior) MarsWorld.Interior.SetActive(false);
            if (curRoom != null && curRoom.Room) curRoom.Room.SetActive(false);
            if (Greenhouse.I && Greenhouse.I.Room) Greenhouse.I.Room.SetActive(false);
            MarsWorld.SetSurface(true); Game.Sky.Mode = SkyMode.Surface; Game.Sky.Apply();
            foreach (var lbl in FindObjectsByType<NearOnly>(FindObjectsSortMode.None)) lbl.gameObject.SetActive(false); // no floating signs in the film
            var rocket = MarsWorld.Rocket; var pad = MarsWorld.Pad;
            // a movie camera (the player's eyes are switched off for the film)
            var camGo = new GameObject("LaunchCam"); var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 50; cam.nearClipPlane = 0.1f; cam.farClipPlane = 5000;
            ConfigCam(cam);
            var myEars = player.Cam.GetComponent<AudioListener>(); if (myEars) myEars.enabled = false; camGo.AddComponent<AudioListener>();
            player.Cam.enabled = false; Game.Cam = cam;
            void Look(Vector3 from, Vector3 at) { cam.transform.position = from; cam.transform.rotation = Quaternion.LookRotation(at - from); }

            // where things are: the rocket's top part, the tower (+x of the pad), the hatch facing the tower
            var all = Util.Bounds(rocket.gameObject); float cut = all.min.y + all.size.y * 0.6f;
            Bounds up = new Bounds(all.center, Vector3.zero); bool any = false;
            foreach (var r in rocket.GetComponentsInChildren<Renderer>()) if (r.bounds.center.y > cut) { if (!any) { up = r.bounds; any = true; } else up.Encapsulate(r.bounds); }
            if (!any) up = all;
            var axis = new Vector3(up.center.x, 0, up.center.z);
            var tower = pad.TransformPoint(new Vector3(5.6f, 0, 0));
            var toTower = new Vector3(tower.x - axis.x, 0, tower.z - axis.z).normalized;
            var side = Vector3.Cross(Vector3.up, toTower);
            float radius = Mathf.Clamp(Mathf.Min(up.extents.x, up.extents.z) * 0.85f, 0.8f, 3f);
            float floorY = Mathf.Min(pad.position.y + 15f, up.max.y - 3f);
            var hatch = new Vector3(axis.x, floorY, axis.z) + toTower * radius;
            var armEnd = new Vector3(tower.x, floorY, tower.z);
            var armRot = Quaternion.LookRotation(-toTower).eulerAngles;
            float armLen = Vector3.Distance(armEnd, hatch);
            var props = new List<GameObject>();
            // ---- the CREW ACCESS ARM: a covered walkway with windows, from the tower to a small "white room" at the hatch.
            // It hangs on a hinge at the tower, so after boarding it swings away from the rocket (like real launch pads).
            var white = R.Lit(new Color(0.9f, 0.91f, 0.92f), 0.45f); var grate = R.HullDark; var glassM = R.Holo(new Color(0.6f, 0.85f, 1f), 0.3f, 0.06f);
            var armPivot = new GameObject("CrewArmPivot").transform; armPivot.position = armEnd; armPivot.rotation = Quaternion.LookRotation(hatch - armEnd); props.Add(armPivot.gameObject);
            void Part(string n, Vector3 lp, Vector3 size, Material m) => ProcMesh.Prim(PrimitiveType.Cube, n, armPivot, lp, size, m, null, false);
            float L = armLen - 0.1f, W = 1.7f;
            Part("Floor", new Vector3(0, -0.07f, L / 2), new Vector3(W, 0.14f, L), grate);
            Part("Roof", new Vector3(0, 2.35f, L / 2), new Vector3(W + 0.1f, 0.1f, L), white);
            Part("RoofLight", new Vector3(0, 2.28f, L / 2), new Vector3(0.25f, 0.03f, L - 0.4f), R.Emissive(Color.white, new Color(0.8f, 0.78f, 0.72f)));
            foreach (int sd in new[] { -1, 1 })
            {
                Part("Wall", new Vector3(sd * W / 2, 0.22f, L / 2), new Vector3(0.05f, 0.45f, L), white);
                Part("Stripe", new Vector3(sd * (W / 2 + 0.03f), 0.38f, L / 2), new Vector3(0.02f, 0.08f, L), R.Orange);
                Part("Window", new Vector3(sd * W / 2, 1.37f, L / 2), new Vector3(0.02f, 1.85f, L), glassM);
                for (float z = 0.1f; z <= L; z += 1.2f) Part("Rib", new Vector3(sd * W / 2, 1.18f, z), new Vector3(0.08f, 2.36f, 0.08f), grate);
            }
            // the white room at the rocket end, softly lit
            Part("WhiteRoomTop", new Vector3(0, 2.45f, L - 0.8f), new Vector3(W + 0.5f, 0.12f, 1.6f), white);
            var roomLamp = new GameObject("WhiteRoomLight").AddComponent<Light>(); roomLamp.transform.SetParent(armPivot, false); roomLamp.transform.localPosition = new Vector3(0, 2.0f, L - 0.9f);
            roomLamp.type = LightType.Point; roomLamp.range = 5; roomLamp.intensity = 0.7f; roomLamp.color = new Color(1f, 0.97f, 0.9f);
            // the rocket's side hatch: a white door on a side hinge, a warm light inside, a status lamp
            var hatchFrame = new GameObject("HatchFrame").transform; hatchFrame.position = hatch; hatchFrame.rotation = Quaternion.LookRotation(toTower); props.Add(hatchFrame.gameObject);
            ProcMesh.Prim(PrimitiveType.Cube, "Inside", hatchFrame, new Vector3(0, 1.0f, -0.08f), new Vector3(1.05f, 1.85f, 0.04f), R.Emissive(new Color(0.8f, 0.7f, 0.55f), new Color(0.55f, 0.44f, 0.3f)), null, false);
            foreach (var (pos, size) in new[] { (new Vector3(0, 2.0f, 0), new Vector3(1.35f, 0.14f, 0.12f)), (new Vector3(0, -0.02f, 0), new Vector3(1.35f, 0.14f, 0.12f)), (new Vector3(-0.62f, 1.0f, 0), new Vector3(0.12f, 2.1f, 0.12f)), (new Vector3(0.62f, 1.0f, 0), new Vector3(0.12f, 2.1f, 0.12f)) })
                ProcMesh.Prim(PrimitiveType.Cube, "Frame", hatchFrame, pos, size, grate, null, false);
            var hinge = new GameObject("HatchHinge").transform; hinge.SetParent(hatchFrame, false); hinge.localPosition = new Vector3(-0.55f, 0, 0.04f);
            ProcMesh.Prim(PrimitiveType.Cube, "Door", hinge, new Vector3(0.55f, 1.0f, 0), new Vector3(1.08f, 1.9f, 0.1f), white, null, false);
            ProcMesh.Prim(PrimitiveType.Cylinder, "DoorWindow", hinge, new Vector3(0.55f, 1.35f, 0.055f), new Vector3(0.32f, 0.01f, 0.32f), R.Lit(new Color(0.05f, 0.08f, 0.12f), 0.9f), new Vector3(90, 0, 0), false);
            ProcMesh.Prim(PrimitiveType.Cube, "DoorHandle", hinge, new Vector3(0.95f, 0.95f, 0.08f), new Vector3(0.05f, 0.28f, 0.05f), grate, null, false);
            var statusMat = R.Emissive(Color.black, new Color(3f, 0.6f, 0.1f));
            ProcMesh.Prim(PrimitiveType.Sphere, "Status", hatchFrame, new Vector3(0.75f, 2.05f, 0.08f), Vector3.one * 0.09f, statusMat, null, false);
            var lamp = new GameObject("HatchLight").AddComponent<Light>(); lamp.transform.position = hatch + Vector3.up * 1.6f + toTower * 0.6f; lamp.type = LightType.Point; lamp.range = 5; lamp.intensity = 0; lamp.color = new Color(1f, 0.85f, 0.6f); props.Add(lamp.gameObject);
            // the tower elevator: a small cage that rides up the tower
            var lift = new GameObject("Lift"); lift.transform.position = new Vector3(tower.x, pad.position.y + 0.55f, tower.z); lift.transform.rotation = Quaternion.LookRotation(-toTower); props.Add(lift);
            ProcMesh.Prim(PrimitiveType.Cube, "LiftFloor", lift.transform, Vector3.zero, new Vector3(1.6f, 0.12f, 1.6f), grate, null, false);
            foreach (var c in new[] { new Vector3(-0.75f, 0, -0.75f), new Vector3(0.75f, 0, -0.75f), new Vector3(-0.75f, 0, 0.75f), new Vector3(0.75f, 0, 0.75f) })
                ProcMesh.Prim(PrimitiveType.Cube, "LiftPost", lift.transform, c + Vector3.up * 1.15f, new Vector3(0.06f, 2.3f, 0.06f), R.Orange, null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "LiftTop", lift.transform, Vector3.up * 2.3f, new Vector3(1.6f, 0.08f, 1.6f), R.Orange, null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "LiftRail", lift.transform, new Vector3(-0.78f, 1.0f, 0), new Vector3(0.04f, 0.04f, 1.5f), R.Orange, null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "LiftRail", lift.transform, new Vector3(0.78f, 1.0f, 0), new Vector3(0.04f, 0.04f, 1.5f), R.Orange, null, false);
            // the astronaut (you!) - walks with real steps
            var astro = AstroRig.Spawn(lift.transform.position + Vector3.up * 0.06f, Quaternion.LookRotation(-toTower).eulerAngles.y, 1.8f, null);
            var rig = astro.GetComponent<AstroRig>();
            props.Add(astro);
            var astroOff = astro.transform.position - lift.transform.position;

            // SHOT 1: the tower elevator takes the commander up
            Cinema.I.Caption("The commander rides the tower elevator up to the rocket…", 4.5f);
            yield return Cinema.I.FromBlack(0.6f);
            Sfx.Play("hum", 0.4f, 0.7f);
            float t = 0, d = 4.5f; float y0 = lift.transform.position.y, y1 = floorY - 0.06f;
            while (t < d)
            {
                t += Time.deltaTime; float k = Tween.EaseInOut(t / d);
                var lp = lift.transform.position; lp.y = Mathf.Lerp(y0, y1, k); lift.transform.position = lp; astro.transform.position = lp + astroOff;
                Look(tower + side * 11f + toTower * 6f + Vector3.up * (lp.y + 1.2f), lp + Vector3.up * 1.2f);
                yield return null;
            }
            // SHOT 2: across the crew access arm (seen through its windows)
            Cinema.I.Caption("…and walks across the crew access arm.", 3.5f);
            // he stops in front of the hatch (OUTSIDE the rocket); the hatch swings open as he arrives
            var walkFrom = astro.transform.position; var door = hatch + toTower * 1.4f; var walkTo = new Vector3(door.x, walkFrom.y, door.z);
            var mid2 = (armEnd + hatch) / 2;
            if (rig) rig.Walk = 1;
            bool opening = false;
            for (t = 0; t < 1; t += Time.deltaTime / 3.6f)
            {
                astro.transform.position = Vector3.Lerp(walkFrom, walkTo, t);
                if (t > 0.62f)
                {
                    if (!opening) { opening = true; Cinema.I.Caption("The hatch opens…", 2f); Sfx.Play("whoosh", 0.45f, 1.1f); Sfx.Play("click", 0.6f, 0.7f); }
                    float k = Tween.EaseInOut(Mathf.InverseLerp(0.62f, 1f, t)); hinge.localRotation = Quaternion.Euler(0, -105 * k, 0); lamp.intensity = 1.1f * k;
                }
                Look(mid2 + side * 5.5f + Vector3.up * 1.7f - toTower * 0.5f, astro.transform.position + Vector3.up * 1.1f);
                yield return null;
            }
            hinge.localRotation = Quaternion.Euler(0, -105, 0); lamp.intensity = 1.1f;
            // SHOT 3: in the white room, over his shoulder: he steps through the open hatch into the rocket
            var shot3 = hatch + toTower * 3.4f + side * 0.45f + Vector3.up * 1.75f;
            Look(shot3, hatch + Vector3.up * 1.1f);
            var cabin = hatch - toTower * 1.0f; cabin.y = walkTo.y;
            if (rig) rig.Walk = 0.9f;
            for (t = 0; t < 1; t += Time.deltaTime / 2.2f)
            {
                astro.transform.position = Vector3.Lerp(walkTo, cabin, t);
                if (Vector3.Dot(astro.transform.position - hatch, toTower) < -0.45f) astro.SetActive(false); // inside: out of sight
                yield return null;
            }
            astro.SetActive(false);
            Cinema.I.Caption("…and closes. Strap in, Commander!", 2.2f);
            for (t = 0; t < 1; t += Time.deltaTime / 1.0f) { float k = Tween.EaseInOut(t); hinge.localRotation = Quaternion.Euler(0, -105 * (1 - k), 0); lamp.intensity = 1.1f * (1 - k); yield return null; }
            Sfx.Play("thud", 0.6f, 0.85f);
            statusMat.SetColor("_EmissionColor", new Color(0.2f, 3f, 0.6f)); Sfx.Play("beep", 0.5f, 1.3f); // hatch sealed: green
            yield return new WaitForSeconds(0.9f);
            // SHOT 4: the crew access arm swings away from the rocket
            Cinema.I.Caption("Hatch sealed. The crew access arm swings away.", 3f);
            Sfx.Play("hum", 0.5f, 0.5f);
            var r0rot = armPivot.rotation;
            for (t = 0; t < 1; t += Time.deltaTime / 3f)
            {
                armPivot.rotation = Quaternion.AngleAxis(-80 * Tween.EaseInOut(t), Vector3.up) * r0rot;
                Look(tower + side * 24f + toTower * 10f + Vector3.up * (floorY - pad.position.y + 4f), (armEnd + hatch) / 2);
                yield return null;
            }
            yield return new WaitForSeconds(0.6f);

            // SHOT 3: wide view: countdown and lift-off
            var wide = new Vector3(axis.x, pad.position.y, axis.z) + side * 48f - toTower * 22f + Vector3.up * 5f;
            var mid = new Vector3(axis.x, all.center.y, axis.z);
            Look(wide, mid);
            var exh = FX.Exhaust(rocket, Vector3.zero, 1.8f); exh.transform.rotation = Quaternion.Euler(90, 0, 0); exh.Stop();
            for (int i = 5; i >= 1; i--)
            {
                Cinema.I.Caption($"LAUNCH IN  {i}", 0.9f); Sfx.Play("beep", 0.6f, i == 1 ? 1.4f : 1);
                if (i == 2) { var vent = FX.Smoke(rocket.position + Vector3.up * 2f, 0.6f, null, 0.5f); props.Add(vent.gameObject); } // engines chill: white vapour
                yield return new WaitForSeconds(1f);
            }
            exh.Play(); // quiet lift-off: no engine or explosion sound
            var roar = AudioManager.I.Loop("rocket", 0f); roar.mute = true; // quiet flight home: no music, no engine drone
            FX.DustBlast(rocket.position, 2f); FX.Explosion(rocket.position + Vector3.up * 1.5f, 1.2f);
            // big billowing clouds roll out sideways from the pad
            for (int i = 0; i < 6; i++)
            {
                var dir = Quaternion.Euler(0, i * 60 + 15, 0) * Vector3.forward;
                FX.DustBlast(rocket.position + dir * (4 + i % 2 * 3) + Vector3.up * 0.5f, 1.6f);
                props.Add(FX.Smoke(rocket.position + dir * 7f, 1.4f, null, 0.35f).gameObject);
            }
            Cinema.I.Caption("LIFT-OFF!", 2);
            var r0 = rocket.position; float tt = 0;
            while (tt < 3.2f)
            {
                tt += Time.deltaTime; rocket.position = r0 + Vector3.up * tt * tt * 3f;
                Look(wide + Random.insideUnitSphere * 0.12f, rocket.position + Vector3.up * all.size.y * 0.5f);
                yield return null;
            }
            // SHOT 4: through the dust, a camera next to the engine looks back down at the base
            Cinema.I.Caption("Goodbye, Mars base! The habitat, the plantation and the shelter wait for the next crew.", 6);
            var baseAt = MarsWorld.MarsBase ? MarsWorld.MarsBase.position : r0;
            while (tt < 10f)
            {
                tt += Time.deltaTime; rocket.position = r0 + Vector3.up * tt * tt * 3f;
                var engineCam = rocket.position + toTower * (Mathf.Max(all.extents.x, all.extents.z) + 3.5f) + Vector3.up * 4f; // outside the boosters, next to the engines
                Look(engineCam + Random.insideUnitSphere * 0.05f, Vector3.Lerp(rocket.position, baseAt, 0.85f));
                yield return null;
            }
            yield return Cinema.I.ToBlack(1f);
            AudioManager.I.FadeSource(roar, 0, 1); Destroy(roar.gameObject, 1.5f);
            foreach (var g in props) if (g) Destroy(g);
            Destroy(camGo);
            Cinema.I.Letterbox(false, 0.01f);
        }

        IEnumerator SkyCycle(float d)
        {
            float t = 0, from = Game.Sky.TimeOfDay;
            while (t < d) { t += Time.deltaTime; Game.Sky.TimeOfDay = from + (t / d) * 6f; yield return null; }
            Game.Sky.TimeOfDay = 0.45f;
        }
    }
}
