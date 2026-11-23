using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MissionMars
{
    /// <summary>A rocket part that the wind keeps blowing away. Catch it, solve the puzzle, keep it.</summary>
    public class RocketPart
    {
        public string Id, Name, Icon; public Color Color;
        public Vector2[] Path;          // spots it tumbles between; the last one is where it stops
        public int Stage;               // index into Path
        public GameObject Go, Beam; public Interactable Use; public Light Light;
        public bool Found, Installed, Moving;
        public int Puzzle, Section;
        public GameObject Slot, Mounted;
        public Vector3 Pos => Go.transform.position;
    }

    /// <summary>Builds the whole playable world.</summary>
    public static class MarsWorld
    {
        public static readonly Vector2 WakeXZ = new Vector2(0, 0);
        public static readonly Vector2 CrashXZ = new Vector2(10, 26);
        public static readonly Vector2 BunkerXZ = new Vector2(-22, -48); // the shelter is next to the MARS BASE
        public static readonly Vector2 PadXZ = new Vector2(40, -34);
        public static readonly Vector3 InteriorOrigin = new Vector3(0, -90, 0);
        public static readonly Vector2[] IceXZ = { new Vector2(-82, -8), new Vector2(-74, -66), new Vector2(-48, -96) };
        public static readonly Vector2 BaseXZ = new Vector2(4, -70); // the MARS BASE (bought Tripo model, Assets/Paid)
        public const float BaseSize = 34f;                            // metres across (smaller = sharper texture, real-size doors)
        public static Transform MarsBase;

        public static GameObject Root, Interior;
        public static Transform Wreck, Bunker, Pad, Rocket;
        public static Interactable BunkerDoor, ExitDoor, MoxieCrate, WaterCrate, MoxieSlot, WaterSlot, Bed, Tap, LaunchConsole, RocketUse, DataConsole;
        public static readonly List<Interactable> Pouches = new List<Interactable>();
        public static GameObject MoxieInstalled, WaterInstalled, MoxieGhost, WaterGhost;
        public static Vector3 BunkerOutside, InteriorSpawn; public static float BunkerOutsideYaw, InteriorYaw;
        public static TextMeshPro TimerText, TimerInside; public static GameObject TimerHolo;
        public static RocketPart[] Parts;
        public static Transform Earth, Phobos;
        public static readonly List<Light> InteriorLights = new List<Light>();
        public static Light AlarmLight;
        public static ParticleSystem MoxieBubbles, WaterDrip;
        // new missions: closed doors, power-up, oxygen leak, rover + ice
        public static Transform BunkerDoorPanel, ExitDoorPanel;
        public static Interactable BatteryPlug, PowerSwitch, LeakValve;
        public static GameObject Cable; public static ParticleSystem LeakSteam; public static Vector3 LeakPos;
        public static Rover TheRover; public static readonly List<GameObject> IceBlocks = new List<GameObject>();
        static readonly List<(Light l, float on)> powerLights = new List<(Light, float)>();
        static readonly List<(Material m, Color c)> powerGlows = new List<(Material, Color)>();
        static Light emergency;

        static Material holoCyan, holoAmber;

        public static Vector3 G(Vector2 xz, float up = 0) => MarsTerrain.Ground(xz.x, xz.y, up);
        public static Vector3 G(float x, float z, float up = 0) => MarsTerrain.Ground(x, z, up);

        public static void Build()
        {
            Root = new GameObject("World");
            holoCyan = R.Holo(new Color(0.3f, 0.9f, 1f), 1.4f, 0.12f);
            holoAmber = R.Holo(new Color(1f, 0.6f, 0.2f), 1.4f, 0.12f);
            var terrain = MarsTerrain.Build(R.Template("Terrain"));
            terrain.transform.SetParent(Root.transform, true);
            BuildWreck();
            BuildBunker();
            BuildPad();
            BuildParts();
            BuildRoverAndIce();
            BuildMarsBase();
            PlaceLabels();
            Rocks();
            BuildRover();
            BuildInterior();
            Greenhouse.Build();
            BuildRooms();
            SkyBodies();
            FX.DustBanks(new Vector3(0, MarsTerrain.Base + 2, 0)).transform.SetParent(Root.transform, true);
        }

        static bool Avoid(Vector2 xz, float pad)
        {
            if (Vector2.Distance(xz, WakeXZ) < 8 + pad) return true;
            if (Vector2.Distance(xz, CrashXZ) < 26 + pad) return true;
            if (Vector2.Distance(xz, BunkerXZ) < 20 + pad) return true;
            if (Vector2.Distance(xz, PadXZ) < 16 + pad) return true;
            foreach (var q in IceXZ) if (Vector2.Distance(xz, q) < 5 + pad) return true;
            if (Vector2.Distance(xz, BaseXZ) < BaseSize * 1.15f + pad) return true; // keep big boulders away from the base view
            if (Parts != null) foreach (var p in Parts) foreach (var q in p.Path) if (Vector2.Distance(xz, q) < 5 + pad) return true;
            return false;
        }

        // ------------------------------------------------------------------
        static void Rocks()
        {
            var parent = new GameObject("Rocks").transform; parent.SetParent(Root.transform);
            var meshes = new Mesh[8]; for (int i = 0; i < meshes.Length; i++) meshes[i] = ProcMesh.Rock(100 + i * 7, 2);
            var mat = R.Rock; mat.enableInstancing = true;
            var dark = R.LitTex("rock_boulder_dry_diff", "rock_boulder_dry_nor", new Color(0.45f, 0.30f, 0.24f), 1f, 0.1f, 0); dark.enableInstancing = true;
            var scan = R.Model("SF_Rocks");
            var rng = new System.Random(9);
            float Rnd() => (float)rng.NextDouble();
            int placed = 0;
            for (int i = 0; i < 3000 && placed < 950; i++)
            {
                float x = Rnd() * 640 - 320, z = Rnd() * 640 - 320;
                var xz = new Vector2(x, z);
                if (Avoid(xz, 1)) continue;
                float big = Mathf.Pow(Rnd(), 7f);
                float s = Mathf.Lerp(0.18f, 1.2f, Mathf.Pow(Rnd(), 2.5f)) + big * 5f;
                float density = Mathf.PerlinNoise(x * 0.02f + 3, z * 0.02f + 9);
                if (density < 0.42f && big < 0.2f) continue;
                var g = ProcMesh.MeshObj("Rock", meshes[rng.Next(meshes.Length)], Rnd() < 0.3f ? dark : mat, parent);
                var n = MarsTerrain.Normal(x, z);
                g.transform.position = G(x, z, -s * 0.18f);
                g.transform.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, n, 0.6f)) * Quaternion.Euler(0, Rnd() * 360, 0);
                g.transform.localScale = new Vector3(s * (0.8f + Rnd() * 0.5f), s * (0.7f + Rnd() * 0.5f), s * (0.8f + Rnd() * 0.5f));
                if (s > 1.4f) { var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = g.GetComponent<MeshFilter>().sharedMesh; }
                else g.GetComponent<MeshRenderer>().shadowCastingMode = s > 0.6f ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                placed++;
            }
            // photogrammetry rock scans (Sketchfab, CC-BY) scattered near the play area if available
            if (scan)
            {
                for (int i = 0; i < 14; i++)
                {
                    float a = i * 2.39f, d = 30 + (i * 37 % 90);
                    var xz = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    if (Avoid(xz, 2)) continue;
                    var g = Util.PlaceModel("SF_Rocks", G(xz, -0.3f), 3 + (i % 4) * 1.5f, i * 47, parent, false, true);
                }
            }
        }

        // ------------------------------------------------------------------
        static void BuildWreck()
        {
            Wreck = new GameObject("CrashSite").transform; Wreck.SetParent(Root.transform);
            Vector3 c = G(CrashXZ);
            // our crashed ship (Saturn V) - destroyed for good, it stays here as wreckage
            var ship = Util.PlaceModel("Saturn_V", c, 40, 0, Wreck);
            var sb0 = Util.Bounds(ship);
            Quaternion lie = sb0.size.y >= sb0.size.x && sb0.size.y >= sb0.size.z ? Quaternion.Euler(0, 0, 84) : sb0.size.x >= sb0.size.z ? Quaternion.Euler(0, 90, 6) : Quaternion.Euler(6, 0, 0);
            ship.transform.rotation = Quaternion.Euler(0, 118, 0) * lie;
            var sb1 = Util.Bounds(ship); ship.transform.position += new Vector3(c.x - sb1.center.x, c.y - sb1.min.y - 1.2f, c.z - sb1.center.z);
            foreach (var r in ship.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                foreach (var m in mats)
                {
                    if (m.HasProperty("baseColorFactor")) m.SetColor("baseColorFactor", m.GetColor("baseColorFactor") * new Color(0.5f, 0.45f, 0.42f));
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * new Color(0.5f, 0.45f, 0.42f));
                }
                r.materials = mats;
            }
            var chute = ProcMesh.MeshObj("Parachute", ProcMesh.Dome(24, 6, 0), R.Lit(new Color(0.95f, 0.55f, 0.2f), 0.2f), Wreck);
            chute.transform.position = G(CrashXZ.x + 6, CrashXZ.y - 16, 0.1f); chute.transform.localScale = new Vector3(7f, 0.6f, 5f); chute.transform.rotation = Quaternion.Euler(0, 30, 6);
            // crater gouge + scattered debris
            var gouge = ProcMesh.MeshObj("Scorch", ProcMesh.Dome(24, 4, 0), R.Lit(new Color(0.12f, 0.08f, 0.07f), 0.05f), Wreck);
            gouge.transform.position = G(CrashXZ, -0.25f); gouge.transform.localScale = new Vector3(16, 0.3f, 9); gouge.transform.rotation = Quaternion.Euler(0, 118, 0);
            var rng = new System.Random(3);
            float Rnd() => (float)rng.NextDouble();
            var tank = ProcMesh.Prim(PrimitiveType.Cylinder, "BrokenTank", Wreck, Vector3.zero, new Vector3(3.2f, 4.5f, 3.2f), R.Scorched);
            tank.transform.position = G(CrashXZ.x - 22, CrashXZ.y - 10, 1.1f); tank.transform.rotation = Quaternion.Euler(84, 30, 0);
            var ring = ProcMesh.Prim(PrimitiveType.Cylinder, "Ring", Wreck, Vector3.zero, new Vector3(3.6f, 0.3f, 3.6f), R.HullDark);
            ring.transform.position = G(CrashXZ.x - 17, CrashXZ.y - 6, 1.4f); ring.transform.rotation = Quaternion.Euler(70, 50, 10);
            for (int i = 0; i < 46; i++)
            {
                float a = Rnd() * Mathf.PI * 2, d = 5 + Rnd() * 38;
                var p = CrashXZ + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                if (Vector2.Distance(p, WakeXZ) < 4) continue;
                bool plate = Rnd() < 0.7f;
                var g = ProcMesh.Prim(plate ? PrimitiveType.Cube : PrimitiveType.Cylinder, "Debris", Wreck, Vector3.zero,
                    plate ? new Vector3(0.6f + Rnd() * 2.4f, 0.05f + Rnd() * 0.08f, 0.5f + Rnd() * 1.8f) : new Vector3(0.3f + Rnd() * 0.6f, 0.4f + Rnd() * 1.2f, 0.3f + Rnd() * 0.6f),
                    Rnd() < 0.5f ? R.Scorched : (Rnd() < 0.5f ? R.Hull : R.HullDark), null, d > 12);
                g.transform.position = G(p, 0.1f);
                g.transform.rotation = Quaternion.Euler(Rnd() * 50 - 25, Rnd() * 360, Rnd() * 50 - 25);
            }
            // a piece of debris right next to where you wake up
            var near = ProcMesh.Prim(PrimitiveType.Cube, "NearPlate", Wreck, Vector3.zero, new Vector3(2.4f, 0.08f, 1.6f), R.Scorched);
            near.transform.position = G(WakeXZ.x + 2.4f, WakeXZ.y + 1.5f, 0.3f); near.transform.rotation = Quaternion.Euler(18, 30, 12);
            FX.Fire(near.transform.position + Vector3.up * 0.2f, 0.5f, Wreck);
            FX.Smoke(near.transform.position + Vector3.up * 0.8f, 0.8f, Wreck, 0.1f);

            FX.Fire(G(CrashXZ.x - 4, CrashXZ.y + 3, 0.6f), 1.8f, Wreck);
            FX.Fire(G(CrashXZ.x + 7, CrashXZ.y - 5, 0.4f), 1.2f, Wreck);
            FX.Fire(G(CrashXZ.x - 21, CrashXZ.y - 10, 0.8f), 1.0f, Wreck);
            FX.Smoke(G(CrashXZ.x - 4, CrashXZ.y + 3, 2f), 2.6f, Wreck, 0.09f);
            FX.Smoke(G(CrashXZ.x + 7, CrashXZ.y - 5, 2f), 1.8f, Wreck, 0.14f);
            FX.Smoke(G(CrashXZ.x - 21, CrashXZ.y - 10, 2f), 1.5f, Wreck, 0.12f);
            FX.Sparks(G(CrashXZ.x + 2, CrashXZ.y, 2.2f), Wreck);
            var fl = new GameObject("FireLight").AddComponent<Light>(); fl.transform.SetParent(Wreck); fl.transform.position = G(CrashXZ.x - 4, CrashXZ.y + 3, 2f);
            fl.type = LightType.Point; fl.range = 22; fl.intensity = 7; fl.color = new Color(1f, 0.55f, 0.2f); fl.gameObject.AddComponent<Flicker>();
            if (AudioManager.I) AudioManager.I.Loop("fire", 0.7f, fl.transform, 1, 35);

            // salvage: the two machines + water pouches
            MoxieCrate = Machine("MOXIE", "MOXIE Oxygen Maker", G(CrashXZ.x - 9, CrashXZ.y - 9), new Color(0.3f, 0.85f, 1f));
            WaterCrate = Machine("ICE", "Ice-to-Water Machine", G(CrashXZ.x + 12, CrashXZ.y - 8), new Color(0.45f, 0.65f, 1f));
            Pouches.Clear();
            foreach (var p in new[] { new Vector2(CrashXZ.x - 2, CrashXZ.y - 14), new Vector2(CrashXZ.x + 16, CrashXZ.y + 4), new Vector2(-6, 7) })
            {
                var g = new GameObject("WaterPouch"); g.transform.SetParent(Wreck); g.transform.position = G(p, 0.15f);
                ProcMesh.Prim(PrimitiveType.Capsule, "Bag", g.transform, Vector3.zero, new Vector3(0.28f, 0.2f, 0.12f), R.Lit(new Color(0.75f, 0.85f, 0.95f), 0.85f), new Vector3(90, 0, 20), false);
                ProcMesh.Prim(PrimitiveType.Cylinder, "Cap", g.transform, new Vector3(0, 0, 0.2f), new Vector3(0.06f, 0.04f, 0.06f), R.CyanGlow, new Vector3(90, 0, 0), false);
                var l = new GameObject("L").AddComponent<Light>(); l.transform.SetParent(g.transform, false); l.transform.localPosition = Vector3.up * 0.6f; l.type = LightType.Point; l.range = 3; l.intensity = 1.5f; l.color = new Color(0.4f, 0.8f, 1f);
                g.AddComponent<Bob>();
                Pouches.Add(Interactable.Add(g, "Use", "Emergency Air & Water Pack", null));
            }
        }

        static Interactable Machine(string id, string label, Vector3 pos, Color glow)
        {
            var g = new GameObject(label); g.transform.SetParent(Wreck); g.transform.position = pos;
            var box = ProcMesh.Prim(PrimitiveType.Cube, "Case", g.transform, new Vector3(0, 0.45f, 0), new Vector3(1.2f, 0.9f, 0.9f), R.Panel, new Vector3(4, 20, -3));
            ProcMesh.Prim(PrimitiveType.Cube, "Band", box.transform, new Vector3(0, 0.3f, 0.51f), new Vector3(1.02f, 0.12f, 0.02f), R.Orange, null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Led", box.transform, new Vector3(-0.3f, -0.1f, 0.51f), new Vector3(0.25f, 0.08f, 0.02f), R.Emissive(Color.black, glow * 2.5f), null, false);
            if (id == "MOXIE") Util.PlaceModel("Orbital_Replacement_Unit", pos + Vector3.up * 0.9f, 0.9f, 30, g.transform);
            else for (int i = 0; i < 2; i++) ProcMesh.Prim(PrimitiveType.Cylinder, "Tank", g.transform, new Vector3(-0.25f + i * 0.5f, 1.15f, 0), new Vector3(0.36f, 0.28f, 0.36f), R.Lit(new Color(0.55f, 0.75f, 0.95f), 0.9f, 0.2f), null, false);
            var ringG = ProcMesh.Prim(PrimitiveType.Cylinder, "HoloRing", g.transform, new Vector3(0, 0.02f, 0), new Vector3(2.2f, 0.01f, 2.2f), holoAmber, null, false);
            ringG.AddComponent<Spin>().Speed = 40;
            var l = new GameObject("Glow").AddComponent<Light>(); l.transform.SetParent(g.transform, false); l.transform.localPosition = Vector3.up * 1.6f;
            l.type = LightType.Point; l.range = 5; l.intensity = 3; l.color = new Color(1f, 0.65f, 0.3f);
            return Interactable.Add(g, "Pick up", label, null);
        }

        // ------------------------------------------------------------------
        static void BuildBunker()
        {
            Bunker = new GameObject("Bunker").transform; Bunker.SetParent(Root.transform);
            Vector3 c = G(BunkerXZ); Bunker.position = c;
            Vector3 toWake = new Vector3(WakeXZ.x - BunkerXZ.x, 0, WakeXZ.y - BunkerXZ.y).normalized;
            Bunker.rotation = Quaternion.LookRotation(toWake);
            // UNDERGROUND shelter: the rooms are dug in and covered by a hill of Mars soil (regolith).
            // Studies of Mars habitats say about 1-2 m of soil (or a lava tube) is needed to block most space radiation;
            // a building standing alone on the surface would not protect the crew.
            var soil = R.LitTex("red_laterite_soil_stones_diff", "red_laterite_soil_stones_nor", new Color(0.86f, 0.55f, 0.4f), 3f, 0.08f, 0f);
            var mound = ProcMesh.MeshObj("SoilMound", ProcMesh.Dome(32, 10, 0), soil, Bunker);
            mound.transform.localPosition = new Vector3(0, -0.6f, -1.5f); mound.transform.localScale = new Vector3(9.5f, 4.6f, 9.5f);
            var mmc = mound.AddComponent<MeshCollider>(); mmc.sharedMesh = mound.GetComponent<MeshFilter>().sharedMesh;
            var mound2 = ProcMesh.MeshObj("SoilMound2", ProcMesh.Dome(24, 8, 0), soil, Bunker);
            mound2.transform.localPosition = new Vector3(-8.5f, -0.4f, -5f); mound2.transform.localScale = new Vector3(5.5f, 3f, 5.5f);
            var mmc2 = mound2.AddComponent<MeshCollider>(); mmc2.sharedMesh = mound2.GetComponent<MeshFilter>().sharedMesh;
            // air vents and a periscope camera poking out of the soil
            // ---- the ENTRANCE: a concrete portal cut into the soil hill with a heavy steel blast door (realistic PBR textures) ----
            var concrete = R.LitTex("rock_boulder_dry_diff", "rock_boulder_dry_nor", new Color(0.66f, 0.63f, 0.6f), 2.5f, 0.06f, 0f);
            var steel = R.LitTex("metal_plate_diff", "metal_plate_nor", new Color(0.5f, 0.51f, 0.53f), 1.2f, 0.5f, 0.85f);
            var darkSteel = R.LitTex("metal_plate_diff", "metal_plate_nor", new Color(0.22f, 0.23f, 0.25f), 1.2f, 0.45f, 0.9f);
            var hazardY = R.Lit(new Color(1f, 0.76f, 0.08f), 0.35f); var hazardK = R.Lit(new Color(0.06f, 0.06f, 0.06f), 0.3f);
            var lockT = new GameObject("Airlock").transform; lockT.SetParent(Bunker, false); lockT.localPosition = new Vector3(0, 0, 7.2f);
            // front retaining wall + angled wing walls holding back the soil
            ProcMesh.Prim(PrimitiveType.Cube, "PortalWall", lockT, new Vector3(0, 1.95f, -0.55f), new Vector3(5.6f, 3.9f, 1.0f), concrete);
            ProcMesh.Prim(PrimitiveType.Cube, "PortalTop", lockT, new Vector3(0, 3.95f, -0.45f), new Vector3(6.0f, 0.3f, 1.25f), concrete, null, false);
            // the soil hill comes right up over the portal (radiation shielding: about 2 m of soil on top)
            var cover = ProcMesh.MeshObj("SoilCover", ProcMesh.Dome(28, 10, 0), soil, lockT);
            cover.transform.localPosition = new Vector3(0, -0.4f, -3.9f); cover.transform.localScale = new Vector3(5.4f, 4.4f, 2.9f); // behind the portal wall only
            { var cmc = cover.AddComponent<MeshCollider>(); cmc.sharedMesh = cover.GetComponent<MeshFilter>().sharedMesh; }
            for (int sd = -1; sd <= 1; sd += 2)
                ProcMesh.Prim(PrimitiveType.Cube, "RampWall", lockT, new Vector3(sd * 1.35f, 0.35f, 1.6f), new Vector3(0.3f, 0.7f, 2.6f), concrete);
            // heavy steel frame and the blast door
            ProcMesh.Prim(PrimitiveType.Cube, "FrameTop", lockT, new Vector3(0, 2.75f, 0.02f), new Vector3(2.4f, 0.35f, 0.4f), darkSteel, null, false);
            for (int sd = -1; sd <= 1; sd += 2) ProcMesh.Prim(PrimitiveType.Cube, "FrameSide", lockT, new Vector3(sd * 1.02f, 1.3f, 0.02f), new Vector3(0.35f, 2.6f, 0.4f), darkSteel, null, false);
            // yellow + black hazard chevrons on the frame
            for (int k = 0; k < 7; k++)
            {
                var stripe = ProcMesh.Prim(PrimitiveType.Cube, "Hazard", lockT, new Vector3(-0.9f + k * 0.3f, 2.75f, 0.23f), new Vector3(0.14f, 0.33f, 0.02f), k % 2 == 0 ? hazardY : hazardK, new Vector3(0, 0, 35), false);
            }
            for (int sd = -1; sd <= 1; sd += 2)
                for (int k = 0; k < 8; k++)
                    ProcMesh.Prim(PrimitiveType.Cube, "Hazard", lockT, new Vector3(sd * 1.02f, 0.15f + k * 0.32f, 0.23f), new Vector3(0.33f, 0.15f, 0.02f), k % 2 == 0 ? hazardY : hazardK, new Vector3(0, 0, 35), false);
            // the door is CLOSED (it keeps the air in). Press E: it slides open, you go in, it shuts behind you.
            ProcMesh.Prim(PrimitiveType.Cube, "Doorway", lockT, new Vector3(0, 1.25f, -0.05f), new Vector3(1.7f, 2.4f, 0.02f), R.Emissive(new Color(0.75f, 0.8f, 0.85f), new Color(0.9f, 1.0f, 1.1f)), null, false);
            BunkerDoorPanel = DoorPanel(lockT, new Vector3(0, 1.25f, 0.12f), new Vector3(1.7f, 2.45f, 0.18f));
            foreach (var r in BunkerDoorPanel.GetComponentsInChildren<Renderer>()) if (r.name == "Slab") r.sharedMaterial = steel; // a heavy steel blast door
            for (int k = 0; k < 3; k++) ProcMesh.Prim(PrimitiveType.Cube, "DoorRib", BunkerDoorPanel, new Vector3(0, -0.8f + k * 0.8f, 0.11f), new Vector3(1.6f, 0.08f, 0.04f), darkSteel, null, false);
            var door = BunkerDoorPanel.gameObject;
            // keypad, status light, amber beacon and wall lamps
            ProcMesh.Prim(PrimitiveType.Cube, "Keypad", lockT, new Vector3(1.55f, 1.3f, 0.0f), new Vector3(0.3f, 0.45f, 0.1f), darkSteel, null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "KeypadScreen", lockT, new Vector3(1.55f, 1.4f, 0.06f), new Vector3(0.22f, 0.14f, 0.02f), R.Emissive(Color.black, new Color(0.3f, 2.2f, 0.9f)), null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "DoorLight", lockT, new Vector3(0, 2.98f, 0.1f), new Vector3(1.4f, 0.1f, 0.08f), R.Emissive(Color.black, new Color(0.3f, 2.4f, 1.0f)), null, false);
            var beacon = ProcMesh.Prim(PrimitiveType.Sphere, "Beacon", lockT, new Vector3(0, 4.25f, -0.3f), new Vector3(0.35f, 0.3f, 0.35f), R.Emissive(new Color(0.5f, 0.3f, 0f), new Color(3f, 1.4f, 0.1f)), null, false);
            var bl = new GameObject("BeaconLight").AddComponent<Light>(); bl.transform.SetParent(beacon.transform, false); bl.type = LightType.Point; bl.range = 10; bl.intensity = 2.5f; bl.color = new Color(1f, 0.6f, 0.15f); bl.gameObject.AddComponent<Blink>().Rate = 0.6f;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                ProcMesh.Prim(PrimitiveType.Cube, "WallLamp", lockT, new Vector3(sd * 2.3f, 2.9f, 0.0f), new Vector3(0.5f, 0.18f, 0.25f), R.Emissive(new Color(0.8f, 0.8f, 0.75f), new Color(2.4f, 2.3f, 2.0f)), null, false);
            }
            var dl = new GameObject("DoorLamp").AddComponent<Light>(); dl.transform.SetParent(lockT, false); dl.transform.localPosition = new Vector3(0, 3.2f, 1.6f);
            dl.type = LightType.Point; dl.range = 10; dl.intensity = 3.2f; dl.color = new Color(1f, 0.95f, 0.85f);
            // ramp going DOWN to the door
            var ramp = ProcMesh.Prim(PrimitiveType.Cube, "Ramp", lockT, new Vector3(0, -0.05f, 1.7f), new Vector3(2.4f, 0.2f, 2.8f), concrete, new Vector3(-6, 0, 0), false);
            var sign = World3DText(lockT, new Vector3(0, 3.35f, 0.02f), "UNDERGROUND SHELTER", 2.6f, new Color(1f, 0.85f, 0.4f));
            var sign2 = World3DText(lockT, new Vector3(0, 3.12f, 0.02f), "2 m of Mars soil blocks space rays", 1.7f, new Color(0.9f, 0.95f, 1f));
            sign.transform.localRotation = sign2.transform.localRotation = Quaternion.Euler(0, 180, 0); // face the player outside
            BunkerDoor = Interactable.Add(door, "Go down into", "Underground shelter", null); BunkerDoor.Range = 4.5f;
            BunkerOutside = lockT.TransformPoint(new Vector3(0, 0.1f, 3.2f)); BunkerOutside.y = MarsTerrain.H(BunkerOutside.x, BunkerOutside.z) + 0.1f;
            BunkerOutsideYaw = Bunker.eulerAngles.y;
            // solar field + antenna
            for (int i = 0; i < 5; i++)
            {
                var p = Bunker.TransformPoint(new Vector3(-12 + i * 3.2f, 0, -11));
                p.y = MarsTerrain.H(p.x, p.z);
                ProcMesh.Prim(PrimitiveType.Cylinder, "Post", Root.transform, p + Vector3.up * 0.7f, new Vector3(0.12f, 0.7f, 0.12f), R.HullDark);
                var pan = ProcMesh.Prim(PrimitiveType.Cube, "Solar", Root.transform, p + Vector3.up * 1.5f, new Vector3(2.8f, 0.06f, 1.7f), R.Emissive(new Color(0.04f, 0.07f, 0.18f), new Color(0.02f, 0.05f, 0.18f), 0.92f));
                pan.transform.rotation = Bunker.rotation * Quaternion.Euler(30, 0, 0);
            }

            // the 2-year countdown hologram above the bunker
            TimerHolo = new GameObject("TimerHolo"); TimerHolo.transform.SetParent(Bunker, false); TimerHolo.transform.localPosition = new Vector3(0, 11.5f, 0);
            var ringH = ProcMesh.Prim(PrimitiveType.Cylinder, "Ring", TimerHolo.transform, new Vector3(0, -2.2f, 0), new Vector3(7f, 0.02f, 7f), holoCyan, null, false); ringH.AddComponent<Spin>().Speed = 25;
            ProcMesh.Prim(PrimitiveType.Cylinder, "Beam", TimerHolo.transform, new Vector3(0, -4.5f, 0), new Vector3(0.2f, 2.5f, 0.2f), holoCyan, null, false);
            TimerText = World3DText(TimerHolo.transform, Vector3.zero, "", 7, new Color(0.55f, 0.95f, 1f));
            TimerText.gameObject.AddComponent<Billboard>();
            TimerHolo.SetActive(false);
        }

        public static TextMeshPro World3DText(Transform parent, Vector3 lpos, string s, float size, Color c)
        {
            var g = new GameObject("Text3D"); g.transform.SetParent(parent, false); g.transform.localPosition = lpos;
            var t = g.AddComponent<TextMeshPro>(); t.font = R.Title; t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center;
            t.text = s; t.rectTransform.sizeDelta = new Vector2(20, 6); t.enableWordWrapping = false;
            t.fontSharedMaterial.EnableKeyword("GLOW_ON");
            return t;
        }

        static void Dome(Transform parent, Vector3 lp, float r, Material glass, Material frame)
        {
            var d = ProcMesh.MeshObj("Dome", ProcMesh.Dome(28, 10, 0), glass, parent);
            d.transform.localPosition = lp; d.transform.localScale = new Vector3(r, r * 0.78f, r);
            var mc = d.AddComponent<MeshCollider>(); mc.sharedMesh = d.GetComponent<MeshFilter>().sharedMesh;
            // geodesic ribs
            for (int i = 0; i < 8; i++)
            {
                var rib = ProcMesh.MeshObj("Rib", ProcMesh.Dome(28, 10, 0), frame, d.transform);
                rib.transform.localScale = new Vector3(0.05f, 1.012f, 1.012f); rib.transform.localEulerAngles = new Vector3(0, i * 22.5f, 0);
            }
            for (int k = 1; k <= 3; k++)
            {
                float h = k / 4f, rr = Mathf.Sqrt(1 - h * h);
                var band = ProcMesh.Prim(PrimitiveType.Cylinder, "Band", d.transform, new Vector3(0, h, 0), new Vector3(rr * 2.02f, 0.012f, rr * 2.02f), frame, null, false);
            }
            ProcMesh.Prim(PrimitiveType.Cylinder, "Footing", parent, lp + Vector3.up * 0.15f, new Vector3(r * 2.04f, 0.3f, r * 2.04f), R.HullDark, null, false);
        }

        static void Tube(Transform parent, Vector3 a, Vector3 b, Material m)
        {
            var mid = (a + b) / 2 + Vector3.up * 1.1f; var dir = b - a;
            var t = ProcMesh.Prim(PrimitiveType.Cylinder, "Tube", parent, mid, new Vector3(1.8f, dir.magnitude / 2 + 1f, 1.8f), m, null, false);
            t.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0);
        }

        // ------------------------------------------------------------------
        static void BuildPad()
        {
            Pad = new GameObject("LaunchPad").transform; Pad.SetParent(Root.transform);
            Vector3 c = G(PadXZ); Pad.position = c;
            ProcMesh.Prim(PrimitiveType.Cylinder, "Pad", Pad, new Vector3(0, 0.25f, 0), new Vector3(15, 0.5f, 15), R.LitTex("metal_plate_diff", "metal_plate_nor", new Color(0.35f, 0.33f, 0.32f), 6, 0.3f, 0.3f));
            ProcMesh.Prim(PrimitiveType.Cylinder, "PadRing", Pad, new Vector3(0, 0.52f, 0), new Vector3(7f, 0.02f, 7f), R.Emissive(Color.black, new Color(2f, 0.7f, 0.2f)), null, false);
            ProcMesh.Prim(PrimitiveType.Cylinder, "Flame", Pad, new Vector3(0, 0.53f, 0), new Vector3(6.5f, 0.02f, 6.5f), R.Lit(new Color(0.08f, 0.07f, 0.07f), 0.2f), null, false);
            var tower = new GameObject("Tower").transform; tower.SetParent(Pad, false); tower.localPosition = new Vector3(5.6f, 0, 0);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2) * 1.6f - 0.8f, z = (i / 2) * 1.6f - 0.8f;
                ProcMesh.Prim(PrimitiveType.Cube, "Leg", tower, new Vector3(x, 8, z), new Vector3(0.18f, 16, 0.18f), R.Orange);
            }
            for (int k = 0; k < 8; k++)
            {
                ProcMesh.Prim(PrimitiveType.Cube, "Brace", tower, new Vector3(0, 1 + k * 2, -0.8f), new Vector3(1.8f, 0.1f, 0.1f), R.HullDark, new Vector3(0, 0, k % 2 == 0 ? 35 : -35), false);
                ProcMesh.Prim(PrimitiveType.Cube, "Brace", tower, new Vector3(0, 1 + k * 2, 0.8f), new Vector3(1.8f, 0.1f, 0.1f), R.HullDark, new Vector3(0, 0, k % 2 == 0 ? -35 : 35), false);
            }
            var tl = new GameObject("TowerLight").AddComponent<Light>(); tl.transform.SetParent(tower, false); tl.transform.localPosition = new Vector3(0, 16.5f, 0);
            tl.type = LightType.Point; tl.range = 12; tl.intensity = 4; tl.color = new Color(1, 0.2f, 0.15f); tl.gameObject.AddComponent<Blink>();

            bool sf = R.Model("SF_Rocket");
            RocketModel = sf ? "SF_Rocket" : "Saturn_V";
            var rocket = Util.PlaceUpright(RocketModel, c + Vector3.up * 0.55f, 24, Pad);
            Rocket = rocket.transform;
            RocketUse = Interactable.Add(rocket, "Repair", "Escape Rocket", null, 1.0f); RocketUse.Range = 9; RocketUse.enabled = false;
            var console = new GameObject("Console").transform; console.SetParent(Pad, false); console.localPosition = new Vector3(-7f, 0.5f, -3.5f);
            console.localRotation = Quaternion.Euler(0, 60, 0);
            ProcMesh.Prim(PrimitiveType.Cube, "Desk", console, new Vector3(0, 0.55f, 0), new Vector3(1.6f, 1.1f, 0.8f), R.Panel);
            ProcMesh.Prim(PrimitiveType.Cube, "Screen", console, new Vector3(0, 1.35f, 0.1f), new Vector3(1.4f, 0.6f, 0.06f), R.Emissive(Color.black, new Color(0.3f, 1.6f, 0.7f)), new Vector3(-20, 0, 0));
            LaunchConsole = Interactable.Add(console.gameObject, "Start", "Launch Countdown", null); LaunchConsole.enabled = false;
        }

        // ------------------------------------------------------------------
        public static string RocketModel = "SF_Rocket";
        public static List<Renderer>[] PadBins;
        static readonly Dictionary<Renderer, Material[]> padMats = new Dictionary<Renderer, Material[]>();
        public static readonly string[] SectionNames = { "Engines & Boosters", "Main Body", "Upper Stage" };

        /// <summary>Split an upright rocket into 3 sections (bottom / middle / top) by renderer height.</summary>
        public static List<Renderer>[] Bins(GameObject root)
        {
            var bins = new[] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
            var b = Util.Bounds(root);
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                float k = (r.bounds.center.y - b.min.y) / Mathf.Max(0.01f, b.size.y);
                bins[k < 0.35f ? 0 : k < 0.55f ? 1 : 2].Add(r);
            }
            // never leave a bin empty: borrow the closest renderer
            for (int i = 0; i < 3; i++) if (bins[i].Count == 0) { int j = i == 0 ? 1 : i - 1; if (bins[j].Count > 1) { bins[i].Add(bins[j][bins[j].Count - 1]); bins[j].RemoveAt(bins[j].Count - 1); } }
            return bins;
        }

        /// <summary>A copy of the rocket showing only one section, centred on its own origin (pivot = section centre).</summary>
        public static GameObject SectionCopy(int bin, float height, Transform parent)
        {
            var holder = new GameObject("Section" + bin); if (parent) holder.transform.SetParent(parent, false);
            var full = Util.PlaceUpright(RocketModel, Vector3.zero, height, null);
            Object.DestroyImmediate(full.GetComponent<BoxCollider>());
            var bins = Bins(full);
            for (int j = 0; j < 3; j++) if (j != bin) foreach (var r in bins[j]) r.enabled = false;
            var c = Util.Bounds(full).center;
            full.transform.SetParent(holder.transform, true);
            full.transform.localPosition = -c; full.transform.localRotation = Quaternion.identity;
            return holder;
        }

        public static void BuildSection(int i)
        {
            if (PadBins == null) return;
            foreach (var r in PadBins[i]) if (padMats.TryGetValue(r, out var m)) r.sharedMaterials = m;
            var b = new Bounds(); bool init = false;
            foreach (var r in PadBins[i]) { if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds); }
            FX.BurstSparks(b.center, new Color(1f, 0.7f, 0.3f), 80);
        }

        static void BuildParts()
        {
            // the pad rocket starts as a hologram; each recovered section turns real
            PadBins = Bins(Rocket.gameObject);
            var ghost = R.Holo(new Color(0.3f, 0.9f, 1f), 1.2f, 0.1f);
            padMats.Clear();
            foreach (var bin in PadBins) foreach (var r in bin) { padMats[r] = r.sharedMaterials; var gm = new Material[r.sharedMaterials.Length]; for (int k = 0; k < gm.Length; k++) gm[k] = ghost; r.sharedMaterials = gm; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }

            // 3 details from the crashed ship, scattered across Mars. Each one builds one section of the new rocket.
            Parts = new[]
            {
                new RocketPart { Id = "ENGINE", Name = "Engine Nozzle", Icon = "rocket", Color = new Color(1f, 0.45f, 0.3f), Puzzle = 0, Section = 0,
                    Path = new[] { new Vector2(-100, 86), new Vector2(-118, 102) } },
                new RocketPart { Id = "VALVE", Name = "Fuel Valve", Icon = "power", Color = new Color(1f, 0.75f, 0.3f), Puzzle = 1, Section = 1,
                    Path = new[] { new Vector2(100, 80), new Vector2(116, 96) } },
                new RocketPart { Id = "COMPUTER", Name = "Flight Computer", Icon = "data", Color = new Color(0.45f, 1f, 0.6f), Puzzle = 2, Section = 2,
                    Path = new[] { new Vector2(102, -100), new Vector2(110, -124) } },
            };
            // a different map every game: 3 of 6 landing spots, chosen at random
            var spots = new List<Vector2> { new Vector2(-118, 102), new Vector2(116, 96), new Vector2(110, -124), new Vector2(-110, -116), new Vector2(-146, -8), new Vector2(20, 150) };
            for (int i = spots.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (spots[i], spots[j]) = (spots[j], spots[i]); }
            for (int i = 0; i < Parts.Length; i++) { var fin = spots[i]; Parts[i].Path = new[] { fin - fin.normalized * 22f, fin }; }
            var parent = new GameObject("Parts").transform; parent.SetParent(Root.transform);
            foreach (var p in Parts)
            {
                var g = new GameObject("Part_" + p.Id); g.transform.SetParent(parent);
                g.transform.position = G(p.Path[0], 0.4f);
                var model = new GameObject("Model").transform; model.SetParent(g.transform, false);
                PartMesh(p.Id, model);
                model.localScale = Vector3.one * 1.4f;
                var beam = ProcMesh.Prim(PrimitiveType.Cylinder, "Beacon", g.transform, new Vector3(0, 60, 0), new Vector3(0.8f, 60, 0.8f), R.Holo(p.Color, 1.2f, 0.25f, 1f), null, false);
                beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p.Beam = beam;
                var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(g.transform, false); l.transform.localPosition = new Vector3(0, 1.5f, 0);
                l.type = LightType.Point; l.range = 9; l.intensity = 4; l.color = p.Color; p.Light = l;
                FX.Smoke(g.transform.position + Vector3.up * 0.5f, 0.5f, g.transform, 0.12f);
                p.Use = Interactable.Add(g, "Solve puzzle to unlock", p.Name, null); p.Use.Range = 5f;
                p.Go = g;
                g.SetActive(false);
            }
        }

        static void PartMesh(string id, Transform t, Material over = null)
        {
            Material M(Material m) => over ? over : m;
            switch (id)
            {
                case "VALVE":
                    ProcMesh.Prim(PrimitiveType.Cylinder, "Body", t, new Vector3(0, 0.35f, 0), new Vector3(0.45f, 0.35f, 0.45f), M(R.HullDark), null, false);
                    ProcMesh.Prim(PrimitiveType.Cylinder, "Pipe", t, new Vector3(0, 0.35f, 0), new Vector3(0.22f, 0.7f, 0.22f), M(R.Hull), new Vector3(0, 0, 90), false);
                    ProcMesh.Prim(PrimitiveType.Cylinder, "Wheel", t, new Vector3(0, 0.78f, 0), new Vector3(0.7f, 0.05f, 0.7f), M(R.Orange), null, false);
                    ProcMesh.Prim(PrimitiveType.Cylinder, "Stem", t, new Vector3(0, 0.7f, 0), new Vector3(0.08f, 0.1f, 0.08f), M(R.Hull), null, false);
                    break;
                case "COMPUTER":
                    ProcMesh.Prim(PrimitiveType.Cube, "Box", t, new Vector3(0, 0.3f, 0), new Vector3(0.9f, 0.55f, 0.6f), M(R.Panel), null, false);
                    ProcMesh.Prim(PrimitiveType.Cube, "Screen", t, new Vector3(0, 0.35f, 0.31f), new Vector3(0.7f, 0.35f, 0.02f), M(R.Emissive(new Color(0.05f, 0.2f, 0.1f), new Color(0.2f, 1.6f, 0.6f))), null, false);
                    ProcMesh.Prim(PrimitiveType.Cube, "Handle", t, new Vector3(0, 0.62f, 0), new Vector3(0.5f, 0.06f, 0.08f), M(R.HullDark), null, false);
                    break;
                case "ANTENNA":
                    ProcMesh.Prim(PrimitiveType.Cylinder, "Mast", t, new Vector3(0, 0.5f, 0), new Vector3(0.08f, 0.5f, 0.08f), M(R.Hull), null, false);
                    var dish = ProcMesh.MeshObj("Dish", ProcMesh.Dome(20, 6, 0), M(R.Lit(new Color(0.9f, 0.9f, 0.92f), 0.6f, 0.3f)), t);
                    dish.transform.localPosition = new Vector3(0, 1.0f, 0); dish.transform.localScale = new Vector3(0.6f, 0.25f, 0.6f); dish.transform.localEulerAngles = new Vector3(-120, 0, 0);
                    ProcMesh.Prim(PrimitiveType.Cylinder, "Base", t, new Vector3(0, 0.06f, 0), new Vector3(0.5f, 0.06f, 0.5f), M(R.HullDark), null, false);
                    break;
                default:
                    var bell = ProcMesh.MeshObj("Bell", ProcMesh.Dome(24, 8, 0), M(R.Lit(new Color(0.35f, 0.3f, 0.28f), 0.7f, 0.9f)), t);
                    bell.transform.localPosition = new Vector3(0, 0.05f, 0); bell.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
                    ProcMesh.Prim(PrimitiveType.Cylinder, "Throat", t, new Vector3(0, 1.0f, 0), new Vector3(0.35f, 0.15f, 0.35f), M(R.HullDark), null, false);
                    ProcMesh.Prim(PrimitiveType.Cylinder, "Ring", t, new Vector3(0, 0.5f, 0), new Vector3(0.95f, 0.03f, 0.95f), M(R.Orange), null, false);
                    break;
            }
        }

        static void BuildRover()
        {
            var site = new GameObject("RoverSite").transform; site.SetParent(Root.transform);
            Util.PlaceModel("Mars_2020_Perseverance_Rover", G(-58, 30), 3.0f, 120, site, false, true);
            Util.PlaceModel("Ingenuity_Mars_Helicopter", G(-52, 35), 1.2f, 30, site, false, true);
        }

        /// <summary>Our drivable crew rover parked next to the shelter, glowing path lights, and 3 blocks of Mars ice
        /// for the bonus mission (NASA orbiters found water ice just under the ground in many places on Mars).</summary>
        static void BuildRoverAndIce()
        {
            var lockT = Bunker.Find("Airlock");
            // path lights from the shelter door
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 3; k++)
                {
                    var wp = lockT.TransformPoint(new Vector3(s * 1.9f, 0, 2.4f + k * 3f)); wp.y = MarsTerrain.H(wp.x, wp.z);
                    var b = Prop("P_Sci_Fi_Bollard_Light", Root.transform, Vector3.zero, 0, 2.4f);
                    if (b) b.transform.position = wp;
                    else
                    {
                        ProcMesh.Prim(PrimitiveType.Cylinder, "Bollard", Root.transform, wp + Vector3.up * 0.45f, new Vector3(0.18f, 0.45f, 0.18f), R.Panel, null, false);
                        ProcMesh.Prim(PrimitiveType.Cylinder, "BollardTop", Root.transform, wp + Vector3.up * 0.92f, new Vector3(0.2f, 0.04f, 0.2f), R.CyanGlow, null, false);
                    }
                }
            var rp = Bunker.TransformPoint(new Vector3(8f, 0, 12f));
            TheRover = Rover.Create(rp, Bunker.eulerAngles.y + 20, Root.transform);

            IceBlocks.Clear();
            var parent = new GameObject("Ice").transform; parent.SetParent(Root.transform);
            var ice = new Color(0.7f, 0.9f, 1f);
            foreach (var xz in IceXZ)
            {
                var g = new GameObject("IceBlock"); g.transform.SetParent(parent); g.transform.position = G(xz);
                var m = Util.PlaceModel("K_rock_crystalsLargeA", G(xz), 2.6f, xz.x * 7, g.transform);
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.materials;
                    foreach (var mm in mats) { if (mm.HasProperty("baseColorFactor")) mm.SetColor("baseColorFactor", ice); if (mm.HasProperty("_BaseColor")) mm.SetColor("_BaseColor", ice); }
                    r.materials = mats;
                }
                if (m.transform.childCount == 0) ProcMesh.Prim(PrimitiveType.Cube, "Ice", g.transform, new Vector3(0, 0.8f, 0), Vector3.one * 1.4f, R.Lit(ice, 0.95f), new Vector3(20, 30, 10), false);
                var ring = ProcMesh.Prim(PrimitiveType.Cylinder, "Ring", g.transform, new Vector3(0, 0.05f, 0), new Vector3(4.5f, 0.01f, 4.5f), R.Holo(new Color(0.5f, 0.85f, 1f), 1.6f, 0.2f), null, false);
                ring.AddComponent<Spin>().Speed = 50;
                var beam = ProcMesh.Prim(PrimitiveType.Cylinder, "Beacon", g.transform, new Vector3(0, 25, 0), new Vector3(0.5f, 25, 0.5f), R.Holo(new Color(0.5f, 0.85f, 1f), 1.2f, 0.2f, 1f), null, false);
                beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(g.transform, false); l.transform.localPosition = Vector3.up * 2f;
                l.type = LightType.Point; l.range = 8; l.intensity = 3; l.color = new Color(0.5f, 0.85f, 1f); l.shadows = LightShadows.None;
                g.SetActive(false);
                IceBlocks.Add(g);
            }
        }

        /// <summary>Dish tower: send a message to Earth. It travels at the speed of light and still needs
        /// several minutes (3-22 min one way, depending on where the planets are). In the game 1 second = 1 minute.</summary>
        static void EarthLink(GameObject tower)
        {
            bool busy = false;
            // replies use the NASA Mars fact sheet numbers (same values as the MARS & MOON research site)
            string[] replies =
            {
                $"Great job, Junior Astronaut! A day on Mars (a sol) lasts {MarsFacts.SolLength}.",
                $"Message received! Mars air is {MarsFacts.CO2}% carbon dioxide, so we can't breathe it.",
                "Hello from Earth! On Mars you weigh only 38% of what you weigh at home.",
                $"We hear you! Mars is cold: about {MarsFacts.MeanTempC} °C on average.",
                $"Hi from Mission Control! Phobos goes around Mars in only {MarsFacts.PhobosHours} hours.",
                $"Greetings! One Mars year lasts {MarsFacts.YearDays:0} Earth days.",
            };
            var link = Interactable.Add(tower, "Send a message to", "EARTH (radio)", null);
            link.Range = 4f;
            link.OnUse = () =>
            {
                if (busy) { if (Game.Hud) Game.Hud.Toast("STILL WAITING…", "Your message is still flying through space!", "clock", HC.Amber); return; }
                busy = true;
                Game.Story.StartCoroutine(Co());
            };
            IEnumerator Co()
            {
                // the REAL delay today: Earth–Mars distance from NASA/JPL Horizons, divided by the speed of light
                var nl = NasaLive.I;
                double km = nl ? nl.DistanceKm : 225e6;
                float minutes = (float)(km / MarsFacts.LightKmPerSec / 60.0);
                string src = nl ? nl.SourceText : "NASA/JPL";
                if (Game.Hud)
                {
                    Game.Hud.Toast("MESSAGE SENT TO EARTH!", $"Today Earth is {km / 1e6:0.0} million km away ({src}). Even light needs {minutes:0.0} minutes!", "clock", HC.Cyan);
                    Game.Hud.Say($"Our message needs {minutes:0} minutes to reach Earth, and the answer needs {minutes:0} minutes to come back. In the game 1 second = 1 minute. Let's wait!");
                }
                yield return new WaitForSeconds(minutes * 2f); // there and back: 1 game second = 1 real minute
                if (Game.Hud) Game.Hud.Toast("REPLY FROM EARTH", replies[UnityEngine.Random.Range(0, replies.Length)], "clock", HC.Green);
                AstroPass.Award("earthcall", "Called Earth", 1);
                busy = false;
            }
        }

        // ------------------------------------------------------------------ MARS BASE
        /// <summary>The MARS BASE, built from clean artist-made pieces (Quaternius Ultimate Space Kit + Kenney Space Kit, both CC0):
        /// a habitat hub, a walk-in PLANTATION dome (press E at its door to go inside), a lab with the weather station,
        /// the dish that calls Earth and a solar field. Every building has a big floating name and teaches something.</summary>
        static void BuildMarsBase()
        {
            if (!R.Model("Q_Base_Large")) return;
            var root = new GameObject("MarsBase").transform; root.SetParent(Root.transform); MarsBase = root;
            root.position = G(BaseXZ);
            var toBunker = new Vector3(BunkerXZ.x - BaseXZ.x, 0, BunkerXZ.y - BaseXZ.y);
            root.rotation = Quaternion.LookRotation(toBunker); // the front (+z) faces the shelter

            // one kit piece, standing on the ground at (x, z) in base space; returns the piece
            GameObject Kit(string model, float x, float z, float yaw, float scale, bool solid = true, float lift = 0)
            {
                var prefab = R.Model(model); if (!prefab) return null;
                var w = root.TransformPoint(new Vector3(x, 0, z)); w.y = MarsTerrain.H(w.x, w.z) - 0.15f + lift;
                var g = Object.Instantiate(prefab, root, true); g.name = model;
                g.transform.position = w; g.transform.rotation = root.rotation * Quaternion.Euler(0, yaw, 0); g.transform.localScale = Vector3.one * scale;
                foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (solid)
                {
                    var b = Util.Bounds(g); var bc = g.AddComponent<BoxCollider>();
                    bc.center = g.transform.InverseTransformPoint(b.center);
                    bc.size = new Vector3(b.size.x, b.size.y, b.size.z) / scale * 0.94f;
                }
                return g;
            }

            Interactable FrontDoor(GameObject b, string model, float sc, string label, out Vector3 outside)
            {
                var fl = ModelRoom.FrontDoorLocal(model);
                var z = new GameObject(model + "Door"); z.transform.SetParent(b.transform, false);
                z.transform.localPosition = fl; z.transform.localScale = Vector3.one / sc;
                { var zb = z.AddComponent<BoxCollider>(); zb.size = new Vector3(2.6f, 3f, 1.4f); zb.isTrigger = true; } // press E, but never blocks you
                outside = b.transform.TransformPoint(fl + new Vector3(0, 0, 3f / sc)); outside.y = MarsTerrain.H(outside.x, outside.z) + 0.1f;
                var it = Interactable.Add(z, "Go into", label, null); it.Range = 5f; return it;
            }

            // 1. HABITAT (living quarters): the team's realistic model if present, walls from its real shape
            GameObject hub;
            if (R.Model("P_HabitatOut"))
            {
                hub = Kit("P_HabitatOut", 0, 0, 0, 16f, false);
                ModelRoom.OutsideWalls(hub, "HabitatOut", 16f);
                HabitatDoor = FrontDoor(hub, "HabitatOut", 16f, "HABITAT (bed, kitchen, computer)", out HabitatOutside);
            }
            else hub = Kit("Q_Base_Large", 0, 0, 0, 1.5f);
            // 2. PLANTATION: big geodesic glass dome with a real door (press E to walk in)
            GameObject dome;
            if (R.Model("P_PlantationOut"))
            {
                // the team's realistic Tripo dome (Assets/Paid): 1 unit wide, door on its +z side
                const float DS = 18f;
                dome = Kit("P_PlantationOut", 20, -3, 0, DS, false);
                ModelRoom.OutsideWalls(dome, "PlantationOut", DS); // solid exactly where the dome is
                var doorZone = new GameObject("PlantationDoor"); doorZone.transform.SetParent(dome.transform, false);
                doorZone.transform.localPosition = new Vector3(0, 0.07f, 0.5f); doorZone.transform.localScale = Vector3.one / DS;
                var dbc = doorZone.AddComponent<BoxCollider>(); dbc.size = new Vector3(2.2f, 2.8f, 1.0f); dbc.isTrigger = true;
                GreenhouseDoorPanel = null;
                GreenhouseDoor = Interactable.Add(doorZone, "Go into", "PLANTATION (grow food)", null); GreenhouseDoor.Range = 5f;
                GreenhouseOutside = root.TransformPoint(new Vector3(20, 0, -3 + 0.5f * DS + 2.5f));
            }
            else
            {
                dome = Kit("Q_GeodesicDome", 20, -3, 0, 1.9f);
                Kit("Q_Connector", 10.8f, -1.5f, 0, 1.5f, false);
                // the dome's entrance: a small white airlock room with a sliding door (like the shelter's)
                var vest = new GameObject("PlantationAirlock").transform; vest.SetParent(root, false);
                { var w = root.TransformPoint(new Vector3(20, 0, 6.6f)); w.y = MarsTerrain.H(w.x, w.z) - 0.1f; vest.position = w; vest.rotation = root.rotation; }
                ProcMesh.Prim(PrimitiveType.Cube, "Body", vest, new Vector3(0, 1.7f, -0.6f), new Vector3(3.8f, 3.4f, 3.4f), R.Panel);
                ProcMesh.Prim(PrimitiveType.Cube, "Roof", vest, new Vector3(0, 3.48f, -0.6f), new Vector3(4.0f, 0.16f, 3.6f), R.Orange, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "Stripe", vest, new Vector3(0, 0.35f, 1.12f), new Vector3(3.8f, 0.2f, 0.04f), R.Orange, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "Doorway", vest, new Vector3(0, 1.25f, 1.11f), new Vector3(1.4f, 2.3f, 0.02f), R.Emissive(new Color(0.3f, 0.5f, 0.3f), new Color(0.6f, 1.6f, 0.6f)), null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "Lamp", vest, new Vector3(0, 2.75f, 1.15f), new Vector3(1.3f, 0.1f, 0.06f), R.Emissive(Color.black, new Color(0.4f, 2.6f, 0.8f)), null, false);
                var plSign = World3DText(vest, new Vector3(0, 3.05f, 1.16f), "PLANTATION", 2.6f, new Color(0.55f, 1f, 0.55f)); plSign.transform.localRotation = Quaternion.Euler(0, 180, 0);
                GreenhouseDoorPanel = DoorPanel(vest, new Vector3(0, 1.25f, 1.2f), new Vector3(1.5f, 2.4f, 0.12f));
                GreenhouseDoor = Interactable.Add(GreenhouseDoorPanel.gameObject, "Go into", "PLANTATION (grow food)", null); GreenhouseDoor.Range = 5f;
                GreenhouseOutside = root.TransformPoint(new Vector3(20, 0, 10.5f)); GreenhouseOutside.y = MarsTerrain.H(GreenhouseOutside.x, GreenhouseOutside.z) + 0.1f;
            }
            GreenhouseOutside.y = MarsTerrain.H(GreenhouseOutside.x, GreenhouseOutside.z) + 0.1f;
            GreenhouseOutsideYaw = root.eulerAngles.y;
            // 3. LAB with the WEATHER STATION radar on its roof
            GameObject lab;
            if (R.Model("P_WeatherOut"))
            {
                lab = Kit("P_WeatherOut", -18, -2, 0, 14f, false);
                ModelRoom.OutsideWalls(lab, "WeatherOut", 14f);
                WeatherDoor = FrontDoor(lab, "WeatherOut", 14f, "WEATHER LAB (weather computer)", out WeatherOutside);
            }
            else
            {
                lab = Kit("Q_House_Cylinder", -18, -2, 0, 1.4f);
                Kit("Q_Roof_Radar", -18, -2, 0, 1.4f, false);
                Kit("Q_Connector", -10.6f, -1.5f, 0, 1.5f, false);
            }
            // 4. DISH that talks to Earth, on a metal tower

            // 5. SOLAR FIELD (power for the base)
            GameObject panel = null;
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 4; i++)
                {
                    var p = Kit("Q_SolarPanel_Ground", 6 + i * 3.3f, -15 - row * 4.2f, 0, 2.2f);
                    if (!panel) panel = p;
                }
            Kit("K_machine_generatorLarge", 20, -16, 90, 3.2f);
            Kit("K_barrels", -24, 6, 30, 3f);
            Kit("K_machine_barrelLarge", 8, 7, 0, 2.6f);
            // a few big rocks around the edge, so the base sits in a real landscape
            Kit("Q_Rock_Large_1", -30, -14, 40, 2.2f);
            Kit("Q_Rock_Large_2", 33, 4, 200, 2.0f);
            Kit("Q_Rock_Large_1", 28, -22, 120, 1.6f);

            // what each building does (press E)
            if (hub && !HabitatDoor) Habitat(hub);
            if (lab && !WeatherDoor) WeatherStation(lab);

            if (panel) SolarInfo(panel);

            // big floating names + NOVA's guided tour stops
            BaseStops.Clear();
            void Stop(Vector3 local, string name, string what, string line, Color c)
            {
                var w = root.TransformPoint(local); w.y = MarsTerrain.H(w.x, w.z) + local.y;
                Label(Root.transform, w, name, what, c);
                BaseStops.Add((w, line));
            }
            Stop(new Vector3(0, HabitatDoor ? 17.5f : 11, 0), "HABITAT", "home: eat, sleep, fresh air", "This is the HABITAT, the home of the base. Inside: beds, a kitchen and the navigation computer that tracks Earth and Mars.", HC.Cyan);
            Stop(new Vector3(20, 13.5f, -3), "PLANTATION", "go inside to grow potatoes", "The glass dome is the PLANTATION. Sunlight comes through the glass. Go inside to plant, water and harvest potatoes!", HC.Green);
            Stop(new Vector3(-18, WeatherDoor ? 17 : 11, -2), "WEATHER LAB", "real NASA weather", "This is the WEATHER LAB. Inside, scientists study real Mars weather measured by NASA's InSight lander and make forecasts.", HC.Amber);
            Stop(new Vector3(11, 6, -17), "SOLAR PANELS", "power from the Sun", "SOLAR PANELS make our power. Mars gets only about half the sunlight Earth gets, so we need a lot of them.", HC.Amber);
            var title = World3DText(root, new Vector3(0, 24, 0), "MARS BASE", 16, new Color(1f, 0.8f, 0.35f));
            title.gameObject.AddComponent<Billboard>();
        }

        public static Interactable HabitatDoor, WeatherDoor; public static Vector3 HabitatOutside, WeatherOutside;
        public static ModelRoom HabitatRoom, WeatherRoom;
        public static Interactable Bunk, Kitchen, Locker, NavComputer, WeatherComputer, Racks;

        /// <summary>The walk-in HABITAT and WEATHER LAB rooms, made from the team's Tripo interiors (Assets/Paid).
        /// Press-E zones sit on the model's furniture (positions measured from the model's height map).</summary>
        static void BuildRooms()
        {
            HabitatRoom = ModelRoom.Build("HabitatIn", new Vector3(0, -260, 0), 8.5f, new Vector2(0.45f, 0.92f), new Color(0.92f, 0.93f, 0.96f), new Color(1f, 0.95f, 0.88f));
            if (HabitatRoom != null)
            {
                Bunk = HabitatRoom.Zone("Beds", 0.195f, 0.5f, 0.23f, 0.85f, 1.3f, "Sleep in", "your bunk bed (1 sol passes)", null);
                Kitchen = HabitatRoom.Zone("Kitchen", 0.735f, 0.205f, 0.25f, 0.29f, 1.6f, "Eat & drink at", "the kitchen", null);
                Locker = HabitatRoom.Zone("Lockers", 0.76f, 0.88f, 0.2f, 0.12f, 2.5f, "Check", "spacesuit locker", null);
                NavComputer = Terminal(HabitatRoom, 0.47f, 0.33f, "NAVIGATION COMPUTER (Earth & Mars)");
            }
            WeatherRoom = ModelRoom.Build("WeatherIn", new Vector3(0, -340, 0), 7.5f, new Vector2(0.45f, 0.86f), new Color(0.9f, 0.94f, 1f), new Color(0.92f, 0.97f, 1f));
            if (WeatherRoom != null)
            {
                WeatherComputer = WeatherRoom.Zone("WeatherDesk", 0.17f, 0.26f, 0.18f, 0.27f, 1.5f, "Use", "WEATHER COMPUTER (real NASA data)", null);
                Racks = WeatherRoom.Zone("Racks", 0.76f, 0.69f, 0.19f, 0.42f, 2.2f, "Read", "the instrument racks", null);
                // the RADIO TO EARTH lives in the weather lab now (real light delay from NASA JPL)
                var radio = Terminal(WeatherRoom, 0.5f, 0.4f, "RADIO TO EARTH");
                EarthLink(radio.gameObject);
            }
        }

        /// <summary>A computer terminal on free floor: a desk with a glowing screen you press E on.</summary>
        static Interactable Terminal(ModelRoom room, float fx, float fz, string label)
        {
            var spot = room.FreeSpot(fx, fz);
            var g = new GameObject("Terminal"); g.transform.SetParent(room.T, false); g.transform.localPosition = spot;
            var desk = R.Model("K_desk_computer");
            if (desk) { var d = Object.Instantiate(desk, g.transform, false); d.transform.localScale = Vector3.one * 1.6f; }
            else ProcMesh.Prim(PrimitiveType.Cube, "Desk", g.transform, new Vector3(0, 0.4f, 0), new Vector3(1.4f, 0.8f, 0.7f), R.Panel);
            var holo = ProcMesh.Prim(PrimitiveType.Cylinder, "Holo", g.transform, new Vector3(0, 1.45f, 0), new Vector3(0.9f, 0.01f, 0.9f), R.Holo(new Color(0.3f, 0.9f, 1f), 1.6f, 0.2f), null, false);
            holo.AddComponent<Spin>().Speed = 40;
            var earth = ProcMesh.Prim(PrimitiveType.Sphere, "HoloEarth", g.transform, new Vector3(-0.2f, 1.7f, 0), Vector3.one * 0.22f, R.Emissive(new Color(0.1f, 0.3f, 0.6f), new Color(0.3f, 0.7f, 1.6f)), null, false);
            var mars = ProcMesh.Prim(PrimitiveType.Sphere, "HoloMars", g.transform, new Vector3(0.3f, 1.65f, 0), Vector3.one * 0.15f, R.Emissive(new Color(0.6f, 0.25f, 0.1f), new Color(1.6f, 0.6f, 0.3f)), null, false);
            var l = new GameObject("Glow").AddComponent<Light>(); l.transform.SetParent(g.transform, false); l.transform.localPosition = new Vector3(0, 1.8f, 0);
            l.type = LightType.Point; l.range = 4; l.intensity = 1.6f; l.color = new Color(0.4f, 0.85f, 1f); l.shadows = LightShadows.None;
            var bc = g.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.9f, 0); bc.size = new Vector3(1.6f, 1.8f, 1.0f);
            var it = Interactable.Add(g, "Use", label, null, 0, false); it.Range = 4f; return it;
        }

        public static Interactable GreenhouseDoor; public static Transform GreenhouseDoorPanel; public static Vector3 GreenhouseOutside; public static float GreenhouseOutsideYaw;

        /// <summary>Big floating names over the main places, so kids always know where they are.</summary>
        static void PlaceLabels()
        {
            Label(Root.transform, Bunker.TransformPoint(new Vector3(0, 7.5f, -1.5f)), "YOUR SHELTER", "underground: safe from space rays", HC.Green);
            if (TheRover) Label(TheRover.transform, TheRover.transform.position + Vector3.up * 4.5f, "ROVER", "press E to drive", HC.Amber);
            Label(Root.transform, Pad.position + Vector3.up * 28f, "LAUNCH PAD", "build your new rocket here", HC.Amber);
            Label(Root.transform, G(CrashXZ, 14f), "CRASHED SHIP", "the air + water machines are here", HC.Red);
        }

        /// <summary>Points NOVA shows on the base tour: where to look + what NOVA says.</summary>
        public static readonly List<(Vector3 pos, string line)> BaseStops = new List<(Vector3, string)>();

        /// <summary>A big floating name (and a short "what it is") above a place. Always faces you; only shows when you are near.</summary>
        public static void Label(Transform parent, Vector3 world, string name, string what, Color c)
        {
            var g = new GameObject("Label_" + name); g.transform.SetParent(parent, true); g.transform.position = world;
            var t = World3DText(g.transform, Vector3.zero, name, 11, c); t.fontStyle = TMPro.FontStyles.Bold;
            t.outlineWidth = 0.25f; t.outlineColor = new Color32(0, 0, 0, 200);
            var s2 = World3DText(g.transform, new Vector3(0, -1.9f, 0), what, 7, Color.white);
            s2.outlineWidth = 0.25f; s2.outlineColor = new Color32(0, 0, 0, 200);
            g.AddComponent<Billboard>(); g.AddComponent<NearOnly>();
        }

        /// <summary>Farm: plant potatoes, wait, eat (plants make food AND oxygen; astronauts must grow their own food).</summary>
        static void BaseFarm(GameObject zone, string label)
        {
            bool planted = false; float ready = 0;
            Interactable use = null;
            use = Interactable.Add(zone, "Plant potatoes in", label, () =>
            {
                var suit = Game.Suit; var hud = Game.Hud;
                if (!planted)
                {
                    planted = true; ready = Time.time + 20; use.Verb = "Eat from";
                    if (hud) hud.Toast("POTATOES PLANTED!", "Plants make FOOD and OXYGEN. On Mars, astronauts must grow their own food. Come back in 20 seconds!", "food", HC.Green);
                    AstroPass.Award("farm", "Mars farmer", 1);
                }
                else if (Time.time >= ready)
                {
                    if (suit) suit.Drink(25); ready = Time.time + 60;
                    if (hud) hud.Toast("YUM! FRESH POTATOES", "Food gives you energy. Life support +25", "food", HC.Green);
                }
                else if (hud) hud.Toast("STILL GROWING…", $"Plants need time. Ready in {Mathf.CeilToInt(ready - Time.time)} seconds!", "food", HC.Amber);
            });
            use.Range = 6f;
        }

        /// <summary>Weather station: the REAL Mars weather measured by NASA's InSight lander.</summary>
        static void WeatherStation(GameObject zone)
        {
            var use = Interactable.Add(zone, "Check", "Weather station", () =>
            {
                var api = NasaApi.I; var hud = Game.Hud; if (!api || !hud) return;
                var w = api.LastSol;
                hud.Toast("MARS WEATHER (NASA InSight)", $"Night {w.tempMinC:0} °C, day {w.tempMaxC:0} °C. Air pressure {w.pressurePa:0} Pa: less than 1% of Earth's!", "sun", HC.Amber);
                hud.Say($"This is real weather that NASA's InSight lander measured on Mars (sol {w.sol}). The air is so thin that water boils away and ice turns straight into gas!", 6);
                AstroPass.Award("weather", "Weather watcher", 1);
            });
            use.Range = 6f;
        }

        /// <summary>Habitat dome: a safe room with fresh air.</summary>
        static void Habitat(GameObject zone)
        {
            float next = 0;
            var use = Interactable.Add(zone, "Rest in", "Habitat dome", () =>
            {
                var suit = Game.Suit; var hud = Game.Hud;
                if (Time.time < next) { if (hud) hud.Toast("THE AIR IS REFILLING", "Come back in a minute.", "air", HC.Amber); return; }
                next = Time.time + 60; if (suit) suit.Drink(30);
                if (hud) hud.Toast("FRESH AIR!", "The habitat keeps air at the right pressure, like home. Life support +30", "air", HC.Cyan);
            });
            use.Range = 6f;
        }

        /// <summary>Solar field: explains why power is precious on Mars.</summary>
        public static Transform SolarPanel;
        static void SolarInfo(GameObject panel)
        {
            if (!panel) return;
            SolarPanel = panel.transform;
            // the solar panels open the NIGHT POWER PLANNER (power budget mission)
            var i = Interactable.Add(panel, "Plan the night power at", "SOLAR PANELS", () => { if (Computers.I) Computers.I.OpenPower(); });
            i.Range = 5f;
        }


        // ------------------------------------------------------------------
        static void BuildInterior()
        {
            Interior = new GameObject("BunkerInterior");
            var t = Interior.transform; t.position = InteriorOrigin;
            float W = 11, D = 9, Hh = 3.6f;
            var wallM = R.Lit(new Color(0.74f, 0.76f, 0.8f), 0.5f, 0.05f); var floorM = R.LitTex("metal_plate_diff", "metal_plate_nor", new Color(0.3f, 0.31f, 0.34f), 9, 0.5f, 0.6f); var ceilM = R.Lit(new Color(0.2f, 0.21f, 0.23f), 0.4f, 0.2f);
            ProcMesh.Prim(PrimitiveType.Cube, "Floor", t, new Vector3(0, -0.1f, 0), new Vector3(W, 0.2f, D), floorM);
            ProcMesh.Prim(PrimitiveType.Cube, "Ceiling", t, new Vector3(0, Hh + 0.1f, 0), new Vector3(W, 0.2f, D), ceilM);
            ProcMesh.Prim(PrimitiveType.Cube, "WallN", t, new Vector3(0, Hh / 2, D / 2), new Vector3(W, Hh, 0.2f), wallM);
            ProcMesh.Prim(PrimitiveType.Cube, "WallS", t, new Vector3(0, Hh / 2, -D / 2), new Vector3(W, Hh, 0.2f), wallM);
            ProcMesh.Prim(PrimitiveType.Cube, "WallE", t, new Vector3(W / 2, Hh / 2, 0), new Vector3(0.2f, Hh, D), wallM);
            ProcMesh.Prim(PrimitiveType.Cube, "WallW", t, new Vector3(-W / 2, Hh / 2, 0), new Vector3(0.2f, Hh, D), wallM);
            for (int i = -2; i <= 2; i++)
            {
                ProcMesh.Prim(PrimitiveType.Cube, "Rib", t, new Vector3(i * 2.2f, Hh - 0.05f, 0), new Vector3(0.25f, 0.18f, D), R.HullDark, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "RibN", t, new Vector3(i * 2.2f, Hh / 2, D / 2 - 0.12f), new Vector3(0.25f, Hh, 0.08f), R.HullDark, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "RibS", t, new Vector3(i * 2.2f, Hh / 2, -D / 2 + 0.12f), new Vector3(0.25f, Hh, 0.08f), R.HullDark, null, false);
            }
            ProcMesh.Prim(PrimitiveType.Cube, "FloorStripe", t, new Vector3(0, 0.005f, 0), new Vector3(W - 1, 0.01f, 0.25f), R.Orange, null, false);

            // ---- LIGHTS: bright and warm once the power is on (the power-up mission turns them on) ----
            powerLights.Clear(); powerGlows.Clear(); InteriorLights.Clear();
            var strip = R.Emissive(Color.black, new Color(2.2f, 2.3f, 2.5f)); Glow(strip);
            bool paidLamps = R.Model("P_P_Sci_Fi_Corridor_Ceiling_Strip");
            for (int i = -1; i <= 1; i += 2)
            {
                if (!paidLamps) ProcMesh.Prim(PrimitiveType.Cube, "Strip", t, new Vector3(i * 1.1f, Hh - 0.02f, 0), new Vector3(0.12f, 0.03f, D - 1), strip, null, false);
                else for (int k = -1; k <= 1; k++) Glow(Prop("P_Sci_Fi_Corridor_Ceiling_Strip", t, new Vector3(i * 1.1f, Hh, k * 2.6f), 90, 1.3f));
            }
            for (int i = -1; i <= 1; i += 2)
                for (int k = -1; k <= 1; k += 2)
                {
                    var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(t, false); l.transform.localPosition = new Vector3(i * 2.6f, Hh - 0.5f, k * 1.9f);
                    l.type = LightType.Point; l.range = 11; l.intensity = 3.2f; l.color = new Color(1f, 0.96f, 0.9f); l.shadows = LightShadows.None;
                    InteriorLights.Add(l); powerLights.Add((l, l.intensity));
                }
            // wall lamps (one per wall)
            Glow(Prop("P_Sci_Fi_Patterned_Wall_Light", t, new Vector3(-W / 2 + 0.12f, 2.3f, -1.2f), 90, 1.4f));
            Glow(Prop("P_Sci_Fi_Patterned_Wall_Light", t, new Vector3(W / 2 - 0.12f, 2.4f, 0), -90, 1.4f));
            Glow(Prop("P_Sci_Fi_Patterned_Wall_Light", t, new Vector3(3.7f, 2.3f, D / 2 - 0.12f), 180, 1.4f));
            Glow(Prop("P_Sci_Fi_Patterned_Wall_Light", t, new Vector3(0.6f, 2.3f, -D / 2 + 0.12f), 0, 1.4f));
            // dim orange emergency light: all you have while the power is off
            emergency = new GameObject("Emergency").AddComponent<Light>(); emergency.transform.SetParent(t, false); emergency.transform.localPosition = new Vector3(-2f, Hh - 0.4f, 1.5f);
            emergency.type = LightType.Point; emergency.range = 12; emergency.intensity = 0; emergency.color = new Color(1f, 0.55f, 0.25f); emergency.shadows = LightShadows.None;
            AlarmLight = new GameObject("Alarm").AddComponent<Light>(); AlarmLight.transform.SetParent(t, false); AlarmLight.transform.localPosition = new Vector3(0, Hh - 0.3f, -2);
            AlarmLight.type = LightType.Point; AlarmLight.range = 12; AlarmLight.intensity = 0; AlarmLight.color = new Color(1f, 0.15f, 0.08f);
            AlarmLight.gameObject.AddComponent<Blink>().Rate = 1.2f;

            // ---- POWER CORNER (north wall): battery + cable reel + main switch ----
            var bat = Prop("P_Sci_Fi_Battery_Unit", t, new Vector3(-3.0f, 0, D / 2 - 0.45f), 180, 1.8f);
            if (!bat)
            {
                bat = ProcMesh.Prim(PrimitiveType.Cube, "Battery", t, new Vector3(-3.0f, 0.38f, D / 2 - 0.45f), new Vector3(1.1f, 0.75f, 0.6f), R.Panel);
                ProcMesh.Prim(PrimitiveType.Cube, "Charge", bat.transform, new Vector3(0, 0.1f, -0.51f), new Vector3(0.7f, 0.25f, 0.02f), R.Emissive(Color.black, new Color(0.3f, 2f, 0.6f)), null, false);
            }
            Glow(bat);
            BatteryPlug = Interactable.Add(bat, "Plug in", "Battery (stores solar power)", null); BatteryPlug.enabled = false;
            PropH("CRM_Reactor_HoseReel", t, new Vector3(-4.0f, 0, D / 2 - 0.5f), 180, 0.9f);
            var sw = PropH("CRM_Reactor_PowerModule", t, new Vector3(-1.4f, 0, D / 2 - 0.5f), 180, 1.8f);
            if (!sw)
            {
                sw = new GameObject("MainSwitch"); sw.transform.SetParent(t, false); sw.transform.localPosition = new Vector3(-1.4f, 0, D / 2 - 0.5f);
                ProcMesh.Prim(PrimitiveType.Cube, "Box", sw.transform, new Vector3(0, 0.9f, 0), new Vector3(1f, 1.8f, 0.6f), R.Panel);
                ProcMesh.Prim(PrimitiveType.Cube, "Lever", sw.transform, new Vector3(0, 1.2f, -0.35f), new Vector3(0.15f, 0.45f, 0.12f), R.Orange, null, false);
            }
            Glow(sw);
            PowerSwitch = Interactable.Add(sw, "Hold E: turn on", "MAIN POWER SWITCH", null, 1.0f); PowerSwitch.enabled = false;
            // the cable appears when you plug it in
            Cable = ProcMesh.Prim(PrimitiveType.Cylinder, "Cable", t, new Vector3(-2.2f, 0.05f, D / 2 - 0.75f), new Vector3(0.07f, 0.8f, 0.07f), R.CyanGlow, new Vector3(0, 0, 90), false);
            Cable.SetActive(false);
            var powerSign = World3DText(t, new Vector3(-2.2f, 2.55f, D / 2 - 0.15f), "POWER", 2.6f, new Color(1f, 0.8f, 0.3f));
            powerSign.transform.localRotation = Quaternion.Euler(0, 0, 0);

            // ---- AIR FILTER (south wall): this is where the oxygen leak happens ----
            var filter = PropH("CRM_Reactor_FilterUnit", t, new Vector3(2.4f, 0, -D / 2 + 0.6f), 0, 1.8f);
            if (!filter) filter = ProcMesh.Prim(PrimitiveType.Cube, "AirFilter", t, new Vector3(2.4f, 0.9f, -D / 2 + 0.55f), new Vector3(1.2f, 1.8f, 0.7f), R.Panel);
            PropH("CRM_Reactor_Canister_01", t, new Vector3(3.7f, 0, -D / 2 + 0.55f), 0, 1.4f);
            PropH("CRM_Reactor_UtilityCabinet_02", t, new Vector3(4.7f, 0, -D / 2 + 0.5f), 0, 1.9f);
            Glow(PropH("CRM_Reactor_Light_03", t, new Vector3(-0.7f, 0, -D / 2 + 0.45f), 0, 1.0f));
            var pipe = ProcMesh.Prim(PrimitiveType.Cylinder, "AirPipe", t, new Vector3(2.4f, 2.7f, -D / 2 + 0.3f), new Vector3(0.22f, 0.9f, 0.22f), R.Hull);
            LeakPos = t.TransformPoint(new Vector3(2.4f, 2.5f, -D / 2 + 0.5f));
            LeakSteam = FX.Mist(LeakPos, t, new Color(0.9f, 0.95f, 1f, 0.9f));
            { var m = LeakSteam.main; m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f); var e = LeakSteam.emission; e.rateOverTime = 45; var s = LeakSteam.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 18; s.radius = 0.05f; LeakSteam.transform.localRotation = Quaternion.Euler(0, 0, 0); LeakSteam.Stop(); }
            LeakValve = Interactable.Add(pipe, "Hold E: patch", "AIR LEAK", null, 1.5f); LeakValve.enabled = false;

            // exit: a CLOSED airlock door; Mars daylight shows behind it when it slides open
            ProcMesh.Prim(PrimitiveType.Cube, "ExitDoorway", t, new Vector3(-3.2f, 1.15f, -D / 2 + 0.12f), new Vector3(1.3f, 2.3f, 0.02f), R.Emissive(new Color(0.9f, 0.55f, 0.35f), new Color(1.6f, 0.85f, 0.5f)), null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "ExitSign", t, new Vector3(-3.2f, 2.55f, -D / 2 + 0.15f), new Vector3(0.7f, 0.18f, 0.05f), R.Emissive(Color.black, new Color(0.3f, 2.2f, 0.9f)), null, false);
            ExitDoorPanel = DoorPanel(t, new Vector3(-3.2f, 1.15f, -D / 2 + 0.24f), new Vector3(1.4f, 2.35f, 0.1f));
            ExitDoor = Interactable.Add(ExitDoorPanel.gameObject, "Go outside", "Airlock", null);
            InteriorSpawn = t.TransformPoint(new Vector3(-3.2f, 0.05f, -D / 2 + 1.4f)); InteriorYaw = 0;

            // MOXIE station (east wall)
            var mox = new GameObject("MoxieStation").transform; mox.SetParent(t, false); mox.localPosition = new Vector3(W / 2 - 1f, 0, 1.6f); mox.localRotation = Quaternion.Euler(0, -90, 0);
            MoxieGhost = StationGhost(mox, holoCyan, "MOXIE");
            MoxieInstalled = new GameObject("Installed"); MoxieInstalled.transform.SetParent(mox, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Body", MoxieInstalled.transform, new Vector3(0, 0.6f, 0), new Vector3(1.3f, 1.2f, 0.9f), R.Panel);
            ProcMesh.Prim(PrimitiveType.Cube, "Label", MoxieInstalled.transform, new Vector3(0, 1.0f, 0.46f), new Vector3(0.8f, 0.14f, 0.02f), R.Orange, null, false);
            for (int i = 0; i < 3; i++)
            {
                ProcMesh.Prim(PrimitiveType.Cylinder, "Tube", MoxieInstalled.transform, new Vector3(-0.4f + i * 0.4f, 1.65f, 0), new Vector3(0.18f, 0.45f, 0.18f), R.CyanGlow, null, false);
            }
            Util.PlaceModel("Orbital_Replacement_Unit", mox.TransformPoint(new Vector3(0, 1.2f, -0.1f)), 0.8f, -90, MoxieInstalled.transform);
            MoxieBubbles = FX.Mist(mox.TransformPoint(new Vector3(0, 2.0f, 0)), MoxieInstalled.transform, new Color(0.4f, 0.8f, 1f));
            MoxieInstalled.SetActive(false);
            MoxieSlot = Interactable.Add(MoxieGhost, "Install", "MOXIE here", null); MoxieSlot.enabled = false;

            // Water station (north-east)
            var wat = new GameObject("WaterStation").transform; wat.SetParent(t, false); wat.localPosition = new Vector3(W / 2 - 1f, 0, -1.6f); wat.localRotation = Quaternion.Euler(0, -90, 0);
            WaterGhost = StationGhost(wat, holoCyan, "WATER");
            WaterInstalled = new GameObject("Installed"); WaterInstalled.transform.SetParent(wat, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Body", WaterInstalled.transform, new Vector3(0, 0.6f, 0), new Vector3(1.2f, 1.2f, 0.8f), R.Panel);
            for (int i = 0; i < 2; i++) ProcMesh.Prim(PrimitiveType.Cylinder, "Tank", WaterInstalled.transform, new Vector3(-0.3f + i * 0.6f, 1.6f, 0), new Vector3(0.45f, 0.4f, 0.45f), R.Lit(new Color(0.4f, 0.7f, 1f), 0.95f, 0.1f), null, false);
            var tap = ProcMesh.Prim(PrimitiveType.Cube, "Tap", WaterInstalled.transform, new Vector3(0, 0.8f, 0.45f), new Vector3(0.3f, 0.2f, 0.15f), R.CyanGlow);
            WaterInstalled.SetActive(false);
            WaterSlot = Interactable.Add(WaterGhost, "Install", "Water machine here", null); WaterSlot.enabled = false;
            Tap = Interactable.Add(tap, "Drink", "Fresh Water", null);

            // bed
            ProcMesh.Prim(PrimitiveType.Cube, "Bunk", t, new Vector3(-W / 2 + 0.7f, 0.45f, 2.2f), new Vector3(1.1f, 0.3f, 2.3f), R.HullDark);
            var mattress = ProcMesh.Prim(PrimitiveType.Cube, "Mattress", t, new Vector3(-W / 2 + 0.7f, 0.66f, 2.2f), new Vector3(1.0f, 0.14f, 2.2f), R.Lit(new Color(0.2f, 0.32f, 0.45f), 0.1f));
            Bed = Interactable.Add(mattress, "Sleep", "Skip ahead 30 days", null); Bed.enabled = false;

            // data console with countdown
            var desk = new GameObject("DataConsole").transform; desk.SetParent(t, false); desk.localPosition = new Vector3(0.8f, 0, D / 2 - 0.8f);
            ProcMesh.Prim(PrimitiveType.Cube, "Desk", desk, new Vector3(0, 0.75f, 0), new Vector3(2.8f, 0.08f, 0.9f), R.Panel);
            ProcMesh.Prim(PrimitiveType.Cube, "Leg", desk, new Vector3(0, 0.37f, 0), new Vector3(2.6f, 0.74f, 0.1f), R.HullDark);
            var scr = ProcMesh.Prim(PrimitiveType.Cube, "Screen", desk, new Vector3(0, 1.75f, 0.35f), new Vector3(2.6f, 1.3f, 0.06f), R.Emissive(new Color(0.01f, 0.02f, 0.03f), new Color(0.02f, 0.08f, 0.12f), 0.9f));
            TimerInside = World3DText(desk, new Vector3(0, 1.75f, 0.3f), "", 3.2f, new Color(0.55f, 0.95f, 1f));

            TimerInside.rectTransform.sizeDelta = new Vector2(2.4f, 1.2f);
            DataConsole = Interactable.Add(scr, "Open", "NASA Data", null);
            Util.PlaceModel("Helmet", desk.TransformPoint(new Vector3(-0.9f, 0.8f, 0)), 0.42f, 160, t);
            Util.PlaceModel("Wrench", desk.TransformPoint(new Vector3(0.9f, 0.8f, 0.1f)), 0.35f, 70, t);
            Util.PlaceModel("Crew_Lock_Bag", t.TransformPoint(new Vector3(-W / 2 + 1.0f, 0, -1.6f)), 0.9f, 20, t);
            Interior.SetActive(false);
        }

        // ------------------------------------------------------------------ props, power, doors
        /// <summary>A bought prop at its real size (times scale). Returns null when the paid files are not in the project (GitHub copy).</summary>
        static GameObject Prop(string name, Transform parent, Vector3 lpos, float yaw, float scale = 1f)
        {
            var prefab = R.Model("P_" + name); if (!prefab) return null;
            var g = Object.Instantiate(prefab, parent, false); g.name = name;
            g.transform.localPosition = lpos; g.transform.localRotation = Quaternion.Euler(0, yaw, 0); g.transform.localScale = Vector3.one * scale;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }
        /// <summary>A bought prop scaled to a height (in metres), standing on the floor at lpos.</summary>
        static GameObject PropH(string name, Transform parent, Vector3 lpos, float yaw, float height)
        {
            var g = Prop(name, parent, lpos, yaw); if (!g) return null;
            var b = Util.Bounds(g); float s = height / Mathf.Max(0.01f, b.size.y);
            g.transform.localScale = Vector3.one * s;
            b = Util.Bounds(g); g.transform.position += Vector3.up * (parent.TransformPoint(lpos).y - b.min.y);
            return g;
        }
        static void Glow(Material m) { if (m && m.HasProperty("_EmissionColor")) powerGlows.Add((m, m.GetColor("_EmissionColor"))); }
        static void Glow(GameObject g)
        {
            if (!g) return;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials) Glow(m);
        }
        public static bool PowerOn { get; private set; } = true;
        /// <summary>0 = power off (dark, orange emergency light), 1 = all lights on.</summary>
        public static void SetPower(float k)
        {
            PowerOn = k > 0.99f;
            foreach (var (l, on) in powerLights) if (l) { l.intensity = on * k; l.enabled = l.intensity > 0.01f; }
            foreach (var (m, c) in powerGlows) if (m) m.SetColor("_EmissionColor", c * k);
            if (emergency) emergency.intensity = Mathf.Lerp(1.6f, 0, k);
            if (Game.Sky) Game.Sky.InteriorBright = Mathf.Lerp(0.35f, 1f, k);
        }

        static Transform DoorPanel(Transform parent, Vector3 lp, Vector3 size)
        {
            var root = new GameObject("DoorPanel").transform; root.SetParent(parent, false); root.localPosition = lp;
            float fz = size.z / 2 + 0.01f;
            ProcMesh.Prim(PrimitiveType.Cube, "Slab", root, Vector3.zero, size, R.Panel);
            for (int i = -1; i <= 1; i += 2)
                ProcMesh.Prim(PrimitiveType.Cube, "Stripe", root, new Vector3(0, i * size.y * 0.38f, fz), new Vector3(size.x, 0.16f, 0.02f), R.Orange, null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Window", root, new Vector3(0, size.y * 0.16f, fz), new Vector3(size.x * 0.4f, 0.32f, 0.02f), R.Emissive(new Color(0.05f, 0.15f, 0.2f), new Color(0.2f, 0.9f, 1.3f)), null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Handle", root, new Vector3(-size.x * 0.36f, -0.05f, fz + 0.03f), new Vector3(0.08f, 0.5f, 0.06f), R.HullDark, null, false);
            ProcMesh.Prim(PrimitiveType.Sphere, "Status", root, new Vector3(size.x * 0.36f, 0.1f, fz), Vector3.one * 0.12f, R.Emissive(Color.black, new Color(0.3f, 2.5f, 0.6f)), null, false);
            var sd = root.gameObject.AddComponent<SlideDoor>(); sd.Closed = lp; sd.Slide = size.x + 0.05f;
            return root;
        }

        static GameObject StationGhost(Transform parent, Material holo, string label)
        {
            var g = new GameObject("Ghost"); g.transform.SetParent(parent, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Outline", g.transform, new Vector3(0, 0.6f, 0), new Vector3(1.3f, 1.2f, 0.9f), holo, null, true);
            ProcMesh.Prim(PrimitiveType.Cylinder, "Pad", g.transform, new Vector3(0, 0.02f, 0), new Vector3(1.8f, 0.01f, 1.8f), holo, null, false);
            var txt = World3DText(g.transform, new Vector3(0, 1.6f, 0), label, 2.2f, new Color(0.5f, 0.95f, 1f));
            txt.rectTransform.sizeDelta = new Vector2(3, 0.6f); txt.transform.localRotation = Quaternion.Euler(0, 180, 0);
            return g;
        }

        // ------------------------------------------------------------------
        static void SkyBodies()
        {
            var em = new Material(R.Template("Planet")); em.SetTexture("_MainTex", (R.Tex("Earth_HD") ? R.Tex("Earth_HD") : R.Tex("Earth__A_")));
            em.SetColor("_AtmoColor", new Color(0.4f, 0.65f, 1f)); em.SetFloat("_AtmoStrength", 1.4f);
            Earth = ProcMesh.Prim(PrimitiveType.Sphere, "SkyEarth", null, Vector3.zero, Vector3.one * 150f, em, null, false).transform;
            Earth.GetComponent<MeshFilter>().sharedMesh = ProcMesh.HiSphere;
            var mm = new Material(R.Template("Planet")); mm.SetTexture("_MainTex", R.Tex("Mars___Phobos")); mm.SetColor("_Tint", new Color(0.8f, 0.78f, 0.76f)); mm.SetFloat("_AtmoStrength", 0);
            Phobos = ProcMesh.Prim(PrimitiveType.Sphere, "SkyPhobos", null, Vector3.zero, new Vector3(110, 85, 95), mm, null, false).transform;
            var sb = Earth.gameObject.AddComponent<SkyBody>(); sb.Dir = new Vector3(0.55f, 0.3f, 0.78f).normalized;
            var sp = Phobos.gameObject.AddComponent<SkyBody>(); sp.Dir = new Vector3(-0.8f, 0.3f, 0.1f).normalized; sp.Spin = 2f;
            Earth.SetParent(Root.transform, true); Phobos.SetParent(Root.transform, true);
        }

        public static void SetSurface(bool on) { if (Root) Root.SetActive(on); }
    }

    /// <summary>Keeps a planet at a fixed direction in the sky (follows the camera so it looks infinitely far).</summary>
    public class SkyBody : MonoBehaviour
    {
        public Vector3 Dir; public float Dist = 2600; public float Spin = 1f; Material m;
        Renderer rend;
        void Start() { rend = GetComponent<Renderer>(); m = rend.material; }
        void LateUpdate()
        {
            var cam = Game.Cam; if (!cam) return;
            transform.position = cam.transform.position + Dir * Dist;
            if (rend && Game.Sky) rend.enabled = Game.Sky.DustStorm < 0.25f;
            transform.Rotate(0, Spin * Time.deltaTime, 0, Space.Self);
            if (m) m.SetVector("_SunDirW", (-Dir + Vector3.up * 0.3f + Vector3.Cross(Vector3.up, Dir) * 0.5f).normalized);
        }
    }

    /// <summary>An airlock door that slides sideways.</summary>
    public class SlideDoor : MonoBehaviour
    {
        public Vector3 Closed; public float Slide = 1.5f;
        public IEnumerator Move(bool open, float d = 0.45f)
        {
            Vector3 from = transform.localPosition, to = Closed + (open ? Vector3.right * Slide : Vector3.zero);
            Sfx.Play("whoosh", 0.35f, open ? 1.4f : 1.1f);
            float t = 0;
            while (t < d) { t += Time.deltaTime; transform.localPosition = Vector3.Lerp(from, to, Tween.EaseInOut(t / d)); yield return null; }
            transform.localPosition = to;
            if (!open) Sfx.Play("thud", 0.3f, 1.3f);
        }
        public void Snap(bool open) { transform.localPosition = Closed + (open ? Vector3.right * Slide : Vector3.zero); }
    }

    /// <summary>Shows its children only when the camera is near (so labels do not clutter the sky).</summary>
    public class NearOnly : MonoBehaviour
    {
        public float Far = 70f, Near = 9f; bool shown = true;
        void LateUpdate()
        {
            var c = Game.Cam; if (!c) return;
            float d = Vector3.Distance(c.transform.position, transform.position);
            bool driving = MarsWorld.TheRover && MarsWorld.TheRover.Driving;
            bool show = d < Far && d > Near && !driving; // hidden when far, when right next to it, and while you drive the rover
            if (show != shown) { shown = show; foreach (Transform ch in transform) ch.gameObject.SetActive(show); }
        }
    }

    public class Billboard : MonoBehaviour
    {
        void LateUpdate() { var c = Game.Cam; if (!c) return; var d = transform.position - c.transform.position; d.y = 0; if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d); }
    }
    public class Spin : MonoBehaviour { public float Speed = 30; void Update() => transform.Rotate(0, Speed * Time.deltaTime, 0, Space.World); }
    public class Bob : MonoBehaviour
    {
        Vector3 basePos; float ph;
        void Start() { basePos = transform.position; ph = Random.value * 6; }
        public void Rebase() { basePos = transform.position; }
        void Update() => transform.position = basePos + Vector3.up * (0.08f + Mathf.Sin(Time.time * 2 + ph) * 0.08f);
    }
    public class Flicker : MonoBehaviour
    {
        Light l; float b;
        void Start() { l = GetComponent<Light>(); b = l.intensity; }
        void Update() => l.intensity = b * (0.75f + Mathf.PerlinNoise(Time.time * 9, 0) * 0.5f);
    }
    public class Blink : MonoBehaviour
    {
        public float Rate = 1f; Light l; float b;
        void Start() { l = GetComponent<Light>(); b = l.intensity; }
        void Update() { if (b > 0) l.intensity = (Mathf.Sin(Time.time * Rate * 6.28f) > 0 ? 1 : 0.15f) * b; }
        public void SetBase(float v) { b = v; }
    }
}
