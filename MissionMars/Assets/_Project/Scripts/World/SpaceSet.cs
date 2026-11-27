using System.Collections;
using UnityEngine;

namespace MissionMars
{
    /// <summary>Space environment for the title screen, the launch/crash intro and the flight home.
    /// Real NASA textures (Mars, Earth, Phobos) and NASA 3D models.</summary>
    public class SpaceSet : MonoBehaviour
    {
        public static SpaceSet I;
        public Transform Mars, MarsHalo, Earth, EarthHalo, Ship, Astronaut, Phobos, Orbiter;
        public Camera Cam;
        public const float MarsR = 1200f;
        public static readonly Vector3 Origin = new Vector3(0, 12000, 0);
        ParticleSystem plasma, exhaust; Light engLight; const bool NoseFlip = false;
        Vector3 sunDir = new Vector3(1, 0.25f, 0.35f).normalized;
        Vector3 earthHome = new Vector3(-9000, 1500, 14000); float earthHomeScale = 160;

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

        Transform Halo(Transform parent, Color c, float scale)
        {
            var halo = new Material(R.Template("Halo")); halo.SetColor("_Color", c); halo.SetVector("_SunDirW", sunDir); halo.SetFloat("_Power", 5); halo.SetFloat("_Intensity", 1.3f);
            var h = ProcMesh.Prim(PrimitiveType.Sphere, "Halo", parent, Vector3.zero, Vector3.one * scale, halo, null, false).transform;
            h.GetComponent<MeshFilter>().sharedMesh = ProcMesh.HiSphere; return h;
        }

