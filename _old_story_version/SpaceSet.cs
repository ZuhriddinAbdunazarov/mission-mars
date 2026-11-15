using System.Collections;
using UnityEngine;

namespace MissionMars
{
    /// <summary>Space environment used by the main menu, the intro crash and the ending.
    /// Real NASA textures (Mars, Earth) and NASA 3D models (MRO, astronaut).</summary>
    public class SpaceSet : MonoBehaviour
    {
        public static SpaceSet I;
        public Transform Mars, MarsHalo, Earth, Ship, Orbiter, Astronaut, Phobos;
        public Camera Cam;
        public const float MarsR = 1200f;
        public static readonly Vector3 Origin = new Vector3(0, 12000, 0);
        ParticleSystem plasma; Material marsMat;
        Vector3 sunDir = new Vector3(1, 0.25f, 0.35f).normalized;

        public static SpaceSet Create()
        {
            var g = new GameObject("SpaceSet"); g.transform.position = Origin;
            var s = g.AddComponent<SpaceSet>(); I = s; s.Build(); return s;
        }

        Material PlanetMat(string tex, Color atmo, float strength)
        {
            var m = new Material(R.Template("Planet")); m.SetTexture("_MainTex", R.Tex(tex));
            m.SetColor("_AtmoColor", atmo); m.SetFloat("_AtmoStrength", strength); m.SetVector("_SunDirW", sunDir); return m;
        }

