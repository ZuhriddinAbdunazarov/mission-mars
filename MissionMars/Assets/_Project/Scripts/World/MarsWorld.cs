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
            if (!Game.ExploreBase) BuildParts(); else Pad.gameObject.SetActive(false);
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
                // every rock taller than a step is solid: you walk around it (or step up on it), never through it
                if (s > 0.55f) { var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = g.GetComponent<MeshFilter>().sharedMesh; }
                if (s <= 1.4f) g.GetComponent<MeshRenderer>().shadowCastingMode = s > 0.6f ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
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
            var hull = ModelRoom.Place("CargoLanderWreck", Wreck, c - Vector3.up * 0.12f);
            ModelRoom.AddColliders(hull);
            if (Game.ExploreBase) return;
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
        public static Transform Vestibule;
        public static SlideDoor InnerHatch;
        static void BuildBunker()
        {
            Bunker = new GameObject("Bunker").transform; Bunker.SetParent(Root.transform);
            Vector3 c = G(BunkerXZ); Bunker.position = c;
            Vector3 toWake = new Vector3(WakeXZ.x - BunkerXZ.x, 0, WakeXZ.y - BunkerXZ.y).normalized;
            Bunker.rotation = Quaternion.LookRotation(toWake);
            var lockT = new GameObject("Airlock").transform;
            lockT.SetParent(Bunker, false);
            lockT.localPosition = new Vector3(0, 0, 7.2f);
            var sill = lockT.TransformPoint(Vector3.forward*3.45f);
            var lockPos=lockT.position; lockPos.y=MarsTerrain.H(sill.x,sill.z); lockT.position=lockPos;
            var entrance = ModelRoom.Place("04_Storm_Shelter_Entrance", lockT, new Vector3(0,-0.35f,-2.8f), -90);
            ModelRoom.Part(entrance, "Armoured hatch").gameObject.SetActive(false);
            var portal = ModelRoom.Part(entrance, "Concrete portal");
            // the hidden ramp leaves a pit in the dirt mound behind the airlock: fill it up to the mound's surface
            portal.gameObject.SetActive(false);
            ModelRoom.AddColliders(entrance);
            PitFill(lockT);
            void Panel(string name, Vector3 p, Vector3 size, Material mat) =>
                ProcMesh.Prim(PrimitiveType.Cube, name, lockT, p, size, mat);
            Panel("Airlock deck", new Vector3(0,-0.045f,1.4f), new Vector3(2.9f,0.12f,4.8f), R.Floor);
            Panel("Roof", new Vector3(0,2.68f,1.4f), new Vector3(3.1f,.22f,4.8f), R.Panel);
            foreach (int side in new[]{-1,1})
            {
                Panel("Pressure wall", new Vector3(side*1.45f,1.34f,1.4f), new Vector3(.16f,2.68f,4.8f), R.Panel);
                Panel("Guidance light", new Vector3(side*1.35f,.46f,1.4f), new Vector3(.045f,.07f,4.2f), R.CyanGlow);
                var pipe = ProcMesh.Prim(PrimitiveType.Cylinder, side<0?"Atmosphere sampling mast":"Equipment exhaust", lockT,
                    new Vector3(side*2,2.6f,-3.4f),new Vector3(.32f,2.6f,.32f),R.HullDark);
                ProcMesh.Prim(PrimitiveType.Cylinder,"Weather hood",lockT,new Vector3(side*2,5.22f,-3.4f),new Vector3(.55f,.08f,.55f),R.Hull);
                for(int k=0;k<3;k++) ProcMesh.Prim(PrimitiveType.Cylinder,"Louvre",lockT,new Vector3(side*2,4.86f+k*.1f,-3.4f),new Vector3(.43f,.023f,.43f),R.Panel);
            }
            var lamp = new GameObject("Vestibule lamp").AddComponent<Light>(); lamp.transform.SetParent(lockT,false);
            lamp.transform.localPosition = new Vector3(0,2.3f,1.2f); lamp.range=5; lamp.intensity=2;
            Vestibule = lockT;
            InnerHatch = DoorPanel(lockT,new Vector3(0,1.27f,-.62f),new Vector3(2.7f,2.5f,.12f)).GetComponent<SlideDoor>();
            BunkerDoorPanel = DoorPanel(lockT,new Vector3(0,1.27f,3.45f),new Vector3(2.7f,2.5f,.12f));
            BunkerDoor = Interactable.Add(BunkerDoorPanel.gameObject,"Cycle","Shelter airlock",null); BunkerDoor.Range=4.5f;
            var outside = lockT.TransformPoint(Vector3.forward*5.5f);
            BunkerOutside = G(outside.x,outside.z,.06f); BunkerOutsideYaw=Bunker.eulerAngles.y;

            // the 2-year countdown hologram above the bunker
            TimerHolo = new GameObject("TimerHolo"); TimerHolo.transform.SetParent(Bunker, false); TimerHolo.transform.localPosition = new Vector3(0, 11.5f, 0);
            var ringH = ProcMesh.Prim(PrimitiveType.Cylinder, "Ring", TimerHolo.transform, new Vector3(0, -2.2f, 0), new Vector3(7f, 0.02f, 7f), holoCyan, null, false); ringH.AddComponent<Spin>().Speed = 25;
            ProcMesh.Prim(PrimitiveType.Cylinder, "Beam", TimerHolo.transform, new Vector3(0, -4.5f, 0), new Vector3(0.2f, 2.5f, 0.2f), holoCyan, null, false);
            TimerText = World3DText(TimerHolo.transform, Vector3.zero, "", 7, new Color(0.55f, 0.95f, 1f));
            TimerText.gameObject.AddComponent<Billboard>();
            TimerHolo.SetActive(false);
        }

        /// <summary>A sloped block of Mars rock that fills the old ramp pit of the shelter mound. The pit sits right behind
        /// the airlock (airlock coordinates, measured from a top-down picture): 3.3 m wide, from the airlock's back (z -1)
        /// about 1.6 m into the hill; the mound around it is about 3 m high inside and 2.3 m next to the airlock.</summary>
        static void PitFill(Transform lockT)
        {
            const float w = 1.8f, yb = -0.4f, zIn = -2.9f, zOut = -0.75f, hIn = 3.05f, hOut = 2.45f;
            var c = new[] {
                new Vector3(-w, yb, zIn), new Vector3(w, yb, zIn), new Vector3(w, yb, zOut), new Vector3(-w, yb, zOut), // bottom
                new Vector3(-w, hIn, zIn), new Vector3(w, hIn, zIn), new Vector3(w, hOut, zOut), new Vector3(-w, hOut, zOut) }; // top
            int[][] quads = { new[] { 4, 5, 6, 7 }, new[] { 0, 3, 2, 1 }, new[] { 0, 1, 5, 4 }, new[] { 3, 7, 6, 2 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 } };
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            foreach (var q in quads)
                for (int side = 0; side < 2; side++) // both sides, so it looks right whichever way the model is mirrored
                {
                    int b = v.Count;
                    foreach (int i in q) { var p = c[i]; v.Add(p); uv.Add(new Vector2(p.x + p.z, p.y + p.z * 0.5f) / 3f); }
                    if (side == 0) tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                    else tri.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
                }
            var mesh = new Mesh { name = "Pit fill" }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var g = new GameObject("Pit fill"); g.transform.SetParent(lockT, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = R.Rock;
            g.AddComponent<MeshCollider>().sharedMesh = mesh;
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
                    ProcMesh.Prim(PrimitiveType.Cylinder, "Bollard", Root.transform, wp + Vector3.up * 0.45f, new Vector3(0.18f, 0.45f, 0.18f), R.Panel, null, false);
                    ProcMesh.Prim(PrimitiveType.Cylinder, "BollardTop", Root.transform, wp + Vector3.up * 0.92f, new Vector3(0.2f, 0.04f, 0.2f), R.CyanGlow, null, false);
                }
            var rp = Bunker.TransformPoint(new Vector3(8f, 0, 12f));
            TheRover = Rover.Create(rp, Bunker.eulerAngles.y + 20, Root.transform);

            IceBlocks.Clear();
            if (Game.ExploreBase) return;
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
            var link = Interactable.Add(tower, "Open", "EARTH RADIO", () => Computers.I.OpenTerminal());
            link.Range = 4f;
        }

        // ------------------------------------------------------------------ MARS BASE
        /// <summary>The MARS BASE, built from clean artist-made pieces (Quaternius Ultimate Space Kit + Kenney Space Kit, both CC0):
        /// a habitat hub, a walk-in PLANTATION dome (press E at its door to go inside), a lab with the weather station,
        /// the dish that calls Earth and a solar field. Every building has a big floating name and teaches something.</summary>
        static void BuildMarsBase()
        {
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

            Interactable Module(string asset, float x, float z, string doorName, string label, out Vector3 outside)
            {
                var module = ModelRoom.Place(asset, root, new Vector3(x, 0, z), -90);
                var pos = module.transform.position;
                module.transform.position = G(pos.x, pos.z);
                ModelRoom.AddColliders(module);
                var door = ModelRoom.Part(module, doorName);
                var approach = door.position + root.forward * 3f;
                outside = G(approach.x, approach.z, 0.1f);
                var use = Interactable.Add(door.gameObject, "Go into", label, null);
                use.Range = 5f;
                return use;
            }

            HabitatDoor = Module("01_Habitat_Exterior", 0, 0, "03 | Airlock", "HABITAT", out HabitatOutside);
            GreenhouseDoor = Module("03_Greenhouse_Exterior", 20, -3, "Airlock,", "PLANTATION (grow food)", out GreenhouseOutside);
            GreenhouseDoorPanel = null;
            GreenhouseOutsideYaw = root.eulerAngles.y;
            WeatherDoor = Module("02_Weather_Lab_Exterior", -18, -2, "Airlock |", "WEATHER LAB", out WeatherOutside);
            // 4. DISH that talks to Earth, on a metal tower

            // 5. SOLAR FIELD (power for the base)
            GameObject panel = null;
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 4; i++)
                {
                    var p = Kit("Q_SolarPanel_Ground", 6 + i * 3.3f, -15 - row * 4.2f, 0, 2.2f);
                    if (!panel) panel = p;
                }
            Kit("K_barrels", -24, 6, 30, 3f);
            // one big rock at the far edge (the rocks next to the plantation were removed: they blocked the view)
            Kit("Q_Rock_Large_1", -30, -14, 40, 2.2f);

            // what each building does (press E)

            if (panel) SolarInfo(panel);

            // big floating names + NOVA's guided tour stops
            BaseStops.Clear();
            void Stop(Vector3 local, string name, string what, string line, Color c)
            {
                var w = root.TransformPoint(local); w.y = MarsTerrain.H(w.x, w.z) + local.y;
                Label(Root.transform, w, name, what, c);
                BaseStops.Add((w, line));
            }
            Stop(new Vector3(0, 10.5f, 0), "HABITAT", "home: eat, sleep, fresh air", "This is the HABITAT, the home of the base. Inside: beds, a kitchen and the navigation computer that tracks Earth and Mars.", HC.Cyan);
            Stop(new Vector3(20, 5f, -3), "PLANTATION", "go inside to grow potatoes", "The glass dome is the PLANTATION. Sunlight comes through the glass. Go inside to plant, water and harvest potatoes!", HC.Green);
            Stop(new Vector3(-18, 7f, -2), "WEATHER LAB", "real NASA weather", "This is the WEATHER LAB. Inside, scientists study real Mars weather measured by NASA's InSight lander and make forecasts.", HC.Amber);
            Stop(new Vector3(11, 6, -17), "SOLAR PANELS", "power from the Sun", "SOLAR PANELS make our power. Mars gets only about half the sunlight Earth gets, so we need a lot of them.", HC.Amber);
            var title = World3DText(root, new Vector3(0, 24, 0), "MARS BASE", 16, new Color(1f, 0.8f, 0.35f));
            title.gameObject.AddComponent<Billboard>();
        }

        public static Interactable HabitatDoor, WeatherDoor; public static Vector3 HabitatOutside, WeatherOutside;
        public static ModelRoom HabitatRoom, WeatherRoom;
        public static Interactable Bunk, Kitchen, Locker, NavComputer, WeatherComputer, Racks;
        public static GameObject[] LockerSuits; public static Transform[] CabinetDoors;
        /// <summary>Swing the suit cabinet's glass doors open (or shut).</summary>
        public static System.Collections.IEnumerator CabinetOpen(bool open, float d = 0.7f)
        {
            if (CabinetDoors == null) yield break;
            Sfx.Play(open ? "whoosh" : "thud", 0.35f, open ? 1.4f : 1.2f);
            float a0 = CabinetDoors[0].localEulerAngles.y; a0 = a0 > 180 ? a0 - 360 : a0; float a1 = open ? 105 : 0;
            for (float t = 0; t < 1; t += Time.deltaTime / d)
            {
                float a = Mathf.Lerp(Mathf.Abs(a0), a1, Tween.EaseInOut(t));
                CabinetDoors[0].localRotation = Quaternion.Euler(0, -a, 0); CabinetDoors[1].localRotation = Quaternion.Euler(0, a, 0);
                yield return null;
            }
            CabinetDoors[0].localRotation = Quaternion.Euler(0, -a1, 0); CabinetDoors[1].localRotation = Quaternion.Euler(0, a1, 0);
        }

        /// <summary>The walk-in HABITAT and WEATHER LAB rooms, made from the team's Tripo interiors (Assets/Paid).
        /// Press-E zones sit on the model's furniture (positions measured from the model's height map).</summary>
        static void BuildRooms()
        {
            HabitatRoom = ModelRoom.Build("06_Habitat_Interior", new Vector3(0, -260, 0), 10, 5);
            Bunk = HabitatRoom.UsePart("Double bunk -", "Sleep in", "your bunk bed (1 sol passes)");
            Kitchen = HabitatRoom.UsePart("Galley", "Eat & drink at", "the kitchen");
            Locker = HabitatRoom.UsePart("EVA locker", "Put on", "SPACESUIT");
            // real 3D astronaut suits in a clean open cabinet (the model has locker + simple suits as ONE piece:
            // we hide its look but keep it as the press-E spot, and build the cabinet around the real NASA suit models)
            if (R.Model("Astronaut"))
            {
                var lockerT = Locker.transform; var sb = Util.Bounds(lockerT.gameObject);
                foreach (var rr in lockerT.GetComponentsInChildren<Renderer>()) rr.enabled = false;
                // a cabinet is wide and shallow: its front is along the SHORT side, toward the open room
                var toRoom = HabitatRoom.Room.transform.position - sb.center; toRoom.y = 0;
                bool alongX = sb.size.x < sb.size.z;
                var face = alongX ? new Vector3(Mathf.Sign(toRoom.x == 0 ? 1 : toRoom.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(toRoom.z == 0 ? 1 : toRoom.z));
                {   // check: the front must open into free space (not a wall); else turn around
                    Physics.SyncTransforms(); var from = sb.center + Vector3.up * 0.3f;
                    float fwdFree = Physics.Raycast(from + face * (Mathf.Abs(Vector3.Dot(sb.extents, face)) + 0.05f), face, out var hf, 3f) ? hf.distance : 3f;
                    float backFree = Physics.Raycast(from - face * (Mathf.Abs(Vector3.Dot(sb.extents, face)) + 0.05f), -face, out var hb, 3f) ? hb.distance : 3f;
                    if (backFree > fwdFree + 0.3f) face = -face;
                }
                float yaw = Quaternion.LookRotation(face).eulerAngles.y;
                var side = Quaternion.Euler(0, 90, 0) * face;
                float w = Mathf.Abs(Vector3.Dot(sb.size, new Vector3(Mathf.Abs(side.x), 0, Mathf.Abs(side.z)))), dp = Mathf.Abs(Vector3.Dot(sb.size, new Vector3(Mathf.Abs(face.x), 0, Mathf.Abs(face.z)))), h = sb.size.y;
                var cab = new GameObject("SuitCabinet").transform; cab.SetParent(HabitatRoom.Room.transform, true);
                cab.position = new Vector3(sb.center.x, sb.min.y, sb.center.z); cab.rotation = Quaternion.Euler(0, yaw, 0);
                var wood = R.Lit(new Color(0.86f, 0.87f, 0.89f), 0.35f); var dark = R.Lit(new Color(0.18f, 0.2f, 0.23f), 0.3f);
                ProcMesh.Prim(PrimitiveType.Cube, "Back", cab, new Vector3(0, h / 2, -dp / 2 + 0.03f), new Vector3(w, h, 0.06f), dark, null, false);
                foreach (int sd in new[] { -1, 1 }) ProcMesh.Prim(PrimitiveType.Cube, "Side", cab, new Vector3(sd * (w / 2 - 0.03f), h / 2, 0), new Vector3(0.06f, h, dp), wood, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "Top", cab, new Vector3(0, h - 0.03f, 0), new Vector3(w, 0.06f, dp), wood, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "Floor", cab, new Vector3(0, 0.04f, 0), new Vector3(w, 0.08f, dp), dark, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "Light", cab, new Vector3(0, h - 0.08f, 0.1f), new Vector3(w * 0.8f, 0.03f, 0.08f), R.Emissive(Color.white, new Color(1.6f, 1.6f, 1.5f)), null, false);
                LockerSuits = new GameObject[2];
                for (int i = 0; i < 2; i++)
                {   // two spacesuits hanging relaxed, arms down (Jungle Jim astronaut model)
                    var at = cab.TransformPoint(new Vector3((i == 0 ? -1 : 1) * w * 0.23f, 0.08f, -0.05f));
                    LockerSuits[i] = AstroRig.Spawn(at, yaw, Mathf.Min(h * 0.86f, 1.8f), cab);
                }
                // glass doors on hinges: they swing open when you take a suit
                var glass = R.Holo(new Color(0.65f, 0.85f, 1f), 0.35f, 0.07f);
                CabinetDoors = new Transform[2];
                for (int i = 0; i < 2; i++)
                {
                    int sd = i == 0 ? -1 : 1;
                    var hinge = new GameObject(i == 0 ? "HingeL" : "HingeR").transform; hinge.SetParent(cab, false);
                    hinge.localPosition = new Vector3(sd * (w / 2 - 0.03f), 0, dp / 2 + 0.02f);
                    float dw = w / 2 - 0.04f;
                    ProcMesh.Prim(PrimitiveType.Cube, "Pane", hinge, new Vector3(-sd * dw / 2, h / 2, 0), new Vector3(dw - 0.06f, h - 0.16f, 0.015f), glass, null, false);
                    foreach (var (pos, size) in new[] { (new Vector3(-sd * dw / 2, 0.06f, 0), new Vector3(dw, 0.05f, 0.04f)), (new Vector3(-sd * dw / 2, h - 0.08f, 0), new Vector3(dw, 0.05f, 0.04f)),
                                                        (new Vector3(-sd * 0.02f, h / 2, 0), new Vector3(0.05f, h - 0.1f, 0.04f)), (new Vector3(-sd * (dw - 0.02f), h / 2, 0), new Vector3(0.05f, h - 0.1f, 0.04f)) })
                        ProcMesh.Prim(PrimitiveType.Cube, "Frame", hinge, pos, size, wood, null, false);
                    ProcMesh.Prim(PrimitiveType.Cube, "Handle", hinge, new Vector3(-sd * (dw - 0.08f), h * 0.5f, 0.04f), new Vector3(0.03f, 0.3f, 0.03f), dark, null, false);
                    CabinetDoors[i] = hinge;
                }
            }
            NavComputer = HabitatRoom.UsePart("Crew workstation", "Use", "NAVIGATION COMPUTER (Earth & Mars)");
            HabitatRoom.Room.SetActive(false);

            WeatherRoom = ModelRoom.Build("07_Weather_Lab_Interior", new Vector3(0, -340, 0), 7, 4);
            WeatherComputer = WeatherRoom.UsePart("Forecast bench", "Use", "WEATHER COMPUTER (real NASA data)");
            Racks = WeatherRoom.UsePart("Instrumentation rack 1", "Read", "the instrument racks");
            EarthLink(ModelRoom.Part(WeatherRoom.Room, "Instrumentation rack 2").gameObject);
            WeatherRoom.Room.SetActive(false);
        }

        public static Interactable GreenhouseDoor; public static Transform GreenhouseDoorPanel; public static Vector3 GreenhouseOutside; public static float GreenhouseOutsideYaw;

        /// <summary>Big floating names over the main places, so kids always know where they are.</summary>
        static void PlaceLabels()
        {
            Label(Root.transform, Bunker.TransformPoint(new Vector3(0, 7.5f, -1.5f)), "YOUR SHELTER", "underground: safe from space rays", HC.Green);
            if (TheRover) Label(TheRover.transform, TheRover.transform.position + Vector3.up * 4.5f, "ROVER", "press E to drive", HC.Amber);
            if (!Game.ExploreBase) Label(Root.transform, Pad.position + Vector3.up * 28f, "LAUNCH PAD", "build your new rocket here", HC.Amber);
            Label(Root.transform, G(CrashXZ, 14f), "CRASHED SHIP", Game.ExploreBase ? "cargo ramp / expedition case" : "mission cargo", HC.Red);
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
            var shell = ModelRoom.Place("09_Storm_Shelter_Interior", t, Vector3.zero);
            shell.transform.localScale = new Vector3(W / 8f, Hh / 2f, D / 5f);
            ModelRoom.Part(shell, "Centered short-wall").gameObject.SetActive(false);
            // solid walls just behind the model walls: no gaps where you could see the outside (behind the bunks)
            foreach (var (pos, size) in new[] { (new Vector3(-W / 2 - 0.12f, Hh / 2, 0), new Vector3(0.14f, Hh + 0.4f, D + 0.4f)), (new Vector3(W / 2 + 0.12f, Hh / 2, 0), new Vector3(0.14f, Hh + 0.4f, D + 0.4f)),
                                                (new Vector3(0, Hh / 2, D / 2 + 0.12f), new Vector3(W + 0.4f, Hh + 0.4f, 0.14f)) })
                ProcMesh.Prim(PrimitiveType.Cube, "BackWall", t, pos, size, R.Panel);

            // Reposition the pack's equipment at the shelter mission stations.
            GameObject Equipment(string name, Transform parent, Vector3 floor, float yaw = 0)
            {
                var part = ModelRoom.Part(shell, name);
                float halfHeight = part.localPosition.y;
                part.SetParent(parent, false);
                part.localRotation = Quaternion.Euler(0, yaw, 0);
                part.localPosition = floor + Vector3.up * halfHeight;
                ModelRoom.AddColliders(part.gameObject);
                return part.gameObject;
            }

            var bunk = Equipment("Double bunk -", t, new Vector3(-W / 2 + 0.7f, 0, 2.2f), 90);
            Equipment("Double bunk 1", t, new Vector3(-W / 2 + 0.7f, 0, 0), 90);
            Equipment("Emergency provisions", t, new Vector3(W / 2 - 0.45f, 0, 0));
            // Leave a real opening behind the cabin hatch, with a lit view of the pressure lock.
            ModelRoom.Part(shell,"Starboard | removable").gameObject.SetActive(false);
            void CabinPanel(string name,float x,float y,float z,float w,float h,float d,Material m) =>
                ProcMesh.Prim(PrimitiveType.Cube,name,t,new Vector3(x,y,z),new Vector3(w,h,d),m);
            float end=-D/2;
            CabinPanel("Port bulkhead",-4.75f,Hh/2,end,1.5f,Hh,.14f,R.Panel);
            CabinPanel("Service bulkhead",1.55f,Hh/2,end,7.9f,Hh,.14f,R.Panel);
            CabinPanel("Hatch lintel",-3.2f,2.98f,end,1.65f,1.24f,.14f,R.Panel);
            CabinPanel("Return passage deck",-3.2f,-.07f,end-1.25f,1.8f,.14f,2.5f,R.Floor);
            CabinPanel("Return passage roof",-3.2f,2.55f,end-1.25f,1.8f,.14f,2.5f,R.Panel);
            foreach(int side in new[]{-1,1}){
                CabinPanel("Return passage lining",-3.2f+side*.85f,1.25f,end-1.25f,.12f,2.5f,2.5f,R.Panel);
                CabinPanel("Return guide lamp",-3.2f+side*.77f,.65f,end-1.25f,.04f,.07f,2.3f,R.CyanGlow);
            }
            DoorPanel(t,new Vector3(-3.2f,1.25f,end-2.4f),new Vector3(1.72f,2.5f,.12f));
            // Static walls and floor coincide with the imported deck, including its corner posts.
            foreach (var prefix in new[] { "Deck", "Port | removable", "Near |", "Door end" })
                ModelRoom.AddColliders(ModelRoom.Part(shell, prefix).gameObject);
            ProcMesh.Prim(PrimitiveType.Cube, "Ceiling", t, new Vector3(0, Hh + 0.1f, 0), new Vector3(W, 0.2f, D), R.Panel);
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
            for (int i = -1; i <= 1; i += 2)
            {
                ProcMesh.Prim(PrimitiveType.Cube, "Strip", t, new Vector3(i * 1.1f, Hh - 0.02f, 0), new Vector3(0.12f, 0.03f, D - 1), strip, null, false);
            }
            for (int i = -1; i <= 1; i += 2)
                for (int k = -1; k <= 1; k += 2)
                {
                    var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(t, false); l.transform.localPosition = new Vector3(i * 2.6f, Hh - 0.5f, k * 1.9f);
                    l.type = LightType.Point; l.range = 11; l.intensity = 3.2f; l.color = new Color(1f, 0.96f, 0.9f); l.shadows = LightShadows.None;
                    InteriorLights.Add(l); powerLights.Add((l, l.intensity));
                }
            // wall lamps (one per wall)
            // dim orange emergency light: all you have while the power is off
            emergency = new GameObject("Emergency").AddComponent<Light>(); emergency.transform.SetParent(t, false); emergency.transform.localPosition = new Vector3(-2f, Hh - 0.4f, 1.5f);
            emergency.type = LightType.Point; emergency.range = 12; emergency.intensity = 0; emergency.color = new Color(1f, 0.55f, 0.25f); emergency.shadows = LightShadows.None;
            AlarmLight = new GameObject("Alarm").AddComponent<Light>(); AlarmLight.transform.SetParent(t, false); AlarmLight.transform.localPosition = new Vector3(0, Hh - 0.3f, -2);
            AlarmLight.type = LightType.Point; AlarmLight.range = 12; AlarmLight.intensity = 0; AlarmLight.color = new Color(1f, 0.15f, 0.08f);
            AlarmLight.gameObject.AddComponent<Blink>().Rate = 1.2f;

            // ---- POWER CORNER (north wall): battery + cable reel + main switch ----
            var bat = Equipment("Battery drawers", t, new Vector3(-3.0f, 0, D / 2 - 0.45f));
            Glow(bat);
            BatteryPlug = Interactable.Add(bat, "Plug in", "Battery (stores solar power)", null); BatteryPlug.enabled = false;
            var sw = new GameObject("MainSwitch"); sw.transform.SetParent(t, false); sw.transform.localPosition = new Vector3(-1.4f, 0, D / 2 - 0.5f);
            ProcMesh.Prim(PrimitiveType.Cube, "Box", sw.transform, new Vector3(0, 0.9f, 0), new Vector3(1f, 1.8f, 0.6f), R.Panel);
            ProcMesh.Prim(PrimitiveType.Cube, "Lever", sw.transform, new Vector3(0, 1.2f, -0.35f), new Vector3(0.15f, 0.45f, 0.12f), R.Orange, null, false);
            Glow(sw);
            PowerSwitch = Interactable.Add(sw, "Hold E: turn on", "MAIN POWER SWITCH", null, 1.0f); PowerSwitch.enabled = false;
            // the cable appears when you plug it in
            Cable = ProcMesh.Prim(PrimitiveType.Cylinder, "Cable", t, new Vector3(-2.2f, 0.05f, D / 2 - 0.75f), new Vector3(0.07f, 0.8f, 0.07f), R.CyanGlow, new Vector3(0, 0, 90), false);
            Cable.SetActive(false);
            var powerSign = World3DText(t, new Vector3(-2.2f, 2.55f, D / 2 - 0.15f), "POWER", 2.6f, new Color(1f, 0.8f, 0.3f));
            powerSign.transform.localRotation = Quaternion.Euler(0, 0, 0);

            // ---- AIR FILTER (south wall): this is where the oxygen leak happens ----
            ProcMesh.Prim(PrimitiveType.Cube, "AirFilter", t, new Vector3(2.4f, 0.9f, -D / 2 + 0.55f), new Vector3(1.2f, 1.8f, 0.7f), R.Panel);
            var pipe = ProcMesh.Prim(PrimitiveType.Cylinder, "AirPipe", t, new Vector3(2.4f, 2.7f, -D / 2 + 0.3f), new Vector3(0.22f, 0.9f, 0.22f), R.Hull);
            LeakPos = t.TransformPoint(new Vector3(2.4f, 2.5f, -D / 2 + 0.5f));
            LeakSteam = FX.Mist(LeakPos, t, new Color(0.9f, 0.95f, 1f, 0.9f));
            { var m = LeakSteam.main; m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f); var e = LeakSteam.emission; e.rateOverTime = 45; var s = LeakSteam.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 18; s.radius = 0.05f; LeakSteam.transform.localRotation = Quaternion.Euler(0, 0, 0); LeakSteam.Stop(); }
            LeakValve = Interactable.Add(pipe, "Hold E: patch", "AIR LEAK", null, 1.5f); LeakValve.enabled = false;

            // exit: a CLOSED airlock door; Mars daylight shows behind it when it slides open
            ProcMesh.Prim(PrimitiveType.Cube, "ExitSign", t, new Vector3(-3.2f, 2.55f, -D / 2 + 0.15f), new Vector3(0.7f, 0.18f, 0.05f), R.Emissive(Color.black, new Color(0.3f, 2.2f, 0.9f)), null, false);
            ExitDoorPanel = DoorPanel(t, new Vector3(-3.2f, 1.18f, -D / 2 + 0.1f), new Vector3(1.7f, 2.4f, 0.1f)); // fills the whole doorway, slides shut behind you
            ExitDoor = Interactable.Add(ExitDoorPanel.gameObject, "Go outside", "Airlock", null);
            InteriorSpawn = t.TransformPoint(new Vector3(-3.2f, 0.05f, -D / 2 + 1.4f)); InteriorYaw = 0;

            // MOXIE station (east wall)
            var mox = new GameObject("MoxieStation").transform; mox.SetParent(t, false); mox.localPosition = new Vector3(W / 2 - 1f, 0, 1.6f); mox.localRotation = Quaternion.Euler(0, -90, 0);
            MoxieGhost = StationGhost(mox, holoCyan, "MOXIE");
            MoxieInstalled = new GameObject("Installed"); MoxieInstalled.transform.SetParent(mox, false);
            Equipment("Oxygen production", MoxieInstalled.transform, Vector3.zero);
            MoxieBubbles = FX.Mist(mox.TransformPoint(new Vector3(0, 2.0f, 0)), MoxieInstalled.transform, new Color(0.4f, 0.8f, 1f));
            MoxieInstalled.SetActive(false);
            MoxieSlot = Interactable.Add(MoxieGhost, "Install", "MOXIE here", null); MoxieSlot.enabled = false;

            // Water station (north-east)
            var wat = new GameObject("WaterStation").transform; wat.SetParent(t, false); wat.localPosition = new Vector3(W / 2 - 1f, 0, -1.6f); wat.localRotation = Quaternion.Euler(0, -90, 0);
            WaterGhost = StationGhost(wat, holoCyan, "WATER");
            WaterInstalled = new GameObject("Installed"); WaterInstalled.transform.SetParent(wat, false);
            var tap = Equipment("Two water purifiers", WaterInstalled.transform, Vector3.zero);
            WaterInstalled.SetActive(false);
            WaterSlot = Interactable.Add(WaterGhost, "Install", "Water machine here", null); WaterSlot.enabled = false;
            Tap = Interactable.Add(tap, "Drink", "Fresh Water", null);

            Bed = Interactable.Add(bunk, "Sleep", "Skip ahead 30 days", null); Bed.enabled = false;

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
        public Vector3 Direction = Vector3.right;
        public Vector3 Closed; public float Slide = 1.5f;
        public bool AutoClose = true; bool isOpen, moving; float openedAt;
        public IEnumerator Move(bool open, float d = 0.45f)
        {
            moving = true;
            Vector3 from = transform.localPosition, to = Closed + (open ? Direction * Slide : Vector3.zero);
            Sfx.Play("whoosh", 0.35f, open ? 1.4f : 1.1f);
            float t = 0;
            while (t < d) { t += Time.deltaTime; transform.localPosition = Vector3.Lerp(from, to, Tween.EaseInOut(t / d)); yield return null; }
            transform.localPosition = to;
            if (!open) Sfx.Play("thud", 0.3f, 1.3f);
            moving = false; isOpen = open; openedAt = Time.time;
        }
        public void Snap(bool open) { transform.localPosition = Closed + (open ? Direction * Slide : Vector3.zero); isOpen = open; openedAt = Time.time; }
        // a door never stays open: it slides shut by itself after 2.5 seconds
        void Update() { if (AutoClose && isOpen && !moving && Time.time - openedAt > 2.5f) StartCoroutine(Move(false)); }
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
