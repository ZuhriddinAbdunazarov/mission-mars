using UnityEngine;

namespace MissionMars
{
    /// <summary>First-person astronaut. Mars gravity = 3.71 m/s^2 (38% of Earth) — jumps feel floaty, as they should.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public const float MarsGravity = 3.71f;
        public Camera Cam;
        public Transform Head;
        public Light HelmetLamp;
        public float WalkSpeed = 4.2f, SprintSpeed = 7.5f, JumpSpeed = 3.4f;
        public static float Sensitivity = 2.0f;
        public bool Frozen;
        public float SpeedMul = 1f;
        public float HeadHeight = 1.62f, ExtraRoll, ExtraPitch;
        public float YawDeg => yaw; public float PitchDeg => pitch;

        CharacterController cc;
        float yaw, pitch, vy, bob, stepT;
        Vector3 vel;
        public float Sway; // radiation sickness sway 0..1
        public float ShakeAmt;
        float shakeT;
        public bool Grounded => cc && cc.isGrounded;
        public float Speed01 { get; private set; }

        public static PlayerController Create(Vector3 pos, float yawDeg)
        {
            var g = new GameObject("Player");
            g.transform.position = pos;
            var cc = g.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.38f; cc.center = new Vector3(0, 0.9f, 0); cc.slopeLimit = 50; cc.stepOffset = 0.45f;
            var p = g.AddComponent<PlayerController>();
            var head = new GameObject("Head").transform; head.SetParent(g.transform, false); head.localPosition = new Vector3(0, 1.62f, 0);
            p.Head = head;
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            camGo.transform.SetParent(head, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 6000; cam.fieldOfView = 68;
            camGo.AddComponent<AudioListener>();
            p.Cam = cam;
            var lamp = new GameObject("HelmetLamp").AddComponent<Light>();
            lamp.transform.SetParent(camGo.transform, false); lamp.transform.localPosition = new Vector3(0.15f, 0.1f, 0);
            lamp.type = LightType.Spot; lamp.spotAngle = 70; lamp.innerSpotAngle = 30; lamp.range = 38; lamp.intensity = 0;
            lamp.color = new Color(0.9f, 0.95f, 1f); lamp.shadows = LightShadows.None;
            p.HelmetLamp = lamp;
            p.yaw = yawDeg;
            return p;
        }

        void Awake() { cc = GetComponent<CharacterController>(); }

        public void Teleport(Vector3 pos, float yawDeg, float pitchDeg = 0)
        {
            cc.enabled = false; transform.position = pos; cc.enabled = true;
            yaw = yawDeg; pitch = pitchDeg; vy = 0; vel = Vector3.zero;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Head.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        public void LookAt(Vector3 world)
        {
            var d = world - Head.position;
            yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            pitch = -Mathf.Asin(Mathf.Clamp(d.normalized.y, -1, 1)) * Mathf.Rad2Deg;
        }

        public void Shake(float amt, float dur) { ShakeAmt = Mathf.Max(ShakeAmt, amt); shakeT = Mathf.Max(shakeT, dur); }

        void Update()
        {
            bool canControl = !Frozen && !UIState.Modal;
            float dt = Time.deltaTime;

            if (canControl)
            {
                yaw += Input.GetAxisRaw("Mouse X") * Sensitivity;
                pitch -= Input.GetAxisRaw("Mouse Y") * Sensitivity;
                pitch = Mathf.Clamp(pitch, -85, 85);
            }

            Vector3 input = Vector3.zero;
            if (canControl)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.z += 1;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.z -= 1;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1;
            }
            input = Vector3.ClampMagnitude(input, 1);
            bool sprint = canControl && Input.GetKey(KeyCode.LeftShift) && input.z > 0;
            float spd = (sprint ? SprintSpeed : WalkSpeed) * SpeedMul;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Vector3 target = transform.TransformDirection(input) * spd;
            float accel = cc.isGrounded ? 10f : 2.5f;
            vel = Vector3.Lerp(vel, target, 1 - Mathf.Exp(-accel * dt));

            if (cc.isGrounded)
            {
                if (vy < 0) vy = -2f;
                if (canControl && Input.GetKeyDown(KeyCode.Space)) { vy = JumpSpeed; Sfx.Play("step", 0.6f, 0.8f); }
            }
            vy -= MarsGravity * dt;
            var flags = cc.Move((vel + Vector3.up * vy) * dt);
            if ((flags & CollisionFlags.Above) != 0 && vy > 0) vy = 0;

            // head bob + footsteps
            float hspeed = new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude;
            Speed01 = Mathf.Clamp01(hspeed / SprintSpeed);
            if (cc.isGrounded && hspeed > 0.5f)
            {
                bob += dt * hspeed * 1.55f;
                stepT += dt * hspeed;
                if (stepT > 1.9f) { stepT = 0; Sfx.Play("step", 0.35f, Random.Range(0.85f, 1.1f)); }
            }
            else bob = Mathf.Lerp(bob, Mathf.Round(bob / Mathf.PI) * Mathf.PI, dt * 4);
            float bobY = Mathf.Sin(bob * 2) * 0.035f * Mathf.Clamp01(hspeed / 3);
            float bobX = Mathf.Cos(bob) * 0.025f * Mathf.Clamp01(hspeed / 3);

            // sickness sway
            float t = Time.time;
            float swayP = Sway * (Mathf.Sin(t * 0.9f) * 3f + Mathf.Sin(t * 2.3f) * 1.2f);
            float swayR = Sway * Mathf.Sin(t * 0.7f) * 4f;

            // shake
            Vector3 sh = Vector3.zero;
            if (shakeT > 0) { shakeT -= dt; sh = Random.insideUnitSphere * ShakeAmt; if (shakeT <= 0) ShakeAmt = 0; }

            Head.localPosition = new Vector3(bobX, HeadHeight + bobY, 0) + sh * 0.1f;
            Head.localRotation = Quaternion.Euler(pitch + ExtraPitch + swayP + sh.x * 4, sh.y * 4, swayR + ExtraRoll + sh.z * 3);
        }
    }
}
