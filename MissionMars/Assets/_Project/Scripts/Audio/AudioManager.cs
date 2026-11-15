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

        public void Init(Dictionary<string, AudioClip> c)
        {
            I = this; clips = c;
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

        public AudioClip Get(string n) { clips.TryGetValue(n, out var c); return c; }

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

        public void Music(string n, float vol = 0.5f, float fade = 2.5f)
        {
            var from = aActive ? musicA : musicB; var to = aActive ? musicB : musicA; aActive = !aActive;
            if (from.clip && from.clip.name == n && from.isPlaying) { aActive = !aActive; return; }
            to.clip = Get(n); to.volume = 0; if (to.clip) to.Play();
            StartCoroutine(Xfade(from, to, vol, fade));
        }

        public void StopMusic(float fade = 2f)
        {
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
