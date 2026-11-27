using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    public static class Sfx
    {
        public static void Play(string n, float vol = 1f, float pitch = 1f) { if (AudioManager.I) AudioManager.I.Play2D(n, vol, pitch); }
        public static void At(string n, Vector3 p, float vol = 1f, float pitch = 1f) { if (AudioManager.I) AudioManager.I.PlayAt(n, p, vol, pitch); }
    }

    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I;
        Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource musicA, musicB; bool aActive = true;
        public AudioSource Wind, Hum, Breath;

        readonly Dictionary<string, List<AudioClip>> variants = new Dictionary<string, List<AudioClip>>();

        public void Init(Dictionary<string, AudioClip> c)
        {
            I = this; clips = c;
            // real recorded sounds (Kenney.nl, CC0) in Resources/Audio replace the code-made ones with the same name.
            // "step__2" is a second version of "step": a random one plays each time, so footsteps never sound the same.
            foreach (var clip in Resources.LoadAll<AudioClip>("Audio"))
            {
                var n = clip.name; int k = n.IndexOf("__"); var key = k > 0 ? n.Substring(0, k) : n;
                if (!variants.TryGetValue(key, out var l)) variants[key] = l = new List<AudioClip>();
                l.Add(clip); clips[key] = l[0];
            }
            StartCoroutine(LoadStreamed());
            for (int i = 0; i < 16; i++) pool.Add(NewSource("sfx" + i));
            musicA = NewSource("musicA"); musicB = NewSource("musicB");
            musicA.loop = musicB.loop = true;
            Wind = Loop("wind", 0f); Hum = Loop("hum", 0f); Breath = Loop("breath", 0f);
        }

        AudioSource NewSource(string n)
        {
            var g = new GameObject(n); g.transform.SetParent(transform, false);
            var s = g.AddComponent<AudioSource>(); s.playOnAwake = false; return s;
        }

        public AudioClip Get(string n)
        {
            if (variants.TryGetValue(n, out var l) && l.Count > 1) return l[Random.Range(0, l.Count)];
            clips.TryGetValue(n, out var c);
            if (!c && n == "rover") clips.TryGetValue("hum", out c);
            return c;
        }

        /// <summary>Music and the Mars wind (CC0, OpenGameArt) stream in from StreamingAssets AFTER the game has started,
        /// so they never make the first loading slower. Until they arrive, the code-made versions play.</summary>
        IEnumerator LoadStreamed()
        {
            var files = new[] { ("music_ambient", "Music/ambient.mp3"), ("wind", "Music/wind.mp3") };
            foreach (var (name, file) in files)
            {
                var url = System.IO.Path.Combine(Application.streamingAssetsPath, file).Replace('\\', '/');
                if (!url.Contains("://")) url = "file:///" + url.TrimStart('/');
                using (var req = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
                {
                    yield return req.SendWebRequest();
                    if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) { Debug.LogWarning($"[Audio] {file}: {req.error}"); continue; }
                    var clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(req);
                    if (!clip) continue;
                    clip.name = name;
                    clips.TryGetValue(name, out var old); clips[name] = clip;
                    if (name.StartsWith("music_")) { realMusic.Add(name); if (wantMusic == name) Music(name, wantVol, 2f); continue; }
                    if (!old) continue;
                    foreach (var s in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (s.clip == old) { bool was = s.isPlaying; s.clip = clip; if (was) { if (s.loop && s.name.StartsWith("loop_")) s.time = Random.Range(0, clip.length * 0.5f); s.Play(); } }
                }
            }
        }

        public void Play2D(string n, float vol, float pitch)
        {
            var c = Get(n); if (!c) return;
            foreach (var s in pool)
                if (!s.isPlaying) { s.spatialBlend = 0; s.pitch = pitch; s.volume = vol; s.clip = c; s.Play(); return; }
        }

        public void PlayAt(string n, Vector3 p, float vol, float pitch)
        {
            var c = Get(n); if (!c) return;
            var g = new GameObject("sfx3d_" + n); g.transform.position = p;
            var s = g.AddComponent<AudioSource>(); s.clip = c; s.volume = vol; s.pitch = pitch;
            s.spatialBlend = 1; s.minDistance = 6; s.maxDistance = 400; s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.Play(); Destroy(g, c.length / Mathf.Max(0.1f, pitch) + 0.2f);
        }

        public AudioSource Loop(string n, float vol, Transform parent = null, float spatial = 0, float maxDist = 60)
        {
            var g = new GameObject("loop_" + n);
            g.transform.SetParent(parent ? parent : transform, false);
            var s = g.AddComponent<AudioSource>();
            s.clip = Get(n); s.loop = true; s.volume = vol; s.spatialBlend = spatial;
            s.minDistance = 3; s.maxDistance = maxDist; s.rolloffMode = AudioRolloffMode.Linear;
            if (s.clip) { s.time = Random.Range(0, s.clip.length * 0.9f); s.Play(); }
            return s;
        }

        readonly HashSet<string> realMusic = new HashSet<string>(); string wantMusic; float wantVol;
        /// <summary>Play a music track. Only REAL recorded music plays (never the old code-made drone): if the file has not
        /// arrived yet, it starts as soon as it does.</summary>
        public void Music(string n, float vol = 0.5f, float fade = 2.5f)
        {
            wantMusic = n; wantVol = vol;
            if (!realMusic.Contains(n)) { StopMusic(fade); wantMusic = n; return; }
            var from = aActive ? musicA : musicB; var to = aActive ? musicB : musicA; aActive = !aActive;
            if (from.clip && from.clip.name == n && from.isPlaying) { aActive = !aActive; return; }
            to.clip = Get(n); to.volume = 0; if (to.clip) to.Play();
            StartCoroutine(Xfade(from, to, vol, fade));
        }

        public void StopMusic(float fade = 2f)
        {
            wantMusic = null;
            var cur = aActive ? musicA : musicB;
            StartCoroutine(Xfade(cur, null, 0, fade));
        }

        IEnumerator Xfade(AudioSource from, AudioSource to, float vol, float dur)
        {
            float f0 = from ? from.volume : 0; float t = 0;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime; float k = t / dur;
                if (from) from.volume = Mathf.Lerp(f0, 0, k);
                if (to) to.volume = Mathf.Lerp(0, vol, k);
                yield return null;
            }
            if (from) { from.volume = 0; from.Stop(); }
        }

        public void FadeSource(AudioSource s, float to, float dur)
        {
            if (!s) return;
            StartCoroutine(FadeCo(s, to, dur));
        }
        IEnumerator FadeCo(AudioSource s, float to, float dur)
        {
            float f = s.volume, t = 0;
            while (t < dur && s) { t += Time.unscaledDeltaTime; s.volume = Mathf.Lerp(f, to, t / dur); yield return null; }
            if (s) s.volume = to;
        }
    }
}
