using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    /// <summary>Looking through the improvised telescope: the Sun (with filter), then the tiny blue Earth.</summary>
    public class TelescopeView : MonoBehaviour
    {
        CanvasGroup group; TextMeshProUGUI label, sub, readout; Image tint;
        static Sprite hole;

        public static TelescopeView Create()
        {
            var g = new GameObject("Telescope"); var t = g.AddComponent<TelescopeView>(); t.Build(); return t;
        }

        static Sprite Hole()
        {
            if (hole) return hole;
            int s = 512; var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2 - 1, dy = (y + 0.5f) / s * 2 - 1; float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(0, 1, (d - 0.86f) / 0.12f);
                    px[y * s + x] = new Color32(0, 0, 0, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            return hole = Sprite.Create(t, new Rect(0, 0, s, s), Vector2.one * 0.5f);
        }

        void Build()
        {
            var cv = gameObject.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 40;
            var cs = gameObject.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 1f;
            group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
            var root = (RectTransform)transform;
            tint = UIX.Img(root, "Tint", new Color(0, 0, 0, 0)); tint.rectTransform.Fill();
            var h = UIX.Img(root, "Hole", Color.white, Hole()); h.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 1000));
            var l = UIX.Img(root, "L", Color.black); l.rectTransform.Set(new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(1, 0.5f), new Vector2(-499, 0), new Vector2(0, 0)); l.rectTransform.offsetMin = new Vector2(0, 0); l.rectTransform.offsetMax = new Vector2(-499, 0);
            var r = UIX.Img(root, "R", Color.black); r.rectTransform.anchorMin = new Vector2(0.5f, 0); r.rectTransform.anchorMax = new Vector2(1, 1); r.rectTransform.offsetMin = new Vector2(499, 0); r.rectTransform.offsetMax = Vector2.zero;
            // reticle
            Color rc = new Color(0.6f, 1f, 0.8f, 0.45f);
            UIX.Img(root, "H1", rc).rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(-260, 0), new Vector2(300, 1.5f));
            UIX.Img(root, "H2", rc).rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(260, 0), new Vector2(300, 1.5f));
            UIX.Img(root, "V1", rc).rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1.5f, 300));
            UIX.Img(root, "V2", rc).rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, -260), new Vector2(1.5f, 300));
            var ring = UIX.Img(root, "Ring", rc, UISprites.Ring); ring.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 140));
            label = UIX.Text(root, "", 34, new Color(0.7f, 1f, 0.85f), R.Title, TextAlignmentOptions.Center); label.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(1200, 50));
            sub = UIX.Text(root, "", 24, new Color(0.75f, 0.85f, 0.85f), R.Body, TextAlignmentOptions.Center); sub.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, -380), new Vector2(1200, 80));
            readout = UIX.Text(root, "", 20, new Color(0.6f, 1f, 0.8f, 0.8f), R.Mono, TextAlignmentOptions.TopLeft); readout.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(-640, 360), new Vector2(500, 200));
        }

        /// <summary>Full scripted telescope sequence. Uses the player camera.</summary>
        public IEnumerator Sequence(PlayerController p, Vector3 eyePos)
        {
            var cam = p.Cam; var sky = Game.Sky;
            UIState.Cinematic = true; UIState.Refresh(); p.Frozen = true;
            yield return Cinema.I.ToBlack(0.5f);
            Game.Hud.ShowHUD(false, 0.2f);
            var oldParent = cam.transform.parent; var oldPos = cam.transform.localPosition; var oldRot = cam.transform.localRotation; float oldFov = cam.fieldOfView;
            cam.transform.SetParent(null); cam.transform.position = eyePos;
            Vector3 sunDir = sky.SunDir; Vector3 earthDir = sky.EarthDir;
            cam.transform.rotation = Quaternion.LookRotation(sunDir); cam.fieldOfView = 9;
            group.alpha = 1; tint.color = new Color(0.25f, 0.12f, 0f, 0.35f);
            label.text = "THE SUN"; sub.text = "Solar filter ON. Even from Mars, never look at the Sun without one.";
            readout.text = "TARGET: SOL\nDIST: 228,000,000 km\nAPPARENT SIZE: 66% of Earth's view\nFILTER: ND5";
            yield return Cinema.I.FromBlack(0.8f);
            yield return Game.Hud.Say("ASTRONAUT", "The Sun... it looks smaller from here. Only about 43% as much light as on Earth.", 1.5f);
            yield return new WaitForSeconds(0.5f);
            // slew to Earth
            label.text = ""; sub.text = "Slewing..."; readout.text = "SLEW IN PROGRESS"; Sfx.Play("whoosh", 0.4f, 0.6f);
            World.ShowEarth(eyePos, earthDir, sky.SunDir, true);
            Quaternion a = cam.transform.rotation, b = Quaternion.LookRotation(earthDir);
            float t = 0;
            while (t < 3f) { t += Time.deltaTime; float k = Tween.EaseInOut(t / 3f); cam.transform.rotation = Quaternion.Slerp(a, b, k); cam.fieldOfView = Mathf.Lerp(9, 0.6f, k); tint.color = new Color(0.25f, 0.12f, 0, 0.35f * (1 - k)); sky.SkyMat.SetFloat("_EarthGlow", 1 - k); yield return null; }
            Sfx.Play("beep", 0.5f);
            label.text = "EARTH"; sub.text = "That tiny blue dot is home — and everyone you have ever known.";
            readout.text = "TARGET: EARTH\nDIST: 225,000,000 km\nLIGHT DELAY: 12.5 min\nSTATUS: OUT OF RANGE";
            yield return Game.Hud.Say("ASTRONAUT", "There it is. Earth. 225 million kilometres away.", 1.6f);
            yield return Game.Hud.Say("ASTRONAUT", "Even if I shouted for help, my signal would need over twelve minutes to arrive.", 1.6f);
            yield return new WaitForSeconds(0.8f);
            yield return Cinema.I.ToBlack(0.6f);
            group.alpha = 0;
            World.ShowEarth(eyePos, earthDir, sky.SunDir, false);
            sky.SkyMat.SetFloat("_EarthGlow", 1);
            cam.transform.SetParent(oldParent); cam.transform.localPosition = oldPos; cam.transform.localRotation = oldRot; cam.fieldOfView = oldFov;
            Game.Hud.ShowHUD(true, 0.3f);
            UIState.Cinematic = false; UIState.Refresh(); p.Frozen = false;
            yield return Cinema.I.FromBlack(0.6f);
        }
    }
}
