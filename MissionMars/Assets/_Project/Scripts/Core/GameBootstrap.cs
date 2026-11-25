using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MissionMars
{
    /// <summary>Single entry point: the whole game is assembled at runtime.</summary>
    public class GameBootstrap : MonoBehaviour
    {
        static Task<System.Collections.Generic.Dictionary<string, float[]>> audioTask;

        IEnumerator Start()
        {
            Game.Runner = this;
            // always English numbers and dates (187.4 not 187,4; "Feb" not "февр.") whatever the browser language is
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            QualitySettings.vSyncCount = 1;
            Time.timeScale = 1; Game.Paused = false; Game.InMenu = true;
            UIState.Reset(); UIState.Cinematic = false;
            var es = new GameObject("EventSystem"); var evs = es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
            // W/S and Enter are game keys (walk, skip NOVA). With UI keyboard navigation on, they moved the
            // selection to hidden buttons like "Play again" and restarted the game. Buttons are mouse-only.
            evs.sendNavigationEvents = false;
            Cinema.Create().SetBlack(1);
            Cinema.I.Caption("MISSION MARS  ·  loading ...", 30);
            yield return null;

#if UNITY_WEBGL && !UNITY_EDITOR
            var task = Task.FromResult(Synth.BuildAll()); // no threads in the browser
#else
            var task = Task.Run(() => Synth.BuildAll());
#endif
            var am = new GameObject("Audio").AddComponent<AudioManager>();
            new GameObject("NasaApi").AddComponent<NasaApi>().Begin();
            new GameObject("NasaLive").AddComponent<NasaLive>().Begin(); // JPL Horizons distance, Mars24 clock, NASA photos (research site REST API)
            var sky = new GameObject("Sky").AddComponent<SkyController>(); sky.Init(); Game.Sky = sky;
            while (!task.IsCompleted) yield return null;
            am.Init(Synth.ToClips(task.Result));
            yield return null;

            var cg = new GameObject("CineCam"); cg.tag = "MainCamera";
            var cine = cg.AddComponent<Camera>(); cine.fieldOfView = 50; cine.nearClipPlane = 0.3f; cine.farClipPlane = 60000;
            cg.AddComponent<AudioListener>();
            Game.Cam = cine;
            PostFX.Setup(cine);
            MarsWorld.Build();
            SpaceSet.Create();
            Hud.Create();
            var screens = Screens.Create();
            Computers.Create();
            var story = gameObject.AddComponent<Story>();
            story.Init(cine, screens);
            yield return null;
            Cinema.I.ClearCaption();

            string vid = Arg("-video");
            if (vid != null) { Application.runInBackground = true; Story.QuickStart = true; StartCoroutine(VideoTour(vid)); }
            // developer preview for checking the world: .../game/?preview=colony or ?preview=shelter
            string url = Application.absoluteURL ?? "";
            if (url.Contains("preview=")) { Story.QuickStart = true; StartCoroutine(Preview(url.Contains("preview=shelter"))); }
            string bshot = Arg("-baseshot");
            if (bshot != null) { Application.runInBackground = true; Story.QuickStart = true; StartCoroutine(BaseShot(bshot)); }
            string mtest = Arg("-missiontest");
            if (mtest != null) { Application.runInBackground = true; Story.QuickStart = true; StartCoroutine(MissionTest(mtest)); }
            string dshot = Arg("-datashot");
            if (dshot != null) { Application.runInBackground = true; StartCoroutine(DataShot(dshot)); }
            string tshot = Arg("-titleshot");
            if (tshot != null) { Application.runInBackground = true; StartCoroutine(TitleShot(tshot)); }
            string shots2 = Arg("-shots2");
            if (shots2 != null) { Application.runInBackground = true; Story.QuickStart = true; StartCoroutine(Tour2(shots2)); }
            string shots = Arg("-shots");
            if (shots != null) { Application.runInBackground = true; Story.QuickStart = true; StartCoroutine(Tour(shots)); }
            yield return story.Run();
        }

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        // ---------------- automated visual QA (only with -shots <dir>) ----------------
        IEnumerator Shot(string dir, string name, float wait = 1.2f)
        {
            yield return new WaitForSeconds(wait);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return null; yield return null;
        }

        /// <summary>Plays all 8 missions by itself (presses E for you) and takes a picture of the kid guide at every step.
        /// Run: MissionMars.exe -missiontest &lt;folder&gt;. Writes log.txt with OK / TIMEOUT for every mission.</summary>
        IEnumerator MissionTest(string dir)
        {
            Directory.CreateDirectory(dir);
            var log = new System.Text.StringBuilder();
            void L(string s) { log.AppendLine($"{Time.unscaledTime:0.0}s  {s}"); File.WriteAllText(Path.Combine(dir, "log.txt"), log.ToString()); }
            IEnumerator WaitKey(string key, float max = 240)
            {
                float t = 0; while ((!Guide.I || Guide.I.Key != key) && t < max) { t += Time.unscaledDeltaTime; yield return null; }
                L(Guide.I && Guide.I.Key == key ? "OK   mission " + Guide.I.Num + " " + key : "FAIL timeout waiting for mission " + key);
                t = 0; while (Guide.I && string.IsNullOrEmpty(Guide.I.Hint) && t < 60) { t += Time.unscaledDeltaTime; yield return null; }
            }
            IEnumerator Idle(float s) { float t = 0; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }
            IEnumerator Free(float max = 40) { float t = 0; while ((!Game.Player || Game.Player.Frozen || UIState.Modal || UIState.Cinematic) && t < max) { t += Time.unscaledDeltaTime; yield return null; } }
            void Use(Interactable i, string what) { if (i && i.OnUse != null) { i.OnUse(); L("press E: " + what); } else L("FAIL nothing to press: " + what); }
            IEnumerator Pic(string n) { Time.timeScale = 1; yield return Shot(dir, n, 1.0f); L("picture " + n + "   hint: " + (Guide.I ? Guide.I.Hint : "")); }

            IEnumerator KeepAlive() { while (true) { if (Game.Suit) { Game.Suit.O2 = Mathf.Max(Game.Suit.O2, 80); Game.Suit.Health = Mathf.Max(Game.Suit.Health, 80); } yield return null; } }
            StartCoroutine(KeepAlive()); // the test runs 4x faster, so the suit would run out of air
            Time.timeScale = 4;
            yield return Free(300); L("player can move");
            yield return Pic("00_look");
            Time.timeScale = 4;
            yield return WaitKey("storm"); yield return Pic("01_storm");
            var front = MarsWorld.BunkerDoorPanel.position + MarsWorld.BunkerDoorPanel.forward * 4f; front.y = MarsTerrain.H(front.x, front.z) + 0.1f;
            Game.Player.Teleport(MarsWorld.BunkerOutside, 0); Game.Player.LookAt(MarsWorld.BunkerDoorPanel.position + Vector3.up); yield return Pic("01b_door");
            Use(MarsWorld.BunkerDoor, "shelter door"); yield return Idle(1.5f); yield return Pic("01c_keypad");
            { var kp = FindFirstObjectByType<Keypad>(); if (kp) { foreach (var c in "1122") { if (!kp) break; kp.SendMessage("Press", c.ToString()); yield return Idle(0.2f); } L("typed door code 1122"); } else L("FAIL no keypad"); }
            Time.timeScale = 4; yield return WaitKey("power"); yield return Free(); yield return Pic("02_power");
            Use(MarsWorld.BatteryPlug, "battery"); yield return Idle(1); yield return Pic("02b_switch");
            Use(MarsWorld.PowerSwitch, "main switch");
            Time.timeScale = 4; yield return WaitKey("machines"); { float t = 0; while (MarsWorld.ExitDoor.OnUse == null && t < 60) { t += Time.unscaledDeltaTime; yield return null; } } yield return Free();
            Use(MarsWorld.ExitDoor, "exit shelter"); yield return Idle(1); yield return Free(); yield return Pic("03_rover");
            Game.Suit.Add(new Item("MOXIE", "MOXIE Oxygen Maker", "air", HC.Cyan, true)); MarsWorld.MoxieCrate.gameObject.SetActive(false);
            yield return Idle(0.5f); yield return Pic("03b_water");
            Game.Suit.Add(new Item("ICE", "Ice-to-Water Machine", "water", HC.Cyan, true)); MarsWorld.WaterCrate.gameObject.SetActive(false);
            yield return Idle(0.5f); yield return Pic("03c_back");
            Use(MarsWorld.BunkerDoor, "shelter door"); yield return Idle(1); yield return Free();
            { float t = 0; while (MarsWorld.MoxieSlot.OnUse == null && t < 60) { t += Time.unscaledDeltaTime; yield return null; } }
            yield return Free(); yield return Pic("03d_slots");
            Use(MarsWorld.MoxieSlot, "MOXIE spot"); yield return Idle(1); Use(MarsWorld.WaterSlot, "water spot");
            Time.timeScale = 4; yield return WaitKey("ice"); yield return Free(); yield return Pic("04_ice_exit");
            Use(MarsWorld.ExitDoor, "exit shelter"); yield return Idle(1); yield return Free(); yield return Pic("04b_ice_arrow");
            { GameObject site = null; float bd = 1e9f; foreach (var g in MarsWorld.IceBlocks) { float d = Vector3.Distance(g.transform.position, MarsWorld.BunkerOutside); if (d < bd) { bd = d; site = g; } }
              var sp = site.transform.position + Vector3.forward * 4f; sp.y = MarsTerrain.H(sp.x, sp.z) + 0.2f; Game.Player.Teleport(sp, 0); Game.Player.LookAt(site.transform.position + Vector3.up);
              yield return Idle(0.5f); yield return Pic("04c_dig"); var dig = site.GetComponent<Interactable>(); Use(dig, "dig ice"); yield return Idle(0.5f); Use(dig, "dig ice"); yield return Idle(0.5f); yield return Pic("04d_back"); }
            Use(MarsWorld.BunkerDoor, "shelter door"); yield return Idle(1); yield return Free(); yield return Pic("04e_machine");
            Use(MarsWorld.Tap, "water machine: put ice in"); yield return Idle(1); yield return Pic("04f_melting"); yield return Idle(6); yield return Pic("04g_drink");
            Use(MarsWorld.Tap, "water machine: drink");
            Time.timeScale = 4; yield return WaitKey("base"); yield return Free(); yield return Pic("05_exit_arrow");
            Use(MarsWorld.ExitDoor, "exit shelter"); yield return Idle(1); yield return Free(); yield return Pic("04b_base_arrow");
            { var b = MarsWorld.MarsBase; var p = b.position + (MarsWorld.BunkerOutside - b.position).normalized * 25f; p.y = MarsTerrain.H(p.x, p.z) + 0.2f; Game.Player.Teleport(p, 0); }
            Time.timeScale = 4; yield return WaitKey("food", 150); yield return Free(); yield return Pic("05_food_arrow");
            Use(MarsWorld.GreenhouseDoor, "plantation door"); yield return Idle(1); yield return Free(); yield return Pic("05b_inside");
            var gh = Greenhouse.I;
            gh.SendMessage("Use"); L("press E: grow lights"); yield return Idle(1.5f); yield return Pic("05c_pump");
            gh.SendMessage("Use"); L("press E: pump"); Time.timeScale = 4;
            { float t = 0; while (gh.State != 3 && t < 60) { t += Time.unscaledDeltaTime; yield return null; } }
            yield return Pic("05d_harvest"); gh.SendMessage("Use"); L("press E: harvest -> bag has POTATO: " + Game.Suit.Has("POTATO")); yield return Idle(1); yield return Pic("05e_cook_arrow");
            Use(gh.ExitDoor, "exit plantation"); yield return Idle(1); yield return Free();
            Use(MarsWorld.HabitatDoor, "habitat door"); yield return Idle(1); yield return Free(); yield return Pic("05f_kitchen");
            Use(MarsWorld.Kitchen, "kitchen: cook"); yield return Idle(1); yield return Pic("05g_cooking"); yield return Idle(6); yield return Pic("05h_eat");
            Use(MarsWorld.Kitchen, "kitchen: eat");
            Time.timeScale = 4; yield return WaitKey("night", 120); yield return Free();
            { var lk = MarsWorld.Locker.transform.position; Game.Player.LookAt(lk + Vector3.up * 0.5f); yield return Idle(0.3f); yield return Pic("x_suits"); }
            if (MarsWorld.HabitatRoom != null) Use(MarsWorld.HabitatRoom.ExitDoor, "exit habitat (no suit: must refuse)"); yield return Idle(1); yield return Pic("x_nosuit");
            Use(MarsWorld.Locker, "put on spacesuit"); yield return Idle(3);
            if (MarsWorld.HabitatRoom != null) Use(MarsWorld.HabitatRoom.ExitDoor, "exit habitat"); yield return Idle(1); yield return Free(); yield return Pic("06_night");
            { var f = typeof(Computers).GetField("PowerPlanOk", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static); (f?.GetValue(null) as System.Action)?.Invoke(); L("night power planned"); }
            yield return Idle(1); yield return Pic("06b_bunk_arrow");
            Use(MarsWorld.HabitatDoor, "habitat door"); yield return Idle(1); yield return Free(); yield return Pic("06c_habitat");
            Use(MarsWorld.Bunk, "bunk bed"); yield return Idle(3); yield return Pic("06d_lying"); yield return Idle(1.5f); yield return Pic("06e_eyes");
            Time.timeScale = 4; yield return WaitKey("rocket", 120); yield return Free();
            { var lk = MarsWorld.Locker.transform.position; Game.Player.LookAt(lk + Vector3.up * 0.5f); yield return Idle(0.3f); yield return Pic("x_suits"); }
            if (MarsWorld.HabitatRoom != null) Use(MarsWorld.HabitatRoom.ExitDoor, "exit habitat (no suit: must refuse)"); yield return Idle(1); yield return Pic("x_nosuit");
            Use(MarsWorld.Locker, "put on spacesuit"); yield return Idle(3);
            if (MarsWorld.HabitatRoom != null) Use(MarsWorld.HabitatRoom.ExitDoor, "exit habitat");
            yield return Idle(1); yield return Free(); yield return Pic("07_rocket");
            foreach (var p in MarsWorld.Parts) { p.Found = true; p.Go.SetActive(false); Game.Suit.Add(new Item(p.Id, p.Name, p.Icon, p.Color)); }
            yield return Idle(1); yield return Pic("07b_pad");
            Use(MarsWorld.RocketUse, "launch pad");
            Time.timeScale = 4; yield return WaitKey("home", 90); yield return Pic("08_home");
            { float w = 0; while (!(MarsWorld.LaunchConsole.enabled && MarsWorld.LaunchConsole.OnUse != null) && w < 60) { w += Time.unscaledDeltaTime; yield return null; } }
            yield return Free(); yield return Pic("08b_console");
            Use(MarsWorld.LaunchConsole, "launch console");
            Time.timeScale = 1;
            foreach (var (sec, n) in new[] { (3f, "10a_lift"), (3.5f, "10b_hatch"), (3.5f, "10c_walk"), (4f, "10d_countdown"), (4.5f, "10e_liftoff"), (2.5f, "10f_enginecam"), (3f, "10g_base") })
            { yield return Idle(sec); ScreenCapture.CaptureScreenshot(Path.Combine(dir, n + ".png")); L("picture " + n); }
            Time.timeScale = 4; yield return Idle(20); yield return Pic("09_end");
            L("DONE"); Application.Quit();
        }

        IEnumerator BaseShot(string dir)
        {
            Directory.CreateDirectory(dir);
            while (!Game.Player || Game.Player.Frozen) yield return null;
            var b = MarsWorld.MarsBase; if (!b) { Application.Quit(); yield break; }
            Vector3[] from = { b.TransformPoint(new Vector3(-30, 0, 24)), b.TransformPoint(new Vector3(36, 0, 22)), b.TransformPoint(new Vector3(10, 0, 16)) };
            for (int i = 0; i < from.Length; i++)
            {
                var p = from[i]; p.y = MarsTerrain.H(p.x, p.z) + 0.2f; if (i == 2) p.y = b.position.y + 0.05f * MarsWorld.BaseSize + 0.3f;
                for (int f = 0; f < 60; f++) { Game.Player.Teleport(p, 0); Game.Player.LookAt(b.position + Vector3.up * (i == 2 ? 6 : 8)); yield return null; }
                yield return Shot(dir, "base" + i, 0.5f);
            }
            { // walk straight at the big dome: the walls must stop us
                var cc = Game.Player.GetComponent<CharacterController>();
                var start = b.TransformPoint(new Vector3(0, 0, 30)); start.y = MarsTerrain.H(start.x, start.z) + 0.3f;
                Game.Player.Teleport(start, 0); yield return null;
                var walk = (b.position - start); walk.y = 0; walk.Normalize();
                for (int f = 0; f < 300; f++) { cc.Move(walk * 0.15f + Vector3.down * 0.1f); yield return null; }
                float left = Vector3.Distance(new Vector3(Game.Player.transform.position.x, 0, Game.Player.transform.position.z), new Vector3(b.position.x, 0, b.position.z));
                Debug.Log($"WALLTEST walked toward the dome centre, stopped {left:0.0} m from it (dome radius ~{0.18f * MarsWorld.BaseSize:0.0} m)");
                Game.Player.LookAt(b.position + Vector3.up * 6);
                yield return Shot(dir, "base_wall", 0.5f);
            }
            { // inside the PLANTATION
                var gh = Greenhouse.I;
                MarsWorld.SetSurface(false); gh.Room.SetActive(true); Game.Sky.Mode = SkyMode.Interior; Game.Sky.InteriorBright = 1.3f; Game.Sky.Apply();
                for (int f = 0; f < 20; f++) { Game.Player.Teleport(gh.Spawn, 20); yield return null; }
                yield return Shot(dir, "greenhouse_empty", 0.5f);
                gh.ShowGrown();
                for (int f = 0; f < 20; f++) { Game.Player.Teleport(gh.Spawn, 20, 12); yield return null; }
                yield return Shot(dir, "greenhouse_grown", 0.5f);
                gh.Room.SetActive(false); MarsWorld.SetSurface(true); Game.Sky.Mode = SkyMode.Surface; Game.Sky.Apply();
            }
            foreach (var (room, n) in new[] { (MarsWorld.HabitatRoom, "habitat_in"), (MarsWorld.WeatherRoom, "weather_in") })
            { // inside the habitat and the weather lab
                if (room == null) continue;
                MarsWorld.SetSurface(false); room.Room.SetActive(true); Game.Sky.Mode = SkyMode.Interior; Game.Sky.InteriorBright = 1.4f; Game.Sky.Apply();
                for (int f = 0; f < 20; f++) { Game.Player.Teleport(room.Spawn, room.SpawnYaw, 8); yield return null; }
                yield return Shot(dir, n, 0.5f);
                if (n == "habitat_in") { Computers.I.OpenOrbit(); yield return Shot(dir, "screen_orbit", 1.5f); Computers.I.Close(); }
                else { Computers.I.OpenWeather(); yield return Shot(dir, "screen_weather", 1f); Computers.I.Close(); }
                room.Room.SetActive(false); MarsWorld.SetSurface(true); Game.Sky.Mode = SkyMode.Surface; Game.Sky.Apply();
            }
            Computers.I.OpenPower(); yield return Shot(dir, "screen_power", 1f); Computers.I.Close();
            { // the habitat and weather lab from outside
                var hp = MarsWorld.HabitatOutside; var bb = MarsWorld.MarsBase;
                if (bb) { var p0 = bb.TransformPoint(new Vector3(-6, 0, 26)); p0.y = MarsTerrain.H(p0.x, p0.z) + 0.2f; for (int f = 0; f < 30; f++) { Game.Player.Teleport(p0, 0); Game.Player.LookAt(bb.TransformPoint(new Vector3(-8, 6, 0))); yield return null; } yield return Shot(dir, "outside_habitat_lab", 0.4f); }
            }
            { // the shelter entrance
                var bo = MarsWorld.BunkerOutside; var fwd = MarsWorld.Bunker.forward; fwd.y = 0; fwd.Normalize();
                var p0 = bo + fwd * 5f; p0.y = MarsTerrain.H(p0.x, p0.z) + 0.2f;
                for (int f = 0; f < 30; f++) { Game.Player.Teleport(p0, 0); Game.Player.LookAt(MarsWorld.BunkerDoorPanel.position + Vector3.up * 0.8f); yield return null; }
                yield return Shot(dir, "bunker_entrance", 0.4f);
            }
            if (Game.ExploreBase)
            {
                var lander=MarsWorld.G(MarsWorld.CrashXZ);
                var p=MarsWorld.G(lander.x-10,lander.z-13,.1f);
                for(int i=0;i<16;i++){Game.Player.Teleport(p,0);Game.Player.LookAt(lander+Vector3.up*1.4f);yield return null;}
                yield return Shot(dir,"cargo_lander");
                MarsWorld.BunkerDoorPanel.GetComponent<SlideDoor>().Snap(true);
                var lockView=MarsWorld.BunkerOutside;
                for(int i=0;i<16;i++){Game.Player.Teleport(lockView,0);Game.Player.LookAt(MarsWorld.InnerHatch.transform.position);yield return null;}
                yield return Shot(dir,"lit_airlock");
                MarsWorld.BunkerDoorPanel.GetComponent<SlideDoor>().Snap(false);
            }
            { // the rover: from its front and from the side
                var rv = MarsWorld.TheRover.transform;
                foreach (var (o, n) in new[] { (new Vector3(0, 0, 9), "rover_front"), (new Vector3(8, 0, 2), "rover_side") })
                {
                    var p = rv.TransformPoint(o); p.y = MarsTerrain.H(p.x, p.z) + 0.2f;
                    for (int f = 0; f < 30; f++) { Game.Player.Teleport(p, 0); Game.Player.LookAt(rv.position + Vector3.up * 1.2f); yield return null; }
                    yield return Shot(dir, n, 0.3f);
                }
            }
            Application.Quit();
        }

        IEnumerator DataShot(string file)
        {
            yield return new WaitForSeconds(12f);
            Screens.I.OpenData(false);
            yield return new WaitForSeconds(2f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(file);
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        IEnumerator TitleShot(string file)
        {
            yield return new WaitForSeconds(6f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(file);
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        // ---------------- demo video recording (only with -video <dir>) ----------------
        bool rec; string recDir; int recFrame;
        void Rec(string dir, string seg) { recDir = Path.Combine(dir, seg); Directory.CreateDirectory(recDir); recFrame = 0; rec = true; }
        void Stop() { rec = false; }
        IEnumerator RecLoop()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                if (!rec) continue;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(recDir, $"f_{recFrame++:00000}.jpg"), tex.EncodeToJPG(90));
                Destroy(tex);
            }
        }

        IEnumerator Preview(bool shelter)
        {
            while (!Game.Player || UIState.Cinematic) yield return null;
            yield return new WaitForSeconds(3f);
            Vector2 target = shelter ? MarsWorld.BunkerXZ : new Vector2(-4, -44);
            Vector2 from = shelter ? target + new Vector2(14, 14) : target + new Vector2(-26, -26);
            for (int i = 0; i < 400; i++) // hold the view for a few seconds
            {
                Game.Player.Teleport(MarsWorld.G(from, shelter ? 1f : 9f), 0);
                Game.Player.LookAt(MarsWorld.G(target, 2f));
                yield return null;
            }
        }

        IEnumerator VideoTour(string dir)
        {
            Directory.CreateDirectory(dir);
            Time.captureFramerate = 30;
            StartCoroutine(RecLoop());
            // A: the crash
            yield return new WaitForSeconds(13.0f);
            Rec(dir, "a_crash"); yield return new WaitForSeconds(6f); Stop();
            // B: waking up
            while (!Game.Player) yield return null;
            yield return new WaitForSeconds(3.5f);
            Rec(dir, "b_wake"); yield return new WaitForSeconds(4f); Stop();
            // C: real solar flare warning
            while (Game.Story.CurrentFlare == null) yield return null;
            Game.Player.LookAt(MarsWorld.BunkerOutside + Vector3.up * 5);
            Rec(dir, "c_flare"); yield return new WaitForSeconds(6f); Stop();
            while (Game.Suit.Storm < 0.3f) yield return null;
            Rec(dir, "c2_storm"); yield return new WaitForSeconds(3f); Stop();
            MarsWorld.BunkerDoor.Use();
            // D: MOXIE info card
            while (!MarsWorld.MoxieSlot.enabled) yield return null;
            yield return new WaitForSeconds(1f);
            MarsWorld.MoxieCrate.Use(); yield return new WaitForSeconds(0.8f);
            Rec(dir, "d_moxie"); yield return new WaitForSeconds(4f); Stop();
            Screens.I.SendMessage("CloseInfoForTour"); yield return new WaitForSeconds(0.6f);
            MarsWorld.MoxieSlot.Use(); yield return new WaitForSeconds(0.5f);
            MarsWorld.WaterCrate.Use(); yield return new WaitForSeconds(0.5f);
            Screens.I.SendMessage("CloseInfoForTour"); yield return new WaitForSeconds(0.6f);
            MarsWorld.WaterSlot.Use();
            Rec(dir, "d2_bunker"); yield return new WaitForSeconds(3f); Stop();
            MarsWorld.ExitDoor.Use();
            yield return new WaitForSeconds(48f);
            // E: puzzle
            var p = MarsWorld.Parts[0];
            p.Stage = p.Path.Length - 1; p.Go.transform.position = MarsWorld.G(p.Path[p.Stage], 0.4f);
            Game.Player.Teleport(MarsWorld.G(p.Path[p.Stage].x + 5, p.Path[p.Stage].y + 4, 0.2f), 0);
            Game.Player.LookAt(p.Go.transform.position + Vector3.up);
            Rec(dir, "e_part"); yield return new WaitForSeconds(2f); Stop();
            p.Use.Use(); yield return new WaitForSeconds(1f);
            Rec(dir, "e_puzzle"); yield return new WaitForSeconds(1.8f);
            Screens.I.TourAnswer(); yield return new WaitForSeconds(2.4f); Stop();
            Screens.I.TourContinue(); yield return new WaitForSeconds(0.6f);
            Screens.I.TourAnswer(); yield return new WaitForSeconds(0.6f);
            Screens.I.TourContinue(); yield return new WaitForSeconds(1f);
            // F: meteors
            Game.Player.Teleport(MarsWorld.G(10, -10, 0.2f), 30);
            Game.Story.StartCoroutine(Game.Story.MeteorShower());
            yield return new WaitForSeconds(5f);
            Rec(dir, "f_meteor"); yield return new WaitForSeconds(5f); Stop();
            yield return new WaitForSeconds(20f);
            // G: build the new rocket
            Game.Player.Teleport(MarsWorld.G(MarsWorld.PadXZ.x - 24, MarsWorld.PadXZ.y + 16, 0.1f), 0);
            Game.Player.LookAt(MarsWorld.Rocket.position + Vector3.up * 9);
            yield return new WaitForSeconds(1f);
            Rec(dir, "g_build");
            yield return new WaitForSeconds(0.8f);
            for (int i = 0; i < 3; i++) { MarsWorld.BuildSection(i); Sfx.Play("build", 0.8f); yield return new WaitForSeconds(1.0f); }
            yield return new WaitForSeconds(1f); Stop();
            // H: the flare report
            Game.Story.StartCoroutine(Game.Story.SecondStorm());
            yield return new WaitForSeconds(42f);
            Rec(dir, "h_report"); yield return new WaitForSeconds(3.5f); Stop();
            Application.Quit();
        }

        /// <summary>Screenshots of the new missions: closed door, dark shelter, power-up, rover, ice, air leak.</summary>
        IEnumerator Tour2(string dir)
        {
            Directory.CreateDirectory(dir);
            while (!Game.Player || Game.Player.Frozen) yield return null;
            while (Game.Suit.Storm < 0.2f) yield return null;
            var door = MarsWorld.BunkerOutside;
            var away = door - MarsWorld.Bunker.position; away.y = 0; Game.Player.Teleport(MarsWorld.G(door.x + away.normalized.x * 5f, door.z + away.normalized.z * 5f, 0.1f), 0); Game.Player.LookAt(MarsWorld.BunkerDoorPanel.position + Vector3.up * 1.5f);
            yield return Shot(dir, "a_door_closed", 1.5f);
            MarsWorld.BunkerDoor.Use();
            yield return Shot(dir, "b_door_opening", 0.25f);
            while (!MarsWorld.BatteryPlug.enabled) yield return null;
            Game.Player.LookAt(MarsWorld.BatteryPlug.transform.position + Vector3.up * 0.6f);
            yield return Shot(dir, "c_dark_power_corner", 2f);
            MarsWorld.BatteryPlug.Use(); yield return new WaitForSeconds(1f); MarsWorld.PowerSwitch.Use();
            yield return Shot(dir, "d_power_on", 3f);
            Game.Player.LookAt(MarsWorld.LeakPos - Vector3.up * 1.2f);
            yield return Shot(dir, "e_south_wall", 1f);
            Game.Player.LookAt(MarsWorld.Interior.transform.TransformPoint(new Vector3(5.5f, 1.5f, 0)));
            yield return Shot(dir, "e2_east_wall", 1f);
            while (!MarsWorld.MoxieSlot.enabled) yield return null;
            MarsWorld.MoxieCrate.Use(); yield return new WaitForSeconds(0.3f); MarsWorld.MoxieSlot.Use();
            yield return new WaitForSeconds(0.5f); MarsWorld.WaterCrate.Use(); yield return new WaitForSeconds(0.3f); MarsWorld.WaterSlot.Use();
            yield return new WaitForSeconds(2f);
            for (int k = 0; k < 3; k++) { Screens.I.SendMessage("CloseInfoForTour", SendMessageOptions.DontRequireReceiver); yield return new WaitForSeconds(0.4f); }
            MarsWorld.ExitDoor.Use();
            yield return new WaitForSeconds(2f);
            Game.Player.LookAt(MarsWorld.TheRover.transform.position);
            yield return Shot(dir, "f_outside_rover", 0.5f);
            MarsWorld.TheRover.Enter();
            yield return Shot(dir, "g_rover_cam", 2f);
            MarsWorld.TheRover.Exit();
            while (!MarsWorld.IceBlocks[0].activeSelf) yield return null;
            var ice = MarsWorld.IceBlocks[0].transform.position;
            Game.Player.Teleport(MarsWorld.G(ice.x + 9, ice.z + 6, 0.1f), 0); Game.Player.LookAt(ice + Vector3.up);
            yield return Shot(dir, "h_ice", 2f);
            Game.Player.Teleport(MarsWorld.BunkerOutside, MarsWorld.BunkerOutsideYaw);
            yield return new WaitForSeconds(20f);
            MarsWorld.BunkerDoor.Use(); yield return new WaitForSeconds(2f);
            Game.Story.StartCoroutine(Game.Story.AirLeak());
            Game.Player.LookAt(MarsWorld.LeakPos);
            yield return Shot(dir, "i_leak", 4f);
            yield return Shot(dir, "j_leak2", 3f);
            Application.Quit();
        }

        IEnumerator Tour(string dir)
        {
            Directory.CreateDirectory(dir);
            yield return Shot(dir, "01_launch", 3f);
            yield return Shot(dir, "02_transit", 6f);
            yield return Shot(dir, "03_entry", 6f);
            yield return Shot(dir, "03b_crash", 2.6f);
            yield return Shot(dir, "03c_crash", 0.4f);
            while (!Game.Player) yield return null;
            yield return Shot(dir, "04_wake", 3f);
            while (Game.Player.Frozen) yield return null;
            yield return Shot(dir, "05_hud", 2f);
            Game.Player.LookAt(Game.Player.Cam.transform.position + MarsWorld.Earth.GetComponent<SkyBody>().Dir * 100);
            yield return Shot(dir, "06_earth", 2f);
            while (Game.Suit.Storm < 0.2f) yield return null;
            Game.Player.LookAt(MarsWorld.BunkerOutside + Vector3.up * 6);
            yield return Shot(dir, "07_storm", 2.5f);
            MarsWorld.BunkerDoor.Use();
            yield return Shot(dir, "08_inside", 2.5f);
            yield return new WaitForSeconds(12f);
            yield return Shot(dir, "09_inside2", 1f);
            // jump ahead: give everything, start parts hunt
            while (!MarsWorld.MoxieSlot.enabled) yield return null;
            var crate = MarsWorld.MoxieCrate; crate.Use(); yield return new WaitForSeconds(0.3f); MarsWorld.MoxieSlot.Use();
            yield return new WaitForSeconds(0.5f); MarsWorld.WaterCrate.Use(); yield return new WaitForSeconds(0.3f); MarsWorld.WaterSlot.Use();
            yield return Shot(dir, "10_machines", 3f);
            MarsWorld.ExitDoor.Use();
            yield return new WaitForSeconds(48f);
            Game.Player.LookAt(MarsWorld.Bunker.position + Vector3.up * 10);
            yield return Shot(dir, "11_timer", 2f);
            Game.Player.Teleport(MarsWorld.G(MarsWorld.PadXZ.x - 22, MarsWorld.PadXZ.y + 14, 0.1f), 0); Game.Player.LookAt(MarsWorld.Rocket.position + Vector3.up * 5);
            yield return Shot(dir, "11b_pad", 1.5f);
            Game.Player.Teleport(MarsWorld.G(-40, -10, 0.1f), 0); Game.Player.LookAt(MarsWorld.G(-66, -40, 6));
            yield return Shot(dir, "11c_basecamp", 1.5f);
            var p = MarsWorld.Parts[0];
            p.Stage = 1; p.Go.transform.position = MarsWorld.G(p.Path[1], 0.4f);
            Game.Player.Teleport(MarsWorld.G(p.Path[1].x + 5, p.Path[1].y + 4, 0.2f), 0);

            Game.Player.LookAt(p.Go.transform.position + Vector3.up * 3);
            yield return Shot(dir, "12_part", 2f);
            p.Use.Use();
            yield return Shot(dir, "13_puzzle", 1.5f);
            Screens.I.OpenData(true);
            yield return Shot(dir, "14_data", 1f);
            Screens.I.SendMessage("CloseData"); Screens.I.TogglePause(false); yield return new WaitForSeconds(0.5f);
            Game.Player.Teleport(MarsWorld.G(10, -10, 0.2f), 0);
            Game.Story.StartCoroutine(Game.Story.MeteorShower());
            yield return Shot(dir, "15_meteors", 9f);
            yield return Shot(dir, "16_meteors2", 2f);
            Game.Story.StartCoroutine(Game.Story.SecondStorm());
            yield return Shot(dir, "16b_flare", 8f);
            yield return new WaitForSeconds(30f);
            yield return Shot(dir, "16c_flare_report", 1f);
            Screens.I.SendMessage("CloseInfoForTour", SendMessageOptions.DontRequireReceiver);
            yield return new WaitForSeconds(1f);
            Game.Story.StartCoroutine(Game.Story.DustStorm());
            yield return Shot(dir, "17_dust", 12f);
            Game.Player.LookAt(MarsWorld.Rocket.position + Vector3.up * 6);
            yield return Shot(dir, "18_dust2", 3f);
            Application.Quit();
        }
    }
}
