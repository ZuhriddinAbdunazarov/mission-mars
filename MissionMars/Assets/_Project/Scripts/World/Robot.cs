using UnityEngine;

namespace MissionMars
{
    /// <summary>NOVA — a small helper drone that floats next to the player, points the way and talks.</summary>
    public class Robot : MonoBehaviour
    {
        public Transform Body, Eye; Light eyeLight;
        public Vector3? Lead;                // when set, the drone flies there and waits
        public bool Follow = true;
        Material eyeMat; float talk;
        Vector3 vel;

        public static Robot Create(Vector3 pos)
        {
            var g = new GameObject("NOVA"); g.transform.position = pos;
            var r = g.AddComponent<Robot>(); r.Build(); Game.Bot = r; return r;
        }

        void Build()
        {
            Body = new GameObject("Body").transform; Body.SetParent(transform, false);
            var shell = R.Lit(new Color(0.88f, 0.89f, 0.9f), 0.75f, 0.1f);
            var dark = R.Lit(new Color(0.05f, 0.06f, 0.07f), 0.9f, 0.3f);
            eyeMat = R.Emissive(new Color(0.05f, 0.2f, 0.25f), new Color(0.3f, 2.4f, 3.2f));
            ProcMesh.Prim(PrimitiveType.Sphere, "Shell", Body, Vector3.zero, new Vector3(0.62f, 0.56f, 0.62f), shell, null, false);
            ProcMesh.Prim(PrimitiveType.Sphere, "Visor", Body, new Vector3(0, 0.03f, 0.17f), new Vector3(0.46f, 0.32f, 0.34f), dark, null, false);
            Eye = ProcMesh.Prim(PrimitiveType.Sphere, "Eye", Body, new Vector3(0, 0.04f, 0.33f), new Vector3(0.2f, 0.09f, 0.04f), eyeMat, null, false).transform;
            ProcMesh.Prim(PrimitiveType.Cylinder, "Ring", Body, new Vector3(0, -0.05f, 0), new Vector3(0.7f, 0.025f, 0.7f), R.Orange, null, false);
            for (int s = -1; s <= 1; s += 2)
            {
                var pod = ProcMesh.Prim(PrimitiveType.Capsule, "Pod", Body, new Vector3(s * 0.38f, -0.06f, 0), new Vector3(0.14f, 0.12f, 0.14f), shell, new Vector3(90, 0, 0), false);
                ProcMesh.Prim(PrimitiveType.Sphere, "Jet", Body, new Vector3(s * 0.38f, -0.16f, 0), new Vector3(0.09f, 0.04f, 0.09f), eyeMat, null, false);
            }
            ProcMesh.Prim(PrimitiveType.Cylinder, "Antenna", Body, new Vector3(0.1f, 0.34f, -0.05f), new Vector3(0.015f, 0.12f, 0.015f), dark, new Vector3(0, 0, -12), false);
            ProcMesh.Prim(PrimitiveType.Sphere, "Tip", Body, new Vector3(0.13f, 0.47f, -0.05f), Vector3.one * 0.05f, R.Emissive(Color.black, new Color(3f, 0.9f, 0.2f)), null, false);
            eyeLight = new GameObject("EyeLight").AddComponent<Light>(); eyeLight.transform.SetParent(Body, false); eyeLight.transform.localPosition = new Vector3(0, 0, 0.6f);
            eyeLight.type = LightType.Point; eyeLight.range = 3.5f; eyeLight.intensity = 1.6f; eyeLight.color = new Color(0.4f, 0.9f, 1f); eyeLight.shadows = LightShadows.None;
        }

        public void Talk(float dur) { talk = Mathf.Max(talk, dur); }
        public void Teleport(Vector3 p) { transform.position = p; vel = Vector3.zero; }

        void Update()
        {
            float dt = Time.deltaTime, t = Time.time;
            var p = Game.Player;
            Vector3 target = transform.position;
            Vector3 lookAt = p ? p.Cam.transform.position : transform.position + Vector3.forward;
            if (Lead.HasValue) { target = Lead.Value; }
            else if (Follow && p)
            {
                var cam = p.Cam.transform;
                var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                var right = Vector3.Cross(Vector3.up, fwd);
                target = p.transform.position + fwd * 2.2f + right * 1.5f + Vector3.up * 2.1f;
                if (!Game.Suit || !Game.Suit.Inside)
                {
                    float gy = MarsTerrain.H(target.x, target.z) + 1.2f;
                    if (target.y < gy) target.y = gy;
                }
            }
            transform.position = Vector3.SmoothDamp(transform.position, target, ref vel, Lead.HasValue ? 0.9f : 0.45f, 14f);
            var dir = lookAt - transform.position; dir.y *= 0.4f;
            if (Lead.HasValue && p && (transform.position - target).sqrMagnitude > 4) dir = vel;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 4);
            Body.localPosition = Vector3.up * Mathf.Sin(t * 2.2f) * 0.07f;
            Body.localRotation = Quaternion.Euler(Mathf.Clamp(vel.magnitude * 3, 0, 18), 0, Mathf.Sin(t * 1.3f) * 4);
            talk -= dt;
            float pulse = talk > 0 ? 0.6f + Mathf.Abs(Mathf.Sin(t * 14)) * 0.8f : 1f;
            Eye.localScale = new Vector3(0.2f, 0.09f * (talk > 0 ? 0.6f + Mathf.Abs(Mathf.Sin(t * 14)) * 0.6f : (Mathf.Repeat(t, 4f) < 0.12f ? 0.15f : 1f)), 0.04f);
            eyeLight.intensity = 1.6f * pulse;
        }
    }
}
