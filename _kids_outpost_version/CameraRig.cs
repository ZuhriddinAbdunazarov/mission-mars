using UnityEngine;

namespace MissionMars
{
    /// <summary>Friendly orbit camera: drag to spin, scroll to zoom, click a building to fly to it.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public Vector3 Focus, FocusTarget;
        public float Yaw = 200, Pitch = 34, Dist = 62;
        public float YawT = 200, PitchT = 34, DistT = 62;
        public bool AutoSpin = true, UserControl;
        Vector3 lastMouse; bool dragging; float dragMoved;
        float shake;

        public static CameraRig Create()
        {
            var g = new GameObject("CameraRig"); var r = g.AddComponent<CameraRig>();
            var cg = new GameObject("Main Camera"); cg.tag = "MainCamera"; cg.transform.SetParent(g.transform, false);
            r.Cam = cg.AddComponent<Camera>(); r.Cam.fieldOfView = 42; r.Cam.nearClipPlane = 0.3f; r.Cam.farClipPlane = 3000;
            cg.AddComponent<AudioListener>();
            return r;
        }

        public void Shake(float amt) { shake = Mathf.Max(shake, amt); }

        public void FlyTo(Vector3 focus, float dist, float pitch = -1, float yaw = float.NaN)
        {
            FocusTarget = focus; DistT = dist; if (pitch > 0) PitchT = pitch; if (!float.IsNaN(yaw)) YawT = yaw;
        }

        public bool ClickedThisFrame(out Vector3 screenPos)
        {
            screenPos = Input.mousePosition;
            return Input.GetMouseButtonUp(0) && dragMoved < 6;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (UserControl && !UIState.Modal)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) { dragging = !Game.PointerOverUI; lastMouse = Input.mousePosition; dragMoved = 0; }
                if (dragging && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
                {
                    var d = Input.mousePosition - lastMouse; lastMouse = Input.mousePosition; dragMoved += d.magnitude;
                    YawT += d.x * 0.25f; PitchT = Mathf.Clamp(PitchT - d.y * 0.15f, 15, 75);
                    AutoSpin = false;
                }
                if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) dragging = false;
                if (!Game.PointerOverUI) DistT = Mathf.Clamp(DistT - Input.mouseScrollDelta.y * 5, 18, 110);
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) { YawT += 60 * dt; AutoSpin = false; }
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) { YawT -= 60 * dt; AutoSpin = false; }
            }
            if (AutoSpin) YawT += 3f * dt;
            float k = 1 - Mathf.Exp(-4f * dt);
            Yaw = Mathf.Lerp(Yaw, YawT, k); Pitch = Mathf.Lerp(Pitch, PitchT, k); Dist = Mathf.Lerp(Dist, DistT, k);
            Focus = Vector3.Lerp(Focus, FocusTarget, k);
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            var pos = Focus - rot * Vector3.forward * Dist;
            float gy = MarsTerrain.H(pos.x, pos.z) + 2f;
            if (pos.y < gy) pos.y = gy;
            if (shake > 0) { pos += Random.insideUnitSphere * shake; shake = Mathf.MoveTowards(shake, 0, dt * 2); }
            Cam.transform.position = pos;
            // look slightly below the focus so the base sits in the visible area above the bottom dock
            Cam.transform.rotation = Quaternion.LookRotation(Focus - pos) * (Game.InMenu ? Quaternion.Euler(-9, -16, 0) : Quaternion.Euler(5, 0, 0));
        }
    }
}
