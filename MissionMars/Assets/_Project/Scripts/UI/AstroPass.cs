using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionMars
{
    /// <summary>ASTRONAUT PASS: every mission gives stars. Stars raise your rank from CADET to COMMANDER,
    /// and every new rank unlocks a reward you can see in the game (NOVA's gold ring, rover TURBO, NOVA's party light).</summary>
    public class AstroPass : MonoBehaviour
    {
        public static AstroPass I;
        public int Stars { get; private set; }
        readonly HashSet<string> done = new HashSet<string>();

        static readonly (string name, int need, string reward)[] Ranks =
        {
            ("CADET", 0, ""),
            ("EXPLORER", 5, "NOVA got a shiny GOLD ring!"),
            ("PILOT", 11, "ROVER TURBO unlocked! Hold SHIFT while you drive."),
            ("ENGINEER", 17, "NOVA got a RAINBOW party light!"),
            ("COMMANDER", 23, "You are a MARS COMMANDER, the best rank!"),
        };
        public int Rank { get { int r = 0; for (int i = 0; i < Ranks.Length; i++) if (Stars >= Ranks[i].need) r = i; return r; } }
        public string RankName => Ranks[Rank].name;

        RectTransform panel; TextMeshProUGUI rankTxt, starTxt, popTxt; RectTransform fill; CanvasGroup popGroup;
        CanvasGroup bannerGroup; RectTransform bannerRt; TextMeshProUGUI bannerRank, bannerReward;
        readonly Queue<(string title, int stars)> queue = new Queue<(string, int)>(); bool showing;

        public static AstroPass Create(Hud hud)
        {
            if (I) return I;
            I = hud.gameObject.AddComponent<AstroPass>(); I.Build(hud.Root); return I;
        }

        void Build(RectTransform root)
        {
            var amber = new Color(1f, 0.78f, 0.25f);
            // badge under the mission clock (top-left)
            var bg = Hud.Glass(root, "AstroPass", new Vector2(0, 1), new Vector2(40, -140), new Vector2(430, 86));
            panel = bg.rectTransform; panel.pivot = new Vector2(0, 1); panel.anchoredPosition = new Vector2(40, -140);
            var star = UIX.Img(panel, "Star", amber, Icons.Get("science")); star.rectTransform.Set(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 4), new Vector2(50, 50));
            var head = UIX.Text(panel, "ASTRONAUT PASS", 15, HC.Dim, R.Bold); head.characterSpacing = 6; head.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(80, -10), new Vector2(-100, 20));
            rankTxt = UIX.Text(panel, "CADET", 28, HC.Text, R.Bold); rankTxt.characterSpacing = 4; rankTxt.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(80, -30), new Vector2(-100, 34));
            starTxt = UIX.Text(panel, "0", 30, amber, R.Bold, TextAlignmentOptions.Right); starTxt.rectTransform.Set(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -24), new Vector2(120, 36));
            var track = UIX.Img(panel, "Track", new Color(1, 1, 1, 0.1f)); track.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(80, 12), new Vector2(-100, 8));
            var f = UIX.Img(track.transform, "Fill", amber); fill = f.rectTransform; fill.Set(Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            // "+2 stars" pop-up next to the badge
            popGroup = UIX.Go("PassPop", root).AddComponent<CanvasGroup>(); var prt = (RectTransform)popGroup.transform;
            prt.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(486, -150), new Vector2(560, 60));
            popTxt = UIX.Text(prt, "", 30, amber, R.Bold); popTxt.rectTransform.Fill(); popTxt.characterSpacing = 3;
            popGroup.alpha = 0;
            // big RANK UP banner in the middle of the screen
            bannerGroup = UIX.Go("RankUp", root).AddComponent<CanvasGroup>(); bannerRt = (RectTransform)bannerGroup.transform;
            bannerRt.At(new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(900, 210));
            var bbg = Hud.Glass(bannerRt, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.08f, 0.05f, 0.01f, 0.85f)); bbg.rectTransform.Fill();
            var bstar = UIX.Img(bannerRt, "Star", amber, Icons.Get("science")); bstar.rectTransform.Set(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(90, 90));
            var bt = UIX.Text(bannerRt, "RANK UP!", 26, amber, R.Bold, TextAlignmentOptions.Center); bt.characterSpacing = 12; bt.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(0, 34));
            bannerRank = UIX.Text(bannerRt, "", 56, HC.Text, R.Title, TextAlignmentOptions.Center); bannerRank.characterSpacing = 8; bannerRank.rectTransform.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -82), new Vector2(0, 70));
            bannerReward = UIX.Text(bannerRt, "", 28, HC.Text, R.Bold, TextAlignmentOptions.Center); bannerReward.rectTransform.Set(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(-40, 40));
            bannerGroup.alpha = 0;
            Refresh();
        }

        void Refresh()
        {
            int r = Rank;
            rankTxt.text = Ranks[r].name;
            starTxt.text = Stars.ToString();
            float k = r >= Ranks.Length - 1 ? 1 : Mathf.InverseLerp(Ranks[r].need, Ranks[r + 1].need, Stars);
            fill.anchorMax = new Vector2(k, 1);
        }

        /// <summary>Give stars for a finished mission (once per id).</summary>
        public static void Award(string id, string title, int stars)
        {
            if (!I || stars <= 0 || I.done.Contains(id)) return;
            I.done.Add(id);
            I.queue.Enqueue((title, stars));
            if (!I.showing) I.StartCoroutine(I.Show());
        }

        /// <summary>Count stars without any pop-up (used during the ending).</summary>
        public static void AwardSilent(string id, int stars)
        {
            if (!I || I.done.Contains(id)) return;
            I.done.Add(id); I.Stars += stars; I.Refresh();
        }

        IEnumerator Show()
        {
            showing = true;
            while (queue.Count > 0)
            {
                var (title, stars) = queue.Dequeue();
                int before = Rank;
                Stars += stars;
                popTxt.text = $"+{stars} STAR{(stars > 1 ? "S" : "")}  <color=#FFFFFF>{title.ToUpper()}</color>";
                Sfx.Play("success", 0.55f, 1.2f);
                var prt = (RectTransform)popGroup.transform;
                Tween.Fade(popGroup, 1, 0.2f);
                Tween.Run(0.35f, t => prt.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Tween.EaseOut(t)));
                Tween.Run(0.4f, t => panel.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * 0.08f));
                Refresh();
                yield return new WaitForSeconds(2.4f);
                Tween.Fade(popGroup, 0, 0.4f);
                if (Rank > before) yield return RankUp(Rank);
                yield return new WaitForSeconds(0.4f);
            }
            showing = false;
        }

        IEnumerator RankUp(int r)
        {
            bannerRank.text = Ranks[r].name;
            bannerReward.text = Ranks[r].reward;
            Sfx.Play("powerup", 0.7f); Sfx.Play("success", 0.8f);
            Tween.Fade(bannerGroup, 1, 0.25f);
            Tween.Run(0.5f, t => bannerRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, Tween.Back(t)));
            GiveReward(r);
            yield return new WaitForSeconds(3.8f);
            Tween.Fade(bannerGroup, 0, 0.5f);
            yield return new WaitForSeconds(0.5f);
        }

        static void GiveReward(int r)
        {
            switch (r)
            {
                case 1: if (Game.Bot) Game.Bot.GoldRing(); break;
                case 2: if (MarsWorld.TheRover) MarsWorld.TheRover.Turbo = true; break;
                case 3: if (Game.Bot) Game.Bot.Party = true; break;
            }
        }
    }
}
