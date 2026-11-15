# Mission Mars: The 2-Year Window

A Junior Astronaut Mission Trainer built in Unity 6 for the **NASA Space Apps Challenge 2026**. It comes with an illustrated handbook for kids aged 7–12.

**How to use it:** kids read the *Junior Astronaut Handbook* (`Book/`) first, then practise in the game. The puzzles in the game use the same questions as the quiz in the book.

## The story
1. A Saturn V launches from Earth and crashes on Mars. The wreck stays at the crash site.
2. The player survives a **real solar flare** taken from NASA DONKI by running to the shielded bunker.
3. The player carries in **MOXIE** (to make oxygen) and an **ice-to-water machine**.
4. The player finds 3 rocket details scattered across Mars and solves a puzzle for each one. A new rocket is built on the pad from those details.
5. The player faces dust storms and meteor showers, waits for the Earth–Mars launch window (about every 26 months), and flies home.

## Real NASA data
The data sources are listed in [`DATA_SOURCES.md`](DATA_SOURCES.md) and on the in-game **NASA DATA** screen:
- DONKI (solar flares)
- Curiosity RAD (radiation dose)
- MOXIE (making oxygen)
- MRO SHARAD (underground ice)
- Opportunity (the 2018 dust storm)
- NASA 3D Resources (models)

## Project layout
- `MissionMars/`: the Unity 6000.0 project (URP). Everything is built at runtime by `Assets/_Project/Scripts`.
- `Book/`: the handbook as HTML and PDF.

## Third-party assets
Some assets used in the demo build are **not included in this repository**, because their licences don't allow sharing the original files:
- the CGTrader rocket model;
- the 8K Earth textures;
- the space panorama;
- the asteroid pack.

The game falls back to NASA models and textures when these files are missing.

The "Astronaut" model is by Jungle Jim (Sketchfab, CC-BY).

*Not an official NASA product.*
