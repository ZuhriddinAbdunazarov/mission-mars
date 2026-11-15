using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    /// <summary>Procedural sound design: every sound in the game is synthesized at startup.</summary>
    public static class Synth
    {
        public const int SR = 44100;
        static System.Random rng = new System.Random(1234);
        static float N() => (float)(rng.NextDouble() * 2 - 1);

        static float[] Clip(string name, float[] d) => d;

        /// <summary>Main thread only: turn generated sample data into AudioClips.</summary>
        public static Dictionary<string, AudioClip> ToClips(Dictionary<string, float[]> data)
        {
            var o = new Dictionary<string, AudioClip>();
            foreach (var kv in data)
            {
                var c = AudioClip.Create(kv.Key, kv.Value.Length, 1, SR, false);
                c.SetData(kv.Value, 0); o[kv.Key] = c;
            }
            return o;
        }

        static float Env(float t, float a, float dcy) => t < a ? t / a : Mathf.Exp(-(t - a) / dcy);

        static void Normalize(float[] d, float peak)
        {
            float m = 0.0001f; foreach (var v in d) m = Mathf.Max(m, Mathf.Abs(v));
            float k = peak / m; for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        /// <summary>Crossfade the tail into the head so the clip loops seamlessly.</summary>
        static float[] MakeLoop(float[] d, float fadeSec)
        {
            int f = (int)(fadeSec * SR);
            int n = d.Length - f;
            var o = new float[n];
            for (int i = 0; i < n; i++) o[i] = d[i];
            for (int i = 0; i < f; i++)
            {
                float t = i / (float)f;
                o[i] = d[i] * t + d[n + i] * (1 - t);
            }
            return o;
        }

        static float[] Tone(float dur, Func<float, float> f)
        {
            int n = (int)(dur * SR); var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = f(i / (float)SR);
            return d;
        }

        static float Sin(float hz, float t) => Mathf.Sin(2 * Mathf.PI * hz * t);

        /// <summary>Thread-safe: generates raw sample data (call from a worker thread).</summary>
        public static Dictionary<string, float[]> BuildAll()
        {
            var c = new Dictionary<string, float[]>();

            c["click"] = Clip("click", Tone(0.06f, t => Sin(1800, t) * Env(t, 0.002f, 0.012f) * 0.5f + Sin(900, t) * Env(t, 0.001f, 0.02f) * 0.3f));
            c["hover"] = Clip("hover", Tone(0.04f, t => Sin(2600, t) * Env(t, 0.002f, 0.008f) * 0.25f));
            c["type"] = Clip("type", Tone(0.025f, t => (Sin(3800, t) * 0.4f + N() * 0.3f) * Env(t, 0.0005f, 0.004f) * 0.35f));
            c["beep"] = Clip("beep", Tone(0.14f, t => Sin(1046, t) * Env(t, 0.004f, 0.06f) * 0.4f));
            c["pickup"] = Clip("pickup", Tone(0.35f, t => (Sin(t < 0.09f ? 880 : 1318, t) + 0.3f * Sin(t < 0.09f ? 1760 : 2636, t)) * Env(t, 0.005f, 0.12f) * 0.35f));
            c["success"] = Clip("success", Tone(1.3f, t =>
            {
                float s = 0; float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                for (int k = 0; k < 4; k++) { float st = k * 0.09f; if (t > st) { float tt = t - st; s += (Sin(notes[k], tt) + 0.25f * Sin(notes[k] * 2, tt) + 0.1f * Sin(notes[k] * 3.01f, tt)) * Env(tt, 0.004f, 0.35f); } }
                return s * 0.22f;
            }));
            c["error"] = Clip("error", Tone(0.45f, t =>
            {
                float hz = t < 0.18f ? 220 : 165; float sq = Mathf.Sign(Sin(hz, t)) * 0.4f + Sin(hz, t) * 0.6f;
                return sq * Env(t < 0.18f ? t : t - 0.18f, 0.005f, 0.12f) * 0.28f;
            }));
            c["notify"] = Clip("notify", Tone(0.5f, t => (Sin(784, t) * Env(t, 0.004f, 0.12f) + Sin(1175, Mathf.Max(0, t - 0.08f)) * Env(Mathf.Max(0, t - 0.08f), 0.004f, 0.18f) * (t > 0.08f ? 1 : 0)) * 0.25f));
            c["alarm"] = Clip("alarm", Tone(1.0f, t =>
            {
                float hz = (t % 0.5f) < 0.25f ? 880 : 620; float s = Mathf.Sign(Sin(hz, t)) * 0.3f + Sin(hz, t) * 0.5f;
                return s * 0.22f * Mathf.Clamp01(((t % 0.25f) / 0.01f)) * Mathf.Clamp01((0.25f - (t % 0.25f)) / 0.02f);
            }));
            c["heartbeat"] = Clip("heartbeat", Tone(1.0f, t => (Sin(55, t) * Env(t, 0.01f, 0.07f) + Sin(50, Mathf.Max(0, t - 0.28f)) * Env(Mathf.Max(0, t - 0.28f), 0.01f, 0.09f) * (t > 0.28f ? 0.8f : 0)) * 0.9f));
            c["whoosh"] = Clip("whoosh", Whoosh());
            c["build"] = Clip("build", Tone(0.5f, t => (N() * 0.5f + Sin(140, t) * 0.5f) * Env(t, 0.005f, 0.08f) * 0.5f + Sin(2400 + 800 * Mathf.Sin(t * 40), t) * Env(t, 0.01f, 0.2f) * 0.12f));
            c["thud"] = Clip("thud", Thud());
            c["step"] = Clip("step", Step());
            c["explosion"] = Clip("explosion", Explosion());
            c["powerup"] = Clip("powerup", Tone(2.2f, t =>
            {
                float hz = 60 + 400 * Mathf.Pow(t / 2.2f, 2); float s = Sin(hz, t) * 0.5f + Sin(hz * 2, t) * 0.25f;
                return s * Mathf.Clamp01(t * 3) * Mathf.Clamp01((2.2f - t) * 4) * 0.35f;
            }));
            c["breath"] = Clip("breath", Breath());

            c["wind"] = Clip("wind", MakeLoop(Wind(14f), 2f));
            c["hum"] = Clip("hum", MakeLoop(Tone(5f, t => (Sin(50, t) * 0.5f + Sin(100, t) * 0.3f + Sin(150, t) * 0.12f) * 0.3f + N() * 0.01f), 0.5f));
            c["rocket"] = Clip("rocket", MakeLoop(Rumble(6f), 1f));
            c["fire"] = Clip("fire", MakeLoop(Fire(6f), 1f));
            c["music_ambient"] = Clip("music_ambient", MakeLoop(Pad(new[] {
                new[]{ 146.83f, 220f, 261.63f, 329.63f },   // Dm9-ish
                new[]{ 116.54f, 174.61f, 233.08f, 293.66f }, // Bb
                new[]{ 130.81f, 196f, 246.94f, 329.63f },   // Cmaj7
                new[]{ 110f, 164.81f, 220f, 261.63f } },    // Am
                9f, 0.18f, 7), 3f));
            c["music_hope"] = Clip("music_hope", MakeLoop(Pad(new[] {
                new[]{ 130.81f, 196f, 261.63f, 329.63f },   // C
                new[]{ 174.61f, 220f, 261.63f, 349.23f },   // F
                new[]{ 110f, 220f, 261.63f, 329.63f },      // Am
                new[]{ 196f, 246.94f, 293.66f, 392f } },    // G
                6f, 0.2f, 11), 2f));
            c["music_tension"] = Clip("music_tension", MakeLoop(Drone(16f), 2f));
            return c;
        }

        static float[] Whoosh()
        {
            int n = (int)(1.2f * SR); var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; float k = 0.02f + 0.25f * Mathf.Sin(Mathf.PI * t / 1.2f);
                lp += (N() - lp) * k; d[i] = lp * Mathf.Sin(Mathf.PI * t / 1.2f);
            }
            Normalize(d, 0.5f); return d;
        }

        static float[] Thud()
        {
            int n = (int)(0.5f * SR); var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++) { float t = i / (float)SR; lp += (N() - lp) * 0.08f; d[i] = (Sin(70 - 30 * t, t) * 0.8f + lp * 0.6f) * Env(t, 0.003f, 0.09f); }
            Normalize(d, 0.7f); return d;
        }

        static float[] Step()
        {
            int n = (int)(0.16f * SR); var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++) { float t = i / (float)SR; lp += (N() - lp) * 0.18f; d[i] = (lp * 0.9f + Sin(90, t) * 0.4f) * Env(t, 0.004f, 0.035f); }
            Normalize(d, 0.5f); return d;
        }

        static float[] Explosion()
        {
            int n = (int)(4f * SR); var d = new float[n]; float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; float k = Mathf.Lerp(0.5f, 0.02f, Mathf.Clamp01(t / 2.5f));
                lp += (N() - lp) * k; lp2 += (lp - lp2) * 0.3f;
                float crack = (rng.NextDouble() < 0.002 * Mathf.Exp(-t * 1.5f)) ? N() * 2 : 0;
                d[i] = (lp2 * 1.2f + Sin(38, t) * 0.9f * Mathf.Exp(-t * 2.2f) + crack) * Env(t, 0.004f, 0.9f);
            }
            Normalize(d, 0.95f); return d;
        }

        static float[] Breath()
        {
            int n = (int)(3.2f * SR); var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; lp += (N() - lp) * 0.06f;
                float env = t < 1.3f ? Mathf.Sin(Mathf.PI * t / 1.3f) : (t > 1.6f && t < 3.1f ? Mathf.Sin(Mathf.PI * (t - 1.6f) / 1.5f) * 0.7f : 0);
                d[i] = lp * env;
            }
            Normalize(d, 0.35f); return d;
        }

        static float[] Wind(float dur)
        {
            int n = (int)(dur * SR); var d = new float[n]; float b = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                b = Mathf.Clamp(b + N() * 0.02f, -1, 1) * 0.998f;
                float gust = 0.55f + 0.3f * Mathf.Sin(t * 0.45f) + 0.15f * Mathf.Sin(t * 1.3f + 1.7f);
                float k = 0.015f + 0.03f * gust;
                lp += (N() - lp) * k;
                d[i] = (b * 0.6f + lp * 1.4f) * gust;
            }
            Normalize(d, 0.6f); return d;
        }

        static float[] Rumble(float dur)
        {
            int n = (int)(dur * SR); var d = new float[n]; float lp = 0, b = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; lp += (N() - lp) * 0.12f; b = Mathf.Clamp(b + N() * 0.04f, -1, 1) * 0.995f;
                d[i] = lp * 0.8f + b * 0.9f + Sin(42, t) * 0.25f + (rng.NextDouble() < 0.004 ? N() * 0.6f : 0);
            }
            Normalize(d, 0.85f); return d;
        }

        static float[] Fire(float dur)
        {
            int n = (int)(dur * SR); var d = new float[n]; float lp = 0, cr = 0;
            for (int i = 0; i < n; i++)
            {
                lp += (N() - lp) * 0.05f;
                if (rng.NextDouble() < 0.0015) cr = 1; cr *= 0.993f;
                d[i] = lp * 0.6f + N() * cr * 0.5f;
            }
            Normalize(d, 0.5f); return d;
        }

        static float[] Drone(float dur)
        {
            int n = (int)(dur * SR); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; float lfo = 0.5f + 0.5f * Mathf.Sin(t * 0.4f);
                d[i] = Sin(55, t) * 0.4f + Sin(55.4f, t) * 0.3f + Sin(82.4f, t) * 0.2f * lfo + Sin(116.5f, t) * 0.12f * (1 - lfo) + Sin(220.3f, t) * 0.05f * lfo;
            }
            Normalize(d, 0.5f); return d;
        }

        /// <summary>Warm evolving pad with detuned voices, soft bells and a feedback delay as "space reverb".</summary>
        static float[] Pad(float[][] chords, float chordLen, float bellRate, int seed)
        {
            var r = new System.Random(seed);
            float dur = chords.Length * chordLen + 4f;
            int n = (int)(dur * SR); var d = new float[n];
            for (int ci = 0; ci < chords.Length; ci++)
            {
                float start = ci * chordLen;
                foreach (var f in chords[ci])
                {
                    for (int v = 0; v < 3; v++)
                    {
                        float det = 1 + (v - 1) * 0.0035f; float ph = (float)r.NextDouble() * 6.28f;
                        int s0 = (int)(start * SR), s1 = Mathf.Min(n, (int)((start + chordLen + 3f) * SR));
                        for (int i = s0; i < s1; i++)
                        {
                            float t = (i - s0) / (float)SR;
                            float env = Mathf.Clamp01(t / 2.5f) * Mathf.Clamp01((chordLen + 3f - t) / 3f);
                            float w = 2 * Mathf.PI * f * det * t + ph;
                            d[i] += (Mathf.Sin(w) + 0.28f * Mathf.Sin(2 * w) + 0.1f * Mathf.Sin(3 * w)) * env * 0.05f;
                        }
                    }
                }
                // bells: arpeggiated chord tones one/two octaves up
                int bells = (int)(chordLen / 0.75f);
                for (int b = 0; b < bells; b++)
                {
                    if (r.NextDouble() > 0.55) continue;
                    float bf = chords[ci][r.Next(chords[ci].Length)] * (r.NextDouble() < 0.5 ? 2 : 4);
                    int s0 = (int)((start + b * 0.75f) * SR);
                    for (int i = s0; i < Mathf.Min(n, s0 + 3 * SR); i++)
                    {
                        float t = (i - s0) / (float)SR;
                        d[i] += (Mathf.Sin(2 * Mathf.PI * bf * t) + 0.2f * Mathf.Sin(2 * Mathf.PI * bf * 2.76f * t) * Mathf.Exp(-t * 6)) * Mathf.Exp(-t * 1.6f) * bellRate * Mathf.Clamp01(t / 0.003f);
                    }
                }
            }
            // feedback delay "reverb"
            int dl = (int)(0.43f * SR), dl2 = (int)(0.71f * SR);
            for (int i = 0; i < n; i++)
            {
                if (i >= dl) d[i] += d[i - dl] * 0.35f;
                if (i >= dl2) d[i] += d[i - dl2] * 0.22f;
            }
            // gentle low-pass
            float lp = 0; for (int i = 0; i < n; i++) { lp += (d[i] - lp) * 0.35f; d[i] = lp; }
            Normalize(d, 0.55f); return d;
        }
    }
}
