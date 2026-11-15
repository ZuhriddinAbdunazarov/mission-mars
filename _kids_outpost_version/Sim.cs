using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    public enum Sys { Air = 0, Farm = 1, Shield = 2, Rover = 3 }

    /// <summary>A solar flare (from NASA DONKI — live or archived).</summary>
    public class FlareEvent
    {
        public string Class = "M5.0"; public string Date = ""; public bool Live;
        public int Damage => Class.StartsWith("X") ? 34 : Class.StartsWith("M") ? 22 : 12;
        public string Size => Class.StartsWith("X") ? "HUGE" : Class.StartsWith("M") ? "BIG" : "small";
    }

    /// <summary>The whole outpost state. Small and cloneable so kids can "try again" from an earlier sol.</summary>
    public class SimState
    {
        public const int GoalSol = 20;
        public int Sol = 1;
        public float Air = 70, Food = 60, Health = 100, Battery = 10, Science = 0;
        public float BatteryCap = 20;
        public int Panels = 3;
        public float Dust;            // 0..1 dust on solar panels
        public bool HasDrill;
        public int ShieldWalls;       // permanent regolith walls 0..2
        public int[] Level = new int[4];
        public int StormStart, StormLeft;
        public List<int> FlareSols = new List<int>();
        public List<FlareEvent> Flares = new List<FlareEvent>();
        public int Seed;
        public bool SawPerchlorate;
        public float DoseMSv;         // total crew dose (for the data screen)

        public bool StormToday => StormLeft > 0;
        public FlareEvent FlareOn(int sol) { int i = FlareSols.IndexOf(sol); return i >= 0 && i < Flares.Count ? Flares[i] : null; }

        public SimState Clone()
        {
            var c = (SimState)MemberwiseClone();
            c.Level = (int[])Level.Clone();
            c.FlareSols = new List<int>(FlareSols); c.Flares = new List<FlareEvent>(Flares);
            return c;
        }
    }

    public struct Preview
    {
        public float PowerMade, PowerUsed, Air, Food, Health, Science, Battery;
        public bool Short; // not enough power
    }

    public class SolReport
    {
        public Preview P; public FlareEvent Flare; public bool Storm; public float RadDamage;
        public string LoseReason;
    }

    public static class Rules
    {
        // power cost per level, and what each level gives
        public static readonly int[] Cost = { 3, 3, 2, 3 };
        public static readonly float[][] Gain =
        {
            new[] { 0f, 10f, 17f },  // Air (oxygen) per sol
            new[] { 0f, 8f, 14f },   // Food per sol
            new[] { 0f, 0.5f, 0.85f }, // Shield: fraction of radiation blocked
            new[] { 0f, 6f, 10f },   // Rover science stars
        };
        public const float AirUse = 12, FoodUse = 9, BaseRad = 2f;
        public const float PanelPower = 4;

        public static float PowerMade(SimState s) => s.Panels * PanelPower * (1 - s.Dust) * (s.StormToday ? 0.35f : 1f);
        public static int PowerUsed(SimState s) { int u = 0; for (int i = 0; i < 4; i++) u += s.Level[i] * Cost[i]; return u; }

        public static float ShieldBlock(SimState s) => Mathf.Clamp01(Gain[2][s.Level[2]] + s.ShieldWalls * 0.2f);

        /// <summary>What tomorrow looks like with the current plan (shown live to the player).</summary>
        public static Preview Predict(SimState s, out float ratio)
        {
            var p = new Preview();
            p.PowerMade = PowerMade(s); p.PowerUsed = PowerUsed(s);
            float avail = p.PowerMade + s.Battery;
            ratio = p.PowerUsed <= 0 ? 1 : Mathf.Clamp01(avail / p.PowerUsed);
            p.Short = ratio < 0.999f;
            p.Battery = Mathf.Clamp(s.Battery + p.PowerMade - p.PowerUsed, 0, s.BatteryCap) - s.Battery;
            float farm = s.HasDrill ? Gain[1][s.Level[1]] : 0;
            p.Air = Gain[0][s.Level[0]] * ratio - AirUse;
            p.Food = farm * ratio - FoodUse;
            p.Science = Gain[3][s.Level[3]] * ratio * (s.StormToday ? 0.5f : 1);
            var fl = s.FlareOn(s.Sol);
            float rad = BaseRad + (fl != null ? fl.Damage : 0);
            float block = Mathf.Clamp01((Gain[2][s.Level[2]] * ratio) + s.ShieldWalls * 0.2f);
            float hp = -rad * (1 - block);
            if (s.Air + p.Air < 20) hp -= 6;
            if (s.Food + p.Food < 20) hp -= 6;
            if (s.Air + p.Air > 40 && s.Food + p.Food > 40) hp += 4;
            p.Health = hp;
            return p;
        }

        public static SolReport Step(SimState s)
        {
            var r = new SolReport();
            r.P = Predict(s, out float ratio);
            r.Flare = s.FlareOn(s.Sol); r.Storm = s.StormToday;
            float block = Mathf.Clamp01((Gain[2][s.Level[2]] * ratio) + s.ShieldWalls * 0.2f);
            r.RadDamage = (BaseRad + (r.Flare != null ? r.Flare.Damage : 0)) * (1 - block);
            s.DoseMSv += 0.67f * (1 - block) + (r.Flare != null ? r.Flare.Damage * 2f * (1 - block) : 0);

            s.Battery = Mathf.Clamp(s.Battery + r.P.PowerMade - r.P.PowerUsed, 0, s.BatteryCap);
            s.Air = Mathf.Clamp(s.Air + r.P.Air, 0, 100);
            s.Food = Mathf.Clamp(s.Food + r.P.Food, 0, 100);
            s.Health = Mathf.Clamp(s.Health + r.P.Health, 0, 100);
            s.Science += r.P.Science;
            if (s.StormLeft > 0) { s.StormLeft--; s.Dust = Mathf.Min(0.6f, s.Dust + 0.2f); }

            if (s.Air <= 0) r.LoseReason = "air";
            else if (s.Food <= 0) r.LoseReason = "food";
            else if (s.Health <= 0) r.LoseReason = "health";

            s.Sol++;
            if (s.Sol == s.StormStart) s.StormLeft = 3;
            return r;
        }

        /// <summary>New mission: storms and flares land on different sols every game (replayability).</summary>
        public static SimState NewMission(List<FlareEvent> flarePool, int seed)
        {
            var rng = new System.Random(seed);
            var s = new SimState { Seed = seed };
            s.StormStart = rng.Next(6, 11);
            var sols = new List<int>();
            int a = rng.Next(3, 6); sols.Add(a);
            int b = a + rng.Next(5, 8); sols.Add(b);
            int c = Mathf.Min(SimState.GoalSol - 1, b + rng.Next(4, 7)); if (c > b) sols.Add(c);
            s.FlareSols = sols;
            var pool = new List<FlareEvent>(flarePool);
            for (int i = 0; i < sols.Count; i++)
            {
                if (pool.Count == 0) pool.AddRange(flarePool);
                int k = rng.Next(pool.Count); s.Flares.Add(pool[k]); pool.RemoveAt(k);
            }
            return s;
        }

        // ---- one-off builds (paid with science stars: Earth sends parts for discoveries) ----
        public class Build { public string Id, Name, Icon, What; public int Stars, PowerNow; }
        public static readonly Build[] Builds =
        {
            new Build { Id = "drill", Name = "Ice Drill", Icon = "drill", What = "Digs up frozen water for plants.", Stars = 0, PowerNow = 6 },
            new Build { Id = "panel", Name = "Solar Panel", Icon = "sun", What = "+4 power every sol.", Stars = 12 },
            new Build { Id = "battery", Name = "Big Battery", Icon = "battery", What = "Stores +15 power for storms.", Stars = 8 },
            new Build { Id = "wall", Name = "Soil Wall", Icon = "shield", What = "Mars dirt blocks 20% of radiation. Forever!", Stars = 10 },
            new Build { Id = "clean", Name = "Clean Panels", Icon = "dust", What = "Wipe the dust off. More power!", Stars = 3 },
        };

        public static bool CanBuild(SimState s, Build b, out string why)
        {
            why = null;
            if (b.Id == "drill" && s.HasDrill) { why = "Built!"; return false; }
            if (b.Id == "panel" && s.Panels >= 6) { why = "Max"; return false; }
            if (b.Id == "wall" && s.ShieldWalls >= 2) { why = "Max"; return false; }
            if (b.Id == "battery" && s.BatteryCap >= 50) { why = "Max"; return false; }
            if (b.Id == "clean" && s.Dust < 0.05f) { why = "Clean"; return false; }
            if (s.Science < b.Stars) { why = "Need " + b.Stars + " stars"; return false; }
            if (s.Battery < b.PowerNow) { why = "Need " + b.PowerNow + " power"; return false; }
            return true;
        }

        public static void DoBuild(SimState s, Build b)
        {
            s.Science -= b.Stars; s.Battery -= b.PowerNow;
            switch (b.Id)
            {
                case "drill": s.HasDrill = true; break;
                case "panel": s.Panels++; break;
                case "battery": s.BatteryCap += 15; break;
                case "wall": s.ShieldWalls++; break;
                case "clean": s.Dust = 0; break;
            }
        }
    }
}