        void Build()
        {
            Mars = ProcMesh.Prim(PrimitiveType.Sphere, "Mars", transform, Vector3.zero, Vector3.one * MarsR * 2, PlanetMat("Mars", new Color(1f, 0.55f, 0.35f), 0.9f), null, false).transform;
            MarsHalo = Halo(transform, new Color(1f, 0.5f, 0.3f), MarsR * 2.12f);
            Phobos = ProcMesh.Prim(PrimitiveType.Sphere, "Phobos", transform, new Vector3(-2600, 600, 1400), new Vector3(46, 36, 40), PlanetMat("Mars___Phobos", Color.black, 0), null, false).transform;
            Earth = ProcMesh.Prim(PrimitiveType.Sphere, "Earth", transform, earthHome, Vector3.one * earthHomeScale, PlanetMat(R.Tex("Earth_HD") ? "Earth_HD" : "Earth__A_", new Color(0.4f, 0.65f, 1f), 0.9f), null, false).transform;
            EarthHalo = Halo(Earth, new Color(0.35f, 0.6f, 1f), 1.06f);
            foreach (var tr in new[] { Mars, Earth }) tr.GetComponent<MeshFilter>().sharedMesh = ProcMesh.HiSphere;

            bool sfAstro = R.Model("SF_Astronaut");
            Astronaut = Util.PlaceModel(sfAstro ? "SF_Astronaut" : "Astronaut", transform.position, 1.9f, 0, transform, true).transform;
            Orbiter = Util.PlaceModel("Mars_Reconnaissance_Orbiter__MRO___A_", transform.position, 14, 0, transform).transform;
            Ship = BuildShip(); Ship.SetParent(transform, true);
            BuildLander(); Lander.SetParent(transform, true); Lander.gameObject.SetActive(false);
            Ship.gameObject.SetActive(false); Astronaut.gameObject.SetActive(false); Orbiter.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        public Transform Lander; ParticleSystem landerFlame, landerPlasma; Light landerLight;
        /// <summary>Our own spaceship (the same rocket we later rebuild on Mars), nose pointing +Z.</summary>
        void BuildLander()
        {
            Lander = new GameObject("Lander").transform;
            var body = Util.PlaceUpright(MarsWorld.RocketModel, Vector3.zero, 34, Lander);
            Object.Destroy(body.GetComponent<BoxCollider>());
            var b = Util.Bounds(body); body.transform.position -= b.center;
            body.transform.SetParent(Lander, true);
            Lander.rotation = Quaternion.Euler(90, 0, 0); // +Y (nose) -> +Z
            var holder = new GameObject("LanderRoot").transform; holder.position = Vector3.zero;
            Lander.SetParent(holder, true); Lander = holder;
            var eng = new GameObject("Engine").transform; eng.SetParent(Lander, false); eng.localPosition = new Vector3(0, 0, -17.5f);
            landerFlame = FX.Flame(eng, 1.2f); landerFlame.transform.localRotation = Quaternion.Euler(180, 0, 0); landerFlame.transform.localPosition = Vector3.zero;
            landerLight = new GameObject("EngLight").AddComponent<Light>(); landerLight.transform.SetParent(eng, false); landerLight.transform.localPosition = new Vector3(0, 0, -3);
            landerLight.type = LightType.Point; landerLight.range = 50; landerLight.intensity = 9; landerLight.color = new Color(1f, 0.6f, 0.25f);
            landerPlasma = FX.Plasma(Lander, 3.5f); landerPlasma.Stop();
        }

        Transform BuildShip()
        {
            var root = new GameObject("Ship").transform;
            var body = Util.PlaceModel("Saturn_V", Vector3.zero, 34, 0, root);
            var bb = Util.Bounds(body);
            if (bb.size.y >= bb.size.x && bb.size.y >= bb.size.z) body.transform.localRotation = Quaternion.Euler(90, 0, 0);
            else if (bb.size.x >= bb.size.z) body.transform.localRotation = Quaternion.Euler(0, NoseFlip ? 90 : -90, 0);
            var b = Util.Bounds(body); body.transform.position -= b.center;
            var eng = new GameObject("Engine").transform; eng.SetParent(root, false); eng.localPosition = new Vector3(0, 0, -17.5f);
            exhaust = FX.Flame(eng, 1f); exhaust.transform.localRotation = Quaternion.Euler(180, 0, 0); exhaust.transform.localPosition = Vector3.zero;
            var main = exhaust.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            engLight = new GameObject("EngLight").AddComponent<Light>(); engLight.transform.SetParent(eng, false); engLight.transform.localPosition = new Vector3(0, 0, -3);
            engLight.type = LightType.Point; engLight.range = 50; engLight.intensity = 9; engLight.color = new Color(1f, 0.6f, 0.25f);
            plasma = FX.Plasma(root, 3.5f); plasma.Stop();
            return root;
        }

        public void Enter(Camera cam)
        {
            Cam = cam; gameObject.SetActive(true);
            MarsWorld.SetSurface(false);
            if (MarsWorld.Interior) MarsWorld.Interior.SetActive(false);
            Game.Sky.Mode = SkyMode.Space; Game.Sky.SpaceSunDir = sunDir; Game.Sky.Apply();
            cam.farClipPlane = 60000; cam.nearClipPlane = 0.3f;
        }

        public void Exit(Camera cam)
        {
            gameObject.SetActive(false);
            MarsWorld.SetSurface(true);
            Game.Sky.Mode = SkyMode.Surface; Game.Sky.Apply();
            cam.farClipPlane = 6000; cam.nearClipPlane = 0.05f;
        }

        void ResetEarth() { Earth.localPosition = earthHome; Earth.localScale = Vector3.one * earthHomeScale; Earth.localRotation = Quaternion.identity; }

        void Update()
        {
            Mars.Rotate(0, 0.6f * Time.deltaTime, 0, Space.Self);
            Earth.Rotate(0, 2f * Time.deltaTime, 0, Space.Self);
        }

        // ------------------------------------------------------------------ title backdrop
        public void MenuPose()
        {
            ResetEarth();
            Ship.gameObject.SetActive(false);
            Orbiter.gameObject.SetActive(true); Astronaut.gameObject.SetActive(true);
            Vector3 c = transform.position;
            Cam.transform.position = c + new Vector3(-600, 380, -2950);
            Cam.transform.rotation = Quaternion.LookRotation((c + new Vector3(-1500, 250, 0)) - Cam.transform.position);
            Cam.fieldOfView = 50;
            // the astronaut floats on the right and FACES the player (model front = +Z), slightly turned toward the menu
            astroHome = Cam.transform.position + Cam.transform.forward * 8.5f + Cam.transform.right * 2.6f + Cam.transform.up * -0.2f;
            // this model's head is its local -X and its visor its local -Y (seen in test screenshots), so:
            // head -> screen up, visor -> toward the player
            astroRot = Quaternion.LookRotation(-Cam.transform.right, Cam.transform.forward);
            Astronaut.position = astroHome; Astronaut.rotation = astroRot; Astronaut.localScale = Vector3.one;
            Orbiter.position = Cam.transform.position + Cam.transform.forward * 140 + Cam.transform.right * 55 + Cam.transform.up * 22;
        }
        Vector3 astroHome; Quaternion astroRot;

        public void MenuTick()
        {
            float t = Time.time;
            // gentle friendly sway and bob (never drifts away or rolls over)
            Astronaut.rotation = astroRot * Quaternion.Euler(Mathf.Sin(t * 0.6f) * 4f, Mathf.Sin(t * 0.45f) * 10f, Mathf.Sin(t * 1.1f) * 6f);
            Astronaut.position = astroHome + Cam.transform.up * Mathf.Sin(t * 0.8f) * 0.18f;
            Orbiter.Rotate(0, 2f * Time.deltaTime, 0.6f * Time.deltaTime, Space.Self);
            Cam.transform.position += Cam.transform.right * Mathf.Sin(t * 0.07f) * 0.15f * Time.deltaTime;
        }

        /// <summary>The title astronaut turns and flies off into space (when the player starts or quits).</summary>
        public IEnumerator FlyAway(float dur = 1.6f)
        {
            if (!Astronaut || !Astronaut.gameObject.activeSelf) yield break;
            Vector3 from = Astronaut.position; Quaternion r0 = Astronaut.rotation;
            Vector3 to = from + Cam.transform.forward * 90f + Cam.transform.up * 28f + Cam.transform.right * 30f;
            Quaternion r1 = Quaternion.FromToRotation(r0 * Vector3.left, (to - from).normalized) * r0; // superhero pose: head (local -X) first
            for (float k = 0; k < 1; k += Time.deltaTime / dur)
            {
                float e = k * k * (3 - 2 * k);
                Astronaut.position = Vector3.LerpUnclamped(from, to, e * e);
                Astronaut.rotation = Quaternion.Slerp(r0, r1, Mathf.Min(1, k * 2.5f));
                Astronaut.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, e);
                yield return null;
            }
            Astronaut.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ intro
        static bool Skip => Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Escape);

