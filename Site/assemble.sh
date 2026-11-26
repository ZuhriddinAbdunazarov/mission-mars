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
# Weak connections cut off one long download (the 44 MB game data stopped halfway), so the big files are also
# split into 4 MB pieces. game.html downloads the pieces 3 at a time, retries any piece that drops and joins them.
# Piece names carry the file's checksum, so browsers never mix pieces of two different builds.
# The whole files stay too (fallback if Build/parts.json cannot be read).
B="$OUT/game/Build"
printf '{' > "$B/parts.json"; first=1
for kind in data:Web.data.unityweb code:Web.wasm.unityweb; do
  key="${kind%%:*}"; file="${kind#*:}"
  sum="$(md5sum "$B/$file" | cut -c1-10)"; size="$(stat -c %s "$B/$file")"
  split -b 4194304 -d -a 2 "$B/$file" "$B/$file.$sum.p"
  names="$(cd "$B" && ls "$file.$sum.p"* | sed 's/.*/"&"/' | paste -sd, -)"
  [ $first = 1 ] || printf ',' >> "$B/parts.json"; first=0
  printf '"%s":{"size":%s,"parts":[%s]}' "$key" "$size" "$names" >> "$B/parts.json"
done
printf '}\n' >> "$B/parts.json"
cat "$B/parts.json"
RESEARCH="${RESEARCH:-$ROOT/../research}"
if [ -d "$RESEARCH/public" ]; then mkdir -p "$OUT/explorer"; cp -r "$RESEARCH/public/." "$OUT/explorer/"; else echo "WARNING: $RESEARCH not found: /explorer/ not included"; fi
cp "$ROOT/Site/game.html" "$OUT/game/index.html"   # full-window game page instead of Unity's 960x600 one
rm -rf "$OUT/game/TemplateData"
du -sh "$OUT" "$OUT"/*
