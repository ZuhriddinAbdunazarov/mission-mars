using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    /// <summary>A walk-in room made from one of the team's Tripo "cutaway interior" models (Assets/Paid, not in git).
    /// The model's real shape (a height map saved next to it) becomes the walls and furniture you bump into, so nothing
    /// invisible blocks you; free floor is where you walk. Adds a glass/white ceiling, lights, a sliding exit door
    /// and press-E zones on the furniture.</summary>
    public class ModelRoom
    {
        public GameObject Room; public Vector3 Spawn; public float SpawnYaw;
        public Interactable ExitDoor; public Transform ExitPanel;
        public float S; float minx, maxx, minz, maxz, floorY; int NX, NZ; float[,] h; Transform t;

        /// <summary>Position inside the room from fractions of the model's width (fx, left→right in the file) and depth (fz).</summary>
        public Vector3 Local(float fx, float fz) => new Vector3(-(minx + fx * (maxx - minx)) * S, 0, (minz + fz * (maxz - minz)) * S); // glTF → Unity mirrors X
        public float Width => (maxx - minx) * S; public float Depth => (maxz - minz) * S;

        public static ModelRoom Build(string model, Vector3 origin, float scale, Vector2 spawnFrac, Color ceiling, Color light)
        {
            var prefab = R.Model("P_" + model); var ta = Resources.Load<TextAsset>("Paid/" + model + "_heights");
            if (!prefab || !ta) return null;
            var r = new ModelRoom { S = scale };
            r.Make(prefab, ta.text, origin, spawnFrac, ceiling, light);
            return r;
        }

        void Make(GameObject prefab, string heights, Vector3 origin, Vector2 spawnFrac, Color ceiling, Color lightCol)
        {
            ReadHeights(heights);
            Room = new GameObject(prefab.name + "Room"); t = Room.transform; t.position = origin;
            var m = UnityEngine.Object.Instantiate(prefab, t, false); m.name = "Model";
            m.transform.localScale = Vector3.one * S; m.transform.localPosition = new Vector3(0, -floorY * S, 0);
            foreach (var rr in m.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // solid walls + furniture from the real shape; free floor stays walkable
            float walk = floorY + 0.018f, top = 0;
            var v = new List<Vector3>(); var tri = new List<int>();
            float cw = Width / NX, cd = Depth / NZ;
            for (int k = 0; k < NZ; k++)
                for (int i = 0; i < NX; i++)
                {
                    float y0 = h[i, k]; top = Mathf.Max(top, y0); if (y0 < walk) continue;
                    var c = Cell(i, k); float Y = Mathf.Max(0.4f, (y0 - floorY) * S);
                    Column(v, tri, c.x - cw / 2, c.x + cw / 2, c.z - cd / 2, c.z + cd / 2, Y);
                }
            var mesh = new Mesh { name = "RoomSolid", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
            var solid = new GameObject("Solid"); solid.transform.SetParent(t, false); solid.AddComponent<MeshCollider>().sharedMesh = mesh;
            var floor = new GameObject("Floor"); floor.transform.SetParent(t, false); floor.transform.localPosition = new Vector3(0, -0.1f, 0);
            floor.AddComponent<BoxCollider>().size = new Vector3(Width + 2, 0.2f, Depth + 2);

            // ceiling + lights
            float H = Mathf.Max(3.2f, (top - floorY) * S);
            ProcMesh.Prim(PrimitiveType.Cube, "Ceiling", t, new Vector3(0, H, 0), new Vector3(Width, 0.1f, Depth), R.Emissive(ceiling * 0.8f, ceiling), null, false);
            for (int i = -1; i <= 1; i += 2)
                for (int k = -1; k <= 1; k += 2)
                {
                    var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(t, false); l.transform.localPosition = new Vector3(i * Width * 0.25f, H - 0.6f, k * Depth * 0.25f);
                    l.type = LightType.Point; l.range = Mathf.Max(Width, Depth) * 0.75f; l.intensity = 2.6f; l.color = lightCol; l.shadows = LightShadows.None;
                }

            // spawn: the free spot nearest spawnFrac that has free floor around it; exit door just behind it
            int si = 0, sk = 0; float best = 1e9f;
            for (int k = 1; k < NZ - 1; k++)
                for (int i = 1; i < NX - 1; i++)
                {
                    bool free = true; for (int a = -1; a <= 1 && free; a++) for (int b = -1; b <= 1 && free; b++) if (h[i + a, k + b] >= walk) free = false;
                    if (!free) continue;
                    float d = new Vector2((i + 0.5f) / NX - spawnFrac.x, (k + 0.5f) / NZ - spawnFrac.y).sqrMagnitude;
                    if (d < best) { best = d; si = i; sk = k; }
                }
            var sp = Cell(si, sk); float back = spawnFrac.y > 0.5f ? 1 : -1; // the door is on the nearer end wall
            Spawn = t.TransformPoint(sp + Vector3.up * 0.05f); SpawnYaw = back > 0 ? 180 : 0;
            var door = new GameObject("DoorPanel").transform; door.SetParent(t, false);
            door.localPosition = sp + new Vector3(0, 1.2f, back * 1.3f); door.localRotation = Quaternion.Euler(0, back > 0 ? 180 : 0, 0);
            ProcMesh.Prim(PrimitiveType.Cube, "Doorway", door, new Vector3(0, 0, -0.1f), new Vector3(1.4f, 2.4f, 0.02f), R.Emissive(new Color(0.9f, 0.55f, 0.35f), new Color(1.6f, 0.85f, 0.5f)), null, false);
            var slab = new GameObject("Slab").transform; slab.SetParent(door, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Panel", slab, Vector3.zero, new Vector3(1.5f, 2.45f, 0.1f), R.Panel);
            ProcMesh.Prim(PrimitiveType.Cube, "Stripe", slab, new Vector3(0, 0.9f, 0.06f), new Vector3(1.5f, 0.16f, 0.02f), R.Orange, null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Window", slab, new Vector3(0, 0.4f, 0.06f), new Vector3(0.6f, 0.32f, 0.02f), R.Emissive(new Color(0.05f, 0.15f, 0.2f), new Color(0.2f, 0.9f, 1.3f)), null, false);
            var sd = slab.gameObject.AddComponent<SlideDoor>(); sd.Closed = Vector3.zero; sd.Slide = 1.55f;
            ExitPanel = slab; ExitDoor = Interactable.Add(slab.gameObject, "Go outside", "Mars base", null);
            var exitSign = MarsWorld.World3DText(door, new Vector3(0, 1.55f, 0.08f), "EXIT", 2f, new Color(0.4f, 1f, 0.6f)); exitSign.transform.localRotation = Quaternion.Euler(0, 180, 0);
            Room.SetActive(false);
        }

        /// <summary>A press-E zone over part of the room (fractions of the model), e.g. the beds. It sits inside the
        /// furniture, so you stand next to it and look at it.</summary>
        public Interactable Zone(string name, float fx, float fz, float sx, float sz, float height, string verb, string label, Action use, float range = 4.5f)
        {
            var g = new GameObject(name); g.transform.SetParent(t, false); g.transform.localPosition = Local(fx, fz) + Vector3.up * height / 2;
            var bc = g.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(sx * Width, height, sz * Depth);
            var it = Interactable.Add(g, verb, label, use, 0, false); it.Range = range; return it;
        }

        /// <summary>The nearest free floor spot to a fraction position (for placing our own props).</summary>
        public Vector3 FreeSpot(float fx, float fz)
        {
            float walk = floorY + 0.018f; int bi = NX / 2, bk = NZ / 2; float best = 1e9f;
            for (int k = 1; k < NZ - 1; k++) for (int i = 1; i < NX - 1; i++)
                {
                    if (h[i, k] >= walk) continue;
                    float d = new Vector2((i + 0.5f) / NX - fx, (k + 0.5f) / NZ - fz).sqrMagnitude; if (d < best) { best = d; bi = i; bk = k; }
                }
            return Cell(bi, bk);
        }
        public Transform T => t;

        Vector3 Cell(int i, int k) => Local((i + 0.5f) / NX, (k + 0.5f) / NZ);

        void ReadHeights(string text)
        {
            var lines = new List<string>(); foreach (var l in text.Split('\n')) { var tl = l.Trim(); if (tl.Length > 0 && tl[0] != '#') lines.Add(tl); }
            var hd = lines[0].Split(' '); var inv = System.Globalization.CultureInfo.InvariantCulture;
            NX = int.Parse(hd[0]); NZ = int.Parse(hd[1]); minx = float.Parse(hd[2], inv); maxx = float.Parse(hd[3], inv); minz = float.Parse(hd[4], inv); maxz = float.Parse(hd[5], inv);
            h = new float[NX, NZ]; var all = new List<float>();
            for (int k = 0; k < NZ; k++) { var row = lines[1 + k].Split(' '); for (int i = 0; i < NX; i++) { h[i, k] = float.Parse(row[i], inv); all.Add(h[i, k]); } }
            // the floor = the most common low height
            all.Sort(); float low = all[(int)(all.Count * 0.6f)]; var bins = new int[200];
            foreach (var y in all) if (y <= low) bins[Mathf.Clamp((int)(y * 200), 0, 199)]++;
            int mb = 0; for (int i = 1; i < 200; i++) if (bins[i] > bins[mb]) mb = i;
            floorY = (mb + 1) / 200f;
        }

        static void Column(List<Vector3> v, List<int> tri, float x0, float x1, float z0, float z1, float Y)
        {
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { int n = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); tri.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3, n, n + 2, n + 1, n, n + 3, n + 2 }); }
            Quad(new Vector3(x0, Y, z0), new Vector3(x1, Y, z0), new Vector3(x1, Y, z1), new Vector3(x0, Y, z1));
            Quad(new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x0, Y, z1), new Vector3(x0, Y, z0));
            Quad(new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x1, Y, z1), new Vector3(x1, Y, z0));
            Quad(new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, Y, z0), new Vector3(x0, Y, z0));
            Quad(new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, Y, z1), new Vector3(x0, Y, z1));
        }

        /// <summary>Solid walls for an OUTSIDE building model (scaled by S, origin at its bottom centre), from its height map:
        /// you bump into what you see, nothing else.</summary>
        public static void OutsideWalls(GameObject building, string model, float S)
        {
            var ta = Resources.Load<TextAsset>("Paid/" + model + "_heights"); if (!ta) return;
            var r = new ModelRoom { S = S }; r.ReadHeights(ta.text);
            var v = new List<Vector3>(); var tri = new List<int>();
            float cw = r.Width / r.NX, cd = r.Depth / r.NZ;
            for (int k = 0; k < r.NZ; k++)
                for (int i = 0; i < r.NX; i++)
                {
                    float y0 = r.h[i, k]; if (y0 < 0.03f) continue;
                    var c = r.Cell(i, k);
                    Column(v, tri, c.x - cw / 2, c.x + cw / 2, c.z - cd / 2, c.z + cd / 2, y0 * S);
                }
            var mesh = new Mesh { name = model + "Walls", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
            var g = new GameObject("Walls"); g.transform.SetParent(building.transform, false); g.transform.localScale = Vector3.one / S; // the building is scaled by S
            g.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>Front (+z) door spot of an outside model, in its own (scaled) local space.</summary>
        public static Vector3 FrontDoorLocal(string model)
        {
            var ta = Resources.Load<TextAsset>("Paid/" + model + "_heights"); if (!ta) return new Vector3(0, 0.07f, 0.5f);
            var r = new ModelRoom { S = 1 }; r.ReadHeights(ta.text); return new Vector3(0, 0.07f, r.maxz);
        }
    }
}
