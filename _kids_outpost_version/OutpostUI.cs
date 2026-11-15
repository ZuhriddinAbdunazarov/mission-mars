using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MissionMars
{
    public static class K
    {
        public static readonly Color Ink = new Color(0.13f, 0.18f, 0.29f);
        public static readonly Color Soft = new Color(0.42f, 0.48f, 0.58f);
        public static readonly Color Card = new Color(1, 1, 1, 0.96f);
        public static readonly Color Power = new Color(1f, 0.74f, 0.1f);
        public static readonly Color Air = new Color(0.25f, 0.67f, 1f);
        public static readonly Color Food = new Color(0.33f, 0.78f, 0.35f);
        public static readonly Color Health = new Color(1f, 0.36f, 0.5f);
        public static readonly Color Shield = new Color(0.56f, 0.42f, 1f);
        public static readonly Color Star = new Color(1f, 0.6f, 0.1f);
        public static readonly Color Go = new Color(0.2f, 0.78f, 0.42f);
        public static readonly Color Orange = new Color(1f, 0.45f, 0.2f);
        public static readonly Color Blue = new Color(0.2f, 0.5f, 1f);
        public static readonly Color Bad = new Color(0.95f, 0.25f, 0.3f);
        public static Color SysColor(int i) => i == 0 ? Air : i == 1 ? Food : i == 2 ? Shield : Star;
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    /// <summary>Bouncy, chunky kid-style button.</summary>
    public class KidButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Image Face, Shadow; public TextMeshProUGUI Label; public Image Icon; public Action OnClick; public bool Interactable = true;
        Color face; float hover, press; RectTransform faceRT; float pulse;
        public void Pulse(bool on) { pulse = on ? 1 : 0; }
        public void SetColor(Color c) { face = c; Face.color = c; Shadow.color = new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, 1); }
        public void Init(Image f, Image s, TextMeshProUGUI l, Color c) { Face = f; Shadow = s; Label = l; faceRT = f.rectTransform; SetColor(c); }
        public void OnPointerEnter(PointerEventData e) { if (Interactable) { hover = 1; Sfx.Play("hover", 0.4f); } }
        public void OnPointerExit(PointerEventData e) { hover = 0; press = 0; }
        public void OnPointerDown(PointerEventData e) { if (Interactable) press = 1; }
        public void OnPointerUp(PointerEventData e)
        {
            if (Interactable && press > 0 && hover > 0) { Sfx.Play("click", 0.8f); OnClick?.Invoke(); }
            press = 0;
        }
        void Update()
        {
            float s = 1 + hover * 0.05f + pulse * 0.06f * Mathf.Sin(Time.unscaledTime * 6);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * s, Time.unscaledDeltaTime * 14);
            faceRT.anchoredPosition = new Vector2(0, press > 0 ? -2 : 4);
            Face.color = Interactable ? face : new Color(0.75f, 0.77f, 0.8f);
            Shadow.color = Interactable ? new Color(face.r * 0.6f, face.g * 0.6f, face.b * 0.6f) : new Color(0.6f, 0.62f, 0.66f);
        }
    }

    public class OutpostUI : MonoBehaviour
    {
        public RectTransform Root;
        CanvasGroup title, hud, popup, shop, data;
        public Action OnPlay, OnNextSol, OnHow; public Action<int, int> OnLevel; public Action<Rules.Build> OnBuild;
        // hud refs
        TextMeshProUGUI solBig, solSmall, weather, sunChip; Image weatherIcon;
        readonly Dictionary<string, (Image bar, TextMeshProUGUI val, TextMeshProUGUI delta, RectTransform rt)> chips = new Dictionary<string, (Image, TextMeshProUGUI, TextMeshProUGUI, RectTransform)>();
        TextMeshProUGUI powerNum, powerSub; RectTransform powerSlots; readonly List<Image> slots = new List<Image>();
        readonly List<(RectTransform rt, TextMeshProUGUI effect, Image[] pips, KidButton minus, KidButton plus, TextMeshProUGUI state, Image glow)> sysCards = new List<(RectTransform, TextMeshProUGUI, Image[], KidButton, KidButton, TextMeshProUGUI, Image)>();
        KidButton nextBtn, buildBtn, dataBtn;
        // rovy
        CanvasGroup rovy; TextMeshProUGUI rovyText; KidButton rovyOk; bool rovyAck; RectTransform rovyRT;
        // popup
        Image popIconBg, popIcon; TextMeshProUGUI popTitle, popBody, popData; RectTransform popDataBox, popButtons, popCard;
        int popChoice = -1;
        RectTransform shopGrid, dataList, floatLayer;
        CanvasGroup banner; TextMeshProUGUI bannerBig, bannerSmall;

        public static OutpostUI Create()
        {
            var g = new GameObject("OutpostUI");
            var cv = g.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 20;
            var cs = g.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
            g.AddComponent<GraphicRaycaster>();
            var ui = g.AddComponent<OutpostUI>(); ui.Root = (RectTransform)g.transform; ui.Build(); return ui;
        }

        // ---------------- helpers ----------------
        static TextMeshProUGUI T(Transform p, string s, float size, Color c, bool head = false, TextAlignmentOptions a = TextAlignmentOptions.Left)
        {
            var t = UIX.Text(p, s, size, c, head ? R.Kid : R.KidBody, a); t.enableWordWrapping = true; return t;
        }
        static Image Card(Transform p, string name, Color? c = null)
        {
            var sh = UIX.Img(p, name + "Shadow", new Color(0.1f, 0.05f, 0.02f, 0.22f), UISprites.Round);
            var i = UIX.Img(p, name, c ?? K.Card, UISprites.Round, true);
            sh.transform.SetParent(i.transform, false); sh.rectTransform.Fill(); sh.rectTransform.offsetMin = new Vector2(-2, -8); sh.rectTransform.offsetMax = new Vector2(2, -2);
            sh.transform.SetAsFirstSibling();
            // shadow must render behind: move it out as sibling before card
            sh.transform.SetParent(p, false); sh.transform.SetSiblingIndex(i.transform.GetSiblingIndex());
            sh.gameObject.AddComponent<FollowRect>().Target = i.rectTransform;
            return i;
        }
        static Image IconCircle(Transform p, string icon, Color c, float size)
        {
            var bg = UIX.Img(p, "IconBg", c, UISprites.Circle);
            bg.rectTransform.sizeDelta = Vector2.one * size;
            var ic = UIX.Img(bg.transform, "Icon", Color.white, Icons.Get(icon)); ic.rectTransform.Fill(size * 0.2f); ic.preserveAspect = true;
            return bg;
        }
        public static KidButton Btn(Transform p, string label, Color c, float w, float h, Action onClick, string icon = null, float fs = 30)
        {
            var root = UIX.Go("Btn_" + label, p).RT(); root.sizeDelta = new Vector2(w, h);
            var shadow = UIX.Img(root, "Shadow", Color.black, UISprites.Round, false); shadow.rectTransform.Fill();
            var face = UIX.Img(root, "Face", c, UISprites.Round, true); face.rectTransform.Set(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(0, -4));
            var hi = UIX.Img(face.transform, "Hi", new Color(1, 1, 1, 0.18f), UISprites.Round); hi.rectTransform.Set(new Vector2(0, 0.55f), Vector2.one, new Vector2(0.5f, 1), Vector2.zero, new Vector2(-12, -6));
            var t = T(face.transform, label, fs, Color.white, true, TextAlignmentOptions.Center); t.rectTransform.Fill(); t.enableWordWrapping = false;
            var kb = root.gameObject.AddComponent<KidButton>(); kb.Init(face, shadow, t, c); kb.OnClick = onClick;
            if (icon != null)
            {
                var ic = UIX.Img(face.transform, "Icon", Color.white, Icons.Get(icon)); ic.preserveAspect = true;
                ic.rectTransform.Set(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(h * 0.28f, 0), Vector2.one * h * 0.5f);
                kb.Icon = ic;
                if (!string.IsNullOrEmpty(label)) t.rectTransform.offsetMin = new Vector2(h * 0.75f, 0);
                else ic.rectTransform.Set(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * h * 0.5f);
            }
            return kb;
        }
        static CanvasGroup Layer(Transform p, string n) { var g = UIX.Go(n, p).AddComponent<CanvasGroup>(); ((RectTransform)g.transform).Fill(); return g; }
        static void Show(CanvasGroup g, bool on, float d = 0.3f) { g.blocksRaycasts = on; g.interactable = on; Tween.Fade(g, on ? 1 : 0, d); }

        // ---------------- build ----------------
        void Build()
        {
            BuildHUD(); BuildRovy(); BuildTitle(); BuildPopup(); BuildShop(); BuildData(); BuildBanner();
            floatLayer = UIX.Go("Floats", Root).RT(); floatLayer.Fill();
        }

        void BuildTitle()
        {
            title = Layer(Root, "Title");
            var shade = UIX.Img(title.transform, "Shade", new Color(0.1f, 0.03f, 0.02f, 0.35f), UISprites.Vignette); shade.rectTransform.Fill();
            var glow = UIX.Img(title.transform, "Glow", new Color(0.25f, 0.06f, 0.02f, 0.55f), UISprites.Glow); glow.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1900, 900));
            var sub0 = T(title.transform, "NASA SPACE APPS CHALLENGE 2026", 26, new Color(1, 0.9f, 0.75f), true, TextAlignmentOptions.Center); sub0.characterSpacing = 8;
            sub0.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(1400, 40));
            var t1s = T(title.transform, "MARS BASE ONE", 150, new Color(0.55f, 0.18f, 0.05f, 0.8f), true, TextAlignmentOptions.Center); t1s.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(4, 172), new Vector2(1600, 170)); t1s.enableWordWrapping = false;
            var t1 = T(title.transform, "MARS BASE ONE", 150, Color.white, true, TextAlignmentOptions.Center); t1.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(1600, 170)); t1.enableWordWrapping = false;
            var pill = UIX.Img(title.transform, "Pill", K.Orange, UISprites.Round); pill.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 66), new Vector2(720, 64));
            var t2 = T(pill.transform, "Junior Astronaut Mission Trainer", 34, Color.white, true, TextAlignmentOptions.Center); t2.rectTransform.Fill();
            var t3 = T(title.transform, "Run a real Mars base. Keep your crew safe for 20 sols!", 32, Color.white, false, TextAlignmentOptions.Center);
            t3.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1400, 50));
            var play = Btn(title.transform, "PLAY", K.Go, 380, 110, () => OnPlay?.Invoke(), "play", 56); ((RectTransform)play.transform).At(new Vector2(0.5f, 0.5f), new Vector2(0, -140), new Vector2(380, 110));
            play.Pulse(true);
            var how = Btn(title.transform, "HOW TO PLAY", K.Blue, 330, 74, () => OnHow?.Invoke(), "robot", 28); ((RectTransform)how.transform).At(new Vector2(0.5f, 0.5f), new Vector2(-180, -270), new Vector2(330, 74));
            var dat = Btn(title.transform, "NASA DATA", K.Shield, 330, 74, OpenData, "data", 28); ((RectTransform)dat.transform).At(new Vector2(0.5f, 0.5f), new Vector2(180, -270), new Vector2(330, 74));
            var foot = T(title.transform, "Real data from NASA: DONKI space weather · Curiosity RAD · Perseverance MOXIE  ·  3D models: NASA 3D Resources  ·  Not an official NASA product", 18, new Color(1, 1, 1, 0.8f), false, TextAlignmentOptions.Center);
            foot.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1800, 30));
        }

        void BuildHUD()
        {
            hud = Layer(Root, "HUD"); hud.alpha = 0; hud.blocksRaycasts = false;
            var h = hud.transform;
            // sol badge
            var badge = Card(h, "Sol"); badge.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -24), new Vector2(270, 128));
            solBig = T(badge.transform, "SOL 1", 52, K.Ink, true); solBig.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -2), new Vector2(-40, 64));
            solSmall = T(badge.transform, "of 20 · rocket home on sol 20", 19, K.Soft); solSmall.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(26, -62), new Vector2(-40, 26));
            var wchip0 = 0; var wchip = UIX.Img(badge.transform, "Weather", new Color(1, 0.95f, 0.85f), UISprites.Round); wchip.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(-24, 34));
            weatherIcon = UIX.Img(wchip.transform, "WI", K.Power, Icons.Get("sun")); weatherIcon.rectTransform.At(new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(26, 26));
            weather = T(wchip.transform, "Sunny", 20, K.Ink, true, TextAlignmentOptions.MidlineLeft); weather.enableWordWrapping = false; weather.rectTransform.Fill(); weather.rectTransform.offsetMin = new Vector2(40, 0);

            // resource chips
            var row = UIX.Go("Resources", h).RT(); row.At(new Vector2(0.5f, 1), new Vector2(40, -24), new Vector2(1240, 96));
            string[] ids = { "power", "air", "food", "health", "science" };
            string[] names = { "Battery", "Air", "Food", "Health", "Stars" };
            Color[] cols = { K.Power, K.Air, K.Food, K.Health, K.Star };
            for (int i = 0; i < ids.Length; i++)
            {
                var c = Card(row, "Chip_" + ids[i]); c.rectTransform.Set(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(i * 250, 0), new Vector2(236, 96));
                var ic = IconCircle(c.transform, ids[i], cols[i], 64); ic.rectTransform.At(new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(64, 64));
                var nm = T(c.transform, names[i], 20, K.Soft, true); nm.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(86, -12), new Vector2(-96, 24));
                var val = T(c.transform, "0", 34, K.Ink, true); val.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(86, -34), new Vector2(-96, 40)); val.enableWordWrapping = false;
                var dl = T(c.transform, "", 24, K.Go, true, TextAlignmentOptions.Right); dl.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(86, -36), new Vector2(-100, 40)); dl.enableWordWrapping = false;
                var barBg = UIX.Img(c.transform, "BarBg", new Color(0.9f, 0.91f, 0.94f), UISprites.Round); barBg.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(86, 12), new Vector2(-100, 12));
                var bar = UIX.Img(barBg.transform, "Bar", cols[i], UISprites.Round); bar.rectTransform.Set(Vector2.zero, new Vector2(0.5f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
                if (ids[i] == "science") barBg.gameObject.SetActive(false);
                chips[ids[i]] = (bar, val, dl, c.rectTransform);
            }

            // data button + sun chip
            dataBtn = Btn(h, "NASA DATA", K.Shield, 230, 70, OpenData, "data", 24); ((RectTransform)dataBtn.transform).Set(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -26), new Vector2(230, 70));
            var sc = Card(h, "SunChip"); sc.rectTransform.Set(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -108), new Vector2(330, 52));
            var si = UIX.Img(sc.transform, "SI", K.Power, Icons.Get("sun")); si.rectTransform.At(new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(30, 30));
            sunChip = T(sc.transform, "", 18, K.Ink, true, TextAlignmentOptions.MidlineLeft); sunChip.rectTransform.Fill(); sunChip.rectTransform.offsetMin = new Vector2(48, 0); sunChip.rectTransform.offsetMax = new Vector2(-10, 0);

            // ---------- bottom dock ----------
            var dock = Card(h, "Dock"); dock.rectTransform.Set(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(1820, 250));
            var d = dock.transform;
            // power panel
            var pIcon = IconCircle(d, "power", K.Power, 70); pIcon.rectTransform.At(new Vector2(0, 1), new Vector2(26, -24), new Vector2(70, 70));
            var pl = T(d, "POWER TODAY", 22, K.Soft, true); pl.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(108, -24), new Vector2(240, 30));
            powerNum = T(d, "0 / 12", 44, K.Ink, true); powerNum.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(108, -48), new Vector2(240, 54)); powerNum.enableWordWrapping = false;
            powerSub = T(d, "", 19, K.Soft); powerSub.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -112), new Vector2(330, 60));
            powerSlots = UIX.Go("Slots", d).RT(); powerSlots.Set(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 26), new Vector2(330, 40));
            for (int i = 0; i < 24; i++)
            {
                var s = UIX.Img(powerSlots, "S", new Color(0.88f, 0.9f, 0.93f), UISprites.Round);
                s.rectTransform.Set(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2((i % 12) * 27.5f, i < 12 ? 21 : 0), new Vector2(24, 17));
                slots.Add(s);
            }

            // system cards
            string[] sn = { "AIR MAKER", "GREENHOUSE", "SHIELD", "ROVER" };
            string[] si2 = { "air", "food", "shield", "rocket" };
            string[] sd = { "Turns Mars air into oxygen", "Grows food with water", "Protects from the Sun", "Explores for science stars" };
            for (int i = 0; i < 4; i++)
            {
                int idx = i; var col = K.SysColor(i);
                var c = UIX.Img(d, "Sys" + i, new Color(col.r, col.g, col.b, 0.1f), UISprites.Round, true);
                c.rectTransform.Set(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(380 + i * 282, 0), new Vector2(268, 222));
                var glow = UIX.Img(c.transform, "Glow", new Color(col.r, col.g, col.b, 0), UISprites.RoundOutline); glow.rectTransform.Fill(-4);
                var head = UIX.Img(c.transform, "Head", col, UISprites.Round); head.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(-16, 50));
                var hic = UIX.Img(head.transform, "I", Color.white, Icons.Get(si2[i])); hic.rectTransform.At(new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(36, 36)); hic.preserveAspect = true;
                var hn = T(head.transform, sn[i], 26, Color.white, true, TextAlignmentOptions.MidlineLeft); hn.rectTransform.Fill(); hn.rectTransform.offsetMin = new Vector2(54, 0);
                var desc = T(c.transform, sd[i], 18, K.Soft, false, TextAlignmentOptions.Center); desc.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(-20, 24));
                var eff = T(c.transform, "", 25, K.Ink, true, TextAlignmentOptions.Top); eff.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(-16, 60)); eff.lineSpacing = -18;
                var state = T(c.transform, "OFF", 24, K.Soft, true, TextAlignmentOptions.Center); state.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(116, 40)); state.lineSpacing = -30;
                var pips = new Image[2];
                for (int k = 0; k < 2; k++) { pips[k] = UIX.Img(c.transform, "Pip", new Color(0.85f, 0.87f, 0.9f), UISprites.Round); pips[k].rectTransform.At(new Vector2(0.5f, 0), new Vector2(-24 + k * 48, 14), new Vector2(40, 10)); }
                var minus = Btn(c.transform, "", K.Soft, 64, 64, () => OnLevel?.Invoke(idx, -1), "minus"); ((RectTransform)minus.transform).At(new Vector2(0, 0), new Vector2(44, 44), new Vector2(64, 64));
                var plus = Btn(c.transform, "", col, 64, 64, () => OnLevel?.Invoke(idx, +1), "plus"); ((RectTransform)plus.transform).At(new Vector2(1, 0), new Vector2(-44, 44), new Vector2(64, 64));
                sysCards.Add((c.rectTransform, eff, pips, minus, plus, state, glow));
            }

            buildBtn = Btn(d, "BUILD", K.Shield, 250, 82, OpenShop, "build", 32); ((RectTransform)buildBtn.transform).Set(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -20), new Vector2(250, 82));
            nextBtn = Btn(d, "NEXT SOL", K.Go, 250, 112, () => OnNextSol?.Invoke(), "play", 36); ((RectTransform)nextBtn.transform).Set(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 20), new Vector2(250, 112));
        }

        void BuildRovy()
        {
            rovy = Layer(Root, "Rovy"); rovy.alpha = 0; rovy.blocksRaycasts = false;
            rovyRT = UIX.Go("Box", rovy.transform).RT(); rovyRT.Set(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 300), new Vector2(760, 170));
            var av = IconCircle(rovyRT, "robot", K.Orange, 130); av.rectTransform.At(new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(130, 130));
            var ring = UIX.Img(av.transform, "Ring", Color.white, UISprites.Ring); ring.rectTransform.Fill(-6);
            var name = UIX.Img(rovyRT, "Name", K.Ink, UISprites.Round); name.rectTransform.At(new Vector2(0, 0), new Vector2(18, -6), new Vector2(110, 34));
            var nt = T(name.transform, "ROVY", 22, Color.white, true, TextAlignmentOptions.Center); nt.rectTransform.Fill();
            var bub = Card(rovyRT, "Bubble"); bub.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(150, 0), Vector2.zero);
            bub.rectTransform.offsetMin = new Vector2(150, 0); bub.rectTransform.offsetMax = Vector2.zero;
            rovyText = T(bub.transform, "", 29, K.Ink, false, TextAlignmentOptions.MidlineLeft); rovyText.rectTransform.Fill(); rovyText.rectTransform.offsetMin = new Vector2(26, 10); rovyText.rectTransform.offsetMax = new Vector2(-150, -10);
            rovyText.fontStyle = FontStyles.Bold;
            rovyOk = Btn(bub.transform, "OK!", K.Go, 120, 64, () => rovyAck = true, null, 28); ((RectTransform)rovyOk.transform).At(new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(120, 64));
        }

        void BuildPopup()
        {
            popup = Layer(Root, "Popup"); popup.alpha = 0; popup.blocksRaycasts = false;
            var dim = UIX.Img(popup.transform, "Dim", new Color(0.05f, 0.03f, 0.08f, 0.55f), null, true); dim.rectTransform.Fill();
            popCard = Card(popup.transform, "Card").rectTransform; popCard.At(new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(860, 560));
            popIconBg = IconCircle(popCard, "warn", K.Orange, 150); popIconBg.rectTransform.Set(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(150, 150));
            popIcon = popIconBg.transform.GetChild(0).GetComponent<Image>();
            popTitle = T(popCard, "", 54, K.Ink, true, TextAlignmentOptions.Center); popTitle.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -88), new Vector2(-60, 70));
            popBody = T(popCard, "", 30, K.Ink, false, TextAlignmentOptions.Top); popBody.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(-90, 150));
            popDataBox = UIX.Img(popCard, "DataBox", new Color(0.9f, 0.95f, 1f), UISprites.Round).rectTransform;
            popDataBox.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(-80, 120));
            var tag = UIX.Img(popDataBox, "Tag", K.Blue, UISprites.Round); tag.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(230, 34));
            var tt = T(tag.transform, "REAL NASA DATA", 19, Color.white, true, TextAlignmentOptions.Center); tt.rectTransform.Fill();
            popData = T(popDataBox, "", 22, K.Ink, false, TextAlignmentOptions.MidlineLeft); popData.rectTransform.Fill(); popData.rectTransform.offsetMin = new Vector2(22, 8); popData.rectTransform.offsetMax = new Vector2(-22, -18);
            popButtons = UIX.Go("Buttons", popCard).RT(); popButtons.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(0, 80));
        }

        void BuildShop()
        {
            shop = Layer(Root, "Shop"); shop.alpha = 0; shop.blocksRaycasts = false;
            var dim = UIX.Img(shop.transform, "Dim", new Color(0.05f, 0.03f, 0.08f, 0.55f), null, true); dim.rectTransform.Fill();
            var card = Card(shop.transform, "Card"); card.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1500, 640));
            var hd = T(card.transform, "BUILD SHOP", 54, K.Ink, true, TextAlignmentOptions.Center); hd.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(0, 70));
            var sub = T(card.transform, "Find science stars with your rover. Mission Control sends you new parts!", 26, K.Soft, false, TextAlignmentOptions.Center);
            sub.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(0, 40));
            shopGrid = UIX.Go("Grid", card.transform).RT(); shopGrid.Set(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            shopGrid.offsetMin = new Vector2(40, 120); shopGrid.offsetMax = new Vector2(-40, -150);
            var close = Btn(card.transform, "CLOSE", K.Soft, 220, 70, () => Show(shop, false), "close", 26); ((RectTransform)close.transform).At(new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(220, 70));
        }

        void BuildData()
        {
            data = Layer(Root, "Data"); data.alpha = 0; data.blocksRaycasts = false;
            var dim = UIX.Img(data.transform, "Dim", new Color(0.05f, 0.03f, 0.08f, 0.6f), null, true); dim.rectTransform.Fill();
            var card = Card(data.transform, "Card"); card.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 900));
            var hd = T(card.transform, "REAL NASA DATA IN THIS GAME", 50, K.Ink, true, TextAlignmentOptions.Center); hd.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(0, 66));
            dataList = UIX.Go("List", card.transform).RT(); dataList.Set(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            dataList.offsetMin = new Vector2(50, 120); dataList.offsetMax = new Vector2(-50, -100);
            var close = Btn(card.transform, "CLOSE", K.Soft, 220, 70, () => { Show(data, false); UIState.Pop(); }, "close", 26); ((RectTransform)close.transform).At(new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(220, 70));
        }

        void BuildBanner()
        {
            banner = Layer(Root, "Banner"); banner.alpha = 0; banner.blocksRaycasts = false;
            bannerBig = T(banner.transform, "", 140, Color.white, true, TextAlignmentOptions.Center); bannerBig.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1600, 170));
            bannerSmall = T(banner.transform, "", 36, Color.white, true, TextAlignmentOptions.Center); bannerSmall.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1600, 50));
            var sh = bannerBig.gameObject.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0.4f, 0.1f, 0, 0.6f); sh.effectDistance = new Vector2(4, -4);
        }

        // ================= public API =================
        public void ShowTitle(bool on) => Show(title, on, 0.5f);
        public void ShowHUD(bool on) => Show(hud, on, 0.5f);

        public IEnumerator Banner(string big, string small, float hold = 1.2f)
        {
            bannerBig.text = big; bannerSmall.text = small;
            Tween.Run(0.5f, t => bannerBig.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Tween.Back(t)));
            yield return FadeG(banner, 1, 0.3f);
            yield return new WaitForSecondsRealtime(hold);
            yield return FadeG(banner, 0, 0.4f);
        }

        static IEnumerator FadeG(CanvasGroup g, float to, float d) { float f = g.alpha, t = 0; while (t < d) { t += Time.unscaledDeltaTime; g.alpha = Mathf.Lerp(f, to, t / d); yield return null; } g.alpha = to; }

        public void Refresh(SimState s, Preview p)
        {
            solBig.text = $"SOL {s.Sol}";
            solSmall.text = $"of {SimState.GoalSol} sols";
            var fl = s.FlareOn(s.Sol); var flT = s.FlareOn(s.Sol + 1);
            if (fl != null) { weather.text = $"<color={K.Hex(K.Bad)}>FLARE TONIGHT!</color>"; weatherIcon.sprite = Icons.Get("flare"); weatherIcon.color = K.Bad; }
            else if (s.StormToday) { weather.text = "Dust storm!"; weatherIcon.sprite = Icons.Get("dust"); weatherIcon.color = new Color(0.7f, 0.45f, 0.3f); }
            else if (flT != null) { weather.text = "Flare tomorrow!"; weatherIcon.sprite = Icons.Get("warn"); weatherIcon.color = K.Orange; }
            else { weather.text = "Sunny day"; weatherIcon.sprite = Icons.Get("sun"); weatherIcon.color = K.Power; }
            sunChip.text = NasaData.SunStatus(out _);

            SetChip("power", s.Battery / s.BatteryCap, $"{s.Battery:0}<size=60%>/{s.BatteryCap:0}</size>", p.Battery);
            SetChip("air", s.Air / 100f, $"{s.Air:0}%", p.Air);
            SetChip("food", s.Food / 100f, $"{s.Food:0}%", p.Food);
            SetChip("health", s.Health / 100f, $"{s.Health:0}%", p.Health);
            SetChip("science", 0, $"{s.Science:0}", p.Science);

            int made = Mathf.FloorToInt(p.PowerMade + 0.01f), used = (int)p.PowerUsed;
            bool over = p.PowerUsed > p.PowerMade + 0.01f;
            powerNum.text = $"<color={(over ? K.Hex(K.Bad) : K.Hex(K.Ink))}>{used}</color> / {made}";
            powerSub.text = over
                ? (p.Short ? $"<color={K.Hex(K.Bad)}><b>Not enough power!</b></color> Machines will slow down." : $"Using <b>{p.PowerUsed - p.PowerMade:0}</b> from the battery.")
                : (s.StormToday ? "Dust storm: panels make less power." : $"Extra <b>{p.PowerMade - p.PowerUsed:0}</b> goes to the battery.");
            // slots coloured by system
            int k = 0;
            for (int i = 0; i < 4; i++) for (int n = 0; n < s.Level[i] * Rules.Cost[i]; n++) { if (k < slots.Count) { slots[k].color = k < made ? K.SysColor(i) : K.Bad; k++; } }
            for (; k < slots.Count; k++) slots[k].color = k < made ? new Color(1f, 0.92f, 0.6f) : new Color(0.9f, 0.91f, 0.94f, k < Mathf.Max(made, 12) ? 1 : 0.3f);

            for (int i = 0; i < 4; i++)
            {
                var c = sysCards[i]; int L = s.Level[i];
                for (int j = 0; j < 2; j++) c.pips[j].color = j < L ? K.SysColor(i) : new Color(0.85f, 0.87f, 0.9f);
                c.state.text = (L == 0 ? "OFF" : L == 1 ? $"<color={K.Hex(K.SysColor(i))}>ON</color>" : $"<color={K.Hex(K.SysColor(i))}>MAX</color>") + $"\n<size=62%><color={K.Hex(K.Soft)}>{(L == 0 ? Rules.Cost[i] + " power" : "uses " + Rules.Cost[i] * L)}</color></size>";
                c.minus.Interactable = L > 0; c.plus.Interactable = L < 2;
                string eff;
                switch (i)
                {
                    case 0: eff = L == 0 ? $"<color={K.Hex(K.Bad)}>No new air!</color>" : $"+{Rules.Gain[0][L]:0} air"; break;
                    case 1: eff = !s.HasDrill ? (L > 0 ? $"<color={K.Hex(K.Bad)}>Needs water!</color>" : "Needs the Ice Drill") : L == 0 ? $"<color={K.Hex(K.Bad)}>No new food!</color>" : $"+{Rules.Gain[1][L]:0} food"; break;
                    case 2: eff = L == 0 ? "No shield" : $"Blocks {Rules.Gain[2][L] * 100:0}% radiation"; break;
                    default: eff = L == 0 ? "Parked" : $"+{Rules.Gain[3][L]:0} stars"; break;
                }
                c.effect.text = eff;
            }
        }

        void SetChip(string id, float fill, string val, float delta)
        {
            var c = chips[id];
            c.bar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fill), 1);
            c.bar.enabled = fill > 0.005f;
            c.val.text = val;
            int d = Mathf.RoundToInt(delta);
            c.delta.text = d == 0 ? "" : d > 0 ? $"<color={K.Hex(K.Go)}>+{d}</color>" : $"<color={K.Hex(K.Bad)}>{d}</color>";
            if (id != "science" && id != "power" && fill < 0.25f) c.val.color = Color.Lerp(K.Ink, K.Bad, Mathf.PingPong(Time.unscaledTime * 2, 1)); else c.val.color = K.Ink;
        }

        public void HighlightSys(int i)
        {
            for (int k = 0; k < sysCards.Count; k++)
            {
                var g = sysCards[k].glow; bool on = k == i; var col = K.SysColor(k);
                g.color = new Color(col.r, col.g, col.b, on ? 1 : 0);
                sysCards[k].plus.Pulse(on);
            }
        }
        public void PulseNext(bool on) => nextBtn.Pulse(on);
        public void PulseBuild(bool on) => buildBtn.Pulse(on);
        public void SetNextEnabled(bool on) { nextBtn.Interactable = on; }

        public RectTransform ChipRect(string id) => chips[id].rt;

        // ---- Rovy ----
        public IEnumerator Rovy(string text, bool waitOk = true, float autoHide = 0)
        {
            int my = ++rovyId; rovyText.text = text; rovyAck = false;
            rovyOk.gameObject.SetActive(waitOk);
            rovy.blocksRaycasts = true;
            Sfx.Play("notify", 0.5f);
            Tween.Run(0.4f, t => rovyRT.anchoredPosition = new Vector2(Mathf.Lerp(-200, 30, Tween.Back(t)), 300));
            Tween.Fade(rovy, 1, 0.25f);
            rovyText.maxVisibleCharacters = 0; rovyText.ForceMeshUpdate();
            int n = rovyText.textInfo.characterCount;
            for (int i = 0; i <= n; i += 2) { rovyText.maxVisibleCharacters = i; if (i % 6 == 0) Sfx.Play("type", 0.15f); yield return null; }
            rovyText.maxVisibleCharacters = 99999;
            if (waitOk) { while (!rovyAck && !Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.Space)) yield return null; HideRovy(); }
            else if (autoHide > 0) { yield return new WaitForSecondsRealtime(autoHide); if (my == rovyId) HideRovy(); }
        }
        int rovyId;
        public void HideRovy() { rovy.blocksRaycasts = false; Tween.Fade(rovy, 0, 0.25f); }

        // ---- Popup ----
        public IEnumerator Popup(string icon, Color iconColor, string title, string body, string realData, string[] buttons, Color[] btnColors, Action<int> result)
        {
            UIState.Push();
            popIcon.sprite = Icons.Get(icon); popIconBg.color = iconColor;
            popTitle.text = title; popBody.text = body;
            popDataBox.gameObject.SetActive(!string.IsNullOrEmpty(realData)); popData.text = realData ?? "";
            foreach (Transform c in popButtons) Destroy(c.gameObject);
            popChoice = -1;
            float bw = buttons.Length > 1 ? 340 : 360;
            for (int i = 0; i < buttons.Length; i++)
            {
                int idx = i;
                var b = Btn(popButtons, buttons[i], btnColors != null && i < btnColors.Length ? btnColors[i] : K.Go, bw, 84, () => popChoice = idx, null, 30);
                ((RectTransform)b.transform).At(new Vector2(0.5f, 0.5f), new Vector2((i - (buttons.Length - 1) / 2f) * (bw + 30), 0), new Vector2(bw, 84));
            }
            popBody.ForceMeshUpdate();
            float h = 300 + Mathf.Max(60, popBody.preferredHeight) + (string.IsNullOrEmpty(realData) ? 0 : 150);
            popCard.sizeDelta = new Vector2(860, h);
            popBody.rectTransform.sizeDelta = new Vector2(-90, popBody.preferredHeight + 10);
            Show(popup, true, 0.25f);
            Tween.Run(0.45f, t => popCard.localScale = Vector3.one * Mathf.Lerp(0.7f, 1, Tween.Back(t)));
            Sfx.Play("whoosh", 0.4f, 1.3f);
            while (popChoice < 0) { if (buttons.Length == 1 && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))) popChoice = 0; yield return null; }
            Show(popup, false, 0.2f);
            UIState.Pop();
            result?.Invoke(popChoice);
            yield return new WaitForSecondsRealtime(0.2f);
        }

        // ---- floating numbers ----
        public void Float(Vector3 world, string text, Color c)
        {
            var cam = Game.Cam; var sp = cam.WorldToScreenPoint(world); if (sp.z < 0) return;
            float s = Mathf.Pow(2, Mathf.Lerp(Mathf.Log(1920f / Screen.width, 2), Mathf.Log(1080f / Screen.height, 2), 0.5f));
            var bg = UIX.Img(floatLayer, "F", Color.white, UISprites.Round); bg.rectTransform.sizeDelta = new Vector2(150, 54);
            var t = T(bg.transform, text, 34, c, true, TextAlignmentOptions.Center); t.rectTransform.Fill(); t.enableWordWrapping = false;
            var start = new Vector2(sp.x * s, sp.y * s);
            bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = Vector2.zero; bg.rectTransform.anchoredPosition = start;
            var g = bg.Group();
            Tween.Run(2.2f, k => { if (!bg) return; bg.rectTransform.anchoredPosition = start + new Vector2(0, 90 * Tween.EaseOut(k)); g.alpha = k < 0.75f ? 1 : 1 - (k - 0.75f) / 0.25f; bg.transform.localScale = Vector3.one * (k < 0.15f ? Tween.Back(k / 0.15f) : 1); },
                () => { if (bg) Destroy(bg.gameObject); });
        }

        // ---- shop ----
        SimState shopState;
        public void BindShop(SimState s) { shopState = s; }
        void OpenShop()
        {
            if (shopState == null) return;
            RefreshShop();
            Show(shop, true, 0.25f);
        }
        public void RefreshShop()
        {
            foreach (Transform c in shopGrid) Destroy(c.gameObject);
            var bs = Rules.Builds;
            for (int i = 0; i < bs.Length; i++)
            {
                var b = bs[i];
                var c = UIX.Img(shopGrid, "Item", new Color(0.95f, 0.96f, 0.98f), UISprites.Round, true);
                c.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * 286, 0), new Vector2(270, 360));
                var ic = IconCircle(c.transform, b.Icon, i == 0 ? K.Air : i == 1 ? K.Power : i == 2 ? K.Go : i == 3 ? K.Shield : new Color(0.7f, 0.45f, 0.3f), 100);
                ic.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(100, 100));
                var n = T(c.transform, b.Name, 30, K.Ink, true, TextAlignmentOptions.Center); n.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -126), new Vector2(-10, 40));
                var w = T(c.transform, b.What, 21, K.Soft, false, TextAlignmentOptions.Center); w.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(-24, 70));
                string cost = b.Stars > 0 ? $"<color={K.Hex(K.Star)}>{b.Stars} stars</color>" : $"<color={K.Hex(K.Power)}>{b.PowerNow} power</color>";
                var co = T(c.transform, "Cost: " + cost, 24, K.Ink, true, TextAlignmentOptions.Center); co.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 92), new Vector2(0, 32));
                bool can = Rules.CanBuild(shopState, b, out string why);
                var bt = Btn(c.transform, can ? "BUILD" : why, can ? K.Go : K.Soft, 220, 64, () => { if (Rules.CanBuild(shopState, b, out _)) { OnBuild?.Invoke(b); RefreshShop(); } }, null, 24);
                bt.Interactable = can;
                ((RectTransform)bt.transform).At(new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(220, 64));
            }
        }
        public void CloseShop() => Show(shop, false);

        // ---- data screen ----
        public void OpenData()
        {
            foreach (Transform c in dataList) Destroy(c.gameObject);
            bool live; string sun = NasaData.SunStatus(out live);
            var api = NasaApi.I;
            string flareLine = live && api != null && api.LatestFlare != null
                ? $"<b>LIVE right now:</b> {api.FlareCount30d} flares in the last 30 days. Latest big one: class {api.LatestFlare.classType}."
                : "<b>Offline:</b> using archived real flares, e.g. the X9.0 flare of 3 Oct 2024 (strongest of this solar cycle).";
            var rows = new (string icon, Color c, string name, string what)[]
            {
                ("flare", K.Bad, "NASA DONKI — Space Weather Database", $"Every solar flare in the game is a REAL flare recorded by NASA. Bigger flare class (M, X) = more radiation for your crew.\n{flareLine}"),
                ("shield", K.Shield, "Curiosity rover — RAD radiation detector", "Curiosity measured about 0.67 mSv of radiation per sol on the Mars surface. That is why your crew loses a little health every sol without a shield."),
                ("air", K.Air, "Perseverance rover — MOXIE experiment", "MOXIE turned Mars carbon dioxide into oxygen: 122 g in total, up to 12 g per hour. Your AIR MAKER works the same way."),
                ("food", K.Food, "Phoenix lander — Mars soil chemistry", "Phoenix found perchlorate salts in Mars soil (2008). That is why plants need clean water from ice, not Mars dirt."),
                ("dust", new Color(0.7f, 0.45f, 0.3f), "Opportunity rover & Mars climate", "A planet-wide dust storm in 2018 blocked sunlight and ended Opportunity's solar-powered mission. Storms in the game cut your solar power."),
                ("clock", K.Blue, "Mars facts (NASA)", "1 sol = 24 h 39 min. Sunlight is ~43% of Earth's. Radio to Earth takes 3–22 minutes."),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                var row = UIX.Img(dataList, "Row", new Color(0.95f, 0.96f, 0.98f), UISprites.Round);
                row.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -i * 110), new Vector2(0, 100));
                var ic = IconCircle(row.transform, r.icon, r.c, 70); ic.rectTransform.At(new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(70, 70));
                var n = T(row.transform, r.name, 26, K.Ink, true); n.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(110, -6), new Vector2(-130, 34));
                var w = T(row.transform, r.what, 19, K.Soft); w.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(110, -38), new Vector2(-130, 60));
            }
            UIState.Push();
            Show(data, true, 0.25f);
        }
    }

    /// <summary>Keeps a shadow image glued under its card.</summary>
    public class FollowRect : MonoBehaviour
    {
        public RectTransform Target; RectTransform me;
        void LateUpdate()
        {
            if (!Target) { Destroy(gameObject); return; }
            if (!me) me = (RectTransform)transform;
            me.anchorMin = Target.anchorMin; me.anchorMax = Target.anchorMax; me.pivot = Target.pivot;
            me.anchoredPosition = Target.anchoredPosition + new Vector2(0, -6); me.sizeDelta = Target.sizeDelta + new Vector2(4, 4);
            me.localScale = Target.localScale;
        }
    }
}
