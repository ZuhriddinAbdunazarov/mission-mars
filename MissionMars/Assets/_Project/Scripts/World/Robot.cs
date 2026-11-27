using UnityEngine;

namespace MissionMars
{
    /// <summary>NOVA — a small helper drone that floats next to the player, points the way and talks.</summary>
    public class Robot : MonoBehaviour
    {
        public Transform Body, Eye; Light eyeLight;
        public Vector3? Lead;                // when set, the drone flies there and waits
        public bool Follow = true;
        Material eyeMat, ringMat, tipMat; Transform tip; float talk;
        public bool Party; // Astronaut Pass reward: rainbow antenna light
        /// <summary>Astronaut Pass reward: a shiny gold ring.</summary>
        public void GoldRing() { ringMat.SetColor("_BaseColor", new Color(1f, 0.78f, 0.2f)); ringMat.SetFloat("_Metallic", 0.9f); ringMat.SetFloat("_Smoothness", 0.8f); FX.BurstSparks(Body.position, new Color(1f, 0.8f, 0.3f), 50); }
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
            ringMat = R.Lit(new Color(0.95f, 0.45f, 0.12f), 0.4f);
            ProcMesh.Prim(PrimitiveType.Cylinder, "Ring", Body, new Vector3(0, -0.05f, 0), new Vector3(0.7f, 0.025f, 0.7f), ringMat, null, false);
            for (int s = -1; s <= 1; s += 2)
            {
                var pod = ProcMesh.Prim(PrimitiveType.Capsule, "Pod", Body, new Vector3(s * 0.38f, -0.06f, 0), new Vector3(0.14f, 0.12f, 0.14f), shell, new Vector3(90, 0, 0), false);
                ProcMesh.Prim(PrimitiveType.Sphere, "Jet", Body, new Vector3(s * 0.38f, -0.16f, 0), new Vector3(0.09f, 0.04f, 0.09f), eyeMat, null, false);
            }
            ProcMesh.Prim(PrimitiveType.Cylinder, "Antenna", Body, new Vector3(0.1f, 0.34f, -0.05f), new Vector3(0.015f, 0.12f, 0.015f), dark, new Vector3(0, 0, -12), false);
            tipMat = R.Emissive(Color.black, new Color(3f, 0.9f, 0.2f));
            tip = ProcMesh.Prim(PrimitiveType.Sphere, "Tip", Body, new Vector3(0.13f, 0.47f, -0.05f), Vector3.one * 0.05f, tipMat, null, false).transform;
            eyeLight = new GameObject("EyeLight").AddComponent<Light>(); eyeLight.transform.SetParent(Body, false); eyeLight.transform.localPosition = new Vector3(0, 0, 0.6f);
            eyeLight.type = LightType.Point; eyeLight.range = 3.5f; eyeLight.intensity = 1.6f; eyeLight.color = new Color(0.4f, 0.9f, 1f); eyeLight.shadows = LightShadows.None;
        }

        public void Talk(float dur) { talk = Mathf.Max(talk, dur); }
        public void Teleport(Vector3 p) { transform.position = p; vel = Vector3.zero; spot = p; rethink = 0; }

        Vector3 spot; float rethink; bool wasTalking, lastTalkSpot;
        /// <summary>A free place near the player: not inside the rover, a building or a hill, and with a clear view to the player.</summary>
        Vector3 FreeSpot(PlayerController p, bool talking)
        {
            var cam = p.Cam.transform;
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up); if (fwd.sqrMagnitude < 0.01f) fwd = p.transform.forward; fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            var head = cam.position;
            Vector3[] tries = talking
                ? new[] { fwd * 2.8f + right * 1.6f, fwd * 2.8f - right * 1.6f, right * 2.6f + fwd * 0.6f, -right * 2.6f + fwd * 0.6f }
                : new[] { right * 3.2f + fwd * 0.8f, -right * 3.2f + fwd * 0.8f, right * 2.6f - fwd * 1.6f, -right * 2.6f - fwd * 1.6f };
            bool outside = !Game.Suit || !Game.Suit.Inside;
            foreach (var off in tries)
            {
                var c = p.transform.position + off + Vector3.up * (talking ? 1.35f : 1.7f);
                if (outside) c.y = Mathf.Max(c.y, MarsTerrain.H(c.x, c.z) + 1.3f);
                if (Physics.CheckSphere(c, 0.5f, ~0, QueryTriggerInteraction.Ignore)) continue;               // inside something
                var d = c - head;
                if (Physics.Raycast(head, d.normalized, out var h, d.magnitude, ~0, QueryTriggerInteraction.Ignore) && !h.collider.GetComponent<CharacterController>()) continue; // a wall between
                return c;
            }
            return p.transform.position + Vector3.up * 2.8f - fwd * 0.5f; // nowhere free: float just above and behind
        }

        void Update()
        {
            float dt = Time.deltaTime, t = Time.time;
            var p = Game.Player;
            Vector3 target = transform.position;
            Vector3 lookAt = p ? p.Cam.transform.position : transform.position + Vector3.forward;
            if (Lead.HasValue) { target = Lead.Value; }
            else if (Follow && p)
            {
                // NOVA keeps a little distance: about 3 m to the side. She comes into view only when she talks.
                // She only flies when you walked away from her spot, so she does not dance around in front of you.
                rethink -= dt;
                bool far = (transform.position - p.transform.position).sqrMagnitude > 6.5f * 6.5f;
                if (rethink <= 0 || far || (talk > 0) != wasTalking)
                {
                    rethink = 0.35f; wasTalking = talk > 0;
                    var cand = FreeSpot(p, talk > 0);
                    if (far || (cand - spot).sqrMagnitude > 2.2f * 2.2f || (talk > 0) != lastTalkSpot) { spot = cand; lastTalkSpot = talk > 0; }
                }
                target = spot;
            }
            // never fly through a wall, the rover or the shelter mound: go over it
            var step = target - transform.position;
            if (step.sqrMagnitude > 0.04f && Physics.SphereCast(transform.position, 0.35f, step.normalized, out var hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.GetComponent<CharacterController>())
                target = transform.position + Vector3.up * 1.5f + step.normalized * Mathf.Max(0, hit.distance - 0.5f);
            transform.position = Vector3.SmoothDamp(transform.position, target, ref vel, Lead.HasValue ? 0.9f : 0.7f, 10f);
            var dir = lookAt - transform.position; dir.y *= 0.4f;
            if (Lead.HasValue && p && (transform.position - target).sqrMagnitude > 4) dir = vel;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 4);
            Body.localPosition = Vector3.up * Mathf.Sin(t * 2.2f) * 0.07f;
            Body.localRotation = Quaternion.Euler(Mathf.Clamp(vel.magnitude * 3, 0, 18), 0, Mathf.Sin(t * 1.3f) * 4);
            talk -= dt;
            float pulse = talk > 0 ? 0.6f + Mathf.Abs(Mathf.Sin(t * 14)) * 0.8f : 1f;
            Eye.localScale = new Vector3(0.2f, 0.09f * (talk > 0 ? 0.6f + Mathf.Abs(Mathf.Sin(t * 14)) * 0.6f : (Mathf.Repeat(t, 4f) < 0.12f ? 0.15f : 1f)), 0.04f);
            eyeLight.intensity = 1.6f * pulse;
            if (Party && tip) { var c = Color.HSVToRGB(Mathf.Repeat(t * 0.5f, 1), 0.9f, 1) * 4f; tipMat.SetColor("_EmissionColor", c); tip.localScale = Vector3.one * (0.09f + Mathf.Abs(Mathf.Sin(t * 6)) * 0.04f); }
        }
    }
}
