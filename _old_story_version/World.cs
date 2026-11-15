using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    public class Part
    {
        public string Name, Short; public GameObject Go, Beacon; public Interactable Use; public Question[] Qs;
        public bool Found, Installed; public Marker Marker; public Light Light;
    }

    /// <summary>Builds the whole playable world and exposes the important objects to the Director.</summary>
    public static class World
    {
        // ---- layout (world XZ, origin = crash site) ----
        public static readonly Vector2 CrashXZ = new Vector2(6, 18);
        public static readonly Vector2 ShelterXZ = new Vector2(46, -38);
        public static readonly Vector2 PadXZ = new Vector2(74, -4);
        public static readonly Vector2 ObservatoryXZ = new Vector2(-46, 42);
        public static readonly Vector2 RoverXZ = new Vector2(112, 84);
        public static readonly Vector2[] PartXZ = { new Vector2(-122, 108), new Vector2(124, 100), new Vector2(-112, -124), new Vector2(118, -132) };
        public static readonly Vector3 InteriorOrigin = new Vector3(0, -90, 0);

        public static GameObject Root, Interior;
        public static Terrain Terrain;
        public static readonly List<GameObject> Salvage = new List<GameObject>();
        public static GameObject ObsBlueprint, ObsBuilt; public static Interactable ObsBuild, TelescopeUse; public static Vector3 TelescopeEye; public static Transform[] ObsPieces;
        public static Interactable HabDoor, HabExit; public static Vector3 HabOutsideSpawn; public static float HabOutsideYaw;
        public static Vector3 InteriorSpawn; public static float InteriorYaw;
        public static Interactable Moxie, Crate, Bed, Medkit; public static GameObject MoxieGlow, HandbookProp; public static Light MoxieLight;
        public static MonitorScreen Monitor; public static readonly List<Light> InteriorLights = new List<Light>(); public static Light EmergencyLight;
        public static Part[] Parts;
        public static Transform Rocket; public static Interactable RocketInstall, LaunchConsole; public static GameObject[] RocketSlots;
        public static GameObject EarthSphere, MoonSphere;
        public static readonly List<GameObject> O2Cans = new List<GameObject>();
        public static Interactable RoverScan, HeliScan;
        public static Vector3 SpawnPos; public static float SpawnYaw;
        public static ParticleSystem AmbientDust;
        public static Transform WreckRoot;

        static Material holoCyan, holoOrange;

        public static void Build()
        {
            Root = new GameObject("World");
            holoCyan = R.Holo(new Color(0.3f, 0.9f, 1f), 1.4f, 0.12f);
            holoOrange = R.Holo(new Color(1f, 0.5f, 0.2f), 1.4f, 0.12f);

            Terrain = MarsTerrain.Build(R.Template("Terrain"));
            Terrain.transform.SetParent(Root.transform, true);
            Rocks();
            Wreck();
            Observatory();
            Habitat();
            LaunchPad();
            Robots();
            BuildParts();
            O2();
            BuildInterior();
            SkyObjects();
            FX.DustBanks(new Vector3(0, MarsTerrain.Base + 2, 0)).transform.SetParent(Root.transform, true);

            SpawnPos = MarsTerrain.Ground(-8, -4, 0.1f); SpawnYaw = 30;
        }

        static Vector3 G(Vector2 xz, float up = 0) => MarsTerrain.Ground(xz.x, xz.y, up);
        static Vector3 G(float x, float z, float up = 0) => MarsTerrain.Ground(x, z, up);

        // ------------------------------------------------------------------
        static void Rocks()
        {
            var parent = new GameObject("Rocks").transform; parent.SetParent(Root.transform);
            var meshes = new Mesh[8]; for (int i = 0; i < meshes.Length; i++) meshes[i] = ProcMesh.Rock(100 + i * 7, 2);
            var mat = R.Rock; mat.enableInstancing = true;
            var dark = R.LitTex("rock_boulder_dry_diff", "rock_boulder_dry_nor", new Color(0.45f, 0.30f, 0.24f), 1f, 0.1f, 0); dark.enableInstancing = true;
            var rng = new System.Random(9);
            float Rnd() => (float)rng.NextDouble();
            int placed = 0;
            for (int i = 0; i < 2600 && placed < 900; i++)
            {
                float x = Rnd() * 620 - 310, z = Rnd() * 620 - 310;
                var xz = new Vector2(x, z);
                if (Avoid(xz, 1)) continue;
                float big = Mathf.Pow(Rnd(), 7f);
                float s = Mathf.Lerp(0.18f, 1.2f, Mathf.Pow(Rnd(), 2.5f)) + big * 5f;
                // more rocks near craters' rims and on slopes
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
        }

        static bool Avoid(Vector2 xz, float pad)
        {
            if (Vector2.Distance(xz, Vector2.zero) < 34 + pad) return true;
            if (Vector2.Distance(xz, ShelterXZ) < 22 + pad) return true;
            if (Vector2.Distance(xz, PadXZ) < 18 + pad) return true;
            if (Vector2.Distance(xz, ObservatoryXZ) < 10 + pad) return true;
            if (Vector2.Distance(xz, RoverXZ) < 10 + pad) return true;
            foreach (var p in PartXZ) if (Vector2.Distance(xz, p) < 6 + pad) return true;
            return false;
        }

        // ------------------------------------------------------------------
        static void Wreck()
        {
            WreckRoot = new GameObject("CrashSite").transform; WreckRoot.SetParent(Root.transform);
            var ship = Util.PlaceModel("Ares_1__A_", G(CrashXZ, -1.6f), 34, 0, WreckRoot, true, true);
            ship.transform.rotation = Quaternion.Euler(0, 118, 0) * Quaternion.Euler(0, 0, 76);
            ship.transform.position = G(CrashXZ, 1.2f);
            foreach (var r in ship.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                foreach (var m in mats) if (m.HasProperty("baseColorFactor")) m.SetColor("baseColorFactor", m.GetColor("baseColorFactor") * new Color(0.55f, 0.5f, 0.48f));
                r.materials = mats;
            }
            // broken-off section + debris
            var rng = new System.Random(3);
            float Rnd() => (float)rng.NextDouble();
            var tank = ProcMesh.Prim(PrimitiveType.Cylinder, "BrokenTank", WreckRoot, Vector3.zero, new Vector3(3.2f, 4.5f, 3.2f), R.Scorched);
            tank.transform.position = G(CrashXZ.x - 22, CrashXZ.y - 12, 1.1f); tank.transform.rotation = Quaternion.Euler(84, 30, 0);
            var ring = ProcMesh.Prim(PrimitiveType.Cylinder, "Ring", WreckRoot, Vector3.zero, new Vector3(3.6f, 0.3f, 3.6f), R.HullDark);
            ring.transform.position = G(CrashXZ.x - 18, CrashXZ.y - 9, 1.4f); ring.transform.rotation = Quaternion.Euler(70, 50, 10);
            for (int i = 0; i < 38; i++)
            {
                float a = Rnd() * Mathf.PI * 2, d = 6 + Rnd() * 34;
                var p = CrashXZ + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                bool plate = Rnd() < 0.7f;
                var g = ProcMesh.Prim(plate ? PrimitiveType.Cube : PrimitiveType.Cylinder, "Debris", WreckRoot, Vector3.zero,
                    plate ? new Vector3(0.6f + Rnd() * 2.4f, 0.05f + Rnd() * 0.08f, 0.5f + Rnd() * 1.8f) : new Vector3(0.3f + Rnd() * 0.6f, 0.4f + Rnd() * 1.2f, 0.3f + Rnd() * 0.6f),
                    Rnd() < 0.5f ? R.Scorched : (Rnd() < 0.5f ? R.Hull : R.HullDark), null, d > 12);
                g.transform.position = G(p, 0.1f);
                g.transform.rotation = Quaternion.Euler(Rnd() * 50 - 25, Rnd() * 360, Rnd() * 50 - 25);
            }
            // fire & smoke landmarks
            Vector3 hot = ship.transform.position + Vector3.up * 0.5f;
            FX.Fire(G(CrashXZ.x - 4, CrashXZ.y + 3, 0.6f), 1.6f, WreckRoot);
            FX.Fire(G(CrashXZ.x + 7, CrashXZ.y - 5, 0.4f), 1.1f, WreckRoot);
            FX.Fire(G(CrashXZ.x - 20, CrashXZ.y - 11, 0.8f), 1.0f, WreckRoot);
            FX.Smoke(G(CrashXZ.x - 4, CrashXZ.y + 3, 2f), 2.2f, WreckRoot, 0.09f);
            FX.Smoke(G(CrashXZ.x + 7, CrashXZ.y - 5, 2f), 1.6f, WreckRoot, 0.14f);
            FX.Smoke(G(CrashXZ.x - 20, CrashXZ.y - 11, 2f), 1.4f, WreckRoot, 0.12f);
            FX.Sparks(G(CrashXZ.x + 2, CrashXZ.y, 2.2f), WreckRoot);
            var fl = new GameObject("FireLight").AddComponent<Light>(); fl.transform.SetParent(WreckRoot); fl.transform.position = G(CrashXZ.x - 4, CrashXZ.y + 3, 2f);
            fl.type = LightType.Point; fl.range = 18; fl.intensity = 6; fl.color = new Color(1f, 0.55f, 0.2f); fl.gameObject.AddComponent<Flicker>();
            if (AudioManager.I) AudioManager.I.Loop("fire", 0.7f, fl.transform, 1, 30);

            // salvage items
            Salvage.Clear();
            AddSalvage("Hull Plate", "hull", G(CrashXZ.x - 12, CrashXZ.y - 2, 0.15f));
            AddSalvage("Hull Plate", "hull", G(CrashXZ.x + 13, CrashXZ.y + 9, 0.15f));
            AddSalvage("Optical Lens", "lens", G(CrashXZ.x - 6, CrashXZ.y - 14, 0.25f));
            AddSalvage("Optical Lens", "lens", G(CrashXZ.x + 16, CrashXZ.y - 6, 0.25f));
            AddSalvage("Wiring Bundle", "wire", G(CrashXZ.x - 16, CrashXZ.y + 10, 0.2f));
            AddSalvage("Solar Panel", "solar", G(CrashXZ.x + 2, CrashXZ.y - 20, 0.2f));
        }

        static void AddSalvage(string label, string kind, Vector3 pos)
        {
            var g = new GameObject("Salvage_" + label); g.transform.SetParent(WreckRoot); g.transform.position = pos;
            switch (kind)
            {
                case "hull":
                    ProcMesh.Prim(PrimitiveType.Cube, "Plate", g.transform, new Vector3(0, 0.05f, 0), new Vector3(1.4f, 0.06f, 0.9f), R.Hull, new Vector3(6, 20, 4));
                    ProcMesh.Prim(PrimitiveType.Cube, "Strip", g.transform, new Vector3(0, 0.1f, 0.3f), new Vector3(1.3f, 0.03f, 0.08f), R.Emissive(Color.black, new Color(2.2f, 0.8f, 0.2f)), new Vector3(6, 20, 4), false);
                    break;
                case "lens":
                    ProcMesh.Prim(PrimitiveType.Cylinder, "Barrel", g.transform, new Vector3(0, 0.15f, 0), new Vector3(0.35f, 0.18f, 0.35f), R.HullDark, new Vector3(80, 30, 0));
                    ProcMesh.Prim(PrimitiveType.Sphere, "Glass", g.transform, new Vector3(0.05f, 0.15f, 0.17f), new Vector3(0.3f, 0.3f, 0.08f), R.Emissive(new Color(0.1f, 0.2f, 0.3f), new Color(0.4f, 1.2f, 1.8f), 0.95f), new Vector3(0, 30, 0), false);
                    break;
                case "wire":
                    for (int i = 0; i < 4; i++)
                        ProcMesh.Prim(PrimitiveType.Cylinder, "Wire", g.transform, new Vector3(i * 0.08f - 0.12f, 0.06f, 0), new Vector3(0.05f, 0.6f, 0.05f), R.Emissive(new Color(0.8f, 0.3f, 0.1f), new Color(0.9f, 0.3f, 0.05f)), new Vector3(90, 10 * i, 0), false);
                    ProcMesh.Prim(PrimitiveType.Cylinder, "Spool", g.transform, new Vector3(0, 0.12f, 0), new Vector3(0.45f, 0.12f, 0.45f), R.HullDark);
                    break;
                case "solar":
                    ProcMesh.Prim(PrimitiveType.Cube, "Panel", g.transform, new Vector3(0, 0.08f, 0), new Vector3(1.8f, 0.05f, 1.1f), R.Emissive(new Color(0.05f, 0.08f, 0.2f), new Color(0.05f, 0.15f, 0.5f), 0.9f), new Vector3(10, 40, 0));
                    ProcMesh.Prim(PrimitiveType.Cube, "Frame", g.transform, new Vector3(0, 0.05f, 0), new Vector3(1.9f, 0.04f, 1.2f), R.Hull, new Vector3(10, 40, 0), false);
                    break;
            }
            var ring = ProcMesh.Prim(PrimitiveType.Cylinder, "HoloRing", g.transform, new Vector3(0, 0.02f, 0), new Vector3(1.6f, 0.01f, 1.6f), holoOrange, null, false);
            ring.AddComponent<Spin>().Speed = 40;
            var l = new GameObject("Glow").AddComponent<Light>(); l.transform.SetParent(g.transform, false); l.transform.localPosition = Vector3.up * 0.8f;
            l.type = LightType.Point; l.range = 3.5f; l.intensity = 2.5f; l.color = new Color(1f, 0.6f, 0.3f);
            g.AddComponent<Bob>();
            Interactable.Add(g, "Salvage", label, null);
            Salvage.Add(g);
        }

        // ------------------------------------------------------------------
        static void Observatory()
        {
            var site = new GameObject("Observatory").transform; site.SetParent(Root.transform);
            Vector3 c = G(ObservatoryXZ, 0);
            site.position = c;
            ObsBlueprint = new GameObject("Blueprint"); ObsBlueprint.transform.SetParent(site, false);
            ObsBuilt = new GameObject("Built"); ObsBuilt.transform.SetParent(site, false);

            // built version (pieces animate in later)
            var dome = ProcMesh.MeshObj("Dome", ProcMesh.Dome(28, 10, 0.12f), R.Hull, ObsBuilt.transform);
            dome.transform.localScale = new Vector3(3.4f, 3.0f, 3.4f); dome.transform.localPosition = new Vector3(0, 0.6f, 0);
            dome.transform.localEulerAngles = new Vector3(0, 200, 0);
            var mc = dome.AddComponent<MeshCollider>(); mc.sharedMesh = dome.GetComponent<MeshFilter>().sharedMesh;
            var baseRing = ProcMesh.Prim(PrimitiveType.Cylinder, "Base", ObsBuilt.transform, new Vector3(0, 0.3f, 0), new Vector3(7.2f, 0.3f, 7.2f), R.HullDark);
            var tarp = ProcMesh.Prim(PrimitiveType.Cube, "Tarp", ObsBuilt.transform, new Vector3(3.8f, 0.9f, 0.5f), new Vector3(0.05f, 1.6f, 2.6f), R.Orange, new Vector3(0, 0, 12));
            var mount = ProcMesh.Prim(PrimitiveType.Cylinder, "Mount", ObsBuilt.transform, new Vector3(0, 1.2f, 0), new Vector3(0.25f, 0.9f, 0.25f), R.HullDark);
            var tube = ProcMesh.Prim(PrimitiveType.Cylinder, "Tube", ObsBuilt.transform, new Vector3(0, 2.4f, 0.5f), new Vector3(0.5f, 1.5f, 0.5f), R.Hull, new Vector3(-55, 0, 0));
            var lens = ProcMesh.Prim(PrimitiveType.Cylinder, "Lens", tube.transform, new Vector3(0, 1.0f, 0), new Vector3(0.9f, 0.02f, 0.9f), R.Emissive(new Color(0.1f, 0.2f, 0.3f), new Color(0.3f, 0.9f, 1.4f), 0.95f), null, false);
            var eyep = ProcMesh.Prim(PrimitiveType.Cylinder, "Eyepiece", ObsBuilt.transform, new Vector3(0, 1.55f, -0.55f), new Vector3(0.18f, 0.25f, 0.18f), R.HullDark, new Vector3(-55, 0, 0));
            ObsPieces = new[] { baseRing.transform, dome.transform, tarp.transform, mount.transform, tube.transform, eyep.transform };
            TelescopeEye = site.TransformPoint(new Vector3(0, 1.75f, -0.9f));
            TelescopeUse = Interactable.Add(eyep, "Look through", "Telescope", null);
            TelescopeUse.Range = 4f;
            ObsBuilt.SetActive(false);

            // holographic blueprint
            var bd = ProcMesh.MeshObj("HoloDome", ProcMesh.Dome(28, 10, 0.12f), holoCyan, ObsBlueprint.transform);
            bd.transform.localScale = new Vector3(3.4f, 3.0f, 3.4f); bd.transform.localPosition = new Vector3(0, 0.6f, 0);
            ProcMesh.Prim(PrimitiveType.Cylinder, "HoloTube", ObsBlueprint.transform, new Vector3(0, 2.4f, 0.5f), new Vector3(0.5f, 1.5f, 0.5f), holoCyan, new Vector3(-55, 0, 0), false);
            ProcMesh.Prim(PrimitiveType.Cylinder, "HoloBase", ObsBlueprint.transform, new Vector3(0, 0.3f, 0), new Vector3(7.2f, 0.3f, 7.2f), holoCyan, null, false);
            var trig = ProcMesh.Prim(PrimitiveType.Cube, "BuildTrigger", ObsBlueprint.transform, new Vector3(0, 1.2f, 0), new Vector3(7f, 2.4f, 7f), holoCyan, null, true);
            trig.GetComponent<Renderer>().enabled = false; trig.GetComponent<Collider>().isTrigger = true;
            ObsBuild = Interactable.Add(trig, "Build", "Observatory", null, 2.2f);
            ObsBuild.Range = 6;
            ObsBlueprint.SetActive(false);
        }

        // ------------------------------------------------------------------
        static void Habitat()
        {
            var hab = new GameObject("Habitat").transform; hab.SetParent(Root.transform);
            Vector3 c = G(ShelterXZ, 0); hab.position = c;
            var model = Util.PlaceModel("Habitat_Demonstration_Unit__part_1_", c, 11, 25, hab, false, true);
            var b = Util.Bounds(model);
            // airlock in front (toward crash site)
            Vector3 toCrash = (new Vector3(0, 0, 0) - c); toCrash.y = 0; toCrash.Normalize();
            float radius = Mathf.Max(b.extents.x, b.extents.z);
            Vector3 lockPos = c + toCrash * (radius + 1.4f);
            lockPos.y = MarsTerrain.H(lockPos.x, lockPos.z);
            var airlock = new GameObject("Airlock").transform; airlock.SetParent(hab); airlock.position = lockPos;
            airlock.rotation = Quaternion.LookRotation(toCrash);
            ProcMesh.Prim(PrimitiveType.Cube, "Frame", airlock, new Vector3(0, 1.4f, -0.6f), new Vector3(2.4f, 2.9f, 2.0f), R.Panel);
            var door = ProcMesh.Prim(PrimitiveType.Cube, "Door", airlock, new Vector3(0, 1.25f, 0.42f), new Vector3(1.3f, 2.2f, 0.1f), R.HullDark);
            ProcMesh.Prim(PrimitiveType.Cube, "DoorLight", airlock, new Vector3(0, 2.55f, 0.45f), new Vector3(1.1f, 0.08f, 0.05f), R.Emissive(Color.black, new Color(0.3f, 2.2f, 0.9f)), null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Stripe", airlock, new Vector3(0, 0.25f, 0.42f), new Vector3(2.4f, 0.2f, 0.05f), R.Orange, null, false);
            var dl = new GameObject("DoorLamp").AddComponent<Light>(); dl.transform.SetParent(airlock, false); dl.transform.localPosition = new Vector3(0, 2.7f, 1.2f);
            dl.type = LightType.Point; dl.range = 8; dl.intensity = 3; dl.color = new Color(0.4f, 1f, 0.7f);
            HabDoor = Interactable.Add(door, "Enter", "Habitat", null);
            HabOutsideSpawn = lockPos + toCrash * 2.6f + Vector3.up * 0.1f;
            HabOutsideYaw = Quaternion.LookRotation(toCrash).eulerAngles.y;

            // solar array
            for (int i = 0; i < 4; i++)
            {
                Vector3 side = Vector3.Cross(Vector3.up, toCrash);
                Vector3 p = c - toCrash * (radius + 4) + side * (i - 1.5f) * 3.2f;
                p.y = MarsTerrain.H(p.x, p.z);
                var post = ProcMesh.Prim(PrimitiveType.Cylinder, "Post", hab, Vector3.zero, new Vector3(0.12f, 0.7f, 0.12f), R.HullDark);
                post.transform.position = p + Vector3.up * 0.7f;
                var pan = ProcMesh.Prim(PrimitiveType.Cube, "Solar", hab, Vector3.zero, new Vector3(2.8f, 0.06f, 1.7f), R.Emissive(new Color(0.04f, 0.07f, 0.18f), new Color(0.02f, 0.06f, 0.2f), 0.92f));
                pan.transform.position = p + Vector3.up * 1.5f; pan.transform.rotation = Quaternion.LookRotation(toCrash) * Quaternion.Euler(-30, 0, 0);
            }
            var dish = Util.PlaceModel("Tall_Dish__dish_", c + Vector3.Cross(Vector3.up, toCrash) * (radius + 3), 4f, 140, hab, false, true);
            dish.transform.position = new Vector3(dish.transform.position.x, MarsTerrain.H(dish.transform.position.x, dish.transform.position.z), dish.transform.position.z);
        }

        // ------------------------------------------------------------------
        static void LaunchPad()
        {
            var pad = new GameObject("LaunchPad").transform; pad.SetParent(Root.transform);
            Vector3 c = G(PadXZ, 0); pad.position = c;
            ProcMesh.Prim(PrimitiveType.Cylinder, "Pad", pad, new Vector3(0, 0.25f, 0), new Vector3(14, 0.5f, 14), R.LitTex("metal_plate_diff", "metal_plate_nor", new Color(0.35f, 0.33f, 0.32f), 6, 0.3f, 0.3f));
            ProcMesh.Prim(PrimitiveType.Cylinder, "PadRing", pad, new Vector3(0, 0.52f, 0), new Vector3(6.5f, 0.02f, 6.5f), R.Emissive(Color.black, new Color(2f, 0.7f, 0.2f)), null, false);
            ProcMesh.Prim(PrimitiveType.Cylinder, "Flame", pad, new Vector3(0, 0.53f, 0), new Vector3(6f, 0.02f, 6f), R.Lit(new Color(0.08f, 0.07f, 0.07f), 0.2f), null, false);
            // gantry tower
            var tower = new GameObject("Tower").transform; tower.SetParent(pad, false); tower.localPosition = new Vector3(5.2f, 0, 0);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2) * 1.6f - 0.8f, z = (i / 2) * 1.6f - 0.8f;
                ProcMesh.Prim(PrimitiveType.Cube, "Leg", tower, new Vector3(x, 12, z), new Vector3(0.18f, 24, 0.18f), R.Orange);
            }
            for (int k = 0; k < 12; k++)
            {
                ProcMesh.Prim(PrimitiveType.Cube, "Brace", tower, new Vector3(0, 1 + k * 2, -0.8f), new Vector3(1.8f, 0.1f, 0.1f), R.HullDark, new Vector3(0, 0, k % 2 == 0 ? 35 : -35), false);
                ProcMesh.Prim(PrimitiveType.Cube, "Brace", tower, new Vector3(0, 1 + k * 2, 0.8f), new Vector3(1.8f, 0.1f, 0.1f), R.HullDark, new Vector3(0, 0, k % 2 == 0 ? -35 : 35), false);
            }
            ProcMesh.Prim(PrimitiveType.Cube, "Arm", tower, new Vector3(-2.2f, 18, 0), new Vector3(3.2f, 0.4f, 0.8f), R.HullDark);
            var tl = new GameObject("TowerLight").AddComponent<Light>(); tl.transform.SetParent(tower, false); tl.transform.localPosition = new Vector3(0, 24.5f, 0);
            tl.type = LightType.Point; tl.range = 12; tl.intensity = 4; tl.color = new Color(1, 0.2f, 0.15f); tl.gameObject.AddComponent<Blink>();

            var rocket = Util.PlaceModel("Redstone_3__Freedom_7_", c + Vector3.up * 0.55f, 25, 0, pad, true, true);
            Rocket = rocket.transform;
            // install slots: holographic boxes on the rocket
            RocketSlots = new GameObject[4];
            var rb = Util.Bounds(rocket);
            for (int i = 0; i < 4; i++)
            {
                var s = ProcMesh.Prim(PrimitiveType.Cube, "Slot" + i, pad, Vector3.zero, new Vector3(0.9f, 0.9f, 0.9f), holoOrange, null, false);
                float ang = i * 90 + 45;
                Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                s.transform.position = new Vector3(c.x, c.y + 2.0f + i * 0.0f, c.z) + dir * (Mathf.Max(rb.extents.x, rb.extents.z) + 0.3f);
                s.SetActive(false);
                RocketSlots[i] = s;
            }
            var console = new GameObject("Console").transform; console.SetParent(pad, false); console.localPosition = new Vector3(-6.2f, 0.5f, -3f);
            console.localRotation = Quaternion.Euler(0, 60, 0);
            ProcMesh.Prim(PrimitiveType.Cube, "Desk", console, new Vector3(0, 0.55f, 0), new Vector3(1.6f, 1.1f, 0.8f), R.Panel);
            var scr = ProcMesh.Prim(PrimitiveType.Cube, "Screen", console, new Vector3(0, 1.35f, 0.1f), new Vector3(1.4f, 0.6f, 0.06f), R.Emissive(Color.black, new Color(0.3f, 1.6f, 0.7f)), new Vector3(-20, 0, 0));
            LaunchConsole = Interactable.Add(console.gameObject, "Use", "Launch Console", null);
            RocketInstall = Interactable.Add(rocket, "Install modules on", "Escape Rocket", null, 1.5f);
            RocketInstall.Range = 9;
            RocketInstall.enabled = false; LaunchConsole.enabled = false;
        }

        // ------------------------------------------------------------------
        static void Robots()
        {
            var site = new GameObject("RoverSite").transform; site.SetParent(Root.transform);
            var rover = Util.PlaceModel("Mars_2020_Perseverance_Rover", G(RoverXZ), 3.0f, 210, site, false, true);
            var heli = Util.PlaceModel("Ingenuity_Mars_Helicopter", G(RoverXZ.x + 6, RoverXZ.y - 4), 1.2f, 30, site, false, true);
            RoverScan = Interactable.Add(rover, "Scan", "Perseverance Rover", null); RoverScan.Range = 7;
            HeliScan = Interactable.Add(heli, "Scan", "Ingenuity Helicopter", null); HeliScan.Range = 4;
            // wheel tracks
            for (int i = 0; i < 40; i++)
            {
                var p = RoverXZ + new Vector2(-i * 1.2f, -i * 0.7f) + new Vector2(Mathf.Sin(i * 0.2f) * 2, 0);
                foreach (float off in new[] { -1.1f, 1.1f })
                {
                    var t = ProcMesh.Prim(PrimitiveType.Cube, "Track", site, Vector3.zero, new Vector3(0.4f, 0.02f, 1.2f), R.Lit(new Color(0.38f, 0.22f, 0.15f), 0.05f), null, false);
                    var q = p + new Vector2(0.7f, -1.2f).normalized * off;
                    t.transform.position = G(q, 0.0f); t.transform.rotation = Quaternion.Euler(0, 60, 0);
                    t.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        // ------------------------------------------------------------------
        static void BuildParts()
        {
            var qs = HandbookContent.Questions;
            Question Q(string id) { foreach (var q in qs) if (q.Id == id) return q; return null; }
            Parts = new[]
            {
                new Part { Name = "Fuel Pump Valve", Short = "VALVE", Qs = new[]{ Q("valve1"), Q("valve2") } },
                new Part { Name = "Flight Computer", Short = "COMPUTER", Qs = new[]{ Q("board1"), Q("board2") } },
                new Part { Name = "Star Tracker", Short = "TRACKER", Qs = new[]{ Q("tracker1"), Q("tracker2") } },
                new Part { Name = "Radio Antenna", Short = "ANTENNA", Qs = new[]{ Q("antenna1"), Q("antenna2") } },
            };
            var parent = new GameObject("Parts").transform; parent.SetParent(Root.transform);
            for (int i = 0; i < Parts.Length; i++)
            {
                var p = Parts[i];
                var g = new GameObject("Part_" + p.Short); g.transform.SetParent(parent);
                g.transform.position = G(PartXZ[i], 0);
                var crate = ProcMesh.Prim(PrimitiveType.Cube, "Case", g.transform, new Vector3(0, 0.35f, 0), new Vector3(1.1f, 0.7f, 0.8f), R.Hull, new Vector3(8, 25 + i * 30, -6));
                ProcMesh.Prim(PrimitiveType.Cube, "Band", crate.transform, new Vector3(0, 0, 0.51f), new Vector3(1.02f, 0.18f, 0.02f), R.Orange, null, false);
                switch (i)
                {
                    case 0: ProcMesh.Prim(PrimitiveType.Cylinder, "Valve", g.transform, new Vector3(0, 0.95f, 0), new Vector3(0.35f, 0.25f, 0.35f), R.HullDark); ProcMesh.Prim(PrimitiveType.Cylinder, "Wheel", g.transform, new Vector3(0, 1.25f, 0), new Vector3(0.6f, 0.04f, 0.6f), R.Orange, null, false); break;
                    case 1: ProcMesh.Prim(PrimitiveType.Cube, "Board", g.transform, new Vector3(0, 0.78f, 0), new Vector3(0.8f, 0.05f, 0.55f), R.Emissive(new Color(0.05f, 0.25f, 0.1f), new Color(0.1f, 0.8f, 0.3f)), new Vector3(0, 20, 0), false); break;
                    case 2: ProcMesh.Prim(PrimitiveType.Cylinder, "Lens", g.transform, new Vector3(0, 1.0f, 0), new Vector3(0.35f, 0.3f, 0.35f), R.HullDark, new Vector3(30, 0, 0)); ProcMesh.Prim(PrimitiveType.Sphere, "Eye", g.transform, new Vector3(0, 1.2f, 0.12f), new Vector3(0.25f, 0.25f, 0.1f), R.CyanGlow, new Vector3(30, 0, 0), false); break;
                    case 3: var d = Util.PlaceModel("Tall_Dish__dish_", g.transform.position + Vector3.up * 0.7f, 1.4f, 40, g.transform, false, false); break;
                }
                // beacon beam
                var beam = ProcMesh.Prim(PrimitiveType.Cylinder, "Beacon", g.transform, new Vector3(0, 60, 0), new Vector3(0.9f, 60, 0.9f), R.Holo(new Color(1f, 0.55f, 0.2f), 1.2f, 0.25f, 1f), null, false);
                beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p.Beacon = beam;
                var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(g.transform, false); l.transform.localPosition = new Vector3(0, 2, 0);
                l.type = LightType.Point; l.range = 9; l.intensity = 4; l.color = new Color(1f, 0.6f, 0.3f); p.Light = l;
                var ring = ProcMesh.Prim(PrimitiveType.Cylinder, "HoloRing", g.transform, new Vector3(0, 0.03f, 0), new Vector3(2.4f, 0.01f, 2.4f), holoOrange, null, false);
                ring.AddComponent<Spin>().Speed = 30;
                p.Use = Interactable.Add(g, "Inspect", p.Name, null);
                p.Go = g;
                g.SetActive(false);
            }
        }

        static void O2()
        {
            var parent = new GameObject("O2").transform; parent.SetParent(Root.transform);
            Vector2[] spots = { new Vector2(-60, 70), new Vector2(60, 60), new Vector2(-70, -70), new Vector2(70, -80), new Vector2(-115, 95), new Vector2(118, 92), new Vector2(-104, -114), new Vector2(110, -120), new Vector2(0, 90), new Vector2(0, -95) };
            foreach (var s in spots)
            {
                var g = new GameObject("O2Canister"); g.transform.SetParent(parent); g.transform.position = G(s, 0);
                ProcMesh.Prim(PrimitiveType.Cylinder, "Tank", g.transform, new Vector3(0, 0.45f, 0), new Vector3(0.35f, 0.4f, 0.35f), R.Lit(new Color(0.85f, 0.9f, 0.95f), 0.7f, 0.3f), new Vector3(0, 0, 10));
                ProcMesh.Prim(PrimitiveType.Cylinder, "Band", g.transform, new Vector3(0, 0.55f, 0), new Vector3(0.37f, 0.05f, 0.37f), R.CyanGlow, new Vector3(0, 0, 10), false);
                var l = new GameObject("L").AddComponent<Light>(); l.transform.SetParent(g.transform, false); l.transform.localPosition = Vector3.up; l.type = LightType.Point; l.range = 4; l.intensity = 2; l.color = Pal.Cyan;
                g.AddComponent<Bob>();
                Interactable.Add(g, "Take", "O<sub>2</sub> Canister", null);
                O2Cans.Add(g);
                g.SetActive(false);
            }
        }

        // ------------------------------------------------------------------
        static void BuildInterior()
        {
            Interior = new GameObject("HabitatInterior");
            var t = Interior.transform; t.position = InteriorOrigin;
            float W = 10, D = 8, Hh = 3.4f;
            var wallMat = R.Panel; var floorMat = R.Floor;
            ProcMesh.Prim(PrimitiveType.Cube, "Floor", t, new Vector3(0, -0.1f, 0), new Vector3(W, 0.2f, D), floorMat);
            ProcMesh.Prim(PrimitiveType.Cube, "Ceiling", t, new Vector3(0, Hh + 0.1f, 0), new Vector3(W, 0.2f, D), R.HullDark);
            ProcMesh.Prim(PrimitiveType.Cube, "WallN", t, new Vector3(0, Hh / 2, D / 2), new Vector3(W, Hh, 0.2f), wallMat);
            ProcMesh.Prim(PrimitiveType.Cube, "WallS", t, new Vector3(0, Hh / 2, -D / 2), new Vector3(W, Hh, 0.2f), wallMat);
            ProcMesh.Prim(PrimitiveType.Cube, "WallE", t, new Vector3(W / 2, Hh / 2, 0), new Vector3(0.2f, Hh, D), wallMat);
            ProcMesh.Prim(PrimitiveType.Cube, "WallW", t, new Vector3(-W / 2, Hh / 2, 0), new Vector3(0.2f, Hh, D), wallMat);
            // ribs + light strips
            for (int i = -2; i <= 2; i++)
            {
                ProcMesh.Prim(PrimitiveType.Cube, "Rib", t, new Vector3(i * 2.2f, Hh - 0.05f, 0), new Vector3(0.25f, 0.18f, D), R.HullDark, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "RibN", t, new Vector3(i * 2.2f, Hh / 2, D / 2 - 0.12f), new Vector3(0.25f, Hh, 0.08f), R.HullDark, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "RibS", t, new Vector3(i * 2.2f, Hh / 2, -D / 2 + 0.12f), new Vector3(0.25f, Hh, 0.08f), R.HullDark, null, false);
            }
            var strip = R.Emissive(Color.black, new Color(1.6f, 1.7f, 1.9f));
            for (int i = -1; i <= 1; i += 2) ProcMesh.Prim(PrimitiveType.Cube, "Strip", t, new Vector3(i * 1.1f, Hh - 0.02f, 0), new Vector3(0.12f, 0.03f, D - 1), strip, null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "FloorStripe", t, new Vector3(0, 0.005f, 0), new Vector3(W - 1, 0.01f, 0.25f), R.Orange, null, false);

            // lights
            InteriorLights.Clear();
            for (int i = -1; i <= 1; i += 2)
            {
                var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(t, false); l.transform.localPosition = new Vector3(i * 2.6f, Hh - 0.4f, 0);
                l.type = LightType.Point; l.range = 9; l.intensity = 0; l.color = new Color(0.9f, 0.95f, 1f); l.shadows = LightShadows.None;
                InteriorLights.Add(l);
            }
            EmergencyLight = new GameObject("Emergency").AddComponent<Light>(); EmergencyLight.transform.SetParent(t, false); EmergencyLight.transform.localPosition = new Vector3(0, Hh - 0.3f, -2);
            EmergencyLight.type = LightType.Point; EmergencyLight.range = 12; EmergencyLight.intensity = 3; EmergencyLight.color = new Color(1f, 0.12f, 0.08f);
            EmergencyLight.gameObject.AddComponent<Blink>().Rate = 1.2f;

            // exit door (south wall)
            var door = ProcMesh.Prim(PrimitiveType.Cube, "ExitDoor", t, new Vector3(-3.2f, 1.15f, -D / 2 + 0.15f), new Vector3(1.3f, 2.3f, 0.1f), R.HullDark);
            ProcMesh.Prim(PrimitiveType.Cube, "ExitSign", t, new Vector3(-3.2f, 2.5f, -D / 2 + 0.15f), new Vector3(0.7f, 0.18f, 0.05f), R.Emissive(Color.black, new Color(0.3f, 2.2f, 0.9f)), null, false);
            HabExit = Interactable.Add(door, "Exit to", "Surface", null);
            InteriorSpawn = t.TransformPoint(new Vector3(-3.2f, 0.05f, -D / 2 + 1.4f)); InteriorYaw = 0;

            // MOXIE unit (east wall)
            var mox = new GameObject("MOXIE").transform; mox.SetParent(t, false); mox.localPosition = new Vector3(W / 2 - 0.9f, 0, 1.2f); mox.localRotation = Quaternion.Euler(0, -90, 0);
            var oru = Util.PlaceModel("Orbital_Replacement_Unit", mox.position, 1.3f, -90, mox, false, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Body", mox, new Vector3(0, 0.55f, 0), new Vector3(1.2f, 1.1f, 0.9f), R.Panel);
            ProcMesh.Prim(PrimitiveType.Cube, "Label", mox, new Vector3(0, 0.9f, 0.46f), new Vector3(0.7f, 0.14f, 0.02f), R.Orange, null, false);
            MoxieGlow = new GameObject("Glow"); MoxieGlow.transform.SetParent(mox, false);
            for (int i = 0; i < 3; i++)
                ProcMesh.Prim(PrimitiveType.Cylinder, "Tube", MoxieGlow.transform, new Vector3(-0.35f + i * 0.35f, 1.55f, 0), new Vector3(0.16f, 0.45f, 0.16f), R.CyanGlow, null, false);
            MoxieGlow.SetActive(false);
            for (int i = 0; i < 3; i++)
                ProcMesh.Prim(PrimitiveType.Cylinder, "TubeGlass", mox, new Vector3(-0.35f + i * 0.35f, 1.55f, 0), new Vector3(0.2f, 0.46f, 0.2f), R.Lit(new Color(0.3f, 0.35f, 0.4f), 0.9f, 0.2f), null, false);
            MoxieLight = new GameObject("MoxieLight").AddComponent<Light>(); MoxieLight.transform.SetParent(mox, false); MoxieLight.transform.localPosition = new Vector3(0, 1.6f, 0.8f);
            MoxieLight.type = LightType.Point; MoxieLight.range = 5; MoxieLight.intensity = 0; MoxieLight.color = Pal.Cyan;
            // battery bank + cable
            ProcMesh.Prim(PrimitiveType.Cube, "Battery", t, new Vector3(W / 2 - 0.6f, 0.45f, -0.6f), new Vector3(0.8f, 0.9f, 1.2f), R.HullDark);
            ProcMesh.Prim(PrimitiveType.Cube, "BatLed", t, new Vector3(W / 2 - 1.01f, 0.75f, -0.6f), new Vector3(0.02f, 0.08f, 0.6f), R.Emissive(Color.black, new Color(2, 1.2f, 0.2f)), null, false);
            Moxie = Interactable.Add(mox.gameObject, "Connect power to", "MOXIE Oxygen Generator", null, 2f);

            // monitor (north wall)
            Monitor = MonitorScreen.Create(t, new Vector3(0.8f, 1.75f, D / 2 - 0.25f), new Vector3(0, 0, 0), 2.6f);
            Monitor.transform.localRotation = Quaternion.Euler(0, 180, 0);
            Monitor.transform.localRotation = Quaternion.identity;

            // desk + helmet
            ProcMesh.Prim(PrimitiveType.Cube, "Desk", t, new Vector3(0.8f, 0.75f, D / 2 - 0.8f), new Vector3(2.6f, 0.08f, 0.9f), R.Panel);
            ProcMesh.Prim(PrimitiveType.Cube, "DeskLeg", t, new Vector3(0.8f, 0.37f, D / 2 - 0.8f), new Vector3(2.4f, 0.74f, 0.1f), R.HullDark);
            Util.PlaceModel("Helmet", t.TransformPoint(new Vector3(-0.1f, 0.8f, D / 2 - 0.8f)), 0.42f, 160, t, false, false);
            Util.PlaceModel("Wrench", t.TransformPoint(new Vector3(1.6f, 0.8f, D / 2 - 0.7f)), 0.35f, 70, t, false, false);

            // bunk (west)
            ProcMesh.Prim(PrimitiveType.Cube, "Bunk", t, new Vector3(-W / 2 + 0.6f, 0.45f, 1.8f), new Vector3(1.0f, 0.3f, 2.2f), R.HullDark);
            var mattress = ProcMesh.Prim(PrimitiveType.Cube, "Mattress", t, new Vector3(-W / 2 + 0.6f, 0.66f, 1.8f), new Vector3(0.9f, 0.14f, 2.1f), R.Lit(new Color(0.2f, 0.32f, 0.45f), 0.1f));
            Bed = Interactable.Add(mattress, "Rest until", "Sunrise", null);

            // cargo crate with the Handbook
            var crateRoot = new GameObject("Crate").transform; crateRoot.SetParent(t, false); crateRoot.localPosition = new Vector3(-W / 2 + 1.0f, 0, -1.4f);
            Util.PlaceModel("Crew_Lock_Bag", crateRoot.position, 0.9f, 20, crateRoot, false, false);
            ProcMesh.Prim(PrimitiveType.Cube, "CrateBox", crateRoot, new Vector3(0.9f, 0.35f, 0), new Vector3(0.9f, 0.7f, 0.7f), R.Orange);
            HandbookProp = ProcMesh.Prim(PrimitiveType.Cube, "Handbook", crateRoot, new Vector3(0.9f, 0.74f, 0), new Vector3(0.32f, 0.07f, 0.42f), R.Lit(new Color(0.35f, 0.16f, 0.08f), 0.3f), new Vector3(0, 15, 0), false);
            Crate = Interactable.Add(crateRoot.gameObject, "Search", "Cargo Crate", null);

            // medkit
            var med = ProcMesh.Prim(PrimitiveType.Cube, "Medkit", t, new Vector3(-1.4f, 1.5f, D / 2 - 0.15f), new Vector3(0.5f, 0.35f, 0.15f), R.Lit(new Color(0.9f, 0.9f, 0.92f), 0.4f));
            ProcMesh.Prim(PrimitiveType.Cube, "Cross1", med.transform, new Vector3(0, 0, -0.55f), new Vector3(0.5f, 0.15f, 0.2f), R.Emissive(new Color(0.8f, 0.1f, 0.1f), new Color(1.2f, 0.1f, 0.1f)), null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Cross2", med.transform, new Vector3(0, 0, -0.55f), new Vector3(0.18f, 0.5f, 0.2f), R.Emissive(new Color(0.8f, 0.1f, 0.1f), new Color(1.2f, 0.1f, 0.1f)), null, false);
            Medkit = Interactable.Add(med, "Use", "Medical Kit", null);

            foreach (var i in new[] { Moxie, Crate, Bed, Medkit, HabExit }) i.enabled = false;
        }

        // ------------------------------------------------------------------
        static void SkyObjects()
        {
            var em = new Material(R.Template("Planet")); em.SetTexture("_MainTex", R.Tex("Earth__A_"));
            em.SetColor("_AtmoColor", new Color(0.4f, 0.65f, 1f)); em.SetFloat("_AtmoStrength", 1.4f);
            EarthSphere = ProcMesh.Prim(PrimitiveType.Sphere, "EarthTelescope", null, Vector3.zero, Vector3.one * 9.4f, em, null, false);
            var mm = new Material(R.Template("Planet")); mm.SetTexture("_MainTex", R.Tex("Mars___Phobos")); mm.SetColor("_Tint", new Color(0.75f, 0.75f, 0.78f)); mm.SetFloat("_AtmoStrength", 0);
            MoonSphere = ProcMesh.Prim(PrimitiveType.Sphere, "MoonTelescope", null, Vector3.zero, Vector3.one * 2.6f, mm, null, false);
            EarthSphere.SetActive(false); MoonSphere.SetActive(false);
        }

        public static void ShowEarth(Vector3 eye, Vector3 dir, Vector3 sunDir, bool on)
        {
            EarthSphere.SetActive(on); MoonSphere.SetActive(on);
            if (!on) return;
            EarthSphere.transform.position = eye + dir.normalized * 3000;
            var side = Vector3.Cross(dir, Vector3.up).normalized;
            MoonSphere.transform.position = EarthSphere.transform.position + side * 13f + Vector3.up * 3f;
            foreach (var g in new[] { EarthSphere, MoonSphere })
                g.GetComponent<Renderer>().material.SetVector("_SunDirW", sunDir);
            EarthSphere.transform.rotation = Quaternion.Euler(20, 140, 0);
        }

        public static void SetSurfaceActive(bool on)
        {
            if (Root) Root.SetActive(on);
        }
    }

    // ---------------- small behaviours ----------------
    public class Spin : MonoBehaviour { public float Speed = 30; void Update() => transform.Rotate(0, Speed * Time.deltaTime, 0, Space.World); }
    public class Bob : MonoBehaviour
    {
        Vector3 basePos; float ph;
        void Start() { basePos = transform.position; ph = Random.value * 6; }
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
