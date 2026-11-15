using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    /// <summary>The Colonist &amp; Engineer's Field Handbook — an open book with a table of contents and scrollable chapters.</summary>
    public class HandbookUI : MonoBehaviour
    {
        public static HandbookUI I;
        public bool IsOpen { get; private set; }
        CanvasGroup group; RectTransform book;
        TextMeshProUGUI chNum, chTitle, chSub, chBody, keyFact, pageNum;
        ScrollRect scroll;
        readonly List<(Image bg, TextMeshProUGUI txt)> toc = new List<(Image, TextMeshProUGUI)>();
        int current;
        int highlight = -1;
        public System.Action OnClosed;

        public static HandbookUI Create()
        {
            var g = new GameObject("Handbook");
            var h = g.AddComponent<HandbookUI>(); I = h; h.Build(); return h;
        }

        void Build()
        {
            var cv = gameObject.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 70;
            var cs = gameObject.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
            var root = (RectTransform)transform;
            var dim = UIX.Img(root, "Dim", new Color(0.01f, 0.01f, 0.015f, 0.82f), null, true); dim.rectTransform.Fill();

            book = UIX.Go("Book", root).RT(); book.At(new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1560, 900));
            var shadow = UIX.Img(book, "Shadow", new Color(0, 0, 0, 0.6f), UISprites.Glow); shadow.rectTransform.Fill(-80);
            var cover = UIX.Panel(book, "Cover", new Color(0.24f, 0.13f, 0.08f), true, new Color(0.85f, 0.6f, 0.3f, 0.35f)); cover.rectTransform.Fill();
            // pages
            var left = UIX.Panel(book, "LeftPage", Pal.Paper, false); left.rectTransform.Set(new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            left.rectTransform.offsetMin = new Vector2(24, 24); left.rectTransform.offsetMax = new Vector2(-2, -24);
            var right = UIX.Panel(book, "RightPage", Pal.Paper, false); right.rectTransform.Set(new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            right.rectTransform.offsetMin = new Vector2(2, 24); right.rectTransform.offsetMax = new Vector2(-24, -24);
            var spine = UIX.Img(book, "Spine", new Color(0.35f, 0.25f, 0.18f, 0.45f), UISprites.Glow); spine.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 860));

            // ---- left page: title + contents ----
            var lp = left.rectTransform;
            var bookTitle = UIX.Text(lp, "THE COLONIST & ENGINEER'S", 18, Pal.InkAccent, R.Bold, TextAlignmentOptions.Center); bookTitle.characterSpacing = 10;
            bookTitle.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(-80, 26));
            var bookTitle2 = UIX.Text(lp, "Field Handbook", 46, Pal.Ink, R.Book, TextAlignmentOptions.Center); bookTitle2.fontStyle = FontStyles.Bold;
            bookTitle2.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(-80, 60));
            var ed = UIX.Text(lp, "Survival · Science · Engineering — Mars Edition", 19, new Color(0.4f, 0.32f, 0.27f), R.Book, TextAlignmentOptions.Center); ed.fontStyle = FontStyles.Italic;
            ed.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -132), new Vector2(-80, 28));
            var rule = UIX.Img(lp, "Rule", new Color(0.55f, 0.35f, 0.2f, 0.5f)); rule.rectTransform.Set(new Vector2(0.1f, 1), new Vector2(0.9f, 1), new Vector2(0.5f, 1), new Vector2(0, -172), new Vector2(0, 2));
            var contents = UIX.Text(lp, "CONTENTS", 16, Pal.InkAccent, R.Bold); contents.characterSpacing = 10;
            contents.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -190), new Vector2(-120, 24));
            var chs = HandbookContent.Chapters;
            for (int i = 0; i < chs.Length; i++)
            {
                int idx = i;
                var row = UIX.Img(lp, "TOC" + i, new Color(0.66f, 0.24f, 0.08f, 0), UISprites.Round, true);
                row.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(46, -222 - i * 41), new Vector2(-92, 38));
                var t = UIX.Text(row.transform, $"<color=#A83D14><b>{i + 1:00}</b></color>   {chs[i].Title}", 23, Pal.Ink, R.Book, TextAlignmentOptions.MidlineLeft);
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(14, 0);
                var b = row.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { Show(idx); });
                var hv = row.gameObject.AddComponent<TocHover>(); hv.Img = row;
                toc.Add((row, t));
            }
            var kfBox = UIX.Panel(lp, "KeyFact", new Color(0.66f, 0.24f, 0.08f, 0.1f), true, new Color(0.66f, 0.24f, 0.08f, 0.4f));
            kfBox.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(-100, 86));
            var kfh = UIX.Text(kfBox.transform, "KEY FACT", 14, Pal.InkAccent, R.Bold); kfh.characterSpacing = 8;
            kfh.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(18, -10), new Vector2(-36, 20));
            keyFact = UIX.Text(kfBox.transform, "", 21, Pal.Ink, R.Book);
            keyFact.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(18, -32), new Vector2(-36, 50));
            keyFact.fontStyle = FontStyles.Bold;

            // ---- right page: chapter ----
            var rp = right.rectTransform;
            chNum = UIX.Text(rp, "", 16, Pal.InkAccent, R.Bold); chNum.characterSpacing = 12;
            chNum.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(64, -44), new Vector2(-128, 24));
            chTitle = UIX.Text(rp, "", 42, Pal.Ink, R.Book); chTitle.fontStyle = FontStyles.Bold;
            chTitle.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(64, -70), new Vector2(-128, 56));
            chSub = UIX.Text(rp, "", 21, new Color(0.42f, 0.34f, 0.28f), R.Book); chSub.fontStyle = FontStyles.Italic;
            chSub.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(64, -126), new Vector2(-128, 30));
            var rule2 = UIX.Img(rp, "Rule", new Color(0.55f, 0.35f, 0.2f, 0.5f)); rule2.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(64, -166), new Vector2(-128, 2));

            var view = UIX.Go("Viewport", rp).RT(); view.Set(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            view.offsetMin = new Vector2(64, 80); view.offsetMax = new Vector2(-50, -180);
            view.gameObject.AddComponent<RectMask2D>();
            var vimg = view.gameObject.AddComponent<Image>(); vimg.color = new Color(0, 0, 0, 0);
            chBody = UIX.Text(view, "", 23, Pal.Ink, R.Book);
            chBody.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-16, 100));
            chBody.lineSpacing = 6; chBody.paragraphSpacing = 14;
            var fit = chBody.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = chBody.rectTransform; scroll.viewport = view; scroll.horizontal = false; scroll.scrollSensitivity = 40; scroll.movementType = ScrollRect.MovementType.Clamped;

            pageNum = UIX.Text(rp, "", 18, new Color(0.45f, 0.36f, 0.3f), R.Book, TextAlignmentOptions.Center);
            pageNum.rectTransform.Set(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(200, 30));
            var prev = UIX.Button(rp, "PREV", () => Show(current - 1), 150, 44, 18, Pal.InkAccent);
            prev.GetComponent<RectTransform>().Set(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, 22), new Vector2(150, 44));
            var next = UIX.Button(rp, "NEXT", () => Show(current + 1), 150, 44, 18, Pal.InkAccent);
            next.GetComponent<RectTransform>().Set(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-50, 22), new Vector2(150, 44));

            var close = UIX.Button(book, "CLOSE   [H]", Close, 200, 46, 18);
            close.GetComponent<RectTransform>().Set(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 14), new Vector2(200, 46));
            var hint = UIX.Text(book, "Scroll to read  ·  Click a chapter to jump", 18, Pal.Dim, R.Bold, TextAlignmentOptions.Left);
            hint.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(10, 22), new Vector2(600, 30));

            gameObject.SetActive(true);
        }

        public void Open(int chapter = -1, bool highlightIt = false)
        {
            if (IsOpen) { if (chapter >= 0) Show(chapter); return; }
            IsOpen = true; UIState.Push();
            group.blocksRaycasts = true;
            highlight = highlightIt ? chapter : -1;
            Show(chapter >= 0 ? chapter : current);
            Sfx.Play("whoosh", 0.45f, 1.4f);
            Tween.Fade(group, 1, 0.3f);
            Tween.Run(0.4f, t => book.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, Tween.EaseOut(t)));
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; UIState.Pop();
            group.blocksRaycasts = false;
            Tween.Fade(group, 0, 0.25f);
            OnClosed?.Invoke();
        }

        public void Show(int i)
        {
            var chs = HandbookContent.Chapters;
            i = Mathf.Clamp(i, 0, chs.Length - 1);
            if (i != current) Sfx.Play("whoosh", 0.3f, 1.8f);
            current = i;
            var c = chs[i];
            chNum.text = $"CHAPTER {i + 1:00}";
            chTitle.text = c.Title; chSub.text = c.Subtitle; chBody.text = c.Body; keyFact.text = c.KeyFact;
            pageNum.text = $"— {i + 1} / {chs.Length} —";
            scroll.verticalNormalizedPosition = 1;
            chBody.rectTransform.anchoredPosition = Vector2.zero;
            for (int k = 0; k < toc.Count; k++)
            {
                bool sel = k == i;
                toc[k].bg.color = new Color(0.66f, 0.24f, 0.08f, sel ? 0.16f : 0);
                toc[k].bg.GetComponent<TocHover>().Selected = sel;
                string mark = k == highlight ? "   <color=#C2410C><b>— ANSWER HERE</b></color>" : "";
                toc[k].txt.text = $"<color=#A83D14><b>{k + 1:00}</b></color>   {(sel ? "<b>" : "")}{chs[k].Title}{(sel ? "</b>" : "")}{mark}";
            }
            Tween.Run(0.25f, t => { if (chBody) chBody.alpha = t; });
        }

        void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.H)) { Close(); return; }
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Show(current + 1);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Show(current - 1);
        }
    }

    public class TocHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public Image Img; public bool Selected;
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (!Selected) Img.color = new Color(0.66f, 0.24f, 0.08f, 0.08f); Sfx.Play("hover", 0.3f); }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (!Selected) Img.color = new Color(0.66f, 0.24f, 0.08f, 0); }
    }
}
