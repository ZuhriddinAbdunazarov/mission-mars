using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    /// <summary>Tag on clickable buildings.</summary>
    public class BuildingRef : MonoBehaviour { public string Id; }

    /// <summary>The 3D Mars outpost. Visuals follow the simulation state so kids SEE every decision.</summary>
    public class Outpost : MonoBehaviour
    {
        public static Outpost I;
        public readonly Dictionary<string, Transform> Anchors = new Dictionary<string, Transform>();

        // pieces that react to state
        ParticleSystem redIn, blueOut, drillDust, stormDust, flareRain;
        readonly List<Renderer> panelBoards = new List<Renderer>();
        readonly List<GameObject> panels = new List<GameObject>();
        readonly List<Renderer> batteryBands = new List<Renderer>();
        readonly List<Transform> plants = new List<Transform>();
        readonly List<GameObject> walls = new List<GameObject>();
        GameObject drillBuilt, drillGhost, shieldBubble, rover, heli, ship;
        Transform drillBit, dish;
        Light growLight, moxieLight;
        Material bubbleMat, greenhouseGlass, panelMat, panelDusty;
        float roverAngle; int roverLevel, moxieLevel, farmLevel; bool hasDrill; int shieldLevel;
        readonly List<Walker> crew = new List<Walker>();

        static readonly Color White = new Color(0.93f, 0.93f, 0.95f);
        static readonly Color Orange = new Color(1f, 0.47f, 0.16f);
        static readonly Color Teal = new Color(0.2f, 0.85f, 0.95f);

        public static Vector3 Pos(string id) => I.Anchors.TryGetValue(id, out var t) ? t.position : Vector3.zero;

        public static Outpost Build()
        {
            var g = new GameObject("Outpost"); var o = g.AddComponent<Outpost>(); I = o; o.Create(); return o;
        }

        Transform Anchor(string id, float x, float z, float yaw = 0)
        {
            var t = new GameObject(id).transform; t.SetParent(transform);
            t.position = MarsTerrain.Ground(x, z); t.rotation = Quaternion.Euler(0, yaw, 0);
            Anchors[id] = t;
            return t;
        }

        static Material M(Color c, float smooth = 0.45f, float metal = 0) => R.Lit(c, smooth, metal);
        static Material E(Color baseC, Color emis) => R.Emissive(baseC, emis);

        GameObject P(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3? rot = null, bool col = false)
            => ProcMesh.Prim(t, t.ToString(), parent, pos, scale, m, rot, col);

        void Clickable(Transform t, string id, Vector3 center, Vector3 size)
        {
            var bc = t.gameObject.AddComponent<BoxCollider>(); bc.center = center; bc.size = size;
            t.gameObject.AddComponent<BuildingRef>().Id = id;
        }

        void Create()
        {
            var terrain = MarsTerrain.Build(R.Template("Terrain"));
            terrain.transform.SetParent(transform, true);
            Rocks();
            var white = M(White, 0.55f); var orange = M(Orange, 0.4f); var dark = M(new Color(0.22f, 0.24f, 0.28f), 0.5f, 0.6f);
            var glass = E(new Color(0.1f, 0.3f, 0.4f), Teal * 1.6f);

            // ---------- habitat ----------
            var hab = Anchor("hab", 0, 0);
            var dome = ProcMesh.MeshObj("Dome", ProcMesh.Dome(32, 12), white, hab); dome.transform.localScale = new Vector3(5.2f, 4.2f, 5.2f); dome.transform.localPosition = new Vector3(0, 1.2f, 0);
            P(PrimitiveType.Cylinder, hab, new Vector3(0, 0.6f, 0), new Vector3(10.6f, 0.6f, 10.6f), white);
            P(PrimitiveType.Cylinder, hab, new Vector3(0, 1.25f, 0), new Vector3(10.7f, 0.08f, 10.7f), orange);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45 * Mathf.Deg2Rad;
                P(PrimitiveType.Sphere, hab, new Vector3(Mathf.Cos(a) * 4.6f, 2.6f, Mathf.Sin(a) * 4.6f), new Vector3(0.9f, 0.9f, 0.9f), glass);
            }
            P(PrimitiveType.Cylinder, hab, new Vector3(0, 5.5f, 0), new Vector3(1.2f, 0.25f, 1.2f), orange);
            var airlock = P(PrimitiveType.Cylinder, hab, new Vector3(0, 1.6f, -5.6f), new Vector3(2.2f, 1.6f, 2.2f), white, new Vector3(90, 0, 0));
            P(PrimitiveType.Cube, hab, new Vector3(0, 1.4f, -7.2f), new Vector3(1.4f, 2.2f, 0.1f), dark);
            P(PrimitiveType.Cube, hab, new Vector3(0, 2.7f, -7.2f), new Vector3(1.2f, 0.12f, 0.12f), E(Color.black, new Color(0.3f, 2f, 0.8f)));
            Clickable(hab, "hab", new Vector3(0, 2.5f, 0), new Vector3(11, 5, 11));
            var hl = new GameObject("HabLight").AddComponent<Light>(); hl.transform.SetParent(hab, false); hl.transform.localPosition = new Vector3(0, 3, -8);
            hl.type = LightType.Point; hl.range = 12; hl.intensity = 2; hl.color = new Color(1f, 0.85f, 0.6f);

            // shield walls (regolith bags) + bubble
            for (int ring = 0; ring < 2; ring++)
            {
                var w = new GameObject("Wall" + ring); w.transform.SetParent(hab, false);
                var bag = M(new Color(0.62f, 0.34f, 0.2f), 0.1f);
                float rad = 8.2f + ring * 1.4f;
                for (int i = 0; i < 26; i++)
                {
                    float a = i / 26f * Mathf.PI * 2; if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 270)) < 18) continue;
                    var b = P(PrimitiveType.Sphere, w.transform, new Vector3(Mathf.Cos(a) * rad, 0.5f, Mathf.Sin(a) * rad), new Vector3(2.2f, 1.1f, 1.3f), bag);
                    b.transform.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90, 0);
                }
                w.SetActive(false); walls.Add(w);
            }
            shieldBubble = P(PrimitiveType.Sphere, hab, new Vector3(0, 0, 0), Vector3.one * 19, R.Holo(new Color(0.6f, 0.45f, 1f), 0.9f, 0.04f));
            shieldBubble.GetComponent<MeshFilter>().sharedMesh = ProcMesh.HiSphere;
            shieldBubble.SetActive(false);

            // ---------- MOXIE ----------
            var mox = Anchor("air", -15, 6, 20);
            P(PrimitiveType.Cube, mox, new Vector3(0, 1.3f, 0), new Vector3(3.6f, 2.6f, 2.6f), white);
            P(PrimitiveType.Cube, mox, new Vector3(0, 2.0f, -1.31f), new Vector3(2.4f, 0.5f, 0.05f), E(Color.black, Teal * 2f));
            P(PrimitiveType.Cylinder, mox, new Vector3(-2.3f, 2.4f, 0), new Vector3(1.4f, 0.6f, 1.4f), M(new Color(0.95f, 0.3f, 0.25f)));
            P(PrimitiveType.Sphere, mox, new Vector3(2.4f, 1.6f, 0), new Vector3(1.8f, 1.8f, 1.8f), M(new Color(0.3f, 0.7f, 1f), 0.8f));
            var pipe = P(PrimitiveType.Cylinder, mox, new Vector3(5.5f, 0.6f, -1.5f), new Vector3(0.4f, 4.5f, 0.4f), white, new Vector3(0, 0, 90));
            Clickable(mox, "air", new Vector3(0, 1.5f, 0), new Vector3(6, 3.4f, 3.4f));
            moxieLight = new GameObject("MoxLight").AddComponent<Light>(); moxieLight.transform.SetParent(mox, false); moxieLight.transform.localPosition = new Vector3(0, 3.5f, -2);
            moxieLight.type = LightType.Point; moxieLight.range = 9; moxieLight.color = Teal;
            bubbleMat = new Material(R.Template("ParticleAdd")); bubbleMat.mainTexture = Bubble();
            redIn = Bubbles(mox, new Vector3(-6, 2.4f, 0), new Vector3(3.5f, 0, 0), new Color(1f, 0.35f, 0.25f));
            blueOut = Bubbles(mox, new Vector3(2.4f, 2.6f, 0), new Vector3(0, 1.4f, 1.2f), new Color(0.35f, 0.75f, 1f));

            // ---------- greenhouse ----------
            var gh = Anchor("farm", 15, 6, -20);
            P(PrimitiveType.Cylinder, gh, new Vector3(0, 0.3f, 0), new Vector3(9, 0.3f, 9), white);
            var gdome = ProcMesh.MeshObj("Glass", ProcMesh.Dome(32, 12), R.Holo(new Color(0.55f, 1f, 0.75f), 1.1f, 0.06f), gh);
            gdome.transform.localScale = new Vector3(4.4f, 3.6f, 4.4f); gdome.transform.localPosition = new Vector3(0, 0.6f, 0);
            var frame = ProcMesh.MeshObj("Frame", ProcMesh.Dome(8, 4), white, gh); frame.transform.localScale = new Vector3(4.45f, 3.65f, 4.45f); frame.transform.localPosition = new Vector3(0, 0.6f, 0);
            frame.GetComponent<MeshRenderer>().enabled = false;
            for (int r = -1; r <= 1; r++)
            {
                P(PrimitiveType.Cube, gh, new Vector3(r * 1.6f, 0.75f, 0), new Vector3(1.1f, 0.35f, 5.2f), M(new Color(0.85f, 0.88f, 0.9f)));
                for (int k = 0; k < 5; k++)
                {
                    var plant = P(PrimitiveType.Sphere, gh, new Vector3(r * 1.6f, 1.2f, -2 + k), Vector3.one * 0.8f, M(new Color(0.3f, 0.85f, 0.35f), 0.3f));
                    plants.Add(plant.transform);
                }
            }
            growLight = new GameObject("Grow").AddComponent<Light>(); growLight.transform.SetParent(gh, false); growLight.transform.localPosition = new Vector3(0, 3f, 0);
            growLight.type = LightType.Point; growLight.range = 10; growLight.color = new Color(1f, 0.4f, 1f);
            Clickable(gh, "farm", new Vector3(0, 2f, 0), new Vector3(9, 4.5f, 9));

            // ---------- solar field ----------
            var sol = Anchor("power", 0, -22);
            panelMat = E(new Color(0.08f, 0.16f, 0.45f), new Color(0.05f, 0.12f, 0.4f));
            panelDusty = M(new Color(0.55f, 0.36f, 0.24f), 0.1f);
            for (int i = 0; i < 6; i++)
            {
                int row = i / 3, colm = i % 3;
                var pg = new GameObject("Panel" + i); pg.transform.SetParent(sol, false); pg.transform.localPosition = new Vector3((colm - 1) * 5.2f, 0, row * -4.6f);
                P(PrimitiveType.Cylinder, pg.transform, new Vector3(0, 0.7f, 0), new Vector3(0.18f, 0.7f, 0.18f), dark);
                var board = P(PrimitiveType.Cube, pg.transform, new Vector3(0, 1.5f, 0), new Vector3(4.6f, 0.1f, 2.9f), panelMat, new Vector3(-28, 0, 0));
                P(PrimitiveType.Cube, pg.transform, new Vector3(0, 1.47f, 0), new Vector3(4.75f, 0.06f, 3.05f), white, new Vector3(-28, 0, 0));
                panelBoards.Add(board.GetComponent<Renderer>()); panels.Add(pg);
            }
            Clickable(sol, "power", new Vector3(0, 1.5f, -2.3f), new Vector3(16, 3, 9));

            // ---------- battery ----------
            var bat = Anchor("battery", -14, -11, 30);
            for (int i = 0; i < 3; i++)
            {
                P(PrimitiveType.Cylinder, bat, new Vector3((i - 1) * 1.7f, 1.2f, 0), new Vector3(1.4f, 1.2f, 1.4f), white);
                var band = P(PrimitiveType.Cylinder, bat, new Vector3((i - 1) * 1.7f, 1.2f, 0), new Vector3(1.45f, 0.5f, 1.45f), E(new Color(0.1f, 0.3f, 0.1f), new Color(0.3f, 2f, 0.4f)));
                batteryBands.Add(band.GetComponent<Renderer>());
            }
            Clickable(bat, "battery", new Vector3(0, 1.2f, 0), new Vector3(6, 2.6f, 2));

            // ---------- ice drill ----------
            var dr = Anchor("drill", 15, -12, -30);
            drillBuilt = new GameObject("Built"); drillBuilt.transform.SetParent(dr, false);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2) * 2 - 1, z = (i / 2) * 2 - 1;
                P(PrimitiveType.Cube, drillBuilt.transform, new Vector3(x * 0.8f, 3, z * 0.8f), new Vector3(0.2f, 6, 0.2f), orange, new Vector3(-z * 7, 0, x * 7));
            }
            P(PrimitiveType.Cube, drillBuilt.transform, new Vector3(0, 6, 0), new Vector3(1.4f, 0.4f, 1.4f), orange);
            drillBit = P(PrimitiveType.Cylinder, drillBuilt.transform, new Vector3(0, 2.5f, 0), new Vector3(0.35f, 2.5f, 0.35f), dark).transform;
            P(PrimitiveType.Cylinder, drillBuilt.transform, new Vector3(2.4f, 1.1f, 0), new Vector3(1.8f, 1.1f, 1.8f), M(new Color(0.35f, 0.75f, 1f), 0.8f));
            drillDust = FX.Make("DrillDust", dr.position, dr, FX.Alpha, ps =>
            {
                var m = ps.main; m.startLifetime = 2; m.startSpeed = 1.5f; m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1f); m.startColor = new Color(0.75f, 0.5f, 0.35f, 0.5f);
                var e = ps.emission; e.rateOverTime = 8; var s = ps.shape; s.shapeType = ParticleSystemShapeType.Hemisphere; s.radius = 0.5f;
            });
            drillDust.transform.SetParent(drillBuilt.transform, true);
            drillGhost = P(PrimitiveType.Cube, dr, new Vector3(0, 3, 0), new Vector3(2, 6, 2), R.Holo(new Color(0.4f, 0.9f, 1f), 1.2f, 0.1f));
            Clickable(dr, "drill", new Vector3(0, 3, 0), new Vector3(5, 6, 4));

            // ---------- comms dish ----------
            var cm = Anchor("comms", -19, -1, 60);
            P(PrimitiveType.Cylinder, cm, new Vector3(0, 1.5f, 0), new Vector3(0.4f, 1.5f, 0.4f), dark);
            var d = ProcMesh.MeshObj("Dish", ProcMesh.Dome(24, 8), white, cm); d.transform.localScale = new Vector3(2.4f, 0.8f, 2.4f); d.transform.localPosition = new Vector3(0, 3.6f, 0);
            d.transform.localEulerAngles = new Vector3(140, 0, 0); dish = d.transform;
            var blink = new GameObject("Blink").AddComponent<Light>(); blink.transform.SetParent(cm, false); blink.transform.localPosition = new Vector3(0, 4.8f, 0);
            blink.type = LightType.Point; blink.range = 5; blink.intensity = 3; blink.color = new Color(1f, 0.3f, 0.2f); blink.gameObject.AddComponent<Blink>();
            Clickable(cm, "comms", new Vector3(0, 2.5f, 0), new Vector3(3, 5, 3));

            // ---------- rover (NASA Perseverance) + Ingenuity ----------
            rover = Util.PlaceModel("Mars_2020_Perseverance_Rover", MarsTerrain.Ground(30, 0), 3.2f, 0, transform);
            Anchors["rover"] = rover.transform;
            Clickable(rover.transform, "rover", new Vector3(0, 1.2f, 0), new Vector3(4, 3, 4));
            heli = Util.PlaceModel("Ingenuity_Mars_Helicopter", MarsTerrain.Ground(30, 3), 1.2f, 0, transform);

            // ---------- landing pad + ship ----------
            var pad = Anchor("pad", 30, 22);
            P(PrimitiveType.Cylinder, pad, new Vector3(0, 0.15f, 0), new Vector3(10, 0.15f, 10), M(new Color(0.4f, 0.4f, 0.42f), 0.2f));
            P(PrimitiveType.Cylinder, pad, new Vector3(0, 0.31f, 0), new Vector3(7, 0.02f, 7), E(Color.black, Orange * 1.5f));
            P(PrimitiveType.Cylinder, pad, new Vector3(0, 0.32f, 0), new Vector3(6.4f, 0.02f, 6.4f), M(new Color(0.35f, 0.35f, 0.37f), 0.2f));
            ship = Util.PlaceModel("Redstone_3__Freedom_7_", pad.position + Vector3.up * 0.3f, 16, 0, transform, true);
            ship.SetActive(false);

            // ---------- crew ----------
            string[] stops = { "hab", "air", "farm", "power", "battery", "comms" };
            Color[] suits = { new Color(1f, 0.55f, 0.2f), new Color(0.3f, 0.75f, 1f), new Color(0.95f, 0.85f, 0.25f) };
            for (int i = 0; i < 3; i++) crew.Add(Walker.Create(transform, suits[i], stops, i));

            // ---------- weather FX ----------
            stormDust = FX.Make("Storm", new Vector3(0, 6, 0), transform, FX.Alpha, ps =>
            {
                var m = ps.main; m.startLifetime = 6; m.startSpeed = 0; m.maxParticles = 700; m.startSize = new ParticleSystem.MinMaxCurve(4, 10);
                m.startColor = new Color(0.75f, 0.45f, 0.28f, 0.35f); m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
                var e = ps.emission; e.rateOverTime = 110;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(120, 12, 120);
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
                v.x = new ParticleSystem.MinMaxCurve(12, 20); v.y = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f); v.z = new ParticleSystem.MinMaxCurve(2, 5);
            });
            stormDust.Stop();
            flareRain = FX.Make("FlareRain", new Vector3(0, 40, 0), transform, FX.Add, ps =>
            {
                var m = ps.main; m.startLifetime = 1.4f; m.startSpeed = 40; m.maxParticles = 800; m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
                m.startColor = new Color(1f, 0.85f, 0.4f);
                var e = ps.emission; e.rateOverTime = 260;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(70, 1, 70); s.rotation = new Vector3(90, 0, 0);
                var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 2;
            });
            flareRain.transform.rotation = Quaternion.Euler(90, 0, 0);
            flareRain.Stop();

            FX.DustBanks(new Vector3(0, MarsTerrain.Base + 2, 0)).transform.SetParent(transform, true);
        }

        static Texture2D _bubble;
        static Texture2D Bubble()
        {
            if (_bubble) return _bubble;
            int s = 64; _bubble = new Texture2D(s, s, TextureFormat.RGBA32, true);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2 - 1, dy = (y + 0.5f) / s * 2 - 1; float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Clamp01(1 - Mathf.Abs(d - 0.8f) * 8) + Mathf.Clamp01(0.35f - d) * 0.6f;
                    float hl = Mathf.Clamp01(1 - new Vector2(dx + 0.35f, dy - 0.35f).magnitude * 5);
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(ring + hl) * 255 * (d < 1 ? 1 : 0)));
                }
            _bubble.SetPixels32(px); _bubble.Apply(true); return _bubble;
        }

        ParticleSystem Bubbles(Transform parent, Vector3 local, Vector3 vel, Color c)
        {
            var ps = FX.Make("Bubbles", parent.TransformPoint(local), parent, bubbleMat, p =>
            {
                var m = p.main; m.startLifetime = 1.8f; m.startSpeed = 0; m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f); m.startColor = c; m.maxParticles = 200;
                m.simulationSpace = ParticleSystemSimulationSpace.Local;
                var e = p.emission; e.rateOverTime = 0;
                var s = p.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.6f;
                var v = p.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
                v.x = new ParticleSystem.MinMaxCurve(vel.x * 0.8f, vel.x * 1.2f); v.y = new ParticleSystem.MinMaxCurve(vel.y + 0.2f, vel.y + 0.8f); v.z = new ParticleSystem.MinMaxCurve(vel.z * 0.8f - 0.2f, vel.z * 1.2f + 0.2f);
                var col = p.colorOverLifetime; col.enabled = true;
                var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
                col.color = gr;
            });
            ps.transform.localPosition = local;
            return ps;
        }

        void Rocks()
        {
            var parent = new GameObject("Rocks").transform; parent.SetParent(transform);
            var meshes = new Mesh[6]; for (int i = 0; i < meshes.Length; i++) meshes[i] = ProcMesh.Rock(100 + i * 7, 2);
            var mat = R.LitTex("rock_boulder_dry_diff", "rock_boulder_dry_nor", new Color(0.85f, 0.5f, 0.36f), 1f, 0.1f, 0); mat.enableInstancing = true;
            var rng = new System.Random(9);
            float Rnd() => (float)rng.NextDouble();
            for (int i = 0, placed = 0; i < 2000 && placed < 450; i++)
            {
                float x = Rnd() * 500 - 250, z = Rnd() * 500 - 250;
                if (new Vector2(x, z).magnitude < 42) continue;
                if (Mathf.PerlinNoise(x * 0.02f + 3, z * 0.02f + 9) < 0.45f) continue;
                float s = Mathf.Lerp(0.3f, 1.6f, Mathf.Pow(Rnd(), 2.5f)) + Mathf.Pow(Rnd(), 8f) * 5f;
                var g = ProcMesh.MeshObj("Rock", meshes[rng.Next(meshes.Length)], mat, parent);
                g.transform.position = MarsTerrain.Ground(x, z, -s * 0.15f);
                g.transform.rotation = Quaternion.Euler(0, Rnd() * 360, 0);
                g.transform.localScale = new Vector3(s, s * 0.8f, s);
                placed++;
            }
        }

        // ================= state -> visuals =================
        public void Apply(SimState s, Preview p, float ratio)
        {
            moxieLevel = Mathf.RoundToInt(s.Level[0] * Mathf.Ceil(ratio)); farmLevel = s.Level[1]; hasDrill = s.HasDrill; roverLevel = s.Level[3]; shieldLevel = s.Level[2];
            var e1 = redIn.emission; e1.rateOverTime = moxieLevel * 8; var e2 = blueOut.emission; e2.rateOverTime = moxieLevel * 8;
            moxieLight.intensity = moxieLevel * 1.5f;
            growLight.intensity = hasDrill ? farmLevel * 2.5f : 0;
            float grow = hasDrill ? Mathf.Clamp01(0.3f + farmLevel * 0.35f + s.Food / 300f) : 0.15f;
            foreach (var pl in plants) { pl.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, grow); pl.GetComponent<Renderer>().sharedMaterial = hasDrill ? GreenMat : BrownMat; }
            for (int i = 0; i < panels.Count; i++) panels[i].SetActive(i < s.Panels);
            foreach (var b in panelBoards) b.sharedMaterial = s.Dust > 0.3f ? panelDusty : panelMat;
            int lit = Mathf.CeilToInt(s.Battery / s.BatteryCap * 3);
            for (int i = 0; i < batteryBands.Count; i++) batteryBands[i].sharedMaterial = i < lit ? BandOn : BandOff;
            drillBuilt.SetActive(s.HasDrill); drillGhost.SetActive(!s.HasDrill);
            for (int i = 0; i < walls.Count; i++) walls[i].SetActive(i < s.ShieldWalls);
            shieldBubble.SetActive(shieldLevel > 0);
            if (shieldLevel > 0) shieldBubble.GetComponent<Renderer>().sharedMaterial.SetFloat("_Intensity", 0.5f + shieldLevel * 0.5f);
            if (s.StormToday && !stormDust.isPlaying) stormDust.Play(); else if (!s.StormToday && stormDust.isPlaying) stormDust.Stop();
            heli.SetActive(roverLevel >= 2);
        }

        static Material _green, _brown, _bandOn, _bandOff;
        static Material GreenMat => _green ? _green : (_green = R.Lit(new Color(0.3f, 0.85f, 0.35f), 0.3f));
        static Material BrownMat => _brown ? _brown : (_brown = R.Lit(new Color(0.45f, 0.3f, 0.2f), 0.1f));
        static Material BandOn => _bandOn ? _bandOn : (_bandOn = R.Emissive(new Color(0.1f, 0.4f, 0.1f), new Color(0.3f, 2.2f, 0.4f)));
        static Material BandOff => _bandOff ? _bandOff : (_bandOff = R.Lit(new Color(0.2f, 0.22f, 0.25f), 0.4f));

        public IEnumerator FlareHit(float block)
        {
            flareRain.Play();
            var mat = shieldBubble.GetComponent<Renderer>().sharedMaterial;
            float t = 0;
            while (t < 3f)
            {
                t += Time.deltaTime;
                if (shieldBubble.activeSelf) mat.SetFloat("_Intensity", 1 + block * 3 * (0.6f + 0.4f * Mathf.Sin(t * 20)));
                yield return null;
            }
            flareRain.Stop();
        }

        public IEnumerator ShipArrives()
        {
            ship.SetActive(true);
            var end = ship.transform.position; var start = end + Vector3.up * 120;
            var b = Util.Bounds(ship);
            var flame = FX.Exhaust(ship.transform, ship.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z)), 1.2f);
            float t = 0;
            while (t < 6f) { t += Time.deltaTime; ship.transform.position = Vector3.Lerp(start, end, Tween.EaseOut(t / 6f)); yield return null; }
            FX.DustBlast(end, 1.5f);
            Sfx.Play("thud", 0.8f);
            yield return new WaitForSeconds(0.5f);
            Destroy(flame.gameObject);
        }

        void Update()
        {
            if (drillBuilt.activeSelf) drillBit.Rotate(0, 360 * Time.deltaTime, 0, Space.Self);
            dish.parent.Rotate(0, 10 * Time.deltaTime, 0);
            if (roverLevel > 0)
            {
                roverAngle += Time.deltaTime * (roverLevel * 0.05f);
                float r = 32;
                var pos = MarsTerrain.Ground(Mathf.Cos(roverAngle) * r, Mathf.Sin(roverAngle) * r);
                var dir = new Vector3(-Mathf.Sin(roverAngle), 0, Mathf.Cos(roverAngle));
                rover.transform.position = pos; rover.transform.rotation = Quaternion.LookRotation(dir);
                if (heli.activeSelf) heli.transform.position = pos + Vector3.up * (4 + Mathf.Sin(Time.time * 2) * 0.6f) + dir * 3;
                if (heli.activeSelf) heli.transform.Rotate(0, 180 * Time.deltaTime, 0);
            }
        }
    }

    /// <summary>Cute little astronaut who walks between buildings.</summary>
    public class Walker : MonoBehaviour
    {
        string[] stops; int idx; Vector3 target; float wait; Transform body;
        public static Walker Create(Transform parent, Color suit, string[] stops, int seed)
        {
            var g = new GameObject("Astronaut"); g.transform.SetParent(parent);
            var w = g.AddComponent<Walker>(); w.stops = stops; w.idx = seed * 2 % stops.Length;
            var white = R.Lit(new Color(0.95f, 0.95f, 0.97f), 0.5f);
            var b = new GameObject("Body").transform; b.SetParent(g.transform, false); w.body = b;
            ProcMesh.Prim(PrimitiveType.Capsule, "Suit", b, new Vector3(0, 0.75f, 0), new Vector3(0.7f, 0.6f, 0.6f), white, null, false);
            ProcMesh.Prim(PrimitiveType.Sphere, "Helmet", b, new Vector3(0, 1.55f, 0), Vector3.one * 0.62f, white, null, false);
            ProcMesh.Prim(PrimitiveType.Sphere, "Visor", b, new Vector3(0, 1.57f, 0.17f), new Vector3(0.48f, 0.36f, 0.32f), R.Emissive(new Color(0.9f, 0.6f, 0.1f), new Color(0.8f, 0.45f, 0.05f)), null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Pack", b, new Vector3(0, 0.95f, -0.32f), new Vector3(0.55f, 0.6f, 0.25f), R.Lit(suit, 0.4f), null, false);
            ProcMesh.Prim(PrimitiveType.Cylinder, "Stripe", b, new Vector3(0, 0.95f, 0), new Vector3(0.72f, 0.06f, 0.62f), R.Lit(suit, 0.4f), null, false);
            g.transform.position = Outpost.Pos(stops[w.idx]) + new Vector3(seed * 1.5f, 0, -8);
            w.NextTarget();
            return w;
        }

        void NextTarget()
        {
            idx = (idx + 1 + Random.Range(0, 2)) % stops.Length;
            var p = Outpost.Pos(stops[idx]);
            var off = Random.insideUnitCircle * 2.5f;
            var dirOut = (p - Vector3.zero); dirOut.y = 0;
            target = p + (stops[idx] == "hab" ? new Vector3(0, 0, -8) : dirOut.normalized * -4f) + new Vector3(off.x, 0, off.y);
            target = MarsTerrain.Ground(target.x, target.z);
        }

        void Update()
        {
            if (wait > 0) { wait -= Time.deltaTime; body.localPosition = Vector3.zero; return; }
            var to = target - transform.position; to.y = 0;
            if (to.magnitude < 0.3f) { wait = Random.Range(1.5f, 4f); NextTarget(); return; }
            var step = to.normalized * Time.deltaTime * 1.8f;
            var np = transform.position + step; np.y = MarsTerrain.H(np.x, np.z);
            transform.position = np;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 6);
            body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(Time.time * 7)) * 0.25f, 0); // bouncy low-gravity hop
        }
    }

    public class Blink : MonoBehaviour
    {
        public float Rate = 1f; Light l; float b;
        void Start() { l = GetComponent<Light>(); b = l.intensity; }
        void Update() { l.intensity = (Mathf.Sin(Time.time * Rate * 6.28f) > 0 ? 1 : 0.1f) * b; }
    }
}
