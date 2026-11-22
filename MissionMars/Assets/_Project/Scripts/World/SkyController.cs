using System.Collections;
using UnityEngine;

namespace MissionMars
{
    public enum SkyMode { Surface, Interior, Space }

    /// <summary>Drives sun, skybox, ambient light and fog. TimeOfDay: 0.25 sunrise, 0.5 noon, 0.75 sunset.</summary>
    public class SkyController : MonoBehaviour
    {
        public Light Sun, NightFill;
        public Material SkyMat;
        public float TimeOfDay = 0.42f;
        public SkyMode Mode = SkyMode.Surface;
        public float Day { get; private set; }
        public Vector3 SunDir { get; private set; }
        public Vector3 EarthDir { get; private set; }
        public float OutsideTempC { get; private set; }
        public float SpaceSunIntensity = 2.2f;
        public float DustStorm; // 0..1 dust storm strength (thick brown haze, dim sun)
        public float InteriorBright = 1f; // bunker ambient light: low while the power is off
        public Vector3 SpaceSunDir = new Vector3(1, 0.25f, 0.35f);

        Texture pano, stars;
        static readonly Color FogDay = new Color(0.80f, 0.58f, 0.42f);
        static readonly Color FogNight = new Color(0.035f, 0.035f, 0.055f);

        public void Init()
        {
            var sg = new GameObject("Sun");
            sg.transform.SetParent(transform);
            Sun = sg.AddComponent<Light>();
            Sun.type = LightType.Directional; Sun.shadows = LightShadows.Soft; Sun.shadowStrength = 0.88f;
            Sun.shadowBias = 0.04f; Sun.shadowNormalBias = 0.5f;
            RenderSettings.sun = Sun;

            var ng = new GameObject("NightFill");
            ng.transform.SetParent(transform);
            NightFill = ng.AddComponent<Light>();
            NightFill.type = LightType.Directional; NightFill.shadows = LightShadows.None;
            NightFill.color = new Color(0.45f, 0.55f, 0.85f);
            ng.transform.rotation = Quaternion.Euler(70, 30, 0);

            SkyMat = new Material(R.Template("Sky"));
            RenderSettings.skybox = SkyMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            Apply();
        }

        public static Vector3 SunPath(float tod)
        {
            float th = (tod - 0.25f) * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(th), Mathf.Sin(th) * 0.9f, -Mathf.Sin(th) * 0.42f - 0.12f).normalized;
        }

        void Update() => Apply();

        public void Apply()
        {
            if (!Sun) return;
            if (Mode == SkyMode.Space) { ApplySpace(); return; }

            SunDir = SunPath(TimeOfDay);
            EarthDir = SunPath(TimeOfDay - 0.095f) ; // Earth trails the Sun: visible as an "evening star" after sunset
            EarthDir = (EarthDir + Vector3.up * 0.04f).normalized;
            float y = SunDir.y;
            Day = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.1f, 0.18f, y));

            Sun.transform.rotation = Quaternion.LookRotation(-SunDir);
            float low = Mathf.Clamp01(1 - y * 3f);
            Sun.color = Color.Lerp(new Color(1f, 0.94f, 0.84f), new Color(1f, 0.70f, 0.48f), low);
            Sun.intensity = 1.9f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.03f, 0.12f, y));
            Sun.enabled = Sun.intensity > 0.01f;
            NightFill.intensity = Mathf.Lerp(0.16f, 0f, Day);
            NightFill.enabled = NightFill.intensity > 0.005f;

            bool inside = Mode == SkyMode.Interior;
            Color skyA = Color.Lerp(new Color(0.06f, 0.07f, 0.12f), new Color(0.64f, 0.48f, 0.38f), Day);
            Color eqA = Color.Lerp(new Color(0.045f, 0.045f, 0.07f), new Color(0.58f, 0.40f, 0.30f), Day);
            Color gA = Color.Lerp(new Color(0.02f, 0.02f, 0.025f), new Color(0.30f, 0.19f, 0.13f), Day);
            if (inside) { skyA = new Color(0.2f, 0.2f, 0.22f) * InteriorBright; eqA = new Color(0.15f, 0.15f, 0.16f) * InteriorBright; gA = new Color(0.06f, 0.06f, 0.07f) * InteriorBright; }
            RenderSettings.ambientSkyColor = skyA; RenderSettings.ambientEquatorColor = eqA; RenderSettings.ambientGroundColor = gA;

            RenderSettings.fog = true;
            RenderSettings.fogColor = inside ? new Color(0.02f, 0.02f, 0.025f) : Color.Lerp(FogNight, FogDay, Day) * (1 - low * 0.25f);
            RenderSettings.fogDensity = inside ? 0.0005f : Mathf.Lerp(0.0055f, 0.0042f, Day);
            if (!inside && DustStorm > 0)
            {
                RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, 0.05f, DustStorm);
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, new Color(0.52f, 0.31f, 0.19f), DustStorm);
                Sun.intensity *= 1 - 0.7f * DustStorm;
                RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, new Color(0.55f, 0.33f, 0.2f), DustStorm);
            }

            if (stars && SkyMat.GetTexture("_StarTex") != stars) { SkyMat.SetTexture("_StarTex", stars); SkyMat.SetFloat("_StarIntensity", 1.6f); }
            SkyMat.SetVector("_SunDir", SunDir);
            SkyMat.SetVector("_EarthDir", EarthDir);
            SkyMat.SetFloat("_Day", Day);
            SkyMat.SetFloat("_Space", 0);
            SkyMat.SetFloat("_Exposure", 1);

            OutsideTempC = (NasaApi.I ? NasaApi.I.TempC(Day) : Mathf.Lerp(-81f, -12f, Day)) + Mathf.Sin(Time.time * 0.1f) * 1.5f; // real NASA InSight readings
        }

        void ApplySpace()
        {
            if (!pano) { pano = R.Tex("Space_Pano"); stars = SkyMat.GetTexture("_StarTex"); }
            if (pano) { SkyMat.SetTexture("_StarTex", pano); SkyMat.SetFloat("_StarIntensity", 2.4f); }
            SunDir = SpaceSunDir.normalized;
            Sun.transform.rotation = Quaternion.LookRotation(-SunDir);
            Sun.color = new Color(1f, 0.97f, 0.92f); Sun.intensity = SpaceSunIntensity; Sun.enabled = true;
            NightFill.enabled = false;
            RenderSettings.ambientSkyColor = new Color(0.03f, 0.035f, 0.05f);
            RenderSettings.ambientEquatorColor = new Color(0.02f, 0.02f, 0.03f);
            RenderSettings.ambientGroundColor = new Color(0.01f, 0.01f, 0.015f);
            RenderSettings.fog = false;
            SkyMat.SetVector("_SunDir", SunDir);
            SkyMat.SetVector("_EarthDir", new Vector3(-0.6f, 0.15f, 0.8f));
            SkyMat.SetFloat("_Day", 0);
            SkyMat.SetFloat("_Space", 1);
        }

        public IEnumerator Timelapse(float to, float dur)
        {
            float from = TimeOfDay, t = 0;
            while (t < dur)
            {
                t += Time.deltaTime; float k = Mathf.SmoothStep(0, 1, t / dur);
                TimeOfDay = Mathf.Lerp(from, to, k);
                yield return null;
            }
            TimeOfDay = to;
        }
    }
}
