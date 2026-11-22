using System.Collections.Generic;

namespace MissionMars
{
    /// <summary>Real NASA facts and datasets used by the game. Everything here is shown to players in the NASA DATA screen.</summary>
    public static class NasaData
    {
        /// <summary>Archived real solar flares from NASA DONKI (used when there is no internet). Clearly labelled "archived" in game.</summary>
        public static readonly FlareEvent[] Archive =
        {
            new FlareEvent { Class = "X9.0", Date = "2024-10-03" },
            new FlareEvent { Class = "X8.7", Date = "2024-05-14" },
            new FlareEvent { Class = "X7.1", Date = "2024-10-01" },
            new FlareEvent { Class = "X6.3", Date = "2024-02-22" },
            new FlareEvent { Class = "X5.8", Date = "2024-05-11" },
        };

        public static List<FlareEvent> FlarePool()
        {
            var list = new List<FlareEvent>();
            var api = NasaApi.I;
            if (api != null && api.AllFlares != null && api.AllFlares.Length > 0) // live, or saved at build time
            {
                foreach (var f in api.AllFlares)
                {
                    if (string.IsNullOrEmpty(f.classType)) continue;
                    if (!(f.classType.StartsWith("X") || f.classType.StartsWith("M"))) continue;
                    string d = f.peakTime != null && f.peakTime.Length >= 10 ? f.peakTime.Substring(0, 10) : "";
                    list.Add(new FlareEvent { Class = f.classType, Date = d, Live = api.FlaresLive });
                }
            }
            if (list.Count < 3) list.AddRange(Archive);
            return list;
        }

        public static string SunStatus(out bool live)
        {
            var api = NasaApi.I; live = api != null && api.FlaresLive;
            if (api == null || api.AllFlares == null || api.AllFlares.Length == 0) return "Sun data: archived";
            int big = 0; foreach (var f in api.AllFlares) if (f.classType != null && (f.classType.StartsWith("X") || f.classType.StartsWith("M"))) big++;
            return big >= 8 ? $"Real Sun: VERY ACTIVE ({big} big flares this month)" : big >= 2 ? $"Real Sun: active ({big} big flares this month)" : "Real Sun: calm this month";
        }

        // short, kid-friendly facts (each one is real NASA science)
        public const string MoxieFact = "Real NASA tech! MOXIE on the Perseverance rover made oxygen from Mars air — 122 grams in total.";
        public const string FarmFact = "Mars dirt has a salt called perchlorate that is bad for us. NASA's Phoenix lander found it in 2008. So we grow plants in water instead!";
        public const string ShieldFact = "Mars has no magnetic shield like Earth. NASA's Curiosity rover measured about 0.67 mSv of radiation every sol — much more than on Earth.";
        public const string RoverFact = "Rovers explore for us. Perseverance has driven over 30 km and collects rock samples for scientists.";
        public const string SolarFact = "Mars gets only about 43% of the sunlight Earth gets. Dust on panels makes it even less!";
        public const string StormFact = "Real! In 2018 a giant dust storm covered all of Mars and stopped NASA's Opportunity rover, which ran on solar power.";
        public const string SolFact = "A Mars day is called a SOL. It lasts 24 hours and 39 minutes.";
        public const string DelayFact = "Radio messages take 3 to 22 minutes to travel between Mars and Earth.";
        public const string WaterFact = "Mars has lots of ice under the ground. NASA's orbiters found it with radar.";
    }
}

namespace MissionMars
{
    public class FlareEvent
    {
        public string Class = "M5.0"; public string Date = ""; public bool Live;
        public string Size => Class.StartsWith("X") ? "EXTREME" : Class.StartsWith("M") ? "STRONG" : "minor";
        /// <summary>Radiation multiplier for the storm: X flares hit hardest.</summary>
        public float Strength { get { float n = 1; if (Class.Length > 1) float.TryParse(Class.Substring(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out n); return Class.StartsWith("X") ? 1.0f + n * 0.05f : Class.StartsWith("M") ? 0.7f + n * 0.03f : 0.5f; } }
    }
}
