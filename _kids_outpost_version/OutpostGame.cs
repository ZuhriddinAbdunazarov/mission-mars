using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    /// <summary>Game flow: title → short intro → ROVY tutorial → sol-by-sol mission with real NASA events → win / try again.</summary>
    public class OutpostGame : MonoBehaviour
    {
        SimState s; readonly List<SimState> history = new List<SimState>();
        OutpostUI ui; Outpost world; CameraRig rig;
        bool busy, playing, nextPressed, playPressed, tutorial;
        bool tipBuild, tipDust, tipAir, tipFood, tipHealth;
        float lastTip;

        public void Init(OutpostUI u, Outpost w, CameraRig r)
        {
            ui = u; world = w; rig = r;
            ui.OnPlay = () => playPressed = true;
            ui.OnHow = () => StartCoroutine(HowTo());
            ui.OnNextSol = () => { if (!busy && playing) nextPressed = true; };
            ui.OnLevel = ChangeLevel;
            ui.OnBuild = DoBuild;
        }

        Vector3 Pos(string id) => Outpost.Pos(id);
        Vector3 Home => Outpost.Pos("hab") + Vector3.up * 2;

        // ================================================================
        public IEnumerator Run()
        {
            while (true)
            {
                yield return Title();
                yield return Intro();
                NewMission();
                yield return Tutorial();
                yield return Mission();
            }
        }

        IEnumerator Title()
        {
            Game.InMenu = true; playing = false; playPressed = false;
            ui.ShowHUD(false); ui.ShowTitle(true);
            rig.UserControl = false; rig.AutoSpin = true; rig.FlyTo(Home, 75, 20);
            Game.Sky.TimeOfDay = 0.36f;
            AudioManager.I.Music("music_hope", 0.5f, 2f);
            AudioManager.I.FadeSource(AudioManager.I.Wind, 0.25f, 2f);
            yield return Cinema.I.FromBlack(1.2f);
            while (!playPressed) yield return null;
            ui.ShowTitle(false);
        }

        IEnumerator HowTo()
        {
            yield return ui.Popup("robot", K.Orange, "HOW TO PLAY",
                "1. Give power to your machines with  <b>+</b>  and  <b>−</b>.\n2. Press <b>NEXT SOL</b> to live one Mars day.\n3. Keep <color=#3FA9FF><b>Air</b></color>, <color=#55C759><b>Food</b></color> and <color=#FF5C80><b>Health</b></color> up for 20 sols!",
                "Power is limited — every choice costs something. Just like real NASA mission planners!",
                new[] { "GOT IT!" }, null, null);
        }

        IEnumerator Intro()
        {
            Game.InMenu = false;
            AudioManager.I.Music("music_ambient", 0.4f, 2f);
            rig.AutoSpin = false;
            rig.Focus = rig.FocusTarget = Home;
            rig.Dist = rig.DistT = 220; rig.Pitch = rig.PitchT = 70; rig.Yaw = rig.YawT = 160;
            rig.FlyTo(Home, 60, 38, 200);
            Sfx.Play("whoosh", 0.6f, 0.7f);
            StartCoroutine(ui.Banner("WELCOME TO MARS!", "Sol 1  ·  Jezero Crater", 1.6f));
            float t = 0; while (t < 3.2f && !Input.GetMouseButtonDown(0)) { t += Time.deltaTime; yield return null; }
            rig.UserControl = true;
            ui.ShowHUD(true);
        }

        void NewMission()
        {
            s = Rules.NewMission(NasaData.FlarePool(), Random.Range(1, 999999));
            history.Clear();
            tipBuild = tipDust = tipAir = tipFood = tipHealth = false;
            ui.BindShop(s);
            Game.Sky.TimeOfDay = 0.42f;
            RefreshAll();
        }

        void RefreshAll()
        {
            var p = Rules.Predict(s, out float ratio);
            ui.Refresh(s, p);
            world.Apply(s, p, ratio);
        }

        // ================================================================
        IEnumerator Tutorial()
        {
            tutorial = true; playing = true; busy = true;
            yield return ui.Rovy("Hi! I'm <b>ROVY</b>. Three astronauts live in this Mars base. Let's keep them safe!");
            ui.HighlightSys(0); busy = false;
            rig.FlyTo(Pos("air") + Vector3.up * 2, 40, 32);
            StartCoroutine(ui.Rovy("Astronauts need <color=#3FA9FF><b>AIR</b></color>. Press the  <b>+</b>  on the <b>AIR MAKER</b>!", false));
            while (s.Level[0] < 1) yield return null;
            ui.HighlightSys(-1);
            yield return ui.Rovy("Look! Red Mars air goes in... blue <b>oxygen</b> comes out! This is real NASA tech called <b>MOXIE</b>.");
            rig.FlyTo(Pos("farm") + Vector3.up * 2, 40, 32);
            ui.HighlightSys(1);
            StartCoroutine(ui.Rovy("They need <color=#55C759><b>FOOD</b></color> too. Press  <b>+</b>  on the <b>GREENHOUSE</b>!", false));
            while (s.Level[1] < 1) yield return null;
            ui.HighlightSys(-1); ui.HideRovy();
            yield return Perchlorate();
            rig.FlyTo(Home, 60, 38);
            yield return ui.Rovy("But <color=#E6A800><b>POWER</b></color> is limited! Every machine uses some. Look at <b>POWER TODAY</b> at the bottom.");
            yield return ui.Rovy("Every choice costs something — just like for real NASA teams. Ready? Press <b>NEXT SOL</b>!");
            ui.PulseNext(true);
            tutorial = false;
        }

        IEnumerator Perchlorate()
        {
            if (s.HasDrill) yield break;
            s.SawPerchlorate = true;
            Sfx.Play("error", 0.6f);
            yield return ui.Popup("warn", K.Orange, "Oh no! Toxic dirt!",
                "Mars dirt has a salt that is bad for plants and people. We can't plant in it.\nLet's grow plants in <b>water</b> instead. Build an <b>Ice Drill</b> to get water from underground ice!",
                NasaData.FarmFact, new[] { "BUILD ICE DRILL" }, new[] { K.Air }, null);
            var drill = Rules.Builds[0];
            if (!Rules.CanBuild(s, drill, out _)) s.Battery = Mathf.Max(s.Battery, drill.PowerNow);
            DoBuild(drill);
            yield return new WaitForSeconds(1.2f);
        }

        // ================================================================
        IEnumerator Mission()
        {
            while (true)
            {
                busy = false; nextPressed = false;
                yield return SolStartEvents();
                while (!nextPressed) { Tips(); yield return null; }
                ui.PulseNext(false);
                busy = true;
                history.Add(s.Clone());
                yield return AdvanceSol();
                if (Lost(out string why)) { bool retry = false; yield return Fail(why, r => retry = r); if (!retry) yield break; continue; }
                if (s.Sol > SimState.GoalSol) { yield return Win(); yield break; }
            }
        }

        IEnumerator SolStartEvents()
        {
            var fl = s.FlareOn(s.Sol);
            if (fl != null)
            {
                Sfx.Play("alarm", 0.5f);
                rig.FlyTo(Home, 60, 38);
                string src = fl.Live ? $"LIVE from NASA DONKI: a class <b>{fl.Class}</b> flare happened on {fl.Date}." : $"From NASA DONKI (archived): a real class <b>{fl.Class}</b> flare on {fl.Date}.";
                yield return ui.Popup("flare", K.Bad, "SOLAR FLARE TONIGHT!",
                    $"The Sun is shooting out a <b>{fl.Size}</b> blast of radiation. It will hit your base tonight!\nTurn on the <b>SHIELD</b> to protect your crew. But a shield uses power...",
                    src + " Flares this big really happen!", new[] { "GET READY!" }, new[] { K.Shield }, null);
                ui.HighlightSys(2);
            }
            else ui.HighlightSys(-1);
            if (s.StormLeft == 3)
            {
                Sfx.Play("whoosh", 0.7f, 0.5f);
                yield return ui.Popup("dust", new Color(0.75f, 0.45f, 0.3f), "DUST STORM!",
                    "A giant dust storm is here for <b>3 sols</b>. Your solar panels will make much less power.\nUse your battery wisely!",
                    NasaData.StormFact, new[] { "WE CAN DO IT!" }, new[] { K.Orange }, null);
            }
            if (!s.StormToday && s.Dust > 0.3f && !tipDust) { tipDust = true; StartCoroutine(ui.Rovy("The storm is over, but the panels are dusty! Clean them in the <b>BUILD</b> shop.", false, 6)); ui.PulseBuild(true); }
            if (s.Science >= 8 && !tipBuild) { tipBuild = true; StartCoroutine(ui.Rovy("You found science stars! Mission Control can send new parts. Press <b>BUILD</b>!", false, 6)); ui.PulseBuild(true); }
        }

        void Tips()
        {
            if (Time.time - lastTip < 8) return;
            var p = Rules.Predict(s, out _);
            if (!tipAir && s.Air + p.Air < 35) { tipAir = true; lastTip = Time.time; StartCoroutine(ui.Rovy("Air is getting low! Give the <b>AIR MAKER</b> more power.", false, 6)); ui.HighlightSys(0); }
            else if (!tipFood && s.Food + p.Food < 35) { tipFood = true; lastTip = Time.time; StartCoroutine(ui.Rovy("Food is getting low! Turn up the <b>GREENHOUSE</b>.", false, 6)); ui.HighlightSys(1); }
            else if (!tipHealth && s.Health < 50) { tipHealth = true; lastTip = Time.time; StartCoroutine(ui.Rovy("Crew health is low. Keep Air and Food high and use the <b>SHIELD</b> to heal.", false, 6)); }
        }

        IEnumerator AdvanceSol()
        {
            ui.HideRovy(); ui.CloseShop();
            rig.FlyTo(Home, 60, 38);
            var fl = s.FlareOn(s.Sol);
            float block = Rules.ShieldBlock(s);
            // dusk
            yield return Game.Sky.Timelapse(0.82f, 1.4f);
            var before = s.Clone();
            var rep = Rules.Step(s);
            if (fl != null)
            {
                Sfx.Play("explosion", 0.5f);
                rig.Shake(0.6f);
                yield return world.FlareHit(block);
            }
            // results float above buildings
            Float(Pos("air") + Vector3.up * 6, s.Air - before.Air, K.Air, "air");
            Float(Pos("farm") + Vector3.up * 6, s.Food - before.Food, K.Food, "food");
            Float(Pos("hab") + Vector3.up * 8, s.Health - before.Health, K.Health, "health");
            Float(Pos("battery") + Vector3.up * 5, s.Battery - before.Battery, new Color(0.85f, 0.6f, 0f), "power");
            if (s.Science - before.Science > 0.5f) Float(Pos("rover") + Vector3.up * 5, s.Science - before.Science, K.Star, "stars");
            Sfx.Play("beep", 0.5f);
            RefreshAll();
            yield return new WaitForSeconds(0.6f);
            Game.Sky.TimeOfDay = -0.2f + 0.02f;
            StartCoroutine(ui.Banner($"SOL {s.Sol}", s.Sol <= SimState.GoalSol ? $"{SimState.GoalSol - s.Sol + 1} sols until the ship comes" : "The ship is here!", 1.0f));
            yield return Game.Sky.Timelapse(0.42f - 0.0f + 0f, 1.6f);
            Game.Sky.TimeOfDay = 0.42f;
            if (rep.P.Short) StartCoroutine(ui.Rovy("Not enough power yesterday! Some machines slowed down.", false, 5));
            if (fl != null)
            {
                int lost = Mathf.RoundToInt(rep.RadDamage);
                string res = block >= 0.8f ? $"Your shield blocked <b>{block * 100:0}%</b>! The crew is safe. Great choice!" :
                             block > 0 ? $"Your shield blocked <b>{block * 100:0}%</b>. The crew lost <b>{lost}</b> health." :
                             $"No shield! The crew lost <b>{lost}</b> health. Next time, turn on the SHIELD.";
                yield return ui.Popup(block >= 0.8f ? "shield" : "health", block >= 0.8f ? K.Shield : K.Health, block >= 0.8f ? "SHIELD WORKED!" : "THE FLARE HIT!", res,
                    $"Real world: NASA's space weather team watches the Sun every day (DONKI). Earth's magnetic field protects us — Mars has none, so shields matter. Flare used: {fl.Class}, {fl.Date}{(fl.Live ? " (live)" : " (archived)")}.",
                    new[] { "OK" }, new[] { K.Go }, null);
            }
        }

        void Float(Vector3 pos, float d, Color c, string what)
        {
            int v = Mathf.RoundToInt(d); if (v == 0) return;
            ui.Float(pos, (v > 0 ? "+" : "") + v + " " + what, v > 0 ? c : K.Bad);
        }

        bool Lost(out string why)
        {
            why = s.Air <= 0 ? "air" : s.Food <= 0 ? "food" : s.Health <= 0 ? "health" : null;
            return why != null;
        }

        IEnumerator Fail(string why, System.Action<bool> retry)
        {
            Sfx.Play("error", 0.7f);
            string title = why == "air" ? "Out of air!" : why == "food" ? "Out of food!" : "The crew got sick!";
            string body = why == "air" ? "The astronauts needed more oxygen.\n<b>Tip:</b> keep the AIR MAKER on, even when power is low."
                        : why == "food" ? "The greenhouse could not grow enough food.\n<b>Tip:</b> turn the GREENHOUSE up early — plants take time!"
                        : "Too much radiation or too little air and food.\n<b>Tip:</b> turn on the SHIELD before a solar flare.";
            int choice = 0;
            yield return ui.Popup(why == "air" ? "air" : why == "food" ? "food" : "health", K.Bad, title, body,
                "Real NASA planners test many plans in simulations before a mission. Failing here is how you learn!",
                new[] { "TRY AGAIN (2 sols back)", "NEW MISSION" }, new[] { K.Go, K.Blue }, c => choice = c);
            if (choice == 0 && history.Count > 0)
            {
                int back = Mathf.Max(0, history.Count - 2);
                var restored = history[back].Clone();
                history.RemoveRange(back, history.Count - back);
                CopyInto(restored);
                RefreshAll();
                StartCoroutine(ui.Rovy("Let's try a different plan! What will you change?", false, 5));
                retry(true);
            }
            else
            {
                NewMission();
                StartCoroutine(ui.Rovy("A brand new mission! Storms and flares will come at different times.", false, 6));
                retry(true);
            }
        }

        void CopyInto(SimState src)
        {
            s.Sol = src.Sol; s.Air = src.Air; s.Food = src.Food; s.Health = src.Health; s.Battery = src.Battery; s.Science = src.Science;
            s.BatteryCap = src.BatteryCap; s.Panels = src.Panels; s.Dust = src.Dust; s.HasDrill = src.HasDrill; s.ShieldWalls = src.ShieldWalls;
            s.Level = (int[])src.Level.Clone(); s.StormLeft = src.StormLeft; s.StormStart = src.StormStart; s.DoseMSv = src.DoseMSv;
        }

        IEnumerator Win()
        {
            playing = false;
            rig.FlyTo(Pos("pad") + Vector3.up * 6, 60, 25);
            AudioManager.I.Music("music_hope", 0.6f, 2f);
            yield return ui.Rovy("Look up! The ship from Earth is here!", false, 3);
            yield return world.ShipArrives();
            Sfx.Play("success", 0.8f);
            int stars = Mathf.RoundToInt(s.Science);
            string rank = s.Health >= 80 ? "SUPER COMMANDER" : s.Health >= 50 ? "MISSION COMMANDER" : "BRAVE COMMANDER";
            int choice = 0;
            yield return ui.Popup("science", K.Star, "MISSION COMPLETE!",
                $"You kept your crew safe for <b>{SimState.GoalSol} sols</b> on Mars!\nHealth: <b>{s.Health:0}%</b>   ·   Science stars: <b>{stars}</b>\nRank: <color=#FF7A33><b>{rank}</b></color>",
                $"Your crew's radiation dose was about {s.DoseMSv:0} mSv (based on Curiosity RAD data). Real mission planners balance choices like yours every day.",
                new[] { "PLAY AGAIN", "MENU" }, new[] { K.Go, K.Blue }, c => choice = c);
            if (choice == 0) { NewMission(); yield return Mission(); }
            else yield return Cinema.I.ToBlack(0.6f);
        }

        // ================================================================ player actions
        void ChangeLevel(int i, int d)
        {
            if (busy && !tutorial || s == null) return;
            int nl = Mathf.Clamp(s.Level[i] + d, 0, 2);
            if (nl == s.Level[i]) return;
            s.Level[i] = nl;
            Sfx.Play(d > 0 ? "pickup" : "click", 0.6f, d > 0 ? 1 + nl * 0.1f : 0.8f);
            RefreshAll();
            if (i == 1 && nl > 0 && !s.HasDrill && !tutorial && !s.SawPerchlorate) StartCoroutine(Perchlorate());
        }

        void DoBuild(Rules.Build b)
        {
            Rules.DoBuild(s, b);
            Sfx.Play("build", 0.8f); Sfx.Play("success", 0.5f);
            string anchor = b.Id == "drill" ? "drill" : b.Id == "panel" || b.Id == "clean" ? "power" : b.Id == "battery" ? "battery" : "hab";
            var at = Pos(anchor);
            FX.BurstSparks(at + Vector3.up * 3, new Color(1f, 0.85f, 0.4f), 60);
            if (b.Id == "clean") FX.DustBlast(at, 0.6f);
            rig.FlyTo(at + Vector3.up * 2, 42, 32);
            ui.Float(at + Vector3.up * 6, b.Name + "!", K.Go);
            RefreshAll();
            ui.PulseBuild(false);
        }

        // ================================================================ clicking buildings
        void Update()
        {
            if (!playing || busy || UIState.Modal) return;
            if (rig.ClickedThisFrame(out var sp) && !Game.PointerOverUI)
            {
                var ray = Game.Cam.ScreenPointToRay(sp);
                if (Physics.Raycast(ray, out var hit, 500))
                {
                    var b = hit.collider.GetComponentInParent<BuildingRef>();
                    if (b) OnBuildingClicked(b.Id);
                }
            }
        }

        void OnBuildingClicked(string id)
        {
            Sfx.Play("beep", 0.5f);
            rig.FlyTo(Pos(id) + Vector3.up * 2, 38, 32);
            string say; int sys = -1;
            switch (id)
            {
                case "air": say = NasaData.MoxieFact; sys = 0; break;
                case "farm": say = NasaData.FarmFact; sys = 1; break;
                case "hab": say = "Your 3 astronauts live here. " + NasaData.ShieldFact; sys = 2; break;
                case "rover": say = NasaData.RoverFact; sys = 3; break;
                case "power": say = NasaData.SolarFact; break;
                case "battery": say = "Batteries save extra power for the night and for dust storms."; break;
                case "drill": say = s.HasDrill ? NasaData.WaterFact : "Build the Ice Drill to get water for plants!"; break;
                case "comms": say = NasaData.DelayFact; break;
                default: say = NasaData.SolFact; break;
            }
            ui.HighlightSys(sys);
            StartCoroutine(ui.Rovy(say, false, 7));
        }
    }
}
