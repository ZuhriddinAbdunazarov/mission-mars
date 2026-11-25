using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    /// <summary>The shelter door keypad. The code (1122) is printed at the end of the Junior Astronaut Handbook,
    /// and shown on a sticky note next to the keypad so nobody gets stuck. Click the buttons or type the numbers.</summary>
    public class Keypad : MonoBehaviour
    {
        public const string Code = "1122";
        string typed = ""; int wrong; bool done;
        TextMeshProUGUI display, help;
        Image screen;

        /// <summary>Show the keypad and wait until the right code is typed.</summary>
        public static IEnumerator Ask()
        {
            var g = new GameObject("Keypad", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cv = g.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 80;
            var sc = g.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1600, 900); sc.matchWidthOrHeight = 0.5f;
            var k = g.AddComponent<Keypad>(); k.Build((RectTransform)g.transform);
            UIState.Push();
            Sfx.Play("beep", 0.5f);
            while (!k.done) yield return null;
            yield return new WaitForSecondsRealtime(0.6f);
            UIState.Pop(); Computers.ClosedFrame = Time.frameCount;
            Destroy(g);
        }

        static void Put(RectTransform rt, float x, float y, float w, float h) { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h); }

        void Build(RectTransform root)
        {
            var shade = UIX.Img(root, "Shade", new Color(0.01f, 0.015f, 0.02f, 0.65f), null, true); shade.rectTransform.Fill();
            var panel = UIX.Img(root, "Panel", new Color(0.13f, 0.14f, 0.16f, 0.98f), UISprites.Round, true).rectTransform; Put(panel, -60, 0, 520, 720);
            var title = UIX.Text(panel, "SHELTER DOOR", 34, HC.Amber, R.Kid, TextAlignmentOptions.Center); Put(title.rectTransform, 0, 310, 480, 50);
            var sub = UIX.Text(panel, "Type the 4-number door code", 22, HC.Text, R.KidBody, TextAlignmentOptions.Center); Put(sub.rectTransform, 0, 268, 480, 34);
            screen = UIX.Img(panel, "Screen", new Color(0.02f, 0.08f, 0.05f), UISprites.Round); Put(screen.rectTransform, 0, 200, 400, 84);
            display = UIX.Text(screen.transform, "_ _ _ _", 54, new Color(0.4f, 1f, 0.6f), R.Mono, TextAlignmentOptions.Center); display.rectTransform.Fill();
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" };
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                var b = UIX.Button(panel, key, () => Press(key), 110, 84, 36, key == "OK" ? HC.Green : key == "C" ? HC.Red : HC.Cyan);
                Put((RectTransform)b.transform, -130 + (i % 3) * 130, 90 - (i / 3) * 100, 110, 84);
            }
            help = UIX.Text(panel, "", 20, HC.Text, R.KidBody, TextAlignmentOptions.Center); Put(help.rectTransform, 0, -320, 480, 50);
            // the sticky note with the code (from the end of the Handbook)
            var note = UIX.Img(root, "Note", new Color(1f, 0.92f, 0.45f), UISprites.Round).rectTransform; Put(note, 330, 120, 270, 220);
            note.localRotation = Quaternion.Euler(0, 0, -6);
            var nt = UIX.Text(note, "JUNIOR ASTRONAUT HANDBOOK\nend of the book:\n<size=60><b>1122</b></size>", 22, new Color(0.15f, 0.12f, 0.05f), R.Kid, TextAlignmentOptions.Center);
            nt.rectTransform.Fill(14);
        }

        void Update()
        {
            if (done) return;
            for (int d = 0; d <= 9; d++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d)) Press(d.ToString());
            if (Input.GetKeyDown(KeyCode.Backspace)) Press("C");
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Press("OK");
        }

        void Press(string key)
        {
            if (done) return;
            Sfx.Play("click", 0.6f, 1.2f);
            if (key == "C") typed = "";
            else if (key == "OK") { Check(); return; }
            else if (typed.Length < 4) typed += key;
            Show();
            if (typed.Length == 4) Check();
        }

        void Show()
        {
            var s = ""; for (int i = 0; i < 4; i++) s += (i < typed.Length ? typed[i].ToString() : "_") + (i < 3 ? " " : "");
            display.text = s;
        }

        void Check()
        {
            if (typed == Code)
            {
                done = true; display.text = "OPEN"; display.color = HC.Green; screen.color = new Color(0.03f, 0.2f, 0.08f);
                Sfx.Play("success", 0.7f); help.text = "Code accepted. Welcome, Commander!";
                return;
            }
            wrong++; typed = ""; Show();
            Sfx.Play("alarm", 0.4f, 1.4f); StartCoroutine(Flash());
            help.text = wrong >= 3 ? "Look at the yellow note: the code is 1 1 2 2" : "Wrong code. Look at the end of your Handbook!";
            if (wrong == 3 && Game.Hud) Game.Hud.Say("The code is at the END of your Junior Astronaut Handbook: 1, 1, 2, 2!", 5);
        }

        IEnumerator Flash()
        {
            for (int i = 0; i < 3; i++) { screen.color = new Color(0.35f, 0.04f, 0.03f); yield return new WaitForSecondsRealtime(0.08f); screen.color = new Color(0.02f, 0.08f, 0.05f); yield return new WaitForSecondsRealtime(0.08f); }
        }
    }
}
