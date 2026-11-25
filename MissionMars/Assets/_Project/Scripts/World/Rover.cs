using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    /// <summary>A cartoon crew rover the player can drive: W/S drive, A/D steer, E get in / get out.
    /// Real NASA crew rovers (the Apollo Lunar Roving Vehicle) let astronauts travel much farther than on foot.</summary>
    public class Rover : MonoBehaviour
    {
        public bool Driving { get; private set; }
        public bool Turbo;                 // Astronaut Pass reward: hold SHIFT for extra speed
        public float MaxSpeed = 7f; // about 25 km/h: real Mars rovers drive slowly and carefully
        public Interactable Use;
        public System.Action OnFirstDrive;
        readonly List<Transform> wheels = new List<Transform>();
        float tireRadius = 0.48f;
        float speed, yaw, camYaw; Vector3 camPos; int enterFrame; bool everDriven;
        AudioSource hum; Light headlight;

        public static Rover Create(Vector3 pos, float yawDeg, Transform parent)
        {
            var g = new GameObject("Rover"); g.transform.SetParent(parent, false);
            var r = g.AddComponent<Rover>(); r.Build(); r.yaw = yawDeg; r.Place(pos, true);
            return r;
        }

        /// <summary>The team's bought rover model (Assets/Paid, not in git). It is long along its X axis with the cabin at -X: turn it so the cabin faces +Z.</summary>
        bool BuildModel()
        {
            var prefab = R.Model("P_MarsBaseCrewRover"); if (!prefab) return false;
            var m = Instantiate(prefab, transform, false); m.name = "Model";
            m.transform.localRotation = Quaternion.Euler(0, -90, 0); // cabin to the front (+Z)
            var b = Util.Bounds(m);
            m.transform.localScale = Vector3.one * (4.8f / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z)));
            b = Util.Bounds(m);
            m.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
            foreach (var r in m.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            b = Util.Bounds(m);
            foreach (var part in m.GetComponentsInChildren<Transform>())
                if (part.name.StartsWith("Tire_")) wheels.Add(part);
            if (wheels.Count > 0) tireRadius = Util.Bounds(wheels[0].gameObject).extents.y;
            var bc = gameObject.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size;
            Use = Interactable.Add(gameObject, "Drive", "MARS ROVER", Enter, 0, false); Use.Range = 5.5f;
            headlight = new GameObject("Headlight").AddComponent<Light>(); headlight.transform.SetParent(transform, false);
            headlight.transform.localPosition = new Vector3(0, b.size.y * 0.6f, b.max.z + 0.2f); headlight.transform.localRotation = Quaternion.Euler(12, 0, 0);
            headlight.type = LightType.Spot; headlight.spotAngle = 70; headlight.range = 30; headlight.intensity = 0; headlight.shadows = LightShadows.None;
            return true;
        }

        void Build()
        {
            if (BuildModel()) return;
            var t = transform;
            var white = R.Lit(new Color(0.95f, 0.95f, 0.97f), 0.55f, 0.05f);
            var rubber = R.Lit(new Color(0.12f, 0.12f, 0.13f), 0.15f, 0f);
            var glass = R.Emissive(new Color(0.25f, 0.55f, 0.8f), new Color(0.05f, 0.25f, 0.45f), 0.95f);
            ProcMesh.Prim(PrimitiveType.Cube, "Chassis", t, new Vector3(0, 0.95f, 0), new Vector3(2.2f, 0.55f, 4.0f), white, null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Stripe", t, new Vector3(0, 1.02f, 0), new Vector3(2.24f, 0.14f, 4.04f), R.Orange, null, false);
            ProcMesh.Prim(PrimitiveType.Sphere, "Cabin", t, new Vector3(0, 1.45f, 0.55f), new Vector3(1.9f, 1.35f, 2.1f), glass, null, false);
            ProcMesh.Prim(PrimitiveType.Cylinder, "CabinRing", t, new Vector3(0, 1.25f, 0.55f), new Vector3(2.0f, 0.06f, 2.2f), white, null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Cargo", t, new Vector3(0, 1.45f, -1.35f), new Vector3(1.9f, 0.55f, 1.2f), R.Panel, null, false);
            ProcMesh.Prim(PrimitiveType.Cube, "Bumper", t, new Vector3(0, 0.8f, 2.05f), new Vector3(2.0f, 0.22f, 0.15f), R.HullDark, null, false);
            for (int s = -1; s <= 1; s += 2)
                ProcMesh.Prim(PrimitiveType.Cube, "Lamp", t, new Vector3(s * 0.7f, 1.0f, 2.03f), new Vector3(0.35f, 0.16f, 0.05f), R.Emissive(Color.black, new Color(2.5f, 2.4f, 2f)), null, false);
            // antenna with a little dish (talks to Earth through orbiters)
            ProcMesh.Prim(PrimitiveType.Cylinder, "Mast", t, new Vector3(-0.65f, 2.0f, -1.5f), new Vector3(0.05f, 0.45f, 0.05f), R.HullDark, null, false);
            var dish = ProcMesh.MeshObj("Dish", ProcMesh.Dome(16, 5, 0), white, t);
            dish.transform.localPosition = new Vector3(-0.65f, 2.5f, -1.5f); dish.transform.localScale = new Vector3(0.45f, 0.15f, 0.45f); dish.transform.localEulerAngles = new Vector3(-60, 0, 0);
            ProcMesh.Prim(PrimitiveType.Sphere, "Beacon", t, new Vector3(0.65f, 1.8f, -1.6f), Vector3.one * 0.18f, R.Emissive(Color.black, new Color(3f, 1.2f, 0.2f)), null, false);
            // 6 big wheels (they spin when you drive)
            for (int s = -1; s <= 1; s += 2)
                for (int k = -1; k <= 1; k++)
                {
                    var w = new GameObject("Wheel").transform; w.SetParent(t, false); w.localPosition = new Vector3(s * 1.28f, 0.48f, k * 1.45f);
                    ProcMesh.Prim(PrimitiveType.Cylinder, "Tyre", w, Vector3.zero, new Vector3(0.95f, 0.17f, 0.95f), rubber, new Vector3(0, 0, 90), false);
                    ProcMesh.Prim(PrimitiveType.Cylinder, "Hub", w, new Vector3(s * 0.18f, 0, 0), new Vector3(0.42f, 0.03f, 0.42f), R.Orange, new Vector3(0, 0, 90), false);
                    ProcMesh.Prim(PrimitiveType.Cube, "Spoke", w, new Vector3(s * 0.19f, 0, 0), new Vector3(0.02f, 0.75f, 0.1f), R.HullDark, null, false);
                    wheels.Add(w);
                }
            var bc = gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1.2f, 0); bc.size = new Vector3(2.8f, 2.0f, 4.3f);
            Use = Interactable.Add(gameObject, "Drive", "MARS ROVER", Enter, 0, false); Use.Range = 5.5f;
            headlight = new GameObject("Headlight").AddComponent<Light>(); headlight.transform.SetParent(t, false);
            headlight.transform.localPosition = new Vector3(0, 1.3f, 2.2f); headlight.transform.localRotation = Quaternion.Euler(12, 0, 0);
            headlight.type = LightType.Spot; headlight.spotAngle = 70; headlight.range = 30; headlight.intensity = 0; headlight.shadows = LightShadows.None;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        public void Park(Vector3 position, float heading)
        {
            yaw = heading;
            Place(position, true);
        }

        void Place(Vector3 p, bool snap)
        {
            p.y = MarsTerrain.H(p.x, p.z);
            transform.position = p;
            var n = MarsTerrain.Normal(p.x, p.z);
            var q = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, n, 0.8f)) * Quaternion.Euler(0, yaw, 0);
            transform.rotation = snap ? q : Quaternion.Slerp(transform.rotation, q, Time.deltaTime * 8);
        }

        public void Enter()
        {
            var p = Game.Player; if (!p || Driving) return;
            Driving = true; enterFrame = Time.frameCount;
            p.Frozen = true; Use.enabled = false; GetComponent<BoxCollider>().enabled = false;
            speed = 0; camYaw = 0;
            camPos = transform.position + Vector3.up * 3.5f - transform.forward * 9f;
            headlight.intensity = 6;
            if (AudioManager.I) { if (!hum) hum = AudioManager.I.Loop("hum", 0.0f, transform, 0); AudioManager.I.FadeSource(hum, 0.45f, 0.5f); }
            Sfx.Play("powerup", 0.4f, 1.3f);
            if (!everDriven) { everDriven = true; OnFirstDrive?.Invoke(); }
        }

        /// <summary>Get out next to the rover.</summary>
        public void Exit()
        {
            var p = Game.Player; if (!Driving) return;
            Driving = false; Use.enabled = true; speed = 0; GetComponent<BoxCollider>().enabled = true;
            headlight.intensity = 0;
            if (hum && AudioManager.I) AudioManager.I.FadeSource(hum, 0, 0.4f);
            if (!p) return;
            var side = transform.position + transform.right * 2.8f; side.y = MarsTerrain.H(side.x, side.z) + 0.1f;
            p.Cam.transform.localPosition = Vector3.zero; p.Cam.transform.localRotation = Quaternion.identity;
            p.Teleport(side, yaw);
            p.Frozen = false;
            Sfx.Play("thud", 0.3f, 1.2f);
        }

        void Update()
        {
            if (!Driving) return;
            var p = Game.Player; if (!p) return;
            float dt = Time.deltaTime;
            bool control = !UIState.Modal && !UIState.Cinematic && !Game.Paused;
            float th = 0, st = 0;
            if (control)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) th += 1;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) th -= 1;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) st += 1;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) st -= 1;
            }
            float max = MaxSpeed * (Turbo && Input.GetKey(KeyCode.LeftShift) ? 1.6f : 1f);
            float target = th > 0 ? max : th < 0 ? -max * 0.45f : 0;
            float rate = Mathf.Abs(target) > Mathf.Abs(speed) ? 6f : 11f;
            speed = Mathf.MoveTowards(speed, target, rate * dt);
            yaw += st * Mathf.Clamp01(Mathf.Abs(speed) / 3f) * 75f * dt * (speed < 0 ? -1 : 1);

            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var pos = transform.position;
            var step = fwd * speed * dt;
            // bump into big rocks and buildings (but drive up gentle hills)
            if (Mathf.Abs(speed) > 0.2f)
            {
                var dir = fwd * Mathf.Sign(speed);
                foreach (var h in Physics.SphereCastAll(pos + Vector3.up * 1.3f, 0.9f, dir, Mathf.Abs(speed * dt) + 1.4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.collider.transform.IsChildOf(transform) || h.collider.transform.IsChildOf(p.transform)) continue;
                    if (h.normal.y > 0.6f || h.distance <= 0) continue;
                    if (Mathf.Abs(speed) > 5) { Sfx.Play("thud", 0.6f, 0.8f); p.Shake(0.25f, 0.25f); }
                    speed = -speed * 0.3f; step = Vector3.zero; break;
                }
            }
            pos += step;
            pos.x = Mathf.Clamp(pos.x, -300, 300); pos.z = Mathf.Clamp(pos.z, -300, 300);
            float travel = Vector3.Dot(pos - transform.position, fwd);
            Place(pos, false);
            foreach (var w in wheels) w.Rotate(transform.right, travel / tireRadius * Mathf.Rad2Deg, Space.World);
            if (hum) hum.pitch = 0.7f + Mathf.Abs(speed) / max * 0.9f;
            if (Mathf.Abs(speed) > 4 && Random.value < dt * 6) FX.DustBlast(transform.position - fwd * 2f, 0.25f);

            // the astronaut rides inside: keep the player (for air, radiation, markers) at the rover
            p.Teleport(transform.position + Vector3.up * 0.2f, yaw);
            if (control && Input.GetKeyDown(KeyCode.E) && Time.frameCount > enterFrame + 1) Exit();
        }

        void LateUpdate()
        {
            if (!Driving) return;
            var p = Game.Player; if (!p) return;
            float dt = Time.deltaTime;
            if (!UIState.Modal) camYaw += Input.GetAxisRaw("Mouse X") * PlayerController.Sensitivity * 1.5f;
            if (Mathf.Abs(speed) > 1) camYaw = Mathf.Lerp(camYaw, 0, dt * 1.2f); // swing back behind the rover while driving
            var q = Quaternion.Euler(0, yaw + camYaw, 0);
            var focus = transform.position + Vector3.up * 2.2f;
            var want = focus - q * Vector3.forward * 9f + Vector3.up * 2.2f;
            float gy = MarsTerrain.H(want.x, want.z) + 1.2f; if (want.y < gy) want.y = gy;
            camPos = Vector3.Lerp(camPos, want, 1 - Mathf.Exp(-dt * 7));
            var cam = p.Cam.transform;
            cam.position = camPos;
            cam.rotation = Quaternion.LookRotation(focus + q * Vector3.forward * 3f - camPos);
        }
    }
}