        void Build()
        {
            marsMat = PlanetMat("Mars", new Color(1f, 0.55f, 0.35f), 0.9f);
            Mars = ProcMesh.Prim(PrimitiveType.Sphere, "Mars", transform, Vector3.zero, Vector3.one * MarsR * 2, marsMat, null, false).transform;
            var halo = new Material(R.Template("Halo")); halo.SetColor("_Color", new Color(1f, 0.5f, 0.3f)); halo.SetVector("_SunDirW", sunDir); halo.SetFloat("_Power", 5); halo.SetFloat("_Intensity", 1.3f);
            MarsHalo = ProcMesh.Prim(PrimitiveType.Sphere, "MarsHalo", transform, Vector3.zero, Vector3.one * MarsR * 2.12f, halo, null, false).transform;
            Phobos = ProcMesh.Prim(PrimitiveType.Sphere, "Phobos", transform, new Vector3(-2600, 600, 1400), new Vector3(46, 36, 40), PlanetMat("Mars___Phobos", Color.black, 0), null, false).transform;
            Earth = ProcMesh.Prim(PrimitiveType.Sphere, "Earth", transform, new Vector3(-9000, 1500, 14000), Vector3.one * 160, PlanetMat("Earth__A_", new Color(0.4f, 0.65f, 1f), 1.4f), null, false).transform;

            foreach (var tr in new[] { Mars, MarsHalo, Earth }) tr.GetComponent<MeshFilter>().sharedMesh = ProcMesh.HiSphere;
            Orbiter = Util.PlaceModel("Mars_Reconnaissance_Orbiter__MRO___A_", transform.position + new Vector3(0, 0, 0), 14, 0, transform, false, false).transform;
            Astronaut = Util.PlaceModel("Astronaut", transform.position, 1.9f, 0, transform, true, false).transform;
            Ship = BuildShip();
            Ship.SetParent(transform, true);
            Ship.gameObject.SetActive(false);
            Astronaut.gameObject.SetActive(false);
            Orbiter.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        Transform BuildShip()
        {
            var root = new GameObject("Ship").transform;
            var body = Util.PlaceModel("Ares_1__A_", Vector3.zero, 30, 0, root, true, false);
            body.transform.localRotation = Quaternion.Euler(90, 0, 0); // nose forward (+Z)
            var b = Util.Bounds(body);
            body.transform.position -= b.center;
            var eng = new GameObject("Engine").transform; eng.SetParent(root, false); eng.localPosition = new Vector3(0, 0, -15);
            var exh = FX.Exhaust(eng, Vector3.zero, 1.2f);
            exh.transform.localRotation = Quaternion.Euler(180, 0, 0);
            var main = exh.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var el = new GameObject("EngLight").AddComponent<Light>(); el.transform.SetParent(eng, false); el.transform.localPosition = new Vector3(0, 0, -3);
            el.type = LightType.Point; el.range = 40; el.intensity = 8; el.color = new Color(1f, 0.6f, 0.25f);
            plasma = FX.Plasma(root, 6f);
            plasma.Stop();
            return root;
        }

        public void Enter(Camera cam)
        {
            Cam = cam; gameObject.SetActive(true);
            World.SetSurfaceActive(false);
            if (World.Interior) World.Interior.SetActive(false);
            Game.Sky.Mode = SkyMode.Space; Game.Sky.SpaceSunDir = sunDir; Game.Sky.Apply();
            cam.farClipPlane = 40000; cam.nearClipPlane = 0.3f;
        }

        public void Exit(Camera cam)
        {
            gameObject.SetActive(false);
            World.SetSurfaceActive(true);
            Game.Sky.Mode = SkyMode.Surface; Game.Sky.Apply();
            cam.farClipPlane = 6000; cam.nearClipPlane = 0.05f;
        }

        void Update()
        {
            Mars.Rotate(0, 0.6f * Time.deltaTime, 0, Space.Self);
            Earth.Rotate(0, 3f * Time.deltaTime, 0, Space.Self);
        }

        // ------------------------------------------------------------------
        /// <summary>Main menu backdrop: Mars filling the right side, MRO orbiting, astronaut drifting.</summary>
        public void MenuPose()
        {
            Ship.gameObject.SetActive(false);
            Orbiter.gameObject.SetActive(true); Astronaut.gameObject.SetActive(true);
            Vector3 c = transform.position;
            Cam.transform.position = c + new Vector3(-600, 380, -2950);
            Cam.transform.rotation = Quaternion.LookRotation((c + new Vector3(-1100, 250, 0)) - Cam.transform.position);
            Cam.fieldOfView = 50;
            Astronaut.position = Cam.transform.position + Cam.transform.forward * 9 + Cam.transform.right * -2.6f + Cam.transform.up * -0.8f;
            Orbiter.position = Cam.transform.position + Cam.transform.forward * 120 + Cam.transform.right * 40 + Cam.transform.up * 18;
        }

        public void MenuTick()
        {
            float t = Time.time;
            Astronaut.Rotate(new Vector3(0.6f, 3f, 1.2f) * Time.deltaTime, Space.Self);
            Astronaut.position += Cam.transform.up * Mathf.Sin(t * 0.5f) * 0.002f;
            Orbiter.Rotate(0, 2f * Time.deltaTime, 0.6f * Time.deltaTime, Space.Self);
            Cam.transform.position += Cam.transform.right * Mathf.Sin(t * 0.07f) * 0.15f * Time.deltaTime;
        }

        // ------------------------------------------------------------------
        /// <summary>Scene 1: zoom through space to Mars, ship enters atmosphere, malfunction, crash.</summary>
        public IEnumerator Intro()
        {
            Orbiter.gameObject.SetActive(false); Astronaut.gameObject.SetActive(false);
            Ship.gameObject.SetActive(true);
            Vector3 c = transform.position;
            var cam = Cam.transform;
            AudioManager.I.Music("music_tension", 0.5f, 2f);

            // shot 1: deep space -> Mars approaches
            Cam.fieldOfView = 55;
            Vector3 p0 = c + new Vector3(4000, 2600, -16000), p1 = c + new Vector3(500, 500, -3800);
            float t = 0, dur = 7f;
            Cinema.I.Caption("2026 · INTERPLANETARY TRANSIT · DAY 214 OF 214", 6);
            yield return Cinema.I.FromBlack(1.5f);
            while (t < dur)
            {
                t += Time.deltaTime; float k = Tween.EaseInOut(t / dur);
                cam.position = Vector3.Lerp(p0, p1, k);
                cam.rotation = Quaternion.LookRotation(c - cam.position + new Vector3(0, -200, 0));
                Ship.position = cam.position + cam.forward * 60 + cam.up * -8 + cam.right * 6;
                Ship.rotation = Quaternion.LookRotation(c - Ship.position);
                yield return null;
            }

            // shot 2: chase cam, atmospheric entry, plasma
            Cinema.I.Caption("ENTERING MARTIAN ATMOSPHERE", 3);
            Vector3 entryA = c + new Vector3(300, 300, -(MarsR + 260)), entryB = c + new Vector3(80, 60, -(MarsR + 30));
            plasma.Play(); Sfx.Play("whoosh", 0.7f, 0.6f);
            var roar = AudioManager.I.Loop("rocket", 0f); AudioManager.I.FadeSource(roar, 0.8f, 2f);
            t = 0; dur = 6f;
            while (t < dur)
            {
                t += Time.deltaTime; float k = t / dur;
                Ship.position = Vector3.Lerp(entryA, entryB, k);
                Ship.rotation = Quaternion.LookRotation(entryB - entryA) * Quaternion.Euler(0, 0, Mathf.Sin(t * 2) * 6 + k * 40);
                cam.position = Ship.position - Ship.forward * 70 + Vector3.up * 14 + Random.insideUnitSphere * (0.5f + k * 2.5f);
                cam.rotation = Quaternion.LookRotation(Ship.position - cam.position);
                if (t > 2.2f && t - Time.deltaTime <= 2.2f) { Game.Hud.Warn("HULL TEMPERATURE CRITICAL", 3.5f); Sfx.Play("alarm", 0.6f); }
                if (t > 4f && t - Time.deltaTime <= 4f) { Game.Hud.Warn("MAIN ENGINE FAILURE", 2.5f); Sfx.Play("alarm", 0.7f); Sfx.Play("thud", 0.8f); }
                yield return null;
            }
            yield return Cinema.I.WhiteOut(0.35f);
            plasma.Stop(); Ship.gameObject.SetActive(false);
            AudioManager.I.FadeSource(roar, 0, 0.5f); Destroy(roar.gameObject, 1f);
            Sfx.Play("explosion", 1f);
            Cinema.I.SetBlack(1);
            yield return Cinema.I.WhiteIn(0.1f);
            AudioManager.I.StopMusic(1f);
            yield return new WaitForSeconds(2.2f);
        }

        /// <summary>Finale: rocket climbs away from Mars toward the blue dot of Earth.</summary>
        public IEnumerator Ending()
        {
            Orbiter.gameObject.SetActive(false); Astronaut.gameObject.SetActive(false);
            Ship.gameObject.SetActive(true);
            Vector3 c = transform.position; var cam = Cam.transform;
            Cam.fieldOfView = 50;
            Vector3 start = c + new Vector3(-200, MarsR + 80, -300);
            Vector3 toEarth = (Earth.position - start).normalized;
            float t = 0, dur = 10f;
            yield return Cinema.I.FromBlack(1.2f);
            while (t < dur)
            {
                t += Time.deltaTime; float k = Tween.EaseInOut(t / dur);
                Ship.position = start + toEarth * (t * 60 + t * t * 12);
                Ship.rotation = Quaternion.LookRotation(toEarth);
                cam.position = Ship.position - toEarth * Mathf.Lerp(60, 160, k) + Vector3.Cross(toEarth, Vector3.up) * 30 + Vector3.up * Mathf.Lerp(-10, 40, k);
                cam.rotation = Quaternion.Slerp(Quaternion.LookRotation(Ship.position - cam.position), Quaternion.LookRotation(Earth.position - cam.position), k * 0.85f);
                yield return null;
            }
        }
    }
}
