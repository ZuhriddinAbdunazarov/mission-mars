using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    public class MenuUI : MonoBehaviour
    {
        CanvasGroup group, side; RectTransform sideRT;
        RawImage apodImg; TextMeshProUGUI apodTitle, apodMeta, flareText;
        public Action<int> OnStart; public Action OnQuit;
        readonly List<CanvasGroup> stagger = new List<CanvasGroup>();

        public static MenuUI Create()
        {
            var g = new GameObject("MenuUI"); var m = g.AddComponent<MenuUI>(); m.Build(); return m;
        }

        void Build()
        {
            var cv = gameObject.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 30;
            var cs = gameObject.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>();
            var root = (RectTransform)transform;
            var shade = UIX.Img(root, "Shade", new Color(0, 0, 0, 0.55f), UISprites.HGradient); shade.rectTransform.Set(Vector2.zero, new Vector2(0.62f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);

            var col = UIX.Go("Column", root).RT(); col.Set(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(130, 0), new Vector2(900, 0));
            var tag = UIX.Text(col, "NASA SPACE APPS CHALLENGE  ·  2026", 18, Pal.Accent, R.Bold); tag.characterSpacing = 10;
            tag.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -170), new Vector2(0, 26));
            var title = UIX.Text(col, "MISSION MARS", 104, Pal.Text, R.Title); title.characterSpacing = 4;
            title.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(-4, -200), new Vector2(0, 130));
            title.enableWordWrapping = false;
            var sub = UIX.Text(col, "THE LAUNCH WINDOW", 34, Pal.Accent, R.Bold); sub.characterSpacing = 26;
            sub.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -322), new Vector2(0, 44));
            var line = UIX.Img(col, "Line", new Color(1, 0.48f, 0.22f, 0.8f), UISprites.HGradient); line.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -380), new Vector2(520, 2));
            var tagline = UIX.Text(col, "Crash-landed in Jezero Crater. 780 days until the planets align.\nSurvive with science — and get home.", 25, new Color(0.82f, 0.86f, 0.9f), R.Body);
            tagline.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -398), new Vector2(-160, 80));
            foreach (var t in new Component[] { tag, title, sub, line, tagline }) stagger.Add(t.Group());

            string[] labels = { "START MISSION", "CHAPTER SELECT", "FIELD HANDBOOK", "CONTROLS", "QUIT" };
            Action[] acts = { () => OnStart?.Invoke(0), ShowChapters, () => HandbookUI.I.Open(0), ShowControls, () => OnQuit?.Invoke() };
            for (int i = 0; i < labels.Length; i++)
            {
                var b = UIX.Button(col, labels[i], acts[i], 380, 62, 24, i == 0 ? Pal.Accent : Pal.Cyan);
                ((RectTransform)b.transform).Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -520 - i * 76), new Vector2(380, 62));
                b.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
                b.GetComponentInChildren<TextMeshProUGUI>().rectTransform.offsetMin = new Vector2(30, 0);
                if (i == 0) { var fx = b.GetComponent<UIButtonFx>(); fx.SetBase(new Color(0.55f, 0.2f, 0.06f, 0.85f)); }
                stagger.Add(b.Group());
            }

            // side panel (chapters / controls)
            side = UIX.Go("Side", root).AddComponent<CanvasGroup>(); sideRT = (RectTransform)side.transform;
            sideRT.Set(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(580, -170), new Vector2(560, 420));
            side.alpha = 0; side.blocksRaycasts = false;

            // NASA live card
            var card = UIX.Panel(root, "NasaCard", new Color(0.03f, 0.045f, 0.07f, 0.82f), true, new Color(0.37f, 0.89f, 0.95f, 0.25f));
            card.rectTransform.Set(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 110), new Vector2(460, 410));
            var ch = UIX.Text(card.transform, "LIVE FROM NASA OPEN APIs", 15, Pal.Cyan, R.Bold); ch.characterSpacing = 6;
            ch.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -16), new Vector2(-40, 22));
            var imgBg = UIX.Img(card.transform, "ImgBg", new Color(0, 0, 0, 0.6f), UISprites.Round); imgBg.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -46), new Vector2(-40, 226));
            var mask = imgBg.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = true;
            var ig = UIX.Go("Img", imgBg.transform); apodImg = ig.AddComponent<RawImage>(); apodImg.rectTransform.Fill(); apodImg.color = new Color(1, 1, 1, 0);
            var loading = UIX.Text(imgBg.transform, "Astronomy Picture of the Day\nconnecting to api.nasa.gov ...", 18, Pal.Dim, R.Body, TextAlignmentOptions.Center); loading.rectTransform.Fill();
            apodTitle = UIX.Text(card.transform, "", 21, Pal.Text, R.Bold);
            apodTitle.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -282), new Vector2(-40, 28));
            apodTitle.enableWordWrapping = false; apodTitle.overflowMode = TextOverflowModes.Ellipsis;
            apodMeta = UIX.Text(card.transform, "", 15, Pal.Dim, R.Mono);
            apodMeta.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -310), new Vector2(-40, 20));
            flareText = UIX.Text(card.transform, "", 16, new Color(1f, 0.8f, 0.55f), R.Body);
            flareText.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -338), new Vector2(-40, 60));
            stagger.Add(card.Group());
            if (NasaApi.I) NasaApi.I.Updated += () => { RefreshNasa(); if (NasaApi.I.ApodImage) loading.text = ""; };
            RefreshNasa();
            if (NasaApi.I && NasaApi.I.ApodImage) loading.text = "";

            var foot = UIX.Text(root, "3D models & planet textures: NASA 3D Resources  ·  Ground textures: Poly Haven (CC0)  ·  Fonts: Google Fonts (OFL)  ·  Live data: api.nasa.gov  ·  Not an official NASA product", 15, new Color(0.6f, 0.65f, 0.7f, 0.8f), R.Body, TextAlignmentOptions.BottomLeft);
            foot.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(130, 34), new Vector2(-700, 24));
        }

        void RefreshNasa()
        {
            var n = NasaApi.I; if (!n) return;
            if (n.ApodData != null)
            {
                apodTitle.text = n.ApodData.title;
                apodMeta.text = $"NASA APOD  ·  {n.ApodData.date}";
            }
            if (n.ApodImage)
            {
                apodImg.texture = n.ApodImage; apodImg.color = Color.white;
                // cover-fit
                float ar = n.ApodImage.width / (float)n.ApodImage.height, boxAr = 420f / 226f;
                apodImg.uvRect = ar > boxAr ? new Rect((1 - boxAr / ar) / 2, 0, boxAr / ar, 1) : new Rect(0, (1 - ar / boxAr) / 2, 1, ar / boxAr);
            }
            flareText.text = n.FlaresDone || n.LatestFlare != null ? n.FlareSummary() : "Fetching NASA DONKI space-weather data...";
        }

        public void Show(bool on)
        {
            gameObject.SetActive(true);
            group.blocksRaycasts = on;
            if (!on) { Tween.Fade(group, 0, 0.6f, () => gameObject.SetActive(false)); return; }
            group.alpha = 1;
            for (int i = 0; i < stagger.Count; i++)
            {
                var g = stagger[i]; float d = i * 0.07f; g.alpha = 0;
                var rt = (RectTransform)g.transform; var basePos = rt.anchoredPosition;
                Tween.Run(0.7f + d, t => { float k = Mathf.Clamp01((t * (0.7f + d) - d) / 0.7f); if (g) { g.alpha = k; rt.anchoredPosition = basePos + new Vector2((1 - Tween.EaseOut(k)) * -40, 0); } });
            }
        }

        void ClearSide() { foreach (Transform c in sideRT) Destroy(c.gameObject); }

        void ShowChapters()
        {
            ClearSide();
            var bg = UIX.Panel(sideRT, "Bg", new Color(0.03f, 0.045f, 0.07f, 0.85f)); bg.rectTransform.Fill();
            var h = UIX.Text(sideRT, "CHAPTER SELECT", 16, Pal.Accent, R.Bold); h.characterSpacing = 8;
            h.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(28, -22), new Vector2(-56, 24));
            string[] names = { "PROLOGUE  ·  The Crash (intro film)", "ACT I  ·  Scavenge & Observatory", "ACT II  ·  Life Support & Handbook", "ACT III  ·  Recover the Modules", "FINALE  ·  Launch Window" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var b = UIX.Button(sideRT, names[i], () => OnStart?.Invoke(idx), 500, 58, 20, Pal.Cyan);
                ((RectTransform)b.transform).Set(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -62 - i * 68), new Vector2(500, 58));
                var t = b.GetComponentInChildren<TextMeshProUGUI>(); t.alignment = TextAlignmentOptions.MidlineLeft; t.rectTransform.offsetMin = new Vector2(26, 0); t.characterSpacing = 1;
            }
            OpenSide();
        }

        void ShowControls()
        {
            ClearSide();
            var bg = UIX.Panel(sideRT, "Bg", new Color(0.03f, 0.045f, 0.07f, 0.85f)); bg.rectTransform.Fill();
            var h = UIX.Text(sideRT, "CONTROLS", 16, Pal.Accent, R.Bold); h.characterSpacing = 8;
            h.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(28, -22), new Vector2(-56, 24));
            string[,] rows = { { "W A S D", "Move" }, { "MOUSE", "Look around" }, { "SHIFT", "Sprint (uses more O<sub>2</sub>)" }, { "SPACE", "Jump — Mars gravity is 38% of Earth's!" }, { "E", "Interact  (hold for some actions)" }, { "H", "Open the Field Handbook" }, { "1 – 4", "Answer calibration questions" }, { "ESC", "Pause" } };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                var k = UIX.Panel(sideRT, "Key", new Color(1, 1, 1, 0.1f)); k.raycastTarget = false;
                k.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -60 - i * 44), new Vector2(120, 36));
                var kt = UIX.Text(k.transform, rows[i, 0], 17, Pal.Text, R.Bold, TextAlignmentOptions.Center); kt.rectTransform.Fill();
                var d = UIX.Text(sideRT, rows[i, 1], 21, new Color(0.85f, 0.88f, 0.92f), R.Body, TextAlignmentOptions.MidlineLeft);
                d.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(166, -60 - i * 44), new Vector2(-190, 36));
            }
            OpenSide();
        }

        void OpenSide()
        {
            side.blocksRaycasts = true; side.alpha = 0;
            Tween.Fade(side, 1, 0.3f);
            var basePos = new Vector2(580, -170);
            Tween.Run(0.4f, t => sideRT.anchoredPosition = basePos + new Vector2((1 - Tween.EaseOut(t)) * 30, 0));
        }
    }

    // ======================================================================
    public class PauseUI : MonoBehaviour
    {
        CanvasGroup group; public bool IsOpen; TextMeshProUGUI sens;
        public Action OnRestart, OnMenu;

        public static PauseUI Create() { var g = new GameObject("PauseUI"); var p = g.AddComponent<PauseUI>(); p.Build(); return p; }

        void Build()
        {
            var cv = gameObject.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 80;
            var cs = gameObject.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
            var root = (RectTransform)transform;
            UIX.Img(root, "Dim", new Color(0, 0, 0, 0.7f), null, true).rectTransform.Fill();
            var p = UIX.Panel(root, "Panel", new Color(0.03f, 0.045f, 0.07f, 0.92f)); p.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 560));
            UIX.Corners(p.transform, Pal.Accent, 18);
            var t = UIX.Text(p.transform, "PAUSED", 40, Pal.Text, R.Title, TextAlignmentOptions.Center); t.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(0, 56));
            string[] l = { "RESUME", "FIELD HANDBOOK", "RESTART MISSION", "MAIN MENU" };
            Action[] a = { Close, () => { if (Game.HasHandbook) { Close(); HandbookUI.I.Open(); } else Sfx.Play("error", 0.5f); }, () => { Close(); OnRestart?.Invoke(); }, () => { Close(); OnMenu?.Invoke(); } };
            for (int i = 0; i < l.Length; i++)
            {
                var b = UIX.Button(p.transform, l[i], a[i], 400, 58, 22);
                ((RectTransform)b.transform).Set(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120 - i * 72), new Vector2(400, 58));
            }
            var sl = UIX.Text(p.transform, "MOUSE SENSITIVITY", 16, Pal.Dim, R.Bold, TextAlignmentOptions.Center); sl.characterSpacing = 6;
            sl.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 92), new Vector2(0, 24));
            var minus = UIX.Button(p.transform, "–", () => SetSens(-0.25f), 60, 46, 26);
            ((RectTransform)minus.transform).Set(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-110, 36), new Vector2(60, 46));
            var plus = UIX.Button(p.transform, "+", () => SetSens(0.25f), 60, 46, 26);
            ((RectTransform)plus.transform).Set(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(110, 36), new Vector2(60, 46));
            sens = UIX.Text(p.transform, "", 26, Pal.Text, R.Mono, TextAlignmentOptions.Center);
            sens.rectTransform.Set(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(140, 46));
            SetSens(0);
        }

        void SetSens(float d) { PlayerController.Sensitivity = Mathf.Clamp(PlayerController.Sensitivity + d, 0.5f, 6f); sens.text = PlayerController.Sensitivity.ToString("0.00"); }

        public void Open() { if (IsOpen) return; IsOpen = true; UIState.Push(); group.blocksRaycasts = true; Tween.Fade(group, 1, 0.2f); Time.timeScale = 0; }
        public void Close() { if (!IsOpen) return; IsOpen = false; UIState.Pop(); group.blocksRaycasts = false; Tween.Fade(group, 0, 0.2f); Time.timeScale = 1; }
    }

    // ======================================================================
    public class ResultsUI : MonoBehaviour
    {
        public static IEnumerator<object> Dummy() { yield break; }

        public static GameObject Show(int correctFirst, int total, int mistakes, float dose, Action onMenu, Action onAgain)
        {
            var g = new GameObject("Results");
            var cv = g.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 90;
            var cs = g.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
            g.AddComponent<GraphicRaycaster>();
            var grp = g.AddComponent<CanvasGroup>(); grp.alpha = 0;
            var root = (RectTransform)g.transform;
            UIX.Img(root, "Dim", new Color(0, 0, 0, 0.55f), UISprites.Vignette).rectTransform.Fill();
            var small = UIX.Text(root, "TRANSFER ORBIT ACHIEVED  ·  ETA EARTH: 7 MONTHS", 20, Pal.Accent, R.Bold, TextAlignmentOptions.Center); small.characterSpacing = 10;
            small.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(1600, 30));
            var big = UIX.Text(root, "MISSION COMPLETE", 84, Pal.Text, R.Title, TextAlignmentOptions.Center);
            big.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(1700, 110));
            var sub = UIX.Text(root, "You survived on Mars using science — and you are going home.", 28, new Color(0.85f, 0.88f, 0.92f), R.Body, TextAlignmentOptions.Center);
            sub.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(1600, 40));

            string[,] stats = { { $"{correctFirst}/{total}", "ANSWERED FIRST TRY" }, { mistakes.ToString(), "CALIBRATION RETRIES" }, { "780", "DAYS ON MARS" }, { $"{dose:0}", "RADIATION DOSE (mSv)" } };
            for (int i = 0; i < 4; i++)
            {
                var p = UIX.Panel(root, "Stat", new Color(0.03f, 0.045f, 0.07f, 0.85f), true, new Color(1, 0.48f, 0.22f, 0.3f));
                p.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2((i - 1.5f) * 300, -10), new Vector2(270, 160));
                var v = UIX.Text(p.transform, stats[i, 0], 54, Pal.Text, R.Title, TextAlignmentOptions.Center); v.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(0, 70));
                var l = UIX.Text(p.transform, stats[i, 1], 15, Pal.Accent, R.Bold, TextAlignmentOptions.Center); l.characterSpacing = 4; l.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(-20, 24));
            }
            string rank = correctFirst >= total ? "RANK: FLIGHT DIRECTOR" : correctFirst >= total - 3 ? "RANK: MISSION SPECIALIST" : "RANK: CADET — READ THE HANDBOOK AGAIN!";
            var rk = UIX.Text(root, rank, 26, Pal.Good, R.Bold, TextAlignmentOptions.Center); rk.characterSpacing = 8;
            rk.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, -140), new Vector2(1400, 40));
            var b1 = UIX.Button(root, "PLAY AGAIN", onAgain, 300, 60, 22);
            ((RectTransform)b1.transform).At(new Vector2(0.5f, 0.5f), new Vector2(-170, -250), new Vector2(300, 60));
            var b2 = UIX.Button(root, "MAIN MENU", onMenu, 300, 60, 22, Pal.Cyan);
            ((RectTransform)b2.transform).At(new Vector2(0.5f, 0.5f), new Vector2(170, -250), new Vector2(300, 60));
            var cred = UIX.Text(root, "Made for the NASA Space Apps Challenge 2026  ·  Science facts from NASA mission data  ·  NASA 3D models & textures: NASA 3D Resources", 16, Pal.Dim, R.Body, TextAlignmentOptions.Center);
            cred.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1700, 26));
            Tween.Fade(grp, 1, 1.2f);
            return g;
        }
    }
}
