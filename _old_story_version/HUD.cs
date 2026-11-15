using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    public class Marker
    {
        public Vector3 Pos; public Transform Follow; public string Label; public Color Color; public bool Active = true;
        public RectTransform Compass, World; public TextMeshProUGUI CompassDist, WorldLabel;
        public Vector3 P => Follow ? Follow.position : Pos;
    }

    public class HUD : MonoBehaviour
    {
        public Canvas Canvas;
        RectTransform root;
        CanvasGroup hudGroup, vitalsGroup, objGroup, clockGroup, checklistGroup, subtitleGroup, warnGroup, factGroup, promptGroup, hintGroup;
        TextMeshProUGUI objTitle, objText, objProgress, clockSol, clockDays, clockLabel, subSpeaker, subText, warnText, factTitle, factBody, promptLabel, promptKey, checklistTitle, hintText;
        Image o2Bar, radBar, tempBar, crosshair, holdRing;
        TextMeshProUGUI o2Val, radVal, tempVal;
        Image visor, vignette, crack, frost, radPulse, burn, lowO2;
        RectTransform compassStrip, markerLayer, toastLayer, checklistItems;
        readonly List<(RectTransform rt, float ang)> compassTicks = new List<(RectTransform, float)>();
        readonly List<Marker> markers = new List<Marker>();
        Coroutine subCo, factCo, warnCo;
        public bool VisorOn = true, Cracked;
        float heartbeatT;

        public static HUD Create()
        {
            var g = new GameObject("HUD");
            var h = g.AddComponent<HUD>();
            h.Build();
            return h;
        }

        void Build()
        {
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay; Canvas.sortingOrder = 10;
            var cs = gameObject.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            root = (RectTransform)transform;

            // ---------- overlays ----------
            var ov = UIX.Go("Overlays", root).RT(); ov.Fill();
            vignette = Full(ov, "Vignette", new Color(0, 0, 0, 0.38f), UISprites.Vignette);
            burn = Full(ov, "Burn", new Color(1f, 0.35f, 0.1f, 0), UISprites.Vignette);
            radPulse = Full(ov, "RadPulse", new Color(1f, 0.05f, 0.05f, 0), UISprites.Vignette);
            lowO2 = Full(ov, "LowO2", new Color(0, 0, 0, 0), UISprites.Vignette);
            frost = Full(ov, "Frost", new Color(0.85f, 0.95f, 1f, 0), UISprites.Frost);
            crack = Full(ov, "Crack", new Color(1, 1, 1, 0), UISprites.Crack);
            visor = Full(ov, "Visor", new Color(0.015f, 0.018f, 0.025f, 0.92f), UISprites.Visor);

            hudGroup = UIX.Go("HUDRoot", root).AddComponent<CanvasGroup>(); ((RectTransform)hudGroup.transform).Fill();
            var hr = (RectTransform)hudGroup.transform;

            // ---------- clock (top-left) ----------
            clockGroup = UIX.Go("Clock", hr).AddComponent<CanvasGroup>();
            ((RectTransform)clockGroup.transform).At(new Vector2(0, 1), new Vector2(60, -48), new Vector2(520, 110));
            ((RectTransform)clockGroup.transform).pivot = new Vector2(0, 1);
            clockSol = UIX.Text(clockGroup.transform, "SOL 1  ·  JEZERO CRATER, MARS", 18, Pal.Dim, R.Mono);
            clockSol.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), Vector2.zero, new Vector2(0, 24));
            clockLabel = UIX.Text(clockGroup.transform, "NEXT LAUNCH WINDOW", 15, Pal.Accent, R.Bold);
            clockLabel.characterSpacing = 8;
            clockLabel.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -30), new Vector2(0, 22));
            clockDays = UIX.Text(clockGroup.transform, "— DAYS", 36, Pal.Text, R.Title);
            clockDays.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -52), new Vector2(0, 48));
            clockGroup.alpha = 0;

            // ---------- objective ----------
            objGroup = UIX.Go("Objective", hr).AddComponent<CanvasGroup>();
            var oRT = (RectTransform)objGroup.transform; oRT.At(new Vector2(0, 1), new Vector2(60, -190), new Vector2(470, 120)); oRT.pivot = new Vector2(0, 1);
            var oBg = UIX.Img(oRT, "Bg", new Color(0.02f, 0.03f, 0.05f, 0.55f), UISprites.HGradient); oBg.rectTransform.Fill(); oBg.rectTransform.offsetMin = new Vector2(-14, -10); oBg.rectTransform.offsetMax = new Vector2(60, 8);
            var bar = UIX.Img(oRT, "Bar", Pal.Accent, null); bar.rectTransform.Set(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(-14, -1), new Vector2(3, -2));
            objTitle = UIX.Text(oRT, "OBJECTIVE", 15, Pal.Accent, R.Bold); objTitle.characterSpacing = 8;
            objTitle.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), Vector2.zero, new Vector2(0, 22));
            objText = UIX.Text(oRT, "", 27, Pal.Text, R.Body);
            objText.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -24), new Vector2(0, 70));
            objProgress = UIX.Text(oRT, "", 20, Pal.Cyan, R.Mono);
            objProgress.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -90), new Vector2(0, 26));
            objGroup.alpha = 0;

            // ---------- compass ----------
            var comp = UIX.Go("Compass", hr).RT(); comp.At(new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(760, 64));
            var compBg = UIX.Img(comp, "Line", new Color(1, 1, 1, 0.18f)); compBg.rectTransform.Set(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(0, 1));
            var center = UIX.Img(comp, "Center", Pal.Accent); center.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(2, 14));
            comp.gameObject.AddComponent<RectMask2D>();
            compassStrip = UIX.Go("Strip", comp).RT(); compassStrip.Fill();
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            for (int a = 0; a < 360; a += 15)
            {
                bool card = a % 45 == 0;
                var t = UIX.Text(compassStrip, card ? names[a / 45] : "|", card ? (a % 90 == 0 ? 22 : 16) : 12, card && a % 90 == 0 ? Pal.Text : Pal.Dim, card ? R.Bold : R.Mono, TextAlignmentOptions.Center);
                t.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, card ? -6 : -4), new Vector2(60, 26));
                compassTicks.Add((t.rectTransform, a));
            }
            markerLayer = UIX.Go("Markers", hr).RT(); markerLayer.Fill();

            // ---------- vitals ----------
            vitalsGroup = UIX.Go("Vitals", hr).AddComponent<CanvasGroup>();
            var vRT = (RectTransform)vitalsGroup.transform; vRT.At(new Vector2(0, 0), new Vector2(60, 56), new Vector2(380, 132)); vRT.pivot = new Vector2(0, 0);
            var vBg = UIX.Img(vRT, "Bg", new Color(0.02f, 0.03f, 0.05f, 0.5f), UISprites.HGradient); vBg.rectTransform.Fill(); vBg.rectTransform.offsetMin = new Vector2(-16, -12); vBg.rectTransform.offsetMax = new Vector2(80, 12);
            o2Bar = Vital(vRT, 0, "O<sub>2</sub>", Pal.Cyan, out o2Val);
            radBar = Vital(vRT, 1, "RAD", Pal.Warn, out radVal);
            tempBar = Vital(vRT, 2, "TEMP", new Color(0.55f, 0.75f, 1f), out tempVal);
            vitalsGroup.alpha = 0;

            // ---------- checklist ----------
            checklistGroup = UIX.Go("Checklist", hr).AddComponent<CanvasGroup>();
            var cRT = (RectTransform)checklistGroup.transform; cRT.At(new Vector2(1, 1), new Vector2(-50, -48), new Vector2(400, 420)); cRT.pivot = new Vector2(1, 1);
            var cBg = UIX.Panel(cRT, "Bg", new Color(0.03f, 0.04f, 0.06f, 0.62f)); cBg.raycastTarget = false; cBg.rectTransform.Fill();
            checklistTitle = UIX.Text(cRT, "MISSION CHECKLIST", 15, Pal.Accent, R.Bold); checklistTitle.characterSpacing = 6;
            checklistTitle.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -16), new Vector2(-40, 22));
            checklistItems = UIX.Go("Items", cRT).RT(); checklistItems.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -48), new Vector2(-40, 360));
            checklistGroup.alpha = 0;

            // ---------- crosshair + prompt ----------
            crosshair = UIX.Img(hr, "Crosshair", new Color(1, 1, 1, 0.75f), UISprites.Circle); crosshair.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));
            promptGroup = UIX.Go("Prompt", hr).AddComponent<CanvasGroup>();
            var pRT = (RectTransform)promptGroup.transform; pRT.At(new Vector2(0.5f, 0.5f), new Vector2(0, -96), new Vector2(600, 56));
            var keyBg = UIX.Panel(pRT, "Key", new Color(0.95f, 0.95f, 0.97f, 0.95f), false); keyBg.raycastTarget = false;
            keyBg.rectTransform.Set(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(44, 44));
            holdRing = UIX.Img(keyBg.transform, "Hold", Pal.Accent, UISprites.Ring); holdRing.rectTransform.Fill(-10);
            holdRing.type = Image.Type.Filled; holdRing.fillMethod = Image.FillMethod.Radial360; holdRing.fillOrigin = 2; holdRing.fillAmount = 0;
            promptKey = UIX.Text(keyBg.transform, "E", 24, new Color(0.05f, 0.06f, 0.08f), R.Bold, TextAlignmentOptions.Center); promptKey.rectTransform.Fill();
            promptLabel = UIX.Text(pRT, "", 26, Pal.Text, R.Bold, TextAlignmentOptions.MidlineLeft);
            promptLabel.rectTransform.Set(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(560, 44));
            promptLabel.enableWordWrapping = false;
            promptGroup.alpha = 0;

            // ---------- subtitles ----------
            subtitleGroup = UIX.Go("Subtitles", hr).AddComponent<CanvasGroup>();
            var sRT = (RectTransform)subtitleGroup.transform; sRT.At(new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(1200, 110));
            var sBg = UIX.Img(sRT, "Bg", new Color(0, 0, 0, 0.55f), UISprites.Glow); sBg.rectTransform.Fill(); sBg.rectTransform.offsetMin = new Vector2(-120, -50); sBg.rectTransform.offsetMax = new Vector2(120, 40);
            subSpeaker = UIX.Text(sRT, "", 17, Pal.Accent, R.Bold, TextAlignmentOptions.Top); subSpeaker.characterSpacing = 8;
            subSpeaker.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 24));
            subText = UIX.Text(sRT, "", 31, Pal.Text, R.Body, TextAlignmentOptions.Top);
            subText.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(0, 80));
            subtitleGroup.alpha = 0;

            // ---------- warning banner ----------
            warnGroup = UIX.Go("Warning", hr).AddComponent<CanvasGroup>();
            var wRT = (RectTransform)warnGroup.transform; wRT.At(new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(760, 60));
            var wBg = UIX.Panel(wRT, "Bg", new Color(0.35f, 0.03f, 0.02f, 0.75f), true, new Color(1, 0.3f, 0.2f, 0.7f)); wBg.raycastTarget = false; wBg.rectTransform.Fill();
            warnText = UIX.Text(wRT, "", 26, new Color(1, 0.85f, 0.8f), R.Title, TextAlignmentOptions.Center); warnText.rectTransform.Fill(); warnText.characterSpacing = 6;
            warnGroup.alpha = 0;

            // ---------- fact card ----------
            factGroup = UIX.Go("Fact", hr).AddComponent<CanvasGroup>();
            var fRT = (RectTransform)factGroup.transform; fRT.At(new Vector2(1, 0.5f), new Vector2(-50, -40), new Vector2(440, 250)); fRT.pivot = new Vector2(1, 0.5f);
            var fBg = UIX.Panel(fRT, "Bg", new Color(0.03f, 0.05f, 0.08f, 0.86f), true, new Color(0.37f, 0.89f, 0.95f, 0.35f)); fBg.raycastTarget = false; fBg.rectTransform.Fill();
            UIX.Corners(fRT, Pal.Cyan, 16);
            var fHead = UIX.Text(fRT, "DID YOU KNOW?", 15, Pal.Cyan, R.Bold); fHead.characterSpacing = 8;
            fHead.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -20), new Vector2(-48, 22));
            factTitle = UIX.Text(fRT, "", 26, Pal.Text, R.Bold);
            factTitle.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -46), new Vector2(-48, 34));
            factBody = UIX.Text(fRT, "", 21, new Color(0.82f, 0.87f, 0.92f), R.Body);
            factBody.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -84), new Vector2(-48, 160));
            factBody.lineSpacing = -8;
            factGroup.alpha = 0;

            // ---------- toasts + hints ----------
            toastLayer = UIX.Go("Toasts", hr).RT(); toastLayer.At(new Vector2(1, 0), new Vector2(-50, 230), new Vector2(380, 300)); toastLayer.pivot = new Vector2(1, 0);
            hintGroup = UIX.Go("Hints", hr).AddComponent<CanvasGroup>();
            var hRT = (RectTransform)hintGroup.transform; hRT.At(new Vector2(1, 0), new Vector2(-50, 40), new Vector2(600, 30)); hRT.pivot = new Vector2(1, 0);
            hintText = UIX.Text(hRT, "", 18, Pal.Dim, R.Bold, TextAlignmentOptions.Right); hintText.rectTransform.Fill(); hintText.characterSpacing = 3;
            hintGroup.alpha = 0;
        }

        Image Full(RectTransform p, string n, Color c, Sprite s) { var i = UIX.Img(p, n, c, s); i.rectTransform.Fill(); i.type = Image.Type.Simple; return i; }

        Image Vital(RectTransform p, int row, string label, Color c, out TextMeshProUGUI val)
        {
            float y = -row * 44;
            var l = UIX.Text(p, label, 18, c, R.Bold); l.characterSpacing = 4;
            l.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(70, 26));
            var bg = UIX.Img(p, label + "BarBg", new Color(1, 1, 1, 0.1f), UISprites.Round);
            bg.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(72, y - 9), new Vector2(190, 8));
            var fill = UIX.Img(bg.transform, "Fill", c, UISprites.Round);
            fill.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            val = UIX.Text(p, "", 19, Pal.Text, R.Mono, TextAlignmentOptions.Left);
            val.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(274, y + 1), new Vector2(140, 26));
            val.enableWordWrapping = false;
            return fill;
        }

        static void SetFill(Image fill, float v)
        {
            v = Mathf.Clamp01(v);
            fill.rectTransform.anchorMax = new Vector2(v, 1);
            fill.enabled = v > 0.01f;
        }

        // ================= API =================
        public void ShowHUD(bool on, float dur = 0.6f) => Tween.Fade(hudGroup, on ? 1 : 0, dur);
        public void ShowVitals(bool on) => Tween.Fade(vitalsGroup, on ? 1 : 0, 0.6f);
        public void ShowClock(bool on) => Tween.Fade(clockGroup, on ? 1 : 0, 0.6f);
        public void ShowChecklist(bool on) => Tween.Fade(checklistGroup, on ? 1 : 0, 0.6f);
        public void SetHints(string s) { hintText.text = s; Tween.Fade(hintGroup, string.IsNullOrEmpty(s) ? 0 : 1, 0.5f); }
        public void SetSol(string s) => clockSol.text = s;

        public void SetClock(int days, string label = "NEXT LAUNCH WINDOW")
        {
            clockLabel.text = label;
            clockDays.text = days <= 0 ? "<color=#5CF28C>OPEN</color>" : $"{days} <size=60%>DAYS</size>";
        }

        public void SetObjective(string text, string progress = null, string title = "OBJECTIVE")
        {
            StartCoroutine(ObjCo(text, progress, title));
        }
        IEnumerator ObjCo(string text, string progress, string title)
        {
            if (objGroup.alpha > 0.01f) { Tween.Fade(objGroup, 0, 0.25f); yield return new WaitForSecondsRealtime(0.28f); }
            objTitle.text = title; objText.text = text; objProgress.text = progress ?? "";
            var rt = (RectTransform)objGroup.transform;
            Sfx.Play("notify", 0.6f);
            Tween.Run(0.5f, t => rt.anchoredPosition = new Vector2(Mathf.Lerp(20, 60, Tween.EaseOut(t)), -190));
            Tween.Fade(objGroup, 1, 0.4f);
        }
        public void SetProgress(string p) { objProgress.text = p; Tween.Run(0.3f, t => objProgress.transform.localScale = Vector3.one * (1 + 0.25f * (1 - t))); }
        public void ClearObjective() => Tween.Fade(objGroup, 0, 0.4f);

        public void SetChecklist(IList<(string text, bool done)> items, string title)
        {
            foreach (Transform c in checklistItems) Destroy(c.gameObject);
            checklistTitle.text = title;
            for (int i = 0; i < items.Count; i++)
            {
                float y = -i * 30;
                var box = UIX.Img(checklistItems, "Box", items[i].done ? Pal.Good : new Color(1, 1, 1, 0.25f), items[i].done ? UISprites.Check : UISprites.RoundOutline);
                box.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y - 3), new Vector2(20, 20));
                var t = UIX.Text(checklistItems, items[i].text, 19, items[i].done ? new Color(0.7f, 0.9f, 0.75f) : Pal.Text, R.Body);
                t.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(30, y), new Vector2(-30, 26));
                t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
            }
            var rt = (RectTransform)checklistGroup.transform;
            rt.sizeDelta = new Vector2(400, 66 + items.Count * 30);
        }

        public IEnumerator Say(string speaker, string text, float hold = 2.2f)
        {
            if (subCo != null) StopCoroutine(subCo);
            yield return subCo = StartCoroutine(SayCo(speaker, text, hold));
        }
        public void SayAsync(string speaker, string text, float hold = 2.5f)
        {
            if (subCo != null) StopCoroutine(subCo);
            subCo = StartCoroutine(SayCo(speaker, text, hold));
        }
        IEnumerator SayCo(string speaker, string text, float hold)
        {
            subSpeaker.text = speaker; subText.text = text; subText.maxVisibleCharacters = 0;
            subtitleGroup.alpha = 1;
            subText.ForceMeshUpdate();
            int n = subText.textInfo.characterCount;
            for (int i = 0; i <= n; i++)
            {
                subText.maxVisibleCharacters = i;
                if (i % 3 == 0 && i < n) Sfx.Play("type", 0.25f, Random.Range(0.9f, 1.15f));
                if (Input.GetKey(KeyCode.Return)) { subText.maxVisibleCharacters = n; break; }
                yield return new WaitForSeconds(0.022f);
            }
            float t = 0;
            while (t < hold + text.Length * 0.025f) { t += Time.deltaTime; if (Input.GetKeyDown(KeyCode.Return)) break; yield return null; }
            float a = 1; while (a > 0) { a -= Time.deltaTime * 3; subtitleGroup.alpha = a; yield return null; }
            subtitleGroup.alpha = 0; subCo = null;
        }

        public void Fact(string title, string body, float dur = 10f)
        {
            if (factCo != null) StopCoroutine(factCo);
            factCo = StartCoroutine(FactCo(title, body, dur));
        }
        IEnumerator FactCo(string title, string body, float dur)
        {
            factTitle.text = title; factBody.text = body;
            factBody.ForceMeshUpdate();
            var rt = (RectTransform)factGroup.transform;
            rt.sizeDelta = new Vector2(440, Mathf.Max(170, 110 + factBody.preferredHeight));
            Sfx.Play("beep", 0.5f);
            Tween.Run(0.5f, t => rt.anchoredPosition = new Vector2(Mathf.Lerp(40, -50, Tween.EaseOut(t)), -40));
            Tween.Fade(factGroup, 1, 0.4f);
            yield return new WaitForSeconds(dur);
            Tween.Fade(factGroup, 0, 0.6f);
        }

        public void Warn(string text, float dur = 4f)
        {
            if (warnCo != null) StopCoroutine(warnCo);
            warnCo = StartCoroutine(WarnCo(text, dur));
        }
        IEnumerator WarnCo(string text, float dur)
        {
            warnText.text = text; float t = 0;
            while (t < dur) { t += Time.deltaTime; warnGroup.alpha = 0.65f + 0.35f * Mathf.Sin(t * 8); yield return null; }
            Tween.Fade(warnGroup, 0, 0.5f);
        }

        public void Toast(string text, Color c)
        {
            var p = UIX.Panel(toastLayer, "Toast", new Color(0.03f, 0.04f, 0.06f, 0.85f), true, new Color(c.r, c.g, c.b, 0.5f));
            p.raycastTarget = false;
            var rt = p.rectTransform; rt.Set(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(360, 48));
            var dot = UIX.Img(rt, "Dot", c, UISprites.Diamond); dot.rectTransform.At(new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(18, 18));
            var t = UIX.Text(rt, text, 22, Pal.Text, R.Bold, TextAlignmentOptions.MidlineLeft); t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(46, 0);
            foreach (RectTransform other in toastLayer) if (other != rt) other.anchoredPosition += new Vector2(0, 56);
            var g = p.Group(); g.alpha = 0;
            Tween.Run(0.35f, k => { if (rt) { rt.anchoredPosition = new Vector2(Mathf.Lerp(60, 0, Tween.EaseOut(k)), rt.anchoredPosition.y); g.alpha = k; } });
            StartCoroutine(KillToast(p.gameObject, g));
        }
        IEnumerator KillToast(GameObject go, CanvasGroup g)
        {
            yield return new WaitForSeconds(3.5f);
            if (g) Tween.Fade(g, 0, 0.5f);
            yield return new WaitForSeconds(0.6f);
            if (go) Destroy(go);
        }

        public Marker AddMarker(Vector3 pos, string label, Color c, Transform follow = null)
        {
            var m = new Marker { Pos = pos, Follow = follow, Label = label, Color = c };
            var ci = UIX.Img(compassStrip, "M", c, UISprites.Diamond); m.Compass = ci.rectTransform; m.Compass.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
            m.CompassDist = UIX.Text(m.Compass, "", 14, c, R.Mono, TextAlignmentOptions.Center); m.CompassDist.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0, -14), new Vector2(80, 18));
            var wi = UIX.Img(markerLayer, "WM", c, UISprites.Diamond); m.World = wi.rectTransform; m.World.At(new Vector2(0, 0), Vector2.zero, new Vector2(22, 22));
            m.WorldLabel = UIX.Text(m.World, "", 17, Pal.Text, R.Bold, TextAlignmentOptions.Center); m.WorldLabel.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, 26), new Vector2(300, 24));
            m.WorldLabel.enableWordWrapping = false;
            markers.Add(m);
            return m;
        }
        public void RemoveMarker(Marker m)
        {
            if (m == null) return;
            markers.Remove(m);
            if (m.Compass) Destroy(m.Compass.gameObject);
            if (m.World) Destroy(m.World.gameObject);
        }
        public void ClearMarkers() { foreach (var m in markers.ToArray()) RemoveMarker(m); }

        public void SetCracked(bool on) { Cracked = on; }

        void Update()
        {
            var p = Game.Player; var suit = Game.Suit;
            if (!p) return;
            var cam = p.Cam;
            float yaw = cam.transform.eulerAngles.y;

            // compass
            const float pxPerDeg = 760f / 130f;
            foreach (var (rt, ang) in compassTicks)
            {
                float d = Mathf.DeltaAngle(yaw, ang);
                rt.anchoredPosition = new Vector2(d * pxPerDeg, rt.anchoredPosition.y);
                rt.gameObject.SetActive(Mathf.Abs(d) < 68);
            }
            Vector2 scale = new Vector2(1920f / Screen.width, 1080f / Screen.height);
            float s = Mathf.Lerp(scale.x, scale.y, 0.5f);
            // log-space match like CanvasScaler with match 0.5
            s = Mathf.Pow(2, Mathf.Lerp(Mathf.Log(1920f / Screen.width, 2), Mathf.Log(1080f / Screen.height, 2), 0.5f));
            foreach (var m in markers)
            {
                var to = m.P - cam.transform.position; to.y = 0;
                float ang = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float d = Mathf.DeltaAngle(yaw, ang);
                float dist = Vector3.Distance(m.P, cam.transform.position);
                m.Compass.anchoredPosition = new Vector2(Mathf.Clamp(d, -64, 64) * pxPerDeg, 18);
                m.CompassDist.text = $"{dist:0}m";
                m.Compass.gameObject.SetActive(m.Active);
                var sp = cam.WorldToScreenPoint(m.P + Vector3.up * 1.5f);
                bool vis = m.Active && sp.z > 0 && dist > 4 && !UIState.Modal;
                m.World.gameObject.SetActive(vis);
                if (vis)
                {
                    m.World.anchoredPosition = new Vector2(sp.x * s, sp.y * s);
                    m.WorldLabel.text = dist < 60 ? $"{m.Label}  <color=#9AA8B5>{dist:0}m</color>" : $"<color=#9AA8B5>{dist:0}m</color>";
                    float pulse = 1 + 0.12f * Mathf.Sin(Time.time * 4);
                    m.World.localScale = Vector3.one * pulse;
                }
            }

            // prompt
            var it = Game.Interactor ? Game.Interactor.Current : null;
            float pa = it ? 1 : 0;
            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, pa, Time.deltaTime * 8);
            if (it)
            {
                promptLabel.text = it.HoldTime > 0 ? $"<color=#9AA8B5>HOLD</color>  {it.Verb} {it.Label}" : $"{it.Verb} {it.Label}";
                holdRing.fillAmount = Game.Interactor.HoldProgress;
            }
            crosshair.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(crosshair.rectTransform.sizeDelta.x, it ? 14 : 6, Time.deltaTime * 12);
            crosshair.color = it ? Pal.Accent : new Color(1, 1, 1, 0.7f);
            crosshair.enabled = !UIState.Cinematic;

            // vitals + overlays
            if (suit)
            {
                SetFill(o2Bar, suit.O2 / 100f);
                o2Bar.color = suit.O2 < 25 ? Color.Lerp(Pal.Bad, Pal.Cyan, Mathf.PingPong(Time.time * 2, 1)) : Pal.Cyan;
                o2Val.text = $"{suit.O2:0}%";
                SetFill(radBar, Mathf.Max(suit.Sickness / 100f, suit.SolarEvent));
                radBar.color = Color.Lerp(Pal.Warn, Pal.Bad, Mathf.Max(suit.SolarEvent, suit.Sickness / 100f));
                radVal.text = suit.DoseRate >= 1 ? $"<color=#FF5A4D>{suit.DoseRate:0} mSv/h</color>" : $"{suit.DoseRate * 24.6f:0.00} mSv/sol";
                float temp = suit.Outside && Game.Sky ? Game.Sky.OutsideTempC : 19f;
                SetFill(tempBar, Mathf.InverseLerp(-90, 25, temp));
                tempVal.text = suit.Outside ? $"{temp:0}°C <size=70%>OUT</size>" : $"{temp:0}°C <size=70%>IN</size>";

                radPulse.color = new Color(1, 0.05f, 0.05f, (suit.SolarEvent * 0.55f + suit.Sickness / 100f * 0.25f) * (0.6f + 0.4f * Mathf.Sin(Time.time * 5)));
                burn.color = new Color(1f, 0.35f, 0.1f, suit.Burns * 0.35f);
                frost.color = new Color(0.85f, 0.95f, 1f, suit.Cold * 0.85f);
                float lo = Mathf.Clamp01((25 - suit.O2) / 25f);
                lowO2.color = new Color(0, 0, 0, lo * 0.8f);
                if (lo > 0 && suit.Active) { heartbeatT -= Time.deltaTime; if (heartbeatT <= 0) { heartbeatT = Mathf.Lerp(1.1f, 0.6f, lo); Sfx.Play("heartbeat", 0.6f * lo + 0.2f); } }
            }
            float va = VisorOn ? 0.92f : 0f;
            visor.color = new Color(visor.color.r, visor.color.g, visor.color.b, Mathf.MoveTowards(visor.color.a, va, Time.deltaTime));
            crack.color = new Color(1, 1, 1, Mathf.MoveTowards(crack.color.a, Cracked && VisorOn ? 0.2f : 0, Time.deltaTime));
        }
    }
}
