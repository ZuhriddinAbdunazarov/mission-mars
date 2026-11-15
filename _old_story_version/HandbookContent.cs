namespace MissionMars
{
    public class Chapter
    {
        public string Title, Subtitle, Body, KeyFact;
    }

    /// <summary>"The Colonist &amp; Engineer's Field Handbook" — the in-game book. Facts are based on NASA mission data.</summary>
    public static class HandbookContent
    {
        const string H = "<color=#A83D14><b>";
        const string E = "</b></color>";

        public static readonly Chapter[] Chapters =
        {
            new Chapter {
                Title = "Welcome to Mars", Subtitle = "Know the planet that is now your home",
                Body =
$@"Mars is the {H}fourth planet from the Sun{E}, orbiting at an average of about 228 million km — one and a half times farther out than Earth.

<b>Size.</b> Mars is about {H}half the width of Earth{E} (radius 3,390 km).

<b>Gravity.</b> Surface gravity is {H}3.71 m/s², about 38% of Earth's{E}. A 50 kg astronaut feels like 19 kg. You can jump higher — but you also fall slower, so plan your landings.

<b>Time.</b> One Martian day is called a {H}sol{E} and lasts {H}24 hours 39 minutes{E}. A Martian year lasts {H}687 Earth days{E}.

<b>Moons.</b> Two small, potato-shaped moons: {H}Phobos{E} and {H}Deimos{E}.

<b>Landmarks.</b> Olympus Mons, the largest volcano in the Solar System, rises about 22 km — two and a half times higher than Mount Everest. The canyon system Valles Marineris is about 4,000 km long.

<b>Why red?</b> The dust is rich in {H}iron oxide{E} — rust.",
                KeyFact = "1 sol = 24 h 39 min.  Gravity = 38% of Earth's." },

            new Chapter {
                Title = "The Martian Air", Subtitle = "Thin, cold — and almost all carbon dioxide",
                Body =
$@"The atmosphere of Mars is about {H}95% carbon dioxide (CO<sub>2</sub>){E}, roughly 3% nitrogen and 2% argon. Oxygen is only about {H}0.17%{E} — you cannot breathe it.

<b>Pressure.</b> Surface pressure is less than {H}1% of Earth's{E}. Without a pressurised suit or habitat, the fluids in your body would begin to boil. Never open your helmet outside.

<b>Sky colours.</b> Fine reddish dust makes the daytime sky look {H}butterscotch{E}. At sunset the same dust scatters {H}blue light{E} forward toward you, so Martian sunsets glow blue — the opposite of Earth.

<b>Dust storms.</b> Local storms happen every year, and every few years a storm can wrap the entire planet. A global storm in 2018 covered the solar panels of the Opportunity rover and ended its mission.

<b>Use it!</b> Because the air is mostly CO<sub>2</sub>, it is a {H}resource{E}: machines can turn it into oxygen and even rocket fuel. See Chapters 5 and 8.",
                KeyFact = "Mars air = ~95% CO<sub>2</sub>. Pressure < 1% of Earth's." },

            new Chapter {
                Title = "Radiation & the Sun", Subtitle = "The invisible danger",
                Body =
$@"Earth is protected by a thick atmosphere and a {H}global magnetic field{E}. Mars lost its global magnetic field billions of years ago, and its atmosphere is very thin. Radiation from space reaches the ground almost unblocked.

<b>How much?</b> NASA's Curiosity rover measured about {H}0.6–0.7 millisieverts (mSv) per sol{E} on the surface. On Earth a person receives roughly 2–3 mSv in a whole year.

<b>Two sources.</b> {H}Galactic cosmic rays{E} arrive constantly from distant exploding stars. {H}Solar particle events{E} are sudden bursts from the Sun that can raise the dose dramatically for hours.

<b>Ultraviolet.</b> Mars has no ozone layer, so strong UV light reaches the surface. A cracked visor means sunburn — fast.

<b>Shielding.</b> The best shields are {H}thick layers of Martian soil (regolith), water or ice{E}. Bury or cover your shelter. Glass and thin metal foil do not stop high-energy particles.

<b>Field rule:</b> When the dosimeter alarm sounds — get inside and stay inside.",
                KeyFact = "No global magnetic field + thin air = high radiation. Shield with soil or water." },

            new Chapter {
                Title = "Cold Nights", Subtitle = "Surviving the temperature swing",
                Body =
$@"Thin air cannot hold heat. When the Sun sets, the warmth of the day escapes straight back into space.

<b>Temperatures.</b> The average on Mars is about {H}−60 °C{E}. A summer afternoon near the equator can reach {H}+20 °C{E}, but the same night drops to about {H}−80 °C{E}. In polar winter it can fall below −120 °C — cold enough for carbon dioxide to freeze into {H}dry-ice snow{E}.

<b>Survival.</b> Your suit heater needs power. Keep batteries charged during the day using solar panels, and get back to an insulated shelter before nightfall.

<b>Solar power.</b> Sunlight on Mars is only about {H}43% as strong as on Earth{E}, because Mars is farther from the Sun. Solar panels must be larger and kept free of dust.",
                KeyFact = "Day +20 °C, night −80 °C. Be inside before dark." },

            new Chapter {
                Title = "Making Oxygen: MOXIE", Subtitle = "Breathing the Martian air",
                Body =
$@"{H}MOXIE{E} — the Mars Oxygen In-Situ Resource Utilization Experiment — flew to Mars aboard NASA's {H}Perseverance rover{E}, which landed in Jezero Crater on 18 February 2021.

<b>How it works.</b> MOXIE pulls in Martian air, compresses it and heats it to about {H}800 °C{E}. A solid-oxide electrolysis cell then {H}splits carbon dioxide{E}:

<align=center><b>2 CO<sub>2</sub>  →  2 CO  +  O<sub>2</sub></b></align>

The products are {H}oxygen (O<sub>2</sub>){E} and {H}carbon monoxide (CO){E}, which is vented.

<b>Results.</b> MOXIE made oxygen on Mars for the first time in April 2021. Over 16 runs it produced {H}122 grams{E} of oxygen — at its best about 12 grams per hour, at 98% purity or better.

<b>Why it matters.</b> A bigger version could make oxygen for astronauts to breathe — and, even more importantly, for {H}rocket propellant{E}. Most of the mass a rocket burns is oxygen. Making it on Mars means not carrying it from Earth. This is called {H}ISRU{E}: In-Situ Resource Utilization — living off the land.",
                KeyFact = "MOXIE: CO<sub>2</sub> → O<sub>2</sub> + CO at ~800 °C." },

            new Chapter {
                Title = "Water & Jezero Crater", Subtitle = "Reading an ancient lake",
                Body =
$@"You crashed inside {H}Jezero Crater{E}, about 45 km wide. Around {H}3.5 billion years ago{E} it held a lake, fed by a river that built a fan-shaped {H}delta{E} of sediment — the kind of place where signs of ancient microbial life could be preserved.

<b>Perseverance</b> is exploring Jezero and collecting rock samples in sealed tubes for future return to Earth.

<b>Water today.</b> Liquid water cannot last on the surface: the pressure is so low that it quickly boils or freezes. But Mars has plenty of {H}water ice{E}: in its polar ice caps and buried just under the surface across large areas.

<b>For colonists</b>, ice is treasure: drinking water, radiation shielding, and — split into hydrogen and oxygen — fuel and air.",
                KeyFact = "Jezero was a lake ~3.5 billion years ago." },

            new Chapter {
                Title = "Orbits & Launch Windows", Subtitle = "Why you must wait",
                Body =
$@"Earth and Mars both orbit the Sun, but at different speeds. Earth completes a lap in 365 days; Mars needs {H}687 days{E}. Earth keeps catching up and lapping Mars.

<b>Distance.</b> The gap between the planets changes from about {H}55 million km{E} at the closest to about {H}400 million km{E} when they are on opposite sides of the Sun.

<b>Launch windows.</b> Rockets do not fly in a straight line. The most fuel-efficient path is a curved {H}transfer orbit{E} that touches both planets' orbits. It only works when the planets are in the right positions — and that alignment repeats about {H}every 26 months (around 780 days){E}.

<b>Travel time.</b> The trip between Earth and Mars on a transfer orbit takes about {H}7 months{E} (6–9).

<b>Your situation:</b> miss a window and you wait another 26 months. Every day of your repair plan counts.",
                KeyFact = "Launch windows open about every 26 months (~780 days)." },

            new Chapter {
                Title = "Rocket Engineering", Subtitle = "Getting off the ground",
                Body =
$@"<b>Newton's third law.</b> For every action there is an equal and opposite reaction. A rocket throws hot gas {H}down{E}, and the gas pushes the rocket {H}up{E}.

<b>Escape velocity.</b> To leave a planet you must reach its escape velocity. On Earth it is {H}11.2 km/s{E}; on Mars only about {H}5.0 km/s{E}, because Mars has less mass. Leaving Mars needs far less fuel.

<b>Fuel from air.</b> The {H}Sabatier reaction{E} combines carbon dioxide with hydrogen to make {H}methane{E} and water:

<align=center><b>CO<sub>2</sub> + 4 H<sub>2</sub>  →  CH<sub>4</sub> + 2 H<sub>2</sub>O</b></align>

Methane burned with liquid oxygen ({H}methalox{E}) is a modern rocket propellant — both parts can be made on Mars.

<b>Key parts.</b> The {H}fuel pump valve{E} controls how much propellant reaches the engine. The {H}flight computer{E} steers. The {H}star tracker{E} knows which way is up. The {H}antenna{E} talks to home.",
                KeyFact = "Escape velocity: Earth 11.2 km/s, Mars 5.0 km/s." },

            new Chapter {
                Title = "Navigation & Electronics", Subtitle = "Finding your way without GPS",
                Body =
$@"<b>No GPS.</b> There are no navigation satellites around Mars.

<b>No compass.</b> Because Mars has {H}no global magnetic field{E}, a magnetic compass will not point north.

<b>Star trackers.</b> Spacecraft use a {H}star tracker{E}: a camera that photographs the sky and recognises {H}patterns of stars{E}, like a fingerprint, to calculate exactly which way the spacecraft is pointing.

<b>Radiation and chips.</b> A single high-energy particle can strike a computer chip and {H}flip a bit{E} in memory — a 0 becomes a 1. This is a {H}single-event upset{E}, and it can crash software. Mars rovers like Curiosity and Perseverance use {H}radiation-hardened{E} computers and error-correcting memory to survive.",
                KeyFact = "Star trackers recognise star patterns. Radiation can flip bits." },

            new Chapter {
                Title = "Calling Home", Subtitle = "Communication across space",
                Body =
$@"Radio waves travel at the {H}speed of light{E} — about 300,000 km per second. Even so, Mars is so far that a message takes between about {H}3 and 22 minutes{E} to reach Earth, depending on where the planets are. A conversation is impossible: you speak, then wait.

<b>Deep Space Network.</b> NASA listens with giant antennas in {H}three places{E} spread around the globe — {H}Goldstone (California), Madrid (Spain) and Canberra (Australia){E} — so one of them always faces Mars as Earth rotates.

<b>Relay orbiters.</b> Rovers usually send data up to spacecraft orbiting Mars, which relay it to Earth. The {H}Mars Reconnaissance Orbiter (MRO){E}, MAVEN and Mars Odyssey all act as relays — MRO has carried a large share of rover data.",
                KeyFact = "Mars to Earth: 3–22 minutes at light speed." },

            new Chapter {
                Title = "Field Notes: Robots of Jezero", Subtitle = "You are not the first explorer here",
                Body =
$@"<b>Perseverance</b> is a car-sized rover that landed in Jezero Crater on {H}18 February 2021{E}. It carries cameras, a laser, a drill and the MOXIE experiment, and collects samples of rock for future return to Earth. It is powered by a {H}radioisotope generator{E} that turns the heat of decaying plutonium into electricity, so it works through dust storms and nights.

<b>Ingenuity</b> rode to Mars under Perseverance's belly. On {H}19 April 2021{E} it made the {H}first powered, controlled flight on another planet{E}. Because the air is so thin, its rotor blades spin at about 2,400 revolutions per minute. Planned for 5 flights, it flew {H}72{E}.

<b>Lesson for survivors:</b> robots and humans explore best together.",
                KeyFact = "Ingenuity: first powered flight on another planet (2021)." },
        };

        // ------------------------------------------------------------------
        public static readonly Question[] Questions =
        {
            new Question("moxie1", "Which gas makes up about 95% of the Martian atmosphere — the gas MOXIE uses to make oxygen?",
                new[]{ "Carbon dioxide (CO<sub>2</sub>)", "Oxygen (O<sub>2</sub>)", "Nitrogen (N<sub>2</sub>)", "Argon (Ar)" }, 0, 1,
                "Mars' air is ~95% CO<sub>2</sub>. MOXIE splits it to release oxygen."),
            new Question("moxie2", "MOXIE splits carbon dioxide into oxygen and which other gas?",
                new[]{ "Methane (CH<sub>4</sub>)", "Carbon monoxide (CO)", "Hydrogen (H<sub>2</sub>)", "Helium (He)" }, 1, 4,
                "2 CO<sub>2</sub> → 2 CO + O<sub>2</sub>. The carbon monoxide is vented outside."),
            new Question("valve1", "Which rocket fuel can be made on Mars from CO<sub>2</sub> and hydrogen (the Sabatier reaction)?",
                new[]{ "Kerosene", "Hydrazine", "Methane (CH<sub>4</sub>)", "Solid fuel pellets" }, 2, 7,
                "CO<sub>2</sub> + 4 H<sub>2</sub> → CH<sub>4</sub> + 2 H<sub>2</sub>O. Methane + liquid oxygen = methalox."),
            new Question("valve2", "What is the escape velocity of Mars — the speed needed to break free of its gravity?",
                new[]{ "About 11.2 km/s", "About 5.0 km/s", "About 0.5 km/s", "About 30 km/s" }, 1, 7,
                "Mars: ~5.0 km/s. Earth: 11.2 km/s. Leaving Mars takes far less fuel."),
            new Question("board1", "Why is radiation on the surface of Mars much stronger than on Earth?",
                new[]{ "Mars is closer to the Sun", "The red dust is radioactive", "No global magnetic field and a very thin atmosphere", "Phobos reflects sunlight onto Mars" }, 2, 2,
                "Earth's magnetic field and thick air block most particles. Mars has neither."),
            new Question("board2", "What can a high-energy particle do to an ordinary computer chip?",
                new[]{ "Flip a bit in memory and cause errors", "Make it run faster", "Recharge its battery", "Nothing — chips are immune" }, 0, 8,
                "A 'single-event upset' flips a 0 into a 1. Rovers use radiation-hardened computers."),
            new Question("tracker1", "How does a spacecraft star tracker work out which way it is pointing?",
                new[]{ "It reads GPS satellites", "It uses a magnetic compass", "It listens to radio from Earth", "It recognises patterns of stars" }, 3, 8,
                "Star patterns are like a fingerprint of the sky. (Compasses fail — Mars has no global magnetic field.)"),
            new Question("tracker2", "How long is one Martian day — a 'sol'?",
                new[]{ "About 10 hours", "About 24 hours 39 minutes", "About 58 Earth days", "687 Earth days" }, 1, 0,
                "A sol is 24 h 39 min — almost like Earth. 687 days is the Martian YEAR."),
            new Question("antenna1", "How long does a radio message take to travel from Mars to Earth?",
                new[]{ "It is instant", "About 1 second", "Between about 3 and 22 minutes", "About 3 days" }, 2, 9,
                "Radio travels at light speed, but Mars is 55–400 million km away."),
            new Question("antenna2", "Which NASA spacecraft orbits Mars and relays rover data back to Earth?",
                new[]{ "Mars Reconnaissance Orbiter (MRO)", "Hubble Space Telescope", "International Space Station", "Voyager 1" }, 0, 9,
                "MRO, MAVEN and Mars Odyssey relay data from the surface to the Deep Space Network."),
            new Question("launch1", "Final check: how often does an Earth–Mars launch window open?",
                new[]{ "Every 6 months", "Every 12 months", "About every 26 months", "Once every 10 years" }, 2, 6,
                "The planets line up for an efficient transfer orbit about every 26 months (~780 days)."),
        };
    }

    public class Question
    {
        public string Id, Text, Explain; public string[] Options; public int Correct, Chapter;
        public Question(string id, string text, string[] opts, int correct, int chapter, string explain)
        { Id = id; Text = text; Options = opts; Correct = correct; Chapter = chapter; Explain = explain; }
    }
}
