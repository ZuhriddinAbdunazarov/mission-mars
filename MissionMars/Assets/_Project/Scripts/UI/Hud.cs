using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    /// <summary>Visual language for the in-game helmet HUD: dark glass, thin lines, cyan data, amber warnings.</summary>
    public static class HC
    {
        public static readonly Color Cyan = new Color(0.45f, 0.9f, 1f);
        public static readonly Color Amber = new Color(1f, 0.68f, 0.25f);
        public static readonly Color Red = new Color(1f, 0.3f, 0.28f);
        public static readonly Color Green = new Color(0.4f, 1f, 0.6f);
        public static readonly Color Glass = new Color(0.015f, 0.03f, 0.045f, 0.62f);
        public static readonly Color Line = new Color(0.6f, 0.9f, 1f, 0.16f);
        public static readonly Color Text = new Color(0.92f, 0.96f, 1f);
        public static readonly Color Dim = new Color(0.62f, 0.72f, 0.8f);
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    public class Waypoint { public string Label; public System.Func<Vector3> Pos; public Color Color; public RectTransform Rt; public TextMeshProUGUI Txt; public Image Icon; public bool On = true; }

    public class Hud : MonoBehaviour
    {
        public Canvas Canvas; public RectTransform Root;
        CanvasGroup group;
        // vitals
        class Bar { public RectTransform Fill; public Image FillImg; public TextMeshProUGUI Val; public Image Icon; public float Shown = 1; public Color Col; public CanvasGroup G; }
        Bar o2, water, health, rad;
        // objective
        TextMeshProUGUI objTitle, objBody; CanvasGroup objGroup; RectTransform objRt;
        // top-left
        TextMeshProUGUI clockTxt, sunTxt;
        // compass
        RectTransform compassStrip; readonly List<(RectTransform rt, float deg)> compassTicks = new List<(RectTransform, float)>();
        // centre
        Image crosshair; TextMeshProUGUI prompt; CanvasGroup promptGroup; Image holdRing; TextMeshProUGUI keyCap;
        // robot
        CanvasGroup botGroup; TextMeshProUGUI botText; Coroutine botCo; Image botPortraitRing;
        // warnings
        CanvasGroup warnGroup; TextMeshProUGUI warnText; Image warnBg; float warnUntil;
        // overlays
        Image burn, glare, lowO2, damage;
        // hotbar
        RectTransform hotbar; readonly List<(Image bg, Image icon, TextMeshProUGUI key, Image sel)> slots = new List<(Image, Image, TextMeshProUGUI, Image)>();
        TextMeshProUGUI itemName; CanvasGroup itemNameGroup;
        // look-at labels
        CanvasGroup lookGroup; TextMeshProUGUI lookTitle, lookSub;
        // toast
        CanvasGroup toastGroup; TextMeshProUGUI toastTitle, toastSub; Image toastIcon;
        public readonly List<Waypoint> Waypoints = new List<Waypoint>();
        RectTransform wpLayer;
        public Interactor Interactor;
        public string ObjectiveId;
        public string ForcedPrompt; // shown when no object is aimed at (e.g. "near a rocket part")

        public static Hud Create()
        {
            var g = new GameObject("HUD");
            var cv = g.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 10;
            var cs = g.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
            g.AddComponent<GraphicRaycaster>();
            var h = g.AddComponent<Hud>(); h.Canvas = cv; h.Root = (RectTransform)g.transform; h.Build(); Game.Hud = h; return h;
        }

        static Image Glass(Transform p, string n, Vector2 anchor, Vector2 pos, Vector2 size, Color? c = null)
        {
            var i = UIX.Img(p, n, c ?? HC.Glass, UISprites.Round); i.rectTransform.At(anchor, pos, size);
            var o = UIX.Img(i.transform, "Line", HC.Line, UISprites.RoundOutline); o.rectTransform.Fill();
            return i;
        }

        void Build()
        {
            group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
            // ---- overlays (bottom-most) ----
            burn = UIX.Img(Root, "Burn", new Color(1f, 0.35f, 0.08f, 0), UISprites.Vignette); burn.rectTransform.Fill(-60);
            glare = UIX.Img(Root, "Glare", new Color(1f, 0.95f, 0.85f, 0), UISprites.Glow); glare.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2600, 2600));
            lowO2 = UIX.Img(Root, "LowO2", new Color(0.02f, 0.03f, 0.08f, 0), UISprites.Vignette); lowO2.rectTransform.Fill(-60);
            damage = UIX.Img(Root, "Damage", new Color(0.8f, 0.05f, 0.05f, 0), UISprites.Vignette); damage.rectTransform.Fill(-60);
            var visor = UIX.Img(Root, "Visor", new Color(0.7f, 0.9f, 1f, 0.06f), UISprites.Visor); visor.rectTransform.Fill(-20);
            wpLayer = UIX.Go("Waypoints", Root).RT(); wpLayer.Fill();

            // ---- top-left: mission clock + live sun ----
            var tl = Glass(Root, "Clock", new Vector2(0, 1), new Vector2(40, -36), new Vector2(430, 92));
            tl.rectTransform.pivot = new Vector2(0, 1); tl.rectTransform.anchoredPosition = new Vector2(40, -36);
            var acc = UIX.Img(tl.transform, "Acc", HC.Cyan); acc.rectTransform.Set(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(3, -24));
            clockTxt = UIX.Text(tl.transform, "", 22, HC.Text, R.Bold); clockTxt.rectTransform.Set(Vector2.zero, Vector2.one, new Vector2(0, 1), new Vector2(20, -12), new Vector2(-30, -20)); clockTxt.characterSpacing = 3;
            sunTxt = UIX.Text(tl.transform, "", 17, HC.Dim, R.Mono); sunTxt.rectTransform.Set(Vector2.zero, Vector2.one, new Vector2(0, 1), new Vector2(20, -50), new Vector2(-30, -20));

            // ---- top-centre compass ----
            var comp = UIX.Img(Root, "Compass", new Color(0, 0, 0, 0)); comp.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(760, 44));
            var mask = comp.gameObject.AddComponent<RectMask2D>(); mask.softness = new Vector2Int(120, 0);
            var line = UIX.Img(comp.transform, "Base", HC.Line); line.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(0, 1));
            compassStrip = UIX.Go("Strip", comp.transform).RT(); compassStrip.Fill();
            for (int d = 0; d < 360; d += 15)
            {
                bool major = d % 90 == 0, mid = d % 45 == 0;
                if (major || mid)
                {
                    string l = d == 0 ? "N" : d == 90 ? "E" : d == 180 ? "S" : d == 270 ? "W" : d == 45 ? "NE" : d == 135 ? "SE" : d == 225 ? "SW" : "NW";
                    var t = UIX.Text(compassStrip, l, major ? 22 : 15, major ? HC.Text : HC.Dim, R.Bold, TextAlignmentOptions.Center);
                    t.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(60, 26)); compassTicks.Add((t.rectTransform, d));
                }
                else
                {
                    var tk = UIX.Img(compassStrip, "Tick", HC.Line * 2); tk.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(2, 10)); compassTicks.Add((tk.rectTransform, d));
                }
            }
            var caret = UIX.Img(comp.transform, "Caret", HC.Cyan, UISprites.Diamond); caret.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0, -6), new Vector2(10, 10));

            // ---- top-right objective ----
            objRt = Glass(Root, "Objective", new Vector2(1, 1), new Vector2(-40, -36), new Vector2(470, 120)).rectTransform; objRt.pivot = new Vector2(1, 1); objRt.anchoredPosition = new Vector2(-40, -36);
            objGroup = objRt.gameObject.AddComponent<CanvasGroup>();
            var oh = UIX.Text(objRt, "OBJECTIVE", 15, HC.Amber, R.Bold); oh.characterSpacing = 8; oh.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(22, -14), new Vector2(-40, 22));
            var ob = UIX.Img(objRt, "Acc", HC.Amber); ob.rectTransform.Set(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(3, -24));
            objTitle = UIX.Text(objRt, "", 26, HC.Text, R.Bold); objTitle.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(22, -38), new Vector2(-40, 32));
            objBody = UIX.Text(objRt, "", 19, HC.Dim, R.Body); objBody.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(22, -72), new Vector2(-40, 60));
            objGroup.alpha = 0;

            // ---- bottom-left vitals ----
            var vit = Glass(Root, "Vitals", new Vector2(0, 0), new Vector2(40, 40), new Vector2(400, 196)); vit.rectTransform.pivot = Vector2.zero; vit.rectTransform.anchoredPosition = new Vector2(40, 40);
            var vh = UIX.Text(vit.transform, "SUIT STATUS", 14, HC.Dim, R.Bold); vh.characterSpacing = 8; vh.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -12), new Vector2(-30, 20));
            o2 = MakeBar(vit.transform, 0, "OXYGEN", "air", HC.Cyan);
            water = MakeBar(vit.transform, 1, "WATER", "water", new Color(0.4f, 0.7f, 1f));
            health = MakeBar(vit.transform, 2, "HEALTH", "health", HC.Green);
            rad = MakeBar(vit.transform, 3, "RADIATION", "flare", HC.Amber);

            // ---- bottom-centre hotbar ----
            hotbar = UIX.Go("Hotbar", Root).RT(); hotbar.At(new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(Survival.Slots * 84, 76));
            for (int i = 0; i < Survival.Slots; i++)
            {
                var s = Glass(hotbar, "Slot" + i, new Vector2(0, 0.5f), new Vector2(i * 84 + 4, 0), new Vector2(76, 76)); s.rectTransform.pivot = new Vector2(0, 0.5f); s.rectTransform.anchoredPosition = new Vector2(i * 84 + 4, 0);
                var sel = UIX.Img(s.transform, "Sel", HC.Cyan, UISprites.RoundOutline); sel.rectTransform.Fill(-2); sel.enabled = false;
                var ic = UIX.Img(s.transform, "Icon", Color.white); ic.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(44, 44)); ic.preserveAspect = true; ic.enabled = false;
                var k = UIX.Text(s.transform, (i + 1).ToString(), 14, HC.Dim, R.Mono); k.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -4), new Vector2(20, 18));
                slots.Add((s, ic, k, sel));
            }
            itemNameGroup = UIX.Go("ItemName", Root).AddComponent<CanvasGroup>(); ((RectTransform)itemNameGroup.transform).At(new Vector2(0.5f, 0), new Vector2(0, 132), new Vector2(600, 30));
            itemName = UIX.Text(itemNameGroup.transform, "", 20, HC.Text, R.Bold, TextAlignmentOptions.Center); itemName.rectTransform.Fill(); itemName.characterSpacing = 4; itemNameGroup.alpha = 0;

            // ---- centre crosshair + prompt ----
            crosshair = UIX.Img(Root, "Cross", new Color(1, 1, 1, 0.75f), UISprites.Circle); crosshair.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));
            holdRing = UIX.Img(Root, "Hold", HC.Cyan, UISprites.Ring); holdRing.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46));
            holdRing.type = Image.Type.Filled; holdRing.fillMethod = Image.FillMethod.Radial360; holdRing.fillOrigin = 2; holdRing.fillAmount = 0;
            promptGroup = UIX.Go("Prompt", Root).AddComponent<CanvasGroup>(); var prt = (RectTransform)promptGroup.transform; prt.At(new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(700, 46));
            var kc = Glass(prt, "Key", new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(42, 42), new Color(0.9f, 0.95f, 1f, 0.95f));
            keyCap = UIX.Text(kc.transform, "E", 22, new Color(0.05f, 0.08f, 0.1f), R.Bold, TextAlignmentOptions.Center); keyCap.rectTransform.Fill();
            prompt = UIX.Text(prt, "", 24, HC.Text, R.Bold, TextAlignmentOptions.Left); prompt.characterSpacing = 2;
            prompt.rectTransform.Set(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(34, 0), new Vector2(600, 44));
            promptGroup.alpha = 0;

            // ---- look-at label (planets) ----
            lookGroup = UIX.Go("Look", Root).AddComponent<CanvasGroup>(); var lrt = (RectTransform)lookGroup.transform; lrt.At(new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(800, 80));
            lookTitle = UIX.Text(lrt, "", 30, HC.Text, R.Title, TextAlignmentOptions.Center); lookTitle.characterSpacing = 10; lookTitle.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 40));
            lookSub = UIX.Text(lrt, "", 19, HC.Dim, R.Body, TextAlignmentOptions.Center); lookSub.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(0, 30));
            lookGroup.alpha = 0;

            // ---- robot dialogue ----
            botGroup = UIX.Go("Bot", Root).AddComponent<CanvasGroup>(); var brt = (RectTransform)botGroup.transform; brt.At(new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(900, 100));
            var bbg = Glass(brt, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 100)); bbg.rectTransform.Fill();
            var portrait = UIX.Img(brt, "Portrait", new Color(0.05f, 0.1f, 0.14f, 0.95f), UISprites.Circle); portrait.rectTransform.Set(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(70, 70));
            botPortraitRing = UIX.Img(portrait.transform, "Ring", HC.Cyan, UISprites.Ring); botPortraitRing.rectTransform.Fill(-3);
            var pic = UIX.Img(portrait.transform, "Icon", HC.Cyan, Icons.Get("robot")); pic.rectTransform.Fill(14);
            var bn = UIX.Text(brt, "NOVA", 15, HC.Cyan, R.Bold); bn.characterSpacing = 8; bn.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(108, -12), new Vector2(-130, 20));
            botText = UIX.Text(brt, "", 23, HC.Text, R.Body); botText.rectTransform.Set(Vector2.zero, Vector2.one, new Vector2(0, 1), new Vector2(108, -34), new Vector2(-130, -42));
            botGroup.alpha = 0;

            // ---- warning banner ----
            warnGroup = UIX.Go("Warn", Root).AddComponent<CanvasGroup>(); var wrt = (RectTransform)warnGroup.transform; wrt.At(new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(820, 56));
            warnBg = UIX.Img(wrt, "Bg", new Color(0.35f, 0.04f, 0.02f, 0.75f), UISprites.Round); warnBg.rectTransform.Fill();
            var wi = UIX.Img(wrt, "Icon", HC.Amber, Icons.Get("warn")); wi.rectTransform.Set(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(32, 32));
            warnText = UIX.Text(wrt, "", 24, HC.Text, R.Bold, TextAlignmentOptions.Center); warnText.rectTransform.Fill(); warnText.characterSpacing = 5;
            warnGroup.alpha = 0;

            // ---- toast (item picked up etc.) ----
            toastGroup = UIX.Go("Toast", Root).AddComponent<CanvasGroup>(); var trt = (RectTransform)toastGroup.transform; trt.At(new Vector2(1, 0.5f), new Vector2(-40, 60), new Vector2(440, 84)); trt.pivot = new Vector2(1, 0.5f); trt.anchoredPosition = new Vector2(-40, 60);
            var tbg = Glass(trt, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); tbg.rectTransform.Fill();
            toastIcon = UIX.Img(trt, "Icon", HC.Cyan); toastIcon.rectTransform.Set(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(46, 46)); toastIcon.preserveAspect = true;
            toastTitle = UIX.Text(trt, "", 22, HC.Text, R.Bold); toastTitle.rectTransform.Set(Vector2.zero, Vector2.one, new Vector2(0, 1), new Vector2(80, -12), new Vector2(-96, -20));
            toastSub = UIX.Text(trt, "", 17, HC.Dim, R.Body); toastSub.rectTransform.Set(Vector2.zero, Vector2.one, new Vector2(0, 1), new Vector2(80, -44), new Vector2(-96, -20));
            toastGroup.alpha = 0;
        }

        Bar MakeBar(Transform p, int row, string label, string icon, Color c)
        {
            var b = new Bar { Col = c };
            var r = UIX.Go(label, p).RT(); r.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -42 - row * 38), new Vector2(-40, 32));
            b.G = r.gameObject.AddComponent<CanvasGroup>();
            b.Icon = UIX.Img(r, "Icon", c, Icons.Get(icon)); b.Icon.rectTransform.Set(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(24, 24));
            var l = UIX.Text(r, label, 15, HC.Dim, R.Bold); l.characterSpacing = 4; l.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, 0), new Vector2(160, 18));
            b.Val = UIX.Text(r, "", 17, HC.Text, R.Mono, TextAlignmentOptions.Right); b.Val.rectTransform.Set(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(140, 18));
            var track = UIX.Img(r, "Track", new Color(1, 1, 1, 0.08f)); track.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(34, 2), new Vector2(-34, 6));
            b.FillImg = UIX.Img(track.transform, "Fill", c); b.Fill = b.FillImg.rectTransform; b.Fill.Set(Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            for (int i = 1; i < 10; i++) { var gap = UIX.Img(track.transform, "Gap", new Color(0.01f, 0.02f, 0.03f, 0.9f)); gap.rectTransform.Set(new Vector2(i / 10f, 0), new Vector2(i / 10f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 0)); }
            return b;
        }

        // ------------------------------------------------------------------ API
        public void Show(bool on, float d = 0.8f) { Tween.Fade(group, on ? 1 : 0, d); }
        public bool Visible => group.alpha > 0.5f;

        public void Objective(string title, string body, string id = null)
        {
            ObjectiveId = id;
            StartCoroutine(ObjCo(title, body));
        }
        IEnumerator ObjCo(string title, string body)
        {
            if (objGroup.alpha > 0) { Tween.Fade(objGroup, 0, 0.25f); yield return new WaitForSecondsRealtime(0.3f); }
            objTitle.text = title; objBody.text = body;
            objTitle.ForceMeshUpdate(); objBody.ForceMeshUpdate();
            float h = 80 + objBody.preferredHeight; objRt.sizeDelta = new Vector2(470, Mathf.Max(110, h));
            Sfx.Play("notify", 0.5f);
            Tween.Fade(objGroup, 1, 0.4f);
            objRt.anchoredPosition = new Vector2(20, -36);
            Tween.Run(0.5f, t => objRt.anchoredPosition = new Vector2(Mathf.Lerp(20, -40, Tween.EaseOut(t)), -36));
        }
        public void ClearObjective() { Tween.Fade(objGroup, 0, 0.3f); }

        public Coroutine Say(string text, float hold = 0)
        {
            if (botCo != null) StopCoroutine(botCo);
            botCo = StartCoroutine(SayCo(text, hold <= 0 ? 2.2f + text.Length * 0.045f : hold));
            return botCo;
        }
        public IEnumerator SayWait(string text, float hold = 0) { yield return Say(text, hold); }
        IEnumerator SayCo(string text, float hold)
        {
            Tween.Fade(botGroup, 1, 0.25f);
            Sfx.Play("beep", 0.35f, 1.4f);
            if (Game.Bot) Game.Bot.Talk(Mathf.Min(hold, 2.5f + text.Length * 0.02f));
            botText.text = text; botText.maxVisibleCharacters = 0;
            int n = text.Length; float t = 0;
            while (botText.maxVisibleCharacters < n)
            {
                t += Time.unscaledDeltaTime * 55; botText.maxVisibleCharacters = Mathf.Min(n, (int)t);
                if (Input.GetKeyDown(KeyCode.Return)) botText.maxVisibleCharacters = n;
                yield return null;
            }
            float w = 0; while (w < hold) { w += Time.unscaledDeltaTime; if (Input.GetKeyDown(KeyCode.Return) && w > 0.3f) break; yield return null; }
            Tween.Fade(botGroup, 0, 0.35f);
            yield return new WaitForSecondsRealtime(0.3f);
            botCo = null;
        }
        public bool Talking => botCo != null;

        public void Warn(string s, float dur = 3.5f, bool red = true)
        {
            warnText.text = s; warnUntil = Time.unscaledTime + dur;
            warnBg.color = red ? new Color(0.4f, 0.04f, 0.02f, 0.78f) : new Color(0.3f, 0.18f, 0.02f, 0.78f);
            Sfx.Play("alarm", 0.45f);
        }

        public void Toast(string title, string sub, string icon, Color c)
        {
            StartCoroutine(ToastCo(title, sub, icon, c));
        }
        IEnumerator ToastCo(string title, string sub, string icon, Color c)
        {
            toastTitle.text = title; toastSub.text = sub; toastIcon.sprite = Icons.Get(icon); toastIcon.color = c;
            Sfx.Play("pickup", 0.7f);
            var rt = (RectTransform)toastGroup.transform;
            Tween.Fade(toastGroup, 1, 0.3f);
            Tween.Run(0.4f, t => rt.anchoredPosition = new Vector2(Mathf.Lerp(40, -40, Tween.EaseOut(t)), 60));
            yield return new WaitForSecondsRealtime(3.2f);
            Tween.Fade(toastGroup, 0, 0.5f);
        }

        public void Damage(float a) { damage.color = new Color(0.8f, 0.05f, 0.05f, Mathf.Max(damage.color.a, a)); }

        public Waypoint AddWaypoint(string label, System.Func<Vector3> pos, Color c)
        {
            var w = new Waypoint { Label = label, Pos = pos, Color = c };
            var rt = UIX.Go("WP_" + label, wpLayer).RT(); rt.At(new Vector2(0, 0), Vector2.zero, new Vector2(200, 60)); rt.pivot = new Vector2(0.5f, 0.5f);
            w.Icon = UIX.Img(rt, "Diamond", c, UISprites.Diamond); w.Icon.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(18, 18));
            w.Txt = UIX.Text(rt, "", 16, c, R.Bold, TextAlignmentOptions.Center); w.Txt.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(240, 22)); w.Txt.characterSpacing = 3;
            w.Rt = rt; Waypoints.Add(w); return w;
        }
        public void RemoveWaypoint(Waypoint w) { if (w == null) return; Waypoints.Remove(w); if (w.Rt) Destroy(w.Rt.gameObject); }
        public void ClearWaypoints() { foreach (var w in Waypoints) if (w.Rt) Destroy(w.Rt.gameObject); Waypoints.Clear(); }

        public void RefreshHotbar(bool flash)
        {
            var s = Game.Suit; if (!s) return;
            for (int i = 0; i < slots.Count; i++)
            {
                var sl = slots[i];
                bool has = i < s.Items.Count;
                sl.icon.enabled = has;
                if (has) { sl.icon.sprite = Icons.Get(s.Items[i].Icon); sl.icon.color = s.Items[i].Color; }
                sl.sel.enabled = has && i == s.Selected;
            }
            if (s.Items.Count > 0)
            {
                var it = s.Items[Mathf.Clamp(s.Selected, 0, s.Items.Count - 1)];
                itemName.text = it.Name.ToUpper() + (it.Heavy ? "  <size=15><color=#FFAE40>HEAVY · SLOWER</color></size>" : "");
                itemNameGroup.alpha = 1; nameT = 2.5f;
                if (flash) { var rt = slots[s.Items.Count - 1].bg.rectTransform; Tween.Run(0.4f, t => rt.localScale = Vector3.one * (1 + 0.25f * (1 - Tween.EaseOut(t)))); }
            }
        }
        float nameT;

        // ------------------------------------------------------------------
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var s = Game.Suit; var p = Game.Player; var cam = Game.Cam;
            if (s)
            {
                Set(o2, s.O2 / 100f, $"{s.O2:0}%", s.O2 < 25);
                Set(water, s.Water / 100f, $"{s.Water:0}%", s.Water < 25);
                Set(health, s.Health / 100f, $"{s.Health:0}%", s.Health < 30);
                float rr = Mathf.Clamp01(s.DoseRate / 40f);
                Set(rad, Mathf.Max(0.04f, rr), s.Inside ? "SHIELDED" : $"{s.DoseRate:0.0} mSv/sol", s.Storm > 0.05f && !s.Inside);
                // overlays
                float burnA = s.Inside ? 0 : s.Storm * 0.55f + Mathf.Sin(Time.time * 3) * s.Storm * 0.08f;
                burn.color = new Color(1f, 0.35f, 0.08f, Mathf.Lerp(burn.color.a, burnA, dt * 3));
                glare.color = new Color(1f, 0.95f, 0.85f, Mathf.Lerp(glare.color.a, s.SunGlare * (0.35f + s.Storm * 0.5f), dt * 5));
                if (cam && Game.Sky && s.SunGlare > 0.01f)
                {
                    var sp = cam.WorldToViewportPoint(cam.transform.position + Game.Sky.SunDir * 100);
                    glare.rectTransform.anchorMin = glare.rectTransform.anchorMax = new Vector2(sp.x, sp.y);
                }
                float low = Mathf.Max(Mathf.Clamp01((25 - s.O2) / 25f), Mathf.Clamp01((25 - s.Water) / 25f));
                lowO2.color = new Color(0.02f, 0.03f, 0.08f, Mathf.Lerp(lowO2.color.a, low * 0.85f, dt * 2));
            }
            damage.color = new Color(0.8f, 0.05f, 0.05f, Mathf.MoveTowards(damage.color.a, 0, dt * 0.8f));

            // clock
            if (Game.Story) clockTxt.text = Game.Story.ClockText();
            sunTxt.text = Game.Story ? Game.Story.SunText() : "";

            // compass
            if (cam)
            {
                float yaw = cam.transform.eulerAngles.y;
                foreach (var (rt, deg) in compassTicks)
                {
                    float d = Mathf.DeltaAngle(yaw, deg);
                    rt.anchoredPosition = new Vector2(d * 4.2f, rt.anchoredPosition.y);
                    rt.gameObject.SetActive(Mathf.Abs(d) < 95);
                }
            }

            // waypoints
            foreach (var w in Waypoints)
            {
                if (!w.Rt) continue;
                bool on = w.On && cam && p && !(s && s.Inside);
                w.Rt.gameObject.SetActive(on);
                if (!on) continue;
                var wp = w.Pos();
                var v = cam.WorldToScreenPoint(wp + Vector3.up * 2);
                float dist = Vector3.Distance(p.transform.position, wp);
                var scale = Root.rect.width / Screen.width;
                Vector2 sc = new Vector2(v.x, v.y);
                bool behind = v.z < 0;
                if (behind) sc = new Vector2(Screen.width - sc.x, Screen.height - sc.y);
                Vector2 cen = new Vector2(Screen.width, Screen.height) / 2;
                Vector2 off = sc - cen;
                float mx = Screen.width / 2 - 70, my = Screen.height / 2 - 120;
                bool clamped = behind || Mathf.Abs(off.x) > mx || Mathf.Abs(off.y) > my;
                if (clamped)
                {
                    if (off.sqrMagnitude < 1) off = new Vector2(0, -1);
                    float k = Mathf.Min(mx / Mathf.Max(0.01f, Mathf.Abs(off.x)), my / Mathf.Max(0.01f, Mathf.Abs(off.y)));
                    off *= k;
                }
                w.Rt.anchoredPosition = (cen + off) * scale;
                w.Txt.text = $"{w.Label}  {dist:0}m";
                float a = dist < 6 ? Mathf.InverseLerp(2, 6, dist) : 1;
                w.Icon.color = new Color(w.Color.r, w.Color.g, w.Color.b, a); w.Txt.color = new Color(w.Color.r, w.Color.g, w.Color.b, a * 0.95f);
            }

            // interaction prompt
            var cur = Interactor ? Interactor.Current : null;
            bool forced = cur == null && !string.IsNullOrEmpty(ForcedPrompt);
            bool showPrompt = (cur || forced) && !UIState.Modal && !UIState.Cinematic;
            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, showPrompt ? 1 : 0, dt * 8);
            if (showPrompt) prompt.text = forced ? ForcedPrompt : $"{cur.Verb.ToUpper()}  <color={HC.Hex(HC.Cyan)}>{cur.Label}</color>";
            holdRing.fillAmount = Interactor ? Interactor.HoldProgress : 0;
            crosshair.color = showPrompt ? HC.Cyan : new Color(1, 1, 1, 0.7f);
            crosshair.rectTransform.sizeDelta = Vector2.one * (showPrompt ? 10 : 6);

            // warning
            float wa = Time.unscaledTime < warnUntil ? (0.75f + Mathf.Sin(Time.unscaledTime * 8) * 0.25f) : 0;
            warnGroup.alpha = Mathf.MoveTowards(warnGroup.alpha, wa, dt * 6);

            // hotbar name fade
            nameT -= dt; itemNameGroup.alpha = Mathf.MoveTowards(itemNameGroup.alpha, nameT > 0 ? 1 : 0, dt * 2);

            // look-at labels for sky bodies
            string lt = null, ls = null;
            if (cam && !(s && s.Inside) && !UIState.Cinematic)
            {
                var f = cam.transform.forward;
                if (MarsWorld.Earth && Vector3.Dot(f, MarsWorld.Earth.GetComponent<SkyBody>().Dir) > 0.996f) { lt = "EARTH"; ls = "Home · about 225 million km away · radio takes 3–22 min"; }
                else if (MarsWorld.Phobos && Vector3.Dot(f, MarsWorld.Phobos.GetComponent<SkyBody>().Dir) > 0.996f) { lt = "PHOBOS"; ls = "The bigger of Mars's two moons · 22 km wide"; }
                else if (Game.Sky && Vector3.Dot(f, Game.Sky.SunDir) > 0.985f && Game.Sky.Day > 0.2f) { lt = "THE SUN"; ls = "Don't stare! Mars has no magnetic shield against its radiation"; }
            }
            if (lt != null) { lookTitle.text = lt; lookSub.text = ls; }
            lookGroup.alpha = Mathf.MoveTowards(lookGroup.alpha, lt != null ? 1 : 0, dt * 4);
        }

        void Set(Bar b, float v, string txt, bool warn)
        {
            b.Shown = Mathf.Lerp(b.Shown, Mathf.Clamp01(v), Time.unscaledDeltaTime * 6);
            b.Fill.anchorMax = new Vector2(b.Shown, 1);
            b.Val.text = txt;
            Color c = warn ? Color.Lerp(b.Col, HC.Red, 0.5f + Mathf.Sin(Time.unscaledTime * 8) * 0.5f) : b.Col;
            b.FillImg.color = c; b.Icon.color = c;
            b.Val.color = warn ? HC.Red : HC.Text;
        }
    }
}
