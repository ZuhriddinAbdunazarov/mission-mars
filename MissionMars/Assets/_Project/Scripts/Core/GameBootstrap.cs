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
            var story = gameObject.AddComponent<Story>();
            story.Init(cine, screens);
            yield return null;
            Cinema.I.ClearCaption();

            string vid = Arg("-video");
            if (vid != null) { Application.runInBackground = true; Story.QuickStart = true; StartCoroutine(VideoTour(vid)); }
            string tshot = Arg("-titleshot");
            if (tshot != null) { Application.runInBackground = true; StartCoroutine(TitleShot(tshot)); }
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