        Coroutine siren;
        /// <summary>The worried part: a two-tone warning siren (like a "trevoga" alarm) until the crash.</summary>
        IEnumerator Siren()
        {
            for (int i = 0; i < 12; i++) { Sfx.Play("alarm", 0.65f, i % 2 == 0 ? 1.0f : 0.78f); yield return new WaitForSeconds(0.45f); }
        }

        public IEnumerator Intro()
        {
            Orbiter.gameObject.SetActive(false); Astronaut.gameObject.SetActive(false);
            Ship.gameObject.SetActive(true); exhaust.Play(); engLight.enabled = true;
            var cam = Cam.transform;
            AudioManager.I.StopMusic(1f); // no music while the rocket flies: only the engines
            // no music and no engine drone: just the AIR RUSHING past (the Mars wind recording, played faster = a whoosh)
            var roar = AudioManager.I.Loop("wind", 0f); roar.pitch = 1.55f; AudioManager.I.FadeSource(roar, 0.5f, 1.2f);
            Sfx.Play("whoosh", 0.8f, 0.7f);

            // SHOT 1 — leaving Earth: huge blue planet below, rocket climbs past the camera
            Vector3 c = transform.position;
            float ER = 2600;
            Vector3 earthC = c + new Vector3(0, -ER - 40, 9000);
            Earth.position = earthC; Earth.localScale = Vector3.one * ER * 2; Earth.localRotation = Quaternion.Euler(-75, 20, 0);
            Vector3 camP = earthC + new Vector3(-30, ER + 70, -ER * 0.55f);
            Cam.fieldOfView = 55;
            Cinema.I.Caption("EARTH  ·  LAUNCH DAY  ·  DESTINATION: MARS", 5);
            yield return Cinema.I.FromBlack(1.2f);
            float t = 0, dur = 6.5f;
            Vector3 up = (camP - earthC).normalized;
            Vector3 fwd = Vector3.Cross(Vector3.right, up).normalized;
            while (t < dur && !Skip)
            {
                t += Time.deltaTime; float k = t / dur;
                Vector3 sp = camP + fwd * (25 + k * k * 650) + up * (-10 + k * 70) + Vector3.right * 9;
                Ship.position = sp;
                Ship.rotation = Quaternion.LookRotation(Vector3.Lerp(up, fwd, 0.3f + k * 0.6f));
                cam.position = camP + Random.insideUnitSphere * (1 - k) * 0.6f;
                cam.rotation = Quaternion.Slerp(Quaternion.LookRotation(fwd - up * 0.32f, up), Quaternion.LookRotation(Ship.position - cam.position, up), 0.25f);
                yield return null;
            }
            // SHOT 2 — the long trip
            yield return Cinema.I.ToBlack(0.6f);
            Cinema.I.Caption("7 MONTHS LATER  ·  225 MILLION KILOMETRES FROM HOME", 4.5f);
            ResetEarth();
            AudioManager.I.FadeSource(roar, 0.12f, 1f); roar.pitch = 1.2f; // space is quiet: only a soft hiss
            Vector3 p0 = c + new Vector3(4000, 2600, -16000), p1 = c + new Vector3(500, 500, -3800);
            Ship.position = p0; Ship.rotation = Quaternion.LookRotation(c - p0);
            cam.position = p0 - Ship.forward * 70 + Ship.right * 32 + Ship.up * 12; cam.rotation = Quaternion.LookRotation(Vector3.Lerp(p0, c, 0.08f) - cam.position);
            yield return Cinema.I.FromBlack(0.8f);
            t = 0; dur = 6f;
            while (t < dur && !Skip)
            {
                t += Time.deltaTime; float k = Tween.EaseInOut(t / dur);
                Vector3 shipP = Vector3.Lerp(p0, p1, k);
                Ship.position = shipP; Ship.rotation = Quaternion.LookRotation(c - shipP);
                cam.position = shipP - Ship.forward * 70 + Ship.right * 32 + Ship.up * 12;
                cam.rotation = Quaternion.LookRotation(Vector3.Lerp(shipP, c, 0.08f) - cam.position);
                yield return null;
            }
            // SHOT 3 — entry, failure
            Cinema.I.Caption("ENTERING MARS ATMOSPHERE", 2.5f);
            Vector3 entryA = c + new Vector3(300, 300, -(MarsR + 260)), entryB = c + new Vector3(80, 60, -(MarsR + 30));
            plasma.Play(); Sfx.Play("whoosh", 0.9f, 0.6f);
            roar.pitch = 1.8f; AudioManager.I.FadeSource(roar, 0.8f, 1.5f); // the air screams past the hot heat shield
            t = 0; dur = 6f;
            bool a1 = false, a2 = false;
            while (t < dur && !Skip)
            {
                t += Time.deltaTime; float k = t / dur;
                Ship.position = Vector3.Lerp(entryA, entryB, k);
                Ship.rotation = Quaternion.LookRotation(entryB - entryA) * Quaternion.Euler(0, 0, Mathf.Sin(t * 2) * 6 + k * 50);
                cam.position = Ship.position - Ship.forward * 70 + Vector3.up * 14 + Random.insideUnitSphere * (0.5f + k * 3f);
                cam.rotation = Quaternion.LookRotation(Ship.position - cam.position);
                if (t > 2.2f && !a1) { a1 = true; Cinema.I.Caption("<color=#FF6A4A>HEAT SHIELD DAMAGED</color>", 2); siren = StartCoroutine(Siren()); Voice.Say("Warning! Heat shield damaged!"); }
                if (t > 4f && !a2) { a2 = true; Cinema.I.Caption("<color=#FF4A3A>MAIN ENGINE FAILURE — BRACE FOR IMPACT</color>", 2.5f); Voice.Say("Main engine failure! Brace for impact!"); Sfx.Play("thud", 0.8f); Sfx.Play("heartbeat", 1f); }
                yield return null;
            }
            // CRASH: the ship breaks apart in a fireball
            if (siren != null) { StopCoroutine(siren); siren = null; }
            Vector3 boom = Ship.position;
            FX.Explosion(boom, 4f, transform);
            plasma.Stop(); exhaust.Stop(); Ship.gameObject.SetActive(false);
            AudioManager.I.FadeSource(roar, 0, 0.6f); Destroy(roar.gameObject, 1f);
            Sfx.Play("explosion", 1f, 0.9f); Sfx.Play("thud", 1f, 0.6f); // the crash: a big BOOM (real CC0 explosion recording)
            Cinema.I.ClearCaption();
            { float tt = 0; var cp = cam.position; var away = (cp - boom).normalized;
              while (tt < 2.0f) { tt += Time.deltaTime; cam.position = cp + away * tt * 10 + Random.insideUnitSphere * Mathf.Max(0, 1.8f - tt) * 2.5f; cam.rotation = Quaternion.LookRotation(boom - cam.position); yield return null; } }
            yield return Cinema.I.WhiteOut(0.25f);
            Cinema.I.ClearCaption();
            Cinema.I.SetBlack(1);
            yield return Cinema.I.WhiteIn(0.1f);
            AudioManager.I.StopMusic(1f);
            yield return new WaitForSeconds(1.6f);
        }

