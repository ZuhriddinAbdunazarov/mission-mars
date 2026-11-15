using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace MissionMars
{
    /// <summary>NASA Open APIs (api.nasa.gov): Astronomy Picture of the Day + DONKI space weather (solar flares).
    /// Put your own key in StreamingAssets/nasa_api_key.txt (falls back to DEMO_KEY). Results are cached so
    /// the game still shows real data when the venue has no internet.</summary>
    public class NasaApi : MonoBehaviour
    {
        [Serializable] public class Apod { public string title, date, explanation, url, hdurl, media_type, copyright; }
        [Serializable] public class Flare { public string flrID, beginTime, peakTime, classType, sourceLocation; }
        [Serializable] class FlareList { public Flare[] items; }

        public static NasaApi I;
        public Apod ApodData; public Texture2D ApodImage; public Flare LatestFlare; public int FlareCount30d; public Flare[] AllFlares = new Flare[0]; public bool FlaresLive;
        public bool ApodDone, FlaresDone;
        public event Action Updated;

        static string Key
        {
            get
            {
                try
                {
                    var p = Path.Combine(Application.streamingAssetsPath, "nasa_api_key.txt");
                    if (File.Exists(p)) { var k = File.ReadAllText(p).Trim(); if (k.Length > 5) return k; }
                }
                catch { }
                return "DEMO_KEY";
            }
        }
        static string Cache(string n) => Path.Combine(Application.persistentDataPath, n);

        public void Begin()
        {
            I = this;
            LoadCache();
            StartCoroutine(FetchApod());
            StartCoroutine(FetchFlares());
        }

        void LoadCache()
        {
            try
            {
                if (File.Exists(Cache("apod.json"))) ApodData = JsonUtility.FromJson<Apod>(File.ReadAllText(Cache("apod.json")));
                if (File.Exists(Cache("apod.jpg"))) { ApodImage = new Texture2D(2, 2); ApodImage.LoadImage(File.ReadAllBytes(Cache("apod.jpg"))); }
                if (File.Exists(Cache("flares.json"))) ParseFlares(File.ReadAllText(Cache("flares.json")));
            }
            catch (Exception e) { Debug.LogWarning("NASA cache: " + e.Message); }
        }

        IEnumerator FetchApod()
        {
            using (var r = UnityWebRequest.Get("https://api.nasa.gov/planetary/apod?api_key=" + Key))
            {
                r.timeout = 25;
                yield return r.SendWebRequest();
                if (r.result == UnityWebRequest.Result.Success)
                {
                    var a = JsonUtility.FromJson<Apod>(r.downloadHandler.text);
                    if (a != null && !string.IsNullOrEmpty(a.title))
                    {
                        ApodData = a;
                        try { File.WriteAllText(Cache("apod.json"), r.downloadHandler.text); } catch { }
                        if (a.media_type == "image" && !string.IsNullOrEmpty(a.url))
                        {
                            using (var ir = UnityWebRequestTexture.GetTexture(a.url))
                            {
                                ir.timeout = 12;
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
                else Debug.Log("APOD offline: " + r.error);
            }
            ApodDone = true; Updated?.Invoke();
        }

        IEnumerator FetchFlares()
        {
            var end = DateTime.UtcNow; var start = end.AddDays(-30);
            string[] urls =
            {
                $"https://api.nasa.gov/DONKI/FLR?startDate={start:yyyy-MM-dd}&endDate={end:yyyy-MM-dd}&api_key={Key}",
                $"https://kauai.ccmc.gsfc.nasa.gov/DONKI/WS/get/FLR?startDate={start:yyyy-MM-dd}&endDate={end:yyyy-MM-dd}",
            };
            foreach (var url in urls)
            {
                if (FlaresLive) break;
                using (var r = UnityWebRequest.Get(url))
                {
                    r.timeout = 20;
                    yield return r.SendWebRequest();
                    if (r.result == UnityWebRequest.Result.Success && r.downloadHandler.text.TrimStart().StartsWith("["))
                    {
                        ParseFlares(r.downloadHandler.text); FlaresLive = true;
                        try { File.WriteAllText(Cache("flares.json"), r.downloadHandler.text); } catch { }
                    }
                    else Debug.Log("DONKI offline: " + url + " " + r.error);
                }
            }
            FlaresDone = true; Updated?.Invoke();
        }

        void ParseFlares(string json)
        {
            try
            {
                var l = JsonUtility.FromJson<FlareList>("{\"items\":" + json + "}");
                if (l?.items != null && l.items.Length > 0)
                {
                    FlareCount30d = l.items.Length; AllFlares = l.items;
                    LatestFlare = l.items[l.items.Length - 1];
                    // prefer the strongest recent X/M class flare for drama
                    foreach (var f in l.items) if (f.classType != null && f.classType.StartsWith("X")) LatestFlare = f;
                }
            }
            catch (Exception e) { Debug.LogWarning("Flares parse: " + e.Message); }
        }

        public string FlareSummary()
        {
            if (LatestFlare == null) return "NASA DONKI space-weather feed: offline (no connection).";
            string day = LatestFlare.peakTime != null && LatestFlare.peakTime.Length >= 10 ? LatestFlare.peakTime.Substring(0, 10) : "recent";
            return $"LIVE NASA DONKI: {FlareCount30d} solar flares in the last 30 days. Notable: class {LatestFlare.classType} on {day}.";
        }
    }
}
