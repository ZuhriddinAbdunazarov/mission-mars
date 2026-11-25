using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    /// <summary>The walk-in PLANTATION inside the glass dome of the Mars base.
    /// Mission: plant potato seeds → turn on the water pump (melted Mars ice) → watch them grow under the sunlight
    /// coming through the glass → harvest → eat. Teaches: plants need light + water + soil, and they give FOOD and OXYGEN.
    /// Real fact: in 2015 astronauts on the Space Station ate lettuce they grew in NASA's Veggie garden.</summary>
    public class Greenhouse : MonoBehaviour
    {
        public static Greenhouse I;
        public static readonly Vector3 Origin = new Vector3(0, -180, 0);
        public GameObject Room; public Vector3 Spawn; public float SpawnYaw;
        public Interactable ExitDoor; public Transform ExitPanel;
        readonly List<Transform> plants = new List<Transform>();
        readonly List<GameObject> potatoes = new List<GameObject>();
        readonly List<Interactable> beds = new List<Interactable>();
        readonly List<Renderer> water = new List<Renderer>();
        Interactable pump, crate; GameObject cratePile; Material waterMat;
        int state;          // 0 plant, 1 water, 2 growing, 3 harvest, 4 eat
        bool told;

        public static Greenhouse Build()
        {
            var g = new GameObject("Greenhouse"); I = g.AddComponent<Greenhouse>(); I.Make(); return I;
        }

        // ---- the realistic room made from the team's Tripo model (Assets/Paid/PlantationIn) ----
        bool modelMode; Interactable ledSwitch; readonly List<Light> growLights = new List<Light>(); readonly List<Material> growMats = new List<Material>();
        readonly List<Vector3> bedTops = new List<Vector3>();
        public Collider BedsCollider;

        public void SetExpeditionCrop(bool ripe,bool lighting)
        {
            foreach(var plant in potatoes) plant.SetActive(ripe);
            foreach(var lamp in growLights) lamp.intensity=lighting?2.2f:0f;
        }
        void MakeFromModel()
        {
            var module = ModelRoom.Build("08_Greenhouse_Interior", Origin, 12, 5);
            Room = module.Room;
            Spawn = module.Spawn;
            SpawnYaw = module.SpawnYaw;
            ExitPanel = module.ExitPanel;
            ExitDoor = module.ExitDoor;
            modelMode = true;

            foreach (var side in new[] { "Port", "Starboard" })
            {
                var bed = module.UsePart(side + " | planting trough", "Harvest", "potatoes", Use);
                bed.enabled = false;
                beds.Add(bed);
                var crop = ModelRoom.Part(Room, side + " | potato stems");
                bedTops.Add(crop.localPosition);
                potatoes.Add(crop.gameObject);
                crop.gameObject.SetActive(false);
                var lamp = ModelRoom.Part(Room, side + " | cantilever");
                var light = lamp.gameObject.AddComponent<Light>();
                light.color = new Color(1f, 0.45f, 1f);
                light.range = 8;
                light.intensity = 0;
                growLights.Add(light);
            }
            var sw = ProcMesh.Prim(PrimitiveType.Cube, "LED switch", Room.transform,
                new Vector3(-5.7f, 1.35f, 0.85f), new Vector3(0.18f, 0.4f, 0.25f), R.CyanGlow);
            ledSwitch = Interactable.Add(sw, "Switch on", "LED GROW LIGHTS", Use);
            pump = module.UsePart("Ice melt system", "Turn on", "WATER PUMP", Use);
            pump.enabled = false;
            crate = module.UsePart("Harvest crate", "Eat", "fresh Mars potatoes", Use);
            cratePile = crate.gameObject;
            crate.enabled = false;
            cratePile.SetActive(false);
            Room.SetActive(false);
        }

        void Make()
        {
            if (Resources.Load<GameObject>("MarsBase9/08_Greenhouse_Interior")) { MakeFromModel(); return; }
            Room = new GameObject("GreenhouseRoom"); var t = Room.transform; t.position = Origin;
            const float W = 14, D = 14, H = 6.5f;
            var white = R.Panel;
            var glass = R.Emissive(new Color(0.85f, 0.62f, 0.45f), new Color(1.05f, 0.78f, 0.55f), 0.9f); // bright Mars sky through the glass
            var frame = R.Lit(new Color(0.92f, 0.93f, 0.95f), 0.5f, 0.1f);
            var soil = R.Lit(new Color(0.36f, 0.22f, 0.13f), 0.05f);
            waterMat = R.Lit(new Color(0.45f, 0.45f, 0.47f), 0.3f); // dry channel until the pump is on

            ProcMesh.Prim(PrimitiveType.Cube, "Floor", t, new Vector3(0, -0.1f, 0), new Vector3(W, 0.2f, D), R.Floor);
            // low white walls + tall glass walls and a glass roof with white frames (sunlight comes in)
            foreach (var (pos, size) in new[] {
                (new Vector3(0, 0.7f, D / 2), new Vector3(W, 1.4f, 0.3f)), (new Vector3(0, 0.7f, -D / 2), new Vector3(W, 1.4f, 0.3f)),
                (new Vector3(W / 2, 0.7f, 0), new Vector3(0.3f, 1.4f, D)), (new Vector3(-W / 2, 0.7f, 0), new Vector3(0.3f, 1.4f, D)) })
                ProcMesh.Prim(PrimitiveType.Cube, "LowWall", t, pos, size, white);
            foreach (var (pos, size) in new[] {
                (new Vector3(0, 1.4f + (H - 1.4f) / 2, D / 2), new Vector3(W, H - 1.4f, 0.1f)), (new Vector3(0, 1.4f + (H - 1.4f) / 2, -D / 2), new Vector3(W, H - 1.4f, 0.1f)),
                (new Vector3(W / 2, 1.4f + (H - 1.4f) / 2, 0), new Vector3(0.1f, H - 1.4f, D)), (new Vector3(-W / 2, 1.4f + (H - 1.4f) / 2, 0), new Vector3(0.1f, H - 1.4f, D)),
                (new Vector3(0, H, 0), new Vector3(W, 0.1f, D)) })
                ProcMesh.Prim(PrimitiveType.Cube, "Glass", t, pos, size, glass);
            for (int i = -3; i <= 3; i++) // window frames
            {
                ProcMesh.Prim(PrimitiveType.Cube, "FrameN", t, new Vector3(i * 2f, 3.9f, D / 2 - 0.08f), new Vector3(0.14f, 5f, 0.12f), frame, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "FrameS", t, new Vector3(i * 2f, 3.9f, -D / 2 + 0.08f), new Vector3(0.14f, 5f, 0.12f), frame, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "FrameE", t, new Vector3(W / 2 - 0.08f, 3.9f, i * 2f), new Vector3(0.12f, 5f, 0.14f), frame, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "FrameW", t, new Vector3(-W / 2 + 0.08f, 3.9f, i * 2f), new Vector3(0.12f, 5f, 0.14f), frame, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "RoofX", t, new Vector3(i * 2f, H - 0.1f, 0), new Vector3(0.14f, 0.14f, D), frame, null, false);
                ProcMesh.Prim(PrimitiveType.Cube, "RoofZ", t, new Vector3(0, H - 0.1f, i * 2f), new Vector3(W, 0.14f, 0.14f), frame, null, false);
            }
            // warm "sunlight" coming through the roof
            for (int i = -1; i <= 1; i += 2)
                for (int k = -1; k <= 1; k += 2)
                {
                    var l = new GameObject("Sun").AddComponent<Light>(); l.transform.SetParent(t, false); l.transform.localPosition = new Vector3(i * 3.5f, H - 0.6f, k * 3.5f);
                    l.type = LightType.Point; l.range = 12; l.intensity = 3.4f; l.color = new Color(1f, 0.93f, 0.8f); l.shadows = LightShadows.None;
                }

            // three soil beds with water channels between them
            for (int b = -1; b <= 1; b++)
            {
                float x = b * 4.2f;
                ProcMesh.Prim(PrimitiveType.Cube, "BedRim", t, new Vector3(x, 0.3f, 0.6f), new Vector3(2.8f, 0.6f, 9.4f), white);
                var bed = ProcMesh.Prim(PrimitiveType.Cube, "Soil", t, new Vector3(x, 0.62f, 0.6f), new Vector3(2.5f, 0.06f, 9.1f), soil);
                var use = Interactable.Add(bed, "Plant", "potato seeds", Use); use.Range = 5f; beds.Add(use);
                for (int k = 0; k < 5; k++)
                {
                    var p = new GameObject("Plant").transform; p.SetParent(t, false); p.localPosition = new Vector3(x + (k % 2 == 0 ? -0.45f : 0.45f), 0.65f, -3.2f + k * 1.9f);
                    PotatoPlant(p, b * 5 + k);
                    p.localScale = Vector3.zero; plants.Add(p);
                    for (int s = 0; s < 3; s++) // potatoes, shown when ready to harvest
                    {
                        var pot = ProcMesh.Prim(PrimitiveType.Sphere, "Potato", t, p.localPosition + new Vector3(-0.3f + s * 0.3f, 0.06f, 0.35f), new Vector3(0.24f, 0.18f, 0.2f), R.Lit(new Color(0.62f, 0.45f, 0.26f), 0.2f), null, false);
                        pot.SetActive(false); potatoes.Add(pot);
                    }
                }
                // LED grow lights (pink = red + blue light, the colours plants use most; NASA's Veggie garden uses the same)
                ProcMesh.Prim(PrimitiveType.Cube, "GrowLight", t, new Vector3(x, 3.1f, 0.6f), new Vector3(0.5f, 0.1f, 8.6f), R.Emissive(new Color(0.2f, 0.1f, 0.2f), new Color(2.6f, 0.7f, 2.8f)), null, false);
                for (int w = -1; w <= 1; w += 2) ProcMesh.Prim(PrimitiveType.Cylinder, "Wire", t, new Vector3(x, 3.9f + 0.7f, 0.6f + w * 3.8f), new Vector3(0.03f, 1.5f, 0.03f), R.HullDark, null, false);
                { var gl = new GameObject("GrowGlow").AddComponent<Light>(); gl.transform.SetParent(t, false); gl.transform.localPosition = new Vector3(x, 2.8f, 0.6f); gl.type = LightType.Point; gl.range = 5; gl.intensity = 1.6f; gl.color = new Color(1f, 0.45f, 1f); gl.shadows = LightShadows.None; }
                if (b < 1) // water channel
                {
                    var ch = ProcMesh.Prim(PrimitiveType.Cube, "WaterChannel", t, new Vector3(x + 2.1f, 0.04f, 0.6f), new Vector3(0.5f, 0.06f, 9.4f), waterMat, null, false);
                    water.Add(ch.GetComponent<Renderer>());
                }
            }
            // the water pump on the back wall (water = melted Mars ice from the shelter's machine)
            var pumpGo = new GameObject("WaterPump"); pumpGo.transform.SetParent(t, false); pumpGo.transform.localPosition = new Vector3(0, 0, D / 2 - 1.1f);
            var barrel = R.Model("K_machine_barrelLarge");
            if (barrel) { var b = Instantiate(barrel, pumpGo.transform, false); b.transform.localScale = Vector3.one * 2.4f; }
            else ProcMesh.Prim(PrimitiveType.Cylinder, "Tank", pumpGo.transform, new Vector3(0, 0.8f, 0), new Vector3(1.2f, 0.8f, 1.2f), R.Panel);
            ProcMesh.Prim(PrimitiveType.Cylinder, "Pipe", t, new Vector3(0, 0.15f, D / 2 - 2.2f), new Vector3(0.25f, 0.1f, 0.25f), R.CyanGlow, null, false);
            var pc = pumpGo.AddComponent<BoxCollider>(); pc.center = new Vector3(0, 1, 0); pc.size = new Vector3(2.2f, 2.2f, 1.6f);
            pump = Interactable.Add(pumpGo, "Turn on", "WATER PUMP (melted Mars ice)", Use, 0, false); pump.Range = 5f; pump.enabled = false;
            // the potato crate near the door
            var crateGo = ProcMesh.Prim(PrimitiveType.Cube, "Crate", t, new Vector3(4.6f, 0.45f, -D / 2 + 1.6f), new Vector3(1.6f, 0.9f, 1.1f), R.Lit(new Color(0.55f, 0.38f, 0.22f), 0.2f));
            cratePile = new GameObject("Pile"); cratePile.transform.SetParent(crateGo.transform, false);
            for (int i = 0; i < 9; i++) ProcMesh.Prim(PrimitiveType.Sphere, "Potato", cratePile.transform, new Vector3(-0.3f + (i % 3) * 0.3f, 0.55f, -0.25f + (i / 3) * 0.25f), new Vector3(0.18f, 0.2f, 0.25f), R.Lit(new Color(0.62f, 0.45f, 0.26f), 0.2f), null, false);
            cratePile.SetActive(false);
            crate = Interactable.Add(crateGo, "Eat", "fresh Mars potatoes", Use); crate.Range = 4f; crate.enabled = false;

            // big signs
            var sign = MarsWorld.World3DText(t, new Vector3(0, 3.6f, D / 2 - 0.2f), "PLANTATION", 7, new Color(0.5f, 1f, 0.5f));
            var sign2 = MarsWorld.World3DText(t, new Vector3(0, 2.6f, D / 2 - 0.2f), "Sunlight + Water + Soil = FOOD + OXYGEN", 3.6f, Color.white);
            sign.outlineWidth = sign2.outlineWidth = 0.25f; sign.outlineColor = sign2.outlineColor = new Color32(0, 0, 0, 220);

            // exit door (same sliding airlock door as the shelter)
            ProcMesh.Prim(PrimitiveType.Cube, "Doorway", t, new Vector3(-3.5f, 1.2f, -D / 2 + 0.17f), new Vector3(1.4f, 2.4f, 0.02f), R.Emissive(new Color(0.9f, 0.55f, 0.35f), new Color(1.6f, 0.85f, 0.5f)), null, false);
            var door = new GameObject("DoorPanel").transform; door.SetParent(t, false); door.localPosition = new Vector3(-3.5f, 1.2f, -D / 2 + 0.3f);
            ProcMesh.Prim(PrimitiveType.Cube, "Slab", door, Vector3.zero, new Vector3(1.5f, 2.45f, 0.1f), R.Panel);
            ProcMesh.Prim(PrimitiveType.Cube, "Stripe", door, new Vector3(0, 0.9f, 0.06f), new Vector3(1.5f, 0.16f, 0.02f), R.Orange, null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Window", door, new Vector3(0, 0.4f, 0.06f), new Vector3(0.6f, 0.32f, 0.02f), R.Emissive(new Color(0.05f, 0.15f, 0.2f), new Color(0.2f, 0.9f, 1.3f)), null, false);
            var sd = door.gameObject.AddComponent<SlideDoor>(); sd.Closed = door.localPosition; sd.Slide = 1.55f;
            ExitPanel = door; ExitDoor = Interactable.Add(door.gameObject, "Go outside", "Mars base", null);
            Spawn = t.TransformPoint(new Vector3(-3.5f, 0.05f, -D / 2 + 1.6f)); SpawnYaw = 0;
            Room.SetActive(false);
        }

        /// <summary>Called by the story each time the player walks in.</summary>
        public IEnumerator OnEnter(Hud hud)
        {
            if(Game.ExploreBase){hud.Say("The grow beds need power and water. Load ice at the melt system, then use the pump.");yield break;}
            Objective(hud);
            if (told) yield break;
            told = true;
            yield return hud.SayWait("Welcome to the PLANTATION! Plants need LIGHT, WATER and good SOIL. On Mars all three are hard to get!", 4);
            yield return hud.SayWait("LIGHT: Mars gets only about half of Earth's sunlight, and dust storms block it. So we add pink LED GROW LIGHTS.", 5);
            yield return hud.SayWait("SOIL: Mars dirt has a salt called perchlorate that is bad for plants and people, so astronauts WASH it first.", 5);
            yield return hud.SayWait("WATER: we melt Mars ice. And the plants give back FOOD and OXYGEN. Let's grow potatoes!", 4);
        }

        void Objective(Hud hud)
        {
            if (modelMode)
            {
                string S2(int n, string txt) => state > n ? $"<color=#7FE07F><s>{txt}</s></color>" : state == n ? $"<b>{txt}</b>" : $"<color=#9FB6D6>{txt}</color>";
                hud.Objective("GROW YOUR FOOD", $"{S2(0, "1. Switch on the LED GROW LIGHTS (by the door)")}\n{S2(1, "2. Turn on the WATER PUMP (big tank)")}\n{S2(2, "3. Wait: the potatoes grow!")}\n{S2(3, "4. Harvest (E on the soil beds)")}\n{S2(4, "5. Eat the potatoes (harvest crate)")}");
                return;
            }
            string Step(int n, string s) => state > n ? $"<color=#7FE07F><s>{s}</s></color>" : state == n ? $"<b>{s}</b>" : $"<color=#9FB6D6>{s}</color>";
            hud.Objective("GROW YOUR FOOD", $"{Step(0, "1. Plant seeds (E on a soil bed)")}\n{Step(1, "2. Turn on the WATER PUMP")}\n{Step(2, "3. Wait: they grow!")}\n{Step(3, "4. Harvest (E on a bed)")}\n{Step(4, "5. Eat the potatoes (crate)")}");
        }

        void Use() { StartCoroutine(Step()); }

        IEnumerator Step()
        {
            var hud = Game.Hud;
            if (modelMode) { yield return StepModel(hud); yield break; }
            switch (state)
            {
                case 0: // plant
                    state = 1; Sfx.Play("build", 0.6f);
                    foreach (var p in plants) StartCoroutine(Grow(p, 0.18f, 0.5f));
                    foreach (var b in beds) b.Verb = "Harvest";
                    pump.enabled = true; foreach (var b in beds) b.enabled = false;
                    Objective(hud);
                    hud.Say("Seeds planted! Now they need WATER. Turn on the WATER PUMP at the back.", 4);
                    break;
                case 1: // water
                    state = 2; pump.enabled = false; Sfx.Play("powerup", 0.5f);
                    waterMat.SetColor("_BaseColor", new Color(0.25f, 0.6f, 1f)); waterMat.SetFloat("_Smoothness", 0.95f);
                    Objective(hud);
                    yield return hud.SayWait("Water from melted Mars ice flows to the plants. Now watch them grow in the sunlight!", 3);
                    for (int stage = 1; stage <= 2; stage++)
                    {
                        yield return new WaitForSeconds(4f);
                        Sfx.Play("pickup", 0.5f, 0.8f + stage * 0.2f);
                        foreach (var p in plants) { StartCoroutine(Grow(p, stage == 1 ? 0.55f : 1f, 1.2f)); FX.BurstSparks(p.position + Vector3.up * 0.4f, new Color(0.5f, 1f, 0.4f), 8); }
                    }
                    yield return new WaitForSeconds(1.5f);
                    foreach (var pot in potatoes) pot.SetActive(true);
                    state = 3; foreach (var b in beds) b.enabled = true;
                    Objective(hud);
                    hud.Say("The potatoes are ready! Press E on a bed to HARVEST them.", 4);
                    break;
                case 3: // harvest
                    state = 4; Sfx.Play("pickup", 0.8f);
                    foreach (var pot in potatoes) pot.SetActive(false);
                    foreach (var p in plants) StartCoroutine(Grow(p, 0f, 0.6f));
                    foreach (var b in beds) b.enabled = false;
                    cratePile.SetActive(true); crate.enabled = true;
                    Objective(hud);
                    hud.Say("What a harvest! Take them to the harvest crate and EAT.", 4);
                    break;
                case 4: // eat
                    state = 0; crate.enabled = false; cratePile.SetActive(false);
                    foreach (var b in beds) { b.enabled = true; b.Verb = "Plant"; }
                    if (Game.Suit) Game.Suit.Drink(40);
                    Sfx.Play("success", 0.7f);
                    hud.Toast("YUM! FRESH MARS POTATOES", "Food gives you energy. Life support +40", "food", HC.Green);
                    AstroPass.Award("farm", "Grew your own food", 3);
                    Objective(hud);
                    yield return hud.SayWait("Real fact: in 2015 astronauts on the Space Station ate lettuce they grew in NASA's Veggie garden, under the same pink LED lights!", 5);
                    yield return hud.SayWait("And in 2017 scientists in Peru grew potatoes in a box that copied Mars conditions. Potatoes could really feed astronauts on Mars!", 5);
                    break;
            }
        }

        static Material leafA, leafB, stem;
        /// <summary>A leafy green potato plant: a short stem and a bunch of round leaves (real potato plants are bushy, about knee high).</summary>
        static void PotatoPlant(Transform p, int seed)
        {
            leafA = leafA ? leafA : R.Lit(new Color(0.28f, 0.68f, 0.24f), 0.35f);
            leafB = leafB ? leafB : R.Lit(new Color(0.42f, 0.8f, 0.3f), 0.35f);
            stem = stem ? stem : R.Lit(new Color(0.3f, 0.5f, 0.18f), 0.2f);
            var rng = new System.Random(seed * 31 + 7);
            ProcMesh.Prim(PrimitiveType.Cylinder, "Stem", p, new Vector3(0, 0.22f, 0), new Vector3(0.07f, 0.22f, 0.07f), stem, null, false);
            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.9f + (float)rng.NextDouble(), r = i == 0 ? 0 : 0.22f + (float)rng.NextDouble() * 0.12f;
                var pos = new Vector3(Mathf.Cos(a) * r, 0.42f + (i == 0 ? 0.18f : (float)rng.NextDouble() * 0.12f), Mathf.Sin(a) * r);
                float s = i == 0 ? 0.4f : 0.3f + (float)rng.NextDouble() * 0.1f;
                ProcMesh.Prim(PrimitiveType.Sphere, "Leaf", p, pos, new Vector3(s, s * 0.6f, s), i % 2 == 0 ? leafA : leafB, null, false);
            }
        }

        /// <summary>For screenshots: show fully grown plants with potatoes.</summary>
        public void ShowGrown() { if (modelMode) { foreach (var pot in potatoes) pot.SetActive(true); foreach (var gm in growMats) gm.SetColor("_EmissionColor", new Color(2.8f, 0.7f, 3f)); foreach (var gl in growLights) gl.intensity = 2.2f; return; } foreach (var p in plants) p.localScale = Vector3.one; foreach (var pot in potatoes) pot.SetActive(true); waterMat.SetColor("_BaseColor", new Color(0.25f, 0.6f, 1f)); }

        IEnumerator StepModel(Hud hud)
        {
            switch (state)
            {
                case 0: // LED grow lights on
                    state = 1; ledSwitch.enabled = false; pump.enabled = true; Sfx.Play("powerup", 0.6f);
                    foreach (var gm in growMats) gm.SetColor("_EmissionColor", new Color(2.8f, 0.7f, 3f));
                    foreach (var gl in growLights) gl.intensity = 2.2f;
                    Objective(hud);
                    hud.Say("Pink LED light! Plants use mostly RED and BLUE light, so that is all we give them. Now the WATER PUMP at the big tank.", 5);
                    break;
                case 1: // water
                    state = 2; pump.enabled = false; Sfx.Play("powerup", 0.5f, 0.8f);
                    Objective(hud);
                    yield return hud.SayWait("Water from melted Mars ice flows through the channels to every plant. Now they grow!", 3);
                    for (int g = 1; g <= 3; g++)
                    {
                        yield return new WaitForSeconds(3f);
                        Sfx.Play("pickup", 0.5f, 0.8f + g * 0.15f);
                        for (int n = 0; n < 12 && bedTops.Count > 0; n++) FX.BurstSparks(Room.transform.TransformPoint(bedTops[Random.Range(0, bedTops.Count)]) + Vector3.up * 0.5f, new Color(0.5f, 1f, 0.4f), 6);
                        hud.Toast($"GROWING… {g * 33 + 1}%", "In real life potatoes need about 3 months. Here it is fast!", "food", HC.Green);
                    }
                    foreach (var pot in potatoes) pot.SetActive(true);
                    state = 3; foreach (var b in beds) b.enabled = true;
                    Objective(hud);
                    hud.Say("The potatoes are ready! Walk to a soil bed and press E to HARVEST.", 4);
                    break;
                case 3: // harvest
                    state = 4; Sfx.Play("pickup", 0.8f);
                    foreach (var pot in potatoes) pot.SetActive(false);
                    foreach (var b in beds) b.enabled = false;
                    cratePile.SetActive(true); crate.enabled = true;
                    Objective(hud);
                    hud.Say("What a harvest! Take them to the harvest crate and EAT.", 4);
                    break;
                case 4: // eat
                    state = 1; crate.enabled = false; cratePile.SetActive(false); pump.enabled = true; // lights stay on: water again to grow more
                    if (Game.Suit) Game.Suit.Drink(40);
                    Sfx.Play("success", 0.7f);
                    hud.Toast("YUM! FRESH MARS POTATOES", "Food gives you energy. Life support +40", "food", HC.Green);
                    AstroPass.Award("farm", "Grew your own food", 3);
                    Objective(hud);
                    yield return hud.SayWait("Real fact: in 2015 astronauts on the Space Station ate lettuce they grew in NASA's Veggie garden, under pink LED lights!", 5);
                    yield return hud.SayWait("And in 2017 scientists in Peru grew potatoes in a box that copied Mars conditions. Potatoes could really feed astronauts!", 5);
                    break;
            }
        }

        static IEnumerator Grow(Transform p, float to, float d)
        {
            float from = p.localScale.x, t = 0;
            while (t < d) { t += Time.deltaTime; p.localScale = Vector3.one * Mathf.Lerp(from, to, Tween.Back(t / d)); yield return null; }
            p.localScale = Vector3.one * to;
        }
    }
}
