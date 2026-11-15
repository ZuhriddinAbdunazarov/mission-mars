using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    /// <summary>Top-most overlay: fades, flashes, letterbox bars and chapter title cards.</summary>
    public class Cinema : MonoBehaviour
    {
        public static Cinema I;
        Image black, white; RectTransform barTop, barBot;
        CanvasGroup titleGroup; TextMeshProUGUI titleSmall, titleBig, titleSub;
        CanvasGroup capGroup; TextMeshProUGUI caption;

        public static Cinema Create()
        {
            var g = new GameObject("Cinema");
            var c = g.AddComponent<Cinema>(); I = c; c.Build(); return c;
        }

        void Build()
        {
            var cv = gameObject.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 1000;
            var cs = gameObject.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)transform;
            barTop = UIX.Img(root, "BarTop", Color.black).rectTransform; barTop.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 130), new Vector2(0, 130));
            barBot = UIX.Img(root, "BarBot", Color.black).rectTransform; barBot.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, -130), new Vector2(0, 130));

            capGroup = UIX.Go("Caption", root).AddComponent<CanvasGroup>();
            ((RectTransform)capGroup.transform).At(new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1500, 60));
            caption = UIX.Text(capGroup.transform, "", 28, new Color(0.9f, 0.92f, 0.95f), R.Body, TextAlignmentOptions.Center);
            caption.rectTransform.Fill(); capGroup.alpha = 0;

            titleGroup = UIX.Go("Title", root).AddComponent<CanvasGroup>();
            ((RectTransform)titleGroup.transform).Fill();
            titleSmall = UIX.Text(titleGroup.transform, "", 22, Pal.Accent, R.Bold, TextAlignmentOptions.Center); titleSmall.characterSpacing = 18;
            titleSmall.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(1600, 40));
            titleBig = UIX.Text(titleGroup.transform, "", 64, Pal.Text, R.Title, TextAlignmentOptions.Center); titleBig.characterSpacing = 10;
            titleBig.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1800, 90));
            titleSub = UIX.Text(titleGroup.transform, "", 24, Pal.Dim, R.Body, TextAlignmentOptions.Center);
            titleSub.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, -68), new Vector2(1400, 40));
            titleGroup.alpha = 0;

            lidTop = UIX.Img(root, "LidTop", Color.black).rectTransform; lidTop.Set(new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero);
            lidBot = UIX.Img(root, "LidBot", Color.black).rectTransform; lidBot.Set(new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            lidTop.gameObject.SetActive(false); lidBot.gameObject.SetActive(false);
            black = UIX.Img(root, "Black", new Color(0, 0, 0, 0)); black.rectTransform.Fill();
            white = UIX.Img(root, "White", new Color(1, 1, 1, 0)); white.rectTransform.Fill();
        }

        RectTransform lidTop, lidBot;
        /// <summary>Eyelids: 0 = closed, 1 = open.</summary>
        public void Eyes(float open)
        {
            bool on = open < 0.999f; lidTop.gameObject.SetActive(on); lidBot.gameObject.SetActive(on);
            float k = Mathf.Clamp01(open);
            lidTop.anchorMin = new Vector2(0, Mathf.Lerp(0.5f, 1.02f, k)); lidBot.anchorMax = new Vector2(1, Mathf.Lerp(0.5f, -0.02f, k));
        }
        public void SetBlack(float a) => black.color = new Color(0, 0, 0, a);
        public IEnumerator ToBlack(float d = 0.8f) { yield return FadeImg(black, 1, d); }
        public IEnumerator FromBlack(float d = 1.2f) { yield return FadeImg(black, 0, d); }
        public IEnumerator Flash(float hold = 0.1f, float d = 1.5f)
        {
            white.color = new Color(1, 1, 1, 1);
            yield return new WaitForSecondsRealtime(hold);
            yield return FadeImg(white, 0, d);
        }
        public IEnumerator WhiteOut(float d) { yield return FadeImg(white, 1, d); }
        public IEnumerator WhiteIn(float d) { yield return FadeImg(white, 0, d); }

        IEnumerator FadeImg(Image i, float to, float d)
        {
            float from = i.color.a, t = 0;
            while (t < d) { t += Time.unscaledDeltaTime; var c = i.color; c.a = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / d)); i.color = c; yield return null; }
            var cc = i.color; cc.a = to; i.color = cc;
        }

        public void Letterbox(bool on, float d = 0.8f)
        {
            float from = barTop.anchoredPosition.y, to = on ? 0 : 130;
            Tween.Run(d, t => { float y = Mathf.Lerp(from, to, Tween.EaseInOut(t)); barTop.anchoredPosition = new Vector2(0, y); barBot.anchoredPosition = new Vector2(0, -y); });
        }

        Coroutine capCo;
        public void Caption(string s, float dur = 4f) { if (capCo != null) StopCoroutine(capCo); capCo = StartCoroutine(CapCo(s, dur)); }
        public void ClearCaption() { if (capCo != null) StopCoroutine(capCo); capCo = null; capGroup.alpha = 0; }
        IEnumerator CapCo(string s, float dur)
        {
            caption.text = s; Tween.Fade(capGroup, 1, 0.5f);
            yield return new WaitForSecondsRealtime(dur);
            Tween.Fade(capGroup, 0, 0.6f);
        }

        public IEnumerator TitleCard(string small, string big, string sub, float hold = 2.8f)
        {
            titleSmall.text = small; titleBig.text = big; titleSub.text = sub;
            titleBig.characterSpacing = 30;
            Sfx.Play("whoosh", 0.5f);
            Tween.Run(hold + 1.2f, t => titleBig.characterSpacing = Mathf.Lerp(30, 10, Tween.EaseOut(t)));
            yield return FadeGroup(titleGroup, 1, 0.8f);
            yield return new WaitForSecondsRealtime(hold);
            yield return FadeGroup(titleGroup, 0, 0.8f);
        }

        IEnumerator FadeGroup(CanvasGroup g, float to, float d)
        {
            float from = g.alpha, t = 0;
            while (t < d) { t += Time.unscaledDeltaTime; g.alpha = Mathf.Lerp(from, to, t / d); yield return null; }
            g.alpha = to;
        }
    }
}
