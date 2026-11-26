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

# call NASA ONCE and save the real numbers + pictures inside the game (key from ../research/.env, never committed)
node "$TOOLS/nasa-snapshot.mjs" || echo "NASA snapshot failed: keeping the numbers saved last time"

# model : max texture px : mesh ratio (1 = keep all triangles) [: allowed shape error, default 0.002]
MODELS=(
  "NASA/Mars_2020_Perseverance_Rover:512:0.2"
  "NASA/Ingenuity_Mars_Helicopter:512:0.4"
  "NASA/Saturn_V:512:1"
  "NASA/Mars_Reconnaissance_Orbiter__MRO___A_:512:0.3:0.002"
  "NASA/Astronaut:512:1"
  "NASA/Orbital_Replacement_Unit:512:1"
  "NASA/Crew_Lock_Bag:512:1"
  "Sketchfab/SF_Rocket:512:0.6"
  "Sketchfab/SF_Asteroid1:256:0.25:0.002"
  "Sketchfab/SF_Asteroid2:256:0.25:0.002"
  "Sketchfab/SF_Asteroid3:256:0.25:0.002"
  "Sketchfab/SF_Asteroid4:256:0.25:0.002"
  "Sketchfab/SF_Astronaut:1024:1"
)

# .gltf models (Codex base + paid rover; not in git) : mesh ratio : allowed shape error (0.002 = looks the same). Pictures stay as they are.
# The original .gltf/.bin are backed up and put back after the build.
ASSETS="$ROOT/MissionMars/Assets"
GLTFS=(
  "Paid/Resources/Paid/MarsBaseCrewRover:0.35:0.002"
  "_Project/Resources/MarsBase9/01_Habitat_Exterior:0.5:0.002"
  "_Project/Resources/MarsBase9/02_Weather_Lab_Exterior:0.5:0.002"
  "_Project/Resources/MarsBase9/03_Greenhouse_Exterior:0.5:0.002"
  "_Project/Resources/MarsBase9/04_Storm_Shelter_Entrance:0.5:0.002"
  "_Project/Resources/MarsBase9/06_Habitat_Interior:0.5:0.002"
  "_Project/Resources/MarsBase9/07_Weather_Lab_Interior:0.5:0.002"
  "_Project/Resources/MarsBase9/08_Greenhouse_Interior:0.5:0.002"
  "_Project/Resources/MarsBase9/09_Storm_Shelter_Interior:0.5:0.002"
)

# put everything back as it was in git: remove the split files, bring back the original .glb (+ its .meta)
restore() {
  for m in "${MODELS[@]}"; do
    f="${m%%:*}"; d="$(dirname "$RES/$f")"; n="$(basename "$f")"
    rm -f "${d:?}/${n:?}.gltf" "${d:?}/${n:?}.gltf.meta" "${d:?}/${n:?}.bin" "${d:?}/${n:?}.bin.meta" "${d:?}/${n:?}"_[0-9]*_*.jpg "${d:?}/${n:?}"_[0-9]*_*.png "${d:?}/${n:?}"_[0-9]*_*.meta
    [ -f "$BAK/live/$f.glb" ] && mv "$BAK/live/$f.glb" "$RES/$f.glb"
    [ -f "$BAK/live/$f.glb.meta" ] && mv "$BAK/live/$f.glb.meta" "$RES/$f.glb.meta"
  done
  for m in "${GLTFS[@]}"; do
    f="${m%%:*}"
    if [ -f "$BAK/gltf/$f.gltf" ]; then mv -f "$BAK/gltf/$f.gltf" "$ASSETS/$f.gltf"; mv -f "$BAK/gltf/$f.bin" "$ASSETS/$f.bin"; fi
  done
  echo "originals restored"
}
trap restore EXIT

for m in "${MODELS[@]}"; do
  IFS=: read -r f px ratio err <<< "$m"
  [ -f "$RES/$f.glb" ] || continue                       # third-party files may be missing (not in git)
  mkdir -p "$(dirname "$BAK/$f.glb")"
  [ -f "$BAK/$f.glb" ] || cp "$RES/$f.glb" "$BAK/$f.glb" # back up the original once
  # 1) lighter copy  2) split into .gltf + separate pictures (Unity compresses those; not the ones inside a .glb)
  mkdir -p "$(dirname "$BAK/web/$f.glb")" "$(dirname "$BAK/live/$f.glb")"
  NODE_OPTIONS=--max-old-space-size=8192 node "$TOOLS/optimize-model.mjs" "$(cygpath -w "$BAK/$f.glb" 2>/dev/null || echo "$BAK/$f.glb")" "$(cygpath -w "$BAK/web/$f.glb" 2>/dev/null || echo "$BAK/web/$f.glb")" "$px" "$ratio" ${err:+"$err"}
  mv "$RES/$f.glb" "$BAK/live/$f.glb"; [ -f "$RES/$f.glb.meta" ] && mv "$RES/$f.glb.meta" "$BAK/live/$f.glb.meta"
  node "$TOOLS/externalize.mjs" "$(cygpath -w "$BAK/web/$f.glb" 2>/dev/null || echo "$BAK/web/$f.glb")" "$(cygpath -w "$(dirname "$RES/$f")" 2>/dev/null || echo "$(dirname "$RES/$f")")" "$(basename "$f")"
done

for m in "${GLTFS[@]}"; do
  IFS=: read -r f ratio err <<< "$m"
  src="$ASSETS/$f"; n="$(basename "$f")"
  [ -f "$src.gltf" ] || continue                         # paid / Codex files may be missing (not in git)
  mkdir -p "$(dirname "$BAK/gltf/$f")"
  if [ -f "$BAK/gltf/$f.gltf" ]; then cp -f "$BAK/gltf/$f.gltf" "$src.gltf"; cp -f "$BAK/gltf/$f.bin" "$src.bin"   # an earlier run stopped: start from the original
  else cp "$src.gltf" "$BAK/gltf/$f.gltf"; cp "$src.bin" "$BAK/gltf/$f.bin"; fi
  tmp="$BAK/gltf_tmp/$n"; rm -rf "$tmp"; mkdir -p "$tmp"
  NODE_OPTIONS=--max-old-space-size=8192 node "$TOOLS/optimize-model.mjs" "$(cygpath -w "$src.gltf" 2>/dev/null || echo "$src.gltf")" "$(cygpath -w "$tmp/$n.gltf" 2>/dev/null || echo "$tmp/$n.gltf")" 99999 "$ratio" "$err"
  cp -f "$tmp/$n.gltf" "$src.gltf"; cp -f "$tmp/$n.bin" "$src.bin"; rm -rf "$tmp"
done

rm -rf "$ROOT/Build/Web"
LOG="$ROOT/Build/webgl_build.log"
"$UNITY" -batchmode -nographics -projectPath "$(cygpath -w "$ROOT/MissionMars" 2>/dev/null || echo "$ROOT/MissionMars")" \
  -buildTarget WebGL -executeMethod MissionMars.EditorTools.ProjectSetup.BuildWeb -logFile "$(cygpath -w "$LOG" 2>/dev/null || echo "$LOG")"
grep -E 'BuildWeb\]|error CS' "$LOG" || true

bash "$ROOT/Site/assemble.sh"
