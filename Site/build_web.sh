#!/usr/bin/env bash
# One command to make the web version that goes on Netlify:
#   bash Site/build_web.sh
# 1. makes lighter, web-only copies of the big 3D models (originals are backed up and put back afterwards)
# 2. builds the Unity WebGL player (Mission Mars/4. Build WebGL)
# 3. assembles Build/Netlify (home page + game + book), ready for: netlify deploy --dir Build/Netlify
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-/c/Program Files/Unity/Hub/Editor/6000.0.85f2/Editor/Unity.exe}"
RES="$ROOT/MissionMars/Assets/_Project/Resources"
BAK="$ROOT/Build/_orig_models"
TOOLS="$ROOT/Site/tools"

[ -d "$TOOLS/node_modules" ] || (cd "$TOOLS" && npm install --silent)

# model : max texture px : mesh ratio (1 = keep all triangles)
MODELS=(
  "NASA/Mars_2020_Perseverance_Rover:512:0.3"
  "NASA/Ingenuity_Mars_Helicopter:512:0.5"
  "NASA/Saturn_V:512:1"
  "NASA/Mars_Reconnaissance_Orbiter__MRO___A_:512:1"
  "NASA/Astronaut:512:1"
  "NASA/Orbital_Replacement_Unit:512:1"
  "NASA/Crew_Lock_Bag:512:1"
  "Sketchfab/SF_Rocket:512:0.6"
  "Sketchfab/SF_Asteroid1:512:1"
  "Sketchfab/SF_Asteroid2:512:1"
  "Sketchfab/SF_Asteroid3:512:1"
  "Sketchfab/SF_Asteroid4:512:1"
  "Sketchfab/SF_Astronaut:1024:1"
)

restore() { for m in "${MODELS[@]}"; do f="${m%%:*}"; [ -f "$BAK/$f.glb" ] && cp "$BAK/$f.glb" "$RES/$f.glb"; done; echo "originals restored"; }
trap restore EXIT

for m in "${MODELS[@]}"; do
  IFS=: read -r f px ratio <<< "$m"
  [ -f "$RES/$f.glb" ] || continue                       # third-party files may be missing (not in git)
  mkdir -p "$(dirname "$BAK/$f.glb")"
  [ -f "$BAK/$f.glb" ] || cp "$RES/$f.glb" "$BAK/$f.glb" # back up the original once
  NODE_OPTIONS=--max-old-space-size=8192 node "$TOOLS/optimize-model.mjs" "$(cygpath -w "$BAK/$f.glb" 2>/dev/null || echo "$BAK/$f.glb")" "$(cygpath -w "$RES/$f.glb" 2>/dev/null || echo "$RES/$f.glb")" "$px" "$ratio"
done

rm -rf "$ROOT/Build/Web"
LOG="$ROOT/Build/webgl_build.log"
"$UNITY" -batchmode -nographics -projectPath "$(cygpath -w "$ROOT/MissionMars" 2>/dev/null || echo "$ROOT/MissionMars")" \
  -buildTarget WebGL -executeMethod MissionMars.EditorTools.ProjectSetup.BuildWeb -logFile "$(cygpath -w "$LOG" 2>/dev/null || echo "$LOG")"
grep -E 'BuildWeb\]|error CS' "$LOG" || true

bash "$ROOT/Site/assemble.sh"
