using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace MissionMars
{
    /// <summary>NASA Open APIs, called through our research site's REST API (github.com/ZuhriddinAbdunazarov/research),
    /// which adds the team's personal api.nasa.gov key on the server (NASA_API_KEY) so the key is never inside the game:
    ///   /explorer/api/donki?type=FLR  solar flares (NASA DONKI)          → the storms in the game
    ///   /explorer/api/donki?type=CME  solar plasma clouds (NASA DONKI)    → NASA DATA screen
    ///   /explorer/api/insight         Mars weather from NASA InSight      → outside temperature on the helmet HUD
    ///   /explorer/api/apod            Astronomy Picture of the Day        → NASA DATA screen
    /// If our site can't be reached the game asks NASA directly (DEMO_KEY / NASA CCMC), then uses cached or archived real data.</summary>
    public class NasaApi : MonoBehaviour
    {
        [Serializable] public class Apod { public string title, date, explanation, url, hdurl, media_type, mediaType, thumbnailUrl, copyright, image; }
        [Serializable] public class Photo { public string title, nasaId, image; }
        /// <summary>NASA numbers saved at build time by Site/tools/nasa-snapshot.mjs (one API call each).</summary>
        [Serializable] public class Snapshot { public string retrievedAt; public Flare[] flares; public int cmeCount = -1; public string cmeLatest; public Sol[] sols; public Apod apod; public Photo[] photos; public double earthMarsKm; }
        public static Snapshot Saved;
        [Serializable] public class Flare { public string flrID, beginTime, peakTime, classType, sourceLocation; }
        [Serializable] class FlareList { public Flare[] items; }
        [Serializable] public class Cme { public string activityID, startTime, note; }
        [Serializable] class CmeList { public Cme[] items; }
        [Serializable] public class Sol { public int sol; public string date, season; public float tempMinC, tempAvgC, tempMaxC, pressurePa, windMs; public string windDir; }
        [Serializable] class SolList { public Sol[] sols; }

        public static NasaApi I;
        public Apod ApodData; public Texture2D ApodImage; public Flare LatestFlare; public int FlareCount30d; public Flare[] AllFlares = new Flare[0]; public bool FlaresLive;
        public int CmeCount30d = -1; public Cme LatestCme; public bool CmeLive;
        public Sol[] Weather; public bool WeatherLive;
        public bool ApodDone, FlaresDone;
        public event Action Updated;

        /// <summary>Real InSight weather (sols 675–681, Oct 2020) used when there is no internet.</summary>
        static readonly Sol[] WeatherArchive =
        {
            new Sol { sol = 675, date = "2020-10-19", season = "fall", tempMinC = -96.9f, tempAvgC = -62.3f, tempMaxC = -15.9f, pressurePa = 751, windMs = 7.2f, windDir = "WNW" },
            new Sol { sol = 678, date = "2020-10-22", season = "fall", tempMinC = -98f, tempAvgC = -63f, tempMaxC = -9f, pressurePa = 744, windMs = 5.2f, windDir = "WNW" },
            new Sol { sol = 681, date = "2020-10-25", season = "fall", tempMinC = -95f, tempAvgC = -62f, tempMaxC = -4f, pressurePa = 744, windMs = 5.6f, windDir = "WNW" },
        };

        static string Api(string route) => NasaLive.SiteBase + "/explorer/api/" + route;
        static string Cache(string n) => Path.Combine(Application.persistentDataPath, n);

        /// <summary>The saved NASA numbers inside the game (loaded once, instantly, no network).</summary>
        public static Snapshot LoadSaved()
        {
            if (Saved != null) return Saved;
            try { var ta = Resources.Load<TextAsset>("Data/nasa_snapshot"); if (ta) Saved = JsonUtility.FromJson<Snapshot>(ta.text); } catch (Exception e) { Debug.LogWarning("NASA snapshot: " + e.Message); }
            return Saved ??= new Snapshot();
        }
        public string SavedDate => Saved != null && !string.IsNullOrEmpty(Saved.retrievedAt) && Saved.retrievedAt.Length >= 10 ? Saved.retrievedAt.Substring(0, 10) : "";

        public void Begin()
        {
            I = this;
            // 1) start instantly with the real numbers saved at build time
            var s = LoadSaved();
            Weather = s.sols != null && s.sols.Length > 0 ? s.sols : WeatherArchive;
            if (s.flares != null && s.flares.Length > 0) SetFlares(s.flares);
            CmeCount30d = s.cmeCount;
            if (!string.IsNullOrEmpty(s.cmeLatest)) LatestCme = new Cme { startTime = s.cmeLatest };
            if (s.apod != null && !string.IsNullOrEmpty(s.apod.title)) { ApodData = s.apod; if (!string.IsNullOrEmpty(s.apod.image)) ApodImage = R.Tex(s.apod.image); }
            // 2) newer numbers from the last time this player was online
            LoadCache();
            // 3) ask once, in the background, for fresh numbers; on any error the saved ones stay

            StartCoroutine(FetchFlares());
            StartCoroutine(FetchWeather());
            StartCoroutine(FetchCme());
            StartCoroutine(FetchApod());
        }

        void LoadCache()
        {
            try
            {
                if (File.Exists(Cache("apod.json"))) ApodData = JsonUtility.FromJson<Apod>(File.ReadAllText(Cache("apod.json")));
                if (File.Exists(Cache("apod.jpg"))) { ApodImage = new Texture2D(2, 2); ApodImage.LoadImage(File.ReadAllBytes(Cache("apod.jpg"))); }
                if (File.Exists(Cache("flares.json"))) { var c = File.ReadAllText(Cache("flares.json")).TrimStart(); UseFlares(c.StartsWith("[") ? "{\"items\":" + c + "}" : c); }
            }
            catch (Exception e) { Debug.LogWarning("NASA cache: " + e.Message); }
        }

        /// <summary>GET the first URL that answers with JSON starting with the given character.</summary>
        static IEnumerator GetFirst(string[] urls, char starts, Action<string> ok, int timeout = 40)
        {
            foreach (var url in urls)
            {
                using (var r = UnityWebRequest.Get(url))
                {
                    r.timeout = timeout;
                    yield return r.SendWebRequest();
                    var t = r.result == UnityWebRequest.Result.Success ? r.downloadHandler.text.TrimStart() : "";
                    if (t.Length > 0 && t[0] == starts && !t.Contains("\"error\"")) { ok(t); yield break; }
                    Debug.Log("NASA API offline: " + url.Split('?')[0] + " " + r.error);
                }
            }
        }

        IEnumerator FetchFlares()
        {
            var end = DateTime.UtcNow; var start = end.AddDays(-30);
            string wrapped = null; // always {"items":[...]}
            yield return GetFirst(new[] { Api("donki?type=FLR") }, '{', t => wrapped = t);
            if (wrapped == null) // our site is down: ask NASA directly
                yield return GetFirst(new[]
                {
                    $"https://api.nasa.gov/DONKI/FLR?startDate={start:yyyy-MM-dd}&endDate={end:yyyy-MM-dd}&api_key=DEMO_KEY",
                    $"https://kauai.ccmc.gsfc.nasa.gov/DONKI/WS/get/FLR?startDate={start:yyyy-MM-dd}&endDate={end:yyyy-MM-dd}",
                }, '[', t => wrapped = "{\"items\":" + t + "}", 20);
            if (wrapped != null && UseFlares(wrapped))
            {
                FlaresLive = true;
                try { File.WriteAllText(Cache("flares.json"), wrapped); } catch { }
            }
            FlaresDone = true; Updated?.Invoke();
        }

        IEnumerator FetchCme()
        {
            string json = null;
            yield return GetFirst(new[] { Api("donki?type=CME") }, '{', t => json = t);
            if (json != null)
            {
                var l = JsonUtility.FromJson<CmeList>(json);
                if (l?.items != null) { CmeCount30d = l.items.Length; CmeLive = true; if (l.items.Length > 0) LatestCme = l.items[l.items.Length - 1]; }
            }
            Updated?.Invoke();
        }

        IEnumerator FetchWeather()
        {
            string json = null;
            yield return GetFirst(new[] { Api("insight") }, '{', t => json = t);
            if (json != null)
            {
                var l = JsonUtility.FromJson<SolList>(json);
                if (l?.sols != null && l.sols.Length > 0) { Weather = l.sols; WeatherLive = true; }
            }
            Updated?.Invoke();
        }

        IEnumerator FetchApod()
        {
            string json = null;
            yield return GetFirst(new[] { Api("apod"), "https://api.nasa.gov/planetary/apod?api_key=DEMO_KEY" }, '{', t => json = t, 45);
            if (json != null)
            {
                var a = JsonUtility.FromJson<Apod>(json);
                if (a != null && !string.IsNullOrEmpty(a.title))
                {
                    ApodData = a;
                    try { File.WriteAllText(Cache("apod.json"), json); } catch { }
                    string type = a.media_type ?? a.mediaType;
                    string img = type == "image" ? a.url : a.thumbnailUrl;
                    if (!string.IsNullOrEmpty(img))
                    {
                        // apod.nasa.gov sends no CORS headers: in the browser load the picture through our site
                        if (Application.platform == RuntimePlatform.WebGLPlayer && img.Contains("apod.nasa.gov")) img = Api("image?url=" + UnityWebRequest.EscapeURL(img));
                        using (var ir = UnityWebRequestTexture.GetTexture(img))
                        {
                            ir.timeout = 25;
                            yield return ir.SendWebRequest();
                            if (ir.result == UnityWebRequest.Result.Success)
                            {
                                ApodImage = DownloadHandlerTexture.GetContent(ir);
                                try { File.WriteAllBytes(Cache("apod.jpg"), ir.downloadHandler.data); } catch { }
                            }
                        }
                    }
                }
            }
            ApodDone = true; Updated?.Invoke();
        }

        /// <summary>Reads {"items":[flares]} and keeps them. Returns false if there were none.</summary>
        bool UseFlares(string wrapped)
        {
            try
            {
                var l = JsonUtility.FromJson<FlareList>(wrapped);
                if (l?.items == null || l.items.Length == 0) return false;
                SetFlares(l.items);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("Flares parse: " + e.Message); return false; }
        }

        void SetFlares(Flare[] items)
        {
            FlareCount30d = items.Length; AllFlares = items;
            LatestFlare = items[items.Length - 1];
            // prefer the strongest recent X class flare for drama
            foreach (var f in items) if (f.classType != null && f.classType.StartsWith("X")) LatestFlare = f;
        }

        /// <summary>Outside temperature from the real InSight readings: night minimum to day maximum.</summary>
        public float TempC(float day01)
        {
            var w = Weather != null && Weather.Length > 0 ? Weather : WeatherArchive;
            var s = w[w.Length - 1];
            return Mathf.Lerp(s.tempMinC, s.tempMaxC, Mathf.Clamp01(day01));
        }
        public Sol LastSol => Weather != null && Weather.Length > 0 ? Weather[Weather.Length - 1] : WeatherArchive[WeatherArchive.Length - 1];

        public string FlareSummary()
        {
            if (LatestFlare == null) return "NASA DONKI space-weather feed: offline (no connection).";
            string day = LatestFlare.peakTime != null && LatestFlare.peakTime.Length >= 10 ? LatestFlare.peakTime.Substring(0, 10) : "recent";
            return $"LIVE NASA DONKI: {FlareCount30d} solar flares in the last 30 days. Notable: class {LatestFlare.classType} on {day}.";
        }
    }
}
