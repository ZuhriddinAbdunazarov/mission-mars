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
        public static readonly Vector2 BunkerXZ = new Vector2(-36, -24);
        public static readonly Vector2 PadXZ = new Vector2(40, -34);
        public static readonly Vector3 InteriorOrigin = new Vector3(0, -90, 0);

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
            Rocks();
            BuildRover();
            BuildInterior();
            SkyBodies();
            FX.DustBanks(new Vector3(0, MarsTerrain.Base + 2, 0)).transform.SetParent(Root.transform, true);
        }

        static bool Avoid(Vector2 xz, float pad)
        {
            if (Vector2.Distance(xz, WakeXZ) < 8 + pad) return true;
            if (Vector2.Distance(xz, CrashXZ) < 26 + pad) return true;
            if (Vector2.Distance(xz, BunkerXZ) < 20 + pad) return true;
            if (Vector2.Distance(xz, PadXZ) < 16 + pad) return true;
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
            var glass = R.Lit(new Color(0.35f, 0.38f, 0.42f), 0.92f, 0.85f);
            var frame = R.Lit(new Color(0.75f, 0.76f, 0.78f), 0.55f, 0.8f);
            // Tharsis-style cluster of geodesic domes, half covered with regolith for radiation shielding
            Dome(Bunker, new Vector3(0, 0, 0), 7.5f, glass, frame);
            Dome(Bunker, new Vector3(-9.5f, 0, -3), 4.6f, glass, frame);
            Dome(Bunker, new Vector3(8.5f, 0, -4), 4.0f, glass, frame);
            Tube(Bunker, new Vector3(-5.5f, 0, -1.5f), new Vector3(-7.5f, 0, -2.4f), frame);
            Tube(Bunker, new Vector3(5.5f, 0, -2f), new Vector3(7f, 0, -3.2f), frame);
            // sandbags ring
            var bag = R.Lit(new Color(0.62f, 0.42f, 0.3f), 0.1f);
            for (int i = 0; i < 26; i++)
            {
                float a = Mathf.Lerp(-150, 150, i / 25f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * 8.6f, 0.25f, Mathf.Cos(a) * 8.6f);
                if (Mathf.Abs(a) < 0.25f) continue; // entrance gap
                ProcMesh.Prim(PrimitiveType.Capsule, "Sandbag", Bunker, p, new Vector3(0.5f, 0.55f, 0.4f), bag, new Vector3(0, a * Mathf.Rad2Deg, 90), true);
            }
            // airlock
            var lockT = new GameObject("Airlock").transform; lockT.SetParent(Bunker, false); lockT.localPosition = new Vector3(0, 0, 7.2f);
            ProcMesh.Prim(PrimitiveType.Cube, "Frame", lockT, new Vector3(0, 1.45f, -0.4f), new Vector3(2.8f, 2.9f, 2.4f), R.Panel);
            // door is OPEN: panel slid aside, lit doorway you walk into
            ProcMesh.Prim(PrimitiveType.Cube, "DoorPanelOpen", lockT, new Vector3(1.05f, 1.25f, 0.9f), new Vector3(1.4f, 2.3f, 0.1f), R.HullDark);
            var door = ProcMesh.Prim(PrimitiveType.Cube, "Door", lockT, new Vector3(0, 1.25f, 0.81f), new Vector3(1.4f, 2.3f, 0.02f), R.Emissive(new Color(0.75f, 0.8f, 0.85f), new Color(0.9f, 1.0f, 1.1f)));
            ProcMesh.Prim(PrimitiveType.Cube, "DoorLight", lockT, new Vector3(0, 2.6f, 0.85f), new Vector3(1.2f, 0.08f, 0.05f), R.Emissive(Color.black, new Color(0.3f, 2.4f, 1.0f)), null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Stripe", lockT, new Vector3(0, 0.25f, 0.82f), new Vector3(2.8f, 0.2f, 0.05f), R.Orange, null, false);
            var dl = new GameObject("DoorLamp").AddComponent<Light>(); dl.transform.SetParent(lockT, false); dl.transform.localPosition = new Vector3(0, 2.8f, 1.6f);
            dl.type = LightType.Point; dl.range = 9; dl.intensity = 3.5f; dl.color = new Color(0.4f, 1f, 0.7f);
            BunkerDoor = Interactable.Add(door, "Enter", "Bunker", null); BunkerDoor.Range = 4.5f;
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
            Util.PlaceModel("Tall_Dish__dish_", Bunker.TransformPoint(new Vector3(11, 0, 3)), 4.5f, 140, Bunker, false, true);

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
            var strip = R.Emissive(Color.black, new Color(1.6f, 1.7f, 1.9f));
            for (int i = -1; i <= 1; i += 2) ProcMesh.Prim(PrimitiveType.Cube, "Strip", t, new Vector3(i * 1.1f, Hh - 0.02f, 0), new Vector3(0.12f, 0.03f, D - 1), strip, null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "FloorStripe", t, new Vector3(0, 0.005f, 0), new Vector3(W - 1, 0.01f, 0.25f), R.Orange, null, false);

            InteriorLights.Clear();
            for (int i = -1; i <= 1; i += 2)
            {
                var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(t, false); l.transform.localPosition = new Vector3(i * 2.6f, Hh - 0.4f, 0);
                l.type = LightType.Point; l.range = 10; l.intensity = 2.2f; l.color = new Color(0.9f, 0.95f, 1f); l.shadows = LightShadows.None;
                InteriorLights.Add(l);
            }
            AlarmLight = new GameObject("Alarm").AddComponent<Light>(); AlarmLight.transform.SetParent(t, false); AlarmLight.transform.localPosition = new Vector3(0, Hh - 0.3f, -2);
            AlarmLight.type = LightType.Point; AlarmLight.range = 12; AlarmLight.intensity = 0; AlarmLight.color = new Color(1f, 0.15f, 0.08f);
            AlarmLight.gameObject.AddComponent<Blink>().Rate = 1.2f;

            // exit
            // exit door is OPEN: panel slid aside, Mars daylight in the doorway
            ProcMesh.Prim(PrimitiveType.Cube, "ExitPanelOpen", t, new Vector3(-3.2f + 1.0f, 1.15f, -D / 2 + 0.22f), new Vector3(1.3f, 2.3f, 0.1f), R.HullDark);
            var door = ProcMesh.Prim(PrimitiveType.Cube, "ExitDoor", t, new Vector3(-3.2f, 1.15f, -D / 2 + 0.16f), new Vector3(1.3f, 2.3f, 0.02f), R.Emissive(new Color(0.9f, 0.55f, 0.35f), new Color(1.6f, 0.85f, 0.5f)));
            ProcMesh.Prim(PrimitiveType.Cube, "ExitSign", t, new Vector3(-3.2f, 2.5f, -D / 2 + 0.15f), new Vector3(0.7f, 0.18f, 0.05f), R.Emissive(Color.black, new Color(0.3f, 2.2f, 0.9f)), null, false);
            ExitDoor = Interactable.Add(door, "Go outside", "Airlock", null);
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
