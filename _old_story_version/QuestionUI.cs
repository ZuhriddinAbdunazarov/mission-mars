using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    /// <summary>Calibration quiz: answer science questions (with the Handbook's help) to unlock modules.</summary>
    public class QuestionUI : MonoBehaviour
    {
        public static QuestionUI I;
        CanvasGroup group; RectTransform panel;
        TextMeshProUGUI header, module, counter, question, hint, feedback;
        readonly List<(Button b, Image bg, TextMeshProUGUI t, Image badge, TextMeshProUGUI letter, UIButtonFx fx)> answers = new List<(Button, Image, TextMeshProUGUI, Image, TextMeshProUGUI, UIButtonFx)>();
        Button continueBtn, bookBtn;
        int chosen = -1; bool continuePressed;
        Question cur;
        public int CorrectFirstTry, Mistakes;

        public static QuestionUI Create()
        {
            var g = new GameObject("QuestionUI");
            var q = g.AddComponent<QuestionUI>(); I = q; q.Build(); return q;
        }

        void Build()
        {
            var cv = gameObject.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 60;
            var cs = gameObject.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
            var root = (RectTransform)transform;
            var dim = UIX.Img(root, "Dim", new Color(0, 0.01f, 0.02f, 0.7f), UISprites.Vignette, true); dim.rectTransform.Fill();
            var dim2 = UIX.Img(root, "Dim2", new Color(0, 0.01f, 0.02f, 0.45f), null, false); dim2.rectTransform.Fill();

            panel = UIX.Panel(root, "Panel", new Color(0.03f, 0.045f, 0.07f, 0.94f), true, new Color(0.37f, 0.89f, 0.95f, 0.3f)).rectTransform;
            panel.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 720));
            UIX.Corners(panel, Pal.Cyan, 22);
            var top = UIX.Img(panel, "TopGlow", new Color(0.37f, 0.89f, 0.95f, 0.08f), UISprites.HGradient); top.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(2, -2), new Vector2(-4, 110));

            header = UIX.Text(panel, "MODULE LOCKED  ·  CALIBRATION REQUIRED", 16, Pal.Cyan, R.Bold); header.characterSpacing = 8;
            header.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(56, -34), new Vector2(-112, 24));
            module = UIX.Text(panel, "", 34, Pal.Text, R.Title);
            module.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(56, -62), new Vector2(-112, 48));
            counter = UIX.Text(panel, "", 18, Pal.Dim, R.Mono, TextAlignmentOptions.TopRight);
            counter.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(56, -36), new Vector2(-112, 24));

            question = UIX.Text(panel, "", 34, Pal.Text, R.Bold);
            question.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(56, -140), new Vector2(-112, 100));
            question.lineSpacing = -6;

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var b = UIX.Button(panel, "", () => Choose(idx), 520, 84, 24, Pal.Cyan);
                var rt = (RectTransform)b.transform;
                rt.Set(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i % 2 == 0 ? -1 : 1) * 274, -268 - (i / 2) * 100), new Vector2(528, 86));
                var label = b.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.MidlineLeft; label.characterSpacing = 0; label.font = R.Body; label.fontSize = 25;
                label.rectTransform.offsetMin = new Vector2(100, 0); label.rectTransform.offsetMax = new Vector2(-16, 0);
                var badge = UIX.Panel(b.transform, "Badge", new Color(1, 1, 1, 0.08f), true, new Color(0.37f, 0.89f, 0.95f, 0.5f)); badge.raycastTarget = false;
                badge.rectTransform.At(new Vector2(0, 0.5f), new Vector2(42, 0), new Vector2(46, 46));
                var letter = UIX.Text(badge.transform, "ABCD"[i].ToString(), 22, Pal.Cyan, R.Title, TextAlignmentOptions.Center); letter.rectTransform.Fill();
                answers.Add((b, b.GetComponent<Image>(), label, badge, letter, b.GetComponent<UIButtonFx>()));
            }

            var hintRow = UIX.Panel(panel, "HintRow", new Color(1f, 0.48f, 0.22f, 0.08f), true, new Color(1f, 0.48f, 0.22f, 0.35f));
            hintRow.raycastTarget = false;
            hintRow.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(-112, 64));
            hint = UIX.Text(hintRow.transform, "", 21, Pal.Text, R.Body, TextAlignmentOptions.MidlineLeft);
            hint.rectTransform.Fill(); hint.rectTransform.offsetMin = new Vector2(22, 0); hint.rectTransform.offsetMax = new Vector2(-300, 0);
            bookBtn = UIX.Button(hintRow.transform, "OPEN HANDBOOK  [H]", OpenBook, 270, 46, 18);
            ((RectTransform)bookBtn.transform).Set(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(270, 46));

            feedback = UIX.Text(panel, "", 23, Pal.Text, R.Body, TextAlignmentOptions.MidlineLeft);
            feedback.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(56, 40), new Vector2(-400, 90));
            continueBtn = UIX.Button(panel, "CONTINUE", () => continuePressed = true, 260, 58, 22, Pal.Good);
            ((RectTransform)continueBtn.transform).Set(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-56, 48), new Vector2(260, 58));
            continueBtn.gameObject.SetActive(false);
        }

        void OpenBook()
        {
            if (cur == null || HandbookUI.I == null) return;
            Tween.Fade(group, 0, 0.2f);
            HandbookUI.I.OnClosed = () => { Tween.Fade(group, 1, 0.25f); HandbookUI.I.OnClosed = null; };
            HandbookUI.I.Open(cur.Chapter, true);
        }

        void Choose(int i) { if (chosen == -2) chosen = i; }

        void Update()
        {
            if (group.blocksRaycasts && !HandbookUI.I.IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.H)) OpenBook();
                if (continueBtn.gameObject.activeSelf && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))) continuePressed = true;
                for (int k = 0; k < 4; k++) if (Input.GetKeyDown(KeyCode.Alpha1 + k)) Choose(k);
            }
        }

        /// <summary>Ask questions in sequence until each is answered correctly.</summary>
        public IEnumerator Ask(string moduleName, IList<Question> qs, System.Action<Question> onCorrect = null)
        {
            UIState.Push();
            module.text = moduleName;
            group.blocksRaycasts = true;
            Tween.Fade(group, 1, 0.35f);
            Tween.Run(0.45f, t => panel.localScale = Vector3.one * Mathf.Lerp(0.9f, 1, Tween.Back(t)));
            Sfx.Play("powerup", 0.4f);

            for (int qi = 0; qi < qs.Count; qi++)
            {
                cur = qs[qi];
                counter.text = $"QUESTION {qi + 1} OF {qs.Count}";
                question.text = cur.Text;
                var ch = HandbookContent.Chapters[cur.Chapter];
                hint.text = $"<color=#FF7B3A><b>HANDBOOK</b></color>  Chapter {cur.Chapter + 1}: <b>{ch.Title}</b>";
                feedback.text = "<color=#9AA8B5>Choose an answer — press 1–4 or click.</color>";
                continueBtn.gameObject.SetActive(false);
                // shuffle display order
                var order = new List<int> { 0, 1, 2, 3 };
                for (int k = 0; k < 4; k++) { int r = Random.Range(k, 4); (order[k], order[r]) = (order[r], order[k]); }
                for (int k = 0; k < 4; k++)
                {
                    var a = answers[k];
                    a.t.text = cur.Options[order[k]];
                    a.fx.Locked = false; a.fx.SetBase(new Color(0.06f, 0.08f, 0.11f, 0.75f));
                    a.badge.color = new Color(1, 1, 1, 0.08f); a.letter.color = Pal.Cyan; a.letter.text = "ABCD"[k].ToString();
                    a.b.interactable = true;
                    var rt = (RectTransform)a.b.transform; float delay = k * 0.06f;
                    var cg = a.b.Group();
                    cg.alpha = 0;
                    Tween.Run(0.35f + delay, t => { if (cg) cg.alpha = Mathf.Clamp01((t * (0.35f + delay) - delay) / 0.35f); });
                }
                question.maxVisibleCharacters = 99999;
                bool firstTry = true;
                while (true)
                {
                    chosen = -2;
                    while (chosen < 0) yield return null;
                    int picked = order[chosen];
                    var a = answers[chosen];
                    if (picked == cur.Correct)
                    {
                        a.fx.SetBase(new Color(0.1f, 0.45f, 0.22f, 0.9f)); a.badge.color = Pal.Good; a.letter.text = "";
                        var chk = UIX.Img(a.badge.transform, "Check", new Color(0.02f, 0.1f, 0.05f), UISprites.Check); chk.rectTransform.Fill(6);
                        Destroy(chk.gameObject, 30f);
                        foreach (var o in answers) { o.fx.Locked = true; o.b.interactable = false; }
                        Sfx.Play("success", 0.7f);
                        feedback.text = $"<color=#5CF28C><b>CORRECT.</b></color>  {cur.Explain}";
                        if (firstTry) CorrectFirstTry++;
                        onCorrect?.Invoke(cur);
                        Tween.Run(0.35f, t => a.b.transform.localScale = Vector3.one * (1 + 0.06f * Mathf.Sin(t * Mathf.PI)));
                        break;
                    }
                    else
                    {
                        firstTry = false; Mistakes++;
                        a.fx.SetBase(new Color(0.45f, 0.08f, 0.06f, 0.9f)); a.badge.color = Pal.Bad; a.letter.color = Color.white;
                        a.fx.Locked = true; a.b.interactable = false;
                        Sfx.Play("error", 0.7f);
                        feedback.text = $"<color=#FF5A4D><b>CALIBRATION FAILED.</b></color>  Check <b>Chapter {cur.Chapter + 1}</b> of the Handbook and try again.";
                        var rt = panel; Vector2 basePos = Vector2.zero;
                        Tween.Run(0.4f, t => rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 40) * 14 * (1 - t), 0));
                    }
                }
                continueBtn.gameObject.SetActive(true);
                continuePressed = false;
                while (!continuePressed) yield return null;
                Sfx.Play("click");
            }
            Tween.Fade(group, 0, 0.3f);
            group.blocksRaycasts = false;
            cur = null;
            UIState.Pop();
            yield return new WaitForSecondsRealtime(0.3f);
        }
    }
}
