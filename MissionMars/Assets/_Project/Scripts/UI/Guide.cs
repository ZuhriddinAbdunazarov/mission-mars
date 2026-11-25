using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    /// <summary>Kid-friendly helper for 7-12 year olds, so nobody gets lost:
    /// 1. a MISSION CHECKLIST ("MISSION 3 OF 8", short steps with ticks),
    /// 2. ONE big ARROW at the top of the screen that always points to the next place, with the distance,
    /// 3. a tall LIGHT BEAM over that place outside,
    /// 4. NOVA repeats what to do (and why) when you are stuck for a while, or when you press H.</summary>
    public class Guide : MonoBehaviour
    {
        public static Guide I;
        public const int Total = 8;
        /// <summary>Where the door out of the current room is (set by the story). Used when the next place is outside.</summary>
        public static Func<Vector3> Exit;

        // ---- state ----
        public int Num { get; private set; }
        public string Key { get; private set; }
        string title; readonly List<string> rows = new List<string>(); readonly List<bool> done = new List<bool>(); int focus = -1;
        string hint;
        Func<Vector3> target, door; string targetName;
        bool urgent; string uTitle, uRow, uHint; Func<Vector3> uTarget; string uName;
        float idle, nextHelp = 35;

        // ---- UI ----
        RectTransform panel; CanvasGroup panelGroup; TextMeshProUGUI head, titleTxt, foot;
        readonly List<(Image icon, TextMeshProUGUI txt)> rowUI = new List<(Image, TextMeshProUGUI)>();
        RectTransform arrow; Image arrowImg; TextMeshProUGUI arrowTxt; CanvasGroup arrowGroup;
        GameObject beam; Transform ring;
        bool dirty = true;

        public static Guide Create(Hud hud)
        {
            var g = hud.gameObject.AddComponent<Guide>(); I = g; g.Build(hud.Root); return g;
        }

        void Build(RectTransform root)
        {
            // checklist, top right (replaces the old one-line objective box)
            panel = Hud.Glass(root, "Mission", new Vector2(1, 1), new Vector2(-40, -36), new Vector2(540, 200)).rectTransform;
            panel.pivot = new Vector2(1, 1); panel.anchoredPosition = new Vector2(-40, -36);
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>(); panelGroup.alpha = 0;
            var acc = UIX.Img(panel, "Acc", HC.Amber); acc.rectTransform.Set(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, -24));
            head = UIX.Text(panel, "", 19, HC.Amber, R.Bold); head.characterSpacing = 6;
            head.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -14), new Vector2(-40, 26));
            titleTxt = UIX.Text(panel, "", 34, HC.Text, R.Kid);
            titleTxt.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -38), new Vector2(-40, 46));
            for (int i = 0; i < 7; i++)
            {
                var ic = UIX.Img(panel, "Tick" + i, HC.Dim, Icons.Get("dot"));
                ic.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -96 - i * 40), new Vector2(26, 26));
                var tx = UIX.Text(panel, "", 24, HC.Text, R.KidBody);
                tx.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(62, -92 - i * 40), new Vector2(-80, 36));
                rowUI.Add((ic, tx));
            }
            foot = UIX.Text(panel, "Stuck? Press  <b>H</b>  and NOVA helps", 17, HC.Dim, R.KidBody);
            foot.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(24, 10), new Vector2(-40, 24));

            // the big arrow, under the compass
            arrowGroup = UIX.Go("GuideArrow", root).AddComponent<CanvasGroup>(); var ag = (RectTransform)arrowGroup.transform;
            ag.At(new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(420, 150)); ag.pivot = new Vector2(0.5f, 1); ag.anchoredPosition = new Vector2(0, -92);
            var disc = UIX.Img(ag, "Disc", new Color(0.015f, 0.03f, 0.045f, 0.55f), UISprites.Circle);
            disc.rectTransform.Set(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(92, 92));
            arrowImg = UIX.Img(disc.transform, "Arrow", HC.Green, ArrowSprite());
            arrow = arrowImg.rectTransform; arrow.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70)); arrow.pivot = new Vector2(0.5f, 0.5f); arrow.anchoredPosition = Vector2.zero;
            arrowTxt = UIX.Text(ag, "", 26, HC.Text, R.Kid, TextAlignmentOptions.Center);
            arrowTxt.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(0, 40));
            arrowTxt.outlineWidth = 0.2f; arrowTxt.outlineColor = new Color32(0, 0, 0, 200);
            arrowGroup.alpha = 0;

            // the light beam over the next place (outside)
            beam = new GameObject("GuideBeam");
            var b = ProcMesh.Prim(PrimitiveType.Cylinder, "Beam", beam.transform, new Vector3(0, 40, 0), new Vector3(1.1f, 40, 1.1f), R.Holo(HC.Green, 1.3f, 0.3f, 1f), null, false);
            b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var r = ProcMesh.Prim(PrimitiveType.Cylinder, "Ring", beam.transform, new Vector3(0, 0.08f, 0), new Vector3(4.5f, 0.02f, 4.5f), R.Holo(HC.Green, 1.6f, 0.35f), null, false);
            r.AddComponent<Spin>().Speed = 60; ring = r.transform;
            beam.SetActive(false);
        }

        static Sprite arrowSprite;
        /// <summary>A fat arrow pointing up (drawn once, no image file needed).</summary>
        static Sprite ArrowSprite()
        {
            if (arrowSprite) return arrowSprite;
            const int N = 128, SS = 4; var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float u = ((x + (sx + 0.5f) / SS) / N) * 2 - 1, v = (y + (sy + 0.5f) / SS) / N;
                            bool head = v >= 0.44f && v <= 0.97f && Mathf.Abs(u) <= 0.9f * (0.97f - v) / 0.53f;
                            bool stem = v >= 0.04f && v < 0.46f && Mathf.Abs(u) <= 0.3f;
                            if (head || stem) hit++;
                        }
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(255 * hit / (SS * SS)));
                }
            t.SetPixels32(px); t.Apply();
            return arrowSprite = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ API used by the story
        /// <summary>Start mission number n (1..8) with its short steps. The first step is the current one.</summary>
        public void Mission(int n, string key, string missionTitle, params string[] steps)
        {
            Num = n; Key = key; title = missionTitle; rows.Clear(); done.Clear();
            foreach (var s in steps) { rows.Add(s); done.Add(false); }
            focus = 0; target = null; door = null; hint = null; urgent = false;
            Fresh(); Sfx.Play("notify", 0.5f);
            Bump(panel);
        }
        /// <summary>A simple panel without a number (e.g. "LOOK AROUND").</summary>
        public void Note(string noteTitle, string line, string help = null)
        {
            Num = 0; Key = null; title = noteTitle; rows.Clear(); done.Clear();
            if (!string.IsNullOrEmpty(line)) { rows.Add(line); done.Add(false); }
            focus = rows.Count > 0 ? 0 : -1; target = null; door = null; hint = help ?? line; urgent = false;
            Fresh(); Sfx.Play("notify", 0.5f);
        }
        /// <summary>Tick step i as done.</summary>
        public void Done(int i) { if (i >= 0 && i < done.Count && !done[i]) { done[i] = true; Fresh(); Sfx.Play("click", 0.6f, 1.4f); } }
        /// <summary>Make step i the current one; say WHAT to do and WHY (NOVA repeats it if you get stuck).</summary>
        public void Focus(int i, string help, Func<Vector3> where = null, string whereName = null, Func<Vector3> doorTo = null, bool tickBefore = true)
        {
            if (tickBefore) for (int k = 0; k < i && k < done.Count; k++) if (!done[k]) { done[k] = true; dirty = true; }
            bool changed = focus != i || hint != help;
            focus = i; hint = help; target = where; targetName = whereName; door = doorTo;
            if (changed) { Fresh(); idle = 0; nextHelp = 35; }
        }
        /// <summary>Only change where the arrow points.</summary>
        public void Point(Func<Vector3> where, string whereName, Func<Vector3> doorTo = null) { target = where; targetName = whereName; door = doorTo; }
        public void Complete() { for (int k = 0; k < done.Count; k++) done[k] = true; focus = -1; target = null; hint = null; Fresh(); }
        public void Hide() { Num = 0; Key = null; title = null; rows.Clear(); done.Clear(); target = null; hint = null; urgent = false; Fresh(); }
        public string Hint => urgent ? uHint : hint;

        /// <summary>A danger interrupts the mission (red title). EndUrgent() goes back to the mission.</summary>
        public void Urgent(string t, string row, string help, Func<Vector3> where = null, string whereName = null)
        {
            urgent = true; uTitle = t; uRow = row; uHint = help; uTarget = where; uName = whereName; Fresh(); idle = 0; nextHelp = 25;
        }
        public void EndUrgent() { if (!urgent) return; urgent = false; Fresh(); }

        static void Bump(RectTransform rt) => Tween.Run(0.5f, t => rt.anchoredPosition = new Vector2(Mathf.Lerp(20, -40, Tween.EaseOut(t)), -36));

        void Fresh() { dirty = true; }

        void Redraw()
        {
            dirty = false;
            bool show = urgent || !string.IsNullOrEmpty(title);
            Tween.Fade(panelGroup, show ? 1 : 0, 0.3f);
            if (!show) return;
            if (urgent)
            {
                head.text = "DANGER!"; head.color = HC.Red; titleTxt.text = uTitle; titleTxt.color = new Color(1f, 0.75f, 0.7f);
                for (int i = 0; i < rowUI.Count; i++)
                {
                    bool on = i == 0; rowUI[i].icon.gameObject.SetActive(on); rowUI[i].txt.gameObject.SetActive(on);
                    if (on) { rowUI[i].icon.sprite = Icons.Get("warn"); rowUI[i].icon.color = HC.Red; rowUI[i].txt.text = uRow; rowUI[i].txt.color = HC.Text; rowUI[i].txt.fontStyle = FontStyles.Bold; }
                }
                Size(1); return;
            }
            head.text = Num > 0 ? $"MISSION {Num} OF {Total}" : "YOUR JOB"; head.color = HC.Amber;
            titleTxt.text = title; titleTxt.color = HC.Text;
            for (int i = 0; i < rowUI.Count; i++)
            {
                bool on = i < rows.Count; rowUI[i].icon.gameObject.SetActive(on); rowUI[i].txt.gameObject.SetActive(on);
                if (!on) continue;
                var (ic, tx) = rowUI[i];
                if (done[i]) { ic.sprite = Icons.Get("check"); ic.color = HC.Green; tx.text = rows[i]; tx.color = new Color(0.55f, 0.85f, 0.6f); tx.fontStyle = FontStyles.Strikethrough; }
                else if (i == focus) { ic.sprite = Icons.Get("play"); ic.color = HC.Amber; tx.text = rows[i]; tx.color = Color.white; tx.fontStyle = FontStyles.Bold; }
                else { ic.sprite = Icons.Get("dot"); ic.color = HC.Dim; tx.text = rows[i]; tx.color = HC.Dim; tx.fontStyle = FontStyles.Normal; }
            }
            Size(rows.Count);
        }
        void Size(int n) => panel.sizeDelta = new Vector2(540, 96 + Mathf.Max(1, n) * 40 + 40);

        // ------------------------------------------------------------------
        void Update()
        {
            if (dirty) Redraw();
            var p = Game.Player; var cam = Game.Cam; var hud = Game.Hud;
            bool play = p && cam && !UIState.Cinematic && !UIState.Modal && !Game.InMenu;

            // where the arrow points: the target, or the door that leads to it
            Func<Vector3> tgt = urgent ? uTarget : target; string name = urgent ? uName : targetName;
            Vector3 tp = Vector3.zero; bool has = play && tgt != null;
            if (has)
            {
                tp = tgt();
                bool otherSpace = Mathf.Abs(tp.y - p.transform.position.y) > 25f; // rooms are built far below the surface
                if (otherSpace)
                {
                    bool insideNow = Game.Suit && Game.Suit.Inside;
                    if (insideNow && Exit != null) { tp = Exit(); name = "EXIT DOOR"; }
                    else if (!insideNow && door != null) { tp = door(); }
                    else has = false;
                    if (has && Mathf.Abs(tp.y - p.transform.position.y) > 25f) has = false;
                }
            }
            float dist = has ? Vector3.Distance(Flat(p.transform.position), Flat(tp)) : 0;
            arrowGroup.alpha = Mathf.MoveTowards(arrowGroup.alpha, has ? 1 : 0, Time.unscaledDeltaTime * 4);
            if (has)
            {
                var f = cam.transform.forward; f.y = 0; var d = tp - cam.transform.position; d.y = 0;
                float ang = Vector3.SignedAngle(f, d, Vector3.up);
                arrow.localEulerAngles = new Vector3(0, 0, -ang);
                bool here = dist < 3.5f;
                float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 5);
                arrowImg.color = (here ? HC.Amber : Mathf.Abs(ang) < 20 ? HC.Green : Color.Lerp(HC.Green, HC.Amber, 0.4f)) * new Color(1, 1, 1, pulse);
                arrowTxt.text = here ? $"{name}: HERE! Press <b>E</b>" : $"{name}  <color=#B8FFC8>{dist:0} m</color>";
            }

            // the beam over the place (only outside, not too close)
            bool beamOn = has && !(Game.Suit && Game.Suit.Inside) && dist > 7f;
            if (beam.activeSelf != beamOn) beam.SetActive(beamOn);
            if (beamOn) beam.transform.position = new Vector3(tp.x, MarsTerrain.H(tp.x, tp.z), tp.z);

            // NOVA helps when you are stuck (or when you press H)
            if (!hud || string.IsNullOrEmpty(Hint)) return;
            bool canTalk = play && !p.Frozen && !hud.Talking;
            if (canTalk) idle += Time.deltaTime;
            if (canTalk && Input.GetKeyDown(KeyCode.H)) { hud.Say(Hint, 6); idle = 0; }
            else if (canTalk && idle > nextHelp) { hud.Say(Hint, 6); idle = 0; nextHelp = 45; }
        }
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
