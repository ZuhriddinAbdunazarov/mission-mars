using System;
using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace MissionMars
{
    /// <summary>Real NASA data shared with the MARS × MOON research site (github.com/ZuhriddinAbdunazarov/research):
    /// - Earth–Mars distance: LIVE from the site's REST API (/explorer/api/ephemeris → NASA/JPL Horizons),
    ///   offline from a daily JPL Horizons table for 2020–2035 (Resources/Data/earth_mars_distance.txt).
    /// - Mars clock and season: NASA GISS Mars24 algorithm (computed here).
    /// - Mars photos: NASA Image and Video Library (images-api.nasa.gov), loaded through /explorer/api/image.
    /// - Fact sheet numbers: <see cref="MarsFacts"/> (NASA NSSDCA Mars fact sheet).</summary>
    public class NasaLive : MonoBehaviour
    {
        public static NasaLive I;
        public const string DefaultSite = "https://spacefrontier01.netlify.app";

        public double DistanceKm = 225e6;     // filled from the JPL table at start, then from the live API
        public bool DistanceLive;             // true = fetched just now from JPL Horizons
        public string DistanceDate = "";
        public readonly Texture2D[] Photos = new Texture2D[3];
        public readonly string[] PhotoTitles = new string[3];
        public event Action Updated;

        static float[] table; static double tableJd0;

        /// <summary>The website the game is served from (so the REST calls are same-site), or our Netlify site.</summary>
        public static string SiteBase
        {
            get
            {
                var u = Application.absoluteURL;
                if (!string.IsNullOrEmpty(u) && u.StartsWith("http") && !u.Contains("localhost") && !u.Contains("127.0.0.1"))
                    try { return new Uri(u).GetLeftPart(UriPartial.Authority); } catch { }
                return DefaultSite;
            }
        }
        public static string ExplorerUrl => SiteBase + "/explorer/";

        public double LightMinutes => DistanceKm / MarsFacts.LightKmPerSec / 60.0;
        public string DistanceText => $"{DistanceKm / 1e6:0.0} million km";
        public string DelayText { get { double m = LightMinutes; int mm = (int)m, ss = (int)Math.Round((m - mm) * 60); if (ss == 60) { mm++; ss = 0; } return $"{mm} min {ss:00} s"; } }
        public string SourceText => DistanceLive ? "LIVE · NASA/JPL Horizons" : "NASA/JPL Horizons table";

        public void Begin()
        {
            I = this;
            LoadTable();
            var t = FromTable(DateTime.UtcNow);
            if (!double.IsNaN(t)) { DistanceKm = t; DistanceDate = DateTime.UtcNow.ToString("yyyy-MM-dd"); }
            StartCoroutine(FetchDistance());
            StartCoroutine(FetchPhotos());
        }

        // ------------------------------------------------------------------ Earth–Mars distance
        static void LoadTable()
        {
            if (table != null) return;
            var ta = Resources.Load<TextAsset>("Data/earth_mars_distance"); if (!ta) return;
            var vals = new System.Collections.Generic.List<float>(6000); bool first = true;
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.Trim(); if (line.Length == 0 || line[0] == '#') continue;
                if (first) { tableJd0 = double.Parse(line, CultureInfo.InvariantCulture); first = false; continue; }
                vals.Add(float.Parse(line, CultureInfo.InvariantCulture));
            }
            table = vals.ToArray();
        }

        static double Jd(DateTime utc) => utc.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays + 2440587.5;
        static DateTime FromJd(double jd) => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(jd - 2440587.5);

        /// <summary>Earth–Mars distance in km from the JPL daily table (NaN outside 2020–2035).</summary>
        public static double FromTable(DateTime utc)
        {
            LoadTable(); if (table == null) return double.NaN;
            double d = Jd(utc) - tableJd0; int i = (int)Math.Floor(d);
            if (i < 0 || i + 1 >= table.Length) return double.NaN;
            return Mathf.Lerp(table[i], table[i + 1], (float)(d - i)) * 1000.0;
        }

        /// <summary>Next time Earth and Mars are closest (opposition season), from the JPL table.</summary>
        public static bool NextClosest(out DateTime date, out double km)
        {
            date = default; km = 0; LoadTable(); if (table == null) return false;
            int i = Math.Max(1, (int)(Jd(DateTime.UtcNow) - tableJd0));
            for (; i < table.Length - 1; i++)
                if (table[i] <= table[i - 1] && table[i] <= table[i + 1]) { date = FromJd(tableJd0 + i); km = table[i] * 1000.0; return true; }
            return false;
        }

        IEnumerator FetchDistance()
        {
            var now = DateTime.UtcNow;
            string url = $"{SiteBase}/explorer/api/ephemeris?start={now:yyyy-MM-ddTHH:mm:ss}Z&steps=1";
            using (var r = UnityWebRequest.Get(url))
            {
                r.timeout = 40;
                yield return r.SendWebRequest();
                if (r.result == UnityWebRequest.Result.Success)
                {
                    var e = Vec(r.downloadHandler.text, "earth"); var m = Vec(r.downloadHandler.text, "mars");
                    if (e != null && m != null)
                    {
                        double dx = m[0] - e[0], dy = m[1] - e[1], dz = m[2] - e[2];
                        double km = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (km > 40e6 && km < 420e6) { DistanceKm = km; DistanceLive = true; DistanceDate = now.ToString("yyyy-MM-dd HH:mm") + " UTC"; }
                    }
                }
                else Debug.Log("JPL Horizons (via research site) offline: " + r.error);
            }
            Updated?.Invoke();
        }

        /// <summary>First [x,y,z] row of a body in the /api/ephemeris JSON: "bodies":{"earth":[[x,y,z],...]}.</summary>
        static double[] Vec(string json, string body)
        {
            var m = Regex.Match(json, "\"" + body + "\"\\s*:\\s*\\[\\s*\\[([^\\]]+)\\]");
            if (!m.Success) return null;
            var p = m.Groups[1].Value.Split(',');
            if (p.Length < 3) return null;
            var v = new double[3];
            for (int i = 0; i < 3; i++) if (!double.TryParse(p[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return null;
            return v;
        }

        // ------------------------------------------------------------------ NASA photos
        IEnumerator FetchPhotos()
        {
            string q = UnityWebRequest.EscapeURL("perseverance rover mars surface");
            using (var r = UnityWebRequest.Get($"https://images-api.nasa.gov/search?q={q}&media_type=image&page_size=20"))
            {
                r.timeout = 25;
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { Debug.Log("NASA Image Library offline: " + r.error); yield break; }
                var hrefs = Regex.Matches(r.downloadHandler.text, "\"href\"\\s*:\\s*\"(https://images-assets\\.nasa\\.gov/[^\"]+~thumb\\.jpg)\"");
                var titles = Regex.Matches(r.downloadHandler.text, "\"title\"\\s*:\\s*\"([^\"]+)\"");
                int got = 0;
                for (int i = 0; i < hrefs.Count && got < Photos.Length; i++)
                {
                    string img = hrefs[i].Groups[1].Value;
                    if (Application.platform == RuntimePlatform.WebGLPlayer) img = $"{SiteBase}/explorer/api/image?url={UnityWebRequest.EscapeURL(img)}";
                    using (var ir = UnityWebRequestTexture.GetTexture(img))
                    {
                        ir.timeout = 20;
                        yield return ir.SendWebRequest();
                        if (ir.result != UnityWebRequest.Result.Success) continue;
                        Photos[got] = DownloadHandlerTexture.GetContent(ir);
                        PhotoTitles[got] = i < titles.Count ? Regex.Unescape(titles[i].Groups[1].Value) : "NASA image";
                        got++;
                        Updated?.Invoke();
                    }
                }
            }
        }
    }

    /// <summary>NASA GISS Mars24 algorithm (Allison &amp; McEwen 2000): the real time and season on Mars right now.</summary>
    public static class Mars24
    {
        static readonly double[,] PBS = { { 0.0071, 2.2353, 49.409 }, { 0.0057, 2.7543, 168.173 }, { 0.0039, 1.1177, 191.837 }, { 0.0037, 15.7866, 21.736 }, { 0.0021, 2.1354, 15.704 }, { 0.0020, 2.4694, 95.528 }, { 0.0018, 32.8493, 49.095 } };
        static double Mod(double a, double n) => ((a % n) + n) % n;
        static double Rad(double d) => d * Math.PI / 180.0;

        /// <param name="ls">solar longitude in degrees (0 = northern spring)</param><param name="mtc">Coordinated Mars Time in hours</param><param name="msd">Mars Sol Date</param>
        public static void Now(out double ls, out double mtc, out double msd)
        {
            double jdUT = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays + 2440587.5;
            double dt = jdUT + 69.184 / 86400.0 - 2451545.0; // TT - UTC = 69.184 s
            double M = Mod(19.3871 + 0.52402073 * dt, 360), aFMS = Mod(270.3871 + 0.524038496 * dt, 360), pbs = 0;
            for (int i = 0; i < 7; i++) pbs += PBS[i, 0] * Math.Cos(Rad(0.985626 * dt / PBS[i, 1] + PBS[i, 2]));
            double eoc = (10.691 + 3.0e-7 * dt) * Math.Sin(Rad(M)) + 0.623 * Math.Sin(Rad(2 * M)) + 0.050 * Math.Sin(Rad(3 * M)) + 0.005 * Math.Sin(Rad(4 * M)) + 0.0005 * Math.Sin(Rad(5 * M)) + pbs;
            ls = Mod(aFMS + eoc, 360);
            msd = (dt - 4.5) / 1.0274912517 + 44796.0 - 0.0009626;
            mtc = Mod(24 * msd, 24);
        }
        public static string Season(double ls) => new[] { "spring", "summer", "autumn", "winter" }[(int)(ls / 90) % 4];
        public static string Clock(double h) { int hh = (int)h, mm = (int)((h - hh) * 60); return $"{hh:00}:{mm:00}"; }
    }

    /// <summary>NASA NSSDCA Mars fact sheet (nssdc.gsfc.nasa.gov/planetary/factsheet/marsfact.html), the same values the research site cites.</summary>
    public static class MarsFacts
    {
        public const double LightKmPerSec = 299792.458;
        public const float Gravity = 3.73f;            // m/s², 38 % of Earth
        public const string SolLength = "24 h 39 min 35 s";
        public const int MeanTempC = -63, ColdestC = -153, WarmestC = 20;
        public const float PressurePa = 636f;          // less than 1 % of Earth's
        public const float CO2 = 95.1f, N2 = 2.59f, Ar = 1.94f, O2 = 0.16f; // % by volume
        public const float YearDays = 686.98f, SynodicDays = 779.94f, Obliquity = 25.19f;
        public const float PhobosHours = 7.65f, DeimosHours = 30.3f;
        public const string Source = "NASA NSSDCA Mars Fact Sheet";
    }
}
