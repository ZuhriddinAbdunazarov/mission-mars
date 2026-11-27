using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MissionMars
{
    /// <summary>NOVA speaks her lines out loud with the browser's own free voice (Web Speech API).
    /// The text stays on screen. Players can switch the voice off in the pause menu (saved on the device).</summary>
    public static class Voice
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void MM_Speak(string text, float rate, float pitch, float volume);
        [DllImport("__Internal")] static extern void MM_StopSpeak();
        [DllImport("__Internal")] static extern int MM_SpeakReady();
        public static bool Available => true;
#else
        static void MM_Speak(string text, float rate, float pitch, float volume) { }
        static void MM_StopSpeak() { }
        static int MM_SpeakReady() => 0;
        public static bool Available => false;
#endif
        const string PrefKey = "missionmars_voice";
        public static bool On
        {
            get { try { return PlayerPrefs.GetInt(PrefKey, 1) == 1; } catch { return true; } }
            set { try { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); } catch { } if (!value) Stop(); }
        }

        static readonly Regex Tags = new Regex("<[^>]+>");
        /// <summary>Seconds the line needs when spoken (so the text stays long enough).</summary>
        public static float Seconds(string text) => Available && On ? Clean(text).Length / 13.5f + 0.5f : 0;
        static string Clean(string t) => Tags.Replace(t ?? "", "").Replace("—", ", ").Replace("·", ", ").Replace("°C", " degrees").Replace("mSv", " millisieverts").Replace("CO2", "C O 2").Replace("O2", "O 2");

        public static void Warm() { try { MM_SpeakReady(); } catch { } }

        public static void Say(string text)
        {
            if (!Available || !On || string.IsNullOrEmpty(text)) return;
            try { MM_Speak(Clean(text), 1.02f, 1.3f, 0.95f); } catch { } // a bit higher pitch: a friendly robot
        }

        public static void Stop() { try { MM_StopSpeak(); } catch { } }
    }
}
