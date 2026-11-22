#!/usr/bin/env bash
# Builds the folder that gets uploaded to Netlify (spacefrontier01.netlify.app):
#   /          Site/index.html (home page)
#   /book/     the Junior Astronaut Handbook (Book/)
#   /game/     the Unity WebGL build (Build/Web, made by "Mission Mars/4. Build WebGL")
#   /explorer/ MARS & MOON research site (github.com/ZuhriddinAbdunazarov/research, cloned next to this repo)
# Upload with its REST API functions:
#   netlify deploy --dir Build/Netlify --functions ../research/netlify/functions
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/Build/Netlify"
rm -rf "$OUT"
mkdir -p "$OUT/book" "$OUT/game"
cp "$ROOT/Site/index.html" "$OUT/index.html"
cp "$ROOT/Site/_headers" "$OUT/_headers"
cp -r "$ROOT/Site/img" "$OUT/img"
cp -r "$ROOT/Book/." "$OUT/book/"
cp "$ROOT/Book/Junior_Astronaut_Handbook.html" "$OUT/book/index.html"
cp -r "$ROOT/Build/Web/." "$OUT/game/"
RESEARCH="${RESEARCH:-$ROOT/../research}"
if [ -d "$RESEARCH/public" ]; then mkdir -p "$OUT/explorer"; cp -r "$RESEARCH/public/." "$OUT/explorer/"; else echo "WARNING: $RESEARCH not found: /explorer/ not included"; fi
cp "$ROOT/Site/game.html" "$OUT/game/index.html"   # full-window game page instead of Unity's 960x600 one
rm -rf "$OUT/game/TemplateData"
du -sh "$OUT" "$OUT"/*
