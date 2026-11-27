using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    /// <summary>What is saved after every finished mission.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int cp;              // the mission to start from (3 = MAKE AIR AND WATER ... 9 = FLY HOME)
        public int sol;
        public string startDate;    // the real date the game started (keeps the calendar the same)
        public int stars;
        public string[] done;       // Astronaut Pass awards already given
        public float o2, health, dose;
        public int windowsMissed;
    }

    /// <summary>Auto-save in the browser's own storage on this device (Unity PlayerPrefs = IndexedDB on the web).
    /// Free, no account, no server, and instant. Another computer or cleared browser data = a new game.</summary>
    public static class SaveGame
    {
        const string Key = "missionmars_save_v1";

        public static SaveData Load()
        {
            try
            {
                if (!PlayerPrefs.HasKey(Key)) return null;
                var d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
                return d != null && d.cp >= 3 && d.cp <= 9 ? d : null;
            }
            catch (Exception e) { Debug.LogWarning("[Save] could not read: " + e.Message); return null; }
        }

        public static void Write(SaveData d)
        {
            try { PlayerPrefs.SetString(Key, JsonUtility.ToJson(d)); PlayerPrefs.Save(); ShowSaved(); }
            catch (Exception e) { Debug.LogWarning("[Save] could not write: " + e.Message); }
        }

        public static void Clear()
        {
            try { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); } catch { }
        }

        // a small "GAME SAVED" note in the corner for 2 seconds
        static void ShowSaved()
        {
            if (!Game.Runner) return;
            var g = new GameObject("SavedNote", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var cv = g.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 70;
            var sc = g.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1600, 900); sc.matchWidthOrHeight = 0.5f;
            var t = UIX.Text(g.transform, "<color=#7CF29A>●</color>  GAME SAVED", 22, HC.Text, R.Bold, TextAlignmentOptions.Right);
            t.rectTransform.Set(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 150), new Vector2(320, 34));
            Game.Runner.StartCoroutine(Fade(g));
        }
        static IEnumerator Fade(GameObject g)
        {
            var cg = g.GetComponent<CanvasGroup>(); cg.blocksRaycasts = false;
            for (float t = 0; t < 2.4f; t += Time.unscaledDeltaTime) { cg.alpha = t < 0.3f ? t / 0.3f : t > 1.9f ? (2.4f - t) / 0.5f : 1; yield return null; }
            UnityEngine.Object.Destroy(g);
        }
    }
}