        /// <summary>The ship splits into its 3 sections, which tumble away trailing smoke.</summary>
        IEnumerator Pieces(Vector3 at, Quaternion rot, Vector3 fwd)
        {
            var secs = new Transform[3]; var vel = new Vector3[3]; var spin = new Vector3[3];
            Vector3 side = Vector3.Cross(fwd, Vector3.up).normalized;
            for (int i = 0; i < 3; i++)
            {
                var s = MarsWorld.SectionCopy(i, 34, transform).transform;
                s.position = at + fwd * (i - 1) * 10; s.rotation = rot * Quaternion.Euler(90, 0, 0);
                FX.Smoke(s.position, 2.5f, s, 0.15f); FX.Fire(s.position, 2f, s);
                vel[i] = fwd * 30 + side * (i - 1) * 45 + Vector3.up * (i == 1 ? 25 : -5); spin[i] = Random.onUnitSphere * 90;
                secs[i] = s;
            }
            float t = 0;
            while (t < 6) { t += Time.deltaTime; for (int i = 0; i < 3; i++) { if (!secs[i]) continue; secs[i].position += vel[i] * Time.deltaTime; secs[i].Rotate(spin[i] * Time.deltaTime, Space.World); } yield return null; }
            foreach (var s in secs) if (s) Destroy(s.gameObject);
        }

