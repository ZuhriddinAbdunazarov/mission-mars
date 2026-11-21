#!/usr/bin/env bash
# Builds the folder that gets uploaded to Netlify (spacefrontier01.netlify.app):
#   /          Site/index.html (home page)
#   /book/     the Junior Astronaut Handbook (Book/)
#   /game/     the Unity WebGL build (Build/Web, made by "Mission Mars/4. Build WebGL")
#   /explorer/ MARS × MOON scientific data explorer (Explorer/public; API via Netlify Functions)
# Deploy with functions:  netlify deploy --prod --dir Build/Netlify --functions Explorer/netlify/functions
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/Build/Netlify"
rm -rf "$OUT"
mkdir -p "$OUT/book" "$OUT/game" "$OUT/explorer"
cp "$ROOT/Site/index.html" "$OUT/index.html"
cp -r "$ROOT/Book/." "$OUT/book/"
cp "$ROOT/Book/Junior_Astronaut_Handbook.html" "$OUT/book/index.html"
cp -r "$ROOT/Build/Web/." "$OUT/game/"
cp "$ROOT/Site/game.html" "$OUT/game/index.html"   # full-window game page instead of Unity's 960x600 one
rm -rf "$OUT/game/TemplateData"
cp -r "$ROOT/Explorer/public/." "$OUT/explorer/"
du -sh "$OUT" "$OUT"/*
