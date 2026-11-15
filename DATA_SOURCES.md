# Mission Mars: The 2-Year Window — NASA Data Sources

**Challenge:** Build a Junior Astronaut Mission Trainer (NASA Space Apps Challenge 2026)
**Audience:** kids aged 7–12

**Story:** Your ship crashes on Mars. You survive a real solar storm, build up air and water, chase the rocket parts and solve puzzles, then fly home when Earth and Mars line up again.

**Core resources:** Oxygen, Water, and Health / Radiation. Every trip outside costs oxygen. Every solar flare raises the same question: run to the shelter, or push on and take damage?

| Game mechanic | NASA dataset / mission | How it is used | Why |
|---|---|---|---|
| **Solar storms** | **DONKI** (Space Weather Database), `api.nasa.gov/DONKI/FLR` | Each storm in the game is a **real** M- or X-class solar flare. When the game is online, it uses live flares from the last 30 days. When it is offline, it uses an archived list of real flares (for example X9.0 on 3 Oct 2024). The flare's class sets how strong the storm is. The game always says whether the data is "LIVE" or "archived". | It links the player's shelter decision to real space weather. |
| **Radiation meter** | **Curiosity rover RAD** | The meter uses about 0.67 mSv per sol at the surface. The soil-covered bunker blocks almost all of it. | It shows why Mars needs shielding: Mars has no magnetic field and very thin air. |
| **Oxygen maker** | **Perseverance MOXIE** | The player brings MOXIE to the bunker. It turns CO2 into O2, so the player's oxygen refills inside. | Making oxygen from Mars air is real NASA technology. |
| **Water machine** | **MRO SHARAD radar** (ice under the ground) | The ice-to-water machine melts ice from under the ground so the player can drink. | Water is the key resource for living on Mars. |
| **2-year timer** | Mars–Earth launch windows (about every 26 months) | A countdown above the base shows when the trip home becomes possible. | It explains why astronauts can't fly home whenever they want. |
| **Puzzles** | NASA Mars facts | Mars air is 95% CO2. A sol is 24 h 39 min. Radio takes 3–22 minutes to reach Earth. Mars gravity is 38% of Earth's. | Short facts in plain language, used to unlock the rocket parts. |
| **Dust storm** | **Opportunity rover** (2018 global dust storm) | A brown haze cuts visibility to a few metres, the sun dims and the suit uses more oxygen. | It shows how dangerous Mars dust storms are. |
| **Meteor shower** | **MRO / InSight** (new impact craters found every year) | Meteors fall with red warning circles, and each one leaves a small crater. | Mars has thin air, so more space rocks reach the ground. |
| **3D models** | **NASA 3D Resources** | Saturn V, Perseverance, Ingenuity, MRO, Redstone and others. | Real NASA hardware. |

**Replayability:** the storms are real flares picked at random from the pool, so they change between runs.
**Failure is never final:** if oxygen, water or health runs out, NOVA the robot carries the player back to the bunker and gives a tip. No progress is lost.

**Credits:** "Astronaut" by Jungle Jim (Sketchfab, CC-BY). 8K Earth textures by khanhnamn063 (CGTrader, free). Free rocket model and 360° space panorama (CGTrader, free; add the authors before submitting). Saturn V and the other hardware: NASA 3D Resources. Ground textures: Poly Haven (CC0). Fonts: Google Fonts (OFL).
*Not an official NASA product.*