        // ------------------------------------------------------------------ flight home
        public IEnumerator Ending()
        {
            Sfx.Play("whoosh", 0.7f, 0.8f); // flying home: a soft whoosh (no music, no engine drone)
            Ship.gameObject.SetActive(false); Ship = Lander; exhaust = landerFlame; plasma = landerPlasma; // fly home in the rocket we built
            Orbiter.gameObject.SetActive(false); Astronaut.gameObject.SetActive(false);
            Ship.gameObject.SetActive(true); exhaust.Play(); plasma.Stop();
            ResetEarth();
            Vector3 c = transform.position; var cam = Cam.transform;
            Cam.fieldOfView = 50;
            Vector3 start = c + new Vector3(-200, MarsR + 80, -300);
            Vector3 toEarth = (Earth.position - start).normalized;
            float t = 0, dur = 8f;
            Cinema.I.Caption("LEAVING MARS  ·  THE LAUNCH WINDOW IS OPEN", 4);
            yield return Cinema.I.FromBlack(1.2f);
            while (t < dur)
            {
                t += Time.deltaTime; float k = Tween.EaseInOut(t / dur);
                Ship.position = start + toEarth * (t * 60 + t * t * 14);
                Ship.rotation = Quaternion.LookRotation(toEarth);
                cam.position = Ship.position - toEarth * Mathf.Lerp(60, 160, k) + Vector3.Cross(toEarth, Vector3.up) * 30 + Vector3.up * Mathf.Lerp(-10, 40, k);
                cam.rotation = Quaternion.Slerp(Quaternion.LookRotation(Ship.position - cam.position), Quaternion.LookRotation(Earth.position - cam.position), k * 0.85f);
                yield return null;
            }
            // arrive: big Earth, ship heading in
            yield return Cinema.I.ToBlack(0.8f);
            float ER = 2600;
            Vector3 earthC = c + new Vector3(0, -ER * 0.4f, 7000);
            Earth.position = earthC; Earth.localScale = Vector3.one * ER * 2; Earth.localRotation = Quaternion.Euler(-20, 160, 0);
            Vector3 camP = earthC + new Vector3(-ER * 0.9f, ER * 0.7f, -ER * 1.6f);
            Cinema.I.Caption("7 MONTHS LATER  ·  HOME", 4);
            yield return Cinema.I.FromBlack(1.0f);
            t = 0; dur = 7f;
            while (t < dur)
            {
                t += Time.deltaTime; float k = Tween.EaseInOut(t / dur);
                Vector3 shipP = Vector3.Lerp(camP + (earthC - camP).normalized * 60 + Vector3.right * 10, earthC + (camP - earthC).normalized * (ER + 200), k * 0.35f);
                Ship.position = shipP; Ship.rotation = Quaternion.LookRotation(earthC - shipP);
                cam.position = camP + (earthC - camP).normalized * k * 300;
                cam.rotation = Quaternion.LookRotation(Vector3.Lerp(Ship.position, earthC, 0.3f + k * 0.5f) - cam.position);
                yield return null;
            }
        }
    }
}
